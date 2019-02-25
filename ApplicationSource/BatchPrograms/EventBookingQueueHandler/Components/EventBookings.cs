using Alphareds.Module.HotelController;
using Alphareds.Module.Model.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBookingQueueHandler.Components
{
    class EventBookings
    {
        public List<string> InformationCaution { get; set; }

        public EventBookings(SuperPNR item, string bookStatus, bool isDeduct)
        {
            try
            {
                HotelServiceController.UpdateEventBooking(item, bookStatus);
            }
            catch (Exception ex)
            {
                InformationCaution.Add(ex.GetBaseException().Message);
            }
        }
    }
}
