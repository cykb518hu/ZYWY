using CSH.Interface.Service.Log;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZWUDataIntegration.Util
{
    class Common
    {
        public static ConfigInfo LoadServiceConfig()
        {
            ConfigInfo config = new ConfigInfo();

            config.InInterval = Convert.ToInt32(ConfigurationManager.AppSettings["InInterval"].Trim().ToString());
            config.LogLevel = ConfigurationManager.AppSettings["LogLevel"].Trim().ToString();
            config.LogFilePath = string.Format("{0}\\{1}", AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["LogFilePath"].Trim().ToString());
            config.LogFileKeepDay = Convert.ToInt32(ConfigurationManager.AppSettings["LogFileKeepDay"].Trim().ToString());

            config.IccaConnectionString = ConfigurationManager.ConnectionStrings["ICCA"].ConnectionString.Trim();
            config.PingTaiConnectionString = ConfigurationManager.ConnectionStrings["Pingtai"].ConnectionString.Trim();

            LogUtil.Initialize(config.LogFilePath, config.LogLevel, config.LogFileKeepDay);

            return config;
        }

    }

    public class ConfigInfo
    {
        public int InInterval { get; set; }
        public string LogFilePath { get; set; }
        public string LogLevel { get; set; }
        public int LogFileKeepDay { get; set; }

        public string IccaConnectionString { get; set; }

        public string PingTaiConnectionString { get; set; }


    }
}
