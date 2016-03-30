using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StrategyPool
{
    /// <summary>
    /// 监控以及记录每日持仓情况，资金情况的类。
    /// </summary>
    class HoldStatus
    {
        public cashStatus cashNow;
        /// <summary>
        /// 构造函数。给定初始的启动资金。
        /// </summary>
        /// <param name="initialCapital">启动资金。</param>
        public HoldStatus(double initialCapital)
        {
            cashNow = new cashStatus(initialCapital);
        }

        /// <summary>
        /// 处理期权持仓情况变化的函数
        /// </summary>
        /// <param name="optionCode">合约代码</param>
        /// <param name="volume">成交量</param>
        /// <param name="price">成交价格</param>
        /// <param name="margin">保证金</param>
        /// <param name="openOrClose">开仓还是平仓</param>
        public void OptionStatusModification(int optionCode,double volume,double price,double margin,string openOrClose)
        {
            if (openOrClose=="open")
            {
                //持仓情况的变动
                if (cashNow.optionList.ContainsKey(optionCode)==false)
                {
                    optionHold myHold = new optionHold(price,volume);
                    cashNow.optionList.Add(optionCode, myHold);
                }
                else
                {
                    optionHold myHold = new optionHold(price, volume);
                    optionHold oldHold = cashNow.optionList[optionCode];
                    if ((oldHold.position+myHold.position)==0)
                    {
                        myHold = new optionHold(0, 0);
                        cashNow.optionList[optionCode] = myHold;
                    }
                    else
                    {
                        myHold.cost = (myHold.cost * myHold.position + oldHold.cost * oldHold.position) / (oldHold.position + myHold.position);
                        myHold.position = (oldHold.position + myHold.position);
                        cashNow.optionList[optionCode] = myHold;
                    }
                }
                //可用资金和保证金的变动
                if (volume>0)//买开的情况
                {
                    cashNow.availableFunds -= volume * price * 10000 + 2.3;

                }
                else //卖开的情况
                {
                    cashNow.availableFunds -= volume * price * 10000 + 2.3;
                    cashNow.optionMargin -= volume*margin * 10000;
                    cashNow.availableFunds += volume*margin * 10000;
                }
            }
            else
            {
                optionHold myHold = new optionHold(cashNow.optionList[optionCode].cost, cashNow.optionList[optionCode].position + volume);
                cashNow.optionList[optionCode] = myHold;
                if (volume>0) //买平
                {
                    cashNow.availableFunds -= volume * price * 10000 + 2.3;
                    cashNow.optionMargin -= volume*margin * 10000;
                    cashNow.availableFunds += volume*margin * 10000;
                }
                else //卖平
                {
                    cashNow.availableFunds -= volume * price * 10000 + 2.3;
                }
            }
        }

        /// <summary>
        /// 计算IH的持仓和保证金情况
        /// </summary>
        /// <param name="price">价格</param>
        /// <param name="volume">交易量</param>
        public void IHStatusModification(double price,double volume )
        {
            cashNow.IHMargin -= 300*(cashNow.IHprice - price) * cashNow.IHhold;
            double marginNeed = price * 300 * 0.2*Math.Abs(cashNow.IHhold+volume);
            double fee =Math.Abs(volume)*price * 300 * 2 / 10000;
            cashNow.availableFunds += cashNow.IHMargin - marginNeed-fee;
            cashNow.IHCost+= cashNow.IHMargin - marginNeed-fee;
            cashNow.IHMargin = marginNeed;
            cashNow.IHhold = cashNow.IHhold + volume;
            cashNow.IHprice = (cashNow.IHhold==0)?0:price;
        }
    }
}
