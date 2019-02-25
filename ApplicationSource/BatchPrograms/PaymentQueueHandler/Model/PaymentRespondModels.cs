using Alphareds.Module.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentQueueHandler.Model
{
    public class RequeryStatus
    {
        interface IRequeryRespond
        {
            int SuperPNRID { get; set; }
            string SuperPNRNo { get; set; }
            int OrderID { get; set; }
            bool Status { get; set; }
        }

        public class Requery : PaymentGatewayResponse, IRequeryRespond
        {
            public int SuperPNRID { get; set; }
            public string SuperPNRNo { get; set; }
            public int OrderID { get; set; }
            public new bool Status { get; set; }
        }

        public class Capture : PaymentGatewayResponse, IRequeryRespond
        {
            public int SuperPNRID { get; set; }
            public string SuperPNRNo { get; set; }
            public int OrderID { get; set; }
            public new bool Status { get; set; }
        }

        public class Void : PaymentGatewayResponse, IRequeryRespond
        {
            public int SuperPNRID { get; set; }
            public string SuperPNRNo { get; set; }
            public int OrderID { get; set; }
            public new bool Status { get; set; }
        }

    }
    public class PaymentGatewayResponse
        {
            public string MerchantCode { get; set; }
            public string AppId { get; set; }
            public int PaymentId { get; set; }
            public string RefNo { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; }
            public string Remark { get; set; }
            public string TransId { get; set; }
            public string AuthCode { get; set; }
            public string Status { get; set; }
            public string ErrDesc { get; set; }
            public string Signature { get; set; }
            public string RiskLevel { get; set; }
            public string Desc { get; set; }

            // Added by capture technical error
            public string TechErrDesc { get; set; }
        }
}
