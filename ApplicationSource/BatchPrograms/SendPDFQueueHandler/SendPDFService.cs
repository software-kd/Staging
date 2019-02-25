using Alphareds.Module.Model.Database;
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
using SendPDFQueueHandler.Functions;
using System.Net.Http;
using System.Dynamic;
using SendPDFQueueHandler.Model;

namespace SendPDFQueueHandler
{
    partial class SendPDFService : ServiceBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public SendPDFService()
        {
            InitializeComponent();

            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("Mayflower Send PDF Scheduler Service"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "Mayflower Send PDF Scheduler Service", "MH PDF_All Log");
            }

            eventLog1.Source = "Mayflower Send PDF Scheduler Service";
            eventLog1.Log = "MH PDF_All Log";
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.
            double requeryAfterSecond = Convert.ToDouble(Helper.GetAppSettingValueEnhanced("RequeryAfterSecond"));
            TimeSpan timeSpanRequery = TimeSpan.FromSeconds(requeryAfterSecond);
            // Set up a timer to trigger every minute.  

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = timeSpanRequery.TotalMilliseconds;
            timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer);
            timer.Start();
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
        }

        private void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            semaphoreSlim.Wait();
            MayFlower dbContext = new MayFlower();
            List<string> logMsg = new List<string>();
            List<bool> statusResp = new List<bool>();
            ItineraryLog ItineraryLog = new ItineraryLog(eventLog1);

            try
            {
                var bookedNotSendPDF = dbContext.SuperPNROrders.Where(x => x.BookingStatusCode == "CON"
                && x.CreatedDate >= new DateTime(2017, 11, 1)
                && (x.PaymentOrders.Count > 0 && x.PaymentOrders.All(a => a.PaymentStatusCode == "CAPT" || a.PaymentStatusCode == "PAID"))
                && (x.SuperPNR.Bookings.Count == 0 || (x.SuperPNR.Bookings.Count > 0 && x.SuperPNR.Bookings.Any(a => !a.IsBookingItinenarySent)))
                && (x.SuperPNR.BookingInsurances.Count == 0 || (x.SuperPNR.BookingInsurances.Count > 0 && x.SuperPNR.BookingInsurances.All(a => a.BookingStatusCode == "CON")))
                && (x.SuperPNR.BookingHotels.Count == 0 || (x.SuperPNR.BookingHotels.Count > 0 && x.SuperPNR.BookingHotels.Any(a => !a.IsBookingItinenarySent)))
                //&& (x.SuperPNR.TourPackageBookings.Count == 0)
                && (x.SuperPNR.TourPackageBookings.Count == 0 || (x.SuperPNR.TourPackageBookings.Count > 0 && x.SuperPNR.TourPackageBookings.Any(a => a.BookingStatusCode == "CON")))
                && (x.SuperPNR.CarRentalBookings.Count == 0 || (x.SuperPNR.CarRentalBookings.Count > 0 && x.SuperPNR.CarRentalBookings.Any(a => a.BookingStatusCode == "CON" && !a.IsBookingItinenarySent))) 
                && (x.SuperPNR.EventBookings.Count == 0 ||
                    (x.SuperPNR.EventBookings.Count > 0 && x.SuperPNR.EventBookings.All(a => a.BookingStatusCode == "CON") &&
                    ((x.SuperPNR.BookingHotels.Count == 0 && x.SuperPNR.Bookings.Count == 0 && x.EventBookings.Any(a => 
                        x.CreatedDate >= new DateTime(2018, 09, 02) && (!a.IsBookingItinenarySent ?? true)))
                    || (x.SuperPNR.BookingHotels.Count > 0 || x.SuperPNR.Bookings.Count > 0)))
                        ));

                foreach (var item in bookedNotSendPDF)
                {
                    #region Email PDF Section
                    bool sendStatus = false;
                    try
                    {
                        var pushObj = new ExpandoObject() as IDictionary<string, Object>;

                        pushObj.Add("eid", Alphareds.Module.Cryptography.Cryptography.AES.Encrypt(item.SuperPNRID.ToString()));
                        pushObj.Add("privateKey", Helper.GetAppSettingValueEnhanced("InternalAppKey"));
                        CallAPI callAPI = new CallAPI();

                        //api/internal_func/pdf/send
                        var resp = callAPI.SendPDF(item.SuperPNR, true);
                        sendStatus = resp?.SendStatus ?? false;

                        logMsg.Add(resp?.Message ?? $"[{item.SuperPNR.SuperPNRNo}] - Error service respond null.");
                    }
                    catch (AggregateException ae)
                    {
                        logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNR.SuperPNRNo} pdf send status : {sendStatus} "
                            + Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                            + ae.ToString() + Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNR.SuperPNRNo} pdf send status : {sendStatus} "
                            + Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                            + ex.ToString() + Environment.NewLine);
                    }
                    #endregion
                }
            }
            catch (Exception ex)
            {
                logMsg.Add("Error on requery send pdf itinerary."
                            + Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                    + ex.ToString() + Environment.NewLine);
            }

            if (logMsg.Count > 0)
            {
                ItineraryLog.WriteEventLog(string.Join(Environment.NewLine, logMsg), EventLogEntryType.Information, 200);
            }

            semaphoreSlim.Release();
        }

        #region HTTP Client Method
        public async Task<T1> GetPOSTRespond<T1, T2>(string url, T2 postModel)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(Helper.GetAppSettingValueEnhanced("InternalAppWebAPIUrl"));
                    //HTTP POST
                    var postTask = await client.PostAsJsonAsync<T2>(url, postModel);

                    if (postTask.IsSuccessStatusCode)
                    {
                        var resStr = await postTask.Content.ReadAsAsync<T1>();

                        return resStr;
                    }
                }
                return default(T1);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        #endregion

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
    }
}
