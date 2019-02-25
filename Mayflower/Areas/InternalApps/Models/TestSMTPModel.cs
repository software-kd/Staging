using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Web;

namespace Mayflower.Areas.InternalApps.Models
{
    public class TestSMTPModel
    {
        public string SendTo { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public Attachment Attachment { get; set; }
        public HttpPostedFile HttpPostedFile { get; set; }
        public List<HttpPostedFile> ListHttpPostedFile { get; set; }
    }
}