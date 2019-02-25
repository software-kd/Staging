using Alphareds.Module.Common;
using Alphareds.Module.Model.Database;
using Alphareds.Module.PaymentController;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentQueueHandler
{
    public partial class PaymentQueueService : ServiceBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public PaymentQueueService()
        {
            InitializeComponent();

            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("Mayflower Payment Scheduler Service"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "Mayflower Payment Scheduler Service", "MH Payment Log");
            }

            eventLog1.Source = "Mayflower Payment Scheduler Service";
            eventLog1.Log = "MH Payment Log";
        }

        protected override void OnStart(string[] args)
        {
            double requeryAfterSecond = Convert.ToDouble(GetAppSettingValueEnhanced("RequeryAfterSecond"));
            TimeSpan timeSpanRequery = TimeSpan.FromSeconds(requeryAfterSecond);
            // Set up a timer to trigger every minute.  

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = timeSpanRequery.TotalMilliseconds;
            timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer);
            timer.Start();

            int sendAfterHour = GetAppSettingValueEnhanced("SendRHIReminderEveryNHour").ToInt();
            sendAfterHour = sendAfterHour <= 0 ? 1 : sendAfterHour;
            System.Timers.Timer timerRHI = new System.Timers.Timer();
            timerRHI.Interval = TimeSpan.FromHours(sendAfterHour).TotalMilliseconds;
            timerRHI.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimerRHI);
            timerRHI.Start();
        }

        protected override void OnStop()
        {
        }

        private void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.  
            //await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            semaphoreSlim.Wait();
            MayFlower dbContext = new MayFlower();
            SqlCommand innerSqlCommand = new SqlCommand();

            try
            {
                Components.BookingList bookedList = new Components.BookingList(dbContext, innerSqlCommand);
                IEnumerable<SuperPNR> bookingProcessed = null;
                List<string> successProcessed = null;

                if (sender.GetType().Name == "String" && !string.IsNullOrWhiteSpace(sender.ToString()))
                {
                    string[] splitAttr = sender.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    bool includeEXP = splitAttr.Length == 2;

                    //bookedList.CheckUpdatePendingPayment(true, true, true, splitAttr[0]).ConfigureAwait(false).GetAwaiter().GetResult();
                    bookingProcessed = bookedList.GetBookingPaid(splitAttr[0], true, true, true);
                }
                else
                {
                    bookedList.CheckUpdatePendingPayment(true, true, true).ConfigureAwait(false).GetAwaiter().GetResult();
                    bookingProcessed = bookedList.GetAllBookingPaid(true, true, true);
                }

                // Save for released travel wallet / travel credit
                if (innerSqlCommand?.Transaction != null)
                {
                    try
                    {
                        innerSqlCommand.Transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        eventLog1.WriteEntry("Error when commit transaction for travel credit & travel wallet."
                            + Environment.NewLine + Environment.NewLine
                            + ex.GetBaseException().Message + Environment.NewLine + Environment.NewLine +
                            ex.StackTrace, EventLogEntryType.Warning);
                    }
                }

                if (bookingProcessed != null)
                {
                    foreach (var item in bookingProcessed)
                    {
                        foreach (var order in item.SuperPNROrders)
                        {
                            int paymentRecordCount = order.PaymentOrders.Count;
                            List<bool> _status = new List<bool>();
                            foreach (var paymentRecord in order.PaymentOrders)
                            {
                                string _paymentMethod = paymentRecord.PaymentMethodCode.ToLower();
                                if (_paymentMethod == "ipafpx")
                                {
                                    try
                                    {
                                        logger.Info(string.Format("SuperPNR {0} - {1} update from '{2}' to '{3}'."
                                            , item.SuperPNRID, item.SuperPNRNo, paymentRecord.PaymentStatusCode, "PAID"));
                                        paymentRecord.PaymentStatusCode = "PAID";
                                        _status.Add(true);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Error(ex, "Fail to update payment.", JsonConvert.SerializeObject(paymentRecord, Formatting.Indented,
                                            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
                                        _status.Add(false);
                                    }
                                }
                                else if (_paymentMethod == "ipacc")
                                {
                                    try
                                    {
                                        if (string.IsNullOrWhiteSpace(paymentRecord.Ipay88TransactionID))
                                        {
                                            throw new Exception($"Payment Order ({item.SuperPNRNo}) record transaction id empty or null, cannot recapture.");
                                        }
                                        else if (paymentRecord.PaymentStatusCode == "CAPT")
                                        {
                                            logger.Info(string.Format("SuperPNR {0} - {1} success captured before.", item.SuperPNRID, item.SuperPNRNo));
                                            _status.Add(true);
                                        }
                                        else
                                        {
                                            var respond = PaymentServiceController.iPay88
                                                .CapturePayment(paymentRecord.Ipay88TransactionID, paymentRecord.CurrencyCode, paymentRecord.PaymentAmount.ToString("n2"));

                                            if (respond.Status == "1") //  && respond.Desc == "Captured" // iPay88 capture doesn't respond desc
                                            {
                                                paymentRecord.PaymentStatusCode = "CAPT";
                                                logger.Info(string.Format("SuperPNR {0} - {1} success captured.", item.SuperPNRID, item.SuperPNRNo));
                                                _status.Add(true);
                                            }
                                            else
                                            {
                                                paymentRecord.RequeryStatusCode = "FAIL";
                                                logger.Info(string.Format("SuperPNR {0} - {1} capture failed.", item.SuperPNRID, item.SuperPNRNo)
                                                    + Environment.NewLine + Environment.NewLine + JsonConvert.SerializeObject(respond, Formatting.Indented));
                                                _status.Add(false);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        paymentRecord.RequeryStatusCode = "ERR";
                                        string objSerialize = null;

                                        try
                                        {
                                            objSerialize = JsonConvert.SerializeObject(paymentRecord, Formatting.Indented,
                                            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                                        }
                                        catch (Exception innerEX)
                                        {
                                            logger.Error(innerEX, "Error while serialize database object.");
                                        }

                                        logger.Error(ex, "Fail to capture payment." + Environment.NewLine + Environment.NewLine
                                            + objSerialize);
                                        _status.Add(false);
                                    }
                                }
                                else if (_paymentMethod == "adyenc")
                                {
                                    try
                                    {
                                        if (string.IsNullOrWhiteSpace(paymentRecord.Ipay88TransactionID))
                                        {
                                            throw new Exception($"Payment Order ({item.SuperPNRNo}) record transaction id empty or null, cannot recapture.");
                                        }
                                        else if (paymentRecord.PaymentStatusCode == "CAPT")
                                        {
                                            logger.Info(string.Format("SuperPNR {0} - {1} success captured before.", item.SuperPNRID, item.SuperPNRNo));
                                            _status.Add(true);
                                        }
                                        else
                                        {
                                            var respond = PaymentServiceController.Adyen
                                                .CapturePayment(paymentRecord.Ipay88RefNo, paymentRecord.Ipay88TransactionID, paymentRecord.CurrencyCode, paymentRecord.PaymentAmount.ToString("n2"));

                                            if (respond.Status == "1") //  && respond.Desc == "Captured"
                                            {
                                                paymentRecord.PaymentStatusCode = "CAPT";
                                                logger.Info(string.Format("SuperPNR {0} - {1} success captured.", item.SuperPNRID, item.SuperPNRNo));
                                                _status.Add(true);
                                            }
                                            else
                                            {
                                                paymentRecord.RequeryStatusCode = "FAIL";
                                                logger.Info(string.Format("SuperPNR {0} - {1} capture failed.", item.SuperPNRID, item.SuperPNRNo)
                                                    + Environment.NewLine + Environment.NewLine + JsonConvert.SerializeObject(respond, Formatting.Indented));
                                                _status.Add(false);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        paymentRecord.RequeryStatusCode = "ERR";
                                        string objSerialize = null;

                                        try
                                        {
                                            objSerialize = JsonConvert.SerializeObject(paymentRecord, Formatting.Indented,
                                            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                                        }
                                        catch (Exception innerEX)
                                        {
                                            logger.Error(innerEX, "Error while serialize database object.");
                                        }

                                        logger.Error(ex, "Fail to capture payment." + Environment.NewLine + Environment.NewLine
                                            + objSerialize);
                                        _status.Add(false);
                                    }
                                }
                                else if (_paymentMethod == "sc" || _paymentMethod == "ac")
                                {
                                    if (paymentRecord.PaymentStatusCode != "PAID")
                                    {
                                        SqlCommand sqlCommand = new SqlCommand();

                                        try
                                        {
                                            if (_paymentMethod == "sc")
                                            {
                                                PaymentServiceController.ClaimUserCredit(paymentRecord.CreatedByID, paymentRecord.CurrencyCode,
                                                    paymentRecord.OrderID, paymentRecord.PaymentAmount, sqlCommand);
                                            }
                                            else if (_paymentMethod == "ac")
                                            {
                                                PaymentServiceController.updateAgentCreditRedeem(item.SuperPNRID, paymentRecord.OrderID, paymentRecord.CreatedByID, paymentRecord.CurrencyCode,
                                                    paymentRecord.PaymentMethodCode, paymentRecord.PaymentAmount, item.MainProductType.ToDescription(), sqlCommand);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            paymentRecord.RequeryStatusCode = "ERR";
                                            string objSerialize = null;

                                            try
                                            {
                                                objSerialize = JsonConvert.SerializeObject(paymentRecord, Formatting.Indented,
                                                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                                            }
                                            catch (Exception innerEX)
                                            {
                                                logger.Error(innerEX, "Error while serialize database object.");
                                            }

                                            logger.Error(ex, "Fail to redeem credit while requery payment." + Environment.NewLine + Environment.NewLine
                                                + objSerialize);
                                            _status.Add(false);
                                        }

                                        if (sqlCommand?.Transaction != null)
                                        {
                                            paymentRecord.PaymentStatusCode = "PAID";
                                            sqlCommand.Transaction.Commit();
                                            logger.Info(string.Format("SuperPNR {0} - {1} success claim credit.", item.SuperPNRID, item.SuperPNRNo));
                                            _status.Add(true);
                                        }
                                        else
                                        {
                                            paymentRecord.RequeryStatusCode = "ERR";
                                            logger.Error("Cannot claim user used travel credit.", JsonConvert.SerializeObject(paymentRecord, Formatting.Indented,
                                                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
                                            _status.Add(false);
                                        }
                                    }
                                    else if (paymentRecord.PaymentStatusCode == "PAID")
                                    {
                                        logger.Info(string.Format("SuperPNR {0} - {1} claimed credit before.", item.SuperPNRID, item.SuperPNRNo));
                                        _status.Add(true);
                                    }
                                }
                                else if (_paymentMethod == "tw")
                                {
                                    if (paymentRecord.PaymentStatusCode != "PAID")
                                    {
                                        SqlCommand sqlCommand = new SqlCommand();

                                        try
                                        {
                                            var productType = paymentRecord.SuperPNROrder.SuperPNR.MainProductType.ToDescription();

                                            // Redeem travel wallet used
                                            PaymentServiceController.UpdateCashCreditRedeem(paymentRecord.CreatedByID, paymentRecord.CurrencyCode, 
                                                paymentRecord.OrderID, paymentRecord.PaymentAmount, productType, sqlCommand);

                                            // Remove holded travel wallet
                                            new PaymentServiceController().TempCashCreditRedeemDelete(paymentRecord.OrderID, paymentRecord.CreatedByID, 
                                                paymentRecord.CurrencyCode, paymentRecord.PaymentAmount, sqlCommand);
                                        }
                                        catch (Exception ex)
                                        {
                                            paymentRecord.RequeryStatusCode = "ERR";
                                            string objSerialize = null;

                                            try
                                            {
                                                objSerialize = JsonConvert.SerializeObject(paymentRecord, Formatting.Indented,
                                                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                                            }
                                            catch (Exception innerEX)
                                            {
                                                logger.Error(innerEX, "Error while serialize database object.");
                                            }

                                            logger.Error(ex, "Fail to redeem travel wallet while requery payment." + Environment.NewLine + Environment.NewLine
                                                + objSerialize);
                                            _status.Add(false);
                                        }

                                        if (sqlCommand?.Transaction != null)
                                        {
                                            paymentRecord.PaymentStatusCode = "PAID";
                                            sqlCommand.Transaction.Commit();
                                            logger.Info(string.Format("SuperPNR {0} - {1} success claim travel wallet.", item.SuperPNRID, item.SuperPNRNo));
                                            _status.Add(true);
                                        }
                                        else
                                        {
                                            paymentRecord.RequeryStatusCode = "ERR";
                                            logger.Error("Cannot claim user used travel wallet.", JsonConvert.SerializeObject(paymentRecord, Formatting.Indented,
                                                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
                                            _status.Add(false);
                                        }
                                    }
                                    else if (paymentRecord.PaymentStatusCode == "PAID")
                                    {
                                        logger.Info(string.Format("SuperPNR {0} - {1} claimed travel wallet before.", item.SuperPNRID, item.SuperPNRNo));
                                        _status.Add(true);
                                    }
                                }
                            }

                            try
                            {
                                if (paymentRecordCount == _status.Count && _status.All(x => x == true))
                                {
                                    string oriStatus = order.BookingStatusCode;
                                    order.BookingStatusCode = "CON";
                                    order.PaymentOrders = UpdatePaymentRequeryStatus(order.PaymentOrders, "SUC");
                                    successProcessed = successProcessed ?? new List<string>();

                                    successProcessed.Add(string.Format("SuperPNR {0} - {1}: Processed. [{2} --> {3}]",
                                        order.SuperPNRID, order.SuperPNR.SuperPNRNo, oriStatus, order.BookingStatusCode));
                                }

                                dbContext.SaveChanges();
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, "Error while attempting to save changes on payment scheduler.");
                            }
                        }
                    }
                }

                if (successProcessed != null && successProcessed.Count > 0)
                {
                    string msg = "Payment Scheduler Information" + Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine, successProcessed);
                    logger.Info(msg);
                    eventLog1.WriteEntry(msg, EventLogEntryType.SuccessAudit, 200);
                }

                if (bookedList.InformationCaution != null && bookedList.InformationCaution.Count > 0)
                {
                    string msg = "Payment Scheduler Information" + Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine, bookedList.InformationCaution);
                    logger.Warn(msg);
                    eventLog1.WriteEntry(msg, EventLogEntryType.Warning);
                }
            }
            catch (Exception ex)
            {
                string msg = "Error Occured when running async action. " + Environment.NewLine + Environment.NewLine + ex.ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Warning);
                logger.Warn(msg);
            }
            finally
            {
                try
                {
                    dbContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    var changedInfo = dbContext.ChangeTracker.Entries()
                        .Where(t => t.State == System.Data.Entity.EntityState.Modified)
                        .Select(t => new
                        {
                            Original = t.OriginalValues.PropertyNames.ToDictionary(pn => pn, pn => t.OriginalValues[pn]),
                            Current = t.CurrentValues.PropertyNames.ToDictionary(pn => pn, pn => t.CurrentValues[pn]),
                        });

                    logger.Error(ex, "Error while attemp to save db change on finally action." +
                        Environment.NewLine + Environment.NewLine +
                        JsonConvert.SerializeObject(changedInfo, Formatting.Indented,
                        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
                }

                semaphoreSlim.Release();
            }
        }

        private void OnTimerRHI(object sender, System.Timers.ElapsedEventArgs args)
        {
            MayFlower dbContext = null;

            try
            {
                dbContext = dbContext ?? new MayFlower();
                var ordersRHI = Components.PaymentCheck.DBQuery.GetOrdersRHI(dbContext, false, true);
                List<string> mailMsg = new List<string>();

                foreach (var order in ordersRHI.Select(x => x.SuperPNROrder).Distinct())
                {
                    order.SuperPNR.LoadPaymentDetailList(true);
                    var groupedPaymentCurrencyDtl = order.SuperPNR.PaymentDetailsList.FirstOrDefault();

                    mailMsg.Add($"<li>" +
                        $"<b>{order.SuperPNR.SuperPNRNo}</b> ({order.BookingStatusCode})<br/>" +
                        $"<i>Main Product: {order.SuperPNR.MainProductType.ToString()},<br/>" +
                        $"Total Order Amount: {order.CurrencyCode} {order.GrandTotalAmt.ToString("n2")}<br/>" +
                        $"Success Payment Amount: {groupedPaymentCurrencyDtl.CurrencyCode} {groupedPaymentCurrencyDtl.TotalPaidAmount.ToString("n2")}</i>" +
                        $"</li>");
                }

                if (mailMsg.Count > 0)
                {
                    string _msg = string.Join("", mailMsg);
                    string fromEmail = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("fromEmailAddress");
                    string sendToEmail = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("RHIReminderEmailList");
                    string _folderPath = System.AppDomain.CurrentDomain.BaseDirectory.ToLower().Replace(@"\bin\debug\", "").Replace(@"\bin\release\", "");
                    string _fullPath = _folderPath + @"\EmailTemplates\RHIEmailReminder.html";

                    bool fileExists = System.IO.File.Exists(_folderPath + @"\EmailTemplates\RHIEmailReminder.html");

                    if (fileExists)
                    {
                        string htmlTemplete = System.IO.File.ReadAllText(_fullPath);
                        htmlTemplete = htmlTemplete.Replace("{ItemListToReplace}", _msg);

                        Alphareds.Module.CommonController.CommonServiceController.InsertEmailQueue(null, fromEmail, sendToEmail, null, null,
                            "Mayflower RHI Booking List", htmlTemplete, null, true);
                    }
                    else
                    {
                        string msg = "Template files not found for the RHI reminder email. " + Environment.NewLine + Environment.NewLine + _msg;
                        eventLog1.WriteEntry(msg, EventLogEntryType.Warning);
                        logger.Warn(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = "Error Occured when preparing the RHI reminder email. " + Environment.NewLine + Environment.NewLine + ex.ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Warning);
                logger.Warn(msg);
            }

            try
            {
                dbContext.Dispose();
            }
            catch
            {
                
            }
        }

        #region Internal Functions
        internal void ConsoleStartupAndStop(string[] args)
        {
            Console.Write("Please key-in SuperPNRNo to requery:");
            string superPNRNo = Console.ReadLine();

            //this.OnStart(args);
            this.OnTimer(superPNRNo, null);
            Console.Write("Press Any Key to Stop...");
            Console.ReadKey();
            Console.ReadLine();
            this.OnStop();
        }

        internal void ImmediateStartup(string[] args)
        {
            this.OnTimer("", null);
            this.OnTimerRHI("", null);
        }
        #endregion

        #region Helper Method
        private string GetAppSettingValueEnhanced(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                { throw new Exception("Key empty cannot be accepted."); }

                var appSetting = System.Configuration.ConfigurationManager.AppSettings[key];
                if (appSetting == null)
                { throw new Exception("App Setting not found."); }

                return appSetting.ToString();
            }
            catch (Exception)
            {
            }
            return string.Empty;
        }

        private ICollection<PaymentOrder> UpdatePaymentRequeryStatus(ICollection<PaymentOrder> paymentOrders, string requeryStatusCode)
        {
            foreach (var item in paymentOrders)
            {
                item.RequeryStatusCode = requeryStatusCode;
            }

            return paymentOrders;
        }
        #endregion
    }
}
