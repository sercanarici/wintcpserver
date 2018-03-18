using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Configuration;

namespace winTCPServer
{
    public class SqlHelper
    {
        public static SqlConnection Baglanti
        {
            get
            {
                return new SqlConnection(ConfigurationManager.ConnectionStrings["baglanti"].ConnectionString);
            }
        }

        public static DataTable GetDataTable(SqlCommand komut, string tableName = "Tablo")
        {
            DataTable dt = null;
            try
            {
                if (komut.Connection.State != ConnectionState.Open)
                {
                    komut.Connection.Open();
                }
                SqlDataAdapter dap = new SqlDataAdapter(komut);
                dt = new DataTable(tableName);
                dap.Fill(dt);
                return dt;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (komut.Connection.State != ConnectionState.Closed)
                {
                    komut.Connection.Close();
                }
            }
        }

        public static object GetSqlValue(object obj)
        {
            return obj == null ? DBNull.Value : obj;

        }

        public static bool? ToBoolean(object obj)
        {
            return obj == DBNull.Value ? (bool?)null : Convert.ToBoolean(obj);
        }

        public static decimal ExecuteScalar(SqlCommand komut)
        {
            try
            {
                if (komut.Connection.State != ConnectionState.Open)
                    komut.Connection.Open();
                return Convert.ToDecimal(komut.ExecuteScalar());
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (komut.Connection.State != ConnectionState.Closed)
                    komut.Connection.Close();
            }
        }

        public static bool ExecuteNonQuery(SqlCommand komut)
        {
            try
            {
                if (komut.Connection.State != ConnectionState.Open)
                    komut.Connection.Open();
                return komut.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (komut.Connection.State != ConnectionState.Closed)
                    komut.Connection.Close();
            }
        }
    }
}
