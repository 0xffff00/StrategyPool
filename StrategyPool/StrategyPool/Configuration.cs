
namespace StrategyPool
{
    /// <summary>
    /// 设置全局参数的类。
    /// </summary>
    class Configuration
    {
        /// <summary>
        /// 数据库的名称。
        /// </summary>
        public static string dataBaseName = "Option";
        /// <summary>
        /// 记录50etf的表名称。
        /// </summary>
        public static string tableOf50ETF = "sh510050";
        /// <summary>
        /// 记录所有期权的表名称。
        /// </summary>
        public static string tableOfOptionAll = "optionAll";
        /// <summary>
        /// 保存交易日信息的表的名称。
        /// </summary>
        public static string tradeDaysTableName = "myTradeDays";
        /// <summary>
        /// 记录IH当月合约的表的名称。
        /// </summary>
        public static string tableOfIHFront = "ihFront";
        /// <summary>
        /// 记录IH下月合约的表的名称。
        /// </summary>
        public static string tableofIHNext = "ihNext";
        /// <summary>
        /// 提供数据库sql连接字符串信息。
        /// </summary>
        public static string connectionString = "server=(local);database=Option;Integrated Security=true;";
        /// <summary>
        /// 给定期权标的的名称。
        /// </summary>
        public static string underlyingAsset = "510050.SH";
        /// <summary>
        /// 保存期权合约基本信息的数据表的名称。
        /// </summary>
        public static string optionCodeTableName = "optionCodeInformation";
        /// <summary>
        /// 无风险收益率。
        /// </summary>
        public static double RiskFreeReturn = 0.05;
    }
}
