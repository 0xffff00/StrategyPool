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
        public SortedDictionary<int, capitalCondition> capitalList = new SortedDictionary<int, capitalCondition>();
        public capitalCondition capitalToday;

        /// <summary>
        /// 构造函数。给定初始的启动资金。
        /// </summary>
        /// <param name="initialCapital">启动资金。</param>
        public HoldStatus(double initialCapital)
        {
            capitalToday = new capitalCondition(initialCapital, 0, 0,0,0);
        }

        /// <summary>
        /// 处理期权头寸变动的函数。
        /// </summary>
        /// <param name="optionChange"></param>
        public void InsertOptionChange(optionFormat optionChange)
        {

        }

        /// <summary>
        /// 处理IH头寸变动的函数。
        /// </summary>
        /// <param name="IHChange"></param>
        public void InsertIHChange(stockFormat IHChange)
        {

        }

    }
}
