using Alphareds.Module.Common;
using Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels;
using Alphareds.Module.ExpediaTAAPWebService.ExpediaTAAP;
using Alphareds.Module.TourplanWebService.TPWS;
using Alphareds.Module.JacTravelWebService.JTWS;
using Alphareds.Module.HotelBedsWebService.HBWS;
using Alphareds.Module.EANRapidHotels.RapidServices;
using Alphareds.Module.HotelController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.ServiceCall;
using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.CommonController;
using System.Linq.Expressions;
using System.Diagnostics;
using HotelBookingHandler.Functions;

namespace HotelBookingHandler
{
    public class BookingQuery
    {
        public class DBQuery
        {
            private static double takeBookingPaymentAfterSecond = Convert.ToDouble(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("TakeBookingPaymentAfterSecond"));
            private static int? _parseSecond = (int?)takeBookingPaymentAfterSecond;

            public static IQueryable<BookingHotel> GetPendingBooking(MayFlower db)
            {
                db = db ?? new MayFlower();

                var paymentList = db.PaymentOrders.Where(x => x.PaymentDate != null
                && (x.PaymentStatusCode == "PEND" || x.PaymentStatusCode == "PAID" || x.PaymentStatusCode == "FAIL") && x.PaymentMethodCode.ToLower().StartsWith("ipa")
                //&& (x.PaymentStatusCode == "PEND" || x.PaymentStatusCode == "PAID") && x.PaymentMethodCode.ToLower().StartsWith("ipa")
                && (x.SuperPNROrder.SuperPNR.Bookings.Count == 0 || (x.SuperPNROrder.SuperPNR.Bookings.Count > 0 && x.SuperPNROrder.SuperPNR.Bookings.All(f => f.BookingStatusCode == "CON" || f.BookingStatusCode == "QPL" || f.BookingStatusCode == "TKI")))
                && (x.SuperPNROrder.SuperPNR.BookingInsurances.Count == 0 || (x.SuperPNROrder.SuperPNR.BookingInsurances.Count > 0 && x.SuperPNROrder.SuperPNR.BookingInsurances.All(f => f.BookingStatusCode == "CON")))
                && x.SuperPNROrder.SuperPNR.BookingHotels.Any(b => b.BookingStatusCode == "PPA")
                && DateTime.Now >= System.Data.Entity.DbFunctions.AddSeconds(x.PaymentDate, _parseSecond));

                var bookingList = paymentList.SelectMany(x => x.SuperPNROrder.SuperPNR.BookingHotels).Distinct();

                return bookingList;
            }

            public static IQueryable<BookingHotel> GetPendingBooking(MayFlower db, string superPNRNo, bool rebookExpired = false)
            {
                db = db ?? new MayFlower();

                var paymentList = db.PaymentOrders.Where(x => x.PaymentDate != null
                && (x.PaymentStatusCode == "PEND" || x.PaymentStatusCode == "PAID" || x.PaymentStatusCode == "FAIL" || x.PaymentStatusCode == "AUTH" || x.PaymentStatusCode == "CAPT")
                && x.PaymentMethodCode.ToLower().StartsWith("ipa")
                && (x.SuperPNROrder.SuperPNR.Bookings.Count == 0 || (x.SuperPNROrder.SuperPNR.Bookings.Count > 0 && x.SuperPNROrder.SuperPNR.Bookings.All(f => f.BookingStatusCode == "CON" || f.BookingStatusCode == "QPL" || f.BookingStatusCode == "TKI")))
                && (x.SuperPNROrder.SuperPNR.BookingInsurances.Count == 0 || (x.SuperPNROrder.SuperPNR.BookingInsurances.Count > 0 && x.SuperPNROrder.SuperPNR.BookingInsurances.All(f => f.BookingStatusCode == "CON")))
                && x.SuperPNROrder.SuperPNR.BookingHotels.Any(b => b.SuperPNRNo == superPNRNo &&
                (b.BookingStatusCode == "PPA" || b.BookingStatusCode == "RHI" || (rebookExpired ? b.BookingStatusCode == "EXP" : false)))
                //&& (x.PaymentStatusCode == "PEND" || x.PaymentStatusCode == "PAID") && x.PaymentMethodCode.ToLower().StartsWith("ipa")
                //&& x.SuperPNROrder.SuperPNR.BookingHotels.Any(b => b.SuperPNRNo == superPNRNo) // any status
                && DateTime.Now >= System.Data.Entity.DbFunctions.AddSeconds(x.PaymentDate, _parseSecond)
                );

                var bookingList = paymentList.SelectMany(x => x.SuperPNROrder.SuperPNR.BookingHotels).Distinct();

                return bookingList;
            }
        }

        public class Expedia
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            string clientIP = "47.88.153.210";
            string sessionId = Guid.NewGuid().ToString();
            string userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
            System.Diagnostics.EventLog eventLog1 { get; set; }
            private StringBuilder BookingLogMsg = new StringBuilder();
            ItineraryLog ItineraryLog { get; set; }

            public Expedia()
            {
                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "MayflowerSource", "MayflowerHotelLog");
                }
                eventLog1.Source = "MayflowerSource";
                eventLog1.Log = "MayflowerHotelLog";
                ItineraryLog = new ItineraryLog(eventLog1);
            }

            private Task<ItineraryResponse> GetReservedItinerary(string itineraryID, string currencyCode)
            {
                string userEmail = Core.IsForStaging ? "ota.test@mayflower-group.com" : "EANB2B@mayflower-group.com";

                GetItineraryModel itineraryModel = new GetItineraryModel();
                itineraryModel.CustomerIpAddress = clientIP;
                itineraryModel.CustomerSessionId = sessionId ?? Guid.NewGuid().ToString();
                itineraryModel.CustomerUserAgent = userAgent;
                itineraryModel.CurrencyCode = currencyCode;
                itineraryModel.ItineraryID = itineraryID;
                itineraryModel.Email = userEmail;

                return Task<ItineraryResponse>.Factory.StartNew(() =>
                {
                    return ExpediaHotelsServiceCall.GetItinerary(itineraryModel);
                });
            }

            /// <summary>
            /// 1 - All Booking Success
            /// 2 - All Booking Fail
            /// 3 - Partial Sucess
            /// </summary>
            /// <param name="token"></param>
            /// <param name="bookStatus"></param>
            /// <param name="isAuthCapture"></param>
            /// <param name="isCrossSell">For clearly indicate is cross sell when email reserve hotel related error.</param>
            public async Task<ProductReserve.BookingRespond> CheckoutReserveRoom(int bookingId, string bookStatus,
                string hostURL, MayFlower db, bool isAuthCapture = false, bool isCrossSell = false)
            {
                BookingHotel booking = db.BookingHotels.Find(bookingId);

                BookingLogMsg.Append(string.Format("SuperPNR : {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, DateTime.Now.ToLoggerDateTime()))
                    .AppendLine().AppendLine();

                var hotel = booking.ToProductHotel(clientIP, userAgent);

                List<ProductReserve.Error> errorLog = new List<ProductReserve.Error>();
                CheckoutProduct product = new CheckoutProduct();
                product.ContactPerson = hotel.ContactPerson;
                product.InsertProduct(hotel);

                StringBuilder sb = new StringBuilder(); // use for append error message
                bool isNoError = true; // use for indicate any error then return to call
                bool isAllReserveSuccess = false;
                bool isPartialSuccess = false;
                bool isGetItineraryError = false;
                bool isAllReserveFail = false;
                List<Exception> exList = new List<Exception>();
                List<ReserveRoomResponse> reserveResultAll = new List<ReserveRoomResponse>();
                List<ReserveRoomResponse> reserveResultSucceed = new List<ReserveRoomResponse>();
                BookingQuery.BookingExpection bookingExpection = null;

                #region Regenerate RateKey for Booking
                if (hotel.RoomDetails.Any(x => x.RateKey == null || x.RateKey == ""))
                {
                    try
                    {
                        var hotelListReq = hotel.SearchHotelInfo;
                        var oldSearchRoomModel = new SearchRoomModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            CurrencyCode = hotelListReq.CurrencyCode,
                            CustomerIpAddress = hotelListReq.CustomerIpAddress,
                            CustomerSessionId = hotelListReq.CustomerSessionId,
                            CustomerUserAgent = hotelListReq.CustomerUserAgent,
                            HotelID = booking.HotelID,
                            SelectedNoOfRoomType = hotel.RoomDetails.Count,
                        };
                        var oldSearchHotelModel = new SearchHotelModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            NoOfAdult = hotelListReq.NoOfAdult,
                            NoOfInfant = hotelListReq.NoOfInfant,
                            NoOfRoom = hotel.RoomDetails.Count,
                        };
                        var roomResult = ESBHotelServiceCall.GetRoomAvailability(oldSearchRoomModel, oldSearchHotelModel, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.Expedia);

                        if (roomResult != null && roomResult.HotelRoomInformationList != null)
                        {
                            int _rateKeyCounter = 0;
                            var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);
                            foreach (var roomSelected in hotel.RoomDetails)
                            {
                                if (!string.IsNullOrWhiteSpace(roomSelected.RateKey))
                                    continue;

                                var _roomQuery = roomList.FirstOrDefault(x =>
                                {
                                    var chargeableRateInfo = x.RateInfos[0].chargeableRateInfo_source ?? x.RateInfos[0].chargeableRateInfo;

                                    if (chargeableRateInfo.NightlyRatesPerRoom == null)
                                    {
                                        chargeableRateInfo.NightlyRatesPerRoom = x.RateInfos[0].Rooms
                                        .SelectMany(xx => xx.ChargeableNightlyRates.Select(d => new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.NightlyRate { baseRate = d.baseRate, rate = d.rate, promo = d.promo })).ToArray();
                                    }

                                    decimal taxAvgPerRoom = (chargeableRateInfo.surchargeTotal.ToDecimal() / hotel.RoomDetails.Count(c => c.RateCode == roomSelected.RateCode && c.RoomTypeCode == roomSelected.RoomTypeCode)).RoundToDecimalPlace();
                                    decimal reQuoteRoomTotalBaseRate = chargeableRateInfo.NightlyRatesPerRoom.Sum(s => s.rate.ToDecimal());

                                    bool tokenExists = x.rateCode == roomSelected.RateCode && x.roomTypeCode == roomSelected.RoomTypeCode;
                                    bool rateExists = ((roomSelected.TotalBaseRate) >= reQuoteRoomTotalBaseRate) &&
                                    (roomSelected.TotalTaxAndServices_Source >= taxAvgPerRoom);

                                    bool roomDescSame = (roomSelected.RoomTypeName == x.description) ||
                                    (!string.IsNullOrWhiteSpace(x.rateDescription) && roomSelected.RateDesc == x.rateDescription);

                                    bool isSameNonRefundable = roomSelected.NonRefundable == x.RateInfos[0].nonRefundable;

                                    /* 2018 July 17 - 
                                     * Fix potentially book same room type hotel but without breakfast with cheaper price.
                                     * */
                                    //bool isRecordMatch = (tokenExists || roomDescSame) && rateExists && isSameNonRefundable;
                                    //if ((!rateExists && roomDescSame) || isRecordMatch)

                                    bool isRecordMatch = tokenExists && roomDescSame && rateExists && isSameNonRefundable;
                                    if (isRecordMatch)
                                    {
                                        BookingLogMsg.AppendLine(string.Format("!!! Hotel ID : [{0}] - {1}", booking.SupplierCode, roomSelected.HotelId));

                                        if (!rateExists && roomDescSame)
                                        {
                                            BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] found, but rate different with original booked amount.", x.rateDescription));

                                            if (!isSameNonRefundable)
                                                BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] cancel policy different.", x.rateDescription))
                                                .AppendLine(string.Format("!!! Customer Policy : [{0}]", roomSelected.NonRefundable))
                                                .AppendLine(string.Format("!!! Latest Policy : [{0}]", x.RateInfos[0].nonRefundable));
                                        }

                                        BookingLogMsg.AppendLine()
                                        .AppendLine(string.Format("!!! Customer Rate : [{0}]", roomSelected.TotalBaseRate_Source))
                                        .AppendLine(string.Format("!!! Latest Rate : [{0}]", reQuoteRoomTotalBaseRate))
                                        .AppendLine();
                                    }

                                    return isRecordMatch;
                                });

                                if (_roomQuery != null)
                                    roomSelected.RateKey = hotel.RoomDetails.Count == _roomQuery.RateInfos[0].Rooms.Length ? _roomQuery.RateInfos[0].Rooms[_rateKeyCounter++]?.rateKey ?? _roomQuery.RateInfos[0].Rooms[0].rateKey
                                        : _roomQuery.RateInfos[0].Rooms[0].rateKey;
                                else
                                    BookingLogMsg.AppendLine(string.Format("!!! Latest room types doesn't match with original room types."));
                            }
                        }
                        else if (roomResult?.Errors?.ErrorMessage != null)
                        {
                            string errorMsg = "!!! Error when regenerate rateKey (Expedia)" + JsonConvert.SerializeObject(roomResult.Errors, Formatting.Indented);
                            logger.Info(errorMsg);
                            BookingLogMsg.AppendLine(errorMsg);
                            exList.Add(new Exception(errorMsg));
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            string errorMsg = "!!! Exception on Expedia Reserve Service - SuperPNRNo: " + booking.SuperPNRNo;
                            sb.AppendLine(errorMsg);
                            logger.Info(ex, errorMsg);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }
                }
                #endregion

                #region Call to Reserve Booking
                try
                {
                    reserveResultAll = ExpediaHotelsServiceCall.ReserveRoomAsync(product);

                    try
                    {
                        BookingLogMsg.AppendLine("Reserve Respond: ")
                            .AppendLine(JsonConvert.SerializeObject(reserveResultAll))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Reserve Respond: ")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    isAllReserveSuccess = reserveResultAll != null && !reserveResultAll.Any(x => x.Errors != null || x.ReserveRoomInformationList == null);
                    isAllReserveFail = !isAllReserveSuccess && reserveResultAll.Count(x => x.Errors != null) == reserveResultAll.Count;
                    isPartialSuccess = !isAllReserveSuccess && (reserveResultAll.Count(x => x.Errors == null) > 0 && reserveResultAll.Count(x => x.Errors == null) < reserveResultAll.Count);

                    reserveResultSucceed = reserveResultAll.Count(x => x.Errors == null) == 0 ? new List<ReserveRoomResponse>() : reserveResultAll.Where(x => x.Errors == null).ToList();

                    #region Action - Reserve Result Error/Fail
                    if (reserveResultAll.Any(x => x.Errors != null))
                    {
                        isNoError = false;
                        string errorMsg = string.Format("Reserve {2} HOTEL fail in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine().AppendLine(errorMsg).AppendLine();

                        var error = reserveResultAll.Select(x => x.Errors).Where(x => x != null);
                        foreach (var item in error)
                        {
                            errorLog.Add(new ProductReserve.Error
                            {
                                ErrorCategory = item.Category,
                                ErrorCode = item.ErrorCode.ToString(),
                                ErrorMsg = item.ErrorMessage
                            });

                            sb.AppendLine();
                            sb.AppendLine("Reserve room fail from Expedia Services:");
                            sb.AppendFormat("{0,-25}: {1}", "Category", item.Category).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Code", item.ErrorCode).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Message", item.ErrorMessage).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        }

                        sb.AppendLine().AppendLine();
                    }
                    else
                    {
                        string errorMsg = string.Format("Reserve {2} HOTEL Success in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine(errorMsg);
                    }
                    #endregion
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on Expedia Reserve Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                List<ItineraryResponse> itineraryResponse = new List<ItineraryResponse>();
                string hotelPhoneNo = null; // assign while update roompaxhotel itinerary number

                #region Update correctly Itinerary Number base on RoomTypeCode
                try
                {
                    foreach (var item in reserveResultSucceed.SelectMany(x => x.ReserveRoomInformationList))
                    {
                        var res = await GetReservedItinerary(item.itineraryId, booking.CurrencyCode);
                        if (res != null)
                            itineraryResponse.Add(res);
                    }

                    try
                    {
                        BookingLogMsg.AppendLine("Itinerary Respond:")
                            .AppendLine(JsonConvert.SerializeObject(itineraryResponse))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Itinerary Respond:")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    // For test error usage
                    //itineraryResponse[0].Errors = new errors() { Category = "testing", ErrorCode = 0, ErrorMessage = "xxxxx" };
                    //itineraryResponse[0].ItineraryList = null;

                    if (itineraryResponse.Any(x => x.Errors != null))
                    {
                        isGetItineraryError = true;
                        isNoError = false;

                        var error = itineraryResponse.Select(x => x.Errors).Where(x => x != null);
                        foreach (var item in error)
                        {
                            sb.AppendLine().AppendLine();
                            sb.AppendLine("Get Itinerary fail from Expedia Services:").AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Category", item.Category).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Code", item.ErrorCode).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Message", item.ErrorMessage).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        }

                        sb.AppendLine().AppendLine();
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on Expedia Get Itinerary Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }

                #endregion

                #region Update RoomPaxHotel Itinerary Number (By Get Itinerary Respond)
                try
                {
                    bool allFailed = itineraryResponse.Count == itineraryResponse.Count(x => x.Errors != null);

                    var itineraryRespList = allFailed ? new List<Itinerary>() : itineraryResponse.Select(x => x.ItineraryList.First());
                    var roomPaxHotelWithoutContact = booking.RoomPaxHotels.Where(x => x.IsContactPerson.HasValue && !x.IsContactPerson.Value);

                    for (int i = 0; i < roomPaxHotelWithoutContact.Count(); i++)
                    {
                        var roomPaxHotel = roomPaxHotelWithoutContact.ToArray()[i];
                        var queryItineraryResp = itineraryRespList.Where(x => x.HotelConfirmationList.Any(t => t.rateCode == roomPaxHotel.RateCode && t.roomTypeCode == roomPaxHotel.RoomTypeCode));
                        int counter = 0;

                        foreach (var bookedRoomSelected in queryItineraryResp.SelectMany(x => x.HotelConfirmationList))
                        {
                            hotelPhoneNo = hotelPhoneNo == null && bookedRoomSelected.HotelInformationList.FirstOrDefault() != null ? bookedRoomSelected.HotelInformationList.FirstOrDefault().phone : hotelPhoneNo;
                            var roomPaxHotelInner = roomPaxHotelWithoutContact.ToArray()[i];
                            var header = queryItineraryResp.First();
                            var bookRespond = reserveResultSucceed.SelectMany(x => x.ReserveRoomInformationList).FirstOrDefault(x => x.itineraryId == header.itineraryId);

                            roomPaxHotelInner.ItineraryNumber = header.itineraryId;
                            roomPaxHotelInner.RoomConfirmationNumber = bookedRoomSelected.confirmationNumber;
                            roomPaxHotelInner.CheckInDateTime = header.itineraryStartDate.ToDateTime();
                            roomPaxHotelInner.CheckOutDateTime = header.itineraryEndDate.ToDateTime();
                            roomPaxHotelInner.ReservationStatusCode = bookedRoomSelected.status;

                            roomPaxHotelInner.SpecialCheckInInstructions = bookRespond.specialCheckInInstructions;
                            roomPaxHotelInner.ProcessedWithConfirmation = string.IsNullOrWhiteSpace(bookRespond.processedWithConfirmation) ? (bool?)null : bookRespond.processedWithConfirmation.ToBoolean();
                            roomPaxHotelInner.CheckInInstructions = bookRespond.checkInInstructions;

                            if (counter < queryItineraryResp.SelectMany(x => x.HotelConfirmationList).Count() - 1)
                            {
                                counter++;
                                i++;
                            }
                        }
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Reserved Hotel Room Pax Error - " + DateTime.Now.ToLoggerDateTime() + " - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Update Booking Header
                try
                {
                    isNoError = reserveResultSucceed != null && reserveResultSucceed.FirstOrDefault() != null;

                    var respondHotelBooked = isNoError ? reserveResultSucceed.First().ReserveRoomInformationList.First() : null;
                    if (isNoError)
                    {
                        booking.CheckInDateTime = respondHotelBooked.arrivalDate.ToDateTime();
                        booking.CheckOutDateTime = respondHotelBooked.departureDate.ToDateTime();
                        booking.HotelStateProvinceCode = respondHotelBooked.hotelStateProvinceCode;
                        booking.HotelAddress = respondHotelBooked.hotelAddress;
                        booking.HotelPostalCode = respondHotelBooked.hotelPostalCode;
                        booking.HotelCountryCode = respondHotelBooked.hotelCountryCode;
                    }
                    booking.HotelPhoneNumber = hotelPhoneNo;

                    booking.BookingStatusCode = isAllReserveFail ? "EXP" :
                        ((sb.Length > 0 || !isNoError || exList.Count > 0) ? Enumeration.SMCBookingStatus.RHI.ToString() : "CON");

                    /* 2017/10/21 - Update Confirmed Payment to PAID.
                     * Require Isolated - Run only when Requery Functions, and move out to another class
                     * No handle capture and authorize here, because we're not sure other component success or not.
                     */
                    //await db.SaveChangesAsync(); // Save first, ensure get latest dbcontext value.
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Reserved Hotel Header Error - " + DateTime.Now.ToLoggerDateTime());
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Reserve Error But Payment Success
                // trigger email to notice Customer Service
                if (((isGetItineraryError || isPartialSuccess) && (sb.Length > 0 || !isNoError)) ||
                    ((isAllReserveSuccess || isPartialSuccess) && exList.Count > 0) ||
                    (!isAuthCapture && (isAllReserveFail || isPartialSuccess || sb.Length > 0 || !isNoError || exList.Count > 0))
                    )
                {
                    bookingExpection = new BookingQuery.BookingExpection(booking, reserveResultAll, sb, isCrossSell);
                }
                #endregion

                if (sb.Length > 0 || !isNoError || exList.Count > 0)
                {
                    AggregateException ae = new AggregateException(exList);

                    logger.Error(ae);
                    foreach (var error in ae.InnerExceptions)
                    {
                        logger.Error(error + Environment.NewLine + Environment.NewLine + sb);
                    }

                    try
                    {
                        ItineraryLog.WriteEventLog("Expedia HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                            BookingLogMsg.ToString() +
                            Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString() +
                            Environment.NewLine + Environment.NewLine + sb
                            , EventLogEntryType.Warning, 301);
                    }
                    catch (Exception ex)
                    {
                        logger.Info("Expedia HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                            BookingLogMsg.ToString() +
                            Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString() +
                            Environment.NewLine + Environment.NewLine + sb +
                            Environment.NewLine + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    try
                    {
                        ItineraryLog.WriteEventLog(BookingLogMsg, EventLogEntryType.SuccessAudit, 302);
                    }
                    catch (Exception ex)
                    {
                        logger.Info(ex, BookingLogMsg.ToString());
                    }
                }

                return new ProductReserve.BookingRespond
                {
                    AttachmentCollection = bookingExpection?.AttachmentCollection,
                    EmailContent = bookingExpection?.EmailContent,
                    BatchBookResult = isAllReserveSuccess ? ProductReserve.BookResultType.AllSuccess :
                    (isPartialSuccess && !isAllReserveFail ? ProductReserve.BookResultType.PartialSuccess : ProductReserve.BookResultType.AllFail),
                    SuperPNRNo = booking.SuperPNRNo,
                    ErrorLog = errorLog,
                };
            }

        }

        public class ExpediaTAAP
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            string clientIP = "47.88.153.210";
            string sessionId = Guid.NewGuid().ToString();
            string userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
            System.Diagnostics.EventLog eventLog1 { get; set; }
            private StringBuilder BookingLogMsg = new StringBuilder();
            ItineraryLog ItineraryLog { get; set; }

            public ExpediaTAAP()
            {
                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "MayflowerSource", "MayflowerHotelLog");
                }
                eventLog1.Source = "MayflowerSource";
                eventLog1.Log = "MayflowerHotelLog";
                ItineraryLog = new ItineraryLog(eventLog1);
            }

            private Task<SearchItineraryResponse> GetReservedItinerary(string itineraryID, string currencyCode)
            {
                string userEmail = Core.IsForStaging ? "ota.test@mayflower-group.com" : "EANB2B@mayflower-group.com";

                GetItineraryModel itineraryModel = new GetItineraryModel();
                itineraryModel.CustomerIpAddress = clientIP;
                itineraryModel.CustomerSessionId = sessionId ?? Guid.NewGuid().ToString();
                itineraryModel.CustomerUserAgent = userAgent;
                itineraryModel.CurrencyCode = currencyCode;
                itineraryModel.ItineraryID = itineraryID;
                itineraryModel.Email = userEmail;

                return Task<SearchItineraryResponse>.Factory.StartNew(() =>
                {
                    return ExpediaTAAPHotelsServiceCall.GetItinerary(itineraryModel);
                });
            }

            /// <summary>
            /// 1 - All Booking Success
            /// 2 - All Booking Fail
            /// 3 - Partial Sucess
            /// </summary>
            /// <param name="token"></param>
            /// <param name="bookStatus"></param>
            /// <param name="isAuthCapture"></param>
            /// <param name="isCrossSell">For clearly indicate is cross sell when email reserve hotel related error.</param>
            public async Task<ProductReserve.BookingRespond> CheckoutReserveRoom(int bookingId, string bookStatus,
                string hostURL, MayFlower db, bool isAuthCapture = false, bool isCrossSell = false)
            {
                BookingHotel booking = db.BookingHotels.Find(bookingId);

                BookingLogMsg.Append(string.Format("SuperPNR : {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, DateTime.Now.ToLoggerDateTime()))
                    .AppendLine().AppendLine();

                var hotel = booking.ToProductHotel(clientIP, userAgent);

                CheckoutProduct product = new CheckoutProduct();
                product.ContactPerson = hotel.ContactPerson;
                product.InsertProduct(hotel);

                StringBuilder sb = new StringBuilder(); // use for append error message
                bool isNoError = true; // use for indicate any error then return to call
                bool isAllReserveSuccess = false;
                bool isPartialSuccess = false;
                bool isGetItineraryError = false;
                bool isAllReserveFail = false;
                List<Exception> exList = new List<Exception>();
                List<BookHotelResponse> reserveResultAll = new List<BookHotelResponse>();
                List<BookHotelResponse> reserveResultSucceed = new List<BookHotelResponse>();
                BookingQuery.BookingExpection bookingExpection = null;

                #region Regenerate RateKey for Booking
                if (hotel.RoomDetails.Any(x => x.RateKey == null || x.RateKey == ""))
                {
                    try
                    {
                        var hotelListReq = hotel.SearchHotelInfo;
                        var oldSearchRoomModel = new SearchRoomModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            CurrencyCode = hotelListReq.CurrencyCode,
                            CustomerIpAddress = hotelListReq.CustomerIpAddress,
                            CustomerSessionId = hotelListReq.CustomerSessionId,
                            CustomerUserAgent = hotelListReq.CustomerUserAgent,
                            HotelID = booking.HotelID,
                            SelectedNoOfRoomType = hotel.RoomDetails.Count,
                        };
                        var oldSearchHotelModel = new SearchHotelModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            NoOfAdult = hotelListReq.NoOfAdult,
                            NoOfInfant = hotelListReq.NoOfInfant,
                            NoOfRoom = hotel.RoomDetails.Count,
                        };
                        var roomResult = ESBHotelServiceCall.GetRoomAvailability(oldSearchRoomModel, oldSearchHotelModel, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.ExpediaTAAP);

                        if (roomResult != null && roomResult.HotelRoomInformationList != null)
                        {
                            int _rateKeyCounter = 0;
                            var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);
                            foreach (var roomSelected in hotel.RoomDetails)
                            {
                                var _roomQuery = roomList.FirstOrDefault(x =>
                                {
                                    var chargeableRateInfo = x.RateInfos[0].chargeableRateInfo_source ?? x.RateInfos[0].chargeableRateInfo;

                                    if (chargeableRateInfo.NightlyRatesPerRoom == null)
                                    {
                                        chargeableRateInfo.NightlyRatesPerRoom = x.RateInfos[0].Rooms
                                        .SelectMany(xx => xx.ChargeableNightlyRates.Select(d => new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.NightlyRate { baseRate = d.baseRate, rate = d.rate, promo = d.promo })).ToArray();
                                    }

                                    decimal taxAvgPerRoom = (chargeableRateInfo.surchargeTotal.ToDecimal() / hotel.RoomDetails.Count(c => c.RateCode == roomSelected.RateCode && c.RoomTypeCode == roomSelected.RoomTypeCode)).RoundToDecimalPlace();
                                    decimal reQuoteRoomTotalBaseRate = chargeableRateInfo.NightlyRatesPerRoom.Sum(s => s.rate.ToDecimal());

                                    bool tokenExists = x.rateCode == roomSelected.RateCode && x.roomTypeCode == roomSelected.RoomTypeCode;
                                    bool rateExists = ((roomSelected.TotalBaseRate) >= reQuoteRoomTotalBaseRate) &&
                                    (roomSelected.TotalTaxAndServices_Source >= taxAvgPerRoom);

                                    bool roomDescSame = (roomSelected.RoomTypeName == x.description) ||
                                    (!string.IsNullOrWhiteSpace(x.rateDescription) && roomSelected.RateDesc == x.rateDescription);

                                    bool isSameNonRefundable = roomSelected.NonRefundable == x.RateInfos[0].nonRefundable;

                                    bool isRecordMatch = (tokenExists || roomDescSame) && rateExists && isSameNonRefundable;

                                    if ((!rateExists && roomDescSame) || isRecordMatch)
                                    {
                                        BookingLogMsg.AppendLine(string.Format("!!! Hotel ID : [{0}] - {1}", booking.SupplierCode, roomSelected.HotelId));

                                        if (!rateExists && roomDescSame)
                                        {
                                            BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] found, but rate different with original booked amount.", x.rateDescription));

                                            if (!isSameNonRefundable)
                                                BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] cancel policy different.", x.rateDescription))
                                                .AppendLine(string.Format("!!! Customer Policy : [{0}]", roomSelected.NonRefundable))
                                                .AppendLine(string.Format("!!! Latest Policy : [{0}]", x.RateInfos[0].nonRefundable));
                                        }

                                        BookingLogMsg.AppendLine()
                                        .AppendLine(string.Format("!!! Customer Rate : [{0}]", roomSelected.TotalBaseRate_Source))
                                        .AppendLine(string.Format("!!! Latest Rate : [{0}]", reQuoteRoomTotalBaseRate))
                                        .AppendLine();
                                    }

                                    return isRecordMatch;
                                });

                                if (_roomQuery != null)
                                    roomSelected.RateKey = hotel.RoomDetails.Count == _roomQuery.RateInfos[0].Rooms.Length ? _roomQuery.RateInfos[0].Rooms[_rateKeyCounter++]?.rateKey ?? _roomQuery.RateInfos[0].Rooms[0].rateKey
                                        : _roomQuery.RateInfos[0].Rooms[0].rateKey;
                                else
                                    BookingLogMsg.AppendLine(string.Format("!!! Latest room types doesn't match with original room types."));
                            }
                        }
                        else if (roomResult?.Errors?.ErrorMessage != null)
                        {
                            string errorMsg = "!!! Error when regenerate rateKey (Expedia)" + JsonConvert.SerializeObject(roomResult.Errors, Formatting.Indented);
                            logger.Info(errorMsg);
                            BookingLogMsg.AppendLine(errorMsg);
                            exList.Add(new Exception(errorMsg));
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            string errorMsg = "!!! Exception on ExpediaTAAP Reserve Service - SuperPNRNo: " + booking.SuperPNRNo;
                            sb.AppendLine(errorMsg);
                            logger.Info(ex, errorMsg);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }
                }
                #endregion

                #region Call to Reserve Booking
                try
                {
                    reserveResultAll = ExpediaTAAPHotelsServiceCall.ReserveRoom(product);

                    try
                    {
                        BookingLogMsg.AppendLine("Reserve Respond: ")
                            .AppendLine(JsonConvert.SerializeObject(reserveResultAll))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Reserve Respond: ")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }
                    if (Core.GetAppSettingValueEnhanced("TaapForceSuccess").ToBoolean() == true)
                    {
                        isAllReserveSuccess = true;
                        isAllReserveFail = !isAllReserveSuccess;
                        isPartialSuccess = !isAllReserveSuccess;
                    }
                    else
                    {
                        isAllReserveSuccess = reserveResultAll != null && !reserveResultAll.Any(x => x.Errors != null);
                        isAllReserveFail = !isAllReserveSuccess && reserveResultAll.Count(x => x.Errors != null) == reserveResultAll.Count;
                        isPartialSuccess = !isAllReserveSuccess && (reserveResultAll.Count(x => x.Errors == null) > 0 && reserveResultAll.Count(x => x.Errors == null) < reserveResultAll.Count);
                    }
                    reserveResultSucceed = reserveResultAll.Count(x => x.Errors == null) == 0 ? new List<BookHotelResponse>() : reserveResultAll.Where(x => x.Errors == null).ToList();

                    #region Action - Reserve Result Error/Fail
                    if (reserveResultAll.Any(x => x.Errors != null))
                    {
                        isNoError = false;
                        string errorMsg = string.Format("Reserve {2} HOTEL fail in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine().AppendLine(errorMsg).AppendLine();

                        var error = reserveResultAll.Select(x => x.Errors).Where(x => x != null);
                        foreach (var item in error)
                        {
                            sb.AppendLine();
                            sb.AppendLine("Reserve room fail from Expedia Services:");
                            sb.AppendFormat("{0,-25}: {1}", "Category", item.Category).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Code", item.ErrorCode).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Message", item.ErrorMessage).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        }

                        sb.AppendLine().AppendLine();
                    }
                    else
                    {
                        string errorMsg = string.Format("Reserve {2} HOTEL Success in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine(errorMsg);
                    }
                    #endregion
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on ExpediaTAAP Reserve Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                List<SearchItineraryResponse> itineraryResponse = new List<SearchItineraryResponse>();
                string hotelPhoneNo = null; // assign while update roompaxhotel itinerary number

                #region Update correctly Itinerary Number base on RoomTypeCode
                try
                {
                    foreach (var item in reserveResultSucceed)
                    {
                        var res = await GetReservedItinerary(item.ItineraryNumber.ToString(), booking.CurrencyCode);
                        if (res != null)
                            itineraryResponse.Add(res);
                    }

                    try
                    {
                        BookingLogMsg.AppendLine("Itinerary Respond:")
                            .AppendLine(JsonConvert.SerializeObject(itineraryResponse))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Itinerary Respond:")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    // For test error usage
                    //itineraryResponse[0].Errors = new errors() { Category = "testing", ErrorCode = 0, ErrorMessage = "xxxxx" };
                    //itineraryResponse[0].ItineraryList = null;

                    if (itineraryResponse.Any(x => x.Errors != null))
                    {
                        isGetItineraryError = true;
                        isNoError = false;

                        var error = itineraryResponse.Select(x => x.Errors).Where(x => x != null);
                        foreach (var item in error)
                        {
                            sb.AppendLine().AppendLine();
                            sb.AppendLine("Get Itinerary fail from Expedia Services:").AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Category", item.Category).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Code", item.ErrorCode).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Message", item.ErrorMessage).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        }

                        sb.AppendLine().AppendLine();
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on Expedia Get Itinerary Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }

                #endregion

                #region Update RoomPaxHotel Itinerary Number (By Get Itinerary Respond)
                try
                {
                    bool allFailed = itineraryResponse.Count == itineraryResponse.Count(x => x.Errors != null);

                    var itineraryRespList = allFailed ? new List<SearchItineraryResponse>() : itineraryResponse;
                    var roomPaxHotelWithoutContact = booking.RoomPaxHotels.Where(x => x.IsContactPerson.HasValue && !x.IsContactPerson.Value);

                    for (int i = 0; i < roomPaxHotelWithoutContact.Count(); i++)
                    {
                        var roomPaxHotel = roomPaxHotelWithoutContact.ToArray()[i];
                        //var queryItineraryResp = itineraryRespList.Where(x => x.HotelDetails.Any(t => t.rateCode == roomPaxHotel.RateCode && t.roomTypeCode == roomPaxHotel.RoomTypeCode));
                        //int counter = 0;

                        //foreach (var bookedRoomSelected in queryItineraryResp.SelectMany(x => x.HotelConfirmationList))
                        //{
                        //    hotelPhoneNo = hotelPhoneNo == null && bookedRoomSelected.HotelInformationList.FirstOrDefault() != null ? bookedRoomSelected.HotelInformationList.FirstOrDefault().phone : hotelPhoneNo;
                        //    var roomPaxHotelInner = roomPaxHotelWithoutContact.ToArray()[i];
                        //    var header = queryItineraryResp.First();
                        //    var bookRespond = reserveResultSucceed.SelectMany(x => x.ReserveRoomInformationList).FirstOrDefault(x => x.itineraryId == header.itineraryId);

                        //    roomPaxHotelInner.ItineraryNumber = header.itineraryId;
                        //    roomPaxHotelInner.RoomConfirmationNumber = bookedRoomSelected.confirmationNumber;
                        //    roomPaxHotelInner.CheckInDateTime = header.itineraryStartDate.ToDateTime();
                        //    roomPaxHotelInner.CheckOutDateTime = header.itineraryEndDate.ToDateTime();
                        //    roomPaxHotelInner.ReservationStatusCode = bookedRoomSelected.status;

                        //    roomPaxHotelInner.SpecialCheckInInstructions = bookRespond.specialCheckInInstructions;
                        //    roomPaxHotelInner.ProcessedWithConfirmation = string.IsNullOrWhiteSpace(bookRespond.processedWithConfirmation) ? (bool?)null : bookRespond.processedWithConfirmation.ToBoolean();
                        //    roomPaxHotelInner.CheckInInstructions = bookRespond.checkInInstructions;

                        //    if (counter < queryItineraryResp.SelectMany(x => x.HotelConfirmationList).Count() - 1)
                        //    {
                        //        counter++;
                        //        i++;
                        //    }
                        //}
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Reserved Hotel Room Pax Error - " + DateTime.Now.ToLoggerDateTime() + " - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Update Booking Header
                try
                {
                    isNoError = reserveResultSucceed != null && reserveResultSucceed.FirstOrDefault() != null;

                    var respondHotelBooked = isNoError ? reserveResultSucceed.First().HotelDetails : null;
                    if (isNoError)
                    {
                        //booking.CheckInDateTime = reserveResultSucceed.First().HotelConfirmation.;
                        //booking.CheckOutDateTime = respondHotelBooked.departureDate.ToDateTime();
                        booking.HotelStateProvinceCode = respondHotelBooked.Location.Address.PostalCode;
                        booking.HotelAddress = respondHotelBooked.Location.Address.Address1Member;
                        booking.HotelPostalCode = respondHotelBooked.Location.Address.PostalCode;
                        booking.HotelCountryCode = respondHotelBooked.Location.Address.Country;
                    }
                    booking.HotelPhoneNumber = hotelPhoneNo;

                    booking.BookingStatusCode = isAllReserveFail ? "EXP" :
                        ((sb.Length > 0 || !isNoError || exList.Count > 0) ? Enumeration.SMCBookingStatus.RHI.ToString() : "CON");

                    /* 2017/10/21 - Update Confirmed Payment to PAID.
                     * Require Isolated - Run only when Requery Functions, and move out to another class
                     * No handle capture and authorize here, because we're not sure other component success or not.
                     */
                    //await db.SaveChangesAsync(); // Save first, ensure get latest dbcontext value.
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Reserved Hotel Header Error - " + DateTime.Now.ToLoggerDateTime());
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Reserve Error But Payment Success
                // trigger email to notice Customer Service
                if (((isGetItineraryError || isPartialSuccess) && (sb.Length > 0 || !isNoError)) ||
                    ((isAllReserveSuccess || isPartialSuccess) && exList.Count > 0) ||
                    (!isAuthCapture && (isAllReserveFail || isPartialSuccess || sb.Length > 0 || !isNoError || exList.Count > 0))
                    )
                {
                    bookingExpection = new BookingQuery.BookingExpection(booking, reserveResultAll, sb, isCrossSell);
                }
                #endregion

                if (sb.Length > 0 || !isNoError || exList.Count > 0)
                {
                    AggregateException ae = new AggregateException(exList);

                    logger.Error(ae);
                    foreach (var error in ae.InnerExceptions)
                    {
                        logger.Error(error + Environment.NewLine + Environment.NewLine + sb);
                    }

                    try
                    {
                        ItineraryLog.WriteEventLog("ExpediaTAAP HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                            BookingLogMsg.ToString() +
                            Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString() +
                            Environment.NewLine + Environment.NewLine + sb
                            , EventLogEntryType.Warning, 301);
                    }
                    catch (Exception ex)
                    {
                        logger.Info("ExpediaTAAP HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                            BookingLogMsg.ToString() +
                            Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString() +
                            Environment.NewLine + Environment.NewLine + sb +
                            Environment.NewLine + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    try
                    {
                        ItineraryLog.WriteEventLog(BookingLogMsg, EventLogEntryType.SuccessAudit, 302);
                    }
                    catch (Exception ex)
                    {
                        logger.Info(ex, BookingLogMsg.ToString());
                    }
                }

                return new ProductReserve.BookingRespond
                {
                    AttachmentCollection = bookingExpection?.AttachmentCollection,
                    EmailContent = bookingExpection?.EmailContent,
                    BatchBookResult = isAllReserveSuccess ? ProductReserve.BookResultType.AllSuccess :
                    (isPartialSuccess && !isAllReserveFail ? ProductReserve.BookResultType.PartialSuccess : ProductReserve.BookResultType.AllFail),
                    SuperPNRNo = booking.SuperPNRNo,
                };
            }

        }

        public class Tourplan
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            string clientIP = "47.88.153.210";
            string sessionId = Guid.NewGuid().ToString();
            string userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
            System.Diagnostics.EventLog eventLog1;
            private StringBuilder BookingLogMsg = new StringBuilder();

            public Tourplan()
            {
                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "MayflowerSource", "MayflowerHotelLog");
                }
                eventLog1.Source = "MayflowerSource";
                eventLog1.Log = "MayflowerHotelLog";
            }

            private Task<List<Reply4>> sendBookHotelRequest(CheckoutProduct checkoutModel)
            {
                var BookHotelRespond = Task<List<Reply4>>.Factory.StartNew(() =>
                {
                    return TourplanServiceCall.BookHotelRooms(checkoutModel);
                });

                return BookHotelRespond;
            }

            private Task<SearchBookingReply> GetReservedItinerary(string itineraryID)
            {
                GetItineraryModel itineraryModel = new GetItineraryModel();
                itineraryModel.ItineraryID = itineraryID;

                return Task<SearchBookingReply>.Factory.StartNew(() =>
                {
                    return TourplanServiceCall.GetBooking(itineraryModel);
                });
            }

            private async Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> getHotelFromEBSSearchModelTP(SearchHotelModel searchModel, List<string> hotelId)
            {
                // 2017/06/30 - Tourplan doesn't sell as dry sell, as dynamic configurable will change to config base if override turned on.
                searchModel.SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
                {
                    Expedia = false,
                    Tourplan = true,
                    JacTravel = false,
                    HotelBeds = false,
                };

                return await Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
                {
                    return ESBHotelServiceCall.GetHotelList(searchModel, hotelId);
                });
            }

            /// <summary>
            /// 1 - All Booking Success
            /// 2 - All Booking Fail
            /// 3 - Partial Sucess
            /// </summary>
            /// <param name="token"></param>
            /// <param name="bookStatus"></param>
            /// <param name="isAuthCapture"></param>
            /// <param name="isCrossSell">For clearly indicate is cross sell when email reserve hotel related error.</param>
            public async Task<ProductReserve.BookingRespond> CheckoutReserveRoom(int bookingId, string bookStatus,
                string hostURL, MayFlower db, bool isAuthCapture = false, bool isCrossSell = false)
            {
                BookingHotel booking = await db.BookingHotels.FindAsync(bookingId);

                BookingLogMsg.Append(string.Format("SuperPNR : {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, DateTime.Now.ToLoggerDateTime())).AppendLine().AppendLine();

                var hotel = booking.ToProductHotel(clientIP, userAgent);
                List<ProductReserve.Error> errorLog = new List<ProductReserve.Error>();
                CheckoutProduct product = new CheckoutProduct();
                product.ContactPerson = hotel.ContactPerson;
                product.InsertProduct(hotel);

                StringBuilder sb = new StringBuilder(); // use for append error message
                bool isNoError = true; // use for indicate any error then return to call
                bool isAllReserveSuccess = false;
                bool isPartialSuccess = false;
                bool isGetItineraryError = false;
                bool isAllReserveFail = false;
                List<string> bookReturnStatus = new List<string>();
                List<Exception> exList = new List<Exception>();
                List<Reply4> reserveResultAll = new List<Reply4>();
                List<Reply4> reserveResultSucceed = new List<Reply4>();
                BookingExpection bookingExpection = null;

                #region Regenerate RateKey for Booking
                if (hotel.RoomDetails.Any(x => x.RateKey == null || x.RateKey == ""))
                {
                    try
                    {
                        var hotelListReq = hotel.SearchHotelInfo;
                        var oldSearchRoomModel = new SearchRoomModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            CurrencyCode = hotelListReq.CurrencyCode,
                            CustomerIpAddress = hotelListReq.CustomerIpAddress,
                            CustomerSessionId = hotelListReq.CustomerSessionId,
                            CustomerUserAgent = hotelListReq.CustomerUserAgent,
                            HotelID = booking.HotelID,
                        };
                        var oldSearchHotelModel = new SearchHotelModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            NoOfAdult = hotelListReq.NoOfAdult,
                            NoOfInfant = hotelListReq.NoOfInfant,
                            NoOfRoom = hotelListReq.NoOfRoom,
                        };
                        var roomResult = await ESBHotelServiceCall.GetRoomAvailabilityAsync(oldSearchRoomModel, oldSearchHotelModel, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.Tourplan);

                        if (roomResult != null && roomResult.HotelRoomInformationList != null)
                        {
                            var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);
                            foreach (var roomSelected in hotel.RoomDetails)
                            {
                                var _roomQuery = roomList.FirstOrDefault(x =>
                                {
                                    var chargeableRateInfo = x.RateInfos[0].chargeableRateInfo_source ?? x.RateInfos[0].chargeableRateInfo;

                                    if (chargeableRateInfo.NightlyRatesPerRoom == null)
                                    {
                                        chargeableRateInfo.NightlyRatesPerRoom = x.RateInfos[0].Rooms
                                        .SelectMany(xx => xx.ChargeableNightlyRates.Select(d => new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.NightlyRate { baseRate = d.baseRate, rate = d.rate, promo = d.promo })).ToArray();
                                    }

                                    decimal taxAvgPerRoom = (chargeableRateInfo.surchargeTotal.ToDecimal() / hotel.RoomDetails.Count(c => c.RateCode == roomSelected.RateCode && c.RoomTypeCode == roomSelected.RoomTypeCode)).RoundToDecimalPlace();
                                    decimal reQuoteRoomTotalBaseRate = chargeableRateInfo.NightlyRatesPerRoom.Sum(s => s.rate.ToDecimal());

                                    bool tokenExists = x.roomTypeCode == roomSelected.RoomTypeCode;

                                    bool rateExists = ((roomSelected.TotalBaseRate) >= reQuoteRoomTotalBaseRate) &&
                                    (roomSelected.TotalTaxAndServices_Source >= taxAvgPerRoom);

                                    bool roomDescSame = (roomSelected.RoomTypeName == x.description) &&
                                    (!string.IsNullOrWhiteSpace(x.rateDescription) && roomSelected.RateDesc == x.rateDescription);

                                    bool isSameNonRefundable = roomSelected.NonRefundable == x.RateInfos[0].nonRefundable;

                                    bool isRecordMatch = (tokenExists || roomDescSame) && rateExists && isSameNonRefundable;

                                    if ((!rateExists && roomDescSame) || isRecordMatch)
                                    {
                                        BookingLogMsg.AppendLine(string.Format("!!! Hotel ID : [{0}] - {1}", booking.SupplierCode, roomSelected.HotelId));

                                        if (!rateExists && roomDescSame)
                                        {
                                            BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] found, but rate different with original booked amount.", x.rateDescription));

                                            if (!isSameNonRefundable)
                                                BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] cancel policy different.", x.rateDescription))
                                                .AppendLine(string.Format("!!! Customer Policy : [{0}]", roomSelected.NonRefundable))
                                                .AppendLine(string.Format("!!! Latest Policy : [{0}]", x.RateInfos[0].nonRefundable));
                                        }

                                        BookingLogMsg.AppendLine()
                                        .AppendLine(string.Format("!!! Customer Rate : [{0}]", roomSelected.TotalBaseRate_Source))
                                        .AppendLine(string.Format("!!! Latest Rate : [{0}]", reQuoteRoomTotalBaseRate))
                                        .AppendLine();
                                    }

                                    return isRecordMatch;
                                });

                                if (_roomQuery != null)
                                    roomSelected.RateKey = _roomQuery.RateInfos[0].Rooms[0].rateKey;
                                else
                                    BookingLogMsg.AppendLine(string.Format("!!! Latest room types doesn't match with original room types."));
                            }
                        }
                        else if (roomResult?.Errors?.ErrorMessage != null)
                        {
                            string errorMsg = "!!! Error when regenerate rateKey (TourPlan)" + JsonConvert.SerializeObject(roomResult.Errors, Formatting.Indented);
                            logger.Info(errorMsg);
                            BookingLogMsg.AppendLine(errorMsg);
                            exList.Add(new Exception(errorMsg));
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            string errorMsg = "!!! Exception on TourPlan Reserve Service - SuperPNRNo: " + booking.SuperPNRNo;
                            sb.AppendLine(errorMsg);
                            logger.Info(ex, errorMsg);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }
                }

                #endregion

                #region Call to Reserve Booking
                try
                {
                    reserveResultAll = await sendBookHotelRequest(product);

                    try
                    {
                        BookingLogMsg.AppendLine("Reserve Respond: ")
                            .AppendLine(JsonConvert.SerializeObject(reserveResultAll))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Reserve Respond: ")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    isAllReserveSuccess = !reserveResultAll.Any(x => x.ErrorReply != null || (x.AddServiceReply != null && x.AddServiceReply.Status != "OK"));
                    isAllReserveFail = !isAllReserveSuccess && reserveResultAll.Count(x => x.AddServiceReply != null && x.AddServiceReply.Status != "OK") == reserveResultAll.Count || (!isAllReserveSuccess && reserveResultAll.Count(x => x.ErrorReply != null) == reserveResultAll.Count);
                    isPartialSuccess = (!isAllReserveSuccess && reserveResultAll.Count(x => x.AddServiceReply == null && x.AddServiceReply.Status != "OK") > 0 && reserveResultAll.Count(x => x.AddServiceReply.Status != "OK") < reserveResultAll.Count) || (!isAllReserveSuccess && (reserveResultAll.Count(x => x.ErrorReply == null) > 0 && reserveResultAll.Count(x => x.ErrorReply == null) < reserveResultAll.Count));

                    reserveResultSucceed = reserveResultAll.Count(x => x.Errors == null && x.ErrorReply == null) == 0 ? new List<Reply4>() : reserveResultAll.Where(x => x.Errors == null && x.ErrorReply == null).ToList();

                    #region Action - Reserve Result Error/Fail
                    if (reserveResultAll.Any(x => x.ErrorReply != null))
                    {
                        isNoError = false;
                        string errorMsg = string.Format("Reserve {2} HOTEL fail in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine().AppendLine(errorMsg).AppendLine();

                        var error = reserveResultAll.Select(x => x.ErrorReply).Where(x => x != null);
                        foreach (var item in error)
                        {
                            errorLog.Add(new ProductReserve.Error
                            {
                                ErrorMsg = item.Error
                            });

                            sb.AppendLine();
                            sb.AppendLine("Reserve room fail from Tourplan Services:");
                            sb.AppendFormat("{0,-25}: {1}", "Error Code", item.Error).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        }

                        sb.AppendLine().AppendLine();
                    }
                    else if (reserveResultAll.Any(x => x.AddServiceReply.Status != "OK"))
                    {
                        isNoError = false;

                        var error = reserveResultAll.Select(x => x.AddServiceReply).Where(x => x.Status != "OK");
                        foreach (var item in error)
                        {
                            sb.AppendLine();
                            sb.AppendLine("Book Hotel room fail from Tourplan Services:");
                            sb.AppendFormat("{0,-25}: {1}", "Booking Status", item.Status).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        }

                        sb.AppendLine().AppendLine();
                    }
                    else
                    {
                        string errorMsg = string.Format("Reserve {2} HOTEL Success in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine(errorMsg);
                    }
                    #endregion
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on Tourplan Reserve Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                List<SearchBookingReply> itineraryResponse = new List<SearchBookingReply>();
                string hotelPhoneNo = null; // assign while update roompaxhotel itinerary number

                #region Update correctly Itinerary Number base on RoomTypeCode
                try
                {
                    foreach (var item in reserveResultSucceed.Where(x => x.AddServiceReply != null).Select(x => x.AddServiceReply))
                    {
                        bookReturnStatus.Add(string.Format("Tourplan ID: {0} - [{1}]", item.BookingId, item.Status));
                        var res = await GetReservedItinerary(item.BookingId);
                        if (res != null)
                        {
                            itineraryResponse.Add(res);
                        }
                    }

                    try
                    {
                        BookingLogMsg.AppendLine("Itinerary Respond:")
                            .AppendLine(JsonConvert.SerializeObject(itineraryResponse))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Itinerary Respond:")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    if (itineraryResponse.Any(x => x.ErrorReply != null || (x.GetBookingReply != null && x.GetBookingReply.Services.Service.Status != "OK")))
                    {
                        isGetItineraryError = true;
                        isNoError = false;

                        var error = itineraryResponse.Select(x => x.ErrorReply).Where(x => x != null).ToList();
                        foreach (var item in error)
                        {
                            sb.AppendLine().AppendLine();
                            sb.AppendLine("Get Itinerary fail from Tourplan Services:").AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Message", item.Error).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        }

                        sb.AppendLine().AppendLine();
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on Tourplan Get Itinerary Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Update RoomPaxHotel Itinerary Number (By Get Itinerary Respond)
                try
                {
                    bool allFailed = itineraryResponse.Count == itineraryResponse.Count(x => x.Errors != null || x.GetBookingReply == null || x.ErrorReply != null
                    || (x.GetBookingReply != null && x.GetBookingReply.Services.Service.Status != "OK"));

                    var itineraryRespList = allFailed ? new List<SearchBookingReply>() : itineraryResponse;
                    var roomPaxHotelWithoutContact = booking.RoomPaxHotels.Where(x => x.IsContactPerson.HasValue && !x.IsContactPerson.Value);

                    for (int i = 0; i < roomPaxHotelWithoutContact.Count(); i++)
                    {
                        var roomPaxHotel = roomPaxHotelWithoutContact.AsEnumerable().ToArray()[i];
                        var queryItinerarySelected = itineraryRespList.FirstOrDefault(x => x.ErrorReply == null && x.Errors == null &&
                        x.GetBookingReply != null && x.GetBookingReply.Services.Service.Opt == roomPaxHotel.RateCode);

                        if (queryItinerarySelected == null)
                        {
                            exList.Add(new Exception("Cannot found booking on Tourplan - itineraryRespList. (HotelController.cs [CheckoutReserveRoomTP])" +
                                Environment.NewLine + "RoomPax ID: " + roomPaxHotel.RoomPaxID +
                                Environment.NewLine + "Opt Code: " + roomPaxHotel.RateCode // OptCode usage
                                ));
                        }
                        else
                        {
                            var queryItineraryResp = queryItinerarySelected.GetBookingReply.Services.Service.RoomConfigs.RoomConfig.Where(x => x.RoomType == roomPaxHotel.RoomTypeCode);
                            int counter = 0;
                            foreach (var bookedRoomSelected in queryItineraryResp)
                            {
                                var header = queryItinerarySelected;
                                var roomPaxHotelInner = roomPaxHotelWithoutContact.ToArray()[i];

                                hotelPhoneNo = hotelPhoneNo == null && header != null ? "" : hotelPhoneNo;
                                roomPaxHotelInner.ItineraryNumber = header.GetBookingReply.BookingId;
                                roomPaxHotelInner.RoomConfirmationNumber = header.GetBookingReply.Ref;
                                roomPaxHotelInner.RoomTypeCode = bookedRoomSelected.RoomType;
                                roomPaxHotelInner.CheckInDateTime = header.GetBookingReply.Services?.Service?.Pickup_Date?.ToDateTime() ?? roomPaxHotelInner.CheckInDateTime;
                                roomPaxHotelInner.CheckOutDateTime = header.GetBookingReply.Services?.Service?.Dropoff_Date?.ToDateTime() ?? roomPaxHotelInner.CheckOutDateTime;
                                roomPaxHotelInner.ReservationStatusCode = header.GetBookingReply.Services.Service.Status;

                                if (counter < queryItineraryResp.Count() - 1)
                                {
                                    counter++;
                                    i++;
                                }
                            }
                        }
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Tourplan Reserved Hotel Room Pax Error - " + DateTime.Now.ToLoggerDateTime() + " - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Update Booking Header
                try
                {
                    isNoError = reserveResultSucceed != null && reserveResultSucceed.FirstOrDefault() != null && reserveResultSucceed.FirstOrDefault().AddServiceReply != null ? true : false;

                    // 2017/07/08 - Mayday Hotfix, caused not update booking status
                    string selectedHotelId = hotel.RoomDetails.FirstOrDefault().HotelId;
                    Func<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation, bool> hotelIdCheck = (x => x.hotelId == selectedHotelId);

                    Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse HotelInfo = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse();
                    List<string> hotelIDs = new List<string>();
                    hotelIDs.Insert(0, selectedHotelId);
                    if (hotel.SearchHotelInfo.BundleType() == BundleTypes.TPConcert)
                    {
                        HotelInfo = await getHotelFromEBSSearchModelTP(hotel.SearchHotelInfo, hotelIDs);
                    }
                    else
                    {
                        hotel.SearchHotelInfo.SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
                        {
                            Expedia = false,
                            Tourplan = true,
                            JacTravel = false,
                            HotelBeds = false,
                        };
                        HotelInfo = ESBHotelServiceCall.GetHotelList(hotel.SearchHotelInfo, hotelIDs);
                    }

                    var respondHotelBooked = HotelInfo.HotelList.FirstOrDefault(hotelIdCheck);

                    if (respondHotelBooked != null && respondHotelBooked.Addresses != null)
                    {
                        var addressReflect = typeof(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.Addresses).GetProperties();
                        List<string> address = new List<string>();
                        foreach (var item in addressReflect)
                        {
                            if (item.PropertyType.Name == "String" && item.Name.Contains("address"))
                            {
                                string _address = (string)item.GetValue(respondHotelBooked.Addresses);
                                if (!string.IsNullOrWhiteSpace(_address) && !address.Any(x => x.Trim() == _address.Trim()))
                                    address.Add(_address);
                            }
                        }

                        booking.HotelStateProvinceCode = respondHotelBooked.Addresses.city;
                        booking.HotelPostalCode = respondHotelBooked.Addresses.postalCode;
                        booking.HotelCountryCode = respondHotelBooked.Addresses.countryCode;
                        booking.HotelAddress = string.Join(", ", address);
                        booking.HotelID = respondHotelBooked.hotelId;
                        booking.ShortDescription = respondHotelBooked.shortDescription;
                    }
                    booking.HotelPhoneNumber = hotelPhoneNo;

                    booking.BookingStatusCode = (sb.Length > 0 || !isNoError || exList.Count > 0) ? Enumeration.SMCBookingStatus.RHI.ToString() : "CON";
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Tourplan Reserved Hotel Header Error - " + DateTime.Now.ToLoggerDateTime());
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Reserve Error But Payment Success
                // trigger email to notice Customer Service
                if (((isGetItineraryError || isPartialSuccess) && (sb.Length > 0 || !isNoError)) ||
                    ((isAllReserveSuccess || isPartialSuccess) && exList.Count > 0) ||
                    (!isAuthCapture && (isAllReserveFail || isPartialSuccess || sb.Length > 0 || !isNoError || exList.Count > 0))
                    )
                {
                    bookingExpection = new BookingQuery.BookingExpection(booking, reserveResultAll, sb, isCrossSell);
                }
                #endregion

                if (sb.Length > 0 || !isNoError || exList.Count > 0)
                {
                    AggregateException ae = new AggregateException(exList);

                    logger.Error(ae);
                    foreach (var error in ae.InnerExceptions)
                    {
                        logger.Error(error);
                    }

                    eventLog1.WriteEntry("Tourplan HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                        BookingLogMsg.ToString() +
                        Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString()
                        , EventLogEntryType.Warning, 301);
                }
                else
                {
                    eventLog1.WriteEntry(BookingLogMsg.ToString(), EventLogEntryType.SuccessAudit, 302);
                }

                return new ProductReserve.BookingRespond
                {
                    AttachmentCollection = bookingExpection?.AttachmentCollection,
                    EmailContent = bookingExpection?.EmailContent,
                    BatchBookResult = isAllReserveSuccess ? ProductReserve.BookResultType.AllSuccess :
                    (isPartialSuccess && !isAllReserveFail ? ProductReserve.BookResultType.PartialSuccess : ProductReserve.BookResultType.AllFail),
                    SuperPNRNo = booking.SuperPNRNo,
                    ErrorLog = errorLog
                };
            }
        }

        public class JacTravel
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            string clientIP = "47.88.153.210";
            string sessionId = Guid.NewGuid().ToString();
            string userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
            System.Diagnostics.EventLog eventLog1;
            private StringBuilder BookingLogMsg = new StringBuilder();

            public JacTravel()
            {
                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "MayflowerSource", "MayflowerHotelLog");
                }
                eventLog1.Source = "MayflowerSource";
                eventLog1.Log = "MayflowerHotelLog";
            }

            private Task<PreBookResponse> sendPreBookHotelRequest(CheckoutProduct checkoutModel)
            {
                var BookHotelRespond = Task<PreBookResponse>.Factory.StartNew(() =>
                {
                    return JacTravelServiceCall.PreBookHotelRooms(checkoutModel);
                });

                return BookHotelRespond;
            }

            private Task<BookResponse> sendBookHotelRequest(CheckoutProduct checkoutModel)
            {
                var BookHotelRespond = Task<BookResponse>.Factory.StartNew(() =>
                {
                    return JacTravelServiceCall.BookHotelRooms(checkoutModel);
                });

                return BookHotelRespond;
            }

            private Task<BookingResponse> GetReservedItinerary(string itineraryID)
            {
                GetItineraryModel itineraryModel = new GetItineraryModel();
                itineraryModel.ItineraryID = itineraryID;

                return Task<BookingResponse>.Factory.StartNew(() =>
                {
                    return JacTravelServiceCall.GetBooking(itineraryModel);
                });
            }

            /// <summary>
            /// 1 - All Booking Success
            /// 2 - All Booking Fail
            /// 3 - Partial Sucess
            /// </summary>
            /// <param name="token"></param>
            /// <param name="bookStatus"></param>
            /// <param name="isAuthCapture"></param>
            /// <param name="isCrossSell">For clearly indicate is cross sell when email reserve hotel related error.</param>
            public async Task<ProductReserve.BookingRespond> CheckoutReserveRoom(int bookingId, string bookStatus,
                string hostURL, MayFlower db, bool isAuthCapture = false, bool isCrossSell = false)
            {
                BookingHotel booking = await db.BookingHotels.FindAsync(bookingId);

                BookingLogMsg.Append(string.Format("SuperPNR : {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, DateTime.Now.ToLoggerDateTime()))
                    .AppendLine().AppendLine();

                var hotel = booking.ToProductHotel(clientIP, userAgent);

                List<ProductReserve.Error> errorLog = new List<ProductReserve.Error>();
                CheckoutProduct product = new CheckoutProduct();
                product.ContactPerson = hotel.ContactPerson;
                product.SuperPNRNo = booking.SuperPNRNo;
                product.InsertProduct(hotel);

                StringBuilder sb = new StringBuilder(); // use for append error message
                bool isNoError = true; // use for indicate any error then return to call
                bool isAllReserveSuccess = false;
                bool isPartialSuccess = false;
                bool isGetItineraryError = false;
                bool isAllReserveFail = false;
                List<Exception> exList = new List<Exception>();
                BookResponse reserveResultAll = new BookResponse();
                BookResponse reserveResultSucceed = new BookResponse();
                PropertyDetailsResponse HotelInfo = new PropertyDetailsResponse();
                BookingExpection bookingExpection = null;

                #region Regenerate RateKey for Booking
                if (hotel.RoomDetails.Any(x => x.RateKey == null || x.RateKey == "" || x.RateKey == "0"))
                {
                    try
                    {
                        var hotelListReq = hotel.SearchHotelInfo;
                        var oldSearchRoomModel = new SearchRoomModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            CurrencyCode = hotelListReq.CurrencyCode,
                            CustomerIpAddress = hotelListReq.CustomerIpAddress,
                            CustomerSessionId = hotelListReq.CustomerSessionId,
                            CustomerUserAgent = hotelListReq.CustomerUserAgent,
                            HotelID = booking.HotelID,
                        };
                        var oldSearchHotelModel = new SearchHotelModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            NoOfAdult = hotelListReq.NoOfAdult,
                            NoOfInfant = hotelListReq.NoOfInfant,
                            NoOfRoom = hotelListReq.NoOfRoom,
                        };
                        var roomResult = await ESBHotelServiceCall.GetRoomAvailabilityAsync(oldSearchRoomModel, oldSearchHotelModel, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.JacTravel);

                        if (roomResult != null && roomResult.HotelRoomInformationList != null)
                        {
                            var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);

                            // Local Debug use Code
                            //var priceList = roomList.SelectMany(x => x.RateInfos.Select(s => s.chargeableRateInfo_source.total));
                            //var roomCodeList = roomList.Select(x => new { x.rateCode, x.roomTypeCode, x.roomTypeId });
                            //var roomCodeList2 = roomList.Select(x => new { x.jacTravelBookingToken, x.jacTravelMealBasis, x.jacTravelMealBasisID, x.jacTravelPropertyRoomTypeID });

                            foreach (var roomSelected in hotel.RoomDetails)
                            {
                                var _roomQuery = roomList.FirstOrDefault(x =>
                                {
                                    bool tokenExists = x.roomTypeCode == roomSelected.RoomTypeCode
                                    || x.jacTravelBookingToken == roomSelected.RoomTypeCode || x.jacTravelPropertyRoomTypeID == roomSelected.RoomTypeCode
                                    || (x.jacTravelBookingToken != null && x.jacTravelBookingToken.StartsWith(roomSelected.RoomTypeCode));

                                    bool rateExists = ((roomSelected.TotalBaseRate + roomSelected.TotalTaxAndServices) >= x.RateInfos[0].chargeableRateInfo.total.ToDecimal())
                                    || (roomSelected.TotalBaseRate_Source + roomSelected.TotalTaxAndServices_Source) >= (x.RateInfos[0].chargeableRateInfo_source?.total.ToDecimalNullable() ?? x.RateInfos[0].chargeableRateInfo.total.ToDecimal());

                                    bool roomDescSame = roomSelected.RoomTypeName == x.description;

                                    bool isSameNonRefundable = roomSelected.NonRefundable == x.RateInfos[0].nonRefundable;

                                    bool isRecordMatch = (tokenExists || roomDescSame) && rateExists && isSameNonRefundable;

                                    if ((!rateExists && roomDescSame) || isRecordMatch)
                                    {
                                        BookingLogMsg.AppendLine(string.Format("!!! Hotel ID : [{0}] - {1}", booking.SupplierCode, roomSelected.HotelId));

                                        if (!rateExists && roomDescSame)
                                        {
                                            BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] found, but rate different with original booked amount.", x.rateDescription));

                                            if (!isSameNonRefundable)
                                                BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] cancel policy different.", x.rateDescription))
                                                .AppendLine(string.Format("!!! Customer Policy : [{0}]", roomSelected.NonRefundable))
                                                .AppendLine(string.Format("!!! Latest Policy : [{0}]", x.RateInfos[0].nonRefundable));
                                        }

                                        BookingLogMsg.AppendLine()
                                        .AppendLine(string.Format("!!! Customer Rate : [{0}]", roomSelected.TotalBaseRate_Source + roomSelected.TotalTaxAndServices_Source))
                                        .AppendLine(string.Format("!!! Latest Rate : [{0}]", x.RateInfos[0].chargeableRateInfo_source?.total ?? x.RateInfos[0].chargeableRateInfo.total))
                                        .AppendLine();
                                    }

                                    return isRecordMatch;
                                });

                                if (_roomQuery != null)
                                {
                                    // JacTravel Service Call user RoomTypeCode to book instead of RateKey
                                    roomSelected.RoomTypeCode = string.IsNullOrWhiteSpace(_roomQuery.jacTravelBookingToken) ?
                                        roomSelected.RoomTypeCode : _roomQuery.jacTravelBookingToken;
                                    roomSelected.RateKey = _roomQuery.RateInfos[0].Rooms[0].rateKey;
                                    roomSelected.MealBasisCode = _roomQuery.jacTravelMealBasisID;
                                    hotel.RoomAvailabilityResponse = roomResult;
                                }
                                else
                                    BookingLogMsg.AppendLine(string.Format("!!! Latest room types doesn't match with original room types."));
                            }
                            //product.InsertProduct(hotel);
                        }
                        else if (roomResult?.Errors?.ErrorMessage != null)
                        {
                            string errorMsg = "!!! Error when regenerate rateKey (JacTravel)" + JsonConvert.SerializeObject(roomResult.Errors, Formatting.Indented);
                            logger.Info(errorMsg);
                            BookingLogMsg.AppendLine(errorMsg);
                            exList.Add(new Exception(errorMsg));
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            string errorMsg = "!!! Exception on JacTravel Reserve Service - SuperPNRNo: " + booking.SuperPNRNo;
                            sb.AppendLine(errorMsg);
                            logger.Info(ex, errorMsg);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }

                    #region Call to PreBookHotelRooms
                    bool roomEnough = false;
                    try
                    {
                        var jcRoomAvailable = await sendPreBookHotelRequest(product);
                        roomEnough = jcRoomAvailable?.ReturnStatus != null && jcRoomAvailable.ReturnStatus.Success.ToBoolean();
                        if (!roomEnough)
                        {
                            isNoError = false;
                            eventLog1.WriteEntry(string.Format("PreBookHotelRooms {2} HOTEL fail in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode), EventLogEntryType.Warning, 301);
                            sb.AppendLine();
                            sb.AppendLine("PreBookHotelRooms fail from JacTravel Services:");
                            sb.AppendFormat("{0,-25}: {1}", "Error Message", (jcRoomAvailable.Errors != null ? reserveResultAll.Errors.ErrorMessage : "")).AppendLine();
                            if (jcRoomAvailable.ReturnStatus != null)
                            {
                                sb.AppendFormat("{0,-25}: {1}", "Return Status Exception", jcRoomAvailable.ReturnStatus.Exception).AppendLine();
                                sb.AppendFormat("{0,-25}: {1}", "Return Status of booking", jcRoomAvailable.ReturnStatus.Success).AppendLine();
                            }
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();

                            sb.AppendLine().AppendLine();
                        }
                        else
                        {
                            hotel.PreBookToken = jcRoomAvailable.PreBookingToken;
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            logger.Info(ex, "Exception on JacTravel PreBookHotelRooms Service - SuperPNRNo: " + booking.SuperPNRNo);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }
                    #endregion
                }
                #endregion

                #region Call to Reserve Booking
                try
                {
                    reserveResultAll = await sendBookHotelRequest(product);
                    isAllReserveSuccess = (reserveResultAll != null && reserveResultAll.Errors == null && reserveResultAll.ReturnStatus != null && reserveResultAll.ReturnStatus.Exception == "" && reserveResultAll.ReturnStatus.Success == "true");
                    isAllReserveFail = !isAllReserveSuccess;
                    isPartialSuccess = false;
                    reserveResultSucceed = reserveResultAll == null ? new BookResponse() : reserveResultAll;

                    try
                    {
                        BookingLogMsg.AppendLine("Reserve Respond: ")
                            .AppendLine(JsonConvert.SerializeObject(reserveResultAll))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Reserve Respond: ")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    #region Action - Reserve Result Error/Fail
                    if (reserveResultAll?.Errors != null || reserveResultAll?.ReturnStatus?.Exception != "" || reserveResultAll?.ReturnStatus?.Success == "false")
                    {
                        errorLog.Add(new ProductReserve.Error
                        {
                            ErrorMsg = reserveResultAll?.Errors?.ErrorMessage + Environment.NewLine
                                        + reserveResultAll?.ReturnStatus?.Exception,
                            ErrorCategory = reserveResultAll?.Errors?.Category,
                        });

                        isNoError = false;

                        string errorMsg = string.Format("Reserve {2} HOTEL fail in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine().AppendLine(errorMsg).AppendLine();

                        sb.AppendLine();
                        sb.AppendLine("Reserve room fail from JacTravel Services:");
                        sb.AppendFormat("{0,-25}: {1}", "Error Message", (reserveResultAll?.Errors != null ? reserveResultAll.Errors.ErrorMessage : "")).AppendLine();
                        if (reserveResultAll?.ReturnStatus != null)
                        {
                            sb.AppendFormat("{0,-25}: {1}", "Return Status Exception", reserveResultAll.ReturnStatus.Exception).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Return Status of booking", reserveResultAll.ReturnStatus.Success).AppendLine();
                        }
                        sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();

                        sb.AppendLine().AppendLine();
                    }
                    else
                    {
                        string errorMsg = string.Format("Reserve {2} HOTEL Success in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine(errorMsg);
                    }
                    #endregion
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on JacTravel Reserve Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion
                BookingResponse itineraryResponse = new BookingResponse();
                string hotelPhoneNo = null; // assign while update roompaxhotel itinerary number

                #region Update correctly Itinerary Number base on RoomTypeCode
                if (!string.IsNullOrWhiteSpace(reserveResultSucceed.BookingReference)) // If booking fail no need to call GetItinerary from API Service
                {
                    try
                    {
                        var res = await GetReservedItinerary(reserveResultSucceed.BookingReference);
                        if (res != null)
                        {
                            itineraryResponse = res;
                        }

                        try
                        {
                            BookingLogMsg.AppendLine("Itinerary Respond:")
                                .AppendLine(JsonConvert.SerializeObject(itineraryResponse))
                                .AppendLine();
                        }
                        catch (Exception ex)
                        {
                            BookingLogMsg.AppendLine()
                                .AppendLine("Error when serialilze Itinerary Respond:")
                                .AppendLine(ex.ToString())
                                .AppendLine();
                        }

                        GetHotelInformationModel GetHotelInfomodel = new GetHotelInformationModel();
                        GetHotelInfomodel.JacTravelPropertyID = hotel.TravelPropertyID;

                        HotelInfo = JacTravelServiceCall.GetPropertyDetails(GetHotelInfomodel);

                        if (itineraryResponse.Errors != null || itineraryResponse.ReturnStatus.Exception != "")
                        {
                            isGetItineraryError = true;
                            isNoError = false;

                            sb.AppendLine().AppendLine();
                            sb.AppendLine("Get Itinerary fail from JacTravel Services:").AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Error Message", (itineraryResponse.Errors != null ? itineraryResponse.Errors.ErrorMessage : "")).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Return Status Exception", itineraryResponse.ReturnStatus != null ? itineraryResponse.ReturnStatus.Exception : "").AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                            sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();

                            sb.AppendLine().AppendLine();
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            logger.Info(ex, "Exception on JacTravel Get Itinerary Service - SuperPNRNo: " + booking.SuperPNRNo);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }
                }

                #endregion

                #region Update RoomPaxHotel Itinerary Number (By Get Itinerary Respond)
                if (!string.IsNullOrWhiteSpace(reserveResultSucceed.BookingReference))
                {
                    try
                    {
                        bool allFailed = itineraryResponse.Errors != null;

                        var itineraryRespLists = allFailed ? new BookingResponse() : itineraryResponse;
                        var roomPaxHotelWithoutContact = booking.RoomPaxHotels.Where(x => x.IsContactPerson.HasValue && !x.IsContactPerson.Value);

                        for (int i = 0; i < roomPaxHotelWithoutContact.Count(); i++)
                        {
                            var roomPaxHotel = roomPaxHotelWithoutContact.ToArray()[i];
                            var queryItineraryResps = itineraryRespLists.Properties.Property.Rooms.Room.Where(x => x.RoomType == roomPaxHotel.RoomTypeDescription);

                            foreach (var queryItineraryResp in queryItineraryResps)
                            {
                                hotelPhoneNo = hotelPhoneNo == null && itineraryRespLists != null ? "" : hotelPhoneNo;
                                var roomPaxHotelInner = roomPaxHotelWithoutContact.ToArray()[i];
                                roomPaxHotelInner.ItineraryNumber = itineraryRespLists.BookingReference;
                                roomPaxHotelInner.RoomConfirmationNumber = itineraryRespLists.BookingReference;
                                roomPaxHotelInner.RoomTypeCode = roomPaxHotel.RoomTypeCode;
                                roomPaxHotelInner.CheckInDateTime = hotel.SearchHotelInfo.ArrivalDate;
                                roomPaxHotelInner.CheckOutDateTime = hotel.SearchHotelInfo.DepartureDate;
                                roomPaxHotelInner.ReservationStatusCode = itineraryRespLists.ReturnStatus.Success;
                                roomPaxHotelInner.SpecialCheckInInstructions = "";
                                roomPaxHotelInner.ProcessedWithConfirmation = (bool?)null;
                                roomPaxHotelInner.CheckInInstructions = "";
                            }
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            logger.Info(ex, "Update Reserved Hotel Room Pax Error - " + DateTime.Now.ToLoggerDateTime() + " - SuperPNRNo: " + booking.SuperPNRNo);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }
                }
                #endregion

                #region Update Booking Header
                try
                {
                    isNoError = reserveResultSucceed != null && reserveResultSucceed.ReturnStatus != null && reserveResultSucceed.ReturnStatus.Success == "true" ? true : false;

                    var respondHotelBooked = isNoError ? HotelInfo : null;
                    if (isNoError && respondHotelBooked != null)
                    {
                        booking.HotelStateProvinceCode = respondHotelBooked.TownCity;
                        booking.HotelAddress = respondHotelBooked.Address1 + (respondHotelBooked.Address2 != null ? ", " + respondHotelBooked.Address2 : "");
                        booking.HotelPostalCode = respondHotelBooked.Postcode;
                        booking.HotelCountryCode = UtilitiesService.GetCountryCode(respondHotelBooked.Country) == string.Empty ? respondHotelBooked.Country.Substring(0, 3) : UtilitiesService.GetCountryCode(respondHotelBooked.Country);
                        booking.HotelPhoneNumber = respondHotelBooked.Telephone;
                    }

                    booking.BookingStatusCode = (sb.Length > 0 || !isNoError || exList.Count > 0) ? Enumeration.SMCBookingStatus.RHI.ToString() : "CON";
                }
                catch (AggregateException ae)
                {
                    logger.Info(ae.GetBaseException(), "Update Reserved Hotel Header Error - " + DateTime.Now.ToLoggerDateTime());
                    exList.Add(ae.GetBaseException());
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Reserve Error But Payment Success
                // trigger email to notice Customer Service
                if (((isGetItineraryError || isPartialSuccess) && (sb.Length > 0 || !isNoError)) ||
                    ((isAllReserveSuccess || isPartialSuccess) && exList.Count > 0) ||
                    (!isAuthCapture && (isAllReserveFail || isPartialSuccess || sb.Length > 0 || !isNoError || exList.Count > 0))
                    )
                {
                    bookingExpection = new BookingExpection(booking, reserveResultAll, sb, isCrossSell);
                }
                #endregion

                if (sb.Length > 0 || !isNoError || exList.Count > 0)
                {
                    AggregateException ae = new AggregateException(exList);
                    logger.Error(ae.GetBaseException(), sb.ToString());

                    eventLog1.WriteEntry("JacTravel HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                        BookingLogMsg.ToString() +
                        Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString() +
                        Environment.NewLine + Environment.NewLine + sb
                        , EventLogEntryType.Warning, 301);
                }
                else
                {
                    eventLog1.WriteEntry(BookingLogMsg.ToString(), EventLogEntryType.SuccessAudit, 302);
                }

                return new ProductReserve.BookingRespond
                {
                    AttachmentCollection = bookingExpection?.AttachmentCollection,
                    EmailContent = bookingExpection?.EmailContent,
                    BatchBookResult = isAllReserveSuccess ? ProductReserve.BookResultType.AllSuccess :
                    (isPartialSuccess && !isAllReserveFail ? ProductReserve.BookResultType.PartialSuccess : ProductReserve.BookResultType.AllFail),
                    SuperPNRNo = booking.SuperPNRNo,
                    ErrorLog = errorLog,
                };
            }
        }

        public class HotelBeds
        {
            ItineraryLog ItineraryLog { get; set; }
            Logger logger = LogManager.GetCurrentClassLogger();
            string clientIP = "47.88.153.210";
            string sessionId = Guid.NewGuid().ToString();
            string userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
            System.Diagnostics.EventLog eventLog1;
            private StringBuilder BookingLogMsg = new StringBuilder();

            public HotelBeds()
            {
                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "MayflowerSource", "MayflowerHotelLog");
                }
                eventLog1.Source = "MayflowerSource";
                eventLog1.Log = "MayflowerHotelLog";
                ItineraryLog = ItineraryLog ?? new ItineraryLog(eventLog1);
            }

            private Task<BookingDetailRS> GetReservedItinerary(string itineraryID)
            {
                GetItineraryModel itineraryModel = new GetItineraryModel();
                itineraryModel.ItineraryID = itineraryID;

                return Task<BookingDetailRS>.Factory.StartNew(() =>
                {
                    return HotelBedServiceCall.DetailBook(itineraryModel);
                });
            }

            /// <summary>
            /// 1 - All Booking Success
            /// 2 - All Booking Fail
            /// 3 - Partial Sucess
            /// </summary>
            /// <param name="token"></param>
            /// <param name="bookStatus"></param>
            /// <param name="isAuthCapture"></param>
            /// <param name="isCrossSell">For clearly indicate is cross sell when email reserve hotel related error.</param>
            public async Task<ProductReserve.BookingRespond> CheckoutReserveRoom(int bookingId, string bookStatus,
                string hostURL, MayFlower db, bool isAuthCapture = false, bool isCrossSell = false)
            {
                BookingHotel booking = await db.BookingHotels.FindAsync(bookingId);

                BookingLogMsg.Append(string.Format("SuperPNR : {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, DateTime.Now.ToLoggerDateTime()))
                    .AppendLine().AppendLine();

                var hotel = booking.ToProductHotel(clientIP, userAgent);

                List<ProductReserve.Error> errorLog = new List<ProductReserve.Error>();
                CheckoutProduct product = new CheckoutProduct();
                product.ContactPerson = hotel.ContactPerson;
                product.SuperPNRNo = booking.SuperPNRNo;
                product.InsertProduct(hotel);

                StringBuilder sb = new StringBuilder(); // use for append error message
                bool isNoError = true; // use for indicate any error then return to call
                bool isAllReserveSuccess = false;
                bool isPartialSuccess = false;
                bool isGetItineraryError = false;
                bool isAllReserveFail = false;
                List<Exception> exList = new List<Exception>();
                BookingRS reserveResultAll = new BookingRS();
                BookingRS reserveResultSucceed = new BookingRS();
                HotelDetailsRS HotelInfo = new HotelDetailsRS();
                BookingQuery.BookingExpection bookingExpection = null;
                string checkrateserr = "";

                #region Regenerate RateKey for Booking
                if (hotel.RoomDetails.Any(x => x.RateKey == null || x.RateKey == ""))
                {
                    try
                    {
                        var hotelListReq = hotel.SearchHotelInfo;
                        var oldSearchRoomModel = new SearchRoomModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            CurrencyCode = hotelListReq.CurrencyCode,
                            CustomerIpAddress = hotelListReq.CustomerIpAddress,
                            CustomerSessionId = hotelListReq.CustomerSessionId,
                            CustomerUserAgent = hotelListReq.CustomerUserAgent,
                            HotelID = booking.HotelID,
                        };
                        var oldSearchHotelModel = new SearchHotelModel
                        {
                            ArrivalDate = hotelListReq.ArrivalDate,
                            DepartureDate = hotelListReq.DepartureDate,
                            NoOfAdult = hotelListReq.NoOfAdult,
                            NoOfInfant = hotelListReq.NoOfInfant,
                            NoOfRoom = hotelListReq.NoOfRoom,
                        };
                        var roomResult = await ESBHotelServiceCall.GetRoomAvailabilityAsync(oldSearchRoomModel, oldSearchHotelModel, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.HotelBeds);

                        if (roomResult != null && roomResult.HotelRoomInformationList != null)
                        {
                            var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);
                            foreach (var roomSelected in hotel.RoomDetails)
                            {
                                var _roomQuery = roomList.FirstOrDefault(x =>
                                {
                                    var chargeableRateInfo = x.RateInfos[0].chargeableRateInfo_source ?? x.RateInfos[0].chargeableRateInfo;

                                    if (chargeableRateInfo.NightlyRatesPerRoom == null)
                                    {
                                        chargeableRateInfo.NightlyRatesPerRoom = x.RateInfos[0].Rooms
                                        .SelectMany(xx => xx.ChargeableNightlyRates.Select(d => new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.NightlyRate { baseRate = d.baseRate, rate = d.rate, promo = d.promo })).ToArray();
                                    }

                                    decimal taxAvgPerRoom = (chargeableRateInfo.surchargeTotal.ToDecimal() / hotel.RoomDetails.Count(c => c.RateCode == roomSelected.RateCode && c.RoomTypeCode == roomSelected.RoomTypeCode)).RoundToDecimalPlace();
                                    decimal reQuoteRoomTotalBaseRate = chargeableRateInfo.NightlyRatesPerRoom.Sum(s => s.rate.ToDecimal());

                                    bool tokenExists = x.roomTypeCode == roomSelected.RoomTypeCode;

                                    bool rateExists = ((roomSelected.TotalBaseRate) >= reQuoteRoomTotalBaseRate) &&
                                    (roomSelected.TotalTaxAndServices_Source >= taxAvgPerRoom);

                                    bool roomDescSame = (roomSelected.RoomTypeName == x.description) ||
                                    (!string.IsNullOrWhiteSpace(x.rateDescription) && roomSelected.RateDesc == x.rateDescription);

                                    bool isSameNonRefundable = roomSelected.NonRefundable == x.RateInfos[0].nonRefundable;

                                    bool isRecordMatch = (tokenExists || roomDescSame) && rateExists && isSameNonRefundable;

                                    if ((!rateExists && roomDescSame) || isRecordMatch)
                                    {
                                        BookingLogMsg.AppendLine(string.Format("!!! Hotel ID : [{0}] - {1}", booking.SupplierCode, roomSelected.HotelId));

                                        if (!rateExists && roomDescSame)
                                        {
                                            BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] found, but rate different with original booked amount.", x.rateDescription));

                                            if (!isSameNonRefundable)
                                                BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] cancel policy different.", x.rateDescription))
                                                .AppendLine(string.Format("!!! Customer Policy : [{0}]", roomSelected.NonRefundable))
                                                .AppendLine(string.Format("!!! Latest Policy : [{0}]", x.RateInfos[0].nonRefundable));
                                        }

                                        BookingLogMsg.AppendLine()
                                        .AppendLine(string.Format("!!! Customer Rate : [{0}]", roomSelected.TotalBaseRate_Source))
                                        .AppendLine(string.Format("!!! Latest Rate : [{0}]", reQuoteRoomTotalBaseRate))
                                        .AppendLine();
                                    }

                                    return isRecordMatch;
                                });

                                if (_roomQuery != null)
                                {
                                    roomSelected.RateKey = _roomQuery.RateInfos[0].Rooms[0].rateKey;
                                    var tpRoomAvailable = HotelBedServiceCall.GetHotelRates(roomSelected); //BookHotel will return bad request error if not check rate
                                    if (tpRoomAvailable.Errors != null && tpRoomAvailable.Hotel == null)
                                    {
                                        checkrateserr = tpRoomAvailable.Errors.ErrorMessage;
                                    }
                                }
                                else
                                {
                                    BookingLogMsg.AppendLine(string.Format("!!! Latest room types doesn't match with original room types."));
                                }
                            }
                        }
                        else if (roomResult?.Errors?.ErrorMessage != null)
                        {
                            string errorMsg = "!!! Error when regenerate rateKey (HotelBeds)" + JsonConvert.SerializeObject(roomResult.Errors, Formatting.Indented);
                            logger.Info(errorMsg);
                            BookingLogMsg.AppendLine(errorMsg);
                            exList.Add(new Exception(errorMsg));
                        }
                    }
                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.InnerExceptions)
                        {
                            logger.Info(ex, "Exception on HotelBeds Reserve Service - SuperPNRNo: " + booking.SuperPNRNo);
                            exList.Add(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        exList.Add(ex);
                    }
                }
                #endregion

                #region Call to Reserve Booking
                try
                {
                    reserveResultAll = HotelBedServiceCall.BookHotel(product);

                    try
                    {
                        BookingLogMsg.AppendLine("Reserve Respond: ")
                            .AppendLine(JsonConvert.SerializeObject(reserveResultAll))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Reserve Respond: ")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    isAllReserveSuccess = (reserveResultAll != null && reserveResultAll.Errors == null);
                    isAllReserveFail = !isAllReserveSuccess;
                    isPartialSuccess = false;
                    reserveResultSucceed = reserveResultAll == null ? new BookingRS() : reserveResultAll;

                    #region Action - Reserve Result Error/Fail
                    if (reserveResultAll.Errors != null)
                    {
                        errorLog.Add(new ProductReserve.Error
                        {
                            ErrorMsg = reserveResultAll.Errors?.ErrorMessage + Environment.NewLine + checkrateserr
                        });

                        isNoError = false;
                        string errorMsg = string.Format("Reserve {2} HOTEL fail in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine().AppendLine(errorMsg).AppendLine();

                        sb.AppendLine();
                        sb.AppendLine("Reserve room fail from HotelBeds Services:");
                        sb.AppendFormat("{0,-25}: {1}", "Error Message", (reserveResultAll.Errors != null ? reserveResultAll.Errors.ErrorMessage : "")).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "GetHotelRates Error", checkrateserr).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();
                        sb.AppendLine().AppendLine();
                    }
                    else
                    {
                        string errorMsg = string.Format("Reserve {2} HOTEL Success in {0} - {1}", booking.SuperPNRID, booking.SuperPNRNo, booking.SupplierCode);
                        BookingLogMsg.AppendLine(errorMsg);
                    }
                    #endregion
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on HotelBeds Reserve Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                BookingDetailRS itineraryResponse = new BookingDetailRS();
                string hotelPhoneNo = null; // assign while update roompaxhotel itinerary number

                #region Update correctly Itinerary Number base on RoomTypeCode
                try
                {
                    var res = await GetReservedItinerary(reserveResultSucceed.Booking.Reference);
                    if (res != null)
                    {
                        itineraryResponse = res;
                    }

                    try
                    {
                        BookingLogMsg.AppendLine("Itinerary Respond:")
                            .AppendLine(JsonConvert.SerializeObject(itineraryResponse))
                            .AppendLine();
                    }
                    catch (Exception ex)
                    {
                        BookingLogMsg.AppendLine()
                            .AppendLine("Error when serialilze Itinerary Respond:")
                            .AppendLine(ex.ToString())
                            .AppendLine();
                    }

                    GetHotelInformationModel GetHotelInfomodel = new GetHotelInformationModel();
                    GetHotelInfomodel.HotelID = booking.HotelID;
                    for (int i = 0; i < 3; i++)
                    {
                        HotelInfo = HotelBedServiceCall.HotelInfo(GetHotelInfomodel);
                        if (HotelInfo?.Errors == null)
                            break;
                    }

                    if (itineraryResponse.Errors != null || itineraryResponse.Booking == null || itineraryResponse.Booking.Status != "CONFIRMED")
                    {
                        isGetItineraryError = true;
                        isNoError = false;

                        sb.AppendLine().AppendLine();
                        sb.AppendLine("Get Itinerary fail from HotelBeds Services:").AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "Error Message", (itineraryResponse.Errors != null ? itineraryResponse.Errors.ErrorMessage : "")).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "Return Status Exception", itineraryResponse.Booking.Status != null ? itineraryResponse.Booking.Status : "").AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "Date Time", DateTime.Now).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "IP Address", clientIP).AppendLine();
                        sb.AppendFormat("{0,-25}: {1}", "HOST URL", hostURL).AppendLine();

                        sb.AppendLine().AppendLine();
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Exception on HotelBeds Get Itinerary Service - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }

                #endregion

                #region Update RoomPaxHotel Itinerary Number (By Get Itinerary Respond)
                try
                {
                    bool allFailed = itineraryResponse.Errors != null;

                    var itineraryRespLists = allFailed ? new BookingDetailRS() : itineraryResponse;
                    var roomPaxHotelWithoutContact = booking.RoomPaxHotels.Where(x => x.IsContactPerson.HasValue && !x.IsContactPerson.Value);

                    for (int i = 0; i < roomPaxHotelWithoutContact.Count(); i++)
                    {
                        var roomPaxHotel = roomPaxHotelWithoutContact.ToArray()[i];
                        var queryItineraryResp = itineraryRespLists.Booking != null ? itineraryRespLists.Booking.Hotel.Rooms.Room.FirstOrDefault(x => x.Code == roomPaxHotel.RoomTypeCode)?.Code : null;

                        if (queryItineraryResp == roomPaxHotel.RoomTypeCode)
                        {
                            hotelPhoneNo = hotelPhoneNo == null && itineraryRespLists != null ? "" : hotelPhoneNo;
                            var roomPaxHotelInner = roomPaxHotelWithoutContact.ToArray()[i];
                            roomPaxHotelInner.ItineraryNumber = itineraryRespLists.Booking.Reference;
                            roomPaxHotelInner.RoomConfirmationNumber = itineraryRespLists.Booking.Hotel.Supplier.VatNumber; //hotelbeds supplier VatNumber return from service for pdf use
                            roomPaxHotelInner.RoomTypeCode = itineraryRespLists.Booking.Hotel.Code;
                            roomPaxHotelInner.CheckInDateTime = hotel.SearchHotelInfo.ArrivalDate;
                            roomPaxHotelInner.CheckOutDateTime = hotel.SearchHotelInfo.DepartureDate;
                            roomPaxHotelInner.ReservationStatusCode = itineraryRespLists.Booking.Status;
                            roomPaxHotelInner.Remarks = itineraryRespLists.Booking.Hotel.Supplier.Name; //hotelbeds supplier name return from service for pdf use
                            //roomPaxHotelInner.SpecialCheckInInstructions = ""; // bookRespond.specialCheckInInstructions;
                            //roomPaxHotelInner.ProcessedWithConfirmation = (bool?)null; // string.IsNullOrWhiteSpace(bookRespond.processedWithConfirmation) ? (bool?)null : bookRespond.processedWithConfirmation.ToBoolean();
                            //roomPaxHotelInner.CheckInInstructions = ""; // bookRespond.checkInInstructions;
                        }
                    }
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Reserved Hotel Room Pax Error - " + DateTime.Now.ToLoggerDateTime() + " - SuperPNRNo: " + booking.SuperPNRNo);
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Update Booking Header
                try
                {
                    isNoError = reserveResultSucceed != null && reserveResultSucceed.Booking.Status == "CONFIRMED" ? true : false;

                    var respondHotelBooked = isNoError ? HotelInfo : null;
                    if (isNoError && respondHotelBooked.Hotel != null)
                    {
                        booking.HotelStateProvinceCode = respondHotelBooked.Hotel.City;
                        booking.HotelAddress = respondHotelBooked.Hotel.Address;
                        booking.HotelPostalCode = respondHotelBooked.Hotel.PostalCode;
                        booking.HotelCountryCode = respondHotelBooked.Hotel.Country.Code;
                        booking.HotelPhoneNumber = respondHotelBooked.Hotel.Phones.Phone.FirstOrDefault()?.PhoneNumber;
                    }

                    booking.BookingStatusCode = (sb.Length > 0 || !isNoError || exList.Count > 0) ? Enumeration.SMCBookingStatus.RHI.ToString() : "CON";
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.InnerExceptions)
                    {
                        logger.Info(ex, "Update Reserved Hotel Header Error - " + DateTime.Now.ToLoggerDateTime());
                        exList.Add(ex);
                    }
                }
                catch (Exception ex)
                {
                    exList.Add(ex);
                }
                #endregion

                #region Reserve Error But Payment Success
                // trigger email to notice Customer Service
                if (((isGetItineraryError || isPartialSuccess) && (sb.Length > 0 || !isNoError)) ||
                    ((isAllReserveSuccess || isPartialSuccess) && exList.Count > 0) ||
                    (!isAuthCapture && (isAllReserveFail || isPartialSuccess || sb.Length > 0 || !isNoError || exList.Count > 0))
                    )
                {
                    bookingExpection = new BookingQuery.BookingExpection(booking, reserveResultAll, sb, isCrossSell);
                }
                #endregion

                if (sb.Length > 0 || !isNoError || exList.Count > 0)
                {
                    AggregateException ae = new AggregateException(exList);

                    logger.Error(ae.ToString());

                    ItineraryLog.WriteEventLog("HotelBeds HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                        BookingLogMsg.ToString() +
                        Environment.NewLine + Environment.NewLine + ae.GetBaseException().ToString()
                        , EventLogEntryType.Warning, 301);
                }
                else
                {
                    ItineraryLog.WriteEventLog(BookingLogMsg.ToString(), EventLogEntryType.SuccessAudit, 302);
                }

                return new ProductReserve.BookingRespond
                {
                    AttachmentCollection = bookingExpection?.AttachmentCollection,
                    EmailContent = bookingExpection?.EmailContent,
                    BatchBookResult = isAllReserveSuccess ? ProductReserve.BookResultType.AllSuccess :
                    (isPartialSuccess && !isAllReserveFail ? ProductReserve.BookResultType.PartialSuccess : ProductReserve.BookResultType.AllFail),
                    SuperPNRNo = booking.SuperPNRNo,
                    ErrorLog = errorLog
                };
            }

        }

        public class EANRapid
        {
            Alphareds.Module.EANRapidHotels.RapidServices.HotelManagerClient hotelManagerClient { get; set; }
            Logger Logger = LogManager.GetCurrentClassLogger();
            string ClientIP = "47.88.153.210";
            string SessionId = Guid.NewGuid().ToString();
            string UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
            System.Diagnostics.EventLog EventLog1 { get; set; }
            private StringBuilder BookingLogMsg = new StringBuilder();
            ItineraryLog ItineraryLog { get; set; }

            public EANRapid()
            {
                InitEANRapidObject();
            }

            public EANRapid(string _userAgent, string _clientIP, string _sessionId)
            {
                InitEANRapidObject();
                UserAgent = _userAgent;
                ClientIP = _clientIP;
                SessionId = _sessionId;
            }

            private Task<RetrieveBookingResponse> GetReservedItinerary(string itineraryID)
            {
                string userEmail = Core.IsForStaging ? "ota.test@mayflower-group.com" : "EANB2B@mayflower-group.com";

                RetrieveBookingRequest itineraryModel = new RetrieveBookingRequest
                {
                    CustomerIp = ClientIP,
                    UserAgent = UserAgent,
                    Opt_CustomerSessionId = SessionId ?? Guid.NewGuid().ToString(),
                    ItineraryId = itineraryID,
                };

                return hotelManagerClient.RetrieveBookingAsync(itineraryModel);
            }

            public Task<RetrieveBookingWithAffiliateReferenceIdResponse> GetReservedItineraryAsync(string affiliateReferenceId)
            {
                string userEmail = Core.IsForStaging ? "ota.test@mayflower-group.com" : "EANB2B@mayflower-group.com";

                RetrieveBookingWithAffiliateReferenceIdRequest itineraryModel = new RetrieveBookingWithAffiliateReferenceIdRequest
                {
                    CustomerIp = ClientIP,
                    UserAgent = UserAgent,
                    Opt_CustomerSessionId = SessionId ?? Guid.NewGuid().ToString(),
                    Affiliate_ReferenceId = affiliateReferenceId,
                    Email = userEmail,
                };

                return hotelManagerClient.RetrieveBookingWithAffiliateReferenceIdAsync(itineraryModel);
            }

            public async Task<ProductReserve.BookingRespond> CheckoutReserveRoom(int bookingId, string bookStatus, string hostURL,
                MayFlower db, bool isAuthCapture = false, bool isCrossSell = false)
            {
                BookingHotel booking = db.BookingHotels.Find(bookingId);

                BookingLogMsg.Append(string.Format("SuperPNR : {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, DateTime.Now.ToLoggerDateTime()))
                    .AppendLine().AppendLine();

                var hotel = booking.ToProductHotel(ClientIP, UserAgent);

                List<ProductReserve.Error> errorLog = new List<ProductReserve.Error>();
                CheckoutProduct product = new CheckoutProduct();
                product.ContactPerson = hotel.ContactPerson;
                product.InsertProduct(hotel);

                StringBuilder sb = new StringBuilder(); // use for append error message
                bool isNoError = true; // use for indicate any error then return to call
                bool isAllReserveSuccess = false;
                bool isPartialSuccess = false;
                
                bool isAllReserveFail = false;
                
                List<ReserveRoomResponse> reserveResultAll = new List<ReserveRoomResponse>();
                List<ReserveRoomResponse> reserveResultSucceed = new List<ReserveRoomResponse>();
                BookingQuery.BookingExpection bookingExpection = null;

                var roomGrp = hotel.RoomDetails.GroupBy(x => new { x.HotelId, x.RateKey, x.RoomTypeCode, x.PreBookRateToken });

                foreach (var room in roomGrp)
                {
                    var roomDtl = new List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.GuestRoomAdditionalInfo>();

                    var bookRoomReq = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBReserveRoomRequest
                    {
                        CustomerIpAddress = ClientIP,
                        CustomerUserAgent = UserAgent,
                        CustomerSessionId = SessionId,
                        ArrivalDate = booking.CheckInDateTime,
                        DepartureDate = booking.CheckOutDateTime,
                        CurrencyCode = booking.CurrencyCode,
                        HotelID = booking.HotelID,
                        HotelSuppliers = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.Suppliers.EANRapid,
                        RateKey = room.Key.RateKey,
                        RoomTypeCode = room.Key.RoomTypeCode,
                        rateType = Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RateType.MerchantStandard,
                        affiliateReferenceId = booking.SuperPNRID.ToString(),//booking.SuperPNRNo,
                        reservationInfo = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ReservationInfo
                        {
                            Email = hotel?.ContactPerson?.Email.Trim(),
                            FirstName = hotel?.ContactPerson?.GivenName.Trim(),
                            LastName = hotel?.ContactPerson?.Surname.Trim(),
                            HomePhone = (UtilitiesService.GetPhoneCode(hotel?.ContactPerson?.Phone1LocationCode?.Trim()) + " " + hotel?.ContactPerson?.Phone1)?
                                            .Replace("+", "").Replace(" ", "").Trim(),
                        },
                        tokenKey = room.Key.PreBookRateToken,
                    };

                    foreach (var item in room)
                    {
                        var childAssign = new List<int>();
                        for (int i = 0; i < item.GuestsInRoomDetail.Childrens; i++)
                        {
                            childAssign.Add(8); // hardcode child age
                        }

                        roomDtl.Add(new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.GuestRoomAdditionalInfo
                        {
                            Email = bookRoomReq.reservationInfo.Email,
                            Title = item?.Title?.Trim() ?? hotel?.ContactPerson?.Title?.Trim(),
                            FirstName = item?.GivenName?.Trim() ?? bookRoomReq.reservationInfo.FirstName?.Trim(),
                            LastName = item?.Surname?.Trim() ?? bookRoomReq.reservationInfo.LastName?.Trim(),
                            HomePhone = bookRoomReq.reservationInfo.HomePhone,
                            smokingPreference = item.SpecialRequest?.SmokingPreferences == SmokingPreferencesEnum.Smoking ? Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SmokingPreference.S :
                                                item.SpecialRequest?.SmokingPreferences == SmokingPreferencesEnum.NonSmoking ? Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SmokingPreference.NS :
                                                Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SmokingPreference.S,
                            SpecialRequests = item?.SpecialRequest?.AdditionalRequest,
                            TotalAdults = item.GuestsInRoomDetail.Adults,
                            NumberOfChildrenAge = childAssign.ToArray(),
                        });
                    }

                    bookRoomReq.NumberOfRoom = roomDtl?.ToArray();
                    Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBReserveRoomResponse bookRoomResp = null;

                    try
                    {
                        Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBHotelManagerClient esbHotelManagerClient = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.ESBHotelManagerClient();
                        bookRoomResp =  await esbHotelManagerClient.BookHotelAsync(bookRoomReq);

                        #region Log booking respond into event viewer
                        try
                        {
                            BookingLogMsg.AppendLine("Itinerary Respond:")
                                .AppendLine(JsonConvert.SerializeObject(bookRoomResp))
                                .AppendLine();
                        }
                        catch (Exception ex)
                        {
                            BookingLogMsg.AppendLine()
                                .AppendLine("Error when serialilze Itinerary Respond:")
                                .AppendLine(ex.ToString())
                                .AppendLine();
                        }
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine(ex.GetBaseException().ToString());
                    }

                    if ((bookRoomResp?.Errors?.ErrorMessage?.Length == 0 || bookRoomResp?.Errors?.ErrorMessage == null)
                        && bookRoomResp?.ReserveRoomInformationList?.Length > 0)
                    {
                        foreach (var item in bookRoomResp.ReserveRoomInformationList)
                        {
                            booking.HotelStateProvinceCode = item.hotelStateProvinceCode ?? booking.HotelStateProvinceCode;
                            booking.HotelAddress = item.hotelAddress ?? booking.HotelAddress;
                            booking.HotelPostalCode = item.hotelPostalCode ?? booking.HotelPostalCode;
                            booking.HotelCountryCode = item.hotelCountryCode ?? booking.HotelCountryCode;

                            booking.HotelPhoneNumber = item.hotelPhoneNumber ?? booking.HotelPhoneNumber;
                            booking.BookingStatusCode = "CON";

                            foreach (var dbRoomPax in booking.RoomPaxHotels)
                            {
                                if (!dbRoomPax.IsContactPerson ?? false)
                                {
                                    dbRoomPax.ItineraryNumber = item.itineraryId ?? "";
                                    dbRoomPax.RoomConfirmationNumber = string.Join(", ", item.confirmationNumbers ?? new string[] { "" });
                                    dbRoomPax.CreatedDate = DateTime.Now;
                                    dbRoomPax.CreatedDateUTC = DateTime.UtcNow;

                                    await db.SaveChangesAsync();
                                }
                            }

                            isAllReserveSuccess = isAllReserveSuccess || item.itineraryId?.Length > 0;
                        }
                    }
                    else
                    {
                        if (bookRoomResp?.Errors?.ErrorMessage?.Length > 0)
                        {
                            isNoError = false;
                            string errorMsg = "!!! Error when reserve room (EANRapid)" + JsonConvert.SerializeObject(bookRoomResp?.Errors, Formatting.Indented);
                            sb.AppendLine(errorMsg);
                        }

                        bookingExpection = new BookingQuery.BookingExpection(booking, reserveResultAll, sb, isCrossSell);
                    }
                }

                if (sb.Length > 0 || !isNoError)
                {
                    try
                    {
                        string errMsg = "Expedia HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                            BookingLogMsg.ToString() + Environment.NewLine + Environment.NewLine + sb;

                        ItineraryLog.WriteEventLog(errMsg, EventLogEntryType.Warning, 301);

                        Logger.Error(errMsg);
                    }
                    catch (Exception ex)
                    {
                        Logger.Info("Expedia HOTEL reserve fail exception. - " + DateTime.Now.ToLoggerDateTime() +
                            BookingLogMsg.ToString() + Environment.NewLine + Environment.NewLine + sb +
                            Environment.NewLine + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    try
                    {
                        ItineraryLog.WriteEventLog(BookingLogMsg, EventLogEntryType.SuccessAudit, 302);
                    }
                    catch (Exception ex)
                    {
                        Logger.Info(ex, BookingLogMsg.ToString());
                    }
                }

                return new ProductReserve.BookingRespond
                {
                    AttachmentCollection = bookingExpection?.AttachmentCollection,
                    EmailContent = bookingExpection?.EmailContent,
                    BatchBookResult = isAllReserveSuccess ? ProductReserve.BookResultType.AllSuccess :
                    (isPartialSuccess && !isAllReserveFail ? ProductReserve.BookResultType.PartialSuccess : ProductReserve.BookResultType.AllFail),
                    SuperPNRNo = booking.SuperPNRNo,
                    ErrorLog = errorLog,
                };
            }

            public async Task<ProductReserve.BookingRespond> CheckoutReserveRoomCheckExist(int superPNRId, int bookingId, string bookStatus, string hostURL,
                MayFlower db, bool isAuthCapture = false, bool isCrossSell = false)
            {
                var output = new ProductReserve.BookingRespond();

                var checkExists = await GetReservedItineraryAsync(superPNRId.ToString());
                
                if (checkExists != null && !string.IsNullOrWhiteSpace(checkExists.itinerary_id))
                {
                    BookingHotel booking = db.BookingHotels.Find(bookingId);

                    BookingLogMsg.Append(string.Format("SuperPNR : {0} - {1} [{2}]", booking.SuperPNRID, booking.SuperPNRNo, DateTime.Now.ToLoggerDateTime()))
                        .AppendLine().AppendLine();

                    var confirmaId = string.Join(", ", checkExists.rooms?.Select(s => s.confirmation_id?.expedia ?? "").Distinct()
                                        ?? new List<string>());
                    foreach (var room in booking.RoomPaxHotels)
                    {
                        room.ItineraryNumber = checkExists.itinerary_id;
                        room.RoomConfirmationNumber = confirmaId;
                    }

                    booking.BookingStatusCode = "CON";

                    await db.SaveChangesAsync();

                    return new ProductReserve.BookingRespond
                    {
                        BatchBookResult = ProductReserve.BookResultType.AllSuccess,
                        SuperPNRNo = booking.SuperPNRNo,
                    };
                }
                else
                {
                    output = await CheckoutReserveRoom(bookingId, bookStatus, hostURL, db, isAuthCapture, isCrossSell);
                }

                return output;
            }

            internal void InitEANRapidObject()
            {
                EventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "MayflowerSource", "MayflowerHotelLog");
                }
                EventLog1.Source = "MayflowerSource";
                EventLog1.Log = "MayflowerHotelLog";
                ItineraryLog = new ItineraryLog(EventLog1);

                hotelManagerClient = Alphareds.Module.ServiceCall.EANRapidHotelServiceCall.RapidHotelManagerClient;
            }
        }

        public class BookingExpection
        {
            public string EmailContent { get; set; }
            public List<System.Net.Mail.Attachment> AttachmentCollection { get; set; }

            Logger logger = LogManager.GetCurrentClassLogger();
            System.Diagnostics.EventLog eventLog1;

            public BookingExpection(BookingHotel booking, object reserveResultAll, StringBuilder additionalError, bool isCrossSell = false)
            {
                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("MayflowerSource"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "MayflowerSource", "MayflowerHotelLog");
                }
                eventLog1.Source = "MayflowerSource";
                eventLog1.Log = "MayflowerHotelLog";

                GenerateRHIEmail(booking, reserveResultAll, additionalError, isCrossSell);
            }

            private void GenerateRHIEmail(BookingHotel booking, object reserveResultAll, StringBuilder additionalError, bool isCrossSell = false)
            {
                List<Attachment> attachCollection = new List<Attachment>();

                string fileName = DateTime.Now.ToLoggerDateTime() + "_Hotel Booking Itinerary" + " - BookingNo_" + booking.SuperPNRNo;

                /* // Disable get PDF which will call database again, and dbContext might not Up To Date.
                var bookedPDF = HotelServiceController.bookingItineraryPrint(booking.BookingID);
                string fullJsonReserveResp = JsonConvert.SerializeObject(reserveResultAll, Formatting.Indented);
                byte[] jsonReserveByte = Encoding.ASCII.GetBytes(fullJsonReserveResp);

                if (bookedPDF != null)
                {
                    Attachment itineraryAttach = new Attachment(new MemoryStream(bookedPDF), fileName + ".pdf");
                    Attachment jsonReserveRespond = new Attachment(new MemoryStream(jsonReserveByte), DateTime.Now.ToLoggerDateTime() + "_ExpediaReserveRespond" + ".json");

                    attachCollection.Add(itineraryAttach);
                    attachCollection.Add(jsonReserveRespond);
                }
                else
                {
                    logger.Log(LogLevel.Error, "Cannot get PDF " + fileName + " - " + DateTime.Now.ToLoggerDateTime());
                    eventLog1.WriteEntry("Cannot get PDF " + fileName + " - " + DateTime.Now.ToLoggerDateTime(), EventLogEntryType.Warning, 301);
                }
                */
                var contactDetail = booking.RoomPaxHotels.FirstOrDefault(x => x.IsContactPerson.HasValue && x.IsContactPerson.Value);
                string phonePrefixNo = UtilitiesService.GetPhoneCode(contactDetail.Phone1LocationCode);

                StringBuilder plainTextContent = new StringBuilder();
                plainTextContent.AppendLine("Dear Support,").AppendLine();
                plainTextContent.Append("There are errors while customer attemp to reserve the hotel. For more information please refer to attached PDF file.");
                plainTextContent.AppendLine();
                plainTextContent.AppendLine().AppendLine("Additional Info:");

                // Booking Status and Customer Details
                plainTextContent.AppendFormat("{0,-25}: {1}", "Booking Status", booking.BookingStatu?.BookingStatusCode ?? booking.BookingStatusCode).AppendLine();
                plainTextContent.AppendFormat("{0,-25}: {1}", "Booking TypeCode", booking.BookingTypeCode).AppendLine();
                plainTextContent.AppendFormat("{0,-25}: {1}", "Hotel Total Booking Amount", booking.TotalBookingAmt).AppendLine();
                plainTextContent.AppendFormat("{0,-25}: {1}", "Customer Name", contactDetail.FullName).AppendLine();
                plainTextContent.AppendFormat("{0,-25}: {1}", "Contact No.", phonePrefixNo + contactDetail.Phone1).AppendLine();
                plainTextContent.AppendFormat("{0,-25}: {1}", "Is Cross Sell", isCrossSell.ToString()).AppendLine();

                eventLog1?.WriteEntry(booking.SupplierHotel.SupplierName + " HOTEL reserve fail, required RHI. - " + DateTime.Now.ToLoggerDateTime() +
                    Environment.NewLine + Environment.NewLine +
                    plainTextContent
                    , EventLogEntryType.Warning, 301);

                AttachmentCollection = attachCollection;
                EmailContent = plainTextContent.ToString() + additionalError.AppendLine().ToString();
            }
        }

        public class CompareRoomRate
        {
            public StringBuilder BookingLogMsg { get; set; }

            private Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier? GetHotelSupplier(string supplierCode)
            {
                switch (supplierCode.ToUpper())
                {
                    case "EAN":
                        return Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.Expedia;
                    case "TP":
                        return Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.Tourplan;
                    case "JAC":
                        return Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.JacTravel;
                    case "HB":
                        return Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.HotelBeds;
                    case "TAAP":
                        return Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelSupplier.ExpediaTAAP;
                    default:
                        return null;
                }
            }

            public CompareRoomRate(ProductHotel hotel, BookingHotel booking)
            {
                BookingLogMsg = new StringBuilder();

                #region Regenerate RateKey for Booking
                try
                {
                    var hotelListReq = hotel.SearchHotelInfo;
                    var oldSearchRoomModel = new SearchRoomModel
                    {
                        ArrivalDate = hotelListReq.ArrivalDate,
                        DepartureDate = hotelListReq.DepartureDate,
                        CurrencyCode = hotelListReq.CurrencyCode,
                        CustomerIpAddress = hotelListReq.CustomerIpAddress,
                        CustomerSessionId = hotelListReq.CustomerSessionId,
                        CustomerUserAgent = hotelListReq.CustomerUserAgent,
                        HotelID = booking.HotelID,
                        SelectedNoOfRoomType = hotel.RoomDetails.Count,
                    };
                    var oldSearchHotelModel = new SearchHotelModel
                    {
                        ArrivalDate = hotelListReq.ArrivalDate,
                        DepartureDate = hotelListReq.DepartureDate,
                        NoOfAdult = hotelListReq.NoOfAdult,
                        NoOfInfant = hotelListReq.NoOfInfant,
                        NoOfRoom = hotel.RoomDetails.Count,
                    };

                    var roomResult = ESBHotelServiceCall.GetRoomAvailability(oldSearchRoomModel, oldSearchHotelModel,
                        GetHotelSupplier(booking.SupplierCode).Value);

                    if (roomResult != null && roomResult.HotelRoomInformationList != null)
                    {
                        int _rateKeyCounter = 0;
                        var roomList = roomResult.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList);
                        foreach (var roomSelected in hotel.RoomDetails)
                        {
                            var _roomQuery = roomList.FirstOrDefault(x =>
                            {
                                var chargeableRateInfo = x.RateInfos[0].chargeableRateInfo_source ?? x.RateInfos[0].chargeableRateInfo;

                                if (chargeableRateInfo.NightlyRatesPerRoom == null)
                                {
                                    chargeableRateInfo.NightlyRatesPerRoom = x.RateInfos[0].Rooms
                                    .SelectMany(xx => xx.ChargeableNightlyRates.Select(d => new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.NightlyRate { baseRate = d.baseRate, rate = d.rate, promo = d.promo })).ToArray();
                                }

                                decimal taxAvgPerRoom = (chargeableRateInfo.surchargeTotal.ToDecimal() / hotel.RoomDetails.Count(c => c.RateCode == roomSelected.RateCode && c.RoomTypeCode == roomSelected.RoomTypeCode)).RoundToDecimalPlace();
                                decimal reQuoteRoomTotalBaseRate = chargeableRateInfo.NightlyRatesPerRoom.Sum(s => s.rate.ToDecimal());

                                bool tokenExists = x.rateCode == roomSelected.RateCode && x.roomTypeCode == roomSelected.RoomTypeCode;
                                bool rateExists = ((roomSelected.TotalBaseRate) >= reQuoteRoomTotalBaseRate) &&
                                (roomSelected.TotalTaxAndServices_Source >= taxAvgPerRoom);

                                bool roomDescSame = (roomSelected.RoomTypeName == x.description) ||
                                (!string.IsNullOrWhiteSpace(x.rateDescription) && roomSelected.RateDesc == x.rateDescription);

                                bool isSameNonRefundable = roomSelected.NonRefundable == x.RateInfos[0].nonRefundable;

                                bool isRecordMatch = (tokenExists || roomDescSame) && rateExists && isSameNonRefundable;

                                if ((!rateExists && roomDescSame) || isRecordMatch)
                                {
                                    BookingLogMsg.AppendLine(string.Format("!!! Hotel ID : [{0}] - {1}", booking.SupplierCode, roomSelected.HotelId));

                                    if (!rateExists && roomDescSame)
                                    {
                                        BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] found, but rate different with original booked amount.", x.rateDescription));

                                        if (!isSameNonRefundable)
                                            BookingLogMsg.AppendLine(string.Format("!!! Rooms [{0}] cancel policy different.", x.rateDescription))
                                            .AppendLine(string.Format("!!! Customer Policy : [{0}]", roomSelected.NonRefundable))
                                            .AppendLine(string.Format("!!! Latest Policy : [{0}]", x.RateInfos[0].nonRefundable));
                                    }

                                    BookingLogMsg.AppendLine()
                                    .AppendLine(string.Format("!!! Customer Rate : [{0}]", roomSelected.TotalBaseRate_Source))
                                    .AppendLine(string.Format("!!! Latest Rate : [{0}]", reQuoteRoomTotalBaseRate))
                                    .AppendLine();
                                }

                                return isRecordMatch;
                            });

                            if (_roomQuery != null)
                                roomSelected.RateKey = hotel.RoomDetails.Count == _roomQuery.RateInfos[0].Rooms.Length ? _roomQuery.RateInfos[0].Rooms[_rateKeyCounter++]?.rateKey ?? _roomQuery.RateInfos[0].Rooms[0].rateKey
                                    : _roomQuery.RateInfos[0].Rooms[0].rateKey;
                            else
                                BookingLogMsg.AppendLine(string.Format("!!! Latest room types doesn't match with original room types."));
                        }
                    }
                    else if (roomResult?.Errors?.ErrorMessage != null)
                    {
                        string errorMsg = "!!! Error when regenerate rateKey" + JsonConvert.SerializeObject(roomResult.Errors, Formatting.Indented);
                        BookingLogMsg.AppendLine(errorMsg);
                    }
                }
                catch (AggregateException ae)
                {
                    throw ae;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                #endregion
            }
        }
    }

}

