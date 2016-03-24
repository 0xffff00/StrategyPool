using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StrategyPool
{
    /// <summary>
    /// 期权价格格式
    /// </summary>
    struct optionFormat
    {
        public int code;
        public string type;
        public double strike;
        public int startDate, endDate;
        public int date, time;
        public double lastPrice;
        public optionPriceWithGreek[] ask, bid;
        public double preClose, preSettle;
        public double openMargin;
    }

    /// <summary>
    /// 记录期权盘口价格变动的结构体。
    /// </summary>
    struct optionPositionChange
    {
        public int lastTime,thisTime;
        public double lastPrice;
        public List<optionPriceWithGreek> askChange, bidChange;
        public optionPositionChange(int lastTime,int thisTime,double lastPrice)
        {
            this.lastTime = lastTime;
            this.thisTime = thisTime;
            this.lastPrice = lastPrice;
            askChange = new List<optionPriceWithGreek>();
            bidChange = new List<optionPriceWithGreek>();
        }
    }
    /// <summary>
    /// 期权带希腊值盘口价格的格式
    /// </summary>
    struct optionPriceWithGreek
    {
        public double price, volume, volatility, delta;
        public optionPriceWithGreek(double price,double volume,double volatility,double delta)
        {
            this.price = price;
            this.volume = volume;
            this.volatility = volatility;
            this.delta = delta;
        }
    }

    /// <summary>
    /// 股票价格的格式
    /// </summary>
    struct stockFormat
    {
        public int code;
        public int date, time;
        public double lastPrice;
        public stockPrice[] ask, bid;
        public double preClose;
    }

    /// <summary>
    /// 股票盘口价格的格式
    /// </summary>
    struct stockPrice
    {
        public double price, volume;
        public stockPrice(double price,double volume)
        {
            this.price = price;
            this.volume = volume;
        }
    }

    /// <summary>
    /// 存储期权基本信息的结构体。
    /// </summary>
    struct optionInfo
    {
        public int optionCode;
        public string optionName;
        public int startDate;
        public int endDate;
        public string optionType;
        public string executeType;
        public double strike;
        public string market;
    }

    /// <summary>
    /// 描述持仓仓位和成本的结构体
    /// </summary>
    struct optionHold
    {
        public double position;
        public double cost;
        public optionHold(double cost,double position)
        {
            this.cost = cost;
            this.position = position;
        }
    }

    /// <summary>
    /// 描述资金情况的结构体
    /// </summary>
    struct cashStatus
    {
        public double availableFunds;
        public double optionMargin;
        public double IHhold, IHprice;
        public double IHMargin;
        public Dictionary<int, optionHold> optionList;
        public cashStatus(double availableFunds,double optionMargin,double IHhold,double IHprice,double IHMargin)
        {
            this.availableFunds = availableFunds;
            this.optionMargin = optionMargin;
            this.IHhold = IHhold;
            this.IHprice = IHprice;
            this.IHMargin = IHMargin;
            optionList = new Dictionary<int, optionHold>();
        }

    }

    /// <summary>
    /// 跨期交易期权对
    /// </summary>
    struct timeSpreadPair
    {
        public int frontCode;
        public int nextCode;
    }

    /// <summary>
    /// 记录期权具体的交易信息
    /// </summary>
    struct optionTradeRecord
    {
        public int optionCode;
        public int date;
        public int time;
        public double price;
        public double volume;
        public optionTradeRecord(int optionCode,int date,int time,double price,double volume)
        {
            this.optionCode = optionCode;
            this.date = date;
            this.time = time;
            this.price = price;
            this.volume = volume;
        }

    }

}
