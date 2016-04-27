using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace StrategyPool
{
    /// <summary>
    /// 数据库速度提取的函数。
    /// </summary>
    class DataApplication
    {
        public string connectionString;
        public string dataBase;

        /// <summary>
        /// 构造函数。获取数据库以及SQL连接字符串。
        /// </summary>
        /// <param name="dataBase">数据库名称</param>
        /// <param name="connectionString">连接字符串</param>
        public DataApplication(string dataBase, string connectionString)
        {
            this.connectionString = connectionString;
            this.dataBase = dataBase;
        }

        //public Dictionary<int,double> GetETFRealizedVolatility(DataTable data,int period)
        //{
            
        //}

        /// <summary>
        /// 读取50etf前收盘数据的函数
        /// </summary>
        /// <param name="tableName">50etf表</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns></returns>
        public DataTable GetETFPreClosePrice(string tableName,int startDate=0,int endDate=0)
        {
            DataTable myDataTable = new DataTable();
            string commandString;
            if (startDate == 0)
            {
                commandString = "select distinct [PreClose],[date] from " + tableName+" order by [Date]";
            }
            else
            {
                if (endDate == 0)
                {
                    endDate = startDate;
                }
                endDate = TradeDays.GetNextTradeDay(endDate);
                commandString = "select distinct [PreClose],[date] from " + tableName + "  where [Date]>=" + startDate.ToString() + " and [Date]<=" + endDate.ToString()+ " order by [Date]";
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = commandString;
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(myDataTable);
                        }
                    }
                }

            }
            catch (Exception)
            {
                throw;
            }
            return myDataTable;
        }

        /// <summary>
        /// 根据给定的表和日期获取数据内容。
        /// </summary>
        /// <param name="tableName">表的名称</param>
        /// <param name="startDate">开始时间</param>
        /// <param name="endDate">结束时间</param>
        /// <returns>DataTable格式的数据</returns>
        public DataTable GetDataTable(string tableName, int startDate=0, int endDate = 0)
        {
            DataTable myDataTable = new DataTable();
            string commandString;
            if (startDate==0)
            {
                commandString = "select * from " + tableName;
            }
            else
            {
                if (endDate == 0)
                {
                    endDate = startDate;
                }
                commandString = "select * from " + tableName + " where [Date]>=" + startDate.ToString() + " and [Date]<=" + endDate.ToString();
            }
            
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = commandString;
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(myDataTable);
                        }
                    }
                }

            }
            catch (Exception)
            {
                throw;
            }
            return myDataTable;
        }

        /// <summary>
        /// 根据期权的数据整理得到链表。
        /// </summary>
        /// <param name="data">DataTable格式的数据源</param>
        /// <returns>List格式的处理后的数据</returns>
        public List<optionFormat> GetOptionList(DataTable data)
        {
            List<optionFormat> optionList = new List<optionFormat>();
            int lastTime = 0;
            foreach (DataRow row in data.Rows)
            {
                optionFormat option = new optionFormat();
                option.ask = new optionPriceWithGreek[5];
                option.bid = new optionPriceWithGreek[5];
                option.code = (int)row["Code"];
                option.type = row["OptionType"].ToString().Trim();
                option.strike = (double)row["Strike"];
                option.startDate = (int)row["StartDate"];
                option.endDate = (int)row["EndDate"];
                option.date = (int)row["Date"];
                //剔除非交易时间的交易数据。
                int now = (int)row["Time"] + (int)row["Tick"] * 500;
                if (now<93000000 || (now>113000000 && now<130000000) || (now>150000000) || (int)row["Tick"]>=2)
                {
                    continue;
                }
                if (now<=lastTime)
                {
                    continue; 
                }
                option.time = now;
                lastTime = now;
                option.lastPrice = (double)row["LastPrice"];
                for (int i = 1; i <= 5; i++)
                {
                    option.ask[i - 1] = new optionPriceWithGreek((double)row["Ask" + i.ToString()], (double)row["Askv" + i.ToString()], (double)row["Ask" + i.ToString() + "Volatility"], (double)row["Ask" + i.ToString() + "Delta"]);
                    option.bid[i - 1] = new optionPriceWithGreek((double)row["Bid" + i.ToString()], (double)row["Bidv" + i.ToString()], (double)row["Bid" + i.ToString() + "Volatility"], (double)row["Bid" + i.ToString() + "Delta"]);
                }
                option.openMargin = (double)row["OpenMargin"];
                option.preClose = (double)row["PreClose"];
                option.preSettle = (double)row["Presettle"];
                option.midDelta = (double)row["MidDelta"];
                option.midVolatility = (double)row["MidVolatility"];
                optionList.Add(option);
            }
            return optionList;
        }

        /// <summary>
        /// 根据期权合约信息的数据整理得到链表。
        /// </summary>
        /// <param name="data">DataTable格式的数据源</param>
        /// <returns>SortedDictionary格式的处理后的数据</returns>
        public SortedDictionary<int,optionInfo> GetOptionInfoList(DataTable data)
        {
            SortedDictionary<int, optionInfo> myList = new SortedDictionary<int, optionInfo>();
            foreach (DataRow row in data.Rows)
            {
                optionInfo contract = new optionInfo();
                contract.optionCode = (int)row["OptionCode"];
                contract.optionName = row["OptionName"].ToString().Trim();
                contract.executeType = row["ExecuteType"].ToString().Trim();
                contract.strike = (double)row["Strike"];
                contract.optionType = row["OptionType"].ToString().Trim();
                contract.startDate = (int)row["StartDate"];
                contract.endDate = (int)row["EndDate"];
                contract.market = row["Market"].ToString().Trim();
                myList.Add(contract.optionCode, contract);
            }
            return myList;
        }

        /// <summary>
        /// 根据股票数据或者期货数据整理得到的链表
        /// </summary>
        /// <param name="data">DataTable格式的股票或者期货数据</param>
        /// <returns>List格式的数据</returns>
        public List<stockFormat> GetStockList(DataTable data)
        {
            List<stockFormat> stockList = new List<stockFormat>();
            int lastTime = 0;
            foreach (DataRow row in data.Rows)
            {
                stockFormat stock = new stockFormat();
                stock.ask = new stockPrice[5];
                stock.bid = new stockPrice[5];
                stock.code = (int)row["Code"];
                stock.date = (int)row["Date"];
                //剔除非交易时间的交易数据。
                int now = (int)row["Time"] + (int)row["Tick"] * 500;
                if (now < 93000000 || (now > 113000000 && now < 130000000) || (now > 150000000) || (int)row["Tick"] >= 2)
                {
                    continue;
                }
                if (now <= lastTime)
                {
                    continue;
                }
                stock.time = now;
                lastTime = now;
                stock.lastPrice = (double)row["LastPrice"];
                for (int i = 1; i <= 5; i++)
                {
                    stock.ask[i - 1] = new stockPrice((double)row["Ask" + i.ToString()], (double)row["Askv" + i.ToString()]);
                    stock.bid[i - 1] = new stockPrice((double)row["Bid" + i.ToString()], (double)row["Bidv" + i.ToString()]);
                }
                stock.preClose = (double)row["PreClose"];
                stockList.Add(stock);
            }
            return stockList;
        }

        /// <summary>
        /// 根据股票数据或者期货数据整理得到的数组
        /// </summary>
        /// <param name="data">DataTable格式的股票或者期货数组</param>
        /// <returns>List格式的数据</returns>
        public stockFormat[] GetStockArray(DataTable data)
        {
            stockFormat[] stockArray = new stockFormat[28802];
            int lastTime = 0;
            foreach (DataRow row in data.Rows)
            {
                stockFormat stock = new stockFormat();
                stock.ask = new stockPrice[5];
                stock.bid = new stockPrice[5];
                stock.code = (int)row["Code"];
                stock.date = (int)row["Date"];
                //剔除非交易时间的交易数据。
                int now = (int)row["Time"] + (int)row["Tick"] * 500;
                if (now < 93000000 || (now > 113000000 && now < 130000000) || (now > 150000000) || (int)row["Tick"] >= 2)
                {
                    continue;
                }
                if (now <= lastTime)
                {
                    continue;
                }
                stock.time = now;
                lastTime = now;
                stock.lastPrice = (double)row["LastPrice"];
                for (int i = 1; i <= 5; i++)
                {
                    stock.ask[i - 1] = new stockPrice((double)row["Ask" + i.ToString()], (double)row["Askv" + i.ToString()]);
                    stock.bid[i - 1] = new stockPrice((double)row["Bid" + i.ToString()], (double)row["Bidv" + i.ToString()]);
                }
                stock.preClose = (double)row["PreClose"];
                stockArray[TradeDays.TimeToIndex(now)] = stock;
            }
            return stockArray;
        }

        /// <summary>
        /// 获取最大的成交价
        /// </summary>
        /// <param name="myArray">交易数据</param>
        /// <returns>最大成交价</returns>
        public double GetArrayMaxLastPrice(stockFormat[] myArray)
        {
            double maxPrice = 0;
            foreach (var item in myArray)
            {
                maxPrice = (maxPrice > item.lastPrice) ? maxPrice : item.lastPrice;
            }
            return maxPrice;
        }

        /// <summary>
        /// 获取最小成交价
        /// </summary>
        /// <param name="myArray">交易数据</param>
        /// <returns>最小成交价</returns>
        public double GetArrayMinLastPrice(stockFormat[] myArray)
        {
            double minPrice = 999;
            foreach (var item in myArray)
            {
                if (item.lastPrice>0)
                {
                    minPrice = (minPrice < item.lastPrice ) ? minPrice : item.lastPrice;
                }
            }
            return minPrice;
        }
    }
}
