using Alphareds.Module.Model.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace EventBundleBookingStatusHandler
{
    class Program
    {
        #region Member variables Declarations

        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog emaillog = log4net.LogManager.GetLogger("EmailLogger");
        //private static ExecutionType executionType = ExecutionType.None;
        private static MayFlower db = new MayFlower();

        #endregion

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
                throw ex;
            }
        }

        static void Main(string[] args)
        {
            log.Debug("EventBundle Booking Status Handler Start.");
            try
            {
                DateTime checkTime = DateTime.Now.AddMinutes(-15);

                var eventPPABooking = db.EventBookings.Where(x => x.BookingStatusCode == "PPA" && x.CreatedDate < checkTime && x.CreatedDate > new DateTime(2018, 10, 30, 11, 0, 0)).ToList();  

                foreach (var item in eventPPABooking)
                {
                    if(item.SuperPNROrder.PaymentOrders.Count == 0 && item.SuperPNR.BookingHotels.Count == 0 && item.SuperPNR.Bookings.Count == 0)
                    {
                        EventBundleBookingStatusHandler.Functions.UpdateEventBundlePPAToEXPBookingStatus.Update(item);
                    }
                }

                log.Debug("EventBundle Booking Status Handler Completed.");
            }
            catch(Exception ex)
            {
                SendErrorMail(ex.ToString());
                log.Error("Unable to complete the Reminder Handler process.", ex);
            }
            




        }
    }
}
