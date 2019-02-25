using Alphareds.Library.DatabaseHandler;
using Alphareds.Module.Common;
using Alphareds.Module.CommonController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

//Here is the once-per-application setup information
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace EmailQueueHandler
{
    class Program
    {
        #region Member variables Declarations

        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog emaillog = log4net.LogManager.GetLogger("EmailLogger");
        private static ExecutionType executionType;
        private static MayFlower db = new MayFlower();
        private static DatabaseHandlerMain dbADO = new DatabaseHandlerMain();

        #endregion

        #region Constructors & Finalizers



        #endregion

        #region Properties

        public enum ExecutionType
        {
            ProcessEmailQueue,
        }

        #endregion

        #region Methods

        private static void SendErrorMail(string error)
        {
            try
            {
                log.Debug("SendErrorEmail Started.");

                emaillog.Debug("Error:" + error);

                log.Debug("SendErrorEmail Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete SendErrorEmail.");
                throw;
            }
        }

        private static void ProcessEmailQueue()
        {
            try
            {
                log.Debug("ProcessEmailQueue Started.");
                //Retrieve the email from email queue.

                int retryCount = Convert.ToInt32(Core.GetAppSettingValueEnhanced("retryCount"));

                log.Debug("Retrieve email from email queue Started.");
                List<EmailQueue> emailQueues = db.EmailQueues.Where(x => x.SentTries < retryCount && !x.IsSent).ToList();
                log.Debug("Retrieved " + emailQueues.Count() + " email(s) from email queue.");

                foreach (EmailQueue q in emailQueues)
                {
                    List<Attachment> attachments = new List<Attachment>();
                    List<EmailAttachment> emailAttachments = db.EmailAttachments.Where(x => x.Id == q.Id).OrderBy(y => y.AttachmentFileName).ToList();
                    foreach (EmailAttachment ea in emailAttachments)
                    {
                        attachments.Add(new Attachment(new MemoryStream(ea.Attachment), ea.AttachmentFileName));
                    }

                    q.SentTries++;
                    CommonServiceController.UpdateEmailQueue(q);

                    bool isSuccess = SendMail(q.Id.ToString(), q.From, q.To, q.CC, q.Bcc, q.Subject, q.Body, attachments, q.IsBodyHtml);
                    if (isSuccess)
                    {
                        log.Debug("Update Is Sent Successfully Started." + q.IsSent.ToString());
                        q.IsSent = true;
                        CommonServiceController.UpdateEmailQueue(q);
                        log.Debug("Update Is Sent Successfully Ended.");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete ProcessEmailQueue.");
                throw ex;
            }
        }

        private static bool SendMail(string id, string from, string to, string cc, string bcc, string subject, string body, List<Attachment> attachCollection = null, bool isBodyHtml = true)
        {
            try
            {
                log.Debug("Sending Email Queue Id: " + id);

                string smtpUserName = Core.GetAppSettingValueEnhanced("smtpUserName");
                string smtpPassword = Core.GetAppSettingValueEnhanced("smtpPassword");
                string smtpServer = Core.GetAppSettingValueEnhanced("smtpServer");
                string smtpPort = Core.GetAppSettingValueEnhanced("smtpPort");
                string enableSsl = Core.GetAppSettingValueEnhanced("enableSsl");
                string disableSendMail = Core.GetAppSettingValueEnhanced("disableSendMail");
                string fromEmailAddress = Core.GetAppSettingValueEnhanced("fromEmailAddress");
                string smtpFromName = Core.GetAppSettingValueEnhanced("smtpFromName");

                if (String.IsNullOrWhiteSpace(fromEmailAddress))
                {
                    fromEmailAddress = from;
                }

                // Create New Email Message
                using (MailMessage mailMessage = new MailMessage())
                {
                    // From
                    MailAddress mailAddress = null;

                    if (string.IsNullOrWhiteSpace(smtpFromName))
                        mailAddress = new MailAddress(fromEmailAddress);
                    else
                        mailAddress = new MailAddress(fromEmailAddress, smtpFromName);

                    mailMessage.From = mailAddress;

                    // To
                    if (to != null)
                    {
                        foreach (string toEmail in to.Split(',', ';'))
                        {
                            if (!string.IsNullOrEmpty(toEmail))
                            {
                                MailAddress receiverMailAddress = new MailAddress(toEmail);
                                mailMessage.To.Add(receiverMailAddress);
                            }
                        }
                    }

                    // CC
                    if (cc != null)
                    {
                        foreach (string toCCEmail in cc.Split(',', ';'))
                        {
                            if (!string.IsNullOrEmpty(toCCEmail))
                            {
                                MailAddress cCMailAddress = new MailAddress(toCCEmail);
                                mailMessage.CC.Add(cCMailAddress);
                            }
                        }
                    }

                    // BCC
                    if (bcc != null)
                    {
                        foreach (string toBCCEmail in bcc.Split(',', ';'))
                        {
                            if (!string.IsNullOrEmpty(toBCCEmail))
                            {
                                MailAddress bCCMailAddress = new MailAddress(toBCCEmail);
                                mailMessage.Bcc.Add(bCCMailAddress);
                            }
                        }
                    }

                    if (isBodyHtml)
                    {
                        body = body.Replace("<br/>", "<br>");
                        body = body.Replace(Environment.NewLine, "<br>");
                    }

                    // Subject and Body
                    mailMessage.Subject = subject;
                    mailMessage.Body = body;
                    mailMessage.IsBodyHtml = isBodyHtml;

                    //Attachments
                    if (attachCollection != null)
                    {
                        foreach (var attach in attachCollection)
                        {
                            mailMessage.Attachments.Add(attach);
                        }
                    }

                    if (mailMessage.To.Count > 0)
                    {
                        using (var smtpClient = new SmtpClient())
                        {
                            bool parseEnableSsl = bool.TryParse(enableSsl, out parseEnableSsl) ? parseEnableSsl : parseEnableSsl;
                            bool parseDisableSendMail = bool.TryParse(disableSendMail, out parseDisableSendMail) ? parseDisableSendMail : parseDisableSendMail;

                            smtpClient.UseDefaultCredentials = false;
                            smtpClient.Host = smtpServer;
                            smtpClient.Port = int.Parse(smtpPort);
                            smtpClient.EnableSsl = parseEnableSsl;
                            smtpClient.Credentials = new NetworkCredential(smtpUserName, smtpPassword);

                            if (!parseDisableSendMail)
                            {
                                smtpClient.Send(mailMessage);
                                log.Debug("Email Queue Id: " + id + " is sent out successfully.");
                            }
                        };

                        return true;
                    }
                    else
                    {
                        log.Debug("The receiver email address for Email Queue Id: " + id + " is empty.");
                        return false;
                    }
                }

            }
            catch (Exception ex)
            {
                log.Debug("Failed to sending email. Reason: " + ex.Message + "\r\n" + ex.StackTrace);
                SendErrorMail(ex.ToString());
                return false;
            }
        }
        #endregion

        #region Events

        static void Main(string[] args)
        {
            try
            {
                log.Debug("Email Queue Handler Started.");

                if (args.Length != 1)
                {
                    throw new Exception("Event Handler must received an argument which is execution type.");
                }

                executionType = (ExecutionType)Enum.Parse(typeof(ExecutionType), args[0].ToString(), true);
                log.Debug("Execution Type: " + executionType.ToString());

                //dbADO.SetConnection(ConfigurationManager.ConnectionStrings["CorpBookingADO"].ConnectionString);

                //Code for Testing
                //alphareds79@gmail.com
                //CommonServiceController.InsertEmailQueue("", "noreply@mayflower-group.com", "kevinkuan@alphareds.com", "jhkuan1989@hotmail.com", "",
                //    "Testing", Core.getMailTemplate("temporarypassword", null), null, true);
                //byte[] bytes1 = System.IO.File.ReadAllBytes(@"C:\WLAN_1_Driver.log");
                //byte[] bytes2 = System.IO.File.ReadAllBytes(@"C:\mydll\capicom.zip");
                //List<Attachment> a = new List<Attachment>();
                //a.Add(new Attachment(new MemoryStream(bytes1), "1.log"));
                //a.Add(new Attachment(new MemoryStream(bytes2), "2.zip"));
                //CommonServiceController.InsertEmailQueue("", "alphareds79@gmail.com", "kevinkuan@alphareds.com", "jhkuan1989@hotmail.com", "",
                //    "This is a testing email Testing", Core.getMailTemplate("temporarypassword", null), a, true);

                if (executionType == ExecutionType.ProcessEmailQueue)
                {
                    ProcessEmailQueue();
                }

                log.Debug("Email Queue Handler Process Completed.");
            }
            catch (Exception ex)
            {
                SendErrorMail(ex.ToString());
                log.Error("Unable to complete the Email Queue Handler process.", ex);
            }
        }


        #endregion
    }
}
