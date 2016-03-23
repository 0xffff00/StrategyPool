using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StrategyPool
{
    /// <summary>
    /// 处理盘口价格变动的类。
    /// </summary>
    class PositionApplication
    {
        public List<optionFormat> optionList = new List<optionFormat>();

        /// <summary>
        /// 构造函数。获取期权交易数据。
        /// </summary>
        /// <param name="optionList">期权交易数据。</param>
        public PositionApplication(List<optionFormat> optionList)
        {
            this.optionList=optionList;
        }

        /// <summary>
        /// 获得期权盘口价格变动的函数。
        /// </summary>
        /// <returns>盘口价格的变动</returns>
        public optionPositionChange[] GetPositionChange()
        {
            optionPositionChange[] changeArray = new optionPositionChange[28802];
            for (int i = 1; i < optionList.Count; i++)
            {
                optionFormat lastPosition = optionList[i - 1];
                optionFormat thisPosition = optionList[i];
                changeArray[TradeDays.TimeToIndex(thisPosition.time)] = getChange(lastPosition, thisPosition);
            }
            return changeArray;
        }

        /// <summary>
        /// 根据当前状态和前一状态，得到两个状态之间的变动情况。
        /// </summary>
        /// <param name="lastPosition">前一状态</param>
        /// <param name="thisPosition">当前状态</param>
        /// <returns>变动情况</returns>
        private optionPositionChange getChange(optionFormat lastPosition,optionFormat thisPosition)
        {
            int lastTime = lastPosition.time ;
            int thisTime = thisPosition.time ;
            optionPositionChange myChange= new optionPositionChange(lastTime, thisTime, thisPosition.lastPrice);
            //分别处理ask和bid的盘口价格，通过分析前一状态的盘口价格和后一状态的盘口价格，得到两个盘口价格之间的具体变动信息。数据结构使用哈希表便于理解和处理。
            #region ask处理的新方法
            SortedDictionary<double, optionPriceWithGreek> askChange = new SortedDictionary<double, optionPriceWithGreek>();
            for (int index = 0; index < 5; index++)
            {
                if (askChange.ContainsKey(thisPosition.ask[index].price))
                {
                    optionPriceWithGreek newStatus = askChange[thisPosition.ask[index].price];
                    newStatus.volume += thisPosition.ask[index].volume;
                    newStatus.volatility = thisPosition.ask[index].volatility;
                    newStatus.delta = thisPosition.ask[index].delta;
                    askChange[thisPosition.ask[index].price] = newStatus;
                }
                else
                {
                    optionPriceWithGreek newStatus = new optionPriceWithGreek(thisPosition.ask[index].price, thisPosition.ask[index].volume, thisPosition.ask[index].volatility, thisPosition.ask[index].delta);
                    askChange.Add(thisPosition.ask[index].price, thisPosition.ask[index]);
                }
                if (askChange.ContainsKey(lastPosition.ask[index].price))
                {
                    optionPriceWithGreek newStatus = askChange[lastPosition.ask[index].price];
                    newStatus.volume -= lastPosition.ask[index].volume;
                    newStatus.volatility = lastPosition.ask[index].volatility;
                    newStatus.delta = lastPosition.ask[index].delta;
                    askChange[lastPosition.ask[index].price] = newStatus;
                }
                else
                {
                    optionPriceWithGreek newStatus = new optionPriceWithGreek(lastPosition.ask[index].price, -lastPosition.ask[index].volume, lastPosition.ask[index].volatility, lastPosition.ask[index].delta);
                    askChange.Add(lastPosition.ask[index].price, newStatus);
                }
            }
            foreach (var item in askChange)
            {
                if (item.Value.volume!=0)
                {
                    myChange.askChange.Add(item.Value);
                }
            }
            #endregion
            #region bid处理的新方法
            Dictionary<double, optionPriceWithGreek> bidChange = new Dictionary<double, optionPriceWithGreek>();
            for (int index = 0; index < 5; index++)
            {
                if (bidChange.ContainsKey(thisPosition.bid[index].price))
                {
                    optionPriceWithGreek newStatus = bidChange[thisPosition.bid[index].price];
                    newStatus.volume += thisPosition.bid[index].volume;
                    newStatus.volatility = thisPosition.bid[index].volatility;
                    newStatus.delta = thisPosition.bid[index].delta;
                    bidChange[thisPosition.bid[index].price] = newStatus;
                }
                else
                {
                    optionPriceWithGreek newStatus = new optionPriceWithGreek(thisPosition.bid[index].price, thisPosition.bid[index].volume, thisPosition.bid[index].volatility, thisPosition.bid[index].delta);
                    bidChange.Add(thisPosition.bid[index].price, thisPosition.bid[index]);
                }
                if (bidChange.ContainsKey(lastPosition.bid[index].price))
                {
                    optionPriceWithGreek newStatus = bidChange[lastPosition.bid[index].price];
                    newStatus.volume -= lastPosition.bid[index].volume;
                    newStatus.volatility = lastPosition.bid[index].volatility;
                    newStatus.delta = lastPosition.bid[index].delta;
                    bidChange[lastPosition.bid[index].price] = newStatus;
                }
                else
                {
                    optionPriceWithGreek newStatus = new optionPriceWithGreek(lastPosition.bid[index].price, -lastPosition.bid[index].volume, lastPosition.bid[index].volatility, lastPosition.bid[index].delta);
                    bidChange.Add(lastPosition.bid[index].price, newStatus);
                }
            }
            bidChange = bidChange.OrderByDescending(o => o.Key).ToDictionary(o => o.Key, p => p.Value);
            foreach (var item in bidChange)
            {
                if (item.Value.volume != 0)
                {
                    myChange.bidChange.Add(item.Value);
                }
            }
            #endregion
            return myChange;
        }

        /// <summary>
        /// 根据前一状态和变动情况得到新的状态
        /// </summary>
        /// <param name="lastShot">前一状态</param>
        /// <param name="change">变动情况</param>
        /// <returns>新的状态</returns>
        public static optionFormat GetPositionShot(optionFormat lastShot,optionPositionChange change)
        {
            //记录常量
            optionFormat nextShot = new optionFormat();
            nextShot.code = lastShot.code;
            nextShot.date = lastShot.date;
            nextShot.time = change.thisTime;
            nextShot.startDate = lastShot.startDate;
            nextShot.endDate = lastShot.endDate;
            nextShot.openMargin = lastShot.openMargin;
            nextShot.preClose = lastShot.preClose;
            nextShot.preSettle = lastShot.preSettle;
            nextShot.strike = lastShot.strike;
            nextShot.type = lastShot.type;
            nextShot.lastPrice = change.lastPrice;
            //计算变动的部分
            #region 计算ask部分
            SortedDictionary<double, optionPriceWithGreek> ask = new SortedDictionary<double, optionPriceWithGreek>();
            if (lastShot.ask!=null)
            {
                for (int i = 0; i < lastShot.ask.Length; i++)
                {
                    optionPriceWithGreek status = new optionPriceWithGreek(lastShot.ask[i].price, lastShot.ask[i].volume, lastShot.ask[i].volatility, lastShot.ask[i].delta);
                    if (status.price > 0 && status.volume > 0)
                    {
                        ask.Add(status.price, status);
                    }
                }
            }
            foreach (optionPriceWithGreek status in change.askChange)
            {
                if (ask.ContainsKey(status.price))
                {
                    ask[status.price] = new optionPriceWithGreek(ask[status.price].price,ask[status.price].volume+status.volume,status.volatility,status.delta);
                }
                else
                {
                    if (status.price > 0 && status.volume > 0)
                    {
                        ask.Add(status.price, new optionPriceWithGreek(status.price, status.volume, status.volatility, status.delta));
                    }
                        
                }
            }
            int num = 0;
            nextShot.ask = new optionPriceWithGreek[5];
            foreach (var item in ask)
            {
                if (item.Value.volume>0)
                {
                    nextShot.ask[num].price = item.Value.price;
                    nextShot.ask[num].volume = item.Value.volume;
                    nextShot.ask[num].volatility = item.Value.volatility;
                    nextShot.ask[num].delta = item.Value.delta;
                    num += 1;
                }
                if (num>=5)
                {
                    break;
                }
            }
            #endregion
            #region 计算bid部分
            Dictionary<double, optionPriceWithGreek> bid = new Dictionary<double, optionPriceWithGreek>();
            if (lastShot.bid!=null)
            {
                for (int i = 0; i < lastShot.bid.Length; i++)
                {
                    optionPriceWithGreek status = new optionPriceWithGreek(lastShot.bid[i].price, lastShot.bid[i].volume, lastShot.bid[i].volatility, lastShot.bid[i].delta);
                    if (status.price > 0 && status.volume > 0)
                    {
                        bid.Add(status.price, status);
                    }
                }
            }
            foreach (optionPriceWithGreek status in change.bidChange)
            {
                if (bid.ContainsKey(status.price))
                {
                    bid[status.price] = new optionPriceWithGreek(bid[status.price].price, bid[status.price].volume + status.volume, status.volatility, status.delta);
                }
                else
                {
                    if (status.price > 0 && status.volume > 0)
                    {
                        bid.Add(status.price, new optionPriceWithGreek(status.price, status.volume, status.volatility, status.delta));
                    }
                }
            }
            bid = bid.OrderByDescending(o => o.Key).ToDictionary(o => o.Key, p => p.Value);
            num = 0;
            nextShot.bid = new optionPriceWithGreek[5];
            foreach (var item in bid)
            {
                if (item.Value.volume > 0)
                {
                    nextShot.bid[num].price = item.Value.price;
                    nextShot.bid[num].volume = item.Value.volume;
                    nextShot.bid[num].volatility = item.Value.volatility;
                    nextShot.bid[num].delta = item.Value.delta;
                    num += 1;
                }
                if (num >= 5)
                {
                    break;
                }
            }
            #endregion
            return nextShot;
        }
    }
}
