using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadPatientVitalSignInfo.Model
{
   public  class PatientModel
    {
        public string ptEncounterId { get; set; }
        public string dbName { get; set; }
    }

    public class IccaUserModel
    {
        public string userId { get; set; }
        public string userDomainName { get; set; }
        public string lastName { get; set; }
    }
}
