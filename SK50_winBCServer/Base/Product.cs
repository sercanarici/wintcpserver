using System;
using System.Data;
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

                    // Eşleşen kayıt kümesi eskisiyle aynı (LIKE '%barkod%'), ancak birden fazla kayıt eşleşirse
                    // rastgele biri yerine önce birebir eşleşen, yoksa en kısa (en yakın) barkod seçilir.
                    if (conType.Name == "FbConnection")
                    {
                        //cmdText = $@"Select first 1 * from {Globals.ViewName} where barcode=@Barkod";
                        cmdText = $@"Select first 1 * from {Globals.ViewName} where barcode like @Barkod
                                     order by case when barcode = @BarkodTam then 0 else 1 end, char_length(barcode)"; //isbn barkod için

                    }

                    if (conType.Name == "SqlConnection")
                    {
                        cmdText = $@"Select top 1 * from {Globals.ViewName} where barcode like @Barkod
                                     order by case when barcode = @BarkodTam then 0 else 1 end, len(barcode)";
                    }

                    var cmd = con.CreateCommand();
                    cmd.CommandText = cmdText;
                    cmd.CommandTimeout = CommandTimeoutSeconds;

                    using (cmd)
                    {
                        var prmTam = cmd.CreateParameter();
                        prmTam.ParameterName = "@BarkodTam";
                        prmTam.Value = _barCode;

                        var prm = cmd.CreateParameter();
                        prm.ParameterName = "@Barkod";
                        //prm.Value = _barCode;
                        //--6057719255  -- 9786057719256

                        //ISBN İÇİN AYAR??
                        if (_barCode.Length == 10)
                        {
                            _barCode = _barCode.Substring(0, 9);
                        }
                        prm.Value = "%" + _barCode + "%";


                        cmd.Parameters.Add(prm);
                        cmd.Parameters.Add(prmTam);

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
                p.Description = ex.Message;
                return p;
            }
           
        }

    }
}
