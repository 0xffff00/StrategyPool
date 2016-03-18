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
        public double price, volumn, volatility, delta;
        public optionPriceWithGreek(double price,double volumn,double volatility,double delta)
        {
            this.price = price;
            this.volumn = volumn;
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
        public double price, volumn;
        public stockPrice(double price,double volumn)
        {
            this.price = price;
            this.volumn = volumn;
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
}
