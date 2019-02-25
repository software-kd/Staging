using Alphareds.Module.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Mayflower.Areas.InternalApps.Models;

namespace Mayflower.Areas.InternalApps.Controllers
{
    [Mayflower.Filters.LocalhostFilter]
    public class ConfigureController : AsyncController
    {
        //http://localhost:52197/InternalApps/Configure/RefreshCachedDBConfigValue
        public ActionResult RefreshCachedDBConfigValue()
        {
            Alphareds.Module.Event.Function.DB.ConcertMinPurchaseAmt = null;
            Alphareds.Module.Event.Function.DB.DBConfigCacheTime = null;

            return null;
        }

        //http://localhost:52197/InternalApps/Configure/SMTP
        public ActionResult SMTP()
        {
            return View();
        }

        // POST: InternalApps/Configure
        [HttpPost]
        public async Task<ActionResult> SMTP(TestSMTPModel testSMTPModel, bool testAttachment = false)
        {
            if (testSMTPModel.SendTo == null)
            {
                return Content("testSMTPModel.SendTo cannot null;");
            }

            var message = new MailMessage()
            {
                Subject = testSMTPModel.Subject,
                Body = testSMTPModel.Body,
                IsBodyHtml = false,
                BodyEncoding = Encoding.UTF8
            };

            string smtpUserName = Core.GetAppSettingValueEnhanced("smtpUserName");
            string smtpPassword = Core.GetAppSettingValueEnhanced("smtpPassword");
            string smtpServer = Core.GetAppSettingValueEnhanced("smtpServer");
            string smtpPort = Core.GetAppSettingValueEnhanced("smtpPort");
            string fromEmailAddress = Core.GetAppSettingValueEnhanced("fromEmailAddress");
            string enableSsl = Core.GetAppSettingValueEnhanced("enableSsl");

            message.From = new MailAddress(fromEmailAddress);

            foreach (var email in testSMTPModel.SendTo.Split(',', ';').Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                message.To.Add(email);
            }

            /*if (testAttachment)
            {
                foreach (var attach in attachCollection)
                {
                    message.Attachments.Add(new Attachment( attach);
                }
            }*/

            using (var smtpClient = new SmtpClient())
            {
                bool parseEnableSsl = bool.TryParse(enableSsl, out parseEnableSsl) ? parseEnableSsl : parseEnableSsl;

                smtpClient.UseDefaultCredentials = false;
                smtpClient.Host = smtpServer;
                smtpClient.Port = int.Parse(smtpPort);
                smtpClient.EnableSsl = parseEnableSsl;
                smtpClient.Credentials = new NetworkCredential(smtpUserName, smtpPassword);

                try
                {
                    //smtpClient.Send(message);
                    await smtpClient.SendMailAsync(message);
                    return Content("Send Success.");
                }
                catch (Exception ex)
                {
                    return Content(ex.ToString());
                }
            };
        }
    }
}