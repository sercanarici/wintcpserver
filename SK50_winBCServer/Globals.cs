using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
