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
    class CrossSalesInsuranceEmail
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
            log.Debug("SendCrossSalesInsuranceEmail Started.");

            try
            {
                Hashtable ht = new Hashtable();

                string hostURL = Core.GetAppSettingValueEnhanced("HostURL");

                string InsuranceUrlFrontPart = Core.GetAppSettingValueEnhanced("InsuranceUrlFrontPart");
                string InsuranceUrlKeyPart = string.Format("superPNRNo={0}", list.SuperPNRNo);
                string InsuranceUrlFull = hostURL + InsuranceUrlFrontPart + InsuranceUrlKeyPart;
                string encodeUrl = System.Web.HttpUtility.UrlEncode(InsuranceUrlFull);
                ht.Add("<#InsuranceURL>", encodeUrl);
                string checkClickLink = string.Format("{0}/member/CheckEmailLinkClicked?orderId={1}&FunctionType={2}&userID=&activeUserID=&url=", hostURL, list.OrderID, "insurance");
                ht.Add("<#checkClick>", checkClickLink);

                string mailTemplate = Core.getMailTemplateByID(list.TemplateID, ht);

                bool isSuccessSend = CommonServiceController.SendEmail(list.Recipient, "Travel Smarter With CHUBB Insurance", mailTemplate);
                if (isSuccessSend)
                {
                    mailTemplate = mailTemplate.Replace("'", "''");
                    var DB = new Service.Database.mysql(ConfigurationManager.ConnectionStrings["MySqlConnector"].ToString());

                    //Reminder!!!! When deploy production need change table naming """""" Stg_EmailMarketingQueueLog for staging , EmailMarketingQueueLog for production
                    //Store MYSQL
                    if (Core.IsForStaging)
                    {
                        DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.Stg_EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, " +
                            "BookingDate, SuperPNRID, OrderID, SuperPNRNo, FlightDestinationCode, FlightDestination, PromoID, PromoCode, " +
                            "FullName, UserID, Recipient, CC, Bcc, CreatedDate) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.BookingDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.SuperPNRID,
                                list.OrderID,
                                list.SuperPNRNo,
                                list.FlightDestinationCode,
                                list.FlightDestination,
                                list.PromoID,
                                list.PromoCode,
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
                        DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, " +
                            "BookingDate, SuperPNRID, OrderID, SuperPNRNo, FlightDestinationCode, FlightDestination, PromoID, PromoCode, " +
                            "FullName, UserID, Recipient, CC, Bcc, CreatedDate) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.BookingDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.SuperPNRID,
                                list.OrderID,
                                list.SuperPNRNo,
                                list.FlightDestinationCode,
                                list.FlightDestination,
                                list.PromoID,
                                list.PromoCode,
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
                log.Debug("Unable to complete SendCrossSalesInsuranceEmail.");
                throw ex;
            }
            log.Debug("SendCrossSalesInsuranceEmail End.");
        }
    }
}
