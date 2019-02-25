using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MayflowerBookingUnitTest
{
    [TestClass]
    public class FCFlightUnitTest
    {
        partial class CustomSearchModel : SearchFlightResultViewModel
        {
            public KeyValuePair<string, int>[] PsgTypeList
            {
                get
                {
                    return new KeyValuePair<string, int>[]
                    {
                        new KeyValuePair<string, int>("ADT", Adults),
                        new KeyValuePair<string, int>("CNN", Childrens),
                        new KeyValuePair<string, int>("INF", Infants),
                    };
                }
            }
        }

        private CustomSearchModel searchModel = new CustomSearchModel();
        private List<PassengerDetailBooking> psgList = new List<PassengerDetailBooking>() { };
        private BookingContactPerson bookingPerson = new BookingContactPerson();
        private Random rand = new Random();
        private static char[] Alphabet = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i).ToArray();

        private string testEmail = "ytchin@alphareds.net";

        //Constructor
        public FCFlightUnitTest()
        {
            searchModel = new CustomSearchModel
            {
                Adults = 1,
                ArrivalStation = "SHA",
                BeginDate = new DateTime(2018, 3, 10),
                CabinClass = "Y",
                Childrens = 0,
                DepartureStation = "KUL",
                DepartureTime = "00002359",
                DirectFlight = false,
                EndDate = new DateTime(2018, 3, 15),
                Infants = 0,
                ReturnTime = "00002359",
                TripType = "Return"
            };

            for (int i = 0; i < searchModel.PsgTypeList.Length; i++)
            {
                for (int a = 0; a < searchModel.PsgTypeList[i].Value; a++)
                {
                    psgList.Add(PassengerDetail(searchModel.PsgTypeList[i].Key));
                }
            }

            bookingPerson = new BookingContactPerson
            {
                Title = "MR",
                Surname = new string(Enumerable.Range(0, 8).Select(i => Alphabet[rand.Next(Alphabet.Length)]).ToArray()),
                Email = testEmail,
                DOB = new DateTime(1991, 8, 8),
                DOBDays = 8,
                DOBMonths = 8,
                DOBYears = 1991,
                GivenName = new string(Enumerable.Range(0, 8).Select(i => Alphabet[rand.Next(Alphabet.Length)]).ToArray()),

            };
        }

        [TestMethod]
        public void Testing()
        {
            var result = CompareToolServiceCall.RequestFlight(searchModel).FlightData.Where(x => x.ServiceSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.SACS);

            var test = result.Where(x =>
            {
                var odo = x.pricedItineryModel.OriginDestinationOptions;
                var segment = odo.SelectMany(a => a.FlightSegments);

                return segment.GroupBy(a => new { a.FlightNumber, a.AirlineCode }).Any(b => b.Count() > 2);
            });
        }

        [TestMethod]
        public void FlightTest()
        {
            var result = CompareToolServiceCall.RequestFlight(searchModel).FlightData.Where(x => x.ServiceSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia);
            int flightCount = result.Count();
            int randomIndex = rand.Next(flightCount);
            var selectedFlight = result.ElementAt(randomIndex);
        }

        public PassengerDetailBooking PassengerDetail(string psgType)
        {
            string title = "";
            string DOB = "";
            int? dobDay = 0;
            int? dobMonth = 0;
            int? dobYear = 0;

            switch (psgType)
            {
                case "ADT":
                    title = "MR";
                    DOB = "08-08-1991";
                    dobDay = 8;
                    dobMonth = 8;
                    dobYear = 1991;
                    break;
                case "CNN":
                    title = "mstr";
                    DOB = "02-03-2000";
                    dobDay = 2;
                    dobMonth = 3;
                    dobYear = 2000;
                    break;
                case "INF":
                    title = "mstr";
                    DOB = "03-03-2017";
                    dobDay = 3;
                    dobMonth = 3;
                    dobYear = 2017;
                    break;
                default:
                    break;
            };

            return new PassengerDetailBooking
            {
                DOB = DOB,
                DOBDays = dobDay,
                DOBMonths = dobMonth,
                DOBYears = dobYear,
                FrequentFlyerNo = string.Empty,
                FrequrntFlyerNoAirline = string.Empty,
                GivenName = new string(Enumerable.Range(0, 8).Select(i => Alphabet[rand.Next(Alphabet.Length)]).ToArray()),
                IdentityNumber = string.Empty,
                InboundBaggageCode = string.Empty,
                MainIsPassenger = false,
                Nationality = string.Empty,
                OutboundBaggageCode = string.Empty,
                PassengerEmail = testEmail,
                PassengerType = psgType,
                Surname = new string(Enumerable.Range(0, 8).Select(i => Alphabet[rand.Next(Alphabet.Length)]).ToArray()),
                Title = title
            };
        }

        [TestMethod]
        public void CommonTest()
        {
            int agentID = -1;
            bool sucess = int.TryParse("b", out agentID);
        }

        [TestMethod]
        public void GenerateDeepLink()
        {
            var selectedFlight = GetRandomFlight(filterBysupplier: true, minSeg: 2);
            string deepLink = "/AffiliateProgram/Flight?";
            deepLink += string.Format("paxInfant={0}&paxAdult={1}&airlineCode={2}&ibDate={3}&ori={4}&class={5}&isRoundtrip={6}&paxChild={7}&des={8}&obDate={9}&isDirectFlight={10}&source={11}&action={12}&"
                                      , searchModel.Infants //paxInfant, 0
                                      , searchModel.Adults //paxAdult, 1
                                      , searchModel.PrefferedAirlineCodeSub //airlineCode, 2
                                      , searchModel.EndDate?.ToString("yyyyMMdd") //ibDate, 3
                                      , searchModel.DepartureStationCode //ori, 4
                                      , searchModel.CabinClass // class, 5
                                      , searchModel.isReturn ? 1 : 0 //isRoundtrip, 6
                                      , searchModel.Childrens //paxChild, 7
                                      , searchModel.ArrivalStationCode //des, 8
                                      , searchModel.BeginDate?.ToString("yyyyMMdd") //obDate, 9
                                      , searchModel.DirectFlight ? 1 : 0 //isDirectFlight, 10
                                      , selectedFlight.ServiceSource.ToString() //source, 11
                                      , "s3" //action, 12
                                      );

            //Construct Segment
            string segment = string.Empty;
            var segs = selectedFlight.pricedItineryModel.OriginDestinationOptions
                       .SelectMany(x => x.FlightSegments)
                       .Select(x =>
                       {
                           return string.Format("{0}{1}_{2}_{3}_{4}_{5}"
                                                , x.DepartureAirportLocationCode
                                                , x.ArrivalAirportLocationCode
                                                , x.ResBookDesigCode
                                                , x.AirlineCode.Trim()
                                                , x.FlightNumber.Trim()
                                                , x.DepartureDateTime.ToString("yyyyMMddHHmm")
                                                );
                       });

            int leng = segs.Count();
            for (int i = 0; i < leng; i++)
            {
                segment += segs.ElementAt(i);

                if (i != (leng - 1))
                {
                    segment += "|";
                }
            }

            deepLink += string.Format("segments={0}"
                                     , segment);

            Console.Write(deepLink);
        }

        public Alphareds.Module.CompareToolWebService.CTWS.flightData GetRandomFlight(Alphareds.Module.CompareToolWebService.CTWS.serviceSource srvSrc = Alphareds.Module.CompareToolWebService.CTWS.serviceSource.SACS, bool filterBysupplier = false, int minSeg = 1)
        {
            try
            {
                var result = CompareToolServiceCall.RequestFlight(searchModel).FlightData;

                if (filterBysupplier)
                {
                    result = result.Where(x =>
                    {
                        bool matched = x.ServiceSource == srvSrc;
                        int singleFltSegs = x.pricedItineryModel.OriginDestinationOptions.FirstOrDefault().FlightSegments.Length;
                        matched = singleFltSegs >= minSeg;
                        return matched;
                    }).ToArray();
                }

                int flightCount = result.Count();
                int randomIndex = rand.Next(flightCount);
                var selectedFlight = result.ElementAt(randomIndex);
                return selectedFlight;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
