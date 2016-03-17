using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data.SqlClient;
using WAPIWrapperCSharp;
using System.Data;

namespace StrategyPool
{

    /// <summary>
    /// 获取交易日期信息的类。
    /// </summary>
    class TradeDays
    {
        private string connectionString = Configuration.connectionString;
        private string dataBaseName = Configuration.dataBaseName;
        private string tradeDaysTableName = Configuration.tradeDaysTableName;

        /// <summary>
        /// 静态变量。记录本地路径。
        /// </summary>
        public static string filePathName;

        /// <summary>
        /// 存储2016年交易日信息。
        /// </summary>
        private static List<int> TradeDayOf2016;

        /// <summary>
        /// 存储历史的交易日信息。
        /// </summary>
        private static List<int> tradeDaysOfDataBase;

        /// <summary>
        /// 存储所有回测时期内的交易日信息。
        /// </summary>
        public List<int> myTradeDay { get; set; }

        /// <summary>
        /// 存储所有回测期内的第三个星期五日期。
        /// </summary>
        public static Dictionary<int, int> ThirdFridayList;

        /// <summary>
        /// 存储所有回测期内的第四个星期三日期。
        /// </summary>
        public static Dictionary<int, int> ForthWednesdayList;

        /// <summary>
        /// 存储每日每个tick对应的时刻。
        /// </summary>
        public static int[] myTradeTicks { get; set; }


        /// <summary>
        /// 静态函数。将数组下标转化为具体时刻。
        /// </summary>
        /// <param name="index">下标</param>
        /// <returns>时刻</returns>
        public static int indexToTime(int index)
        {
            int time0 = index * 500;
            int hour = time0 / 3600000;
            time0 = time0 % 3600000;
            int minute = time0 / 60000;
            time0 = time0 % 60000;
            int second = time0;
            if (hour < 2)
            {
                hour += 9;
                minute += 30;
                if (minute >= 60)
                {
                    minute -= 60;
                    hour += 1;
                }
            }
            else
            {
                hour += 11;
            }
            return hour * 10000000 + minute * 100000 + second;
        }

        /// <summary>
        /// 静态函数。提供数据库sql连接字符串信息。
        /// </summary>
        /// <returns>sql连接字符串</returns>
        public static string GetconnectionString()
        {
            //远程服务器地址
            //return "server=192.168.1.165;database=OptionTick;uid=dfreader;pwd=dfreader;";
            //毛衡的台式机服务器地址
            //return "server=192.168.38.217;database=OptionTick;uid=reader;pwd=reader;";
            //本地服务器地址
            return "server=(local);database=Option;Integrated Security=true;";
        }

        /// <summary>
        /// 静态函数。给出下一交易日。
        /// </summary>
        /// <param name="today">当前交易日</param>
        /// <returns>下一交易日</returns>
        public static int GetNextTradeDay(int today)
        {
            int nextIndex = tradeDaysOfDataBase.FindIndex(delegate (int i) { return i == today; }) + 1;
            if (nextIndex >= tradeDaysOfDataBase.Count)
            {
                return 0;
            }
            else
            {
                return tradeDaysOfDataBase[nextIndex];
            }
        }

        /// <summary>
        /// 静态函数。给出前一交易日。
        /// </summary>
        /// <param name="today">当前交易日</param>
        /// <returns>返回前一交易日</returns>
        public static int GetPreviousTradeDay(int today)
        {
            int preIndex = tradeDaysOfDataBase.FindIndex(delegate (int i) { return i == today; }) - 1;
            if (preIndex < 0)
            {
                return 0;
            }
            else
            {
                return tradeDaysOfDataBase[preIndex];
            }
        }

        /// <summary>
        /// 静态函数。获取交易日间隔天数。
        /// </summary>
        /// <param name="firstday">开始日期</param>
        /// <param name="lastday">结束日期</param>
        /// <returns>间隔天数</returns>
        public static int GetTimeSpan(int firstday, int lastday)
        {
            if (firstday >= tradeDaysOfDataBase[0] && lastday <= tradeDaysOfDataBase[tradeDaysOfDataBase.Count - 1] && lastday >= firstday)
            {
                int startIndex = -1, endIndex = -1;
                for (int i = 0; i < tradeDaysOfDataBase.Count; i++)
                {
                    if (tradeDaysOfDataBase[i] == firstday)
                    {
                        startIndex = i;
                    }
                    if (tradeDaysOfDataBase[i] > firstday && tradeDaysOfDataBase[i - 1] < firstday)
                    {
                        startIndex = i;
                    }
                    if (tradeDaysOfDataBase[i] == lastday)
                    {
                        endIndex = i;
                    }
                    if (tradeDaysOfDataBase[i] > lastday && tradeDaysOfDataBase[i - 1] < lastday)
                    {
                        endIndex = i - 1;
                    }
                }
                if (startIndex != -1 && endIndex != -1)
                {
                    return endIndex - startIndex + 1;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }

        }

        /// <summary>
        /// 获取2016交易日信息。
        /// </summary>
        private void getDaysFrom2016()
        {
            TradeDayOf2016 = new List<int>();
            List<int> HolidayOf2016 = new List<int>(){20160101,20160102,20160103,20160207,20160208,20160209,20160210,20160211,20160212,20160213,
20160402,20160403,20160404,20160430,20160501,20160502,20160609,20160610,20160611,20160915,20160916,20160917,20161001,20161002,20161003,20161004,20161005,20161006,20161007
            };
            bool isDate = false;
            int today = 0;
            for (int month = 1; month <= 12; month++)
            {
                for (int day = 1; day <= 31; day++)
                {
                    string strMonth = (month < 9) ? "0" + month.ToString() : month.ToString();
                    string strDay = (day < 9) ? "0" + day.ToString() : day.ToString();
                    string strDate = "2016-" + strMonth + "-" + strDay;
                    try
                    {
                        today = 20160000 + month * 100 + day;
                        DateTime.Parse(strDate);
                        DateTime mydatetime = Convert.ToDateTime(strDate);
                        isDate = true;
                        if ((int)(mydatetime.DayOfWeek) == 6 || (int)(mydatetime.DayOfWeek) == 0 || (HolidayOf2016.Find(delegate (int tmp) { return tmp == today; }) > 0))
                        {
                            isDate = false;
                        }
                    }
                    catch
                    {
                        isDate = false;
                    }

                    if (isDate == true)
                    {
                        TradeDayOf2016.Add(today);
                    }
                }
            }

        }

        /// <summary>
        /// 从数据库中读取历史交易日信息
        /// </summary>
        /// <returns>返回是否读取成功</returns>
        private bool getDaysFromDataBase()
        {

            tradeDaysOfDataBase = new List<int>();
            //从数据库的表sh510050中读取交易日信息
            using (SqlConnection conn = new SqlConnection(GetconnectionString()))
            {
                conn.Open();//打开数据库  
                            //  Console.WriteLine("数据库打开成功!");
                            //创建数据库命令  
                SqlCommand cmd = conn.CreateCommand();
                //创建查询语句  
                cmd.CommandText = "SELECT distinct(Date) FROM [OptionTick].[dbo].[sh510050] where Date>=20150209 order by Date";
                try
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        tradeDaysOfDataBase.Add(reader.GetInt32(reader.GetOrdinal("Date")));
                    }
                }
                catch (Exception myError)
                {
                    System.Console.WriteLine(myError.Message);
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
            //若成功读入交易日信息，将交易日信息存到本地文件
            if (tradeDaysOfDataBase.Count > 0)
            {
                if (TradeDayOf2016 == null)
                {
                    getDaysFrom2016();
                }
                foreach (var item in TradeDayOf2016)
                {
                    if (item > tradeDaysOfDataBase[tradeDaysOfDataBase.Count - 1])
                    {
                        tradeDaysOfDataBase.Add(item);
                    }
                }
                List<string[]> ls = new List<string[]>();
                foreach (var item in tradeDaysOfDataBase)
                {
                    string[] ls0 = { item.ToString() };
                    ls.Add(ls0);
                }
                DocumentApplication.WriteCsv(filePathName, ls);
            }
            //成功读取交易日信息返回true，否者返回false
            if (tradeDaysOfDataBase.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 构造函数。从万德数据库中读取日期数据，并保持到本地数据库。
        /// </summary>
        /// <param name="startDate">交易日开始时间</param>
        /// <param name="endDate">交易日结束时间</param>
        public TradeDays(int startDate, int endDate = 0)
        {
            //对给定的参数做一些勘误和修正。
            if (endDate == 0)
            {
                endDate = Convert.ToInt32(DateTime.Now.ToString("yyyyMMdd"));
            }
            if (endDate < startDate)
            {
                Console.WriteLine("Wrong trade Date!");
                startDate = endDate;
            }
            if (tradeDaysOfDataBase == null)
            {
                tradeDaysOfDataBase = new List<int>();
            }
            //从本地数据库中读取交易日信息。
            GetDataFromDataBase();
            //从万德数据库中读取交易日信息。但仅在数据库没有构造的时候进行读取。并保持到本地数据库。
            if (tradeDaysOfDataBase.Count == 0 || tradeDaysOfDataBase[tradeDaysOfDataBase.Count - 1] < 20161230)
            {
                GetDataFromWindDataBase();
                SaveTradeDaysData();
            }
            //根据给定的回测开始日期和结束日期，给出交易日列表。
            myTradeDay = new List<int>();

            foreach (int date in tradeDaysOfDataBase)
            {
                if (date >= startDate && date <= endDate)
                {
                    myTradeDay.Add(date);
                }
            }
            //生成每个tick对应的数组下标，便于后期的计算。
            if (myTradeTicks == null)
            {
                myTradeTicks = new int[28800];
            }
            for (int timeIndex = 0; timeIndex < 28800; timeIndex++)
            {
                myTradeTicks[timeIndex] = IndexToTime(timeIndex);
            }
            //生成回测日期内的第四个星期三和第三个星期五。
            if (ThirdFridayList == null)
            {
                ThirdFridayList = new Dictionary<int, int>();
                ForthWednesdayList = new Dictionary<int, int>();
            }
            GetForthWednesday();
            GetThirdFriday();
        }

        /// <summary>
        /// 从本地数据库中读取交易日信息的函数。
        /// </summary>
        /// <returns></returns>
        private bool GetDataFromDataBase()
        {
            bool exist = false;
            int theLastDay = 0;

            if (tradeDaysOfDataBase.Count > 0)
            {
                theLastDay = tradeDaysOfDataBase[tradeDaysOfDataBase.Count - 1];
            }
            //从数据库的表myTradeDay中读取交易日信息
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();//打开数据库  
                            //  Console.WriteLine("数据库打开成功!");
                            //创建数据库命令  
                SqlCommand cmd = conn.CreateCommand();
                //创建查询语句  
                cmd.CommandText = "select [Date] from [" + dataBaseName + "].[dbo].[" + tradeDaysTableName + "] order by[Date]";
                try
                {
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        int today = reader.GetInt32(reader.GetOrdinal("Date"));
                        if (today > theLastDay)
                        {
                            tradeDaysOfDataBase.Add(today);
                        }
                    }
                    reader.Close();
                }
                catch (Exception myError)
                {
                    System.Console.WriteLine(myError.Message);
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
            if (tradeDaysOfDataBase.Count > 0)
            {
                exist = true;
            }
            return exist;
        }

        /// <summary>
        /// 从万德数据库中读取交易日信息数据。
        /// </summary>
        private void GetDataFromWindDataBase()
        {
            int theLastDay = 0;
            if (tradeDaysOfDataBase.Count > 0)
            {
                theLastDay = tradeDaysOfDataBase[tradeDaysOfDataBase.Count - 1];
            }
            //万德API接口的类。
            WindAPI w = new WindAPI();
            w.start();
            //从万德数据库中抓取交易日信息。
            WindData days = w.tdays("20100101", "20161231", "");
            //将万德中读取的数据转化成object数组的形式。
            object[] dayData = days.data as object[];
            foreach (object item in dayData)
            {
                DateTime today = (DateTime)item;
                int now = DateTimeToInt(today);
                if (now > theLastDay)
                {
                    tradeDaysOfDataBase.Add(now);
                }
            }
            w.stop();
        }

        /// <summary>
        /// 将交易日信息存储到本地数据库中。
        /// </summary>
        /// <returns>返回存储是否成功</returns>
        private bool SaveTradeDaysData()
        {
            bool success = false;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();//打开数据库  
                SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "select max([Date]) as [Date] from [" + dataBaseName + "].[dbo].[" + tradeDaysTableName + "]";
                //判断数据表是否存在。
                bool exist = false;
                int theLastDate = 0;
                try
                {
                    theLastDate = (int)cmd.ExecuteScalar();
                    exist = true;
                }
                catch (Exception myerror)
                {
                    System.Console.WriteLine(myerror.Message);
                }
                //若数据表不存在就创建新表。
                if (exist == false)
                {
                    System.Console.WriteLine("Creating new database of tradeDays");
                    cmd.CommandText = "create table [" + dataBaseName + "].[dbo].[" + tradeDaysTableName + "] ([Date] int not null,primary key ([Date]))";
                    try
                    {
                        cmd.ExecuteReader();
                    }
                    catch (Exception myerror)
                    {
                        System.Console.WriteLine(myerror.Message);
                    }
                }
                //如果表中的最大日期小于tradeDaysOfDataBase中的最大日期就更新本表。
                if (tradeDaysOfDataBase.Count > 0 && theLastDate < tradeDaysOfDataBase[tradeDaysOfDataBase.Count - 1])
                {
                    //利用DateTable格式存入数据。
                    DataTable myDataTable = new DataTable();
                    myDataTable.Columns.Add("Date", typeof(int));
                    foreach (int today in tradeDaysOfDataBase)
                    {
                        if (today > theLastDate)
                        {
                            DataRow r = myDataTable.NewRow();
                            r["Date"] = today;
                            myDataTable.Rows.Add(r);
                        }
                    }
                    //利用sqlbulkcopy写入数据
                    using (SqlBulkCopy bulk = new SqlBulkCopy(connectionString))
                    {
                        try
                        {
                            bulk.DestinationTableName = tradeDaysTableName;
                            bulk.ColumnMappings.Add("Date", "Date");
                            bulk.WriteToServer(myDataTable);
                            success = true;
                        }
                        catch (Exception myerror)
                        {
                            System.Console.WriteLine(myerror.Message);
                        }
                    }
                }
                conn.Close();
            }
            return success;
        }


        /// <summary>
        /// 将DateTime格式的日期转化成为int类型的日期。
        /// </summary>
        /// <param name="time">DateTime类型的日期</param>
        /// <returns>Int类型的日期</returns>
        public static int DateTimeToInt(DateTime time)
        {
            return time.Year * 10000 + time.Month * 100 + time.Day;
        }

        /// <summary>
        /// 将Int格式的日期转化为DateTime格式类型的日期。
        /// </summary>
        /// <param name="day"></param>
        /// <returns></returns>
        public static DateTime IntToDateTime(int day)
        {
            string dayString = DateTime.ParseExact(day.ToString(), "yyyyMMdd", null).ToString();
            return Convert.ToDateTime(dayString);
        }

        /// <summary>
        /// 静态函数。将数组下标转化为具体时刻。
        /// </summary>
        /// <param name="Index">下标</param>
        /// <returns>时刻</returns>
        public static int IndexToTime(int index)
        {
            int time0 = index * 500;
            int hour = time0 / 3600000;
            time0 = time0 % 3600000;
            int minute = time0 / 60000;
            time0 = time0 % 60000;
            int second = time0;
            if (hour < 2)
            {
                hour += 9;
                minute += 30;
                if (minute >= 60)
                {
                    minute -= 60;
                    hour += 1;
                }
            }
            else
            {
                hour += 11;
            }
            return hour * 10000000 + minute * 100000 + second;
        }

        /// <summary>
        /// 静态函数。将时间转化为数组下标。
        /// </summary>
        /// <param name="time">时间</param>
        /// <returns>数组下标</returns>
        public static int TimeToIndex(int time)
        {
            int hour = time / 10000000;
            time = time % 10000000;
            int minute = time / 100000;
            time = time % 100000;
            int tick = time / 500;
            int index;
            if (hour >= 13)
            {
                index = 14400 + (hour - 13) * 7200 + minute * 120 + tick;
            }
            else
            {
                index = (int)(((double)hour - 9.5) * 7200) + minute * 120 + tick;
            }
            return index;
        }

        /// <summary>
        /// 在指定日期的当月，给出指定的第几个星期几。
        /// </summary>
        /// <param name="date">给定日期</param>
        /// <param name="whichWeek">第几个</param>
        /// <param name="whichDayOfWeek">星期几</param>
        /// <returns>找到的日期</returns>
        private int GetSpecialDate(DateTime date, int whichWeek, string whichDayOfWeek)
        {
            DateTime searchDate = DateTime.Parse(date.ToString("yyyy-MM-01"));
            int year = searchDate.Year;
            int month = searchDate.Month;
            int number = 0;
            while (searchDate.Year == year && searchDate.Month == month)
            {
                if (searchDate.DayOfWeek.ToString() == whichDayOfWeek)
                {
                    number += 1;
                    if (number == whichWeek)
                    {
                        return DateTimeToInt(searchDate);
                    }
                }
                searchDate = searchDate.AddDays(1);
            }
            return 0;
        }

        /// <summary>
        /// 获取每个月第四个星期三。
        /// </summary>
        private void GetForthWednesday()
        {
            DateTime firstDate = DateTime.Parse(IntToDateTime(myTradeDay[0]).ToString("yyyy-MM-01"));
            DateTime endDate = DateTime.Parse(IntToDateTime(myTradeDay[myTradeDay.Count - 1]).ToString("yyyy-MM-01")); IntToDateTime(myTradeDay[myTradeDay.Count - 1]);
            while (firstDate <= endDate)
            {
                int date = GetRecentTradeDay(GetSpecialDate(firstDate, 4, "Wednesday"));
                if (ForthWednesdayList.ContainsKey(firstDate.Year * 100 + firstDate.Month) == false)
                {
                    ForthWednesdayList.Add(firstDate.Year * 100 + firstDate.Month, date);
                }
                firstDate = firstDate.AddMonths(1);
            }

        }

        /// <summary>
        /// 获取每个月第三个星期五。
        /// </summary>
        private void GetThirdFriday()
        {
            DateTime firstDate = DateTime.Parse(IntToDateTime(myTradeDay[0]).ToString("yyyy-MM-01"));
            DateTime endDate = DateTime.Parse(IntToDateTime(myTradeDay[myTradeDay.Count - 1]).ToString("yyyy-MM-01")); IntToDateTime(myTradeDay[myTradeDay.Count - 1]);
            while (firstDate <= endDate)
            {
                int date = GetRecentTradeDay(GetSpecialDate(firstDate, 3, "Friday"));
                if (ThirdFridayList.ContainsKey(firstDate.Year * 100 + firstDate.Month) == false)
                {
                    ThirdFridayList.Add(firstDate.Year * 100 + firstDate.Month, date);
                }
                firstDate = firstDate.AddMonths(1);
            }

        }

        /// <summary>
        /// 判断今日是否是期权行权日。每月第四个星期三。如果不是交易日，顺延到下一个交易日。
        /// </summary>
        /// <param name="day">日期</param>
        /// <returns>是否是行权日</returns>
        public static bool IsOptionExerciseDate(int day)
        {
            DateTime today = IntToDateTime(day);
            if (day == ForthWednesdayList[today.Year * 100 + today.Month])
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 判断今日是否是金融期货的交割日。每月第三个星期五。如果不是交易日，顺延到下一个交易日。
        /// </summary>
        /// <param name="day">日期</param>
        /// <returns>是否是交割日</returns>
        public static bool IsFinacialFutureDeliveryDate(int day)
        {
            DateTime today = IntToDateTime(day);
            if (day == ThirdFridayList[today.Year * 100 + today.Month])
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 给出当前日期最近的交易日。如果今日是交易日返回今日，否者返回下一个最近的交易日。
        /// </summary>
        /// <param name="today">当前日期</param>
        /// <returns>交易日</returns>
        public static int GetRecentTradeDay(int today)
        {

            for (int i = 0; i < tradeDaysOfDataBase.Count - 1; i++)
            {
                if (tradeDaysOfDataBase[i] == today)
                {
                    return today;
                }
                if (tradeDaysOfDataBase[i] < today && tradeDaysOfDataBase[i + 1] >= today)
                {
                    return tradeDaysOfDataBase[i + 1];
                }
            }
            return 0;
        }
    }
}

