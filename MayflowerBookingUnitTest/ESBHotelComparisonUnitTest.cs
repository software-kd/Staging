using System;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace MayflowerBookingUnitTest
{
    [TestClass]
    public class ESBHotelComparisonUnitTest
    {
        //[TestMethod]
        public void GetHotelList()
        {
            // Initialize Value here for testing
            SearchHotelModel searchHotelModel = new SearchHotelModel
            {
                ArrivalDate = DateTime.Now.AddDays(5),
                DepartureDate = DateTime.Now.AddDays(10),
                CurrencyCode = "MYR",
                CustomerIpAddress = "1",
                CustomerUserAgent = "1",
                CustomerSessionId = new Guid().ToString(),
                Destination = "Ipoh",
                NoOfAdult = 1,
                NoOfInfant = 1,
                NoOfRoom = 1,
                Star = 10,
                
                SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
                {
                    Expedia = true
                    //Tourplan = true
                    //JacTravel = true
                    //HotelBeds = true
                },

                //totalDays = "3"
            };

            searchHotelModel.ArrivalDate = new DateTime(2017, DateTime.Now.Month + 1, 23);
            searchHotelModel.DepartureDate = new DateTime(2017, DateTime.Now.Month + 1, 27);

            List<int> hotelId = new List<int>()
            {
               133263 ,
               66527 ,
               134811
            };

            List<string> hotelIdString = hotelId.ConvertAll(x => x.ToString());

            // Tourplan Hotel ID
            hotelIdString.Add("KULACASCKULTEST01");

            var ESBresult = ESBHotelServiceCall.GetHotelList(searchHotelModel);
            var ESB_Id_Result = ESBHotelServiceCall.GetHotelList(searchHotelModel, hotelIdString);
            
            //throw new Exception("Total Hotel " + ESBresult.HotelList.Length.ToString());
        }

        //[TestMethod]
        public void GetRoomAvailability()
        {
            var searchHotelModel = InitializeTestingModel.SearchHotelModel("Kuala Lumpur, Malaysia");
            //searchHotelModel.RType = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RateType.ConcertPackage;

            searchHotelModel.Result = ESBHotelServiceCall.GetHotelList(searchHotelModel);

            var roomModel = InitializeTestingModel.SearchRoomHotel(searchHotelModel, searchHotelModel.Result.HotelList[0].hotelId);

            //var res = ESBHotelServiceCall.GetRoomAvailability(roomModel, searchHotelModel);
            var res2 = ESBHotelServiceCall.GetRoomAvailability(roomModel, searchHotelModel);
        }

        [TestMethod]
        public void GetRoomAll()
        {
            Search.Hotel.Room room = new Search.Hotel.Room("url", "1", "1", "1")
            {
                
            };
        }

        [TestMethod]
        public void GetTPItinerary()
        {
            GetItineraryModel itineraryModel = new GetItineraryModel();
            itineraryModel.ItineraryID = "609069";

            //TourplanServiceCall.GetBooking(itineraryModel);   
        }
    }
}
