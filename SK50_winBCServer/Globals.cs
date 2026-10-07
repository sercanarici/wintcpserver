using System;
using System.Configuration;

namespace winTCPServer
{
    public enum BarcodeMatchMode
    {
        // Birebir eşleşme (barcode = @Barkod). Hızlı; ISBN kullanmayan yerler için.
        Exact,
        // İçerir (barcode LIKE '%barkod%'). ISBN-10 okutulup ISBN-13 kayıtlı olan yerler için.
        Contains
    }

    public class Globals
    {
        public static string ViewName
        {
            get
            {
               return ConfigurationManager.AppSettings["Viewname"].ToString();

            }
        }

        // App.config: <add key="BarcodeMatch" value="Equals" /> veya "Contains".
        // Ayar yoksa ya da geçersizse eski davranış (Contains) korunur.
        public static BarcodeMatchMode BarcodeMatch
        {
            get
            {
                string value = ConfigurationManager.AppSettings["BarcodeMatch"];
                return string.Equals(value, "Equals", StringComparison.OrdinalIgnoreCase)
                    ? BarcodeMatchMode.Exact
                    : BarcodeMatchMode.Contains;
            }
        }
    }
}
