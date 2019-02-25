using System;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Alphareds.Module.Common;
using HotelBookingHandler;
using System.Threading.Tasks;
using Alphareds.Module.Model.Database;

namespace MayflowerBookingUnitTest.ExpediaService
{
    [TestClass]
    public class TourplanServiceUnitTest
    {
        [TestMethod]
        public async Task CheckoutReserveRoomTP()
        {
            MayFlower db = new MayFlower();
            List<Enumeration.BatchBookResultType> bookResultTask = new List<Enumeration.BatchBookResultType>();

            try
            {
                int bookingid = 1110; //booking id without payment
                BookingQuery.Tourplan tourplan = new BookingQuery.Tourplan();
                var result = await tourplan.CheckoutReserveRoom(bookingid, "PPA", "http://localhost:52197", db);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [TestMethod]
        public async Task CheckoutReserveRoomJAC()
        {
            MayFlower db = new MayFlower();
            List<Enumeration.BatchBookResultType> bookResultTask = new List<Enumeration.BatchBookResultType>();

            try
            {
                int bookingid = 1127;
                BookingQuery.JacTravel jactravel = new BookingQuery.JacTravel();
                var result = await jactravel.CheckoutReserveRoom(bookingid, "PPA", "http://localhost:52197", db);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [TestMethod]
        public async Task CheckoutReserveRoomHB()
        {
            MayFlower db = new MayFlower();
            List<Enumeration.BatchBookResultType> bookResultTask = new List<Enumeration.BatchBookResultType>();

            try
            {
                int bookingid = 1126;
                BookingQuery.HotelBeds hotelbeds = new BookingQuery.HotelBeds();
                var result = await hotelbeds.CheckoutReserveRoom(bookingid, "PPA", "http://localhost:52197", db);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
