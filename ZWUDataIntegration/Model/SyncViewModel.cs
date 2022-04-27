using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZWUDataIntegration.Model
{
    public class SyncViewModel
    {
        public string ViewName { get; set; }
        public DateTime SyncTime { get; set; }

        public string TargetTableName { get; set; }

        public string Condition { get; set; }
    }
}
