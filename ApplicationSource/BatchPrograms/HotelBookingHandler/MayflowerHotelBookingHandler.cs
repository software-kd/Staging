using Alphareds.Module.Common;
using Alphareds.Module.Model;
using Alphareds.Module.ExpediaHotelsWebService;
using Alphareds.Module.ESBHotelComparisonWebService;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.PDFEngineWebService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Alphareds.Module.Model.Database;
using NLog;
using System.Threading;
using PaymentQueueHandler;
using Newtonsoft.Json;

namespace HotelBookingHandler
{
    public partial class MayflowerHotelBookingHandler : ServiceBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public MayflowerHotelBookingHandler()
        {
            InitializeComponent();

            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "MayflowerSource", "MayflowerHotelLog");
            }
            eventLog1.Source = "MayflowerSource";
            eventLog1.Log = "MayflowerHotelLog";
        }

        internal void ConsoleStartupAndStop(string[] args)
        {
            Console.Write("Please key-in SuperPNRNo to requery:");
            string superPNRNo = Console.ReadLine();

            //this.OnStart(args);
            this.OnTimer(superPNRNo, null);
            Console.Write("Press Any Key to Stop...");
            Console.ReadKey();
            //Console.ReadLine();
            this.OnStop();
        }

        internal void ImmediateStartup(string[] args)
        {
            this.OnTimer("", null);
        }

        protected override void OnStart(string[] args)
        {
            //eventLog1.WriteEntry("In OnStart");
            double requeryAfterSecond = Convert.ToDouble(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("RequeryAfterSecond"));
            TimeSpan timeSpanRequery = TimeSpan.FromSeconds(requeryAfterSecond);
            // Set up a timer to trigger every minute.  

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = timeSpanRequery.TotalMilliseconds;
            timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer);
            timer.Start();
        }

        protected override void OnContinue()
        {
            //eventLog1.WriteEntry("In OnContinue.");
        }

        protected override void OnStop()
        {
            //eventLog1.WriteEntry("In onStop.");
        }

        public async void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.  
            await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            //eventLog1.WriteEntry("Starting requery Hotel Booking - " + DateTime.Now.ToLoggerDateTime(), EventLogEntryType.Information, 200);
            try
            {
                //await StartRequery(sender, args);
                await StartRequery_New(sender, args);
            }
            catch (Exception ex)
            {
                string msg = "Error Occured when running async action. " + Environment.NewLine + Environment.NewLine + ex.ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Warning);
                logger.Warn(msg);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private async Task<bool> StartRequery(object sender, System.Timers.ElapsedEventArgs args)
        {
            // testing code
            //PaymentOrder po = new PaymentOrder() { PaymentAmount = 35.05m, CurrencyCode = "MYR", Ipay88RefNo = "304 - ABETPQI" };
            //var resT = PaymentQuery.Query.CheckPaymentPAID(po, 304, 786, "ABETPQI");

            MayFlower db = new MayFlower();
            List<Task<ProductReserve.BookingRespond>> bookResultTask = new List<Task<ProductReserve.BookingRespond>>();
            List<string> dbChangeLog = new List<string>();
            List<string> refNoQueried = new List<string>();
            var bookList = BookingQuery.DBQuery.GetPendingBooking(db);

            if (sender.GetType().Name == "String" && !string.IsNullOrWhiteSpace(sender.ToString()))
            {
                string[] splitAttr = sender.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                bool includeEXP = splitAttr.Length == 2;

                bookList = BookingQuery.DBQuery.GetPendingBooking(db, splitAttr[0], includeEXP);
            }

            try
            {
                foreach (var booking in bookList)
                {
                    var paymentDetail = booking.SuperPNR.SuperPNROrders;
                    string originalStatus = booking.BookingStatusCode;
                    bool paymentSuccess = false;
                    bool requeryPaymentFail = false;
                    foreach (var orderRecord in paymentDetail)
                    {
                        List<Task<bool>> taskList = new List<Task<bool>>();

                        foreach (var payment in orderRecord.PaymentOrders)
                        {
                            if (!string.IsNullOrWhiteSpace(payment.PaymentMethodCode) && payment.PaymentMethodCode.ToLower().StartsWith("ipa") &&
                                refNoQueried.Count(x => x == payment.Ipay88RefNo) == 0)
                            {
                                taskList.Add(PaymentQuery.IPAY88.CheckPaymentPAIDAsync(payment, booking.BookingID, booking.SuperPNRID, booking.SuperPNRNo));
                                refNoQueried.Add(payment.Ipay88RefNo);
                            }
                        }

                        try
                        {
                            var requeryResult = await Task.WhenAll(taskList).ConfigureAwait(false);
                            paymentSuccess = requeryResult.Count(x => x == true) >= 1;

                            if (paymentSuccess)
                                break; // exit looping
                        }
                        catch (AggregateException ae)
                        {
                            requeryPaymentFail = true;
                            string msg = "Unexpected error occured. - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine
                                + ae.ToString() + Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString();
                            eventLog1.WriteEntry(msg, EventLogEntryType.Error, 250);
                            logger.Warn(msg);
                        }
                        catch (Exception ex)
                        {
                            requeryPaymentFail = true;
                            string msg = "Unexpected error occured. - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine
                                + ex.ToString() + Environment.NewLine + Environment.NewLine + ex.GetBaseException().ToString();
                            eventLog1.WriteEntry(msg, EventLogEntryType.Error, 250);
                            logger.Warn(msg);
                        }
                    }

                    if (paymentSuccess)
                    {
                        // requery payment true

                        if (booking.SupplierCode == "EAN")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.Expedia expedia = new BookingQuery.Expedia();
                            bookResultTask.Add(expedia.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "TP")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.Tourplan tourplan = new BookingQuery.Tourplan();
                            bookResultTask.Add(tourplan.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "JAC")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.JacTravel jactravel = new BookingQuery.JacTravel();
                            bookResultTask.Add(jactravel.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "HB")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.HotelBeds hotelbeds = new BookingQuery.HotelBeds();
                            bookResultTask.Add(hotelbeds.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "RAP")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.EANRapid _EANRapid = new BookingQuery.EANRapid();
                            bookResultTask.Add(_EANRapid.CheckoutReserveRoomCheckExist(booking.SuperPNRID, booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else
                        {
                            // Temp usage
                            string msg = string.Format("Hotel Booking supplier stored doesn't found in code - {0} [{1}]", booking.SuperPNRNo, booking.BookingStatusCode);
                            Logger logger = LogManager.GetCurrentClassLogger();
                            logger.Error(msg);
                            eventLog1.WriteEntry(msg, EventLogEntryType.FailureAudit, 404);
                        }
                    }
                    else if (!paymentSuccess && !requeryPaymentFail)
                    {
                        // requery payment false
                        try
                        {
                            foreach (var item in booking.SuperPNR.SuperPNROrders)
                            {
                                item.BookingStatusCode = "EXP";
                                foreach (var order in item.PaymentOrders)
                                {
                                    order.PaymentStatusCode = "FAIL";
                                }
                            }

                            booking.BookingStatusCode = "EXP";
                            dbChangeLog.Add(string.Format("Updating {0} - {1} [{2}] --> [{3}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.BookingStatusCode));
                        }
                        catch (AggregateException ae)
                        {
                            eventLog1.WriteEntry(ae.GetBaseException().ToString(), EventLogEntryType.Warning, 999);
                            logger.Warn(ae, "Hotel Booking Requery Handler" + ae.ToString());
                        }
                        catch (Exception ex)
                        {
                            eventLog1.WriteEntry(ex.GetBaseException().ToString(), EventLogEntryType.Warning, 999);
                            logger.Warn(ex, "Hotel Booking Requery Handler" + ex.ToString());
                        }
                    }
                }

                var bookResultList = await Task.WhenAll(bookResultTask);
            }
            catch (AggregateException ae)
            {
                string msg = "Unexpected error occured. - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine
                    + ae.ToString() + Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Error, 250);
                logger.Warn(msg);
            }
            catch (Exception ex)
            {
                string msg = "Unexpected error occured. - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine
                    + ex.ToString() + Environment.NewLine + Environment.NewLine + ex.GetBaseException().ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Error, 250);
                logger.Warn(msg);
            }
            finally
            {
                /* Save change only at end, prevent open too much connection.
                 * With this method also can shared used between frontend.
                 * Which some content update by SP doesn't reflect to latest context.*/
                await db.SaveChangesAsync();
            }

            // Catch successed record incase any code error caused don't insert log.
            var completedResult = bookResultTask.Where(x => x.Status == TaskStatus.RanToCompletion).Select(x => x.Result);

            if (completedResult != null && completedResult.Count() > 0)
            {
                int successCount = completedResult.Count(x => x.BatchBookResult == ProductReserve.BookResultType.AllSuccess);
                int failCount = completedResult.Count(x => x.BatchBookResult == ProductReserve.BookResultType.AllFail);
                int partialFailCount = completedResult.Count(x => x.BatchBookResult == ProductReserve.BookResultType.PartialSuccess);
                List<string> PDFItem = new List<string>();

                try
                {
                    /* 2017/10/26 - Turn off send PDF first, as Hotel Requery doesn't handle full process yet.
                        * 1) Check any other Product
                        * 2) Capture Payment/Void Payment
                        * 3) Update SuperPNROrders Book Status.
                    */
                    //foreach (var item in completedResult.Where(x => x.BatchBookResult == ProductReserve.BookResultType.AllSuccess))
                    //{
                    //    var _book = bookList.FirstOrDefault(x => x.SuperPNRNo == item.SuperPNRNo);
                    //    Alphareds.Module.HotelController.HotelServiceController.sendHotelItineraryEmailAfterSuccess(_book);
                    //}
                }
                catch (Exception ex)
                {
                    eventLog1.WriteEntry("Send PDF Fail on SuperPNRNo: " + string.Join(Environment.NewLine, PDFItem)
                        + Environment.NewLine + Environment.NewLine + ex.ToString(), EventLogEntryType.Information);
                }

                eventLog1.WriteEntry(string.Format("Stopping requery Hotel Booking - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine + Environment.NewLine +
                                                "Total Booking : {0}" + Environment.NewLine +
                                                "All Book Success: {1}" + Environment.NewLine +
                                                "All Book Fail: {2}" + Environment.NewLine +
                                                "Partial Success: {3}" + Environment.NewLine +
                                                Environment.NewLine + Environment.NewLine +
                                                "Completed record with status: {4}" + Environment.NewLine
                                                , bookList.Count(), successCount, failCount, partialFailCount
                                                , Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, completedResult.Select(x => x.SuperPNRNo + " - " + x.BatchBookResult.ToDescription())))
                                                , EventLogEntryType.SuccessAudit, 300);
            }

            if (dbChangeLog.Count > 0)
            {
                eventLog1.WriteEntry("Total Payment Record Processed: " + dbChangeLog.Count +
                    Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, dbChangeLog), EventLogEntryType.Information, 210);
            }

            var grpReport = bookList.GroupBy(x => x.SuperPNRNo).AsEnumerable().Select(x => new
            {
                Counter = x.Count(),
                RefNo = string.Join(", ", x.Select(y => y.BookingID + " - " + y.SuperPNRNo + " [" + y.OrderID + "]")),
            }).Where(x => x.Counter > 1);

            if (grpReport.Count() > 1)
            {
                string msg = "Potential Problem Record Found (Duplicate SuperPNRNo with different BookingID): " +
                    Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, grpReport.Select(x => x.RefNo));
                eventLog1.WriteEntry(msg, EventLogEntryType.Warning, 400);
                logger.Warn(msg);
            }

            return true;
        }

        private async Task<bool> StartRequery_New(object sender, System.Timers.ElapsedEventArgs args)
        {
            // testing code
            //PaymentOrder po = new PaymentOrder() { PaymentAmount = 35.05m, CurrencyCode = "MYR", Ipay88RefNo = "304 - ABETPQI" };
            //var resT = PaymentQuery.Query.CheckPaymentPAID(po, 304, 786, "ABETPQI");

            MayFlower db = new MayFlower();
            
            PaymentQueueHandler.Components.BookingList bookedList = new PaymentQueueHandler.Components.BookingList(db, null);
            IEnumerable<SuperPNR> bookingProcessed = null;
            List<string> successProcessed = new List<string>();
            List<Task<ProductReserve.BookingRespond>> bookResultTask = new List<Task<ProductReserve.BookingRespond>>();

            if (sender.GetType().Name == "String" && !string.IsNullOrWhiteSpace(sender.ToString()))
            {
                string[] splitAttr = sender.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                bool includeEXP = splitAttr.Length == 2;

                await bookedList.CheckUpdatePendingPayment(true, false, false, splitAttr[0]);
                bookingProcessed = bookedList.GetBookingPaid(splitAttr[0], true, false, false);
            }
            else
            {
                await bookedList.CheckUpdatePendingPayment(true, false, false);
                bookingProcessed = bookedList.GetAllBookingPaid(true, false, false);
            }

            List<string> dbChangeLog = new List<string>();

            try
            {
                foreach (var booking in bookingProcessed.SelectMany(x => x.BookingHotels))
                {
                    string originalStatus = booking.BookingStatusCode;
                    // requery payment true

                    if (originalStatus != "CON")
                    {
                        if (booking.SupplierCode == "EAN")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.Expedia expedia = new BookingQuery.Expedia();
                            bookResultTask.Add(expedia.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "TP")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.Tourplan tourplan = new BookingQuery.Tourplan();
                            bookResultTask.Add(tourplan.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "JAC")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.JacTravel jactravel = new BookingQuery.JacTravel();
                            bookResultTask.Add(jactravel.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "HB")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.HotelBeds hotelbeds = new BookingQuery.HotelBeds();
                            bookResultTask.Add(hotelbeds.CheckoutReserveRoom(booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else if (booking.SupplierCode == "RAP")
                        {
                            dbChangeLog.Add(string.Format("Reserving {3} Booking: {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, originalStatus, booking.SupplierCode));

                            BookingQuery.EANRapid _EANRapid = new BookingQuery.EANRapid();
                            bookResultTask.Add(_EANRapid.CheckoutReserveRoomCheckExist(booking.SuperPNRID, booking.BookingID, booking.BookingStatusCode, Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL"), db));
                        }
                        else
                        {
                            // Temp usage
                            string msg = string.Format("Hotel Booking supplier stored doesn't found in code - {0} [{1}]", booking.SuperPNRNo, booking.BookingStatusCode);
                            Logger logger = LogManager.GetCurrentClassLogger();
                            logger.Error(msg);
                            eventLog1.WriteEntry(msg, EventLogEntryType.FailureAudit, 404);
                        }
                    }
                }

                var bookResultList = await Task.WhenAll(bookResultTask);
            }
            catch (AggregateException ae)
            {
                string msg = "Unexpected error occured. - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine
                    + ae.ToString() + Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Error, 250);
                logger.Warn(msg);
            }
            catch (Exception ex)
            {
                string msg = "Unexpected error occured. - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine
                    + ex.ToString() + Environment.NewLine + Environment.NewLine + ex.GetBaseException().ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Error, 250);
                logger.Warn(msg);
            }
            finally
            {
                /* Save change only at end, prevent open too much connection.
                 * With this method also can shared used between frontend.
                 * Which some content update by SP doesn't reflect to latest context.*/
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    var changedInfo = db.ChangeTracker.Entries()
                        .Where(t => t.State == System.Data.Entity.EntityState.Modified)
                        .Select(t => new
                        {
                            Original = t.OriginalValues.PropertyNames.ToDictionary(pn => pn, pn => t.OriginalValues[pn]),
                            Current = t.CurrentValues.PropertyNames.ToDictionary(pn => pn, pn => t.CurrentValues[pn]),
                        });

                    //TODO: Add window service log
                    string msg = Environment.NewLine + Environment.NewLine +
                                    JsonConvert.SerializeObject(changedInfo, Formatting.Indented,
                                    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                    eventLog1.WriteEntry(ex.GetBaseException().Message.ToString() + msg, EventLogEntryType.Error, 250);

                    logger.Error(ex, "Error while attemp to save db change on finally action." + msg);
                }
            }

            // Catch successed record incase any code error caused don't insert log.
            var completedResult = bookResultTask.Where(x => x.Status == TaskStatus.RanToCompletion).Select(x => x.Result);

            if (completedResult != null && completedResult.Count() > 0)
            {
                int successCount = completedResult.Count(x => x.BatchBookResult == ProductReserve.BookResultType.AllSuccess);
                int failCount = completedResult.Count(x => x.BatchBookResult == ProductReserve.BookResultType.AllFail);
                int partialFailCount = completedResult.Count(x => x.BatchBookResult == ProductReserve.BookResultType.PartialSuccess);

                eventLog1.WriteEntry(string.Format("Stopping requery Hotel Booking - " + DateTime.Now.ToLoggerDateTime() + Environment.NewLine + Environment.NewLine +
                                                "Total Booking : {0}" + Environment.NewLine +
                                                "All Book Success: {1}" + Environment.NewLine +
                                                "All Book Fail: {2}" + Environment.NewLine +
                                                "Partial Success: {3}" + Environment.NewLine +
                                                Environment.NewLine + Environment.NewLine +
                                                "Completed record with status: {4}" + Environment.NewLine
                                                , bookingProcessed.Count(), successCount, failCount, partialFailCount
                                                , Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, completedResult.Select(x => x.SuperPNRNo + " - " + x.BatchBookResult.ToDescription())))
                                                , EventLogEntryType.SuccessAudit, 300);
            }

            if (dbChangeLog.Count > 0)
            {
                eventLog1.WriteEntry("Total Payment Record Processed: " + dbChangeLog.Count +
                    Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, dbChangeLog), EventLogEntryType.Information, 210);
            }

            return true;
        }
    }
}
