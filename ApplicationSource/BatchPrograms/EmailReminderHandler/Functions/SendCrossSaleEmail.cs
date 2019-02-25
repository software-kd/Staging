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
    class CrossSaleEmail
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
            log.Debug("SendCrossSaleEmail Started.");

            try
            {
                Hashtable ht = new Hashtable(); 

                string hashKeyString = "";
                int imageKeyCount = 1;
                //string hotelList = "";
                var dbHotelList = db.EmailMarketingQueueHotels.Where(x => x.EmailMarketingQueueID == list.EmailMarketingQueueID && x.WebImage != null).ToList();
                if (dbHotelList.Count() > 0)
                {
                    for (int i = 1; i <= dbHotelList.Count() && i < 7; i++)
                    {
                        hashKeyString = "<#HotelImageUrl" + imageKeyCount + ">";
                        ht.Add(hashKeyString, dbHotelList[i].WebImage);
                        imageKeyCount++;
                        
                    }
                    if (dbHotelList.Count() < 6) //for place "" for the key in template (remaining position), avoid <img src="tag"> show no/error image
                    {
                        for (int i = imageKeyCount + 1; i < 7; i++)
                        {
                            hashKeyString = "<#HotelImageUrl" + i + ">";
                            ht.Add(hashKeyString, "");
                            imageKeyCount++;
                        }
                    }
                }
                else
                {
                    for (int i = 1; i <= dbHotelList.Count() && i < 7; i++)
                    {
                        hashKeyString = "<#HotelImageUrl" + i + ">";
                        ht.Add(hashKeyString, "");
                    }
                }

                string hostURL = Core.GetAppSettingValueEnhanced("HostURL");
                string checkClickLink = string.Format("{0}/member/CheckEmailLinkClicked?orderId={1}&userID=&FunctionType={2}&activeUserID=&url=", hostURL, list.OrderID, "CrossSalesFlight");
                ht.Add("<#checkClick>", checkClickLink);

                string mailTemplate = Core.getMailTemplateByID(list.TemplateID, ht);

                bool isSuccessTest = CommonServiceController.SendEmail(list.Recipient, "Good news! We’ll pay RM80 on your hotel booking!", mailTemplate);
                if (isSuccessTest)
                {
                    mailTemplate = mailTemplate.Replace("'", "''");

                    var DB = new Service.Database.mysql(ConfigurationManager.ConnectionStrings["MySqlConnector"].ToString());

                    //Reminder!!!! When deploy production need change table naming """""" Stg_EmailMarketingQueueLog for staging , EmailMarketingQueueLog for production
                    //Store MYSQL
                    if (Core.IsForStaging)
                    {
                        if (list.UserID.HasValue)
                        {
                            DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.Stg_EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, BookingDate, SuperPNRID, OrderID, SuperPNRNo, FullName, UserID, Recipient, CC, Bcc, FlightDestinationCode, FlightDestination, CreatedDate, PromoID, PromoCode) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.BookingDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.SuperPNRID,
                                list.OrderID,
                                list.SuperPNRNo,
                                list.FullName,
                                list.UserID,//list.UserID.HasValue ? list.UserID : null,
                                list.Recipient,
                                list.CC,
                                list.Bcc,
                                list.FlightDestinationCode,
                                list.FlightDestination,
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.PromoID,
                                list.PromoCode
                            ));
                            DB.Dispose();
                        }
                        else
                        {
                            DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.Stg_EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, BookingDate, SuperPNRID, OrderID, SuperPNRNo, FullName, Recipient, CC, Bcc, FlightDestinationCode, FlightDestination, CreatedDate, PromoID, PromoCode) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.BookingDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.SuperPNRID,
                                list.OrderID,
                                list.SuperPNRNo,
                                list.FullName,
                                //list.UserID,//list.UserID.HasValue ? list.UserID : null,
                                list.Recipient,
                                list.CC,
                                list.Bcc,
                                list.FlightDestinationCode,
                                list.FlightDestination,
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.PromoID,
                                list.PromoCode
                            ));
                            DB.Dispose();
                        }  
                    }
                    else
                    {
                        if (list.UserID.HasValue)
                        {
                            DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, BookingDate, SuperPNRID, OrderID, SuperPNRNo, FullName, UserID, Recipient, CC, Bcc, FlightDestinationCode, FlightDestination, CreatedDate, PromoID, PromoCode) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.BookingDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.SuperPNRID,
                                list.OrderID,
                                list.SuperPNRNo,
                                list.FullName,
                                list.UserID,//list.UserID.HasValue ? list.UserID : null,
                                list.Recipient,
                                list.CC,
                                list.Bcc,
                                list.FlightDestinationCode,
                                list.FlightDestination,
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.PromoID,
                                list.PromoCode
                            ));
                            DB.Dispose();
                        }
                        else
                        {
                            DB.ExecuteNonQuery(string.Format("INSERT INTO MayflowerLogs.EmailMarketingQueueLog (EmailMarketingQueueID, EmailContent, TemplateID, EmailMarketingType, FunctionType, BookingDate, SuperPNRID, OrderID, SuperPNRNo, FullName, Recipient, CC, Bcc, FlightDestinationCode, FlightDestination, CreatedDate, PromoID, PromoCode) VALUES ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}');",
                                list.EmailMarketingQueueID,
                                mailTemplate,
                                list.TemplateID,
                                list.EmailMarketingType,
                                list.FunctionType,
                                list.BookingDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.SuperPNRID,
                                list.OrderID,
                                list.SuperPNRNo,
                                list.FullName,
                                //list.UserID,//list.UserID.HasValue ? list.UserID : null,
                                list.Recipient,
                                list.CC,
                                list.Bcc,
                                list.FlightDestinationCode,
                                list.FlightDestination,
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff000"),
                                list.PromoID,
                                list.PromoCode
                            ));
                            DB.Dispose();
                        }
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


                string debugMessage = isSuccessTest ? "EmailMarketingQueueID:" + list.EmailMarketingQueueID.ToString() + " email is successfully sent" : "EmailMarketingQueueID:" + list.EmailMarketingQueueID.ToString() + " email is fail sent";


                log.Debug(debugMessage);

            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete SendCrossSaleEmail.");
                throw ex;
            }

            log.Debug("SendCrossSaleEmail End.");
        }
    }
}
