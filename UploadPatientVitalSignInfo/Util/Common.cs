
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UploadPatientVitalSignInfo.Log;

namespace UploadPatientVitalSignInfo.Util
{
    class Common
    {
        public static ConfigInfo LoadServiceConfig()
        {
            ConfigInfo config = new ConfigInfo();
            config.LogLevel = ConfigurationManager.AppSettings["LogLevel"].Trim().ToString();
            config.InInterval = Convert.ToInt32(ConfigurationManager.AppSettings["InInterval"].Trim().ToString());
            config.LogFilePath = string.Format("{0}\\{1}", AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["LogFilePath"].Trim().ToString());
            config.LogFileKeepDay = Convert.ToInt32(ConfigurationManager.AppSettings["LogFileKeepDay"].Trim().ToString());
            config.PostUrl = ConfigurationManager.AppSettings["PostUrl"].Trim().ToString();
            config.UploadUserCode = ConfigurationManager.AppSettings["UploadUserCode"].Trim().ToString();
            config.UploadUserName = ConfigurationManager.AppSettings["UploadUserName"].Trim().ToString();
            config.testUser = ConfigurationManager.AppSettings["testUser"].Trim().ToString();
            config.IccaConnectionString = ConfigurationManager.ConnectionStrings["ICCA"].ConnectionString.Trim();
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

        public string PostUrl { get; set; }

        public string UploadUserCode { get; set; }
        public string UploadUserName { get; set; }
        public string testUser { get; set; }

        public string IccaConnectionString { get; set; }


    }
}
