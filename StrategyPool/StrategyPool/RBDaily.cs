using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace StrategyPool
{
    class RBDaily
    {
        private TradeDays myTradeDays;
        private Dictionary<string, string> mainContract;
        private int startDate,endDate;
        private string recordTableName;
        private string recordCSV;
        private string connectionString = Configuration.connectionString218;

        /// <summary>
        /// 构造函数。存储基本的信息。
        /// </summary>
        /// <param name="initialCapital">初始资金</param>
        /// <param name="startDate">开始时间</param>
        /// <param name="endDate">结束时间</param>
        /// <param name="recordStr">记录字符</param>
        public RBDaily(double initialCapital,int startDate,int endDate,string recordStr)
        {
            if (endDate<startDate)
            {
                endDate = startDate;
            }
            this.startDate = startDate;
            this.endDate = endDate;
            myTradeDays = new TradeDays(startDate, endDate);
            recordTableName = recordStr + DateTime.Now.ToString("yyyyMMddhhmm");
            recordCSV = recordStr + DateTime.Now.ToString("yyyyMMddhhmm") + ".csv";
            mainContract = GetMainContract("RB", "SHF", startDate, endDate);
            //DocumentApplication.RecordCsv(recordCSV, "日期", "总资金", "可用资金", "期权保证金", "期货保证金", "期权现值", "总金额Delta", "期权金额Delta", "期货金额Delta", "日内开仓量", "当日持仓量");
            //判断主力合约的函数

        }

        /// <summary>
        /// 给出回测时期内数据库对应的主力合约表。
        /// </summary>
        /// <param name="contract">品种</param>
        /// <param name="market">市场</param>
        /// <param name="startDate">开始时间</param>
        /// <param name="endDate">结束时间</param>
        /// <returns>主力合约表</returns>
        private Dictionary<string, string> GetMainContract(string contract,string market,int startDate,int endDate)
        {
            Dictionary<string, string> mainContract = new Dictionary<string, string>();
            List<int> monthList = new List<int>();
            for (int year = startDate/10000; year <=endDate/10000; year++)
            {
                for (int month = 1; month <= 12; month++)
                {
                    int thisMonth = year * 100 + month;
                    if (thisMonth>=startDate/100 && thisMonth<=endDate/100)
                    {
                        monthList.Add(thisMonth);
                    }
                }
            }
            foreach  (int month in monthList)
            {
                int max = 0;
                string maxTable="";
                string eachBase = "TradeMarket" + month.ToString();
                for (int i = 0; i <= 12; i++)
                {
                    int eachMonth = (month / 100 + (month % 100 - 1 + i) / 12) * 100 + (month % 100 - 1 + i) % 12 + 1;
                    string eachTable = "MarketData_" + contract + (eachMonth%10000).ToString() + "_" + market;
                    int num = CountNumber(eachBase, eachTable);
                    if (max<num)
                    {
                        maxTable = eachTable;
                        max = num;
                    }
                }
                mainContract.Add(eachBase, maxTable);

            }
            return mainContract;
        }

        /// <summary>
        /// 分析动量策略的函数。
        /// </summary>
        public void MomentumAnalysis()
        {
            double totalPnL = 0;
            for (int dateIndex = 0; dateIndex < myTradeDays.myTradeDay.Count; dateIndex++)
            {
                int today = myTradeDays.myTradeDay[dateIndex];
                string myBase = "TradeMarket" + (today / 100).ToString();
                DataTable myData = GetRBData(myBase,mainContract[myBase],today);
                List<RBFormat> rb = GetRBList(myData);
                RBStatus[] rbArr = RBList2Array(rb);
                double pnl = MomentumDaily(rbArr);
                totalPnL += pnl;
                Console.WriteLine("Date: {0}, P&L: {1}, total: {2}", today, pnl, totalPnL);
            }
        }

        /// <summary>
        /// 逐日分析趋势策略的函数。
        /// </summary>
        /// <param name="rb">螺纹钢tick数据</param>
        /// <returns>当日P&L</returns>
        private double MomentumDaily(RBStatus[] rb)
        {
            double pnl = 0;
            double signal = 0;
            double lambda = 0.1;
            double position = 0;
            double openPrice = 0;
            double[] signalArr = new double[rb.Count() ];
            for (int i = 1; i < rb.Count(); i++)
            {

                signal = signal * (1 - lambda) + (rb[i].avgPrice - rb[i - 1].avgPrice) *Math.Log(rb[i].deltaVolume+0.0000001)*lambda;
                signalArr[i ] = signal;
               // Console.WriteLine(signal);
                //if (Math.Abs(signal)>5 && i>=2*60*10)
            }
            for (int i = 2*60*10; i < rb.Count()-2*60*10; i++)
            {
                if (signalArr[i]>0.3 && position==0)
                {
                    position = 1;
                    openPrice = rb[i].ask;
                   // Console.WriteLine("price: {0}, signal: {1}, position: {2}", openPrice, signalArr[i],position);
                }
                else if (signalArr[i]<-0.3 && position==0)
                {
                    position = -1;
                    openPrice = rb[i].bid;
                   // Console.WriteLine("price: {0}, signal: {1}, position: {2}", openPrice, signalArr[i], position);
                }
                if (position==1 && ((rb[i].bid-openPrice)>10 || ((rb[i].bid - openPrice)<-5 && signalArr[i]<0)))
                {
                    pnl += rb[i].bid - openPrice;
                    position = 0;
                    openPrice = 0;
                   // Console.WriteLine("price: {0}, signal: {1}, position: {2}", rb[i].bid, signalArr[i], position);
                }
                else if (position==-1 && ((rb[i].ask-openPrice)<-10 || ((rb[i].ask - openPrice) > 5 && signalArr[i] > 0)))
                {
                    pnl -= rb[i].ask - openPrice;
                    position = 0;
                    openPrice = 0;
                  //  Console.WriteLine("price: {0}, signal: {1}, position: {2}", rb[i].ask, signalArr[i], position);
                }

            }

            return pnl;
        }


        /// <summary>
        /// 获取原始数据的函数。
        /// </summary>
        /// <param name="dataBase">数据库</param>
        /// <param name="tableName">表</param>
        /// <param name="date">日期</param>
        /// <returns>数据表</returns>
        private DataTable GetRBData(string dataBase,string tableName,int date)
        {
            DataApplication RBData = new DataApplication(dataBase, connectionString);
            return RBData.GetCommodityDataTable(tableName,date, date);
        }

        /// <summary>
        /// 根据数据库名称和表名，获取对应的数据数量
        /// </summary>
        /// <param name="dataBase">数据库</param>
        /// <param name="tableName">表</param>
        /// <returns>条目个数</returns>
        private int CountNumber(string dataBase,string tableName)
        {
            DataApplication RBData = new DataApplication(dataBase, connectionString);
            return RBData.CountNumber(tableName);
        }

        /// <summary>
        /// 将时间转化为下标的函数。螺纹钢时间为9：00-11：30，1：30-3：00以及21：00-23：00。
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private int time2index(int time)
        {
            int index = 0;

            return index;
        }


        /// <summary>
        /// 获取盘口状态数组的函数。
        /// </summary>
        /// <param name="list">列表形式的数据</param>
        /// <returns>数组形式的数据</returns>
        private RBStatus[] RBList2Array(List<RBFormat> list)
        {
            RBStatus[] RBArr = new RBStatus[list.Count-1];
            for (int i = 1; i < list.Count; i++)
            {
                RBArr[i-1].ask = list[i].ask;
                RBArr[i-1].askv = list[i].askv;
                RBArr[i-1].bid = list[i].bid;
                RBArr[i-1].bidv = list[i].bidv;
                RBArr[i - 1].lastPrice = list[i].lastPrice;
                RBArr[i - 1].date = list[i].naturalDate;
                RBArr[i - 1].time = list[i].tradeTime;

            }

            
            for (int i = 1; i < list.Count; i++)
            {
                RBArr[i-1].deltaOpenInterest = list[i].openInterest - list[i - 1].openInterest;
                RBArr[i-1].deltaTurnover = list[i].turnover - list[i-1].turnover;
                RBArr[i - 1].deltaVolume = list[i].volume - list[i-1].volume;
                if (RBArr[i-1].deltaVolume>0)
                {
                    RBArr[i - 1].avgPrice = RBArr[i - 1].deltaTurnover / RBArr[i - 1].deltaVolume/10;
                }
                else
                {
                    if (i>=2)
                    {
                        RBArr[i - 1].avgPrice = RBArr[i - 2].avgPrice;
                    }
                    if (RBArr[i-1].avgPrice==0)
                    {
                        RBArr[i - 1].avgPrice = list[i].lastPrice;
                    }
                }
            }
            return RBArr;
        }
        
        /// <summary>
        /// 将datatable的数据转化为list形式的数据。
        /// </summary>
        /// <param name="data">datatable数据</param>
        /// <returns>list数据</returns>
        private List<RBFormat> GetRBList(DataTable data)
        {
            List<RBFormat> list = new List<RBFormat>();
            foreach (DataRow row in data.Rows)
            {
                RBFormat rb = new RBFormat();
                rb.ask =Convert.ToDouble(row["S1"]);
                rb.askv = Convert.ToDouble(row["SV1"]);
                rb.bid = Convert.ToDouble(row["B1"]);
                rb.bidv = Convert.ToDouble(row["BV1"]);
                rb.lastPrice = Convert.ToDouble(row["cp"]);
                rb.highPrice = Convert.ToDouble(row["hp"]);
                rb.lowPrice = Convert.ToDouble(row["lp"]);
                rb.tradeDate =Convert.ToInt32(row["tdate"]);
                rb.naturalDate = Convert.ToInt32(row["ndate"]);
                rb.tradeTime = Convert.ToInt32(row["ttime"]);
                rb.code =Convert.ToString(row["stkcd"]);
                rb.volume = Convert.ToDouble(row["ts"]);
                rb.turnover = Convert.ToDouble(row["tt"]);
                rb.preClose = Convert.ToDouble(row["PRECLOSE"]);
                rb.preSettle = Convert.ToDouble(row["PrevSettle"]);
                rb.openInterest = Convert.ToDouble(row["OpenInterest"]);
                rb.preOpenInterest = Convert.ToDouble(row["PreOpenInterest"]);
                rb.tradeStatus = Convert.ToInt32(row["TradeStatus"]);
                list.Add(rb);
            }
            return list;
        }
    }
}
