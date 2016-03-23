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

        /// <summary>
        /// 构造函数。初始化各类回测的信息。
        /// </summary>
        /// <param name="initialCapital">初始资金</param>
        /// <param name="startDate">开始时间</param>
        /// <param name="endDate">结束时间</param>
        public TimeSpread(double initialCapital,int startDate,int endDate)
        {
            myHoldStatus = new HoldStatus(initialCapital);
            this.initialCapital = initialCapital;
            this.startDate = startDate;
            this.endDate = endDate;
            myTradeDays = new TradeDays(startDate, endDate);
            myOptionInfo = new OptionCodeInformation(Configuration.dataBaseName, Configuration.optionCodeTableName, Configuration.connectionString);
            feePerUnit = Configuration.optionFeePerUnit;
            myData = new DataApplication(Configuration.dataBaseName, Configuration.connectionString);
        }


        public void TimeSpreadAnalysis()
        {

            //逐步遍历交易日期，逐日进行回测。
            for (int dateIndex = 0; dateIndex < myTradeDays.myTradeDay.Count; dateIndex++)
            {
                int today = myTradeDays.myTradeDay[dateIndex];
                //从状态的类中取出当前的持仓情况。
                Dictionary<int, optionHold> myHold = myHoldStatus.capitalToday.optionList;
                //初始化各类信息，包括记录每个合约具体的盘口价格，具体的盘口价格变动，参与交易之后具体的盘口状态等。
                Dictionary<int, optionPositionChange[]> myShotChange = new Dictionary<int, optionPositionChange[]>(); //盘口价格变化的列表
                Dictionary<int, optionPositionChange> myShotChangeTick = new Dictionary<int, optionPositionChange>();//当前tick下盘口价格变动的
                Dictionary<int, optionFormat> myShot = new Dictionary<int, optionFormat>();//当前tick盘口价格的状态
                //记录IH和50etf的盘口价格。
                stockFormat[] IH = myData.GetStockArray(myData.GetDataTable(GetNextIHFuture(today), today));
                stockFormat[] ETF = myData.GetStockArray(myData.GetDataTable(Configuration.tableOf50ETF, today));


                //第一步，选取今日应当关注的合约代码，包括平价附近的期权合约以及昨日遗留下来的持仓。其中，平价附近的期权合约必须满足交易日的需求，昨日遗留下来的持仓必须全部囊括。近月合约列入code，远月合约列入codeFurther。
                //注意，某些合约既要进行开仓判断又要进行平仓判断。
                List<timeSpreadPair> optionPairList = GetSpreadPair(ETF, today,5,12);

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
                    frontShotStart.preClose = optionList[0].preClose;
                    frontShotStart.strike = optionList[0].strike;
                    frontShotStart.endDate = optionList[0].endDate;
                    myShot.Add(pair.frontCode, frontShotStart);
                    
                    //获取下月合约的数据并整理出tick之间的变动信息。
                    List<optionFormat> optionNextList = myData.GetOptionList(myData.GetDataTable("sh" + pair.nextCode.ToString(), today));
                    PositionApplication myPosAppNext = new PositionApplication(optionNextList);
                    optionPositionChange[] changeNextList = myPosAppNext.GetPositionChange();
                    myShotChange.Add(pair.nextCode, changeNextList);
                    optionFormat nextShotStart = new optionFormat();
                    nextShotStart.openMargin = optionNextList[0].openMargin;
                    nextShotStart.preClose = optionNextList[0].preClose;
                    nextShotStart.strike = optionNextList[0].strike;
                    nextShotStart.endDate = optionNextList[0].endDate;
                    myShot.Add(pair.nextCode, nextShotStart);
                }

                //第三步，按照tick的下标进行遍历。对开平仓的机会进行判断。
                stockFormat IHNow = new stockFormat();
                stockFormat ETFNow = new stockFormat();
                for (int tickIndex = 0; tickIndex < 28802; tickIndex++)
                {
                    int time = TradeDays.IndexToTime(tickIndex);
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
                        double closeVolume = GetClosePosition(myShot[pair.frontCode], myShot[pair.nextCode], myHoldStatus.capitalToday.optionList[pair.frontCode], myHoldStatus.capitalToday.optionList[pair.nextCode]);
                        //如果判断出需要平仓，一系列处理。
                        if (closeVolume > 0)
                        {
                            //处理盘口价格的变动
                            GetPositionModify(myShot[pair.frontCode], myShot[pair.nextCode], closeVolume,time);
                            //处理持仓状态的变动
                            GetHoldStatusModifyByClose(myHoldStatus.capitalToday.optionList[pair.frontCode], myHoldStatus.capitalToday.optionList[pair.nextCode], myShot[pair.frontCode], myShot[pair.nextCode], myHoldStatus.capitalToday, closeVolume);
                        }
                        //开仓
                        if (myHoldStatus.capitalToday.availableFunds/initialCapital<0.3)
                            //如果可用资金占用初始资金的30%以下，就不开仓了
                        {
                            continue;
                        }
                        double openVolume = GetOpenPosition(myShot[pair.frontCode], myShot[pair.nextCode], today, tickIndex);
                        //如果判断出需要开仓，一系列处理。
                        if (openVolume>0)
                        {

                        }
                    }

                 }


            }

        }

        /// <summary>
        /// 根据近月合约和远月合约的盘口价格和波动率情况来计算开仓的数量
        /// </summary>
        /// <param name="frontShot">近月合约情况</param>
        /// <param name="nextShot">远月合约情况</param>
        /// <param name="today">今日日期</param>
        /// <param name="tickIndex">tick下标</param>
        private double GetOpenPosition(optionFormat frontShot, optionFormat nextShot,int today,int tickIndex)
        {
            double openVolume = 0;

            return openVolume;
        }

        /// <summary>
        /// 处理持仓情况变化的函数
        /// </summary>
        /// <param name="longSideHold">买入头寸的持仓</param>
        /// <param name="shortSideHold">卖出头寸的持仓</param>
        /// <param name="longSideShot">买入头寸的盘口情况</param>
        /// <param name="shortSideShot">卖出头寸的盘口情况</param>
        /// <param name="condition">总体持仓情况</param>
        /// <param name="volume">成交量</param>
        private void GetHoldStatusModifyByClose(optionHold longSideHold,optionHold shortSideHold, optionFormat longSideShot, optionFormat shortSideShot, capitalCondition condition,double volume)
        {
            longSideHold.position += volume;
            shortSideHold.position -= volume;
            //卖出组合获得可用资金
            condition.availableFunds += (-longSideShot.ask[0].price + shortSideShot.bid[0].price) * 10000 - 2.3 * 2;
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
            if (frontHold.position==0)
            {
                return 0;
            }
            //简单的止盈止损,买平当月合约，卖平下月合约
            double cost = nextHold.cost - frontHold.cost;//跨期组合开仓成本
            double presentValue = nextShot.bid[0].price - frontShot.ask[0].price;//跨期组合的现值
            if ((presentValue-cost)/cost>1.2 || (presentValue - cost) / cost < 0.8)
            {
                closePosition =Math.Min(frontHold.position, Math.Min(nextShot.bid[0].volume, frontShot.ask[0].volume));
            }
            return closePosition;
        }

        /// <summary>
        /// 根据当日etf价格以及当日日期给出平价附近的备选的期权合约代码。
        /// </summary>
        /// <param name="ETF">etf的交易数据</param>
        /// <param name="date">今日日期</param>
        /// <returns>平价附近的合约</returns>
        private List<timeSpreadPair> GetSpreadPair(stockFormat[] ETF,int date,int minDuration=0,int maxDuration=30)
        {
            List<timeSpreadPair> myPairList = new List<timeSpreadPair>();
            //找出ETF的运动区间获取平值附近的合约。
            double maxEtf = myData.GetArrayMaxLastPrice(ETF);
            double minEtf = myData.GetArrayMinLastPrice(ETF);
            //记录每日参与交易的期权合约代码
            List<int> optionAtTheMoney = myOptionInfo.GetCodeListByStrike(minEtf - 0.05, maxEtf + 0.05, date);
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
                    myPairList.Add(pair);
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
