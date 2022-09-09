using CSH.Interface.Service.Log;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using ZWUDataIntegration.Model;
using ZWUDataIntegration.Util;

namespace ZWUDataIntegration
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
            timerMainCallback.Interval = config.InInterval * 1000;
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
                    CopyData();
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


        //业务处理单元
        public void CopyData()
        {
            config = Common.LoadServiceConfig();
            //1 获取需要同步的view
            var view = GetSyncView();
            if (string.IsNullOrEmpty(view.ViewName)||string.IsNullOrEmpty(view.Condition))
            {
                LogUtil.DebugLog("没有需要同步的View或者配置对应的匹配时间");
            }
            else
            {
                //获取需要同步的数据
                var ds = GetViewDataDetail(view);
                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    //往目标数据库插入数据
                    if (view.ViewName == "v_SJCK_DOCUMENT_HLBD")
                    {
                        //这个view 特殊处理
                        if(InsertToTargetTableSingle(view, ds.Tables[0]))
                        {
                            LogUtil.CommonLog($"{view.ViewName} view 同步到目标数据表:{view.TargetTableName}成功，同步数据条数:{ds.Tables[0].Rows.Count},同步时间段为:{view.SyncTime.ToString("yyyy-MM-dd HH:mm:ss")}到{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
                        }
                    }
                    else
                    {
                        if (InsertToTargetTable(view, ds.Tables[0]))
                        {
                            LogUtil.CommonLog($"{view.ViewName} view 同步到目标数据表:{view.TargetTableName}成功，同步数据条数:{ds.Tables[0].Rows.Count},同步时间段为:{view.SyncTime.ToString("yyyy-MM-dd HH:mm:ss")}到{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
                        }
                    }
                }
                else
                {
                    LogUtil.DebugLog(view.ViewName + "view没有需要同步的数据");
                }
                //更新同步时间，确保下次不会同步重复数据过去
                UpdateSyncView(view);
            }
        }

        public SyncViewModel GetSyncView()
        {
            LogUtil.DebugLog($"开始获取view");
            var result = new SyncViewModel();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var queryString = "select top 1 * from SyncViewList order by SyncTime asc ";
                SqlCommand command = new SqlCommand(queryString, connection);
                try
                {
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        result.ViewName = DBNull.Value == reader["ViewName"] ? "" : reader["ViewName"].ToString();
                        result.SyncTime = DBNull.Value == reader["SyncTime"] ? DateTime.Now : Convert.ToDateTime(reader["SyncTime"]);
                        result.TargetTableName = DBNull.Value == reader["TargetTableName"] ? "": reader["TargetTableName"].ToString();
                        result.Condition = DBNull.Value == reader["Condition"] ? "" : reader["Condition"].ToString();
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog($"获取view报错:" + ex.ToString());
                }
            }
            return result;
        }
        public DataSet GetViewDataDetail(SyncViewModel syncView)
        {
            LogUtil.DebugLog($"开始获取view{syncView.ViewName}数据," + syncView.SyncTime.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = new DataSet();
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var queryString = $"select * from {syncView.ViewName} where {syncView.Condition}>='{syncView.SyncTime.ToString("yyyy-MM-dd HH:mm:ss")}'  ";
                SqlCommand command = new SqlCommand(queryString, connection);
                try
                {
                    connection.Open();
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = command;
                    da.Fill(result);
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog($"获取{syncView.ViewName}view数据详情报错:" + ex.ToString());
                }
            }
            return result;
        }

        public bool InsertToTargetTableSingle(SyncViewModel syncView, DataTable table)
        {
            bool result = true;
            LogUtil.DebugLog($"开始同步view{syncView.ViewName},数据条数:{table.Rows.Count}，同步开始时间:{syncView.SyncTime.ToString("yyyy-MM-dd HH:mm:ss")}");
            try
            {
                using (SqlConnection connection = new SqlConnection(config.PingTaiConnectionString))
                {
                    connection.Open();
                    for (var i = 0; i < table.Rows.Count; i++)
                    {
                        var queryString = $"select blh from IN_ICU_DAT_HLBD where blh='{table.Rows[i]["blh"]}' and wdrq='{table.Rows[i]["wdrq"]}'";
                        var command = new SqlCommand(queryString, connection);
                        var obj = command.ExecuteScalar();
                        if(obj == null)
                        {
                            var sql = $"insert into IN_ICU_DAT_HLBD(jzlsh,blh,wdrq,bt,url)values('{table.Rows[i]["jzlsh"]}','{table.Rows[i]["blh"]}','{table.Rows[i]["wdrq"]}','{table.Rows[i]["bt"]}','{table.Rows[i]["url"]}')";
                            command = new SqlCommand(sql, connection);
                            command.ExecuteNonQuery();
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog($"InsertToTargetTableSingle报错" + ex.ToString());
            }
            return result;
        }

        public bool InsertToTargetTable(SyncViewModel syncView, DataTable table)
        {
            bool result = true;
            LogUtil.DebugLog($"开始同步view{syncView.ViewName},数据条数:{table.Rows.Count}，同步开始时间:{syncView.SyncTime.ToString("yyyy-MM-dd HH:mm:ss")}");
            using (SqlConnection connection = new SqlConnection(config.PingTaiConnectionString))
            {
                var queryString = $"select top 1 * from {syncView.TargetTableName} ";
                SqlCommand command = new SqlCommand(queryString, connection);
                try
                {
                    var tempDs = new DataSet();
                    connection.Open();
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = command;
                    da.FillSchema(tempDs, SchemaType.Source, "dbo");
                    da.Fill(tempDs);
                    //1  如果原始view 字段多余目标表，则移除该字段
                    List<string> removeColumns = new List<string>();
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string sourceName = table.Columns[i].ToString();
                        if (!tempDs.Tables[0].Columns.Contains(sourceName))
                        {
                            removeColumns.Add(sourceName);
                            LogUtil.DebugLog(sourceName + "不存在目标表");
                        }
                    }
                    foreach (var r in removeColumns)
                    {
                        table.Columns.Remove(r);
                    }

                    for (int i = 0; i < tempDs.Tables[0].Columns.Count; i++)
                    {
                        string targetName = tempDs.Tables[0].Columns[i].ToString();
                        var dataType = tempDs.Tables[0].Columns[i].DataType.Name;
                        if (table.Columns.Contains(targetName))
                        {
                            if (!tempDs.Tables[0].Columns[i].AllowDBNull)
                            {
                                for (int j = 0; j < table.Rows.Count; j++)
                                {
                                    if (DBNull.Value == table.Rows[j][targetName])
                                    {
                                        switch (dataType)
                                        {
                                            case "Boolean":
                                            case "Byte":
                                            case "Int16":
                                            case "Int32":
                                            case "Int64":
                                            case "Decimal":
                                            case "Double":
                                            case "Single":
                                                table.Rows[j][targetName] = 0;
                                                break;
                                            case "DateTime":
                                                table.Rows[j][targetName] = "1900-01-01";
                                                break;
                                            case "TimeSpan":
                                                table.Rows[j][targetName] = "00:00:00";
                                                break;
                                            case "String":
                                                table.Rows[j][targetName] = " ";
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog($"检查列名报错" + ex.ToString());
                }
            }
            using (SqlBulkCopy sqlBulk = new SqlBulkCopy(config.PingTaiConnectionString))
            {
                try
                {

                    sqlBulk.BatchSize = 5000;
                    sqlBulk.BulkCopyTimeout = 60;
                    sqlBulk.DestinationTableName = syncView.TargetTableName;
                    foreach (DataColumn col in table.Columns)
                    {
                        sqlBulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                    }
                    sqlBulk.WriteToServer(table);
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog($"插入到目标数据库{syncView.TargetTableName}报错:" + ex.ToString());
                    result = false;
                }
            }

            return result;
        }

        public void UpdateSyncView(SyncViewModel syncView)
        {
            LogUtil.DebugLog($"更新view{syncView.ViewName}同步时间");
            using (SqlConnection connection = new SqlConnection(config.IccaConnectionString))
            {
                var queryString = $"update SyncViewList set SyncTime=GETDATE() where ViewName='{syncView.ViewName}'";
                SqlCommand command = new SqlCommand(queryString, connection);
                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorLog($"更新{syncView.ViewName}view同步时间报错:" + ex.ToString());
                }
            }
        }
    }
}
