using Alphareds.Module.Common;
using Alphareds.Module.CommonController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using Alphareds.Module.ServiceCall;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Alphareds.Module.HotelController;

namespace Mayflower.Controllers
{
    public class AddOnMiniScreenController : Controller
    {
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);

        private MayFlower db = new MayFlower();
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Alphareds.Module.Event.Function.DB eventDBFunc = new Alphareds.Module.Event.Function.DB(logger);
        private Logger Logger { get; set; }
        private string tripid { get; set; }

        public enum ManageMessageId
        {
            RegisterSuccess,
            RegisterFail
        }

        public ActionResult Index()
        {
            return View();
        }

        public async Task<ActionResult> AddOn()
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct);
            bool anyAddOnProduct = false;
            FlightInformation flightInfo = null;
            FlightMasterInfo flightDetailInfo = null;
            DateTime? searchDateFrom = null;
            DateTime? searchDateTo = null;
            string pType = null;
            string flightOrigin = null;
            string destination = null;
            int totalQtyAcceptable = 1;
            MayFlower db = null;
            tripid = Guid.NewGuid().ToString();

            if (checkout.Flight != null)
            {
                db = db ?? new MayFlower();

                // Get Depart Trip Arrival DateTime
                searchDateFrom = checkout.Flight.SearchFlightInfo.BeginDate;
                searchDateTo = checkout.Flight.SearchFlightInfo.EndDate;
                pType = "Flight";
                flightOrigin = checkout.Flight.SearchFlightInfo.DepartureStationCode;
                destination = checkout.Flight.SearchFlightInfo.ArrivalStationCode;
                totalQtyAcceptable = checkout.Flight.SearchFlightInfo.Adults + checkout.Flight.SearchFlightInfo.Childrens;
                bool departFromMYS = db.Stations.FirstOrDefault(x => x.StationCode == flightOrigin)?.CountryCode == "MYS";

                //checking destination support insurance or not
                string arrivalCountryCode = db.Stations.FirstOrDefault(x => x.StationCode == destination)?.CountryCode;
                //bool arrivalCountryHaveInsurance = false;
                var insuranceStartDate = checkout.Flight.SearchFlightInfo.BeginDate.Value.Date;
                var insuranceEndDate = checkout.Flight.SearchFlightInfo.EndDate;
                var totalTravelDay = insuranceEndDate - insuranceStartDate; //"searchDateFrom" will be different when flight reach on next day
                bool totalTravelDaysLessThan45 = checkout.Flight.SearchFlightInfo.isReturn ? totalTravelDay.Value.TotalDays < 45 : true; //more than 45 days no offer insurance
                //arrivalCountryHaveInsurance = UtilitiesService.CheckInsuranceSupportCountry(arrivalCountryCode);

                // TODO Code: Insurance Code Here
                if (Core.IsEnableFlightInsurance && departFromMYS && totalTravelDaysLessThan45) /*arrivalCountryHaveInsurance*/
                {
                    QuoteInsurance(ref checkout);
                }
            }

            if (checkout.Hotel == null && checkout.Flight != null)
            {
                Alphareds.Module.SabreWebService.SWS.ReadTravelItineraryResponse rs = SabreServiceCall.ReadTravelItineraryResponse(checkout.SuperPNRNo);
                var reserveItem = rs.Output.PassengerNameRecord.ReservationItems.ToList();
                List<string> airline = reserveItem.SelectMany(x => x.ReservationFlightSegment).Select(x => x.AirlineCode).Distinct().ToList();
                double stayTotalDays = checkout.Flight.SearchFlightInfo.EndDate.Value.Date.Subtract(searchDateFrom.Value).TotalDays;
                bool lessThan28Days = stayTotalDays < 28;
                string info = lessThan28Days ? null : "longtrip";
                bool forceCrossSale = false;

                #region Query DB is any Cross Sell Hotel Rules
                bool isOneWay = !checkout.Flight.SearchFlightInfo.isReturn;
                IEnumerable<CrossSaleRule> crossSaleAvailaible = null;
                List<CrossSaleRuleHotel> crossSaleRuleHotel = null;
                if (Core.IsEnableHotelCrossSales && !isOneWay)
                {
                    crossSaleAvailaible = CheckIsCrossSalesHotelAvailaible(checkout.Flight.SearchFlightInfo, airline);
                    crossSaleRuleHotel = crossSaleAvailaible != null ? crossSaleAvailaible.SelectMany(x => x.CrossSaleRuleHotels)?.ToList() : null;
                    Session["CrossSaleRules"] = crossSaleRuleHotel;
                }
                #endregion

                if (crossSaleRuleHotel != null)
                {
                    crossSaleRuleHotel = crossSaleRuleHotel.OrderBy(x => x.CrossSaleRule.BookingDateFrom).ToList();

                    if (lessThan28Days && stayTotalDays != 0 && crossSaleRuleHotel.Count() > 0)
                    {
                        crossSaleRuleHotel = crossSaleRuleHotel.ToList();
                        Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse hotelList = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse();
                        List<string> supplierCode = crossSaleRuleHotel.Select(x => x.HotelSupplierCode).Distinct().ToList();

                        SearchHotelModel searchHotelReq = new SearchHotelModel
                        {
                            ArrivalDate = checkout.Flight.SearchFlightInfo.BeginDate.Value.Date,
                            DepartureDate = checkout.Flight.SearchFlightInfo.EndDate.Value.Date,
                            CurrencyCode = "MYR",
                            CustomerIpAddress = Request.UserHostAddress,
                            CustomerUserAgent = Request.UserAgent,
                            NoOfAdult = checkout.Flight.SearchFlightInfo.Adults,
                            NoOfInfant = checkout.Flight.SearchFlightInfo.Childrens,
                            NoOfRoom = 1,
                            Destination = checkout.Flight.SearchFlightInfo.ArrivalStation,
                            IsCrossSell = true,
                            IsB2B = CustomPrincipal.IsAgent,

                            CustomerSessionId = tripid,

                            SupplierIncluded = new SearchSupplier()
                            {
                                Expedia = supplierCode.Contains("EAN") ? true : false,
                                JacTravel = supplierCode.Contains("JAC") ? true : false,
                                Tourplan = supplierCode.Contains("TP") ? true : false,
                                HotelBeds = supplierCode.Contains("HB") ? true : false,
                                EANRapid = supplierCode.Contains("RAP")
                            }
                        };

                        int attemp = 0;
                        attemArea:
                        hotelList = await GetHotelFromSearchModel(searchHotelReq, crossSaleRuleHotel);
                        searchHotelReq.Result = hotelList;
                        bool isErrorDuringGetHotel = (hotelList == null || hotelList.Errors != null || hotelList.HotelList == null || (hotelList.HotelList != null && hotelList.HotelList.Length == 0));
                        if (attemp <= 3 && isErrorDuringGetHotel)
                        {
                            attemp++;
                            goto attemArea;
                        }
                        else if (attemp > 2 && isErrorDuringGetHotel)
                        {
                            string msg = hotelList != null && hotelList.Errors != null ? hotelList.Errors.ErrorMessage : "Service no respond.";
                            logger = LogManager.GetCurrentClassLogger();
                            logger.Error("Error on Flight Search Cross Sales get hotel. " + msg);
                        }

                        if (!isErrorDuringGetHotel)
                        {
                            /*checkout.InsertProduct(new ProductHotel
                            {
                                SearchHotelInfo = searchHotelReq,
                            });*/

                            Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, searchHotelReq);
                            Core.SetSession(Enumeration.SessionName.HotelList, tripid, searchHotelReq);
                            //hotelList.HotelList = HotelServiceController.ProcessDiscountCalculation(hotelList.HotelList, crossSaleRuleHotel);
                            // 2017/09/07 - Direct use markup from compare tool service
                            hotelList.HotelList = hotelList.HotelList.OrderBy(x => x.lowRate).ToArray();
                            CrossSellModels crossSellModels = new CrossSellModels
                            {
                                ForceCrossSell = forceCrossSale,
                                CrossSellRules = crossSaleRuleHotel,
                                HotelInformation = hotelList.HotelList,
                            };

                            checkout.SellItemsAvailable.Hotels = crossSellModels;
                            anyAddOnProduct = true;
                        }
                        else
                        {
                            Session["isErrorDuringGetHotel"] = isErrorDuringGetHotel;
                        }
                    }
                }
            }

            if (Core.IsEnableEventProduct && DateTime.Now >= new DateTime(2017, 10, 21, 12, 0, 0))
            {
                db = db ?? new MayFlower();
                bool withPromoEvent = checkout.PromoCodeFunctions.GetFrontendFunction.DisplayPromoEvent;
                var mainProductAmt = checkout[checkout.MainProductType]?.PricingDetail?.ProductTotalAmount;

                var eventList = eventDBFunc.GetEventProductList(searchDateFrom, searchDateTo, pType, flightOrigin, destination,
                    totalQtyAcceptable, withPromoEvent, mainProductAmt, db);
                if (eventList != null)
                {
                    // TODO: Ticket Code Here
                    /* Temp method, since db store procedure not ready for other product yet
                     * Actual need filter EventTypeCode (ex. CT, TH)
                     * Search 28-01-2017 to 30-01-2017
                     */
                    if (eventList.DetailsInfo.Count > 0 && eventList.HeaderInfo.Count > 0)
                    {
                        eventList.SearchInfo = new SearchInfo
                        {
                            DateFrom = searchDateFrom,
                            DateTo = searchDateTo,
                            Origin = flightOrigin,
                            Destination = destination,
                            ProductType = pType == ProductTypes.Hotel.ToString() ? ProductTypes.Hotel : ProductTypes.Flight,
                        };
                        checkout.SellItemsAvailable.EventProducts = eventList;

                        anyAddOnProduct = true;
                    }
                    else
                    {
                        eventList = null;
                        checkout.SellItemsAvailable.EventProducts = eventList;
                    }
                }
            }

            checkout.ReferralCode = tripid;
            ViewBag.tripid = tripid;
            Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkout);
            return View(checkout);
        }

        public ActionResult UserInformation(BookedProductView model)
        {
            BookedProductView viewModel = (BookedProductView)Core.GetSession(Enumeration.SessionName.BookedProductView);
            //var FlightSummarymodel = TempData["FlightSummary"];
            return View(viewModel);
        }

        [HttpPost]
        public ActionResult CheckPNR(FormCollection form)
        {
            string bookingID = form["pnr"];
            if (bookingID == "")
            {
                ViewBag.HtmlStr = "<span id='PNRError'><strong class='textforerrormsg'>Booking PNR is required </strong></span>";
                return View("Index");
            }

            Alphareds.Module.SabreWebService.SWS.ReadTravelItineraryResponse rs = SabreServiceCall.ReadTravelItineraryResponse(bookingID);
            if (rs.Output != null)
            {
                try
                {
                    if (rs.Output.PassengerNameRecord.ReservationItems.Count() > 0)
                    {
                        BookingConfirmationDetail model = new BookingConfirmationDetail();
                        model.PassengerData = new List<Pax>();
                        foreach (var list in rs.Output.PassengerNameRecord.PassengerData)
                        {
                            Pax pax = new Pax();
                            pax.TitleCode = list.Title;
                            pax.Surname = list.Surname;
                            pax.GivenName = list.GivenName;
                            pax.PassengerEmail = list.PassengerEmail;
                            pax.PassengerType = list.PassengerType;
                            pax.Phone1 = list.Phone1;
                            pax.Phone1LocationCode = list.Phone1LocationCode;
                            pax.Phone1UseType = list.Phone1UseType;
                            pax.Phone2 = list.Phone2;
                            pax.Phone2LocationCode = list.Phone2LocationCode;
                            pax.Phone2UseType = list.Phone2UseType;
                            pax.DOB = list.DateOfBirth;
                            pax.PassengerType = list.PassengerType;
                            model.PassengerData.Add(pax);
                        }

                        model.FlightSegmentPaxType = new List<FlightSegmentPaxType>();

                        foreach (var list in rs.Output.PassengerNameRecord.PassengerData.GroupBy(x => x.PassengerType))
                        {
                            FlightSegmentPaxType PaxType = new FlightSegmentPaxType();
                            PaxType.PassengerType = list.Key;
                            model.FlightSegmentPaxType.Add(PaxType);
                        }

                        Booking bk = new Booking();
                        bk.IsReturn = false;
                        bk.SuperPNRNo = bookingID;
                        model.BookingFlightSegment = new List<FlightSegment>();

                        bool isRoundTrip = false;
                        List<string> flightStationSeg = new List<string>();
                        int loopIndex = 0;
                        int roundSegment = -1;
                        bool breakLoop = false;

                        foreach (var flightList in rs.Output.PassengerNameRecord.ReservationItems)
                        {
                            foreach (var flight in flightList.ReservationFlightSegment)
                            {
                                string _seg = flightStationSeg.LastOrDefault();

                                if (_seg != null)
                                {
                                    if (_seg == $"{flight.ArrivalAirportLocationCode}_{flight.DepartureAirportLocationCode}")
                                    {
                                        roundSegment = loopIndex;
                                        breakLoop = true;
                                        break;
                                    }
                                }

                                flightStationSeg.Add($"{flight.DepartureAirportLocationCode}_{flight.ArrivalAirportLocationCode}");
                            }

                            if (breakLoop)
                            {
                                break;
                            }

                            loopIndex++;
                        }

                        for (int i = 0; i < rs.Output.PassengerNameRecord.ReservationItems.Length; i++)
                        {
                            var segmentItem = rs.Output.PassengerNameRecord.ReservationItems[i];

                            foreach (var itemList in segmentItem.ReservationFlightSegment)
                            {
                                short s;
                                short.TryParse(itemList.FlightNumber, out s);
                                FlightSegment fs = new FlightSegment
                                {
                                    AirlineCode = itemList.AirlineCode,
                                    ArrivalAirportLocationCode = itemList.ArrivalAirportLocationCode,
                                    ArrivalAirportTerminalID = itemList.ArrivalAirportTerminalID,
                                    ArrivalDateTime = itemList.ArrivalDateTime,
                                    DepartureAirportLocationCode = itemList.DepartureAirportLocationCode,
                                    DepartureAirportTerminalID = itemList.DepartureAirportTerminalID,
                                    DepartureDateTime = itemList.DepartureDateTime,
                                    ElapsedTime = itemList.ElapsedTime,
                                    FlightNumber = s,
                                    SegmentNumber = (byte)itemList.SegmentNumber,
                                    SegmentOrder = i >= roundSegment ? "I" : "O",
                                    OperatingCode = itemList.OperatingAirlineCode,
                                    OperatingNumber = itemList.OperatingFlightNumber,
                                };

                                model.BookingFlightSegment.Add(fs);
                            }

                            if (!isRoundTrip)
                            {
                                bk.IsReturn = true;
                                isRoundTrip = i >= roundSegment;
                            }

                            BookingSummaryViewModel BSViewModel = new BookingSummaryViewModel();
                            BSViewModel.BookingSummaryFlightList = new List<BookingSummaryFlight>();
                            foreach (var b in model.BookingFlightSegment.GroupBy(x => x.SegmentOrder))
                            {
                                var kkk = (from a in b
                                           select new BookingSummaryFlightSegment
                                           {

                                               AirEquipType = a.AirEquipType,
                                               AirlineCode = a.AirlineCode,
                                               ArrivalAirportLocationCode = a.ArrivalAirportLocationCode,
                                               ArrivalDateTime = a.ArrivalDateTime,
                                               ArrivalAirportTerminalID = a.ArrivalAirportTerminalID,
                                               DepartureAirportLocationCode = a.DepartureAirportLocationCode,
                                               DepartureDateTime = a.DepartureDateTime,
                                               DepartureAirportTerminalID = a.DepartureAirportTerminalID,
                                               ElapsedTime = a.ElapsedTime,
                                               FlightNumber = a.FlightNumber.ToString(),
                                               OperatingAirlineCode = a.OperatingCode,
                                               OperatingFlightNumber = a.OperatingNumber,
                                               ResBookDesigCode = a.ResBookDesigCode,
                                           }).ToList();
                                BSViewModel.BookingSummaryFlightList.Add(new BookingSummaryFlight()
                                {
                                    BookingSummaryFlightSegmentList = kkk,
                                    TotalElapsedTime = kkk.Select(x => x.ElapsedTime).Sum(x => x),
                                });
                            }
                            model.BookingSummaryViewModel = BSViewModel;

                        }

                        model.ConfirmationOutputDb = bk;
                        BookedProductView viewModel = new BookedProductView
                        {
                            Flight = model
                        };

                        Core.SetSession(Enumeration.SessionName.BookedProductView, viewModel);
                        return RedirectToAction("UserInformation");
                    }
                    else
                    {
                        return RedirectToAction("Index", "AddOnMineScreen");
                    }
                }
                catch (Exception ex)
                {
                    return View("UserInformation");
                }
            }
            else
            {
                return RedirectToAction("Index", "AddOnMineScreen");
            }
        }

        [HttpPost]
        public ActionResult UserInformation(FormCollection form)
        {
            string bookingID = form["Flight.ConfirmationOutputDb.SuperPNRNo"];
            Alphareds.Module.SabreWebService.SWS.ReadTravelItineraryResponse rs = SabreServiceCall.ReadTravelItineraryResponse(bookingID);
            if (rs.Output != null)
            {
                try
                {
                    if (rs.Output.PassengerNameRecord.ReservationItems.Count() > 0)
                    {
                        BookingConfirmationDetail model = new BookingConfirmationDetail();
                        model.PassengerData = new List<Pax>();
                        foreach (var list in rs.Output.PassengerNameRecord.PassengerData)
                        {
                            Pax pax = new Pax();
                            pax.TitleCode = list.Title;
                            pax.Surname = list.Surname;
                            pax.GivenName = list.GivenName;
                            pax.PassengerEmail = list.PassengerEmail;
                            pax.PassengerType = list.PassengerType;
                            pax.Phone1 = list.Phone1;
                            pax.Phone1LocationCode = list.Phone1LocationCode;
                            pax.Phone1UseType = list.Phone1UseType;
                            pax.Phone2 = list.Phone2;
                            pax.Phone2LocationCode = list.Phone2LocationCode;
                            pax.Phone2UseType = list.Phone2UseType;
                            pax.DOB = list.DateOfBirth;
                            pax.PassengerType = list.PassengerType;
                            model.PassengerData.Add(pax);
                        }

                        model.FlightSegmentPaxType = new List<FlightSegmentPaxType>();

                        foreach (var list in rs.Output.PassengerNameRecord.PassengerData.GroupBy(x => x.PassengerType))
                        {
                            FlightSegmentPaxType PaxType = new FlightSegmentPaxType();
                            PaxType.PassengerType = list.Key;
                            PaxType.NoOfPax = (byte)list.Count();
                            model.FlightSegmentPaxType.Add(PaxType);
                        }

                        Booking bk = new Booking();
                        bk.IsReturn = false;
                        model.BookingFlightSegment = new List<FlightSegment>();

                        bool isRoundTrip = false;
                        List<string> flightStationSeg = new List<string>();
                        int loopIndex = 0;
                        int roundSegment = -1;
                        bool breakLoop = false;

                        foreach (var flightList in rs.Output.PassengerNameRecord.ReservationItems)
                        {
                            foreach (var flight in flightList.ReservationFlightSegment)
                            {
                                string _seg = flightStationSeg.LastOrDefault();

                                if (_seg != null)
                                {
                                    if (_seg == $"{flight.ArrivalAirportLocationCode}_{flight.DepartureAirportLocationCode}")
                                    {
                                        roundSegment = loopIndex;
                                        breakLoop = true;
                                        break;
                                    }
                                }

                                flightStationSeg.Add($"{flight.DepartureAirportLocationCode}_{flight.ArrivalAirportLocationCode}");
                            }

                            if (breakLoop)
                            {
                                break;
                            }

                            loopIndex++;
                        }

                        for (int i = 0; i < rs.Output.PassengerNameRecord.ReservationItems.Length; i++)
                        {
                            var segmentItem = rs.Output.PassengerNameRecord.ReservationItems[i];

                            foreach (var itemList in segmentItem.ReservationFlightSegment)
                            {
                                short s;
                                short.TryParse(itemList.FlightNumber, out s);
                                FlightSegment fs = new FlightSegment
                                {
                                    AirlineCode = itemList.AirlineCode,
                                    ArrivalAirportLocationCode = itemList.ArrivalAirportLocationCode,
                                    ArrivalAirportTerminalID = itemList.ArrivalAirportTerminalID,
                                    ArrivalDateTime = itemList.ArrivalDateTime,
                                    DepartureAirportLocationCode = itemList.DepartureAirportLocationCode,
                                    DepartureAirportTerminalID = itemList.DepartureAirportTerminalID,
                                    DepartureDateTime = itemList.DepartureDateTime,
                                    ElapsedTime = itemList.ElapsedTime,
                                    FlightNumber = s,
                                    SegmentNumber = (byte)itemList.SegmentNumber,
                                    SegmentOrder = i >= roundSegment ? "I" : "O",
                                    OperatingCode = itemList.OperatingAirlineCode,
                                    OperatingNumber = itemList.OperatingFlightNumber,
                                };

                                model.BookingFlightSegment.Add(fs);
                            }

                            if (!isRoundTrip)
                            {
                                bk.IsReturn = true;
                                isRoundTrip = i >= roundSegment;
                            }

                            BookingSummaryViewModel BSViewModel = new BookingSummaryViewModel();
                            BSViewModel.BookingSummaryFlightList = new List<BookingSummaryFlight>();
                            foreach (var b in model.BookingFlightSegment.GroupBy(x => x.SegmentOrder))
                            {
                                var kkk = (from a in b
                                           select new BookingSummaryFlightSegment
                                           {

                                               AirEquipType = a.AirEquipType,
                                               AirlineCode = a.AirlineCode,
                                               ArrivalAirportLocationCode = a.ArrivalAirportLocationCode,
                                               ArrivalDateTime = a.ArrivalDateTime,
                                               ArrivalAirportTerminalID = a.ArrivalAirportTerminalID,
                                               DepartureAirportLocationCode = a.DepartureAirportLocationCode,
                                               DepartureDateTime = a.DepartureDateTime,
                                               DepartureAirportTerminalID = a.DepartureAirportTerminalID,
                                               ElapsedTime = a.ElapsedTime,
                                               FlightNumber = a.FlightNumber.ToString(),
                                               OperatingAirlineCode = a.OperatingCode,
                                               OperatingFlightNumber = a.OperatingNumber,
                                               ResBookDesigCode = a.ResBookDesigCode,
                                           }).ToList();
                                BSViewModel.BookingSummaryFlightList.Add(new BookingSummaryFlight()
                                {
                                    BookingSummaryFlightSegmentList = kkk,
                                    TotalElapsedTime = kkk.Select(x => x.ElapsedTime).Sum(x => x),
                                });
                            }
                            model.BookingSummaryViewModel = BSViewModel;

                        }

                        model.ConfirmationOutputDb = bk;
                        BookedProductView viewModel = new BookedProductView
                        {
                            Flight = model
                        };

                        SearchFlightResultViewModel searchModel = new SearchFlightResultViewModel
                        {
                            DepartureStation = viewModel.Flight.BookingSummaryViewModel.BookingSummaryFlightList.FirstOrDefault().BookingSummaryFlightSegmentList.FirstOrDefault().DepartureAirportLocationCode,
                            ArrivalStation = viewModel.Flight.BookingSummaryViewModel.BookingSummaryFlightList.LastOrDefault().BookingSummaryFlightSegmentList.FirstOrDefault().DepartureAirportLocationCode,
                            BeginDate = viewModel.Flight.BookingSummaryViewModel.BookingSummaryFlightList.FirstOrDefault().BookingSummaryFlightSegmentList.FirstOrDefault().DepartureDateTime,
                            EndDate = viewModel.Flight.BookingSummaryViewModel.BookingSummaryFlightList.LastOrDefault().BookingSummaryFlightSegmentList.LastOrDefault().ArrivalDateTime,
                            CabinClass = "",
                            TripType = viewModel.Flight.ConfirmationOutputDb.IsReturn == true ? "Return" : "OneWay",
                            Adults = model.FlightSegmentPaxType.FirstOrDefault(x => x.PassengerType == "ADT")?.NoOfPax ?? 0,
                            Childrens = model.FlightSegmentPaxType.FirstOrDefault(x => x.PassengerType == "CNN")?.NoOfPax ?? 0,
                            Infants = model.FlightSegmentPaxType.FirstOrDefault(x => x.PassengerType == "INF")?.NoOfPax ?? 0,
                            PromoId = viewModel.Flight.ConfirmationOutputDb.PromoID,
                            DepartureTime = viewModel.Flight.BookingSummaryViewModel.BookingSummaryFlightList.FirstOrDefault().TotalElapsedTime.ToString(),
                        };
                        var contact = viewModel.Flight.PassengerData.FirstOrDefault(x => x.IsContactPerson ?? false) ?? viewModel.Flight.PassengerData.FirstOrDefault(x => x.PassengerType == "ADT" && !string.IsNullOrEmpty(x.Phone1));
                        ContactPerson CP = new ContactPerson
                        {
                            Title = contact.TitleCode,
                            GivenName = contact.GivenName,
                            Surname = contact.Surname,
                            Email = contact.PassengerEmail,
                            Phone1 = contact.Phone1,
                            Phone1UseType = contact.Phone1UseType,
                            Phone1LocationCode = db.Stations.FirstOrDefault(x => x.StationCode == contact.Phone1LocationCode).CountryCode,
                            Phone2 = contact.Phone2,
                            Phone2UseType = contact.Phone2UseType,
                            Phone2LocationCode = contact.Phone2LocationCode,
                        };

                        CheckoutProduct checkout = new CheckoutProduct();
                        ProductFlight product = new ProductFlight
                        {
                            ContactPerson = CP,
                            SearchFlightInfo = searchModel,
                            TravellerDetails = new List<TravellerDetail>()
                        };

                        foreach (var list in viewModel.Flight.PassengerData)
                        {
                            TravellerDetail travel = new TravellerDetail();
                            travel.Surname = list.Surname;
                            travel.GivenName = list.GivenName;
                            travel.Title = list.TitleCode;

                            product.TravellerDetails.Add(travel);
                        }

                        checkout.ContactPerson = CP;
                        checkout.SuperPNRNo = bookingID;
                        checkout.InsertProduct(product);

                        Core.SetSession(Enumeration.SessionName.CheckoutProduct, checkout);

                        return RedirectToAction("AddOn");
                    }
                    else
                    {
                        return RedirectToAction("Index", "AddOnMineScreen");
                    }
                }
                catch (Exception ex)
                {
                    return View("UserInformation");
                }
            }
            else
            {
                return RedirectToAction("Index", "AddOnMineScreen");
            }
        }

        protected void QuoteInsurance(ref CheckoutProduct checkoutModel)
        {
            List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote> res = null;

            MayFlower db = new MayFlower();

            try
            {
                res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetTravelQuote(checkoutModel);

                checkoutModel.SellItemsAvailable.Insurance = new CrossSellItemsAvailable.InsuranceInformation
                {
                    ServiceRespond = new List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote>(),
                    QuotedInformations = new List<CrossSellItemsAvailable.InsuranceInformation.QuotedInformation>(),
                };

                foreach (var item in res)
                {
                    if (item.Errors != null)
                    {
                        // No log Not Supported Error Message
                        if ((item.Errors.ErrorMessage == null ||
                            (item.Errors.ErrorMessage != null && !item.Errors.ErrorMessage.ToLower().Contains("not supported"))))
                        {
                            logger.Error("Error return when quote insurance. "
                                + "Destination - From :" + checkoutModel.Flight.SearchFlightInfo.DepartureStationCode.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.ArrivalStationCode.ToString() + Environment.NewLine
                                + "Date - From :" + checkoutModel.Flight.SearchFlightInfo.BeginDate.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.EndDate.ToString()
                                + Environment.NewLine + Environment.NewLine
                                + JsonConvert.SerializeObject(item, Formatting.Indented));
                        }

                        // Break out avoid continue to next line code while looping
                        break;
                    }

                    var quoteStatus = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.MsgStatus;
                    bool isQuoteSuccess = quoteStatus.MsgStatusCd == "Success";

                    //in case have no "item.Errors" but Quote not accepted
                    if (isQuoteSuccess)
                    {
                        checkoutModel.SellItemsAvailable.Insurance.ServiceRespond = res;
                        var resQuotedInfo = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo;

                        #region calculate insurance cost
                        var insuranceMarkup = db.Markups.FirstOrDefault(x => x.MarkupText == "InsuranceCostPercentage");
                        decimal insuranceCostPercentage = insuranceMarkup?.MarkupPrice ?? 0;
                        decimal totalInsuranceCost = 0;
                        if (insuranceCostPercentage != 0 && insuranceMarkup?.MarkupType?.MarkupType1 == "Insurance")
                        {
                            totalInsuranceCost = (resQuotedInfo.InsuredFullToBePaidAmt.Amt.ToDecimal() * (insuranceCostPercentage / 100)).RoundToDecimalPlace();
                        }
                        #endregion

                        CrossSellItemsAvailable.InsuranceInformation.QuotedInformation quotedInformation = new CrossSellItemsAvailable.InsuranceInformation.QuotedInformation
                        {
                            CurrencyCode = resQuotedInfo.InsuredFullToBePaidAmt.CurCd,
                            Price = resQuotedInfo.InsuredFullToBePaidAmt.Amt.ToDecimal(),
                            EffectiveDate = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.EffectiveDt.ToDateTime(),
                            ExpirationDate = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.ExpirationDt.ToDateTime(),
                            TermsConditions = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo.CoverageConditionsInd,
                            InsurancedAddress = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.InsuredOrPrincipal.GeneralPartyInfo.Addr.Addr1,
                            PlanType = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_Plan.PlanDesc,
                            Package = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_InsuredPackage.InsuredPackageDesc,

                            insuranceCost = totalInsuranceCost
                        };
                        checkoutModel.SellItemsAvailable.Insurance.QuotedInformations.Add(quotedInformation);
                    }
                    else
                    {
                        logger.Error("Quote Insurance not success. "
                                + "Destination - From :" + checkoutModel.Flight.SearchFlightInfo.DepartureStationCode.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.ArrivalStationCode.ToString() + Environment.NewLine
                                + "Date - From :" + checkoutModel.Flight.SearchFlightInfo.BeginDate.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.EndDate.ToString()
                                + "Status Message :" + quoteStatus.MsgStatusDesc);
                    }
                }
            }
            catch (AggregateException ae)
            {
                logger.Error(ae.GetBaseException(), "Error when quote insurance.");
            }
            catch (Exception ex)
            {
                logger.Error(ex.GetBaseException(), "Error when quote insurance.");
            }
            finally
            {
                if (checkoutModel.SellItemsAvailable.Insurance.QuotedInformations.Count == 0)
                {
                    checkoutModel.SellItemsAvailable.Insurance = null;
                }
            }
        }

        private IEnumerable<CrossSaleRule> CheckIsCrossSalesHotelAvailaible(SearchFlightResultViewModel flightSelected, List<string> airline, MayFlower dbContext = null)
        {
            dbContext = dbContext ?? new MayFlower();
            var result = dbContext.CrossSaleRules
                .Where(x => x.IsActive && DateTime.Now >= x.BookingDateFrom && DateTime.Now <= x.BookingDateTo
                        && x.IsActive && flightSelected.BeginDate >= x.TravelDateFrom && flightSelected.EndDate <= x.TravelDateTo
                        && x.Destination == flightSelected.ArrivalStation)
                .Include(x => x.CrossSaleRuleHotels)
                .AsEnumerable();

            bool anyAirlineOK = result.Any(x => x.AirlineCode != "-");
            if ((result.Where(x => x.AirlineCode == airline.First()).Count() == 0) || airline.Count > 1)
            {
                result = result.Where(x => x.AirlineCode == "-");
            }
            else if (anyAirlineOK && airline.Count == 1)
            {
                // For specified airline
                result = result.Where(x => x.AirlineCode == airline.First());
            }

            return result.Count() > 0 ? result : null;
        }

        private Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> GetHotelFromSearchModel(SearchHotelModel searchModel, List<string> hotelID)
        {
            var emptyResult = Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
            {
                return null;
            });

            try
            {
                System.Threading.CancellationTokenSource tokenSource = new System.Threading.CancellationTokenSource();
                tokenSource.CancelAfter(5000);
                //return Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelListAsync(searchModel, hotelID);
                var task = Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
                {
                    return Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelList(searchModel, hotelID);
                }, tokenSource.Token);

                //Task[] taskList = new Task[] { task };

                //Task.WaitAny(taskList, (int)5000, tokenSource.Token);
                return task;
            }
            catch (TaskCanceledException)
            {
                return emptyResult;
            }
            catch (OperationCanceledException)
            {
                return emptyResult;
            }
            catch (Exception ex)
            {
                return emptyResult;
            }
        }

        #region Cross sell - call ESB for the 3 hotel id separately if different hotel supplier 
        private async Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> GetHotelFromSearchModel(SearchHotelModel searchModel, List<CrossSaleRuleHotel> hotelCrossSaleRule,
            bool getAsGroup = false)
        {
            Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse hotelList = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse();
            List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> hotelInfo = null;

            if (getAsGroup)
            {
                foreach (var eachSupplier in hotelCrossSaleRule.GroupBy(x => x.HotelSupplierCode))
                {
                    SearchHotelModel tempSearchModel = searchModel.DeepCopy();
                    List<string> hotelID = new List<string>();

                    hotelID.AddRange(eachSupplier.Select(x => x.HotelID).ToList());
                    tempSearchModel.SupplierIncluded = new SearchSupplier()
                    {
                        Expedia = eachSupplier.Key == "EAN",
                        ExpediaTAAP = eachSupplier.Key == "TAAP",
                        HotelBeds = eachSupplier.Key == "HB",
                        JacTravel = eachSupplier.Key == "JAC",
                        Tourplan = eachSupplier.Key == "TP",
                        EANRapid = eachSupplier.Key == "RAP",
                    };

                    var getHotel = await GetHotelFromSearchModel(tempSearchModel, hotelID);

                    if (getHotel?.HotelList != null && getHotel.HotelList.Length > 0)
                    {
                        if (hotelInfo == null)
                        {
                            hotelInfo = getHotel.HotelList.Take(3).ToList();
                            hotelList = getHotel;
                        }
                        else
                        {
                            int remainHotelCount = 3 - getHotel.HotelList.Length;

                            if (remainHotelCount > 0)
                            {
                                hotelInfo.AddRange(getHotel.HotelList.Take(remainHotelCount).ToList());
                            }
                            else
                            {
                                break; // exit loop
                            }
                        }
                    }
                    else
                    {
                        hotelList.Errors = getHotel.Errors ?? hotelList.Errors;
                    }
                }

                if (hotelList != null && hotelInfo != null && hotelInfo.Count > 0)
                {
                    hotelList.HotelList = hotelInfo.ToArray();
                }
            }
            else
            {
                int hotelTaken = 0;
                List<Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>> _taskHotelList =
                    new List<Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>>();

                foreach (var hotel in hotelCrossSaleRule)
                {
                    SearchHotelModel tempSearchModel = searchModel.DeepCopy();
                    List<string> hotelID = new List<string>();

                    hotelID.Add(hotel.HotelID);
                    tempSearchModel.SupplierIncluded = new SearchSupplier()
                    {
                        Expedia = hotel.HotelSupplierCode == "EAN",
                        ExpediaTAAP = hotel.HotelSupplierCode == "TAAP",
                        HotelBeds = hotel.HotelSupplierCode == "HB",
                        JacTravel = hotel.HotelSupplierCode == "JAC",
                        Tourplan = hotel.HotelSupplierCode == "TP",
                        EANRapid = hotel.HotelSupplierCode == "RAP",
                    };

                    _taskHotelList.Add(GetHotelFromSearchModel(tempSearchModel, hotelID));
                }

                for (int i = 0; i < _taskHotelList.Count; i++)
                {
                    if (hotelTaken >= 3)
                        break; // exit loop if enough 3 hotel

                    // Take first completed hotel result from API
                    int ixTaskCompleted = Task.WaitAny(_taskHotelList.ToArray());

                    var _tskCompleted = _taskHotelList[ixTaskCompleted];
                    if (!_tskCompleted.IsCanceled && !_tskCompleted.IsFaulted && _tskCompleted.IsCompleted)
                    {
                        var _result = _tskCompleted.Result;
                        bool isNotNull = hotelList?.HotelList != null;
                        hotelList = hotelList?.HotelList == null ? _result : hotelList;

                        if (_result?.HotelList?.Length > 0 && isNotNull)
                        {
                            hotelList.HotelList = hotelList.HotelList.Concat(_result.HotelList).ToArray();
                        }

                        hotelTaken += _result?.HotelList?.Length ?? 0;
                    }

                    if (_taskHotelList.Remove(_tskCompleted))
                    {
                        // Minus for loop index.
                        i = i - 1;
                    }
                }
            }

            return hotelList;
        }
        #endregion


        [HttpGet]
        public async Task<ActionResult> GetRoomInfo(Search.Hotel.Room roomReq, string data, string tripid, bool cs = false)
        {
            Core.SetSession(Enumeration.SessionName.RoomAvail, tripid, null);
            string[] arr = data?.Split('|') ?? new string[] { }; //HotelID|RoomTypeCode
            SearchRoomModel model = new SearchRoomModel();
            SearchHotelModel model2 = null;
            try
            {
                if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
                {
                    model2 = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                    //model2.AgentCrossSale = false; // boolean default is false

                    model.ArrivalDate = model2.ArrivalDate;
                    model.DepartureDate = model2.DepartureDate;
                    model.CurrencyCode = model2.CurrencyCode;
                    model.CustomerUserAgent = Request.UserAgent;
                    model.CustomerIpAddress = GetUserIP();
                    model.CustomerSessionId = model2.CustomerSessionId;
                }
                model.HotelID = roomReq.HID;
                //model.HotelID = arr[0];
                //var mdel2 = model2.Result.HotelList.Where(x => x.hotelId == model.HotelID).ToList();

                var cacheRoomModel = (SearchRoomModel)Core.GetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + model.HotelID);

                if (true || cacheRoomModel == null || cacheRoomModel.Result == null
                    || cacheRoomModel.Result.HotelRoomInformationList == null || cacheRoomModel.Result.HotelRoomInformationList.Length == 0
                    || cacheRoomModel.Result.HotelRoomInformationList.Any(x => x.roomAvailabilityDetailsList == null || x.roomAvailabilityDetailsList.Length == 0)
                    || cs)
                {
                    ESBHotelManagerClient webservice = new ESBHotelManagerClient();

                    roomReq.InitESBRoomRequest(true);
                    roomReq.ESBRoomRequest.CustomerUserAgent = Request.UserAgent;
                    roomReq.ESBRoomRequest.CustomerSessionId = model.CustomerSessionId;
                    roomReq.ESBRoomRequest.CustomerIpAddress = GetUserIP();

                    model.Result = await webservice.GetRoomAvailabilityAsync(roomReq.ESBRoomRequest);
                }
                else
                {
                    model.Result = cacheRoomModel.Result;
                }

                if (model.Result != null && model.Result.Errors != null)
                {
                    Logger.Error("ESBHotelServiceCall return error when GetRoomAvailability. " + Environment.NewLine + Environment.NewLine +
                        string.Format("Category - {0}: \n\r Message - {1}", model.Result.Errors.Category, model.Result.Errors.ErrorMessage)
                        + Environment.NewLine + Environment.NewLine + "Hotel ID: " + model.HotelID
                        + Environment.NewLine + "Supplier: " + JsonConvert.SerializeObject(model2.SupplierIncluded));
                }
                //var hotelsupp = model.Result.HotelRoomInformationList[0].hotelSupplier.ToString();

                GetLowestRoomRates(ref model);

                // avoid keep calculate price from cached room result
                if (cs && tripid != null && cacheRoomModel == null)
                {
                    var crossSaleRule = (IEnumerable<CrossSaleRuleHotel>)Session["CrossSaleRules" + tripid];
                    //model.Result.HotelRoomInformationList = HotelServiceController.ProcessDiscountCalculation(model.Result.HotelRoomInformationList, crossSaleRule);
                    // 2017/09/07 - Direct use markup from compare tool service
                }


                Core.SetSession(Enumeration.SessionName.RoomAvail, tripid, model);
                Core.SetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + model.HotelID, model);

                if (model.Result.HotelRoomInformationList != null && model.Result.HotelRoomInformationList.Length > 0)
                {
                    #region PromoCode Section
                    if (Core.IsEnablePayByPromoCode && model2.IsPromoCodeUsed)
                    {
                        MayFlower db = new MayFlower();
                        var promoCodeRule = GetPromoCodeDiscountRule(model2, model2.Result.HotelList, db);
                        if (promoCodeRule != null && cacheRoomModel == null)
                        {
                            //ViewBag.PromoCodeMsg = promoCodeRule.DiscountTypeCode == Enumeration.PricingCodeType.FIX.ToString() ? model.CurrencyCode + " " + promoCodeRule.DiscountAmt :
                            //    promoCodeRule.DiscountAmt.ToString("n0") + "%";
                            //Session["PromoCodeMsg" + tripid] = ViewBag.PromoCodeMsg;
                            HotelServiceController.ProcessDiscountCalculation(model.Result.HotelRoomInformationList, promoCodeRule, model2.Destination, model2.NoOfRoom);
                        }
                    }
                    #endregion

                    if (arr.Length > 1)
                    {
                        List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails> room = model.Result.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList)
                            .Where(x => x.roomTypeCode.Equals(arr[1]) || x.jacTravelPropertyRoomTypeID == arr[1] ||
                            (x.jacTravelPropertyRoomTypeID == "0" && x.jacTravelBookingToken == arr[1])).ToList();
                        if (room.Count > 0)
                        {
                            ViewData.Add("SELECTED", room);
                            ViewData.Add("HOTELID", model.HotelID);
                        }
                    }
                }

                #region find total nights selected
                TimeSpan timeDiff = model.DepartureDate.Date - model.ArrivalDate.Date;
                int totaldays = (int)Math.Round(timeDiff.TotalDays);
                ViewData.Add("TOTALNIGHTS", totaldays);
                #endregion
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, DateTime.Now.ToLoggerDateTime() + " - " + "Hotel Search - GetRoomInfo Error.");
            }

            ViewData.Add("VALUEADDS", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_ValueAdds.xml"));
            ViewData.Add("BETTYPES", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_BetTypes.xml"));
            ViewData.Add("AMENITIES", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_Amenities.xml"));
            ViewData.Add("PAXES", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_Paxes.xml"));

            bool ShowMoreOption = arr.Length > 1 && arr[1].Equals("room");

            #region 2017/02/22 - David added portion, store in session for invoking GTM.
            if (!cs && model.Result != null)
            {
                if (model.Result.HotelRoomInformationList != null && model.Result.HotelRoomInformationList.Count() > 0)
                {
                    #region Sorting
                    string SortingName = "";
                    if (Core.GetSession(Enumeration.SessionName.Sorting, tripid) == null) { SortingName = "TripAdvisor Rating"; }
                    else
                    {
                        string sorting = Core.GetSession(Enumeration.SessionName.Sorting, tripid).ToString();
                        switch (sorting)
                        {
                            case "1": SortingName = "TripAdvisor Rating"; break;
                            case "2": SortingName = "Best Deal"; break;
                            case "3": SortingName = "Property Ratings"; break;
                            default: SortingName = "TripAdvisor Rating"; break;
                        }
                    }
                    #endregion

                    #region Filter result
                    FilterHotelResultModel filterResult = null;
                    if (Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid) != null)
                    {
                        filterResult = (FilterHotelResultModel)Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid);
                    }

                    string FilterName = "";
                    if (filterResult == null) { FilterName = "All Hotels"; }
                    else
                    {
                        if (!string.IsNullOrEmpty(filterResult.Rating))
                        {
                            FilterName = (!string.IsNullOrEmpty(FilterName) ? ", Rating" : "Rating");
                        }
                        if (!string.IsNullOrEmpty(filterResult.PropertyName))
                        {
                            FilterName = (!string.IsNullOrEmpty(FilterName) ? ", Property Name" : "Property Name");
                        }
                    }
                    #endregion

                    #region Selected hotel position
                    int positionCounter = 1;
                    if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
                    {
                        SearchHotelModel searchHotelModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                        foreach (Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation hotelInfo in searchHotelModel.Result.HotelList)
                        {
                            if (hotelInfo.hotelId.Equals(model.Result.HotelRoomInformationList.First().hotelId))
                            {
                                break;
                            }
                            positionCounter++;
                        }
                    }
                    #endregion

                    string findRoomTypeCode = "";
                    if (!ShowMoreOption)    //for pop up room info
                    {
                        findRoomTypeCode = arr[1];  //room type code
                    }

                    List<Alphareds.Module.Model.GTM_HotelProductListModel> GTMList = new List<GTM_HotelProductListModel>();
                    foreach (Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails room in model.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.Where(r => r.roomTypeCode.Contains(findRoomTypeCode)))
                    {
                        GTMList.Add(new GTM_HotelProductListModel
                        {
                            name = "Hotel - " + model.Result.HotelRoomInformationList.First().hotelCity,
                            id = model.Result.HotelRoomInformationList.First().hotelId,
                            price = room.RateInfos.First().chargeableRateInfo.averageRate,
                            hotelName = System.Web.HttpUtility.HtmlEncode(model.Result.HotelRoomInformationList.First().hotelName),
                            roomType = room.description,
                            list = FilterName + " - " + SortingName,
                            position = (positionCounter).ToString(),
                            isDynamic = model2 != null ? model2.IsDynamic : false
                        });
                    }

                    Core.SetSession(Enumeration.SessionName.GTM_trackHotelDetail, tripid, JsonConvert.SerializeObject(GTMList));
                }
            }
            #endregion

            ViewBag.sessionId = tripid;

            if (ShowMoreOption)
                return PartialView("~/Views/Hotel/_ShowMoreRoom.cshtml");
            else
                return PartialView("_PopupRoomInfo");
        }

        public string GetUserIP()
        {
            string IpAddress = "xxxxxx";

            long range_Start1 = BitConverter.ToInt32(System.Net.IPAddress.Parse("10.0.0.0").GetAddressBytes().Reverse().ToArray(), 0);
            long range_End1 = BitConverter.ToInt32(System.Net.IPAddress.Parse("10.255.255.255").GetAddressBytes().Reverse().ToArray(), 0);

            long range_Start2 = BitConverter.ToInt32(System.Net.IPAddress.Parse("172.16.0.0").GetAddressBytes().Reverse().ToArray(), 0);
            long range_End2 = BitConverter.ToInt32(System.Net.IPAddress.Parse("172.31.255.255").GetAddressBytes().Reverse().ToArray(), 0);

            long range_Start3 = BitConverter.ToInt32(System.Net.IPAddress.Parse("192.168.0.0").GetAddressBytes().Reverse().ToArray(), 0);
            long range_End3 = BitConverter.ToInt32(System.Net.IPAddress.Parse("192.168.255.255").GetAddressBytes().Reverse().ToArray(), 0);

            try
            {
                string RemoteUserIP = HttpContext == null ? System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"] : HttpContext.Request.ServerVariables["REMOTE_ADDR"];
                long UserIP = BitConverter.ToInt32(System.Net.IPAddress.Parse(RemoteUserIP).GetAddressBytes().Reverse().ToArray(), 0);
                if ((UserIP >= range_Start1 && UserIP <= range_End1) || (UserIP >= range_Start2 && UserIP <= range_End2) || (UserIP >= range_Start3 && UserIP <= range_End3))
                {
                    IpAddress = "211.24.251.38"; //default IP
                }
                else
                {
                    IpAddress = RemoteUserIP;
                }

            }
            catch { }

            return IpAddress;
        }

        private void GetLowestRoomRates(ref SearchRoomModel model)
        {
            if (model.Result != null)
            {
                if (model.Result.HotelRoomInformationList != null && model.Result.HotelRoomInformationList.Length > 0)
                {
                    List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails> newRoomAvail = new List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails>();
                    IEnumerable<string> roomTypeList = new List<string>();
                    List<string> roomComapreKeyInserted = new List<string>();

                    var allRoomResult = model.Result.HotelRoomInformationList
                        .SelectMany(x => x.roomAvailabilityDetailsList)
                        .OrderBy(x => x.RateInfos[0].chargeableRateInfo.averageBaseRate)
                        .GroupBy(x => new { x.roomTypeCode, x.jacTravelBookingToken, x.jacTravelPropertyRoomTypeID });

                    var hotelSupplier = model.Result.HotelRoomInformationList[0].hotelSupplier;

                    foreach (var roomGrp in allRoomResult)
                    {
                        // All also need show all room.
                        if (hotelSupplier == HotelSupplier.Tourplan || true)
                        {
                            // Tourplan need show all room for different service
                            newRoomAvail.AddRange(roomGrp);
                        }
                        else
                        {
                            newRoomAvail.Add(roomGrp.First());
                        }
                    }
                    model.Result.HotelRoomInformationList[0].roomAvailabilityDetailsList = newRoomAvail.ToArray();
                }
            }
        }

        #region 2017/05/08 - Promo Code Section
        public PromoCodeRule GetPromoCodeDiscountRule(SearchHotelModel searchReq, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation[] hotelList, MayFlower dbContext = null, string DPPromoCode = null)
        {
            dbContext = dbContext ?? new MayFlower();

            var result = dbContext.PromoCodeRules
                .Where(
                x => x.IsActive && DateTime.Now >= x.EffectiveFrom && DateTime.Now <= x.EffectiveTo
                && searchReq.ArrivalDate >= x.TravelDateFrom && searchReq.DepartureDate <= x.TravelDateTo
                && (x.PromoCode.ToUpper() == searchReq.PromoCode.ToUpper() || (DPPromoCode != null && x.PromoCode.ToUpper() == DPPromoCode.ToUpper()))
                && x.HotelPromo
                //&& x.PromoProductHotels.Any(d => hotelList.Any(h => h.hotelId == d.HotelID))
                //&& x.Destination == searchReq.Destination
                );

            Expression<Func<PromoCodeRule, bool>> anyDestinationCondition = (x => x.PromoHotelDestinations.Any(d => d.Destination == "-" && d.Active));
            Expression<Func<PromoCodeRule, bool>> anyHotelCondition = (x => x.PromoHotelDestinations.Any(d => searchReq.Destination.StartsWith(d.Destination) && d.PromoHotelLists.Any(p => p.HotelID == "-" && p.Active)));

            bool anyDestinationOK = result.Any(anyDestinationCondition);
            bool anyHotelOK = result.Any(anyHotelCondition);

            if (anyDestinationOK)
            {
                return result.FirstOrDefault(anyDestinationCondition);
            }
            else if (anyHotelOK)
            {
                Expression<Func<PromoCodeRule, bool>> anyHotelConditionForSpecificDestination = (x => x.PromoHotelDestinations.Any(d => searchReq.Destination.StartsWith(d.Destination) && d.Active && d.PromoHotelLists.Any(p => p.HotelID == "-" && p.Active)));
                return result.FirstOrDefault(anyHotelConditionForSpecificDestination);
            }
            else
            {
                return result.FirstOrDefault(x => x.PromoHotelDestinations.Any(d => searchReq.Destination.StartsWith(d.Destination) && d.Active && d.PromoHotelLists.Any(p => p.HotelID != "" && p.Active)));
            }
        }

        public PromoCodeRule GetPromoCodeDiscountRule(SearchHotelModel searchReq, MayFlower dbContext = null, string DPPromoCode = null)
        {
            return GetPromoCodeDiscountRule(searchReq, null, dbContext, DPPromoCode);
        }

        public PromoCodeRule GetPromoCodeFunctionRule(int Frontendfunc, SearchHotelModel model, MayFlower dbContext = null)
        {
            dbContext = dbContext ?? new MayFlower();
            int functionID = dbContext.FrontendFunctions.FirstOrDefault(x => x.FrontEndID == Frontendfunc && x.IsActive)?.FunctionID ?? 0;
            var AutoAppliedPromoRule = dbContext.PromoFunctions.Where(x => x.FunctionID == functionID);
            foreach (var promorule in AutoAppliedPromoRule)
            {
                model.PromoCode = promorule.PromoCodeRule.PromoCode;
                var promoCodeRule = GetPromoCodeDiscountRule(model, dbContext);
                if (promoCodeRule != null)
                {
                    return promoCodeRule;
                }
            }
            return null;
        }


        private bool IsValidPromoCode(SearchHotelModel searchReq, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation[] hotelList, MayFlower dbContext = null)
        {
            return GetPromoCodeDiscountRule(searchReq, hotelList, dbContext) != null;
        }
        #endregion

        protected void AddInsuranceToPayment(ref CheckoutProduct checkoutModel)
        {
            var productinsurance = new ProductInsurance()
            {
                ContactPerson = checkoutModel.ContactPerson,
                ProductSeq = 3,
                PricingDetail = checkoutModel.SellItemsAvailable.Insurance.PricingDetail,
                TotalQuotePax = checkoutModel.SellItemsAvailable.Insurance.ServiceRespond.FirstOrDefault().Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.InsuredOrPrincipal.GeneralPartyInfo.NameInfo.Count() - 1,
                PlanDesc = checkoutModel.SellItemsAvailable.Insurance.QuotedInformations.FirstOrDefault().PlanType
            };
            checkoutModel.InsertProduct(productinsurance);
        }

        // GET: /Member/Register
        public ActionResult AddOnRegister(ManageMessageId? message)
        {
            StringBuilder builderSuccess = new StringBuilder();
            builderSuccess.Append("Your Registration to Mayflower is Successful!").AppendLine();
            builderSuccess.Append("A confirmation email will be sent shortly, kindly check your mailbox.").AppendLine();

            if (message == ManageMessageId.RegisterSuccess)
            {
                ViewBag.StatusMessage = builderSuccess.ToString();
            }
            else if (message == ManageMessageId.RegisterFail)
            {
                ViewBag.StatusMessage = MvcHtmlString.Create("We are very sorry but we seem to have encountered <br /> an issue with your registration." +
                    "<br/>Our administrator is looking into this as soon as possible." +
                    "<br/>We would greatly appreciate if you can try again later.");
            }
            else
            {
                ViewBag.StatusMessage = null;
            }

            return View();
        }
    }
}