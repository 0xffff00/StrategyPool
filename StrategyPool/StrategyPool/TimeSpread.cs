using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace StrategyPool
{
    class TimeSpread
    {
        private TradeDays myTradeDays;
        private OptionCodeInformation myOptionInfo;
        private double feePerUnit;
        private int startDate, endDate;
        private HoldStatus myHoldStatus;
        private DataApplication myData;
        private double initialCapital;
        private string recordTableName;
        private string recordCSV;
        private double initialCash;
        private List<double> netValue = new List<double>();
        private string remark = "";

        /// <summary>
        /// 构造函数。初始化各类回测的信息。
        /// </summary>
        /// <param name="initialCapital">初始资金</param>
        /// <param name="startDate">开始时间</param>
        /// <param name="endDate">结束时间</param>
        public TimeSpread(double initialCapital,int startDate,int endDate,string recordStr)
        {
            initialCash = initialCapital;
            myHoldStatus = new HoldStatus(initialCapital);
            this.initialCapital = initialCapital;
            this.startDate = startDate;
            this.endDate = endDate;
            myTradeDays = new TradeDays(startDate, endDate);
            myOptionInfo = new OptionCodeInformation(Configuration.dataBaseName, Configuration.optionCodeTableName, Configuration.connectionString);
            feePerUnit = Configuration.optionFeePerUnit;
            myData = new DataApplication(Configuration.dataBaseName, Configuration.connectionString);
            recordTableName = recordStr + DateTime.Now.ToString("yyyyMMddhhmm");
            recordCSV=recordStr+ DateTime.Now.ToString("yyyyMMddhhmm")+".csv";
            DocumentApplication.RecordCsv(recordCSV, "日期", "总资金", "可用资金", "期权保证金", "期货保证金", "期权现值", "总金额Delta", "期权金额Delta", "期货金额Delta", "日内开仓量", "当日持仓量");
        }


        /// <summary>
        /// 核心函数。跨期价差的分析函数。
        /// </summary>
        public void TimeSpreadAnalysis()
        {
            double totalVolume = 0;
            double dailyVolume = 0;
            double lastDelta = 0;
            //逐步遍历交易日期，逐日进行回测。
            for (int dateIndex = 0; dateIndex < myTradeDays.myTradeDay.Count; dateIndex++)
            {
                int today = myTradeDays.myTradeDay[dateIndex];
                dailyVolume = 0;
                //从状态的类中取出当前的持仓情况。
                Dictionary<int, optionHold> myHold = myHoldStatus.cashNow.optionList;
                
                //初始化各类信息，包括记录每个合约具体的盘口价格，具体的盘口价格变动，参与交易之后具体的盘口状态等。
                Dictionary<int, optionPositionChange[]> myShotChange = new Dictionary<int, optionPositionChange[]>(); //盘口价格变化的列表
                Dictionary<int, optionPositionChange> myShotChangeTick = new Dictionary<int, optionPositionChange>();//当前tick下盘口价格变动的
                Dictionary<int, optionFormat> myShot = new Dictionary<int, optionFormat>();//当前tick盘口价格的状态
                Dictionary<int, List<optionTradeRecord>> recordList = new Dictionary<int, List<optionTradeRecord>>();//记录今日期权交易信息               
                //记录IH和50etf的盘口价格。
                stockFormat[] IH = myData.GetStockArray(myData.GetDataTable(GetNextIHFuture(today), today));
                stockFormat[] ETF = myData.GetStockArray(myData.GetDataTable(Configuration.tableOf50ETF, today));


                //第一步，选取今日应当关注的合约代码，包括平价附近的期权合约以及昨日遗留下来的持仓。其中，平价附近的期权合约必须满足交易日的需求，昨日遗留下来的持仓必须全部囊括。
                //注意，某些合约既要进行开仓判断又要进行平仓判断。
                List<timeSpreadPair> optionPairList = GetSpreadPair(myHold,ETF, today,3,18);
                

                
                //第二步，记录对应期权合约的盘口价格变动的信息。
                foreach (var pair in optionPairList)
                {
                    //获取当月合约数据并整理出tick之间的变动信息。
                    List<optionFormat> optionList = myData.GetOptionList(myData.GetDataTable("sh" + pair.frontCode.ToString(), today));
                    PositionApplication myPosApp = new PositionApplication(optionList);
                    optionFormat frontShotStart = new optionFormat();
                    optionPositionChange[] changeList = myPosApp.GetPositionChange();
                    myShotChange.Add(pair.frontCode, changeList);
                    frontShotStart.openMargin = optionList[0].openMargin;
                    frontShotStart.type = optionList[0].type;
                    frontShotStart.strike = optionList[0].strike;
                    frontShotStart.endDate = optionList[0].endDate;
                    frontShotStart.code = optionList[0].code;
                    frontShotStart.midDelta = optionList[0].midDelta;
                    frontShotStart.midVolatility = optionList[0].midVolatility;
                    myShot.Add(pair.frontCode, frontShotStart);
                    
                    //获取下月合约的数据并整理出tick之间的变动信息。
                    List<optionFormat> optionNextList = myData.GetOptionList(myData.GetDataTable("sh" + pair.nextCode.ToString(), today));
                    PositionApplication myPosAppNext = new PositionApplication(optionNextList);
                    optionPositionChange[] changeNextList = myPosAppNext.GetPositionChange();
                    myShotChange.Add(pair.nextCode, changeNextList);
                    optionFormat nextShotStart = new optionFormat();
                    nextShotStart.openMargin = optionNextList[0].openMargin;
                    nextShotStart.type = optionNextList[0].type;
                    nextShotStart.strike = optionNextList[0].strike;
                    nextShotStart.endDate = optionNextList[0].endDate;
                    nextShotStart.code = optionNextList[0].code;
                    nextShotStart.midDelta = optionNextList[0].midDelta;
                    nextShotStart.midVolatility = optionNextList[0].midVolatility;
                    myShot.Add(pair.nextCode, nextShotStart);
                }
                //第三步，在开仓之前根据昨日的持仓情况，对今日资金情况进行盘点。用昨日收盘价结算来近似。
                double yesterMargin = GetOptionMargin(myHold, myShot);
                myHoldStatus.cashNow.availableFunds += myHoldStatus.cashNow.optionMargin - yesterMargin;
                myHoldStatus.cashNow.optionMargin = yesterMargin;

                //第四步，按照tick的下标进行遍历。对开平仓的机会进行判断。
                stockFormat IHNow = new stockFormat();
                stockFormat ETFNow = new stockFormat();
                double duration = 0;
                double durationFurther = 0;
                double durationNow = 0;
                if (optionPairList.Count>0)
                {
                    duration = TradeDays.GetTimeSpan(today, myShot[optionPairList[0].frontCode].endDate);
                    durationFurther=TradeDays.GetTimeSpan(today, myShot[optionPairList[0].nextCode].endDate);
                }
                for (int tickIndex = 0; tickIndex < 28802; tickIndex++)
                {
                    int time = TradeDays.IndexToTime(tickIndex);
                    durationNow = duration + (28801 - tickIndex) / 28801.0;
                    //根据tick的变动整理出当前的盘口价格
                    foreach (var pair in optionPairList)
                    {
                        if (myShotChange[pair.frontCode][tickIndex].thisTime>0)
                        {
                            if (myShotChangeTick.ContainsKey(pair.frontCode)==true)
                            {
                                myShotChangeTick[pair.frontCode] = myShotChange[pair.frontCode][tickIndex];
                            }
                            else
                            {
                                myShotChangeTick.Add(pair.frontCode, myShotChange[pair.frontCode][tickIndex]);
                            }
                            myShot[pair.frontCode] = PositionApplication.GetPositionShot(myShot[pair.frontCode], myShotChangeTick[pair.frontCode]);
                        }
                        if (myShotChange[pair.nextCode][tickIndex].thisTime > 0)
                        {
                            if (myShotChangeTick.ContainsKey(pair.nextCode) == true)
                            {
                                myShotChangeTick[pair.nextCode] = myShotChange[pair.nextCode][tickIndex];
                            }
                            else
                            {
                                myShotChangeTick.Add(pair.nextCode, myShotChange[pair.nextCode][tickIndex]);
                            }
                            myShot[pair.nextCode] = PositionApplication.GetPositionShot(myShot[pair.nextCode], myShotChangeTick[pair.nextCode]);
                        }
                    }
                    //根据IH的盘口价格获取当前tick的数据
                    
                    if (IH[tickIndex].time>0)
                    {
                        IHNow = IH[tickIndex];
                    }
                    //根据ETF的盘口价格获取当前tick的数据
                    
                    if (ETF[tickIndex].time > 0)
                    {
                        ETFNow = ETF[tickIndex];
                    }
                    //遍历所有的合约，进行开平仓条件的判断
                    foreach (var pair in optionPairList)
                    {
                        //平仓
                        double closeVolume = 0;
                        if (myHold.ContainsKey(pair.frontCode) && myHold.ContainsKey(pair.nextCode) )
                            //如果没有持仓就不用考虑平仓了
                        {
                            //closeVolume = GetClosePosition(myShot[pair.frontCode], myShot[pair.nextCode], myHold[pair.frontCode], myHold[pair.nextCode]);
                            closeVolume = GetClosePosition2(myShot[pair.frontCode], myShot[pair.nextCode], myHold[pair.frontCode], myHold[pair.nextCode], today, tickIndex, ETFNow.lastPrice, duration, durationFurther);
                        }
                        //如果判断出需要平仓，一系列处理。
                        if (closeVolume > 0)
                        {
                            double frontCost = myHold[pair.frontCode].cost;
                            double nextCost = myHold[pair.nextCode].cost;
                            double frontVolume = myHold[pair.frontCode].position + closeVolume;
                            double nextVolume = myHold[pair.nextCode].position - closeVolume;
                            //处理盘口价格的变动
                            GetPositionModify(myShot[pair.frontCode], myShot[pair.nextCode], closeVolume,time);
                            //处理持仓状态的变动
                            //GetHoldStatusModifyByClose(myHold, myShot[pair.frontCode], myShot[pair.nextCode], ref myHoldStatus.cashNow, closeVolume);
                            myHoldStatus.OptionStatusModification(pair.frontCode, closeVolume, myShot[pair.frontCode].ask[0].price, myShot[pair.frontCode].openMargin, "close");
                            myHoldStatus.OptionStatusModification(pair.nextCode, -closeVolume, myShot[pair.nextCode].bid[0].price, myShot[pair.nextCode].openMargin, "close");
                            //记录交易信息
                            InsertTradeInformation(recordList, pair.frontCode, today, time, ETFNow.lastPrice, myShot[pair.frontCode].strike, myShot[pair.frontCode].type, myShot[pair.frontCode].ask[0].price, closeVolume, myShot[pair.frontCode].ask[0].volatility,frontCost,frontVolume,remark);
                            InsertTradeInformation(recordList, pair.nextCode, today, time, ETFNow.lastPrice, myShot[pair.nextCode].strike, myShot[pair.nextCode].type, myShot[pair.nextCode].bid[0].price, -closeVolume, myShot[pair.nextCode].bid[0].volatility,nextCost,nextVolume,remark);
                        }
                        //开仓
                        if (myHoldStatus.cashNow.availableFunds/initialCapital<0.3 || closeVolume>0)
                            //如果可用资金占用初始资金的30%以下，就不开仓了
                            //如果已经出发平仓，就不再开仓
                        {
                            continue;
                        }

                        double openVolume = 0;
                        if (myShot[pair.frontCode].ask!=null && myShot[pair.nextCode].bid!=null)
                        {
                            openVolume = GetOpenPosition2(myShot[pair.frontCode], myShot[pair.nextCode], today, tickIndex, ETFNow.lastPrice, duration, durationFurther);
                        }
                        //如果判断出需要开仓，一系列处理。
                        if (openVolume>0)
                        {
                            totalVolume += openVolume;
                            dailyVolume += openVolume;

                            //处理盘口价格的变动
                            GetPositionModify(myShot[pair.nextCode], myShot[pair.frontCode], openVolume, time);
                            //处理持仓状态的变动
                            //GetHoldStatusModifyByOpen(myHold, myShot[pair.nextCode], myShot[pair.frontCode], ref myHoldStatus.cashNow, openVolume);
                            myHoldStatus.OptionStatusModification(pair.nextCode, openVolume, myShot[pair.nextCode].ask[0].price, myShot[pair.nextCode].openMargin, "open");
                            myHoldStatus.OptionStatusModification(pair.frontCode, -openVolume, myShot[pair.frontCode].bid[0].price, myShot[pair.frontCode].openMargin, "open");
                            double frontCost = myHold[pair.frontCode].cost;
                            double nextCost = myHold[pair.nextCode].cost;
                            double frontVolume = myHold[pair.frontCode].position ;
                            double nextVolume = myHold[pair.nextCode].position;
                            //记录交易信息
                            InsertTradeInformation(recordList, pair.nextCode, today, time,ETFNow.lastPrice, myShot[pair.nextCode].strike, myShot[pair.nextCode].type, myShot[pair.nextCode].ask[0].price, openVolume,myShot[pair.nextCode].ask[0].volatility,nextCost,nextVolume,remark);
                            InsertTradeInformation(recordList, pair.frontCode, today, time,ETFNow.lastPrice,myShot[pair.frontCode].strike, myShot[pair.frontCode].type,myShot[pair.frontCode].bid[0].price, -openVolume, myShot[pair.frontCode].bid[0].volatility,frontCost,frontVolume,remark);
                        }
                    }
                    if (tickIndex%600==0 && tickIndex>=1800)
                    {
                        optionStatus status=GetOptionStatus(myHold, myShot, ETFNow,today,tickIndex);
                        double cashDelta = ETFNow.lastPrice * status.delta;
                       // Console.WriteLine("Date: {0}, Time: {1}, optionDelta: {2}, optionValue: {3}", today, time, Math.Round(cashDelta),Math.Round(status.presentValue));
                        double IHDelta = myHoldStatus.cashNow.IHhold * IHNow.lastPrice * 300;
                        double IHVolume = -Math.Round((cashDelta + IHDelta) / (IHNow.lastPrice * 300));
                        myHoldStatus.IHStatusModification(IHNow.lastPrice, 0);
                        if (Math.Abs(cashDelta + IHDelta-lastDelta)>300000 && Math.Abs(IHVolume) > 0)
                        {
                            lastDelta =cashDelta+ (myHoldStatus.cashNow.IHhold+IHVolume) * IHNow.lastPrice * 300;
                        }
                        else
                        {
                            IHVolume = 0;
                        }
                        myHoldStatus.IHStatusModification(IHNow.lastPrice, 0);
                        if (Math.Abs(IHVolume)> 0)
                        {
                           myHoldStatus.IHStatusModification(IHNow.lastPrice, IHVolume);
                            List<optionTradeRecord> ihRecord = new List<optionTradeRecord>();
                            int ihName =Convert.ToInt32(GetNextIHFuture(today).Substring(2,4));
                            if (recordList.ContainsKey(ihName))
                            {
                                ihRecord = recordList[ihName];
                            }
                            else
                            {
                                recordList.Add(ihName, ihRecord);
                            }
                            optionTradeRecord ihRecord0 = new optionTradeRecord(ihName, today, time, IHNow.lastPrice, IHVolume,ETFNow.lastPrice);
                            ihRecord.Add(ihRecord0);
                            recordList[ihName] = ihRecord;
                            //Console.WriteLine("Date: {0}, time: {1}, optionDelta: {2}, IHDelta: {3}, IHCost: {4}", today, time, Math.Round(cashDelta), Math.Round(IHDelta),Math.Round(myHoldStatus.cashNow.IHCost));
                            
                        }
                    }
                 }
                //对今日的持仓进行清理
                foreach (int key in new List<int>(myHold.Keys))
                {
                    if (myHold[key].position==0)
                    {
                        myHold.Remove(key);
                    }
                }
                optionStatus statusLast = GetOptionStatus(myHold, myShot, ETFNow, today,28801);
                double delta = ETFNow.lastPrice * statusLast.delta;
                delta += myHoldStatus.cashNow.IHhold * IHNow.lastPrice * 300;
                double totalCash = myHoldStatus.cashNow.availableFunds + myHoldStatus.cashNow.optionMargin + myHoldStatus.cashNow.IHMargin+statusLast.presentValue;
                Console.WriteLine("Date: {0},money: {1}, margin: {2}, volume: {3}, delta: {4}, total: {5}， IHvalue：{6}", today,Math.Round(myHoldStatus.cashNow.availableFunds),Math.Round(myHoldStatus.cashNow.optionMargin),dailyVolume,Math.Round(delta),Math.Round(totalCash),Math.Round(myHoldStatus.cashNow.IHCost));
                DocumentApplication.RecordCsv(recordCSV, today.ToString(),Math.Round(totalCash).ToString(),Math.Round(myHoldStatus.cashNow.availableFunds).ToString(),Math.Round(myHoldStatus.cashNow.optionMargin).ToString(),Math.Round(myHoldStatus.cashNow.IHMargin).ToString(),Math.Round(statusLast.presentValue).ToString(),Math.Round(delta).ToString(),Math.Round(ETFNow.lastPrice * statusLast.delta).ToString(),Math.Round(myHoldStatus.cashNow.IHhold * IHNow.lastPrice * 300).ToString(),Math.Round(dailyVolume).ToString(),Math.Round(statusLast.hold).ToString());
                netValue.Add(Math.Round(totalCash/initialCash,4));
                //将今日交易记录写入数据库
                StoreTradeList(recordList, recordTableName);
            }
            double std = 0, mean = 0, withdrawal = 0, maxNetValue = 0;

            for (int i = 0; i < netValue.Count; i++)
            {
                mean += netValue[i];
                if (netValue[i] > maxNetValue)
                {
                    maxNetValue = netValue[i];
                }
                if ((netValue[i] / maxNetValue - 1) < withdrawal)
                {
                    withdrawal = netValue[i] / maxNetValue - 1;
                }
            }
            mean /= netValue.Count + 1;
            for (int i = 0; i < netValue.Count; i++)
            {
                std += Math.Pow(netValue[i] - mean, 2);
            }
            std = Math.Sqrt(std / netValue.Count);
            double sharpeRatio = 1/Math.Sqrt(netValue.Count) *(netValue[netValue.Count - 1] - 1) / std;
            Console.WriteLine("std: {0}, sharpe: {1}, withdrawal :{2}", std, sharpeRatio, withdrawal);
        }


        /// <summary>
        /// 获取当前持仓期权的状态
        /// </summary>
        /// <param name="myHold">今日持仓</param>
        /// <param name="myShot">当前盘口状态</param>
        /// <param name="ETF">50etf盘口状态</param>
        /// <param name="tickIndex">今日时刻</param>
        /// <returns>期权持仓的状态</returns>
        private optionStatus GetOptionStatus(Dictionary<int, optionHold> myHold, Dictionary<int, optionFormat> myShot,stockFormat ETF,int today,int tickIndex)
        {
            double delta = 0;
            double midPrice;
            double durationPlus = (28801 - tickIndex) / 28801.0;
            double margin = 0;
            double presentValue = 0;
            double hold = 0;
            foreach (var item in myHold)
            {
                int optionCode = item.Key;
                double volume = item.Value.position;
                if (volume>0)
                {
                    hold += volume;
                }
                if (volume!=0)
                {
                    optionFormat option = myShot[optionCode];
                    if (option.ask[0].price * option.bid[0].price != 0)
                    {
                        midPrice = (option.ask[0].price + option.bid[0].price) / 2;
                    }
                    else
                    {
                        midPrice = (option.ask[0].price == 0) ? option.bid[0].price : option.ask[0].price;
                    }
                    double duration=durationPlus+ TradeDays.GetTimeSpan(today, myShot[optionCode].endDate);
                    double volatility = Impv.sigma(ETF.lastPrice, midPrice, option.strike, duration, Configuration.RiskFreeReturn, option.type);
                    delta += volume * 10000 * Impv.optionDelta(ETF.lastPrice, volatility, option.strike, duration, Configuration.RiskFreeReturn, option.type);
                    if (volume<0)
                    {
                        margin += 10000 * Math.Abs(volume) * myShot[optionCode].openMargin;
                    }
                    presentValue += volume * midPrice*10000;
                }
            }
            return new optionStatus(presentValue,margin,delta,hold);
        }

        /// <summary>
        /// 将交易记录存入数据库
        /// </summary>
        /// <param name="recordList">每日的成交记录</param>
        /// <param name="tableName">表的名称</param>
        private void StoreTradeList(Dictionary<int, List<optionTradeRecord>> recordList,string tableName)
        {

            using (SqlConnection conn = new SqlConnection(Configuration.connectionString))
            {
                conn.Open();//打开数据库  
                SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "create table [" + Configuration.dataBaseName + "].[dbo].[" + tableName + "] ([Code] int not null,[Date] int not null,[Time] int not null,[Strike] float,[Type] char(12),[ETF] float,[Price] float,[Volume] float,[Volatility] float,[Cost] float,[HoldVolume] float,[Remark] char(32),primary key ([Code],[Date],[Time]))";
                try
                {
                    cmd.ExecuteReader();
                }
                catch (Exception myerror)
                {
                    //System.Console.WriteLine(myerror.Message);
                }
            }

            using (SqlConnection conn = new SqlConnection(Configuration.connectionString))
            {
                conn.Open();
                DataTable todayData = new DataTable();
                #region DataTable的列名的建立
                todayData.Columns.Add("Code", typeof(int));
                todayData.Columns.Add("Date", typeof(int));
                todayData.Columns.Add("Time", typeof(int));
                todayData.Columns.Add("Strike", typeof(double));
                todayData.Columns.Add("Type", typeof(string));
                todayData.Columns.Add("ETF", typeof(double));
                todayData.Columns.Add("Price", typeof(double));
                todayData.Columns.Add("Volume", typeof(double));
                todayData.Columns.Add("Volatility", typeof(double));
                todayData.Columns.Add("Cost", typeof(double));
                todayData.Columns.Add("HoldVolume", typeof(double));
                todayData.Columns.Add("Remark", typeof(string));
                #endregion

                foreach (var item in recordList)
                {
                    foreach (optionTradeRecord record in item.Value)
                    {
                        #region 将数据写入每一行中。
                        DataRow r = todayData.NewRow();
                        r["Code"] = record.optionCode;
                        r["Date"] = record.date;
                        r["Time"] = record.time;
                        r["Type"] = record.type;
                        r["Strike"] = record.strike;
                        r["ETF"] = record.ETFPrice;
                        r["Price"] = record.price;
                        r["Volume"] = record.volume;
                        r["Volatility"] = record.volatility;
                        r["Cost"] = record.cost;
                        r["HoldVolume"] = record.holdVolume;
                        r["Remark"] = record.remark.Trim();
                        todayData.Rows.Add(r);
                        #endregion
                    }
                }
                using (SqlBulkCopy bulk = new SqlBulkCopy(Configuration.connectionString))
                {
                    try
                    {
                        bulk.BatchSize = 100000;
                        bulk.DestinationTableName = tableName;
                        #region 依次建立数据的映射。
                        bulk.ColumnMappings.Add("Code", "Code");
                        bulk.ColumnMappings.Add("Date", "Date");
                        bulk.ColumnMappings.Add("Time", "Time");
                        bulk.ColumnMappings.Add("ETF", "ETF");
                        bulk.ColumnMappings.Add("Strike", "Strike");
                        bulk.ColumnMappings.Add("Type", "Type");
                        bulk.ColumnMappings.Add("Price", "Price");
                        bulk.ColumnMappings.Add("Volume", "Volume");
                        bulk.ColumnMappings.Add("Volatility","Volatility");
                        bulk.ColumnMappings.Add("Cost", "Cost");
                        bulk.ColumnMappings.Add("HoldVolume", "HoldVolume");
                        bulk.ColumnMappings.Add("Remark", "Remark");
                        #endregion
                        bulk.WriteToServer(todayData);
                    }
                    catch (Exception myerror)
                    {
                        System.Console.WriteLine(myerror.Message);
                    }
                }
                conn.Close();
            }
        }

        /// <summary>
        /// 记录交易信息
        /// </summary>
        /// <param name="recordList">交易列表</param>
        /// <param name="optionCode">合约代码</param>
        /// <param name="date">日期</param>
        /// <param name="time">时间</param>
        /// <param name="price">价格</param>
        /// <param name="volume">交易量</param>
        private void InsertTradeInformation(Dictionary<int, List<optionTradeRecord>> recordList, int optionCode, int date, int time, double ETFPrice, double strike, string type, double price,double volume, double volatility, double cost,double holdVolume,string remark)
        {
            List<optionTradeRecord> myRecord = new List<optionTradeRecord>();
            if (recordList.ContainsKey(optionCode)==true)
            {
                myRecord = recordList[optionCode];
            }
            else
            {
                recordList.Add(optionCode, myRecord);

            }
            optionTradeRecord record = new optionTradeRecord(optionCode, date, time, price, volume,ETFPrice,strike,type,volatility,cost,holdVolume,remark);
            myRecord.Add(record);
            recordList[optionCode] = myRecord;
        }

        /// <summary>
        /// 根据当日持仓情况计算保证金。
        /// </summary>
        /// <param name="holdList">持仓情况</param>
        /// <param name="myShot">价格信息</param>
        /// <returns>保证金总额</returns>
        private double GetOptionMargin(Dictionary<int, optionHold> holdList, Dictionary<int, optionFormat> myShot)
        {
            double margin = 0;
            foreach (int key in holdList.Keys)
            {
                if (holdList[key].position<0)
                {
                    margin += myShot[key].openMargin * Math.Abs(holdList[key].position) * 10000;
                }
            }
            return margin;
        }

       /// <summary>
       /// 处理开仓时持仓情况变化的函数
       /// </summary>
       /// <param name="holdList">持仓期权的列表</param>
       /// <param name="longSideShot">多头部分的盘口情况</param>
       /// <param name="shortSideShot">空头部分的盘口情况</param>
       /// <param name="condition">总体持仓情况</param>
       /// <param name="volume">开仓的数量</param>
        private void GetHoldStatusModifyByOpen(Dictionary<int, optionHold> holdList,optionFormat longSideShot, optionFormat shortSideShot, ref cashStatus condition, double volume)
        {
            if (holdList.ContainsKey(longSideShot.code)==false)
            {
                optionHold option = new optionHold();
                option.cost = longSideShot.ask[0].price;
                option.position = volume;
                holdList.Add(longSideShot.code, option);
            }
            else
            {
                optionHold option = new optionHold();
                optionHold oldOption = holdList[longSideShot.code];
                if ((volume + oldOption.position)==0)
                {
                    option.cost = 0;
                    option.position = 0;
                }
                else
                {
                    option.cost = (longSideShot.ask[0].price * volume + oldOption.cost * oldOption.position) / (volume + oldOption.position);
                    option.position = volume + oldOption.position;
                }
                holdList[longSideShot.code] = option;
            }

            if (holdList.ContainsKey(shortSideShot.code) == false)
            {
                optionHold option = new optionHold();
                option.cost = shortSideShot.bid[0].price;
                option.position = -volume;
                holdList.Add(shortSideShot.code, option);
            }
            else
            {
                optionHold option = new optionHold();
                optionHold oldOption = holdList[shortSideShot.code];
                if ((-volume + oldOption.position)==0)
                {
                    option.cost = 0;
                    option.position = 0;
                }
                else
                {
                    option.cost = (shortSideShot.bid[0].price * (-volume) + oldOption.cost * oldOption.position) / (-volume + oldOption.position);
                    option.position = -volume + oldOption.position;
                }
                holdList[shortSideShot.code] = option;
            }
            //卖出组合获得可用资金
            condition.availableFunds += (-longSideShot.ask[0].price + shortSideShot.bid[0].price)*volume * 10000 - 2.3 * 2;
            //保证金的质押
            condition.optionMargin += volume * shortSideShot.openMargin * 10000;
            //质押保证金损耗可用资金
            condition.availableFunds -= volume * shortSideShot.openMargin * 10000;
        }

        /// <summary>
        /// 根据近月合约和远月合约的盘口价格和波动率情况来计算开仓的数量
        /// </summary>
        /// <param name="frontShot">近月合约情况</param>
        /// <param name="nextShot">远月合约情况</param>
        /// <param name="today">今日日期</param>
        /// <param name="tickIndex">tick下标</param>
        private double GetOpenPosition(optionFormat frontShot, optionFormat nextShot,int today,int tickIndex,double etfPrice,double duration,double durationFurther)
        {
            double openVolume = 0;
            double price = frontShot.bid[0].price;
            double margin = frontShot.openMargin;
            double volumn = frontShot.bid[0].volume;
            double priceFurther = nextShot.ask[0].price;
            double volumnFurther = nextShot.ask[0].volume;
            bool open = false;
            if (etfPrice * price * volumn * priceFurther * volumnFurther > 0)
            {
                double r = 0.05;
                //利用BS公式计算近月以及远月期权的隐含波动率。并用这2个波动率差值得到近月合约到期时候，期权对应的隐含波动率。
                double sigma = frontShot.bid[0].volatility;
                double sigmaFurther = nextShot.ask[0].volatility;
                double lossOfLiquidity = Math.Abs(frontShot.bid[0].price - frontShot.ask[0].price) + Math.Abs(nextShot.bid[0].price - nextShot.ask[0].price);
                double duration0 = duration + (28801 - tickIndex) / 28801.0;
                double durationFurther0=durationFurther+ (28801 - tickIndex) / 28801.0;
                double strike = frontShot.strike;
                string type = frontShot.type;
                double sigmaNew = Math.Sqrt(sigma * sigma * (duration0) / (durationFurther0 - duration0) + sigmaFurther * sigmaFurther * (durationFurther0 - 2 * duration0) / (durationFurther0 - duration0));
                //利用隐含波动率来估计近月期权合约到期时候50etf的价格，这里使用若干倍的sigma来计算。
                double etfPriceFurtherUp = etfPrice * Math.Exp(2 * sigma * Math.Sqrt(duration0 / 252.0));
                double etfPriceFurtherDown = etfPrice * Math.Exp(-2 * sigma * Math.Sqrt(duration0 / 252.0));
                double noChange = Impv.optionLastPrice(etfPrice, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPrice, sigmaNew, strike, 0, r, type);
                //计算出持有头寸价值的上下限。
                double up = Impv.optionLastPrice(etfPriceFurtherUp, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPriceFurtherUp, sigmaNew, strike, 0, r, type);
                double down = Impv.optionLastPrice(etfPriceFurtherDown, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPriceFurtherDown, sigmaNew, strike, 0, r, type);
                double interestNoChange = noChange - (priceFurther - price);
                double interestUp = up - (priceFurther - price);
                double interestDown = down - (priceFurther - price);
                //利用收益风险比例是否大于1来判断开仓信息。
                if ((interestNoChange- lossOfLiquidity) / Math.Abs((Math.Min(interestUp - lossOfLiquidity, interestDown - lossOfLiquidity))) > 1.5 || (Math.Min(interestUp - lossOfLiquidity, interestDown - lossOfLiquidity))>0)
                {
                    if ((interestNoChange - lossOfLiquidity)/margin>0.02)
                    {
                        open = true;
                    }
                   
                }
            }
            if (open == true)
            {
                openVolume = Math.Min(volumn, volumnFurther);
            }
            return openVolume;
        }

        /// <summary>
        /// 根据近月合约和远月合约的盘口价格和波动率情况来计算开仓的数量
        /// </summary>
        /// <param name="frontShot">近月合约情况</param>
        /// <param name="nextShot">远月合约情况</param>
        /// <param name="today">今日日期</param>
        /// <param name="tickIndex">tick下标</param>
        private double GetOpenPosition2(optionFormat frontShot, optionFormat nextShot, int today, int tickIndex, double etfPrice, double duration, double durationFurther)
        {
            remark = "";
            double openVolume = 0;
            double price = frontShot.bid[0].price;
            double margin = frontShot.openMargin;
            double volumn = frontShot.bid[0].volume;
            double priceFurther = nextShot.ask[0].price;
            double volumnFurther = nextShot.ask[0].volume;
            bool open = false;
            if (etfPrice * price * volumn * priceFurther * volumnFurther > 0)
            {
                double r = Configuration.RiskFreeReturn;
                //利用BS公式计算近月以及远月期权的隐含波动率。并用这2个波动率差值得到近月合约到期时候，期权对应的隐含波动率。
                double sigma = frontShot.bid[0].volatility;
                double sigmaFurther = nextShot.ask[0].volatility;
                double duration0 = duration + (28801 - tickIndex) / 28801.0;
                double durationFurther0 = durationFurther + (28801 - tickIndex) / 28801.0;
                double sigmaNew = Math.Sqrt(sigma * sigma * (duration0) / (durationFurther0 - duration0) + sigmaFurther * sigmaFurther * (durationFurther0 - 2 * duration0) / (durationFurther0 - duration0));
                double sigmaNewForward= Math.Sqrt(-sigma * sigma * (duration0) / (duration0) + sigmaFurther * sigmaFurther * (durationFurther0) / (durationFurther0 - duration0));
                double lossOfLiquidity = Math.Abs(frontShot.bid[0].price - frontShot.ask[0].price) + Math.Abs(nextShot.bid[0].price - nextShot.ask[0].price);
                double strike = frontShot.strike;
                string type = frontShot.type;
                //利用隐含波动率来估计近月期权合约到期时候50etf的价格，这里使用若干倍的sigma来计算。
                double etfPriceFurtherUp = etfPrice * Math.Exp(2 * sigma * Math.Sqrt(duration0 / 252.0));
                double etfPriceFurtherDown = etfPrice * Math.Exp(-2 * sigma * Math.Sqrt(duration0 / 252.0));
                double noChange = Impv.optionLastPrice(etfPrice, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPrice, sigmaNew, strike, 0, r, type);
                //计算出持有头寸价值的上下限。
                double up = Impv.optionLastPrice(etfPriceFurtherUp, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPriceFurtherUp, sigmaNew, strike, 0, r, type);
                double down = Impv.optionLastPrice(etfPriceFurtherDown, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPriceFurtherDown, sigmaNew, strike, 0, r, type);
                double interestNoChange = noChange - (priceFurther - price);
                double interestUp = up - (priceFurther - price);
                double interestDown = down - (priceFurther - price);
                //利用收益风险比例是否大于1来判断开仓信息。
                if ((interestNoChange - lossOfLiquidity) / Math.Abs((Math.Min(interestUp - lossOfLiquidity, interestDown - lossOfLiquidity))) > 1.5 || (Math.Min(interestUp - lossOfLiquidity, interestDown - lossOfLiquidity)) > 0)
                {
                    if ((interestNoChange - lossOfLiquidity) / margin > 0.02)
                    {
                        open = true;
                        remark = "满足开仓条件";
                    }

                }
            }
            if (open == true)
            {
                openVolume = Math.Min(volumn, volumnFurther);
            }
            return openVolume;
        }

        /// <summary>
        /// 处理平仓情况变化的函数
        /// </summary>
        /// <param name="longSideHold">买入头寸的持仓</param>
        /// <param name="shortSideHold">卖出头寸的持仓</param>
        /// <param name="longSideShot">买入头寸的盘口情况</param>
        /// <param name="shortSideShot">卖出头寸的盘口情况</param>
        /// <param name="condition">总体持仓情况</param>
        /// <param name="volume">成交量</param>
        private void GetHoldStatusModifyByClose(Dictionary<int, optionHold> holdList, optionFormat longSideShot, optionFormat shortSideShot,ref cashStatus condition,double volume)
        {
            optionHold longSideHold = holdList[longSideShot.code];
            optionHold shortSideHold = holdList[shortSideShot.code];
            longSideHold.position += volume;
            shortSideHold.position -= volume;
            holdList[longSideShot.code] = longSideHold;
            holdList[shortSideShot.code] = shortSideHold;
            //卖出组合获得可用资金
            condition.availableFunds += (-longSideShot.ask[0].price + shortSideShot.bid[0].price) *volume* 10000 - 2.3 * 2;
            //保证金的释放
            condition.optionMargin -= volume * longSideShot.openMargin * 10000;
            //释放的保证金称为可用资金
            condition.availableFunds += volume * longSideShot.openMargin * 10000;
        }

        /// <summary>
        /// 处理盘口价格的变动
        /// </summary>
        /// <param name="longSideShot">买入合约的盘口价格</param>
        /// <param name="shortSideShot">卖出合约的盘口价格</param>
        /// <param name="volume">成交量</param>
        /// <param name="time">成交时间</param>
        private void GetPositionModify(optionFormat longSideShot,optionFormat shortSideShot,double volume,int time)
        {
            longSideShot.ask[0].volume -= volume;
            longSideShot.time = time;
            longSideShot.lastPrice = longSideShot.ask[0].price;
            shortSideShot.bid[0].volume -= volume;
            shortSideShot.time = time;
            shortSideShot.lastPrice = shortSideShot.bid[0].price;
        }


        /// <summary>
        /// 平仓条件2
        /// </summary>
        /// <param name="frontShot"></param>
        /// <param name="nextShot"></param>
        /// <param name="frontHold"></param>
        /// <param name="nextHold"></param>
        /// <param name="today"></param>
        /// <param name="tickIndex"></param>
        /// <param name="etfPrice"></param>
        /// <param name="duration"></param>
        /// <param name="durationFurther"></param>
        /// <returns></returns>
        private double GetClosePosition2(optionFormat frontShot, optionFormat nextShot, optionHold frontHold, optionHold nextHold,int today, int tickIndex, double etfPrice, double duration, double durationFurther)
        {
            remark = "";
            if (frontHold.position == 0 || frontShot.ask == null || nextShot.bid == null)
            {
                return 0;
            }
            double closeVolume = 0;
            double price = frontShot.ask[0].price;
            double margin = frontShot.openMargin;
            double volumn = frontShot.ask[0].volume;
            double priceFurther = nextShot.bid[0].price;
            double volumnFurther = nextShot.bid[0].volume;
            bool close  = false;
            if (etfPrice * price * volumn * priceFurther * volumnFurther > 0)
            {
                double r = Configuration.RiskFreeReturn;
                //利用BS公式计算近月以及远月期权的隐含波动率。并用这2个波动率差值得到近月合约到期时候，期权对应的隐含波动率。
                double sigma = frontShot.ask[0].volatility;
                double sigmaFurther = nextShot.bid[0].volatility;
                // double lossOfLiquidity = Math.Abs(frontShot.bid[0].price - frontShot.ask[0].price) + Math.Abs(nextShot.bid[0].price - nextShot.ask[0].price);
                double lossOfLiquidity = 0;
                double duration0 = duration + (28801 - tickIndex) / 28801.0;
                double durationFurther0 = durationFurther + (28801 - tickIndex) / 28801.0;
                double strike = frontShot.strike;
                string type = frontShot.type;
                double sigmaNew = Math.Sqrt(sigma * sigma * (duration0) / (durationFurther0 - duration0) + sigmaFurther * sigmaFurther * (durationFurther0 - 2 * duration0) / (durationFurther0 - duration0));
                //利用隐含波动率来估计近月期权合约到期时候50etf的价格，这里使用若干倍的sigma来计算。
                double etfPriceFurtherUp = etfPrice * Math.Exp(2 * sigma * Math.Sqrt(duration0 / 252.0));
                double etfPriceFurtherDown = etfPrice * Math.Exp(-2 * sigma * Math.Sqrt(duration0 / 252.0));
                double noChange = Impv.optionLastPrice(etfPrice, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPrice, sigmaNew, strike, 0, r, type);
                //计算出持有头寸价值的上下限。
                double up = Impv.optionLastPrice(etfPriceFurtherUp, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPriceFurtherUp, sigmaNew, strike, 0, r, type);
                double down = Impv.optionLastPrice(etfPriceFurtherDown, sigmaNew, strike, durationFurther0 - duration0, r, type) - Impv.optionLastPrice(etfPriceFurtherDown, sigmaNew, strike, 0, r, type);
                double interestNoChange = noChange - (priceFurther - price);
                double interestUp = up - (priceFurther - price);
                double interestDown = down - (priceFurther - price);
                //止损，收益率不够就止损。
                //if ((interestNoChange - lossOfLiquidity) / margin < 0.01)
                //{
                //    close = true;
                //    remark = "保证金占用过多 ";
                //}
                if ((interestNoChange - lossOfLiquidity) / Math.Abs((Math.Min(interestUp - lossOfLiquidity, interestDown - lossOfLiquidity))) < 0 && (Math.Min(interestUp - lossOfLiquidity, interestDown - lossOfLiquidity)) < 0)
                {
                    close = true;
                    remark += "预期收益不足 ";

                }

                //止盈，只有收益大于一定程度才平仓。
                double cost = nextHold.cost - frontHold.cost;//跨期组合开仓成本
                double presentValue = nextShot.bid[0].price - frontShot.ask[0].price;//跨期组合的现值
                if ((presentValue - cost)/cost>1.25 || (presentValue-cost)>0.03)
                {
                    close = true;
                    remark += "止盈 ";
                }
                ////止损，只有损失大于一定程度才平仓。
                //if ((presentValue - cost) < -0.03)
                //{
                //    close = true;
                //    remark += "止损 ";
                //}
                //强平，期权快到期时强行平仓。
                if (duration0<=3)
                {
                    close = true;
                    remark += "强平 ";
                }
            }
            if (close == true)
            {
                closeVolume = Math.Min(Math.Abs(frontHold.position), Math.Min(volumn,volumnFurther));
            }
            return closeVolume;
        }

        /// <summary>
        /// 根据当前盘口价格计算平仓量的函数。
        /// </summary>
        /// <param name="frontShot">当月合约盘口</param>
        /// <param name="nextShot">下月合约盘口</param>
        /// <param name="frontHold">当月合约持仓</param>
        /// <param name="nextHold">下月合约持仓</param>
        /// <returns>平仓的数量</returns>
        private double GetClosePosition(optionFormat frontShot,optionFormat nextShot,optionHold frontHold,optionHold nextHold)
        {
            double closePosition = frontHold.position;
            if (frontHold.position==0 || frontShot.ask==null || nextShot.bid==null)
            {
                return 0;
            }
            //简单的止盈止损,买平当月合约，卖平下月合约
            double cost = nextHold.cost - frontHold.cost;//跨期组合开仓成本
            double presentValue = nextShot.bid[0].price - frontShot.ask[0].price;//跨期组合的现值
            double midValue = (nextShot.bid[0].price+nextShot.ask[0].price)/2 - (frontShot.ask[0].price+frontShot.bid[0].price)/2;
            if (midValue/cost>1.25 || midValue / cost < 0.5 )
            {
                closePosition =Math.Min(Math.Abs(frontHold.position), Math.Min(nextShot.bid[0].volume, frontShot.ask[0].volume));
            }
            return closePosition;
        }

        /// <summary>
        /// 根据当日etf价格以及当日日期给出平价附近的备选的期权合约代码。
        /// </summary>
        /// <param name="ETF">etf的交易数据</param>
        /// <param name="date">今日日期</param>
        /// <returns>平价附近的合约</returns>
        private List<timeSpreadPair> GetSpreadPair(Dictionary<int,optionHold> holdList,stockFormat[] ETF,int date,int minDuration=0,int maxDuration=30)
        {
            List<timeSpreadPair> myPairList = new List<timeSpreadPair>();
            //记入昨日持仓的合约
            List<int> optionList = new List<int>(holdList.Keys);
            foreach (int key in optionList)
            {
                int code = key;
                int codeFurther= myOptionInfo.GetFurtherOption(code, date).optionCode;
                if (optionList.Contains(codeFurther))
                {
                    timeSpreadPair pair = new timeSpreadPair();
                    pair.frontCode = code;
                    pair.nextCode = codeFurther;
                    myPairList.Add(pair);
                }
            }
            //找出ETF的运动区间获取平值附近的合约。
            double maxEtf = myData.GetArrayMaxLastPrice(ETF);
            double minEtf = myData.GetArrayMinLastPrice(ETF);
            //记录每日参与交易的期权合约代码
            List<int> optionAtTheMoney = myOptionInfo.GetCodeListByStrike(minEtf, maxEtf, date);
            int frontDuration = myOptionInfo.GetFrontDuration(date);
            if (frontDuration<minDuration || maxDuration<frontDuration)
            {
                return myPairList;
            }
            foreach (int optionCode in optionAtTheMoney)
            {
                if (myOptionInfo.GetOptionDuration(optionCode,date)==frontDuration)
                {
                    timeSpreadPair pair = new timeSpreadPair();
                    pair.frontCode = optionCode;
                    pair.nextCode = myOptionInfo.GetFurtherOption(optionCode, date).optionCode;
                    if (myPairList.Contains(pair)==false)
                    {
                        myPairList.Add(pair);
                    }
                   
                }
            }
            return myPairList;
        }

        /// <summary>
        /// 根据当日日期获得下月IH合约表名称。
        /// </summary>
        /// <param name="date">日期</param>
        /// <returns>合约表名称</returns>
        private string GetNextIHFuture(int date)
        {
            string frontTable;
            string nextTable;
            //必要的日期辨认，根据当日的日期得到对应的当月合约和下月合约。
            DateTime thisMonth = TradeDays.IntToDateTime(date);
            DateTime nextMonth = DateTime.Parse(thisMonth.ToString("yyyy-MM-01")).AddMonths(1);
            DateTime nextTwoMonth = DateTime.Parse(nextMonth.ToString("yyyy-MM-01")).AddMonths(1);
            if (date <= TradeDays.ThirdFridayList[thisMonth.Year * 100 + thisMonth.Month] && date >= 20150501)
            //对IH,IF来说，201504的当月合约才IH1505,下月合约为IH1506
            {
                frontTable = "ih" + ((thisMonth.Year % 100) * 100 + thisMonth.Month).ToString();
                nextTable = "ih" + ((nextMonth.Year % 100) * 100 + nextMonth.Month).ToString();
            }
            else
            {
                frontTable = "ih" + ((nextMonth.Year % 100) * 100 + nextMonth.Month).ToString();
                nextTable = "ih" + ((nextTwoMonth.Year % 100) * 100 + nextTwoMonth.Month).ToString();
            }
            return nextTable;
        }
    }
}
