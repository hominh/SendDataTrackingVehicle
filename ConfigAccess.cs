using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace ServiceSendDataGSHT
{
    class ConfigAccess
    {
        public static string GetDatabaseUsername()
        {
            return ConfigurationSettings.AppSettings["DB_USERNAME"].Trim();
        }
        public static string GetDatabasePassword()
        {
            return ConfigurationSettings.AppSettings["DB_PASSWD"].Trim();
        }

        public static string GetDatabaseSchema()
        {
            return ConfigurationSettings.AppSettings["DB_SCHEMA"].Trim();
        }

        public static string GetDatabasePort()
        {
            return ConfigurationSettings.AppSettings["DB_PORT"].Trim();
        }

        public static string GetDatabaseHost()
        {
            return ConfigurationSettings.AppSettings["DB_HOST"].Trim();
        }
    }
}
