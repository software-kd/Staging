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
using System.Data.SqlClient;
using Alphareds.Library.DatabaseHandler;

namespace EmailReminderHandler.Functions
{
    class TravelCreditExpiredEmail
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
            log.Debug("SendTravelCreditExpiredEmail Started.");

            try
            {
                Hashtable ht = new Hashtable();
                string hostURL = Core.GetAppSettingValueEnhanced("HostURL");
                string checkClickLink = string.Format("{0}/member/CheckEmailLinkClicked?orderId=&userID={1}&FunctionType={2}&activeUserID=&url=", hostURL, list.UserID, "TravelCreditExpired");
                ht.Add("<#checkClick>", checkClickLink);

                string mailTemplate = Core.getMailTemplateByID(list.TemplateID, ht);

                bool isSuccessTest = CommonServiceController.SendEmail(list.Recipient, "Your Mayflower's Travel Credit is almost expired!", mailTemplate);
                if (isSuccessTest)
                {
                    mailTemplate = mailTemplate.Replace("'", "''");
                    var DB = new Service.Database.mysql(ConfigurationManager.ConnectionStrings["MySqlConnector"].ToString());

                    //Reminder!!!! When deploy production need change table naming """""" Stg_EmailMarketingQueueLog for staging , EmailMarketingQueueLog for production
                    //Store MYSQL
                    if (Core.IsForStaging)
                    {
                        DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.Stg_EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, TravelCreditExpiredDate, MemberRegistrationDate, FullName, UserID, Recipient, CC, Bcc, CreatedDate) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.TravelCreditExpiredDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
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
                        DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, TravelCreditExpiredDate, MemberRegistrationDate, FullName, UserID, Recipient, CC, Bcc, CreatedDate) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.TravelCreditExpiredDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
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
                }
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete SendTravelCreditExpiredEmail.");
                throw ex;
            }
            log.Debug("SendTravelCreditExpiredEmail End.");
        }
    }
}
