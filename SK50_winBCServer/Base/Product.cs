using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Xml;
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
        public Products()
        {

        }
        
        public static Product GetProductInfo(string _barCode)
        {
            string sql = string.Format("SELECT barcode, title, description, price1, price2 from {0} where barcode like @barcode",Globals.ViewName);
            SqlCommand cmd = new SqlCommand(sql, SqlHelper.Baglanti);
            cmd.Parameters.AddWithValue("@barcode", _barCode);
            DataTable dt =  SqlHelper.GetDataTable(cmd, "Product");

            if (dt.Rows.Count>0)
            {
                DataRow dr = dt.Rows[0];
                Product p = new Product();
                p.Title = dr["title"].ToString();
                p.Barcode = dr["barcode"].ToString();
                p.Description = dr["description"].ToString();
                p.Price = dr["price1"].ToString();
                p.Price2 = dr["price2"].ToString();
                return p;
            }
            return new Product { Barcode = _barCode, Title = "Uyarı!", Description = _barCode + " kodlu Ürün Bulunamadı.", Price = "", Price2 = "" };
        }

    }
}
