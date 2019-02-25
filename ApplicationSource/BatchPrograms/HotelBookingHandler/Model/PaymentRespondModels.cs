using Alphareds.Module.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelBookingHandler.Model
{
    public class IPAY88
    {
        interface IIPAY88Respond
        {
            string SuperPNRNo { get; set; }
        }

        public class RespondCapture : iPayCaptureResponseModels, IIPAY88Respond
        {
            public string SuperPNRNo { get; set; }
        }

        public class RespondVoid : iPayCaptureResponseModels, IIPAY88Respond
        {
            public string SuperPNRNo { get; set; }
        }
    }
}
