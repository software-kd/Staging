using Mayflower.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Linq.Dynamic;
using System.Collections;
//using System.Data.Objects.SqlClient;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Data.SqlClient;
using NLog;
using Alphareds.Module.Model.Database;
using Alphareds.Module.Model;

namespace Mayflower.General
{
    public class Core
    {
        public string smtpUserName;
        public string smtpPassword;
        public string fromEmailAddress;
        public string enableSsl;
        public string smtpServer;
        public string smtpPort;
        public static string adminEmail;

        /// <summary>
        /// Disable for local testing.
        /// Because if SMTP fail it will spend some time's to attemp, cause slow.
        /// </summary>
        private string disableSendMail;

        public Core()
        {
            smtpUserName = GetSettingValue("smtpUserName");
            smtpPassword = GetSettingValue("smtpPassword");
            smtpServer = GetSettingValue("smtpServer");
            smtpPort = GetSettingValue("smtpPort");
            fromEmailAddress = GetSettingValue("fromEmailAddress");
            enableSsl = GetSettingValue("enableSsl");
            disableSendMail = GetSettingValue("disableSendMail");
        }
        
        #region Bind Dropdown data
        public static List<SelectListItem> BindDropdownListData(string tablename, string columnIDName, string columnName, object selectedId)
        {
            MayFlower db = new MayFlower(); //wtf whyy?!!!  
            List<SelectListItem> resultList = new List<SelectListItem>();
            int SelectedIdValue;

            //Winston20160103 - this part not clean.. will tidy up when free
            var type = Assembly.GetExecutingAssembly()
                        .GetTypes()
                        .FirstOrDefault(t => t.Name == tablename);

            if (type != null)
            {
                //selectedText = (selectedText == null) ? string.Empty : selectedText;  
                //SelectedIdValue = int.TryParse(selectedid, out SelectedIdValue) ? SelectedIdValue : 0;  
                //test start
                SelectedIdValue = selectedId != null ? (int.TryParse(selectedId.ToString(), out SelectedIdValue) ? SelectedIdValue : -1) : -1;
                //test end 

                var result = db.Set(type)
                                 .AsQueryable()
                                 .Where("IsActive == true")
                                 .OrderBy(columnName).AsEnumerable().Select(x => new SelectListItem
                                 {
                                     //SqlFunctions.StringConvert((double?)GetPropValue(x, columnIDName))
                                     Value = GetPropValue(x, columnIDName).ToString(),
                                     Text = GetPropValue(x, columnName),
                                     //Selected = (selectedText == GetPropValue(x, columnName)) ? true : false
                                     Selected = (SelectedIdValue == GetPropValue(x, columnIDName)) ? true : false
                                 });

                return resultList = result.ToList();
            }

            return resultList;
        }

        private static object GetPropValue(object obj, string propName)
        {
            return obj.GetType().GetProperty(propName).GetValue(obj, null);
        }
        #endregion

        #region Emails
        //static bool OurCertificateValidation(object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        //{
        //    return certificate.GetCertHashString() == "6B8C79AB966D70277BA86E6F820859A2B5B8CCC0"; // SHA-1 fingerprint

        //}

        public static string getMailTemplate(string Code, Hashtable htParameter)
        {
            string getText = string.Empty;
            StringBuilder sb = new StringBuilder();

            Uri requestUrl = new Uri(HttpContext.Current.Request.Url.ToString());
            string hostAppPath = string.Empty;
            if (HttpContext.Current.Request.ApplicationPath != "/")
            {
                hostAppPath = HttpContext.Current.Request.ApplicationPath;
            }
            string hosturl = requestUrl.GetLeftPart(UriPartial.Authority);

            //ZK move template to db
            var useDBTemp = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("UseDBTemplate");
            if (useDBTemp == "true")
            {
                htParameter.Add("<#HostURL>", Core.GetSettingValue("HostURL"));
                MayFlower db = new MayFlower();

                var emailTemplate = db.EmailTemplates.FirstOrDefault(x => x.TemplateType == Code.ToLower() && x.IsActive == true);

                getText = emailTemplate.EmailTemplate1.ToString();
                if (htParameter != null)
                {
                    foreach (var key in htParameter.Keys)
                    {
                        getText = getText.Replace(key.ToString(), htParameter[key].ToString());
                    }
                }
                return getText;
            }
            else
            {
                #region switch case
                switch (Code.ToLower())
                {
                    case "travellerregister":
                        sb.Append("Hi <#UserName>!<br/><br/>");
                        sb.Append("We need to make sure that this account is not created by those people who have too much time in their hands.<br/><br/>");
                        sb.Append("Thus, please assist to <a href='" + hosturl + "/Member/ActivateByEmailLink?Token=<#Token>&Email=<#Email>'><strong><i>Click me!</i></strong></a> within the next 48 hours to confirm that this is your correct email address and verify that this is really you! <br/><br/>");

                        sb.Append("Glad you understand, <#UserName>!<br/><br/>");
                        sb.Append("Just for your information, logging into your account allows you to save holidays to your shortlist, view it and also to view any other bookings you’ve made. <br/><br/>");
                        sb.Append("Don’t worry, you are only require to do this once and thereafter, you may log into Mayflower using the email address and password you’d provided. <br/><br/>");
                        sb.Append("Thanks Buddy! <br/><br/>");

                        sb.Append("Sincerely<br/><br/>");
                        sb.Append("Mayflower – Your personalized Travel Specialist<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "travellerregister_simple":
                        sb.Append("Hi!<br/><br/>");
                        sb.Append("We need to make sure that this account is not created by those people who have too much time in their hands.<br/><br/>");
                        sb.Append("Thus, please assist to <a href='" + hosturl + "<#ActiveURL>'><strong><i>Click me!</i></strong></a> within the next 48 hours to confirm that this is your correct email address and verify that this is really you! <br/><br/>");

                        sb.Append("Glad you understand!<br/><br/>");
                        sb.Append("Just for your information, logging into your account allows you to save holidays to your shortlist, view it and also to view any other bookings you’ve made. <br/><br/>");
                        sb.Append("Don’t worry, you are only require to do this once and thereafter, you may log into Mayflower using the email address and password you’d provided. <br/><br/>");
                        sb.Append("Thanks Buddy! <br/><br/>");

                        sb.Append("Sincerely<br/><br/>");
                        sb.Append("Mayflower – Your personalized Travel Specialist<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "memberverified":
                        sb.Append("First and foremost, congratulation for joining one of the best online travel booking website! *high five*. <br/><br/>");
                        sb.Append("Now, you can search for the best Mayflower travel deals, view your bookings and save holidays into your shortlist for later viewing. <br/><br/>");
                        sb.Append("Sincerely<br/><br/>");
                        sb.Append("Mayflower<br/><br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "temporarypassword2":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("You have received this e-mail because your Company Admin has activated your account. <br/><br/>");
                        sb.Append("Your Login ID is: <#Email><br/>");
                        sb.Append("Your Password is: <#Password><br/>");
                        sb.Append("<a href='<#ActivationURL>'>[Please click here to login and activate your account.]</a><br/><br/>");
                        sb.Append("We are building up an exciting portfolio of services to deliver to you soon. We would keep you informed as soon as it is ready. <br/><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "passwordresetlink":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("It seems that someone is requesting a change of password, buddy. <br/>");
                        sb.Append("Is that you who asked for it? If yes, click on the link below within the next 24 hours to reset your password. <br/>");
                        sb.Append("<i><a href='<#ResetURL>'>RESET PASSWORD</a></i><br/><br/>");
                        sb.Append("If you didn’t, you can ignore this message and continue searching for the best deals at our Mayflower website.<br/><br/>");
                        sb.Append("Sincerely,<br/>");
                        sb.Append("Mayflower<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "resetpasswordnotice":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("You have received this e-mail because your Mayflower website account password has been changed recently on <#DateChange>. <br/><br/>");
                        sb.Append("<a href='<#LoginURL>'>[Please click here to login your account.]</a><br/><br/>");
                        sb.Append("If you did not make this change, please change your password immediately.<br/><br/>");
                        sb.Append("Sincerely,<br/>");
                        sb.Append("Mayflower<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "resetpassword":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("You have received this e-mail because your Mayflower.com account password has been reset. <br/><br/>");
                        sb.Append("Below is your new login information: <br/><br/>");
                        sb.Append("Your Login ID is: <#Email><br/>");
                        sb.Append("Your Password is: <#Password><br/>");
                        sb.Append("<a href='<#ResetURL>'>[Please click here to login your account.]</a><br/><br/>");
                        sb.Append("We are building up an exciting portfolio of services to deliver to you soon. We would keep you informed as soon as it is ready. <br/><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "temporarypassword":
                        sb.Append("Dear <#AgentName>,<br/><br/>");
                        sb.Append("You have received this e-mail because Mayflower Admin has activated your account.<br/><br/>");
                        sb.Append("Your Login ID is: <#Email><br/>");
                        sb.Append("Your Password is: <#Password><br/>");
                        sb.Append("To login to the system, you will need to click on the link below to login to your account using the above credentials:<br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Account/AgentLogin'>[Click here to login]</a><br/><br/>");
                        sb.Append("Thank you.<br/><br/>");
                        sb.Append("<i>This is a system-generated e-mail. Please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "registercompany":
                        sb.Append("Dear Admin,<br/><br/>");
                        sb.Append("You have a new company '<strong><#OrganizationName></strong>' registered to your system pending for activation <br/>");
                        sb.Append("Click on the link below to activate the '<strong><#OrganizationName>'s</strong>' account. <br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "<#EncAgentID>'>[Click here to active]</a><br/><br/>");
                        sb.Append("Thank you.<br/><br/>");
                        sb.Append("<i>This is a system-generated e-mail. Please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "temporarytravellerpassword":
                        sb.Append("Dear <#PersonName>,<br/><br/>");
                        sb.Append("You have received this e-mail because Mayflower Admin has activated your account.<br/><br/>");
                        sb.Append("Your Login ID is: <#Email><br/>");
                        sb.Append("Your Password is: <#Password><br/>");
                        sb.Append("To login to the system, you will need to click on the link below to login to your account using the above credentials:<br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Account/PersonLogin'>[Click here to login]</a><br/><br/>");
                        sb.Append("Thank you.<br/><br/>");
                        sb.Append("<i>This is a system-generated e-mail. Please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "sendapprovalreminder":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("You have 1 travel request pending your approval. <br/><br/>");
                        sb.Append("Pending User Booking: <#PendingUserName><br/>");
                        sb.Append("Booking ID: <#BookingID><br/>");
                        sb.Append("Passenger Name: <#PassengerName><br/>");
                        sb.Append("Amount: <#BookingAmount><br/>");
                        sb.Append("Origin: <#Origin><br/>");
                        sb.Append("Destination: <#Destination><br/>");
                        sb.Append("Depart Time: <#DepartureTime><br/><br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Booking/BookingDetail?bookingid=<#BookingID>'>[Please click here to check and approve the new booking.]</a><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "sendcompanypaymentreminder":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("You have 1 travel request pending company payment. <br/><br/>");
                        sb.Append("Pending User Company Payment: <#PendingUserName><br/>");
                        sb.Append("Booking ID: <#BookingID><br/><br/>");
                        sb.Append("Origin : <#Origin><br/>");
                        sb.Append("Destination : <#Destination><br/>");
                        sb.Append("Booking Amount : <#BookingAmount><br/>");
                        sb.Append("Number of Pax : <#numberOfPax><br/>");
                        sb.Append("Passenger : <#PassengerName><br/><br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Booking/BookingDetail?bookingid=<#BookingID>'>[Please click here to check and pay for the new booking.]</a><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "sendbookingupdatestatusreminder":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");

                        sb.Append("The booking ID <#BookingID> : <#BookingStatus> <br/><br/>");
                        sb.Append("Origin : <#DepartureStation><br/>");
                        sb.Append("Destination : <#ArrivalStation><br/>");
                        sb.Append("Departure Time : <#DepartureTime><br/>");
                        sb.Append("Booking Amount : <#BookingAmount><br/>");
                        sb.Append("Passenger : <#PassengerName><br/>");
                        sb.Append("Number of Pax : <#numberOfPax><br/><br/>");

                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Booking/BookingDetail?bookingid=<#BookingID>'>[Please click here to check the updated booking.]</a><br/><br/>");
                        sb.Append("<#ExtraInfo><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "pendingpaymentproof":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("You have new payment need to verify. <br/><br/>");
                        sb.Append("Booking ID: <#BookingID><br/><br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Booking/BookingDetail?bookingid=<#BookingID>'>[Please click here to verify the payment.]</a><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "newbooking":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("A new booking has been placed in the system. Kindly log in to update the expiry date for the booking. <br/><br/>");
                        sb.Append("Booking ID: <#BookingID><br/><br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Booking/BookingDetail?bookingid=<#BookingID>'>[Please click here to update the expiry date.]</a><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "approveandreject":
                        sb.Append("Dear <strong><#UserName></strong>,<br/><br/>");
                        sb.Append("The booking booked was <strong><#action></strong> by <#user>. <br/><br/>");
                        sb.Append("Booking ID: <#BookingID><br/><br/>");
                        sb.Append("Origin : <#DepartureStation><br/>");
                        sb.Append("Destination : <#ArrivalStation><br/>");
                        sb.Append("Booking Amount : <#BookingAmount><br/>");
                        sb.Append("Departure Time : <#DepartureTime><br/>");
                        sb.Append("Number of Pax : <#numberOfPax><br/>");
                        sb.Append("Passenger : <#passengerName><br/><br/>");
                        sb.Append("<a href='" + Core.GetSettingValue("HostURL") + "/Booking/BookingDetail?bookingid=<#BookingID>'>[Please click here to check the updated booking.]</a><br/><br/>");
                        sb.Append("Best Regards,<br/>");
                        sb.Append("Mayflower.com Team<br/>");
                        sb.Append("One Stop Travel Site<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;

                    case "subscription":
                        sb.Append("Thank you for subscribing our e-newsletter!<br/><br/>");
                        sb.Append("Feel free to check out and stay tuned with our latest promotions and deals through your own email at all times! <br/><br/>");
                        sb.Append("Should you require any further assistance, please contact our Customer Service Team at General Line: +603-9232-1888 or Email: cs@mayflower-group.com. <br/><br/>");
                        sb.Append("Thanks Buddy, have fun!<br/><br/>");
                        sb.Append("Sincerely, <br/>");
                        sb.Append("Mayflower – Your personalized Travel Specialist<br/><br/>");
                        sb.Append("<i>This is a system generated email, please do not reply.</i>");
                        sb.Append("<br/><br/><br/>");

                        break;
                }
                #endregion

                getText = sb.ToString();
                if (htParameter != null)
                {
                    foreach (var key in htParameter.Keys)
                    {
                        getText = getText.Replace(key.ToString(), htParameter[key].ToString());
                    }
                }
                return getText;
            }
            //end
        }

        #endregion

        #region get configuration value
        public static string GetSettingValue(string key)
        {
            try
            {
                switch (key.ToLower())
                {
                    case "pagesize": return ConfigurationManager.AppSettings["PageSize"].ToString(); //break;
                    case "sqliteconn": return ConfigurationManager.AppSettings["SQLite"].ToString();
                    case "smtpserver": return ConfigurationManager.AppSettings["smtpserver"].ToString();
                    case "smtpusername": return ConfigurationManager.AppSettings["smtpusername"].ToString();
                    case "smtppassword": return ConfigurationManager.AppSettings["smtppassword"].ToString();
                    case "enablessl": return ConfigurationManager.AppSettings["enableSsl"].ToString();
                    case "smtpport": return ConfigurationManager.AppSettings["smtpport"].ToString();
                    case "fromemailaddress": return ConfigurationManager.AppSettings["fromemailaddress"].ToString();
                    case "toadminemailaddress": return ConfigurationManager.AppSettings["toAdminEmailAddress"].ToString();
                    case "hosturl": return ConfigurationManager.AppSettings["HostURL"].ToString();
                    case "sabresignaturekey": return ConfigurationManager.AppSettings["sabreSignatureKey"].ToString();
                    case "dayadvance": return ConfigurationManager.AppSettings["DayAdvance"].ToString();
                    case "showchild": return ConfigurationManager.AppSettings["ShowChild"].ToString();
                    case "showinfant": return ConfigurationManager.AppSettings["ShowInfant"].ToString();
                    case "adminemail": return ConfigurationManager.AppSettings["AdminEmail"].ToString();
                    case "showsubmitcompanypayment": return ConfigurationManager.AppSettings["ShowSubmitCompanyPayment"].ToString();
                    case "showselectpaymentmethod": return ConfigurationManager.AppSettings["ShowSelectPaymentMethod"].ToString();
                    case "tawktoaccount": return ConfigurationManager.AppSettings["TawkToAccount"].ToString();
                    case "disablesendmail": return ConfigurationManager.AppSettings["disableSendMail"].ToString();
                    case "fixconveniencefee": return ConfigurationManager.AppSettings["FixConvenienceFee"].ToString();
                    default:
                        break;
                }
            }
            catch (Exception)
            {
                //ErrorSignal.FromCurrentContext().Raise(e);
            }
            return string.Empty;
        }
        #endregion

        #region password generation/encrypt/decryption

        public static Tuple<string, string, string> GeneratePassword()
        {
            string password = Membership.GeneratePassword(6, 2);
            //auto-gen salt... give the extra umph to password encrypt/decrypt 
            string PasswordSalt = new Random().Next().ToString();

            string encryptedPassword = Encrypt(password, PasswordSalt);

            return Tuple.Create(encryptedPassword, PasswordSalt, password);
        }

        public static string GeneratePasswordSalt()
        {
            //auto-gen salt... give the extra umph to password encrypt/decrypt 
            string PasswordSalt = new Random().Next().ToString();

            return PasswordSalt;
        }

        public static byte[] GenerateSaltedHash(byte[] password, byte[] Passwordsalt)
        {
            HashAlgorithm algorithm = new SHA256Managed();

            byte[] passwordWithSaltBytes = new byte[password.Length + Passwordsalt.Length];

            for (int i = 0; i < password.Length; i++)
            {
                passwordWithSaltBytes[i] = password[i];
            }
            for (int i = 0; i < Passwordsalt.Length; i++)
            {
                passwordWithSaltBytes[password.Length + i] = Passwordsalt[i];
            }

            return algorithm.ComputeHash(passwordWithSaltBytes);
        }

        public static bool ComparePassword(byte[] Password1, byte[] Password2)
        {
            if (Password1.Length != Password2.Length)
            {
                return false;
            }

            for (int i = 0; i < Password1.Length; i++)
            {
                if (Password1[i] != Password2[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static string Encrypt(string password, string salt)
        {
            #region hide this original code
            //byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            //byte[] saltBytes = Encoding.Unicode.GetBytes(salt);

            //byte[] cipherBytes = ProtectedData.Protect(passwordBytes, saltBytes, DataProtectionScope.CurrentUser);

            // return Convert.ToBase64String(cipherBytes);
            #endregion

            #region testing
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            byte[] saltBytes = Encoding.Unicode.GetBytes(salt);

            return Convert.ToBase64String(GenerateSaltedHash(passwordBytes, saltBytes));
            #endregion

        }

        public static string Decrypt(string password, string salt)
        {
            #region hide this original code
            //byte[] cipherBytes = Convert.FromBase64String(password);
            //byte[] saltBytes = Encoding.Unicode.GetBytes(salt);

            //byte[] passwordBytes = ProtectedData.Unprotect(cipherBytes, saltBytes, DataProtectionScope.CurrentUser);

            //return Encoding.Unicode.GetString(passwordBytes);
            #endregion

            #region testing

            #endregion

            return string.Empty;
        }
        #endregion

    }

    #region custom Login
    //Generate cookie for forms auth
    public static class HttpResponseBaseExtensions
    {
        public static int SetAuthCookie<T>(this HttpResponseBase responseBase, string name, bool rememberMe, T userData)
        {
            return Alphareds.Module.Common.HttpResponseBaseExtensions.SetAuthCookie<T>(responseBase, name, rememberMe, userData);
        }
    }

    interface ICustomPrincipal : IPrincipal
    {
        int Id { get; set; }
        string FirstName { get; set; }
        bool IsLoginPasswordNotSetup { get; set; }
    }

    public class CustomPrincipal : Alphareds.Module.Model.UserData, ICustomPrincipal
    {
        private MayFlower db = new MayFlower();

        private IIdentity _identity;
        private string[] _roles;

        // IPrincipal Implementation
        public bool IsInRole(string role)
        {
            return Array.BinarySearch(_roles, role) >= 0 ? true : false;
        }
        public IIdentity Identity
        {
            get { return this._identity; }
        }

        public CustomPrincipal(IIdentity identity)
        {
            _identity = identity;
        }

        public CustomPrincipal(IIdentity identity, string[] roles)
        {
            _identity = identity;
            _roles = new string[roles.Length];
            roles.CopyTo(_roles, 0);
            Array.Sort(_roles);
        }

        public int Id { get; set; }

        //Role Check 
        // Checks whether a principal is in all of the specified set of roles
        public bool IsInAllRoles(params string[] roles)
        {
            foreach (string searchrole in roles)
            {
                if (Array.BinarySearch(_roles, searchrole) < 0)
                    return false;
            }
            return true;
        }
        // Checks whether a principal is in any of the specified set of roles
        public bool IsInAnyRoles(params string[] roles)
        {
            foreach (string searchrole in roles)
            {
                if (Array.BinarySearch(_roles, searchrole) > 0)
                    return true;
            }
            return false;
        }
    }

    public class LoginClass
    {
        public MayFlower MayFlowerDBContext { get; set; }
        public UserData UserData { get; set; }
        public User User { get; set; }

        public LoginClass(MayFlower dbContext = null)
        {
            SetupDBConnection(dbContext);
        }

        public LoginClass(string email, string password, MayFlower dbContext = null, bool isAgentLogin = false)
        {
            SetupDBConnection(dbContext);

            User _inUser = null;

            UserData = _UserData(email, password, out _inUser, false);
            User = _inUser;
        }

        public LoginClass(User __user, MayFlower dbContext = null)
        {
            SetupDBConnection(dbContext);

            User _inUser = null;
            UserData = _UserData(__user.Email, "", out _inUser, false, __user);
            User = _inUser;
        }

        /// <summary>
        /// B2B use only.
        /// </summary>
        public LoginClass(string userLoginID, string password, bool b2b, MayFlower dbContext = null)
        {
            SetupDBConnection(dbContext);

            User _inUser = null;
            UserData = _UserData(userLoginID, password, out _inUser, true);
            User = _inUser;
        }

        private UserData _UserData(string identityLogin, string password, out User userContext, bool isB2B,
            User __userInject = null)
        {
            UserData userData = new UserData
            {
                Email = identityLogin,
                LoginID = identityLogin,
            };

            // 2017/10/02 - Changed to allow duplicate email, so need specific user type only MEMBER
            User user = null;

            if (__userInject != null)
            {
                user = __userInject;
            }
            else if (isB2B)
            {
                user = MayFlowerDBContext.Users.FirstOrDefault(u => u.UserLoginID == identityLogin && (u.UserTypeCode == "FRELCR" || u.UserTypeCode == "AGT"));
            }
            else
            {
                user = MayFlowerDBContext.Users.FirstOrDefault(u => u.Email == identityLogin && (u.UserTypeCode == "MEM"));
            }

            if (user != null)
            {
                userContext = user;
                string password1 = Core.Encrypt(password, user.PwdSalt);

                if (password1 == user.Pwd || (__userInject != null))
                {
                    var userDetail = user.UserDetails.FirstOrDefault();
                    var enumerableUserRoles = user.UsersInRoles.AsEnumerable(); // cache roles to memory, reduce db call

                    userData = new UserData
                    {
                        LoginID = identityLogin,
                        Email = user.Email,
                        FirstName = string.IsNullOrEmpty(userDetail.FirstName) ? "" : userDetail.FirstName.Trim(),
                        IsActive = user.IsActive,
                        IsProfileActive = user.IsProfileActivated,
                        IsComapnyAdmin = enumerableUserRoles.Any(x => x.Role.RoleCode == "COM"),
                        IsAgent = user.UserTypeCode == "AGT",
                        UserId = user.UserID,
                        IsLoginPasswordNotSetup = user.Pwd == null || user.Pwd == "NA",

                        IsDisplayAllSupplier = user.Organization?.OrganizationInTieringGroups?.Any(a => a.TieringGroup.IsDisplaySupplier) ?? false,
                        IsHtlSameDayAllow = user.Organization?.OrganizationInTieringGroups?.Any(a => a.TieringGroup.IsSameDayAllow) ?? false,

                        UserTypeCode = user.UserTypeCode,
                        CreditTerm = user.Organization?.CreditTermInDay ?? 0,
                        OrganizationID = user.OrganizationID,
                        OrganizationLogo = user.UserTypeCode == "AGT" ? user.Organization?.OrganizationLogo : null,
                    };
                }
            }
            else
            {
                userContext = null;
            }

            return userData;
        }

        protected void SetupDBConnection(MayFlower dbContext)
        {
            MayFlowerDBContext = dbContext ?? new MayFlower();
        }

    }
    #endregion

    #region custom role provider

    public class CustomRoleProvider : RoleProvider
    {
        private MayFlower db = new MayFlower();

        public override string[] GetUsersInRole(string roleName)
        {
            throw new NotImplementedException();
        }

        public override bool IsUserInRole(string UserId, string RoleId)
        {
            int id = string.IsNullOrEmpty(UserId) ? 0 : Convert.ToInt32(UserId);
            int role = string.IsNullOrEmpty(RoleId) ? 0 : Convert.ToInt32(RoleId);

            var user = db.UsersInRoles.Any(x => x.UserId == id && x.RoleId == role);
            if (!user)
                return false;
            return true;
            //return user.UserRoles != null && user.UserRoles.Select(u => u.Role).Any(r => r.RoleName == roleName);
        }

        public override string[] GetRolesForUser(string UserId)
        {
            int id = string.IsNullOrEmpty(UserId) ? 0 : Convert.ToInt32(UserId);
            string[] roleName;

            using (MayFlower dbContext = new MayFlower())
            {
                var user = dbContext.Users.SingleOrDefault(u => u.UserID == id);

                if (user == null)
                {
                    dbContext.Dispose();
                    return new string[] { };
                }
                else
                {
                    roleName = dbContext.UsersInRoles.Where(ui => ui.UserId == id).Select(e => e.Role.RoleName).ToArray();
                    dbContext.Dispose();
                    return roleName == null ? new string[] { } : roleName;
                }
            }
        }

        public override string[] GetAllRoles()
        {
            return db.Roles.Select(r => r.RoleName).ToArray();
        }

        #region not in use
        public override string ApplicationName
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override void CreateRole(string roleName)
        {
            throw new NotImplementedException();
        }
        public override void AddUsersToRoles(string[] usernames, string[] roleNames)
        {
            throw new NotImplementedException();
        }
        public override bool DeleteRole(string roleName, bool throwOnPopulatedRole)
        {
            throw new NotImplementedException();
        }
        public override string[] FindUsersInRole(string roleName, string usernameToMatch)
        {
            throw new NotImplementedException();
        }
        public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
        {
            throw new NotImplementedException();
        }
        public override bool RoleExists(string roleName)
        {
            throw new NotImplementedException();
        }
        #endregion

    }

    #endregion

    #region custom cache
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            filterContext.HttpContext.Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            filterContext.HttpContext.Response.Cache.SetValidUntilExpires(false);
            filterContext.HttpContext.Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            filterContext.HttpContext.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            filterContext.HttpContext.Response.Cache.SetNoStore();

            base.OnResultExecuting(filterContext);
        }
    }
    #endregion

}