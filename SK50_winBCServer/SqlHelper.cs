using FirebirdSql.Data.FirebirdClient;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace winTCPServer
{
    public class SqlHelper
    {
        public static IDbConnection Baglanti
        {
            get
            {
                IDbConnection con = null;
                string connStr = ConfigurationManager.ConnectionStrings["baglanti"].ConnectionString;

                var dbType = ConfigurationManager.AppSettings["DbType"];

                if (dbType == null)
                {
                    con = new SqlConnection(connStr);
                }
                else if (dbType.ToString() == "mssql")
                {
                    con = new SqlConnection(connStr);
                }
                else if (dbType.ToString() == "firebird")
                {
                    con = new FbConnection(connStr);
                }

                return con;

            }
        }


        public static decimal ExecuteScalar(IDbCommand komut)
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

        public static bool ExecuteNonQuery(IDbCommand komut)
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

        public static DataTable GetDataTable(IDbCommand komut, string tableName = "Tablo")
        {
            DataTable dt = null;
            try
            {
                if (komut.Connection.State != ConnectionState.Open)
                {
                    komut.Connection.Open();
                }

                using (var reader = komut.ExecuteReader())
                {
                    // Kolon adı karşılaştırması tablonun Locale'ini kullanır; tr-TR'de "TITLE" ile "title"
                    // eşleşmiyor (I/ı sorunu), bu yüzden kültürden bağımsız karşılaştırma yapılır.
                    dt = new DataTable(tableName) { Locale = CultureInfo.InvariantCulture };
                    while (!reader.IsClosed)
                    {
                        dt.Load(reader);
                    }
                }

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

        public static DataTable GetDataTableMsSql(SqlCommand komut, string tableName = "Tablo")
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

    }
}
