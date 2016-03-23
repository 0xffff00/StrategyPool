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


        /// <summary>
        /// 核心函数。跨期价差的分析函数。
        /// </summary>
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


                //第一步，选取今日应当关注的合约代码，包括平价附近的期权合约以及昨日遗留下来的持仓。其中，平价附近的期权合约必须满足交易日的需求，昨日遗留下来的持仓必须全部囊括。
                //注意，某些合约既要进行开仓判断又要进行平仓判断。
                List<timeSpreadPair> optionPairList = GetSpreadPair(myHold,ETF, today,5,12);
                

                
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
                    myShot.Add(pair.nextCode, nextShotStart);
                }
                //第三步，在开仓之前根据昨日的持仓情况，对今日资金情况进行盘点。用昨日收盘价结算来近似。
                double yesterMargin = GetOptionMargin(myHold, myShot);
                myHoldStatus.capitalToday.availableFunds += myHoldStatus.capitalToday.optionMargin - yesterMargin;
                myHoldStatus.capitalToday.optionMargin = yesterMargin;

                //第四步，按照tick的下标进行遍历。对开平仓的机会进行判断。
                stockFormat IHNow = new stockFormat();
                stockFormat ETFNow = new stockFormat();
                double duration = 0;
                double durationFurther = 0;
                if (optionPairList.Count>0)
                {
                    duration = TradeDays.GetTimeSpan(today, myShot[optionPairList[0].frontCode].endDate);
                    durationFurther=TradeDays.GetTimeSpan(today, myShot[optionPairList[0].nextCode].endDate);
                }
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
                        double closeVolume = 0;
                        if (myHold.ContainsKey(pair.frontCode) && myHold.ContainsKey(pair.nextCode))
                            //如果没有持仓就不用考虑平仓了
                        {
                            closeVolume = GetClosePosition(myShot[pair.frontCode], myShot[pair.nextCode], myHold[pair.frontCode], myHold[pair.nextCode]);
                        }
                        //如果判断出需要平仓，一系列处理。
                        if (closeVolume > 0)
                        {
                            //处理盘口价格的变动
                            GetPositionModify(myShot[pair.frontCode], myShot[pair.nextCode], closeVolume,time);
                            //处理持仓状态的变动
                            GetHoldStatusModifyByClose(myHold, myShot[pair.frontCode], myShot[pair.nextCode], ref myHoldStatus.capitalToday, closeVolume);
                            if (myHold.Count % 2 == 1)
                            {

                            }
                        }
                        //开仓
                        if (myHoldStatus.capitalToday.availableFunds/initialCapital<0.3)
                            //如果可用资金占用初始资金的30%以下，就不开仓了
                        {
                            continue;
                        }

                        double openVolume = 0;
                        if (myShot[pair.frontCode].ask!=null && myShot[pair.nextCode].bid!=null)
                        {
                            openVolume = GetOpenPosition(myShot[pair.frontCode], myShot[pair.nextCode], today, tickIndex, ETFNow.lastPrice, duration, durationFurther);
                        }
                        //如果判断出需要开仓，一系列处理。
                        if (openVolume>0)
                        {
                            //处理盘口价格的变动
                            GetPositionModify(myShot[pair.nextCode], myShot[pair.frontCode], openVolume, time);
                            //处理持仓状态的变动
                            GetHoldStatusModifyByOpen(myHold, myShot[pair.nextCode], myShot[pair.frontCode], ref myHoldStatus.capitalToday, openVolume);
                            if (myHold.Count%2==1)
                            {

                            }
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
            }

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
                    margin += myShot[key].openMargin * 10000;
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
        private void GetHoldStatusModifyByOpen(Dictionary<int, optionHold> holdList,optionFormat longSideShot, optionFormat shortSideShot, ref capitalCondition condition, double volume)
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
                option.cost = (longSideShot.ask[0].price * volume + oldOption.cost * oldOption.position) / (volume + oldOption.position);
                option.position = volume + oldOption.position;
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
                option.cost = (shortSideShot.bid[0].price * volume + oldOption.cost * oldOption.position) / (volume + oldOption.position);
                option.position = -volume + oldOption.position;
                holdList[longSideShot.code] = option;
            }
            //卖出组合获得可用资金
            condition.availableFunds += (-longSideShot.ask[0].price + shortSideShot.bid[0].price) * 10000 - 2.3 * 2;
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
            double volumn = frontShot.bid[0].volume;
            double priceFurther = nextShot.ask[0].price;
            double volumnFurther = nextShot.ask[0].volume;
            bool open = false;
            if (etfPrice * price * volumn * priceFurther * volumnFurther > 0)
            {
                double r = 0.05;
                //利用BS公式计算近月以及远月期权的隐含波动率。并用这2个波动率差值得到近月合约到期时候，期权对应的隐含波动率。
                double sigma = frontShot.bid[0].volatility;
                double sigmaFurther = nextShot.bid[0].volatility;
                double duration0 = duration + (28801 - tickIndex) / 28801;
                double durationFurther0=durationFurther+ (28801 - tickIndex) / 28801;
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
                if (interestNoChange / Math.Abs((Math.Min(interestUp, interestDown))) > 1.5)
                {
                    open = true;
                }
            }
            if (open == true)
            {
                openVolume = Math.Min(volumn, volumnFurther);
            }
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
        private void GetHoldStatusModifyByClose(Dictionary<int, optionHold> holdList, optionFormat longSideShot, optionFormat shortSideShot,ref capitalCondition condition,double volume)
        {
            optionHold longSideHold = holdList[longSideShot.code];
            optionHold shortSideHold = holdList[shortSideShot.code];
            longSideHold.position += volume;
            shortSideHold.position -= volume;
            holdList[longSideShot.code] = longSideHold;
            holdList[shortSideShot.code] = shortSideHold;
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
            if (frontHold.position==0 || frontShot.ask==null || nextShot.bid==null)
            {
                return 0;
            }
            //简单的止盈止损,买平当月合约，卖平下月合约
            double cost = nextHold.cost - frontHold.cost;//跨期组合开仓成本
            double presentValue = nextShot.bid[0].price - frontShot.ask[0].price;//跨期组合的现值
            if ((presentValue-cost)/cost>1.2 || (presentValue - cost) / cost < 0.8)
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
