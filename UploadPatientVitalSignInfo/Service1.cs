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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using UploadPatientVitalSignInfo.Log;
using UploadPatientVitalSignInfo.Model;
using UploadPatientVitalSignInfo.Util;

namespace UploadPatientVitalSignInfo
{
    ///
    public partial class Service1 : ServiceBase
    {
        private ConfigInfo config;
        //private Timer timerMainCallback;
        //private Object timerMainCallbackLock = new object();
        //private bool m_IsPaused = false;
        //private bool m_IsStopped = false;
        private List<string> timeLines = new List<string>();
        public Service1()
        {
           // InitializeComponent();
            config = Common.LoadServiceConfig();
            timeLines.Add("03:00");
            timeLines.Add("07:00");
            timeLines.Add("11:00");
            timeLines.Add("15:00");
            timeLines.Add("19:00");
            timeLines.Add("23:00");

            // timerMainCallback = new Timer();
            // timerMainCallback.Interval = config.InInterval * 60000;
            //timerMainCallback.Enabled = true;
            // timerMainCallback.Elapsed += new ElapsedEventHandler(OnTimerMainCallback);
            // timerMainCallback.Elapsed += new ElapsedEventHandler(OnTimerMainCallback);
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                LogUtil.DebugLog("Enter Service OnStart().");
                //m_IsPaused = false;
                //m_IsStopped = false;
                //timerMainCallback.Enabled = true;
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

            //m_IsPaused = false;
            //m_IsStopped = true;
            LogUtil.DebugLog("Exit Service OnStop().");
        }

        /// <summary>
        /// Timer response function.
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Event arguments</param>
        public void BuildPatientData()
        {
            /*
             * 03:00 07:00 11:00 15:00 19:00 23:00 

1. 体温，呼吸，心率： 取整点数据上传
2. 血压： 传入科第一笔NBP数据，间隔7天上传，没笔取当天第一笔数据
3. 尿量 总入量统计：取当天早上7点的数据作为当天数据，如果入科时间晚于7点，ICCA 没有数据就不穿
4. 大便次数：统计时间 前一天8点到今天早上7点，如果有量，算做一次，作为当天数据。  2022-08-19  要求满24 小时， 不满传0 , 没有大便也传0 ，大于等于5 传* ，有灌肠 格式为1/E
5. 事件上传：转入，转出，死亡，出院(死亡，出院根据出科转归字段判断)。 转出，死亡，出院三者互斥
6. 机械通气：开始机械通气标准是通气时间字段为0，要求填写的时候开始时间和表格时间一致这样通气时间值为0，停机械通气判断标准是是否有停止时间

注意 事件 机械通气 上传时必须和整点时间挂钩，目前逻辑是向后取离记录时间最近的整点时间。 
例如 2022-07-09 09:00:00 上传时间为  2022-07-09 11:00:00
例如 2022-07-09 12:00:00 上传时间为  2022-07-09 15:00:00
             * */
            var startTime = DateTime.Now.AddHours(config.InInterval);
            LogUtil.DebugLog("开始获取数据");
            var patients = GetPatients();
            foreach (var patient in patients)
            {
                CheckViewExist(patient);
                var vitalSignList = new List<VitalSignModel>();
                var basicInfo = GetVitalSignBasicInfo(patient);
                LogUtil.DebugLog("患者:" + basicInfo.SYXH + " 入院时间:" + basicInfo.RYSJ + " 入科时间:" + basicInfo.RKSJ);
                
                if (patient.endDate == DateTime.MaxValue)
                {
                    //已经出科的患者不取这些数据
                    var list = GetVitalSign(patient, startTime);
                    if (list.Any())
                    {
                        vitalSignList.AddRange(list);
                    }
                    var weeklyNBPList = GetVitalSignByWeekNBP(patient, basicInfo,startTime);
                    if (weeklyNBPList.Any())
                    {
                        vitalSignList.AddRange(weeklyNBPList);
                    }
                    var weeklyTZList = GetVitalSignByWeekTZ(basicInfo);
                    if (weeklyTZList.Any())
                    {
                        vitalSignList.AddRange(weeklyTZList);
                    }
                    var listSummary = GetVitalSignSummary(patient, basicInfo, startTime);

                    if (listSummary.Any())
                    {
                        vitalSignList.AddRange(listSummary);
                    }
                    var listStool = GetVitalSignStoolNum(patient, basicInfo);
                    if (listStool.Any())
                    {
                        vitalSignList.AddRange(listStool);
                    }
                    var ventList = GetVitalSignVent(patient, startTime,basicInfo);
                    if (ventList.Any())
                    {
                        vitalSignList.AddRange(ventList);
                    }
                    var freeList = GetVitalSignFree(patient, startTime);
                    if (freeList.Any())
                    {
                        vitalSignList.AddRange(freeList);
                    }
                }
                //已经出科的患者需要去出院出科事件
                var eventList = GetVitalSignEvent(patient, startTime);
                if (eventList.Any())
                {
                    vitalSignList.AddRange(eventList);
                }
             
                foreach (var r in vitalSignList)
                {
                   // continue;
                    r.YEXH = "0";
                    r.CZYH = config.UploadUserCode;
                    r.CZYM = config.UploadUserName;
                    r.SCBD = 0;
                    r.KSDM = "3059";
                    r.KSMC = "重症医学科";
                    r.BQDM = "9034";
                    r.BQMC = "重症医学科病区";
                  //  r.SYXH = "162794";
                    r.SYXH = basicInfo.SYXH;
                    r.SG = basicInfo.SG;

                }
                if (vitalSignList.Any())
                {
                    var jSetting = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                    var jsonStr = JsonConvert.SerializeObject(vitalSignList, jSetting);
                    LogUtil.DebugLog("数据内容:" + jsonStr);

                    LogUtil.DebugLog("开始上传数据，上传地址:" + config.PostUrl);
                    var response =  PostHttp(config.PostUrl, jsonStr, "");
                    LogUtil.DebugLog("返回结果:" + response);
                }
                else
                {
                    LogUtil.WarningLog("没有数据需要上传");
                }
            }        
        }

        public List<PatientModel> GetPatients()
        {
            
            var result = new List<PatientModel>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                //and ptEncounterId='D20CC276-C25D-44E1-9312-947947D0E42C' 徐科鹏 and patientName not like '%test%' and patientName not like N'%测试%'
                var sql = @"  SELECT  ptEncounterId, dbName, endDate  FROM v_icca_patients WHERE lifetimeNumber IS NOT NULL and (endDate IS NULL or endDate> DATEADD(HOUR,-5, GETDATE()))
 and (ptEncounterId in('444C57C7-782E-4CA7-B1C9-D97CB46855CA','E2B2C26A-E36B-4AB2-AC39-845FA976EE3C') or startDate>'2022-08-05')";
               // sql = @"SELECT  ptEncounterId, dbName, endDate  FROM v_icca_patients WHERE lifetimeNumber IS NOT NULL and ptEncounterId='" + config.testUser + "'";
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
                        patient.endDate = DBNull.Value == reader["endDate"] ? DateTime.MaxValue : Convert.ToDateTime(reader["endDate"]);
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

        public void CheckViewExist(PatientModel patient)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
                {
                    var sql = @" select table_name from  INFORMATION_SCHEMA.VIEWS where table_name='v_" + patient.dbName + "_pt_intervention'";
                    SqlCommand command = new SqlCommand(sql, connection);
                    connection.Open();
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {

                    }
                    else
                    {
                        LogUtil.WarningLog("视图不存在，需要创建视图");
                        Process.Start(@"http://172.16.200.37:3080/reports/1?userId=CF580CEE-FCA5-413C-943B-819247AB219C&encounterId=" + patient.ptEncounterId);
                        Thread.Sleep(10);
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("CheckViewExist error message:" + ex.ToString());
            }
        }
        /// <summary>
        /// 获取患者的一些基本信息
        /// </summary>
        /// <param name="patient"></param>
        /// <returns></returns>
        public VitalSignModel GetVitalSignBasicInfo(PatientModel patient)
        {
            var result = new VitalSignModel();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT  * FROM  v_" + patient.dbName + "_pt_intervention";
                sql += " WHERE  ptEncounterId ='" + patient.ptEncounterId + "'";
                sql += "and propName in ('encounterIDInt','PtHeight','DemographicOther5','ptWeightIntervention','admDateTimeInt')";
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
                                case "encounterIDInt":
                                    result.SYXH = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    break;
                                case "PtHeight":
                                    result.SG = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    break;
                                case "ptWeightIntervention":
                                    float tz = 0;
                                    float.TryParse(reader["value_t"].ToString(), out tz);
                                    result.TZ = tz;
                                    break;
                                case "DemographicOther5":
                                     result.RKSJ = DBNull.Value == reader["value_t"] ? "" : Convert.ToDateTime(reader["value_t"]).ToString("yyyy-MM-dd HH:mm:ss");
                                    break;
                                case "admDateTimeInt":
                                    result.RYSJ = DBNull.Value == reader["value_t"] ? "" : Convert.ToDateTime(reader["value_t"]).ToString("yyyy-MM-dd HH:mm:ss");
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

        
        /// <summary>
        /// 基本体温数据 按时间点传
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSign(PatientModel patient, DateTime startTime)
        {

            var firstList = GetFirstVitalSign(patient);
            var result = new List<VitalSignModel>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention ";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and propName in ('heartRateInt','temperatureInt','RespirationRateInt')";
                sql += "and storeTime>'" + startTime.ToString("yyyy-MM-dd HH:mm") + "' ";
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
                            var chartTime = Convert.ToDateTime(reader["chartTime"]).ToString("yyyy-MM-dd HH:mm");
                            if (chartTime.Contains("03:00") || chartTime.Contains("07:00") || chartTime.Contains("11:00") || chartTime.Contains("15:00") || chartTime.Contains("19:00") || chartTime.Contains("23:00"))
                            {
                                // flag = true;
                            }
                            else
                            {
                                continue;
                            }
                            var data = new VitalSignModel();
                            data.ZYRQ = Convert.ToDateTime(reader["chartTime"]).ToString("yyyyMMdd");
                            data.CJSJ = Convert.ToDateTime(reader["chartTime"]).ToString("HH:mm");
                            data.LRRQ = Convert.ToDateTime(reader["chartTime"]).ToString("yyyyMMddHH:mm:ss");
                            data.JLSJ = Convert.ToDateTime(reader["storeTime"]).ToString("yyyyMMddHH:mm:ss");
                            switch (propName)
                            {
                                case "heartRateInt":
                                    data.XL = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    break;
                                case "temperatureInt":
                                    data.TW = DBNull.Value == reader["value_t"] ? 0 : Convert.ToSingle(reader["value_t"]);
                                    data.CLFS = 1;
                                    break;
                                case "RespirationRateInt":
                                    data.HX = DBNull.Value == reader["value_t"] ? 0 : Convert.ToInt32(reader["value_t"]);
                                    break;
                            }
                            result.Add(data);
                        }
                    }
                    reader.Close();

                    foreach (var first in firstList)
                    {
                        if (first.XL != null && first.XL > 0)
                        {
                            var item = result.FirstOrDefault(x => x.ZYRQ == first.ZYRQ && x.CJSJ == first.CJSJ && x.XL != null && x.XL > 0);
                            if (item != null)
                            {
                                item.XL = first.XL;
                                item.JLSJ = first.JLSJ;
                            }
                            else
                            {
                                result.Add(first);
                            }
                        }
                        if (first.TW != null && first.TW > 0)
                        {
                            var item = result.FirstOrDefault(x => x.ZYRQ == first.ZYRQ && x.CJSJ == first.CJSJ && x.TW != null && x.TW > 0);
                            if (item != null)
                            {
                                item.TW = first.TW;
                                item.JLSJ = first.JLSJ;
                            }
                            else
                            {
                                result.Add(first);
                            }
                        }
                        if (first.HX != null && first.HX > 0)
                        {
                            var item = result.FirstOrDefault(x => x.ZYRQ == first.ZYRQ && x.CJSJ == first.CJSJ && x.HX != null && x.HX > 0);
                            if (item != null)
                            {
                                item.HX = first.HX;
                                item.JLSJ = first.JLSJ;
                            }
                            else
                            {
                                result.Add(first);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignDetailInfo error message:" + ex.ToString());
                }
            }
            return result;
        }

        public List<VitalSignModel> GetFirstVitalSign(PatientModel patient)
        {
            var result = new List<VitalSignModel>();
            try
            {
                var firstList = new List<VitalEntity>();
                using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
                {
                    var sql = @"  SELECT top 500 * FROM  v_" + patient.dbName + "_pt_intervention ";
                    sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                    sql += "and propName in ('heartRateInt','temperatureInt','RespirationRateInt')";
                    sql += " order by chartTime asc ";
                    SqlCommand command = new SqlCommand(sql, connection);

                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var data = new VitalEntity();
                        data.ItemName = reader["propName"].ToString();
                        data.ItemValue = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                        data.ChartTime = Convert.ToDateTime(reader["chartTime"]);
                        data.StoreTime = Convert.ToDateTime(reader["storeTime"]);
                        firstList.Add(data);

                    }
                    reader.Close();
                    firstList = firstList.OrderBy(x => x.ChartTime).ToList();

                }
                var firstHeartRateInt = firstList.FirstOrDefault(x => x.ItemName == "heartRateInt");
                if (firstHeartRateInt != null)
                {
                    var uploadTime = GetUploadTime(firstHeartRateInt.ChartTime);
                    var record = new VitalSignModel();
                    record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                    record.CJSJ = uploadTime.ToString("HH:mm");
                    record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                    record.JLSJ = firstHeartRateInt.StoreTime.ToString("yyyyMMddHH:mm:ss");
                    record.XL = Convert.ToInt32(firstHeartRateInt.ItemValue);
                    result.Add(record);
                }
                var firstTemperatureInt = firstList.FirstOrDefault(x => x.ItemName == "temperatureInt");
                if (firstTemperatureInt != null)
                {
                    var uploadTime = GetUploadTime(firstTemperatureInt.ChartTime);
                    var record = new VitalSignModel();
                    record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                    record.CJSJ = uploadTime.ToString("HH:mm");
                    record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                    record.JLSJ = firstTemperatureInt.StoreTime.ToString("yyyyMMddHH:mm:ss");
                    record.TW = Convert.ToSingle(firstTemperatureInt.ItemValue);
                    result.Add(record);
                }
                var firstRespirationRateInt = firstList.FirstOrDefault(x => x.ItemName == "RespirationRateInt");
                if (firstRespirationRateInt != null)
                {
                    var uploadTime = GetUploadTime(firstRespirationRateInt.ChartTime);
                    var record = new VitalSignModel();
                    record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                    record.CJSJ = uploadTime.ToString("HH:mm");
                    record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                    record.JLSJ = firstRespirationRateInt.StoreTime.ToString("yyyyMMddHH:mm:ss");
                    record.HX = Convert.ToInt32(firstRespirationRateInt.ItemValue);
                    result.Add(record);
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("GetFirstVitalSign error message:" + ex.ToString());
            }
            return result;
        }

        /// <summary>
        /// 以入院时间为开始时间，间隔7天传的数据 NBP 取最早一笔 早上3点
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="basicInfo"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSignByWeekNBP(PatientModel patient, VitalSignModel basicInfo, DateTime startTime)
        {
            var result = new List<VitalSignModel>();
            var nbpList = new List<VitalEntity>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention ";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += " and propName in ('nonInvasiveBPInt')";
                sql += " and storeTime>'" + startTime.ToString("yyyy-MM-dd") + "' ";
                sql += " order by chartTime asc ";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var chartTime = Convert.ToDateTime(reader["chartTime"]).ToString("yyyy-MM-dd HH:mm");
                        if (chartTime.Contains("03:00"))
                        {
                            
                            var days = (Convert.ToDateTime(reader["chartTime"]).Date - Convert.ToDateTime(basicInfo.RYSJ).Date).TotalDays;
                            if (days % 7 == 0)
                            {
                                var uploadTime = GetUploadTime(Convert.ToDateTime(reader["chartTime"]));
                                var record = new VitalSignModel();
                                record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                                record.CJSJ = uploadTime.ToString("HH:mm");
                                record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                                record.JLSJ = Convert.ToDateTime(reader["storeTime"]).ToString("yyyyMMddHH:mm:ss");
                                record.XY = reader["value_t"].ToString().Split('(')[0].Trim();
                                result.Add(record);
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignByWeek error message:" + ex.ToString());
                }
            }


            return result;
        }

        /// <summary>
        /// 以入院时间为开始时间，间隔7天传的数据体重
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="basicInfo"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSignByWeekTZ( VitalSignModel basicInfo)
        {
            var result = new List<VitalSignModel>();
            try
            {
                var zyrq = Convert.ToDateTime(basicInfo.RYSJ);
                var days = (DateTime.Today - Convert.ToDateTime(basicInfo.RYSJ).Date).TotalDays;
                if (days % 7 == 0)
                {
                    var uploadTime = GetUploadTime(DateTime.Today);
                    var record = new VitalSignModel();
                    record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                    record.CJSJ = uploadTime.ToString("HH:mm");
                    record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                    record.JLSJ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                    record.sPf = "卧床";
                    result.Add(record);
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("GetVitalSignByWeekTZ error message:" + ex.ToString());
            }

            return result;
        }

    

        /// <summary>
        /// 尿量和总入量统计
        /// 1. 取当天早上7点的数据作为当天数据（这个数据其实是昨天的统计）
        /// 2. 如果当天入科时间晚于7点，就不穿
        /// 3. 入科满24小时才传
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSignSummary(PatientModel patient, VitalSignModel basicInfo, DateTime startTime)
        {
            var urineTube = GetVitalSignUrineTube(patient);
            var result = new List<VitalSignModel>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention ";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and propName in ('UrineOutput24Int','totalIn24HrInt','GastroDecompres24Int','DrainOutput24hrInt')";
                sql += "and storeTime>='" + startTime.ToString("yyyy-MM-dd HH:mm") + "' ";// and chartTime<'" + startTime.AddDays(1).ToString("yyyy-MM-dd HH:mm") + "' ";
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
                            var chartTime = Convert.ToDateTime(reader["chartTime"]).ToString("yyyy-MM-dd HH:mm");
                            //LogUtil.ErrorLog(chartTime+" "+ basicInfo.ZYRQ+" "+(Convert.ToDateTime(chartTime) - Convert.ToDateTime(basicInfo.ZYRQ)).Hours.ToString());
                            if (chartTime.Contains("07:00") && (Convert.ToDateTime(chartTime) - Convert.ToDateTime(basicInfo.RKSJ)).TotalHours >= 24)
                            {

                            }
                            else
                            {
                                continue;
                            }
                            var data = new VitalSignModel();
                            data.CJSJ = Convert.ToDateTime(reader["chartTime"]).ToString("HH:mm");
                            data.ZYRQ = Convert.ToDateTime(reader["chartTime"]).ToString("yyyyMMdd");
                            data.LRRQ = Convert.ToDateTime(reader["chartTime"]).ToString("yyyyMMddHH:mm:ss");
                            data.JLSJ = Convert.ToDateTime(reader["storeTime"]).ToString("yyyyMMddHH:mm:ss");
                            var qtOut = 0;
                            switch (propName)
                            {
                                case "UrineOutput24Int":
                                    var urine = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    if (!string.IsNullOrWhiteSpace(urine) && urine.Split('(')[1].Replace(")", "") != "0")
                                    {
                                        data.NL = urine.Split('(')[1].Replace(")", "");
                                        if (urineTube)
                                        {
                                            data.NL = data.NL + "/C";
                                        }
                                        result.Add(data);
                                    }
                                    break;
                                case "totalIn24HrInt":
                                    var totalIn = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    if (!string.IsNullOrWhiteSpace(totalIn) && totalIn.Split('(')[1].Replace(")", "") != "0")
                                    {
                                        data.RLZH = totalIn.Split('(')[1].Replace(")", "");
                                        result.Add(data);
                                    }
                                    break;
                                case "GastroDecompres24Int":
                                    var gastroOut = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    if (!string.IsNullOrWhiteSpace(gastroOut) && gastroOut.Split('(')[1].Replace(")", "") != "0")
                                    {
                                        qtOut = Convert.ToInt32(gastroOut.Split('(')[1].Replace(")", ""));
                                    }
                                    break;
                                case "DrainOutput24hrInt":
                                    var drainOut = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                                    if (!string.IsNullOrWhiteSpace(drainOut) && drainOut.Split('(')[1].Replace(")", "") != "0")
                                    {
                                        qtOut = Convert.ToInt32(drainOut.Split('(')[1].Replace(")", ""));
                                    }
                                    break;
                            }
                            if (qtOut > 0)
                            {
                                var item = result.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.sBw));
                                if (item == null)
                                {
                                    data.sBw = qtOut.ToString();
                                    result.Add(data);
                                }
                                else
                                {
                                    item.sBw = (Convert.ToInt32(item.sBw) + qtOut).ToString();
                                }
                            }

                        }

                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignSummary error message:" + ex.ToString());
                }
            }
            var sBwitem = result.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.sBw));
            if (sBwitem != null)
            {
                sBwitem.sBw += "ml";
            }

            return result;
        }

        /// <summary>
        /// 统计尿管
        /// 统计时间 前一天到今天早上7点
        /// </summary>
        /// <param name="patient"></param>
        /// <returns></returns>
        public bool GetVitalSignUrineTube(PatientModel patient)
        {
            var today = DateTime.Today.AddHours(7);
            var currentTime = DateTime.Now;
            if (currentTime > today)
            {
                currentTime = today.AddDays(-1);
            }
            else
            {
                currentTime = today.AddDays(-2);
            }
            var result = false;
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention ";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and propName in ('UrinaryCatheter_site_Int')";
                sql += "and chartTime>'" + currentTime.ToString("yyyy-MM-dd HH:mm") + "' and chartTime<='" + currentTime.AddDays(1).ToString("yyyy-MM-dd HH:mm") + "' ";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        if (DBNull.Value != reader["value_t"]&&!string.IsNullOrWhiteSpace(reader["value_t"].ToString()))
                        {
                            result = true;
                        }
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignUrine error message:" + ex.ToString());
                }
            }

            return result;
        }


        /// <summary>
        /// 大便次数
        /// 统计时间 前一天到今天早上7点 不满7点传0
        /// 没有量传0
        /// 大于等于5 传*
        /// 有灌肠  1/E
        /// 有肛管插管的 传*
        /// </summary>
        /// <param name="patient"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSignStoolNum(PatientModel patient, VitalSignModel basicInfo)
        {
            var today = DateTime.Today.AddHours(7);

            var startTime = DateTime.Now;
            var endTime = DateTime.Now;
            if (startTime > today)
            {
                startTime = today.AddDays(-1);
            }
            else
            {
                startTime = today.AddDays(-2);
            }
            endTime = startTime.AddDays(1);
            var result = new List<VitalSignModel>();
            var stoolSiteInt = 0;
            var enemaInt = 0;
            var gangguan = false;
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention ";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and propName in ('StoolSiteInt','enema_Int','Subdural_drainage__Site_Int')";
                sql += "and chartTime>'" + startTime.ToString("yyyy-MM-dd HH:mm") + "' and chartTime<='" + endTime.ToString("yyyy-MM-dd HH:mm") + "' ";
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
                            if (propName == "StoolSiteInt" && DBNull.Value != reader["value_t"])
                            {
                                stoolSiteInt += 1;
                            }
                            if (propName == "enema_Int" && DBNull.Value != reader["value_t"])
                            {
                                enemaInt += 1;
                            }
                            if (propName == "Subdural_drainage__Site_Int" && DBNull.Value != reader["value_t"] && reader["desc_t"].ToString().Contains("肛管"))
                            {
                                gangguan = true;
                            }
                        }

                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignDetailSummaryInfo error message:" + ex.ToString());
                }
            }


            var data = new VitalSignModel();
            data.DBCS = stoolSiteInt.ToString();
            if (stoolSiteInt >= 5 || gangguan)
            {
                data.DBCS = "*";
            }
            if (enemaInt > 0)
            {
                if (enemaInt == 1)
                {
                    data.DBCS += "/E";
                }
                else
                {
                    data.DBCS += "/" + enemaInt + "E";
                }
                
            }
            data.ZYRQ = endTime.ToString("yyyyMMdd");
            data.CJSJ = endTime.ToString("HH:mm");
            data.LRRQ = endTime.ToString("yyyyMMddHH:mm:ss");
            data.JLSJ = data.LRRQ;
            result.Add(data);

            return result;
        }

        /// <summary>
        /// 上传事件 
        /// 1. 转入
        /// 2. 转出
        /// 3. 如果是死亡 就传死亡
        /// 4. 如果是出院 就传出院
        /// 2 3 4 互斥
        /// DemographicOther5 入科时间
        /// Demographic27 出科转归
        /// demographicOther14 出科时间
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSignEvent(PatientModel patient, DateTime startTime)
        {
            //DemographicOther5 入科时间  Demographic27  出科转归  demographicOther14 出科时间  admDateTimeInt 入院时间
            var result = new List<VitalSignModel>();
            var eventList = new List<VitalEntity>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and propName in ('DemographicOther5','Demographic27','demographicOther14','admDateTimeInt','Demographic81Int')";
                sql += "and storeTime>'" + startTime.ToString("yyyy-MM-dd HH:mm") + "' ";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var data = new VitalEntity();
                        data.ItemName = reader["propName"].ToString();
                        data.ItemValue = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                        data.ChartTime = Convert.ToDateTime(reader["chartTime"]);
                        data.StoreTime = Convert.ToDateTime(reader["storeTime"]);
                        if (!string.IsNullOrWhiteSpace(data.ItemValue))
                        {
                            eventList.Add(data);
                        }

                    }
                    reader.Close();

                    //0：入院；1：手术 ；2：转入 ；3：分娩 ；4：转出 ；5：出院 ；6：死亡 ；7：转院
                    //比如入院：SBSM传0，时间传: |十六时四十分  最终格式我们这边就显示  入院|十六时四十分
                    var eventIn = new VitalEntity();
                    int eventInTypeInt = 2;
                    var eventInType = eventList.FirstOrDefault(x => x.ItemName == "Demographic81Int");
                    if (eventInType != null&& eventInType.ItemValue == "急诊入院")
                    {
                        eventInTypeInt = 0;
                        eventIn = eventList.FirstOrDefault(x => x.ItemName == "admDateTimeInt");
                    }
                    else
                    {
                        eventIn = eventList.FirstOrDefault(x => x.ItemName == "DemographicOther5");
                    }
                    
                    if (eventIn != null)
                    {
                        var uploadTime = GetUploadTime(Convert.ToDateTime(eventIn.ItemValue));
                        var record = new VitalSignModel();
                        record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                        record.CJSJ = uploadTime.ToString("HH:mm");
                        record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                        record.JLSJ = eventIn.StoreTime.ToString("yyyyMMddHH:mm:ss");
                        record.SBSM = eventInTypeInt;
                        record.SBSJ = "|" + DateToChinese(Convert.ToDateTime(eventIn.ItemValue));
                        result.Add(record);
                    }
                    var eventOut = eventList.FirstOrDefault(x => x.ItemName == "demographicOther14");
                    if (eventOut != null)
                    {
                        var type = 4;
                        var typeItem = eventList.FirstOrDefault(x => x.ItemName == "Demographic27");
                        if (typeItem != null)
                        {
                            if (typeItem.ItemValue == "出院")
                            {
                                type = 5;
                            }
                            if (typeItem.ItemValue == "死亡")
                            {
                                type = 6;
                            }
                        }
                        var uploadTime = GetUploadTime(Convert.ToDateTime(eventOut.ItemValue));
                        var record = new VitalSignModel();
                        record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                        record.CJSJ = uploadTime.ToString("HH:mm");
                        record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                        record.JLSJ = eventOut.StoreTime.ToString("yyyyMMddHH:mm:ss");
                        record.SBSM = type;
                        record.SBSJ = "|" + DateToChinese(Convert.ToDateTime(eventOut.ItemValue));
                        result.Add(record);
                    }
                    var ptWeight = eventList.FirstOrDefault(x => x.ItemName == "ptWeightIntervention");
                    if (ptWeight != null)
                    {
                        //暂时注释

                        //var uploadTime = GetUploadTime(ptWeight.StoreTime);
                        //var record = new VitalSignModel();
                        //record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                        //record.CJSJ = uploadTime.ToString("HH:mm");
                        //record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                        //record.JLSJ = ptWeight.StoreTime.ToString("yyyyMMddHH:mm:ss");
                        //float tz = 0;
                        //float.TryParse(ptWeight.ItemValue, out tz);
                        //record.TZ = tz;
                        //result.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignEvent error message:" + ex.ToString());
                }

            }

            return result;
        }

        /// <summary>
        /// 获取机械通气相关数据
        /// 逻辑
        /// 开始机械通气标准是 通气时间为0 要求填写的时候 开始时间和表格时间一致
        /// 停机械通气判断标准是是否有停止时间
        /// 与入院时间间隔七天 的数据也要取
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSignVent(PatientModel patient, DateTime startTime, VitalSignModel basicInfo)
        {
            var result = new List<VitalSignModel>();
            var ventList = new List<VitalEntity>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention_data";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and interventionPropName in ('Invasive_Ventilation_Time_Int')";
                sql += "and storeTime>'" + startTime.ToString("yyyy-MM-dd HH:mm") + "' ";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var data = new VitalEntity();
                        data.ItemName = reader["propName"].ToString();
                        data.ItemValue = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                        data.ChartTime = Convert.ToDateTime(reader["chartTime"]);
                        data.StoreTime = Convert.ToDateTime(reader["storeTime"]);
                        if (!string.IsNullOrWhiteSpace(data.ItemValue))
                        {
                            ventList.Add(data);
                        }

                    }
                    reader.Close();
                    ventList = ventList.OrderBy(x => x.ChartTime).ToList();

                    var ventStartList = ventList.Where(x => x.ItemName == "InvasiveVentCumulativeHourNum" && x.ItemValue == "0").ToList();
                    if (ventStartList != null)
                    {
                        foreach (var ventStart in ventStartList)
                        {
                            var ventStartTime = ventList.FirstOrDefault(x => x.ItemName == "InvasiveVentilationBeginTime_DT" && x.ChartTime == ventStart.ChartTime);
                            if (ventStartTime != null)
                            {
                                var uploadTime = GetUploadTime(Convert.ToDateTime(ventStartTime.ItemValue));
                                var record = new VitalSignModel();
                                record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                                record.CJSJ = uploadTime.ToString("HH:mm");
                                record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                                record.JLSJ = ventStart.StoreTime.ToString("yyyyMMddHH:mm:ss");
                                record.sXbsm = "机械通气";
                                result.Add(record);
                            }
                        }
                    }
                    var ventEndList = ventList.Where(x => x.ItemName == "InvasiveVentilationEndTime_DT").ToList();
                    foreach (var ventEnd in ventEndList)
                    {
                        var uploadTime = GetUploadTime(Convert.ToDateTime(ventEnd.ItemValue));
                        var record = new VitalSignModel();
                        record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                        record.CJSJ = uploadTime.ToString("HH:mm");
                        record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                        record.JLSJ = ventEnd.StoreTime.ToString("yyyyMMddHH:mm:ss");
                        record.sXbsm = "停机械通气";
                        result.Add(record);


                    }

                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignVent error message:" + ex.ToString());
                }

            }
            var weekData = GetVitalSignVentByWeek(patient, startTime, basicInfo);
            if (weekData.Any())
            {
                result.AddRange(weekData);
            }

            return result;
        }

        //与入院时间间隔七天 的数据也要取
        public List<VitalSignModel> GetVitalSignVentByWeek(PatientModel patient, DateTime startTime, VitalSignModel basicInfo)
        {
            var result = new List<VitalSignModel>();
            try
            {
                var zyrq = Convert.ToDateTime(basicInfo.RYSJ);
                var days = (DateTime.Today - Convert.ToDateTime(basicInfo.RYSJ).Date).TotalDays;
                if (days > 6 && days % 7 == 0)
                {
                    var beginTime = DateTime.Today;
                    var endTime = beginTime.AddHours(23).AddMinutes(59);
                    var ventList = new List<VitalEntity>();
                    using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
                    {
                        var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention_data";
                        sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                        sql += "and interventionPropName in ('Invasive_Ventilation_Time_Int')";
                        sql += "and chartTime>'" + startTime.ToString("yyyy-MM-dd 00:00") + "' and chartTime<='" + endTime.ToString("yyyy-MM-dd HH:mm") + "' ";
                        SqlCommand command = new SqlCommand(sql, connection);

                        connection.Open();
                        var reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            var data = new VitalEntity();
                            data.ItemName = reader["propName"].ToString();
                            data.ItemValue = DBNull.Value == reader["value_t"] ? "" : reader["value_t"].ToString();
                            data.ChartTime = Convert.ToDateTime(reader["chartTime"]);
                            data.StoreTime = Convert.ToDateTime(reader["storeTime"]);
                            if (!string.IsNullOrWhiteSpace(data.ItemValue))
                            {
                                ventList.Add(data);
                            }

                        }
                        reader.Close();
                        ventList = ventList.OrderBy(x => x.ChartTime).ToList();

                        //针对入院间隔7天翻页

                        var item = ventList.FirstOrDefault(x => x.ItemName == "InvasiveVentCumulativeHourNum" && x.ChartTime > beginTime && x.ChartTime <= endTime);
                        if (item != null)
                        {
                            //第一条记录不能是停止记录
                            if (ventList.FirstOrDefault(x => x.ItemName == "InvasiveVentilationEndTime_DT" && x.ChartTime == item.ChartTime) == null)
                            {
                                var uploadTime = GetUploadTime(item.ChartTime.Date);
                                var record = new VitalSignModel();
                                record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                                record.CJSJ = uploadTime.ToString("HH:mm");
                                record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                                record.JLSJ = item.StoreTime.ToString("yyyyMMddHH:mm:ss");
                                record.sXbsm = "机械通气";
                                result.Add(record);
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("GetVitalSignVentByWeek error message:" + ex.ToString());
            }

            return result;
        }


        /// <summary>
        /// 获取不限时数据
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        public List<VitalSignModel> GetVitalSignFree(PatientModel patient, DateTime startTime)
        {
            var result = new List<VitalSignModel>();
            var eventList = new List<VitalEntity>();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var sql = @"  SELECT * FROM  v_" + patient.dbName + "_pt_intervention";
                sql += " WHERE  ptEncounterId IN ('" + patient.ptEncounterId + "')";
                sql += "and propName in ('MedicationAllergyTest_Int')";
                sql += "and storeTime>'" + startTime.ToString("yyyy-MM-dd HH:mm") + "' ";
                SqlCommand command = new SqlCommand(sql, connection);
                try
                {
                    connection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var uploadTime = GetUploadTime(Convert.ToDateTime(reader["chartTime"]));
                        var record = new VitalSignModel();
                        record.ZYRQ = uploadTime.ToString("yyyyMMdd");
                        record.CJSJ = uploadTime.ToString("HH:mm");
                        record.LRRQ = uploadTime.ToString("yyyyMMddHH:mm:ss");
                        record.JLSJ = Convert.ToDateTime( reader["storeTime"]).ToString("yyyyMMddHH:mm:ss");
                        record.YWGM2 = reader["value_t"].ToString();
                        result.Add(record);

                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog("GetVitalSignFree error message:" + ex.ToString());
                }

            }

            return result;
        }


        public string PostHttp(string url, string body, string contentType)
        {
            var responseData = "";
            try
            {
                
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
                httpWebRequest.Timeout = 60000;
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
            }
            catch(Exception ex)
            {
                LogUtil.ErrorLog("上传数据失败:" + ex.ToString());
                responseData = ex.ToString();
            }
            return responseData;
        }

        public DateTime GetUploadTime(DateTime input)
        {
            var currentDay = input.Date;
            foreach (var r in timeLines)
            {
                var currentTime = Convert.ToDateTime(currentDay.ToString("yyyy-MM-dd") + " " + r);
                if (input <= currentTime)
                {
                    return currentTime;
                }
            }
            return Convert.ToDateTime(currentDay.ToString("yyyy-MM-dd") + " 23:00");
        }

        public  string DateToChinese(DateTime str)
        {
            string str1 = str.Hour.ToString();
            string strM = str.Minute.ToString();
            if (str1 == "00")
            {
                str1 = "零";
            }
            else
            {
                if (Convert.ToInt32(str1) < 10)
                {
                    str1 = DateTimeToChinese(str1.ToString());
                }
                else if (Convert.ToInt32(str1) >= 10 && Convert.ToInt32(str1) < 20)
                {
                    str1 = "十" + DateTimeToChinese(str1.Substring(1, 1));
                }
                else
                {
                    str1 = "二十" + DateTimeToChinese(str1.Substring(1, 1));
                }
            }
            if (strM == "00")
            {
                strM = "零";
            }
            else
            {
                if (Convert.ToInt32(strM) < 10)
                {
                    strM = "零" + DateTimeToChinese(strM.ToString());
                }
                else if (Convert.ToInt32(strM) >= 10 && Convert.ToInt32(strM) < 20)
                {
                    strM = "十" + DateTimeToChinese(strM.Substring(1, 1));
                }
                else if (Convert.ToInt32(strM) >= 20 && Convert.ToInt32(strM) < 30)
                {
                    strM = "二十" + DateTimeToChinese(strM.Substring(1, 1));
                }
                else if (Convert.ToInt32(strM) >= 30 && Convert.ToInt32(strM) < 40)
                {
                    strM = "三十" + DateTimeToChinese(strM.Substring(1, 1));
                }
                else if (Convert.ToInt32(strM) >= 40 && Convert.ToInt32(strM) < 50)
                {
                    strM = "四十" + DateTimeToChinese(strM.Substring(1, 1));
                }
                else if (Convert.ToInt32(strM) >= 50 && Convert.ToInt32(strM) < 60)
                {
                    strM = "五十" + DateTimeToChinese(strM.Substring(1, 1));
                }
            }
            return str1.ToString() + "时" + strM + "分";
        }
        public  string DateTimeToChinese(string str)
        {
            switch (str)
            {
                case "1":
                    str = "一";
                    break;
                case "2":
                    str = "二";
                    break;
                case "3":
                    str = "三";
                    break;
                case "4":
                    str = "四";
                    break;
                case "5":
                    str = "五";
                    break;
                case "6":
                    str = "六";
                    break;
                case "7":
                    str = "七";
                    break;
                case "8":
                    str = "八";
                    break;
                case "9":
                    str = "九";
                    break;
                case "0":
                    str = "";
                    break;
                default:
                    break;
            }
            return str;
        }
    }

    public class VitalEntity
    {
        public string ItemName { get; set; }
        public string ItemValue { get; set; }
        public DateTime ChartTime { get; set; }
        public DateTime StoreTime { get; set; }
    }
    
}
