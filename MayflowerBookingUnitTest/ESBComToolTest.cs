using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model;
using Alphareds.Module.CompareToolWebService.CTWS;
using Alphareds.Module.ServiceCall;

namespace MayflowerBookingUnitTest
{
    [TestClass]
    public class ESBComToolTest
    {
        [TestMethod]
        public void TestFlightAsync()
        {
            string test = "WTF";

            string test2 = "FUCK";

            test = string.Intern(test2);
            //SearchFlightResultViewModel searchModel = new SearchFlightResultViewModel
            //{
            //    ArrivalStation = "PEN",
            //    BeginDate = new DateTime(2018, 2, 2),
            //    DepartureStation = "KUL",
            //    CabinClass = "Y",
            //    DepartureTime = "00002359",
            //    DirectFlight = false,
            //    EndDate = new DateTime(2018, 2, 10),
            //    PrefferedAirlineCode = string.Empty,
            //    ReturnTime = "00002359",
            //    TripType = "Return",
            //    Adults = 1,
            //    Childrens = 1,
            //    Infants = 0
            //};

            //flightResponse rs = await CompareToolServiceCall.RequestFlightAsync(searchModel);
        }
    }
}
