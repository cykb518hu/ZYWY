using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZWUDataIntegration.Util
{
    public class LogUtil
    {
        /// <summary>
        /// The lock of the error log file.
        /// </summary>
        private static Object m_ObjLockErrorLog = new Object();
        /// <summary>
        /// Write a error type's log message.
        /// </summary>
        /// <param name="strLogMessage">The message that describes the log.</param>
        public static void ErrorLog(string strLogMessage)
        {

            // Determine whether the LogUtil class is Initiated.
            // Sets log's information
            LogInfo logInfo = new LogInfo();

            logInfo.M_LogDateTime = DateTime.Now;
            logInfo.M_LogMessage = strLogMessage;

            // Write error.log
            lock (m_ObjLockErrorLog)
            {
                LogFile lfErrorLog = new LogFile();
                lfErrorLog.M_LogFilePath = m_LogFilePath;
                lfErrorLog.M_LogFileName = string.Format("{0}-{1}", DateTime.Now.ToString("yyyy-MM-dd"), LOG_ERROR);
                lfErrorLog.M_LogFileKeepTime = m_LogKeepTime;
                lfErrorLog.ErrorLog(logInfo);
            }
        }
    }
    public class LogInfo
    { /// <summary>
      /// Initializes a new instance of the LogInfo class.
      /// </summary>
        public LogInfo()
        {

        }

        /// <summary>
        /// Initializes a new instance of the LogInfo class with a time and a message.
        /// </summary>
        /// <param name="dtLogDateTime">The time of the log record.</param>
        /// <param name="strLogMessage">The message that describes the log.</param>
        public LogInfo(DateTime dtLogDateTime, string strLogMessage)
        {
            M_LogDateTime = dtLogDateTime;
            M_LogMessage = strLogMessage;
        }

        /// <summary>
        /// The time of the log record.
        /// </summary>
        private DateTime m_LogDateTime;

        /// <summary>
        /// Gets or sets the time of the log record.
        /// </summary>
        /// <value>A System.DateTime that records the time of the log.</value>
        public DateTime M_LogDateTime
        {
            get
            {
                return m_LogDateTime;
            }
            set
            {
                m_LogDateTime = value;
            }
        }

        /// <summary>
        /// The message that describes the log.
        /// </summary>
        private string m_LogMessage;

        /// <summary>
        /// Gets or sets the message that describes the log.
        /// </summary>
        /// <value>The message that describes the log.</value>
        public string M_LogMessage
        {
            get
            {
                return m_LogMessage;
            }
            set
            {
                m_LogMessage = value;
            }
        }

        /// <summary>
        /// Creates and returns a string representation of the log information.
        /// </summary>
        /// <returns>A string representation of the log information.</returns>
        public override string ToString()
        {
            string str = "";

            str += "[";
            str += M_LogDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            str += "]:";
            str += M_LogMessage;

            return str;
        }

    }
}
