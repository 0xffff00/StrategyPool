/*
##############################################
策略仓库的建立。
作者：毛衡
版本：v1.0.0
日期：2016年3月17日。
备注：从期权跨期价差（calendar spread）出发，构建
自己的策略仓库。今后可以不限于期权策略。
##############################################
利用DataTable数据格式获取数据库数据。
作者：毛衡
版本：v1.0.1
日期：2016年3月17日。
##############################################
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace StrategyPool
{
    class Program
    {
        static void Main(string[] args)
        {
            DataTableApplication myTable = new DataTableApplication(Configuration.connectionString, "sh510050");
            myTable.GetDataTable("select * from sh10000011 where [Date]=20150209");
            DataRow[] dtrow = myTable.GetData("Date=20150209");
        }
    }
}
