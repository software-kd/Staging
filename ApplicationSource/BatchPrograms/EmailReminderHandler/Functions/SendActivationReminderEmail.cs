using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.CommonController;
using Alphareds.Module.Common;
using Alphareds.Module.MemberController;
using Alphareds.Module.Model.Database;
using System.Collections;
using System.Configuration;
using Alphareds.Library.DatabaseHandler;
using System.Data.SqlClient;
using System.Data;

namespace EmailReminderHandler.Functions
{
    class ActivationReminderEmail
    {
        private static DatabaseHandlerMain dbADO = new DatabaseHandlerMain();
        #region Member variables Declarations

        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog emaillog = log4net.LogManager.GetLogger("EmailLogger");
        
        private static MayFlower db = new MayFlower();

        #endregion
        public static void Send(EmailMarketingQueue list)
        {
            log.Debug("SendActivationReminderEmail Started.");

            try
            {
                string registeredUserID = Alphareds.Module.Cryptography.Cryptography.AES.Encrypt(list.UserID.ToString()); 

                string urlEncodeRegisteredUserID = System.Web.HttpUtility.UrlEncode(registeredUserID);

                Hashtable ht = new Hashtable(); // hash need to add things
                string hostURL = Core.GetAppSettingValueEnhanced("HostURL");
                string ActiveUrlFrontPart = Core.GetAppSettingValueEnhanced("ActiveUrlFrontPart");
                string ActiveUrlKeyPart = string.Format("token={0}&Email={1}&atype={2}", urlEncodeRegisteredUserID, list.Recipient, "nopass");
                string ActiveUrlFull = hostURL + ActiveUrlFrontPart + ActiveUrlKeyPart;
                string encodeUrl = System.Web.HttpUtility.UrlEncode(ActiveUrlFull);
                ht.Add("<#ActiveURL>", encodeUrl);

                
                string checkClickLink = string.Format("{0}/member/CheckEmailLinkClicked?orderId=&userID=&activeUserID={1}&FunctionType={2}&url=", hostURL, list.UserID, "ActivationReminder");
                ht.Add("<#checkClick>", checkClickLink);

                string mailTemplate = Core.getMailTemplateByID(list.TemplateID, ht);
                

                bool isSuccess = CommonServiceController.SendEmail(list.Recipient, "Last 48 hours to update your Mayflower account!", mailTemplate);
                if (isSuccess)
                {
                    mailTemplate = mailTemplate.Replace("'", "''");

                    var DB = new Service.Database.mysql(ConfigurationManager.ConnectionStrings["MySqlConnector"].ToString());

                    //Reminder!!!! When deploy production need change table naming """""" Stg_EmailMarketingQueueLog for staging , EmailMarketingQueueLog for production
                    //store MYSQL
                    if (Core.IsForStaging)
                    {
                        DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.Stg_EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, MemberRegistrationDate, FullName, UserID, Recipient, CC, Bcc, CreatedDate) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.MemberRegistrationDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.FullName,
                                list.UserID,
                                list.Recipient,
                                list.CC,
                                list.Bcc,
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff000")
                            ));
                        DB.Dispose();
                    }
                    else
                    {
                        DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, MemberRegistrationDate, FullName, UserID, Recipient, CC, Bcc, CreatedDate) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.MemberRegistrationDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.FullName,
                                list.UserID,
                                list.Recipient,
                                list.CC,
                                list.Bcc,
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff000")
                            ));
                        DB.Dispose();
                    }
                    //remove MSSQL
                    SqlCommand command = new SqlCommand();

                    if (command.Connection == null)
                    {
                        command = dbADO.OpenConnection(command);
                    }
                    var emailQueueInsertQuery = "DELETE FROM [Mayflower].[BOS].[EmailMarketingQueue] WHERE EmailMarketingQueueID = @emailMarketingQueueID";
                    command.CommandText = emailQueueInsertQuery;
                    command.Parameters.Clear();
                    command.Parameters.Add(new SqlParameter("@EmailMarketingQueueID", list.EmailMarketingQueueID));
                    command.ExecuteNonQuery();
                    command.Transaction.Commit();
                    command.Dispose();
                }

                string debugMessage = isSuccess ? "UserID:" + list.UserID.ToString() + " email is successfully sent" : "UserID:" + list.UserID.ToString() + " email is fail sent";
                log.Debug(debugMessage);

                log.Debug("SendActivationReminderEmail Successfully Ended.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete SendActivationReminderEmail." + ex.ToString());
                throw ex;
            }
        }
    }
}
