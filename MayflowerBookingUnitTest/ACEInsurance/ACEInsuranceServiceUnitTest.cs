using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.Model;
using Alphareds.Module.SabreWebService.SWS;

namespace MayflowerBookingUnitTest.ACEInsurance
{
    [TestClass]
    public class ACEInsuranceServiceUnitTest
    {
        [TestMethod]
        public void GetTravelQuote()
        {
            var product = GenerateTestProduct();

            var res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetTravelQuote(product);
        }

        //[TestMethod]
        public void GetTravelPolicy()
        {
            var product = GenerateTestProduct();

            //var res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetTravelPolicy(product);
        }

        //[TestMethod]
        public void GetPolicyDetails()
        {
            string policyNum = "CTPMYAA0000030";
            Alphareds.Module.ACEInsuranceWebService.ACEIns.Flag flag = Alphareds.Module.ACEInsuranceWebService.ACEIns.Flag.Inquiry;
            
            var res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetPolicyDetails(policyNum, flag);
        }

        private CheckoutProduct GenerateTestProduct()
        {
            SearchFlightResultViewModel searchModel = new SearchFlightResultViewModel
            {
                ArrivalStation = "PEN",
                BeginDate = new DateTime(2018, 2, 2),
                DepartureStation = "KUL",
                CabinClass = "Y",
                DepartureTime = "00002359",
                DirectFlight = false,
                EndDate = new DateTime(2018, 2, 10),
                PrefferedAirlineCode = string.Empty,
                ReturnTime = "00002359",
                TripType = "Return",
                Adults = 1,
                Childrens = 1,
                Infants = 0
            };

            var contactPerson = new ContactPerson
            {
                Address1 = "asd",
                Address2 = "fgh",
                Address3 = "jkl",
                City = "rgrrgeg",
                CountryCode = "123123",
                Email = "asd@asd.asd",
                GivenName = "werwe",
                Phone1 = "12573725",
                Phone1LocationCode = "08",
                Phone1Prefix = "0808",
                Phone1PrefixNo = "321",
                Phone1UseType = "cell",
                Phone2 = "312131",
                Phone2LocationCode = "321654",
                Phone2Prefix = "654",
                Phone2PrefixNo = "564",
                Phone2UseType = "phone",
                PostalCode = "56465",
                State = "asddd",
                Surname = "asdsad",
                Title = "Mr",
                DOB = new DateTime(1999, 08, 01),
            };

            CheckoutProduct product = new CheckoutProduct
            {
                ContactPerson = contactPerson,
            };

            product.InsertProduct(new ProductFlight
            {
                SearchFlightInfo = searchModel,
                ContactPerson = contactPerson,
                TravellerDetails = new List<TravellerDetail>
                {
                    new TravellerDetail
                    {
                        Address1 = "asd",
                        Address2 = "fgh",
                        Address3 = "jkl",
                        City = "rgrrgeg",
                        CountryCode = "123123",
                        Email = "asd@asd.asd",
                        GivenName = "werwe",
                        Phone1 = "12573725",
                        Phone1LocationCode = "08",
                        Phone1Prefix = "0808",
                        Phone1PrefixNo = "321",
                        Phone1UseType = "cell",
                        Phone2 = "312131",
                        Phone2LocationCode = "321654",
                        Phone2Prefix = "654",
                        Phone2PrefixNo = "564",
                        Phone2UseType = "phone",
                        PostalCode = "56465",
                        State = "asddd",
                        Surname = "asdsad",
                        Title = "Mr",
                        DOB = new DateTime(1999, 08, 01),
                    }
                }
            });

            return product;
        }
    }
}
