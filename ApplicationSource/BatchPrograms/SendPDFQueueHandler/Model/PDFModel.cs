using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SendPDFQueueHandler.Model
{
    public class PDFModel
    {
    }

    #region Internal Model
    public class SendPDFRespond
    {
        public bool SendStatus { get; set; }
        public string Message { get; set; }
        public string ErrMsg { get; set; }
    }
    #endregion
}
