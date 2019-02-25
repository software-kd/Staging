using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PaymentQueueHandler;
using Alphareds.Module.Model.Database;
using Newtonsoft.Json;
using EventBookingQueueHandler.Components;
using Alphareds.Module.Model;
using Alphareds.Module.Common;
using Alphareds.Module.HotelController;

namespace EventBookingQueueHandler
{
    public partial class EventBookingQueueService : ServiceBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public EventBookingQueueService()
        {
            InitializeComponent();

            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("Mayflower AddOn Scheduler Service"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "Mayflower AddOn Scheduler Service", "MH AddOn Log");
            }

            eventLog1.Source = "Mayflower AddOn Scheduler Service";
            eventLog1.Log = "MH AddOn Log";

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

            try
            {
                PaymentQueueHandler.Components.BookingList bookPaidList = new PaymentQueueHandler.Components.BookingList(dbContext, null);
                IEnumerable<SuperPNR> bookingProcessed = null;
                List<string> successProcessed = new List<string>();
                Dictionary<string, ProductReserve.BookResultType> reserveStatus = new Dictionary<string, ProductReserve.BookResultType>();

                if (sender.GetType().Name == "String" && !string.IsNullOrWhiteSpace(sender.ToString()))
                {
                    string[] splitAttr = sender.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    bool includeEXP = splitAttr.Length == 2;

                    bookingProcessed = bookPaidList.GetBookingPaid(splitAttr[0], true, true, false);
                }
                else
                {
                    bookPaidList.CheckUpdatePendingPayment(true, true, false).ConfigureAwait(false).GetAwaiter().GetResult();
                    bookingProcessed = bookPaidList.GetAllBookingPaid(true, true, false);
                }

                foreach (var item in bookingProcessed)
                {
                    // Process Add On Here
                    foreach (var record in item.BookingInsurances)
                    {
                        try
                        {
                            if (record.BookingStatusCode == "PPA")
                            {
                                var insService = new InsuranceService(logger);
                                var bookRespond = insService.ConfirmInsuranceQuotation(record, record.SuperPNRNo);

                                bool res = bookRespond?.BatchBookResult == ProductReserve.BookResultType.AllSuccess || bookRespond?.BatchBookResult == ProductReserve.BookResultType.PartialSuccess;
                                reserveStatus.Add(string.Format("Insurance ({0} - {1})", record.SuperPNRID, record.SuperPNRNo), bookRespond?.BatchBookResult ?? ProductReserve.BookResultType.AllFail);

                                if (!res)
                                    break; //any failed exit loop
                                else
                                {
                                    successProcessed.Add(string.Format("Insurance ({0} - {1}): Update from [{2}] --> [{3}]"
                                    , item.SuperPNRID, item.SuperPNRNo, record.BookingStatusCode, "CON"));
                                    record.BookingStatusCode = res ? "CON" : "EXP";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex.GetBaseException(), "Reserve Fail in Insurance Booking - " + DateTime.Now.ToLoggerDateTime());
                            reserveStatus.Add(string.Format("Insurance ({0} - {1})", record.SuperPNRID, record.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                            break; // exit looping prevent continue book
                        }
                        finally
                        {
                            dbContext.SaveChanges();
                        }
                    }

                    bool allSuccess = reserveStatus.All(x => x.Value == ProductReserve.BookResultType.AllSuccess);
                    // Check is main last book item success
                    if (allSuccess && item.EventBookings.Count > 0 && item.EventBookings.Any(x => x.BookingStatusCode == "PPA" || x.BookingStatusCode == "RHI"))
                    {
                        // check if status CON then doesn't execute
                        Components.EventBookings eventBookings = new Components.EventBookings(item, "CON", allSuccess);
                        string bookStatus = string.Join(",", item.EventBookings.Select(x => x.BookingStatusCode));

                        if (eventBookings.InformationCaution == null)
                        {
                            reserveStatus.Add(string.Format("Event ({0} - {1})", item.SuperPNRID, item.SuperPNRNo)
                                , ProductReserve.BookResultType.AllSuccess);

                            successProcessed.Add(string.Format("Event ({0} - {1}): Update from [{2}] --> [{3}]"
                                , item.SuperPNRID, item.SuperPNRNo, bookStatus, "CON"));
                        }
                        else if (eventBookings.InformationCaution != null && eventBookings.InformationCaution.Count > 0)
                        {
                            reserveStatus.Add(string.Format("Event ({0} - {1})", item.SuperPNRID, item.SuperPNRNo) +
                                Environment.NewLine + Environment.NewLine +
                                JsonConvert.SerializeObject(eventBookings.InformationCaution, Formatting.Indented), ProductReserve.BookResultType.AllFail);
                        }
                    }
                    else if (!allSuccess)
                    {
                        string _msg = "Deduct event booking inventory failed." + Environment.NewLine + Environment.NewLine +
                            JsonConvert.SerializeObject(reserveStatus, Formatting.Indented);
                        // throw error and end looping
                        logger.Error(_msg);
                        eventLog1.WriteEntry(_msg, EventLogEntryType.FailureAudit, 401);
                        break;
                    }
                }

                if (successProcessed != null && successProcessed.Count > 0)
                {
                    string msg = "AddOn Scheduler Information" + Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine, successProcessed);
                    logger.Info(msg);
                    eventLog1.WriteEntry(msg, EventLogEntryType.SuccessAudit, 200);
                }

                if (bookPaidList.InformationCaution != null && bookPaidList.InformationCaution.Count > 0)
                {
                    string msg = "AddOn Scheduler Information" + Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine, bookPaidList.InformationCaution);
                    logger.Warn(msg);
                    eventLog1.WriteEntry(msg, EventLogEntryType.Warning);
                }
            }
            catch (Exception ex)
            {
                string msg = "Error Occured when running async action. " + Environment.NewLine + Environment.NewLine + ex.ToString();
                eventLog1.WriteEntry(msg, EventLogEntryType.Warning);
                logger.Warn(ex, msg);
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
        #endregion
    }
}
