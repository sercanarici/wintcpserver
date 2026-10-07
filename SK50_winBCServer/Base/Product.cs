using FirebirdSql.Data.FirebirdClient;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using winTCPServer;

namespace SK50_Checker
{
    [Serializable]
    public class Product
    {
        private string _Title;
        private string _Barcode;
        private string _Description;
        private string _Price;
        private string _Price2;
        private Bitmap _Image;

        public string Title
        {
            get { return _Title; }
            set { _Title = value; }
        }

        public string Barcode
        {
            get
            {
                return _Barcode;
            }

            set
            {
                _Barcode = value;
            }
        }

        public string Description
        {
            get { return _Description; }
            set { _Description = value; }
        }

        public string Price
        {
            get { return _Price; }
            set { _Price = value; }
        }

        public string Price2
        {
            get { return _Price2; }
            set { _Price2 = value; }
        }

        public Bitmap Image
        {
            get { return _Image; }
            set { _Image = value; }
        }

        
    }

    class Products
    {
        // Cihaz birkaç saniye içinde yanıt alamazsa bağlantıyı bırakıyor; varsayılan 30 sn beklemek anlamsız.
        // Zaman aşımında cihaz en azından hata mesajını görür.
        private const int CommandTimeoutSeconds = 5;

        public Products()
        {

        }
        
        public static Product GetProductInfo(string _barCode)
        {
            Product p = new Product() { Barcode = _barCode, Title = "Uyarı!", Description = _barCode + " kodlu Ürün Bulunamadı.", Price = "", Price2 = "" };
            try
            {
                
                var con = SqlHelper.Baglanti;

                using (con)
                {
                    string cmdText = "";
                    var conType = con.GetType();
                    bool exact = Globals.BarcodeMatch == BarcodeMatchMode.Exact;

                    if (conType.Name == "FbConnection")
                    {
                        if (exact)
                        {
                            cmdText = $@"Select first 1 * from {Globals.ViewName} where barcode = @BarkodTam";
                        }
                        else
                        {
                            // Birden fazla kayıt eşleşirse önce birebir eşleşen, yoksa en kısa (en yakın) barkod seçilir.
                            cmdText = $@"Select first 1 * from {Globals.ViewName} where barcode like @Barkod
                                         order by case when barcode = @BarkodTam then 0 else 1 end, char_length(barcode)"; //isbn barkod için
                        }
                    }

                    if (conType.Name == "SqlConnection")
                    {
                        if (exact)
                        {
                            cmdText = $@"Select top 1 * from {Globals.ViewName} where barcode = @BarkodTam";
                        }
                        else
                        {
                            cmdText = $@"Select top 1 * from {Globals.ViewName} where barcode like @Barkod
                                         order by case when barcode = @BarkodTam then 0 else 1 end, len(barcode)";
                        }
                    }

                    var cmd = con.CreateCommand();
                    cmd.CommandText = cmdText;
                    cmd.CommandTimeout = CommandTimeoutSeconds;

                    using (cmd)
                    {
                        var prmTam = cmd.CreateParameter();
                        prmTam.ParameterName = "@BarkodTam";
                        prmTam.Value = _barCode;
                        cmd.Parameters.Add(prmTam);

                        if (!exact)
                        {
                            var prm = cmd.CreateParameter();
                            prm.ParameterName = "@Barkod";
                            //--6057719255  -- 9786057719256

                            //ISBN İÇİN AYAR: ISBN-10 okutulduğunda kontrol hanesi atılıp ISBN-13 kaydı içinde aranır.
                            if (_barCode.Length == 10)
                            {
                                _barCode = _barCode.Substring(0, 9);
                            }
                            prm.Value = "%" + _barCode + "%";

                            cmd.Parameters.Add(prm);
                        }

                        DataTable dt = SqlHelper.GetDataTable(cmd, "Products");

                        if (dt.Rows.Count > 0)
                        {
                            DataRow dr = dt.Rows[0];
                            p.Title = dr["TITLE"].ToString();
                            p.Barcode = dr["BARCODE"].ToString();
                            p.Description = dr["DESCRIPTION"].ToString();
                            p.Price = dr["PRICE1"].ToString();
                            p.Price2 = dr["PRICE2"].ToString();

                        }       
                    }
                }

                return p;
            }
            catch (Exception ex)
            {
                Logger.Write("GetProductInfo (" + _barCode + "): " + ex.Message);
                // Zaman aşımında cihazda uzun İngilizce hata yerine kısa bir Türkçe mesaj gösterilir.
                p.Description = IsTimeout(ex) ? TimeoutMessage : ex.Message;
                return p;
            }

        }

        private const string TimeoutMessage = "Sistem yanıt vermedi. Lütfen tekrar okutun.";

        private static bool IsTimeout(Exception ex)
        {
            // SQL Server: -2 = sorgu zaman aşımı. Firebird: 335544794 = isc_cancelled (CommandTimeout ile iptal).
            SqlException sqlEx = ex as SqlException;
            if (sqlEx != null && sqlEx.Number == -2)
            {
                return true;
            }
            FbException fbEx = ex as FbException;
            if (fbEx != null && fbEx.ErrorCode == 335544794)
            {
                return true;
            }
            return ex is TimeoutException;
        }

    }
}
