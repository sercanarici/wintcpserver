using System.Configuration;

namespace winTCPServer
{
    public class Globals
    {
        public static string ViewName
        {
            get
            {
               return ConfigurationManager.AppSettings["Viewname"].ToString();

            }
        }
    }
}
