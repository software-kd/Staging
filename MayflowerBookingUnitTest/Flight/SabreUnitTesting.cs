using Alphareds.Module.Model;
using Alphareds.Module.SabreWebService.SWS;
using Alphareds.Module.ServiceCall;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MayflowerBookingUnitTest.Flight
{
    [TestClass]
    public class SabreUnitTesting
    {
        SearchFlightResultViewModel flightSearch = InitializeTestingModel.FlightTestingModel.SearchFlightModel();
        List<KeyValuePair<string, int>> psgList = new List<KeyValuePair<string, int>> {
            new KeyValuePair<string, int>("ADT", 1),
        };

        [TestMethod]
        public void MainTesting()
        {
            var selectedFlight = GetRandomFlight(GetSabreFlight());
            var bkFlt = SabreServiceCall.BookFlightEnhancedAirBookResponse(selectedFlight, string.Empty);

            var tvrDtl = InitializeTestingModel.FlightTestingModel.TvrDtl(psgList, bkFlt.Output.OriginDestinationOptions.FirstOrDefault().FlightSegments.FirstOrDefault().DepartureDateTime);
            var test = SabreServiceCall.AddPassengerDetailsResponse(bkFlt, tvrDtl);
        }

        private SearchFlightBargainFinderMaxResponse GetSabreFlight()
        {
            var SabreRs = SabreServiceCall.SearchFlightBargainFinderMaxResponse(flightSearch);
            return SabreRs;
        }

        private PricedItineryModel GetRandomFlight(SearchFlightBargainFinderMaxResponse fltRlts)
        {
            Random rnd = new Random();
            return fltRlts.Output[rnd.Next(0, fltRlts.Output.Length)];
        }
    }
}
