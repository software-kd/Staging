using System;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace MayflowerBookingUnitTest.ExpediaService
{
    [TestClass]
    public class ExpediaServiceUnitTest
    {
        //[TestMethod]
        public void GetHotelListById()
        {
            // Initialize Value here for testing
            SearchHotelModel searchHotelModel = InitializeTestingModel.SearchHotelModel();

            List<int> hotelId = new List<int>
            {
                455291
            };

            var result = Alphareds.Module.ServiceCall.ExpediaHotelsServiceCall.GetHotelList(searchHotelModel, hotelId);

            if (result.Errors != null)
            {
                Exception ex = new Exception();
                throw new Exception("An error occur while getting hotel from service: " + System.Environment.NewLine +
                    result.Errors.ErrorMessage);
            }
        }

        //[TestMethod]
        public void GetHotelList()
        {
            // Initialize Value here for testing
            SearchHotelModel searchHotelModel = InitializeTestingModel.SearchHotelModel("Kuala Lumpur");

            var result = Alphareds.Module.ServiceCall.ExpediaHotelsServiceCall.GetHotelList(searchHotelModel);

            if (result.Errors != null)
            {
                Exception ex = new Exception();
                throw new Exception("An error occur while getting hotel from service: " + System.Environment.NewLine +
                    result.Errors.ErrorMessage);
            }

            SearchRoomModel searchRoomModel = InitializeTestingModel.SearchRoomHotel(searchHotelModel, result.HotelList.First().hotelId);
            var roomAvail = Alphareds.Module.ServiceCall.ExpediaHotelsServiceCall.GetRoomAvailability(searchRoomModel, searchHotelModel);

            if (roomAvail.Errors != null)
            {
                Exception ex = new Exception();
                throw new Exception("An error occur while getting room from service: " + System.Environment.NewLine +
                    roomAvail.Errors.ErrorMessage);
            }
        }

        [TestMethod]
        public  void GetItinerary()
        {
            string userEmail = "ota.test@mayflower-group.com";
            //string userEmail = "EANB2B@mayflower-group.com";

            var test = Alphareds.Module.ServiceCall.ExpediaHotelsServiceCall.GetItinerary(new GetItineraryModel
            {
                CurrencyCode = "MYR",
                CustomerIpAddress = "47.88",
                CustomerSessionId = Guid.NewGuid().ToString(),
                CustomerUserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36",
                Email = userEmail,
                ItineraryID = "279670054"
            });

            throw new Exception("");
        }
    }
}
