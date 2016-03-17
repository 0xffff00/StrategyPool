using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace StrategyPool
{
    class DataTableApplication
    {
        public string connectionString, tableName;
        public DataTable myDataTable;
        public DataTableApplication(string connectionString,string tableName)
        {
            this.connectionString = connectionString;
            this.tableName = tableName;
        }
        public bool GetDataTable(string commandString)
        {
            myDataTable = new DataTable();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = commandString;
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(myDataTable);
                            return true;
                        }
                    }
                }

            }
            catch (Exception)
            {
                return false;
            }
        }

        public DataRow[] GetData(string commandString)
        {
            if (myDataTable!=null)
            {
                return myDataTable.Select(commandString);
            }
            return null;
        }

    }
}
