using Alphareds.Module.Model.Database;
using Alphareds.Module.ServiceCall;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MayflowerBookingUnitTest.PDFEngine
{
    [TestClass]
    public class PDFEngineUnitTest
    {
        //[TestMethod]
        public void GetPDFByFlightHardcoded()
        {
            Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress[] email = new Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress[]
            {
                new Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress
                {
                    Email = "chtan@alphareds.net",
                    //Email = "irylee@alphareds.com",
                    EmailType = Alphareds.Module.PDFEngineWebService.PDFEngine.emailType.To
                }
            };

            string myTitle = "Testing Title Here";
            string myContent = "This is testing lah.";

            int bookingId = 204;
            Booking flightBooking = Alphareds.Module.BookingController.BookingServiceController.getBooking(bookingId);
            BookingHotel hotelBooking = flightBooking.SuperPNR.BookingHotels.First();

            var sendPDFRespond = PDFEngineServiceCall.SendBookingConfirmationPDFEmail(email, myContent, myTitle, flightBooking);
            //var getPDFRespond = PDFEngineServiceCall.getBookingConfirmationPDF(flightBooking, hotelBooking);
            List<string> errorList = new List<string>();

            if (sendPDFRespond.Header.Error != null)
                errorList.Add(sendPDFRespond.Header.Error);

            //if (getPDFRespond.Header.Error != null)
            //    errorList.Add(getPDFRespond.Header.Error);

            if (errorList.Count > 0)
                throw new Exception("Error on PDF Service.");

        }

        [TestMethod]
        public void GetPDFByHotelHardcoded()
        {
            Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress[] email = new Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress[]
            {
                new Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress
                {
                    //Email = "chtan@alphareds.net",
                    Email = "irylee@alphareds.com",
                    EmailType = Alphareds.Module.PDFEngineWebService.PDFEngine.emailType.To
                }
            };

            //string myTitle = "Testing Title Here";
            //string myContent = "This is testing lah.";

            //int bookingId = 204;
            //Booking flightBooking = Alphareds.Module.BookingController.BookingServiceController.getBooking(bookingId);
            //BookingHotel hotelBooking = flightBooking.SuperPNR.BookingHotels.First();
            //var hotelBooking = Alphareds.Module.HotelController.HotelServiceController.GetBookingHotel(936);

            //var sendPDFRespond = PDFEngineServiceCall.SendBookingConfirmationPDFEmail(email, myContent, myTitle, hotelBooking);
            //var getPDFRespond = PDFEngineServiceCall.getBookingConfirmationPDF(flightBooking, hotelBooking);
            List<string> errorList = new List<string>();

            //if (sendPDFRespond.Header.Error != null)
            //    errorList.Add(sendPDFRespond.Header.Error);

            //if (getPDFRespond.Header.Error != null)
            //    errorList.Add(getPDFRespond.Header.Error);

            if (errorList.Count > 0)
                throw new Exception("Error on PDF Service.");

        }

    }
}
