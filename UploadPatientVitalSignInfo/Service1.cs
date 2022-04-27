using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using UploadPatientVitalSignInfo.Log;
using UploadPatientVitalSignInfo.Model;
using UploadPatientVitalSignInfo.Util;

namespace UploadPatientVitalSignInfo
{
    public partial class Service1 : ServiceBase
    {
        private ConfigInfo config;
        private Timer timerMainCallback;
        private Object timerMainCallbackLock = new object();
        private bool m_IsPaused = false;
        private bool m_IsStopped = false;
        public Service1()
        {
            InitializeComponent();
            config = Common.LoadServiceConfig();
            timerMainCallback = new Timer();
            timerMainCallback.Interval = config.InInterval * 10;
            timerMainCallback.Enabled = true;
            timerMainCallback.Elapsed += new ElapsedEventHandler(OnTimerMainCallback);
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                LogUtil.DebugLog("Enter Service OnStart().");
                m_IsPaused = false;
                m_IsStopped = false;
                timerMainCallback.Enabled = true;
            }
            catch (Exception e)
            {
                LogUtil.ErrorLog(e.ToString());
                LogUtil.DebugLog("Error occurred on OnStart().");
            }
            finally
            {
                LogUtil.DebugLog("Exit Service OnStart().");
            }
        }

        protected override void OnStop()
        {
            LogUtil.DebugLog("Enter Service OnStop().");

            m_IsPaused = false;
            m_IsStopped = true;
            LogUtil.DebugLog("Exit Service OnStop().");
        }

        /// <summary>
        /// Timer response function.
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Event arguments</param>
        private void OnTimerMainCallback(object sender, EventArgs e)
        {
            lock (timerMainCallbackLock)
            {
                if (m_IsPaused || m_IsStopped)
                {
                    return;
                }

                try
                {
                    BuildPatientData();
                    timerMainCallback.Enabled = false;
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("OnTimerMainCallback error message:" + ex.ToString());
                }
                finally
                {
                    timerMainCallback.Enabled = true;
                }
            }
        }

        public void BuildPatientData()
        {
            LogUtil.DebugLog("开始获取数据");
            var patients = GetPatients();
            var result = new List<VitalSignModel>();
            foreach (var patient in patients)
            {
                LogUtil.DebugLog("患者:" + patient.ptEncounterId);
                var basicInfo = GetVitalSignBasicInfo(patient);
                var list = GetVitalSignDetailInfo(patient, basicInfo);
                if (list.Any())
                {
                    result.AddRange(list);
                }
            }
            LogUtil.DebugLog("总数据条数:" + result.Count);
            var jsonStr = JsonConvert.SerializeObject(result);
            LogUtil.DebugLog("数据内容:" + jsonStr);

            LogUtil.DebugLog("开始上传数据");

            var response = PostHttp("", jsonStr, "");
            LogUtil.DebugLog("返回结果");

        }

        public List<PatientModel> GetPatients()
        {
            var result = new List<PatientModel>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT  ptEncounterId, dbName  FROM v_icca_patients WHERE endDate IS NULL AND lifetimeNumber IS NOT NULL; ";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var patient = new PatientModel();
                        patient.ptEncounterId = reader["ptEncounterId"].ToString();
                        patient.dbName = reader["dbName"].ToString();
                        result.Add(patient);
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetPatients error message:" + ex.ToString());
                }
            }
            return result;
        }

        public VitalSignModel GetVitalSignBasicInfo(PatientModel patient)
        {
            var result = new VitalSignModel();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT  * FROM  v_" + patient.dbName + "_pt_intervention";
                sql += " WHERE  ptEncounterId ='" + patient.ptEncounterId + "'";
                sql += "and propName in ('lolidInt','PtHeight','admDateTimeInt','ptWeightIntervention')";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var propName = DBNull.Value == reader["propName"] ? "" : reader["propName"].ToString();
                        if (!string.IsNullOrWhiteSpace(propName))
                        {
                            switch (propName)
                            {
                                case "lolidInt":
                                    result.SYXH = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    break;
                                case "PtHeight":
                                    result.SG = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    break;
                                case "ptWeightIntervention":
                                    result.TZ = DBNull.Value == reader["value_t"] ? 0 : Convert.ToDouble(reader["value_t"]);
                                    break;
                                case "admDateTimeInt":
                                    result.ZYRQ = DBNull.Value == reader["value_t"] ? "" : Convert.ToDateTime(reader["value_t"]).ToString("yyyyMMdd");
                                    break;
                            }
                        }
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignBasicInfo error message:" + ex.ToString());
                }
            }
            return result;
        }

        public List<VitalSignModel> GetVitalSignDetailInfo(PatientModel patient, VitalSignModel vitalSign)
        {
            var result = new List<VitalSignModel>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT i.*, u.userDomainName, u.lastName  FROM  v_" + patient.dbName + "_pt_intervention I left join v_icca_users u on  i.userId=u.userId";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and propName in ('heartRateInt','temperatureInt','RespirationRateInt','FSBGInt','arterialBPInt','SpO2Int','UrineOutput24Int','totalIn24HrInt','TotalOut24hr')";
                sql += "and storeTime>'" + DateTime.Now.AddMinutes(-10).ToString("yyyy-MM-dd HH:mm") + "' ";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var propName = DBNull.Value == reader["propName"] ? "" : reader["propName"].ToString();
                        if (!string.IsNullOrWhiteSpace(propName))
                        {
                            var chartTime = reader["chartTime"].ToString();

                            if (chartTime.Contains("03:00") || chartTime.Contains("07:00") || chartTime.Contains("11:00") || chartTime.Contains("15:00") || chartTime.Contains("19:00") || chartTime.Contains("23:00"))
                            {
                            }
                            else
                            {
                                continue;
                            }
                            var data = new VitalSignModel();
                            data.SYXH = vitalSign.SYXH;
                            data.CJSJ = Convert.ToDateTime(chartTime).ToString("HH:mm");
                            data.JLSJ = Convert.ToDateTime(chartTime).ToString("yyyyMMddHH:mm:ss");
                            data.SG = vitalSign.SG;
                            data.TZ = vitalSign.TZ;
                            data.ZYRQ = string.IsNullOrEmpty(vitalSign.ZYRQ) ? "" : vitalSign.ZYRQ;
                            data.SCBD = 0;
                            data.YEXH = "0";
                            data.KSDM = "3059";
                            data.KSMC = "重症医学科";
                            data.BQDM = "9034";
                            data.BQMC = "重症医学科病区";
                            data.CZYH= DBNull.Value == reader["userDomainName"] ? "" : reader["userDomainName"].ToString();
                            data.CZYM= DBNull.Value == reader["lastName"] ? "" : reader["lastName"].ToString();
                            switch (propName)
                            {
                                case "heartRateInt":
                                    data.XL = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    //data.MB 脉搏就是心律
                                    break;
                                case "temperatureInt":
                                    data.TW = DBNull.Value == reader["value_t"] ? 0 : Convert.ToDouble(reader["value_t"]);
                                    data.CLFS = 1;
                                    break;
                                case "RespirationRateInt":
                                    data.HX = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    break;
                                case "arterialBPInt":
                                    var abp = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    data.SSY = Convert.ToInt32(abp.Split('/')[0]);
                                    data.SZY = Convert.ToInt32(abp.Split('/')[1].Split('(')[0]);
                                    data.PJY = Convert.ToInt32(abp.Split('/')[1].Split('(')[1].Replace(")", ""));
                                    break;

                                case "FSBGInt":
                                    data.XT = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    break;
                                case "SpO2Int":
                                    data.XYBHD = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    break;
                                case "UrineOutput24Int":
                                    var urine = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    data.NL = Convert.ToInt32(urine.Split('(')[1].Replace(")", ""));
                                    break;
                                case "totalIn24HrInt":
                                    var totalIn = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    data.RLZH = Convert.ToInt32(totalIn.Split('(')[1].Replace(")", ""));
                                    break;
                                case "TotalOut24hr":
                                    var totalOut = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    data.CLZH = Convert.ToInt32(totalOut.Split('(')[1].Replace(")", ""));
                                    break;
                            }
                            result.Add(data);
                        }
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignDetailInfo error message:" + ex.ToString());
                }
            }
            return result;
        }

        public string PostHttp(string url, string body, string contentType)
        {
            var responseData = "";
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            if (contentType == "xml")
            {
                contentType = "application/xml;charset=UTF-8";
            }
            else
            {
                contentType = "application/json;charset=UTF-8";
            }
            httpWebRequest.ContentType = contentType;
            httpWebRequest.Method = "POST";
            httpWebRequest.Timeout = 20000;
            byte[] btBodys = Encoding.UTF8.GetBytes(body);
            httpWebRequest.ContentLength = btBodys.Length;
            using (Stream reqStream = httpWebRequest.GetRequestStream())
            {
                reqStream.Write(btBodys, 0, btBodys.Length);
            }
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    responseData = reader.ReadToEnd();
                }

            }
            return responseData;
        }
    }

    
}
