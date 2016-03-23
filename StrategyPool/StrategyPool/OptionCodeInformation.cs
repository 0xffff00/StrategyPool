using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace StrategyPool
{
    /// <summary>
    /// 获取期权合约代码信息
    /// </summary>
    class OptionCodeInformation
    {
        public DataTable contractData=new DataTable();
        public SortedDictionary<int, optionInfo> CodeList = new SortedDictionary<int, optionInfo>();
        /// <summary>
        /// 构造函数。获取所有的期权合约代码的信息。
        /// </summary>
        /// <param name="dataBase">数据库名称</param>
        /// <param name="tableName">期权合约代码</param>
        /// <param name="connectionString">连接字符串</param>
        public OptionCodeInformation(string dataBase,string tableName,string connectionString)
        {
            DataApplication myDataBase = new DataApplication(dataBase, connectionString);
            contractData = myDataBase.GetDataTable(tableName);
            CodeList = myDataBase.GetOptionInfoList(contractData);
            TradeDays myTradeDays = new TradeDays(20150209);
        }

        /// <summary>
        /// 根据期权合约代码，返回期权合约的信息。
        /// </summary>
        /// <param name="optionCode">期权合约代码</param>
        /// <returns>optionInfo格式的期权合约信息</returns>
        public optionInfo GetContractInfo(int optionCode)
        {
            optionInfo contract=new optionInfo();
            if (CodeList.ContainsKey(optionCode))
            {
                contract = CodeList[optionCode];
            }
            return contract;
        }

        /// <summary>
        /// 根据期权合约代码和今日日期，给出期权的到期天数。
        /// </summary>
        /// <param name="optionCode">期权合约代码</param>
        /// <param name="date">今日日期</param>
        /// <returns>合约到期天数</returns>
        public int GetOptionDuration(int optionCode,int date)
        {
            optionInfo contract = GetContractInfo(optionCode);
            int duration = -1;
            if (contract.optionCode>0)
            {
                duration = TradeDays.GetTimeSpan(date, contract.endDate);
            }
            return duration;
        }

        /// <summary>
        /// 根据给定的期权合约代码和今日日期，给出对应的远月期权的信息。
        /// </summary>
        /// <param name="optionCode">期权合约代码</param>
        /// <param name="date">今日日期</param>
        /// <returns>optionInfo格式的期权信息</returns>
        public optionInfo GetFurtherOption(int optionCode,int date)
        {
            optionInfo frontContract = GetContractInfo(optionCode);
            int frontDuration = GetOptionDuration(optionCode, date);
            int nextDuration = 999;
            optionInfo nextContract = new optionInfo();
            foreach (var item in CodeList)
            {
                optionInfo contract = item.Value;
                int duration = GetOptionDuration(contract.optionCode, date);
                if (contract.optionCode!=frontContract.optionCode && contract.startDate<=date && contract.endDate>=date && duration>frontDuration && contract.optionType==frontContract.optionType && contract.strike == frontContract.strike && contract.executeType == frontContract.executeType)
                {
                    if (duration>frontDuration && duration<nextDuration)
                    {
                        nextContract = contract;
                        nextDuration = duration;
                    }
                }
            }
            return nextContract;
        }

        /// <summary>
        /// 根据给定的期权合约给出对应(call和put相对应)的期权合约信息。
        /// </summary>
        /// <param name="optionCode">期权合约代码</param>
        /// <returns>optionInfo格式的期权合约信息</returns>
        public optionInfo GetCorrespondingOption(int optionCode)
        {
            optionInfo thisContract = GetContractInfo(optionCode);
            optionInfo correspondingContract = new optionInfo();
            foreach (var item in CodeList)
            {
                optionInfo contract = item.Value;
                if (contract.optionCode != thisContract.optionCode && contract.strike==thisContract.strike && contract.startDate==thisContract.startDate && contract.endDate==thisContract.endDate && contract.optionType!=thisContract.optionType && contract.executeType==thisContract.executeType)
                {
                    correspondingContract = contract;
                }
            }
            return correspondingContract;
        }

        /// <summary>
        /// 给出当日近月合约到期天数。
        /// </summary>
        /// <param name="date">今日日期</param>
        /// <returns>近月合约到期天数</returns>
        public int GetFrontDuration(int date)
        {
            int frontDuration = 999;
            foreach (var item in CodeList)
            {
                optionInfo contract = item.Value;
                if (contract.startDate <= date && contract.endDate >= date)
                {
                    int duration = TradeDays.GetTimeSpan(date, contract.endDate);
                    if (duration < frontDuration)
                    {
                        frontDuration = duration;
                    }
                }
            }
            return frontDuration;
        }

        /// <summary>
        /// 根据当日日期和strike的区间给出合约列表。
        /// </summary>
        /// <param name="minStrike">最低行权价</param>
        /// <param name="maxStrike">最高行权价</param>
        /// <param name="date">今日日期</param>
        /// <returns>期权合约代码的列表</returns>
        public List<int> GetCodeListByStrike(double minStrike,double maxStrike,int date)
        {
            List<int> codeList = new List<int>();
            int frontDuration = GetFrontDuration(date);
            foreach (var item in CodeList)
            {
                optionInfo contract = item.Value;
                if (contract.startDate<=date && contract.endDate>=date && TradeDays.GetTimeSpan(date,contract.endDate)==frontDuration && contract.strike>=minStrike && contract.strike<=maxStrike)
                {
                    codeList.Add(contract.optionCode);
                }
            }
            return codeList;
        }
    }
}
