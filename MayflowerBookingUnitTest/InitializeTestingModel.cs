using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model;

namespace MayflowerBookingUnitTest
{
    public static class InitializeTestingModel
    {
        private static string testEmail = "ytchin@alphareds.net";

        #region Hotel
        public static SearchHotelModel SearchHotelModel(string destination = null)
        {
            return new SearchHotelModel
            {
                Destination = destination,

                ArrivalDate = DateTime.Today.AddDays(20),
                DepartureDate = DateTime.Today.AddDays(30),
                CurrencyCode = "MYR",

                NoOfAdult = 5,
                NoOfRoom = 2,

                CustomerIpAddress = "1.1.1.1",
                CustomerUserAgent = "n/a",
                CustomerSessionId = Guid.NewGuid().ToString(),
            };
        }

        public static SearchHotelModel SearchHotelModel(string destination, int rooms, int adults)
        {
            var _model = InitializeTestingModel.SearchHotelModel(destination);
            _model.NoOfAdult = adults;
            _model.NoOfRoom = rooms;

            return _model;
        }

        public static SearchHotelModel SearchHotelModel(string destination, int rooms, int adults, int childs, List<int> childAge)
        {
            var _model = InitializeTestingModel.SearchHotelModel(destination);
            _model.NoOfAdult = adults;
            _model.NoOfRoom = rooms;
            _model.NoOfInfant = childs;

            _model.NoOfChildAge = childAge;

            if (childAge?.Count < childs)
                throw new Exception("Child age doesn't properly assigned.");
            
            return _model;
        }

        public static SearchRoomModel SearchRoomHotel(SearchHotelModel hotelListReq, string hotelID)
        {
            return new SearchRoomModel
            {
                ArrivalDate = hotelListReq.ArrivalDate,
                DepartureDate = hotelListReq.DepartureDate,
                CurrencyCode = hotelListReq.CurrencyCode,
                CustomerIpAddress = hotelListReq.CustomerIpAddress,
                CustomerSessionId = hotelListReq.CustomerSessionId,
                CustomerUserAgent = hotelListReq.CustomerUserAgent,
                HotelID = hotelID,
            };
        }
        #endregion

        #region Flight
        public static class FlightTestingModel
        {
            public static SearchFlightResultViewModel SearchFlightModel()
            {
                return new SearchFlightResultViewModel
                {
                    Adults = 1,
                    ArrivalStation = "KUL",
                    BeginDate = new DateTime(2018, 4, 4),
                    CabinClass = "Y",
                    Childrens = 0,
                    DepartureStation = "PEN",
                    DepartureTime = "00002359",
                    DirectFlight = false,
                    EndDate = new DateTime(2018, 8, 8),
                    Infants = 0,
                    PrefferedAirlineCode = string.Empty,
                    ReturnTime = "00002359",
                    TripType = "Return",
                    PromoCode = "FlightAnyKUL"
                };
            }

            public static List<TravellerDetail> TvrDtl(List<KeyValuePair<string, int>> psgTypeList, DateTime? departTime = null)
            {
                Random rand = new Random();
                char[] Alphabet = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i).ToArray();
                List<TravellerDetail> tvrList = new List<TravellerDetail>();

                foreach (var psg in psgTypeList)
                {
                    string title = "";
                    DateTime DOB = DateTime.MinValue;

                    switch (psg.Key.ToUpper())
                    {
                        case "ADT":
                            title = "MR";
                            DOB = new DateTime(1991, 8, 8);
                            break;
                        case "CNN":
                            title = "mstr";
                            DOB = new DateTime(2000, 3, 2);
                            break;
                        case "INF":
                            title = "mstr";
                            DOB = new DateTime(2017, 3, 3);
                            break;
                        default:
                            title = "MR";
                            DOB = new DateTime(1991, 8, 8);
                            break;
                    };

                    tvrList.Add(new TravellerDetail
                    {
                        Address1 = string.Empty,
                        Address2 = string.Empty,
                        Address3 = string.Empty,
                        City = string.Empty,
                        CountryCode = string.Empty,
                        DOB = DOB,
                        Email = testEmail,
                        FrequentFlyerNo = null,
                        FrequrntFlyerNoAirline = null,
                        GivenName = new string(Enumerable.Range(0, 8).Select(i => Alphabet[rand.Next(Alphabet.Length)]).ToArray()),
                        IdentityNumber = null,
                        //InboundBaggageCode = null,
                        Nationality = null,
                        //OutboundBaggageCode = null,
                        PassengerType = psg.Key.ToUpper(),
                        PassportExpiryDate = null,
                        PassportExpiryDateDays = null,
                        PassportExpiryDateMonths = null,
                        PassportExpiryDateYears = null,
                        PassportIssueCountry = null,
                        PassportNumber = null,
                        Phone1 = "161234567",
                        Phone1LocationCode = "KUL",
                        Phone1Prefix = "60",
                        Phone1PrefixNo = "60",
                        Phone1UseType = "M",
                        Phone2 = null,
                        Phone2LocationCode = null,
                        Phone2Prefix = null,
                        Phone2PrefixNo = null,
                        Phone2UseType = null,
                        PostalCode = null,
                        State = null,
                        Surname = new string(Enumerable.Range(0, 8).Select(i => Alphabet[rand.Next(Alphabet.Length)]).ToArray()),
                        Title = title,
                        _DepartureDate = departTime ?? DateTime.MinValue
                    });
                }

                return tvrList;
            }
        }
        #endregion
    }
}
