using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace StrategyPool
{
    /// <summary>
    /// 记录文件操作的类
    /// </summary>
    class DocumentApplication
    {
        /// <summary>
        /// 将字符串写入csv文档
        /// </summary>
        /// <param name="filePathName">csv文档路径</param>
        /// <param name="ls">需存储的字符串列表</param>
        public static void WriteCsv(string filePathName, List<string[]> ls)
        {
            WriteCsv(filePathName, false, ls);
        }
        /// <summary>
        /// 将字符串写入csv文档
        /// </summary>
        /// <param name="filePathName">csv文档路径</param>
        /// <param name="append">判断是否是尾部添加的模式</param>
        /// <param name="ls">需存储的字符串列表</param>
        public static void WriteCsv(string filePathName, bool append, List<string[]> ls)
        {
            StreamWriter fileWriter = new StreamWriter(filePathName, append, Encoding.Default);
            foreach (string[] strArr in ls)
            {
                fileWriter.WriteLine(string.Join(",", strArr));
            }
            fileWriter.Flush();
            fileWriter.Close();
        }
        /// <summary>
        /// 读取csv文档
        /// </summary>
        /// <param name="filePathName">读取文档的地址</param>
        /// <returns>返回读取的字符串列表</returns>
        public static List<string[]> ReadCsv(string filePathName)
        {
            List<string[]> ls = new List<string[]>();
            StreamReader fileReader = new StreamReader(filePathName);
            string strLine = "";
            while (strLine != null)
            {
                strLine = fileReader.ReadLine();
                if (strLine != null && strLine.Length > 0)
                {
                    ls.Add(strLine.Split(','));
                }
            }
            fileReader.Close();
            return ls;
        }
    }
}
