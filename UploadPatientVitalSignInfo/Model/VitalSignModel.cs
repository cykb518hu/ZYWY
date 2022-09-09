using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadPatientVitalSignInfo.Model
{
    public class VitalSignModel
    {

        /// <summary>
        /// 入科时间
        /// </summary>
        public string RKSJ { get; set; }
        /// <summary>
        /// 入院时间
        /// </summary>
        public string RYSJ { get; set; }
        /// <summary>
        /// 患者首页序号int类型
        /// </summary>
        public string SYXH { get; set; }

        /// <summary>
        /// 患者婴儿序号int类型
        /// </summary>
        public string YEXH { get; set; }

        /// <summary>
        /// 体温单采集数据对应的住院日期,护理单传空字符串即可 yyyyMMdd
        /// </summary>
        public string ZYRQ { get; set; }

        /// <summary>
        /// 体温单标准时间点 （2、6、10...或3、7、11...的整点时间 格式char(5),HH:mm）
        /// </summary>
        public string CJSJ { get; set; }

        /// <summary>
        /// 记录时间即实际测量时间 yyyyMMddHH:mm:ss
        /// </summary>
        public string JLSJ { get; set; }

        /// <summary>
        /// 护理单时间点char(16),yyyyMMddHH:mm:ss
        /// </summary>
        public string LRRQ { get; set; }
        /// <summary>
        /// 操作员工号
        /// </summary>
        public string CZYH { get; set; }
        /// <summary>
        /// 操作员名称
        /// </summary>
        public string CZYM { get; set; }
        /// <summary>
        /// 病区代码
        /// </summary>
        public string BQDM { get; set; }
        /// <summary>
        /// 病区名称
        /// </summary>
        public string BQMC { get; set; }
        /// <summary>
        /// 科室代码
        /// </summary>
        public string KSDM { get; set; }
        /// <summary>
        /// 科室名称
        /// </summary>
        public string KSMC { get; set; }
        /// <summary>
        /// 呼吸
        /// </summary>
        public int? HX { get; set; }

        /// <summary>
        /// 心率
        /// </summary>
        public int? XL { get; set; }

        /// <summary>
        /// 体温
        /// </summary>
        public float? TW { get; set; }

        /// <summary>
        /// int类型体温测量方式0:口表  1:腋表 2:肛温 3;耳温   
        /// </summary>
        public int? CLFS { get; set; }


        //血压
        public string XY { get; set; }

        /// <summary>
        /// 身高
        /// </summary>
        public int SG { get; set; }

        /// <summary>
        /// 体重
        /// </summary>
        public float? TZ { get; set; }

       
        /// <summary>
        /// 大便次数,（string类型）例如："4/E"、"2/E"或"1"
        /// </summary>
        public string DBCS { get; set; }

        /// <summary>
        /// 小便值
        /// </summary>
        public string NL { get; set; }

        /// <summary>
        /// 总入量
        /// </summary>
        public string RLZH { get; set; }

        /// <summary>
        /// 总出量
        /// </summary>
       // public string CLZH { get; set; }

        /// <summary>
        /// 上传体温单或护理单 0：上传到体温单
        /// </summary>
        public int SCBD { get; set; }

        /// <summary>
        /// 上标
        /// </summary>
        public int? SBSM { get; set; }
        /// <summary>
        /// 上标
        /// </summary>
        public string SBSJ { get; set; }
        /// <summary>
        /// 下标
        /// </summary>
        public string sXbsm { get; set; }

        /// <summary>
        /// 体重状态 卧床
        /// </summary>
        public string sPf { get; set; }


        /// <summary>
        /// 药物过敏
        /// </summary>
        public string YWGM2 { get; set; }

        /// <summary>
        /// 其他 胃肠减压+引流量
        /// </summary>
        public string sBw { get; set; }
    }
}
