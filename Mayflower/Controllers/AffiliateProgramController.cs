using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AutoMapper;
using Alphareds.Module.Model;
using Alphareds.Module.Common;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.CompareToolWebService.CTWS;
using Mayflower.General;
using Alphareds.Module.CommonController;
using NLog;
using System.Collections;
using System.Threading.Tasks;
using WebGrease.Css.Extensions;
using Alphareds.Module.Model.Database;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Alphareds.Module.Flight_Converter;
using System.Reflection;
using System.Web;
using System.Text;

namespace Mayflower.Controllers
{
    public class AffiliateProgramController : AsyncController
    {
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);

        // POST: AffiliateProgram
        //[HttpPost]
        public ActionResult Flight(AffiliateProgramModels flightAP, string authToken = null)
        {
            MayFlower dbCtx = new MayFlower();
            FlightBookingModel _flightBookingModel = new FlightBookingModel();
            string step = Request.QueryString["action"];
            string info = "";

            CheckAndSetAuthLoginToken(authToken, dbCtx);

            SearchFlightResultViewModel searchModel = new SearchFlightResultViewModel
            {
                DepartureStation = flightAP.ori,
                ArrivalStation = flightAP.des,
                Adults = flightAP.paxAdult,
                Childrens = flightAP.paxChild,
                Infants = flightAP.paxInfant,
                CabinClass = flightAP.Class,
                PrefferedAirlineCode = flightAP.airlineCode,
                TripType = flightAP.isRoundTrip == 1 ? "Return" : "OneWay",
                DirectFlight = flightAP.isDirectFlight == 1,
                BeginDate = flightAP.obDate.ToDateTimeNullable(),
                EndDate = flightAP.ibDate.ToDateTimeNullable(),
                GetCache = true,
                AffiliationId = flightAP.affiliationId ?? string.Empty,
                AffiliationPassword = flightAP.affiliationId != null ? dbCtx.Affiliations.FirstOrDefault(x => x.UserCode == flightAP.affiliationId)?.Password : string.Empty,
                AffiliationRef = flightAP.affiliationRef,
                PromoCode = flightAP.promoCode
            };

            // For pass model validation by matching autocomplete validation
            searchModel.ArrivalStation += " - ";
            searchModel.DepartureStation += " - ";
            if (!string.IsNullOrWhiteSpace(searchModel.PrefferedAirlineCode) && searchModel.PrefferedAirlineCode.Length >= 2)
            {
                searchModel.PrefferedAirlineCode += " - ";
            }

            bool isSearchInfoValid = TryValidateModel(searchModel);

            if (!searchModel.isReturn)
            {
                ModelState["EndDate"]?.Errors?.Clear();
                isSearchInfoValid = ModelState.IsValid;
            }

            List<Exception> exList = new List<Exception>();

            if (!isSearchInfoValid)
                exList.Add(new Exception("Invalid ModelState Item."));

            FlightBookingModel flightBookingModel = new FlightBookingModel
            {
                SearchFlightResultViewModel = searchModel
            };

            string tripid = Guid.NewGuid().ToString();
            string sessionNameBooking = Enumeration.SessionName.FlightBooking + tripid;
            string sessionNameAffiliationId = Enumeration.SessionName.AffiliationId + tripid;
            Session[sessionNameBooking] = flightBookingModel;
            Session[sessionNameAffiliationId] = searchModel.AffiliationId;
            //flightBookingModel = (FlightBookingModel)Session[sessionNameBooking];

            //ViewBag.tripid = tripid;

            try
            {
                bool IsSkyscanner = dbCtx.Affiliations.FirstOrDefault(x => x.UserCode == searchModel.AffiliationId)?.Description.ToLower().Contains("skyscanner") ?? false;
                bool IsKayak = dbCtx.Affiliations.FirstOrDefault(x => x.UserCode == searchModel.AffiliationId)?.Description.ToLower().Contains("kayak") ?? false;

                if (IsSkyscanner || IsKayak)
                {
                    var waiveProcessingFee = dbCtx.PromoCodeRules.FirstOrDefault(x => x.PromoCode == "SS15");
                    searchModel.PromoCode = waiveProcessingFee?.PromoCode;
                    searchModel.PromoId = waiveProcessingFee?.PromoID ?? 0;
                }

                if (step == "s2")
                {
                    info = "Search";
                }
                else if (step == "s3")
                {
                    info = "GuestDetails";

                    string source = Request.QueryString["source"];
                    string segmentString = Request.QueryString["segments"];

                    List<FlightSegmentModels> segmentModel = ToFlightSegmentModels(segmentString, searchModel);
                    // Flight search result
                    flightResponse result = CompareToolServiceCall.RequestFlight(searchModel);

                    flightData[] filteredBySupplier = result?.FlightData?.Where(x => x.ServiceSource.ToString() == source)?.ToArray() ?? new flightData[] { };

                    #region Testing Usage - Need to comment disable when start use (Current Enabled)
                    // START - This part for testing purpose only, take 1 odo from result, and update to "segments"
                    //segmentModel = filteredBySupplier.Take(1).SelectMany(x =>
                    //{
                    //    List<FlightSegmentModels> _seg = new List<FlightSegmentModels>();
                    //    bool isOut = true;
                    //    foreach (var seg in x.pricedItineryModel.OriginDestinationOptions)
                    //    {
                    //        int i = 0;
                    //        foreach (var item in seg.FlightSegments)
                    //        {
                    //            _seg.Add(new FlightSegmentModels
                    //            {
                    //                ori = item.DepartureAirportLocationCode,
                    //                des = item.ArrivalAirportLocationCode,
                    //                Class = item.ResBookDesigCode,
                    //                airline_Code = item.AirlineCode,
                    //                flight_No = item.FlightNumber,
                    //                isOutBoundSeg = isOut,
                    //                departure_Date = item.DepartureDateTime,
                    //                index = i++,
                    //            });
                    //        }
                    //        isOut = false;
                    //    }
                    //    return _seg;
                    //}).ToList();
                    // END - This part for testing purpose only, take 1 odo from result, and update to "segments"
                    #endregion

                    flightData[] matchedFlight = MatchFlight(filteredBySupplier, segmentModel?.ToArray() ?? new FlightSegmentModels[] { });
                    int matchFltLen = matchedFlight.Length;

                    if (matchFltLen <= 0)
                    {
                        throw new Exception("Didn't found any matched flight.");
                    }
                    if (matchFltLen > 1)
                    {
                        throw new Exception(string.Format("There having more than {0} flights.", matchFltLen));
                    }

                    var matchedResult = matchedFlight.FirstOrDefault();
                    serviceSource srvSrc = matchedResult.ServiceSource;

                    Mapper.Initialize(cfg =>
                    {
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.PricedItineryModel, Alphareds.Module.SabreWebService.SWS.PricedItineryModel>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.OriginDestinationOption, Alphareds.Module.SabreWebService.SWS.OriginDestinationOption>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.AirItineraryPricingInfo, Alphareds.Module.SabreWebService.SWS.AirItineraryPricingInfo>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.FlightSegmentStop, Alphareds.Module.SabreWebService.SWS.FlightSegmentStop>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.FlightSegment, Alphareds.Module.SabreWebService.SWS.FlightSegment>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.FareBreakDown, Alphareds.Module.SabreWebService.SWS.FareBreakDown>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.BaggageInformation, Alphareds.Module.SabreWebService.SWS.BaggageInformation>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.FareBasisCode, Alphareds.Module.SabreWebService.SWS.FareBasisCode>();
                        cfg.CreateMap<Alphareds.Module.CompareToolWebService.CTWS.FareInfo, Alphareds.Module.SabreWebService.SWS.FareInfo>();
                    });

                    var supplierFltInfo = Mapper.Map<Alphareds.Module.SabreWebService.SWS.PricedItineryModel>(matchedResult.pricedItineryModel);

                    _flightBookingModel.FlightInformation = new FlightInformation
                    {
                        Supplier = srvSrc,
                        SupplierFlightInfo = supplierFltInfo,
                        AvaSSR = null
                    };

                    List<FlightAvailableSSR> avaSSR = null;
                    //Get Available Baggage - AA/TCG/FRFY
                    if (srvSrc == serviceSource.AirAsia || srvSrc == serviceSource.TCG || srvSrc == serviceSource.Firefly)
                    {
                        avaSSR = Alphareds.Module.BookingController.BookingServiceController
                                        .GetAvailableSSR(searchModel
                                        , supplierFltInfo
                                        , srvSrc, "MYR", General.Utilities.GetClientIP);
                    }

                    _flightBookingModel.FlightInformation.AvaSSR = avaSSR;
                    //Session[sessionFlightBooking] = model;

                    List<TravellerDetail> trvDtl = new List<TravellerDetail>();
                    PromoCodeRule promocoderule = searchModel.IsPromoCodeUsed ? new FlightController(this.ControllerContext).GetPromoCodeDiscountRule(searchModel, dbCtx) : null;
                    bool isPromoFlightSupplier = new FlightController(this.ControllerContext).isPromoFlightList(promocoderule, searchModel, srvSrc);
                    decimal ttlAmtDiscount = isPromoFlightSupplier ? new FlightController(this.ControllerContext).CalcPromoCodeDiscount(promocoderule, supplierFltInfo.PricingInfo.TotalBeforeTax) : 0m;


                    #region Flight Verify Step after Selected Flight - As discussed earlier with KC.
                    //string postBackViewPath = "~/Views/Flight/Search.cshtml";
                    string serializeVerifyRq = string.Empty;
                    string serializeVerifyRs = string.Empty;
                    //string errMsg = string.Empty;
                    string sessionID = string.Empty;
                    string currency = matchedResult.pricedItineryModel.PricingInfo.Currency ?? "MYR";

                    Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse rs = new Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse();

                    _flightBookingModel.SearchFlightResultViewModel = searchModel;
                    _flightBookingModel.FlightSearchResultViewModel = _flightBookingModel.FlightSearchResultViewModel
                                    ?? new FlightSearchResultViewModel();
                    _flightBookingModel.FlightSearchResultViewModel.FullFlightSearchResult = _flightBookingModel.FlightSearchResultViewModel.FullFlightSearchResult
                                                                               ?? new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();

                    switch (srvSrc)
                    {
                        #region Sabre - Not in use, Direct proceed to next step, no need verify again, as already flight matching in affiliation program
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.SACS:

                            break;
                        #endregion

                        #region AirAsia - Not in use, Not open search AirAsia flight from Skyscanner/Kayak
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia:
                            //#region Login - To get Session ID
                            //sessionID = AAServiceCall.Logon();

                            //if (string.IsNullOrEmpty(sessionID))
                            //{
                            //    rs.Header = UtilitiesService.GenerateSabreErrorHeader("AirAsia", "AirAsia - No session ID return");
                            //    break;

                            //    throw new Exception(string.Format("[AffiliateProgram] - {0} : No session ID return.", srvSrc));
                            //}
                            //#endregion

                            //#region Get Availability Verify
                            //Alphareds.Module.AAWebService.AAWS.GetAvailabilityRequest getAvailabitlityRq = ServiceMapper.AAServiceMapper.GetAvailabilityRequestMap(searchModel, sessionID, currency, General.Utilities.GetClientIP);
                            //Alphareds.Module.AAWebService.AAWS.GetAvailabilityResponse getAvailabitlityRs = AAServiceCall.GetAvailabilityVerify(getAvailabitlityRq);

                            //if (getAvailabitlityRs == null || getAvailabitlityRs.Errors?.ErrorMessage != null)
                            //{
                            //    //rs.Header = UtilitiesService.GenerateSabreErrorHeader("AA", getAvailabitlityRs.Errors?.ErrorMessage);
                            //    //ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                            //    //return View(postBackViewPath, model);

                            //    throw new Exception(string.Format("[AffiliateProgram] - {0} : Verification failed.", srvSrc));
                            //}
                            //else
                            //{
                            //    serializeVerifyRq = JsonConvert.SerializeObject(getAvailabitlityRq);
                            //    serializeVerifyRs = JsonConvert.SerializeObject(getAvailabitlityRs);
                            //}
                            //#endregion
                            break;
                        #endregion

                        #region BritishAirways
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.BritishAirways:
                            #region BA GetAvailability Rq/Rs - Refresh Cache to Latest Result
                            Alphareds.Module.BAWebService.BAWS.GetFlightAvailabilityRQ BAGetFlightAvailabilityRQ = ServiceMapper.BAServiceMapper.MapBAGetFlightAvailabilityRequest(searchModel);
                            Alphareds.Module.BAWebService.BAWS.AirShoppingRS BAGetFlightAvailabilityRS = BAServiceCall.getFlightResp(BAGetFlightAvailabilityRQ);
                            #endregion

                            #region BA VerifyFlight Rq/Rs
                            Alphareds.Module.BAWebService.BAWS.VerifyFlightRQ BAVerifyFlightRQ = ServiceMapper.BAServiceMapper.MapBAVerifyRequest(_flightBookingModel);
                            Alphareds.Module.BAWebService.BAWS.VerifyFlightRS BAVerifyFlightRS = BAServiceCall.getFlightResp(BAVerifyFlightRQ);

                            if (BAVerifyFlightRS.IsAvailableFlight == "false" || BAVerifyFlightRS.IsAvailableFlight == "false")
                            {
                                //rs.Header = UtilitiesService.GenerateSabreErrorHeader("BA", "No matched flight found");
                                //ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                //return View(postBackViewPath, model);

                                throw new Exception(string.Format("[AffiliateProgram] - {0} : Verification failed.", srvSrc));
                            }
                            else
                            {
                                serializeVerifyRq = JsonConvert.SerializeObject(BAVerifyFlightRQ);
                                serializeVerifyRs = JsonConvert.SerializeObject(BAVerifyFlightRS);
                            }
                            #endregion
                            break;
                        #endregion

                        #region TCG
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.TCG:
                            #region TCG Search Rq/Rs
                            TCGSearchModel.SearchRequest tcgSearchRq = new TCGSearchModel.SearchRequest();
                            TCGSearchModel.SearchResponse tcgSearchRs = new TCGSearchModel.SearchResponse();

                            tcgSearchRq = ServiceMapper.TCGServiceMapper.MapTCGSearchRequest(searchModel);
                            tcgSearchRs = TCGServiceCall.TCGSearch(tcgSearchRq);

                            if (tcgSearchRs.routings == null && tcgSearchRs.routings.Count == 0)
                            {
                                //rs.Header = UtilitiesService.GenerateSabreErrorHeader("TCG", "TCG Search Response Error - No flight result");
                                //break;

                                throw new Exception(string.Format("[AffiliateProgram] - {0} : No flight result.", srvSrc));
                            }
                            #endregion

                            #region TCG Verify Rq/Rs
                            TCGVerifyModel.VerifyRequest tcgVerifyRq = new TCGVerifyModel.VerifyRequest();
                            TCGVerifyModel.VerifyResponse tcgVerifyRs = new TCGVerifyModel.VerifyResponse();

                            tcgVerifyRq = ServiceMapper.TCGServiceMapper.MapTCGVerificationRequest(tcgSearchRs, supplierFltInfo, searchModel);
                            if (tcgVerifyRq.routing != null)
                            {
                                tcgVerifyRs = TCGServiceCall.TCGVerify(tcgVerifyRq);
                            }
                            else
                            {
                                //rs.Header = UtilitiesService.GenerateSabreErrorHeader("TCG", "TCG Verify Request Error - No match flight found.");
                                //ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                //return View(postBackViewPath, model);

                                throw new Exception(string.Format("[AffiliateProgram] - {0} : No matched flight.", srvSrc));
                            }

                            if (tcgVerifyRs.msg != "success" && tcgVerifyRs.status != 0)
                            {
                                //rs.Header = UtilitiesService.GenerateSabreErrorHeader("TCG", "TCG Verify Response Error - Error Code: " + tcgVerifyRs.status + " - " + tcgVerifyRs.msg);
                                //ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                //return View(postBackViewPath, model);

                                throw new Exception(string.Format("[AffiliateProgram] - {0} : Verification failed.", srvSrc));
                            }
                            else
                            {
                                serializeVerifyRq = JsonConvert.SerializeObject(tcgVerifyRq);
                                serializeVerifyRs = JsonConvert.SerializeObject(tcgVerifyRs);
                            }
                            #endregion
                            break;
                        #endregion

                        #region FireFly
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.Firefly:
                            #region Login -To Get Session ID
                            sessionID = FYServiceCall.Login();
                            if (string.IsNullOrEmpty(sessionID))
                            {
                                //rs.Header = UtilitiesService.GenerateSabreErrorHeader("FRFY", "Login: No Session ID Returned.");
                                //break;

                                throw new Exception(string.Format("[AffiliateProgram] - {0} : No session ID return.", srvSrc));
                            }
                            #endregion

                            #region Get Availability Rq/Rs
                            Alphareds.Module.FYWebservice.FYWS.AvailabilityRequest fyAvailabilityRq = ServiceMapper.FYServiceMapper.MapAvailabilityRequest(searchModel, sessionID, currency);
                            Alphareds.Module.FYWebservice.FYWS.AvailabilityResponse fyAvailabitlityRs = FYServiceCall.GetAvailability(fyAvailabilityRq);

                            if (fyAvailabitlityRs.Error != null || fyAvailabitlityRs.Result?.Schedules[0][0].Journeys.Length == 0)
                            {
                                //if (fyAvailabitlityRs.Error != null)
                                //    errMsg = "GetAvailability Response Method: " + fyAvailabitlityRs.Error + ".";
                                //else if (fyAvailabitlityRs.Result.Schedules[0][0].Journeys.Length == 0)
                                //    errMsg = "GetAvailability Response Method: No Response Returned.";
                                //else
                                //    errMsg = "GetAvailability Response Method: Unknown Errors.";

                                //rs.Header = UtilitiesService.GenerateSabreErrorHeader("FRFY", errMsg);
                                //break;

                                throw new Exception(string.Format("[AffiliateProgram] - {0} : No flight result.", srvSrc));
                            }

                            bool isReturn = supplierFltInfo.DirectionInd.ToString() == PricedItineryModel.DirectionIndType.Return.ToString();
                            int scheduleDetailLength = isReturn ? 2 : 1;
                            List<Alphareds.Module.FYWebservice.FYWS.Journey1> matchedScheduleDetails = ServiceMapper.FYServiceMapper.GetMatchedFlight(supplierFltInfo, fyAvailabitlityRs);

                            if (matchedScheduleDetails.Count > scheduleDetailLength)
                            {
                                //ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                //return View(postBackViewPath, model);

                                throw new Exception(string.Format("[AffiliateProgram] - {0} : No matched flight.", srvSrc));
                            }
                            else
                            {
                                serializeVerifyRq = JsonConvert.SerializeObject(fyAvailabilityRq);
                                serializeVerifyRs = JsonConvert.SerializeObject(fyAvailabitlityRs);
                            }
                            #endregion
                            break;
                        #endregion

                        #region Default
                        default:
                            //rs.Header = UtilitiesService.GenerateSabreErrorHeader("Unknown Source", "Unknown Service Source");
                            break;
                            #endregion
                    }
                    #endregion

                    //Check Session Exist
                    CheckoutProduct checkoutProduct = (CheckoutProduct)Alphareds.Module.Common.Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid) ?? new CheckoutProduct();
                    checkoutProduct.IsRegister = false; // reset last option
                    checkoutProduct.ImFlying = false;
                    checkoutProduct.RequireInsurance = false;
                    checkoutProduct.PromoID = searchModel.PromoId;
                    ProductFlight prdFlight = new ProductFlight()
                    {
                        ProductSeq = 1,
                        FlightInfo = new FlightInformation
                        {
                            Supplier = srvSrc,
                            SupplierFlightInfo = supplierFltInfo,
                            AvaSSR = avaSSR
                        },
                        SearchFlightInfo = searchModel,
                        ContactPerson = null,
                        TravellerDetails = trvDtl,

                        BookSeatInformation_Supplier_Request_Json = string.Empty,
                        BookSeatInformation_Supplier_Response_Json = string.Empty,
                        Temp_Booking_Info = string.Empty,

                        FlightVerify_Request_Json = serializeVerifyRq,
                        FlightVerify_Response_Json = serializeVerifyRs,

                        PricingDetail = new ProductPricingDetail()
                        {
                            Ttl_MarkUp = supplierFltInfo.PricingInfo.FareBreakDown.Sum(x => x.ServiceFee * x.PassengerTypeQuantity),
                            Currency = "MYR",
                            Discounts = promocoderule != null ? new List<DiscountDetail>()
                            {
                                new DiscountDetail {
                                    DiscType = DiscountType.CODE,
                                    Disc_Amt = ttlAmtDiscount,
                                    Disc_Desc = searchModel.PromoCode,
                                    PrdType = ProductTypes.Flight,
                                    Seq = 1
                                }
                            } : new List<DiscountDetail>(),
                            Items = supplierFltInfo.PricingInfo.FareBreakDown.Select(x =>
                            {
                                if (checkoutProduct?.Flight == null)
                                {
                                    for (int i = 0; i < x.PassengerTypeQuantity; i++)
                                    {
                                        trvDtl.Add(new TravellerDetail
                                        {
                                            _DepartureDate = searchModel.BeginDate ?? DateTime.Now,
                                            PassengerType = x.PassengerTypeCode,
                                        });
                                    }
                                }
                                return new ProductItem()
                                {
                                    Supplier_TotalAmt = x.CostTotalAfterTax * x.PassengerTypeQuantity,
                                    BaseRate = x.TotalBeforeTax,
                                    GST = x.GoodsAndServicesTax == 0 ? x.TaxBreakDown.Where(a =>
                                                (a.Key.IsStringSame("D8") || a.Key.IsStringSame("GST")) && a.Value >= 0)
                                                .Sum(a => a.Value) : x.GoodsAndServicesTax,
                                    ItemDetail = x.PassengerTypeCode,
                                    ItemQty = x.PassengerTypeQuantity,
                                    Surcharge = x.TaxBreakDown.Where(a =>
                                                !a.Key.IsStringSame("D8") && !a.Key.IsStringSame("GST") && a.Value >= 0)
                                                .Sum(a => a.Value)
                                };
                            }).ToList(),
                            Sequence = 1
                        }
                    };
                    Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                    checkoutProduct.RemoveProduct(ProductTypes.Flight);
                    checkoutProduct.InsertProduct(prdFlight);

                    return RedirectToAction("GuestDetails", "Checkout", new { tripid, affiliationId = flightAP.affiliationId });
                }
            }
            catch (AggregateException _ae)
            {
                exList.AddRange(_ae.InnerExceptions);
            }
            catch (Exception ex)
            {
                exList.Add(ex);
            }

            // Error hanlde section
            if (exList.Count == 0)
            {
                //Check if no step info return from skyscanner/kayak, directly return to homepage
                return (string.IsNullOrEmpty(info) ?
                    RedirectToAction("Index", "Home")
                    : RedirectToAction(info, "Flight", new { tripid, affiliationId = flightAP.affiliationId }));
            }
            else
            {
                /* 
                 * Requirement
                 * If any field validation fails, email PIC from Alphareds with the fields value sample, then redirect to Home Page (step 1)
                 * [not confirm]
                 */
                AggregateException ae = new AggregateException(exList);
                string addMsg = string.Join("<br/>", ae.InnerExceptions.Select(x => x.Message));

                string modelErrItem = string.Join(Environment.NewLine, ModelState.GetModelErrors());
                string mailContent = ErrorMailTemplate(Request.Url.AbsoluteUri, modelErrItem.Replace(Environment.NewLine, "<br/>"), addMsg);
                string mailToPIC = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("AffiliateFlightPIC");

                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Log(LogLevel.Error, ae, "Mayflower Affiliate Program Error"
                    + Environment.NewLine + Environment.NewLine + modelErrItem
                    + Environment.NewLine + Environment.NewLine + JsonConvert.SerializeObject(flightAP, Formatting.Indented, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore })
                    + Environment.NewLine + Environment.NewLine + addMsg
                    + Environment.NewLine + Environment.NewLine + "Url: " + Request?.Url?.ToString());

                bool isSendSuccess = CommonServiceController.SendEmail(mailToPIC, "Mayflower Affiliate Program Error", mailContent, null, true);

                // Either redirect to Skyscanner or Home Page (not confirm)
                return RedirectToAction("Index", "Home");
            }
        }

        public async Task<ActionResult> Hotel(AffiliateHotelSearch hotelSearch, AffiliateHotelSelect hotelSelect)
        {
            HotelController hc = new HotelController(this.ControllerContext);

            IEnumerable<string> hotelList = new List<string>();
            List<string> hotelId = new List<string>();
            List<string> supplier = new List<string>();
            IEnumerable<string> hsupplier = new List<string>();
            if (!string.IsNullOrWhiteSpace(hotelSelect.Hotel))
            {
                hotelList = hotelSelect.Hotel.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                hotelList = hotelList.Distinct();
                string[] separatingChars = { "EAN", "TP", "JAC", "HB", "RAP" };

                foreach (var hotel in hotelList)
                {
                    var supplierCode = separatingChars.FirstOrDefault(s => hotel.ToLower().StartsWith(s.ToLower())) ?? "EAN"; // default use EAN, incase any error
                    string _splitSupplierHotelId = hotel;
                    string appenderForHotelId = null;

                    if (hotel.ToLower().StartsWith(supplierCode.ToLower()))
                    {
                        // Remove trivago passback prefix HotelSupplier
                        _splitSupplierHotelId = _splitSupplierHotelId.Remove(0, supplierCode.Length);
                    }

                    if (supplierCode == "TP")
                    {
                        /* For Tourplan need Frontend append subfix with 17 length, last with ("?" x 6)
                         * ex. TPKULACIBISKL --> TPKULACIBISKL??????
                         */
                        int lengthTourplanHotelId = 17;
                        for (int i = 0; i < lengthTourplanHotelId - _splitSupplierHotelId.Length; i++)
                        {
                            appenderForHotelId += "?";
                        }
                    }

                    hotelId.Add(_splitSupplierHotelId + appenderForHotelId);
                    supplier.Add(supplierCode);
                }

                hsupplier = supplier.Distinct();
            }

            string tripid = Guid.NewGuid().ToString();

            SearchHotelModel searchHotel = new SearchHotelModel
            {
                Destination = hotelSelect.Destination,
                ArrivalDate = hotelSelect.ArrivalDate.ToDateTime(),
                DepartureDate = hotelSelect.DepartureDate.ToDateTime(),
                NoOfRoom = hotelSelect.NoOfRoom,
                NoOfAdult = hotelSelect.NoOfAdult,
                NoOfInfant = hotelSelect.NoOfInfant,
                PromoCode = hotelSelect.PromoCode,
                Star = hotelSelect.Star,
                AffiliateID = hotelSelect.AffiliationId,
                AffiliationRef = hotelSearch.AffiliationRef,
                CustomerUserAgent = Request.UserAgent,
                CustomerSessionId = tripid,
                CurrencyCode = "MYR",
                CustomerIpAddress = Request.UserHostAddress,
                SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
                {
                    Expedia = hsupplier.Contains("EAN") ? true : false,
                    Tourplan = hsupplier.Contains("TP") ? true : false,
                    JacTravel = hsupplier.Contains("JAC") ? true : false,
                    HotelBeds = hsupplier.Contains("HB") ? true : false,
                    ExpediaTAAP = hsupplier.Contains("TAAP") ? true : false,
                    EANRapid = hsupplier.Contains("RAP")
                },
            };

            SearchRoomModel searchRoom = new SearchRoomModel
            {
                ArrivalDate = searchHotel.ArrivalDate,
                DepartureDate = searchHotel.DepartureDate,
                CustomerUserAgent = searchHotel.CustomerUserAgent,
                CustomerSessionId = searchHotel.CustomerSessionId,
                CurrencyCode = searchHotel.CurrencyCode,
                CustomerIpAddress = searchHotel.CustomerIpAddress,
                HotelID = hotelId.FirstOrDefault(),
            };

            if (hotelSelect.AffiliationId?.Length > 0 && hotelSelect?.Hotel?.Length > 0)
            {
                HotelSearchCookie HotelSearchcookie = new HotelSearchCookie
                {
                    HotelID = hotelSelect.Hotel,
                    AffiliateID = hotelSelect.AffiliationId,
                    TripID = tripid,
                    lastsearch = DateTime.Now
                };
                TrackAffiliateSearchCookie(HotelSearchcookie);
            }

            Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse res = null;
            Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityResponse roomres = null;

            ModelState.Clear();

            var isValid = TryValidateModel(searchHotel) && !string.IsNullOrWhiteSpace(hotelSelect.AffiliationId);

            if (isValid)
            {
                if (hotelId.Count > 0)
                {
                    res = await hc.getHotelFromEBSSearchModel(searchHotel, hotelId.ToList());
                }
                else
                {
                    res = await hc.getHotelFromEBSSearchModel(searchHotel);
                }

                searchHotel.Result = res;
                if (searchHotel.Result?.HotelList?.Length > 0 && hotelId.Count == 1)
                {
                    roomres = ESBHotelServiceCall.GetRoomAvailability(searchRoom, searchHotel);
                    searchRoom.Result = roomres;
                }

                // Set Session Here
                Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, searchHotel);
                Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + searchRoom.HotelID, searchRoom);
                Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, null);
                Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.UpdateFilterHotelResult, tripid, null);
                Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.HotelList, tripid, searchHotel); // required for view room

                return RedirectToAction("Search", "Hotel", new { affiliationId = hotelSelect.AffiliationId, tripid, autoexpand = 1 });
            }
            else
            {
                return RedirectToAction("NotFound", "Error", new { err = "invalid-param" });
            }
        }

        public ActionResult GetPromoHotel(AffiliateHotelSearch hotelSearch)
        {
            string tripid = Guid.NewGuid().ToString();
            HotelController hc = new HotelController(this.ControllerContext);

            SearchHotelModel searchHotel = new SearchHotelModel
            {
                Destination = hotelSearch.Destination,
                ArrivalDate = hotelSearch.ArrivalDate.ToDateTime(),
                DepartureDate = hotelSearch.DepartureDate.ToDateTime(),
                NoOfRoom = hotelSearch.NoOfRoom,
                NoOfAdult = hotelSearch.NoOfAdult,
                NoOfInfant = hotelSearch.NoOfInfant,
                PromoCode = hotelSearch.PromoCode,
                Star = hotelSearch.Star,
                CustomerUserAgent = Request.UserAgent,
                CustomerSessionId = tripid,
                CurrencyCode = "MYR",
                CustomerIpAddress = Request.UserHostAddress,
                SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
                {
                    Expedia = CustomPrincipal.IsAgent ? Alphareds.Module.Common.Core.SearchSupplierSetting.B2B.ExpediaEnabled :
                                Alphareds.Module.Common.Core.SearchSupplierSetting.B2C.ExpediaEnabled,
                    Tourplan = CustomPrincipal.IsAgent ? Alphareds.Module.Common.Core.SearchSupplierSetting.B2B.TourplanEnabled :
                                Alphareds.Module.Common.Core.SearchSupplierSetting.B2C.TourplanEnabled,
                    JacTravel = CustomPrincipal.IsAgent ? Alphareds.Module.Common.Core.SearchSupplierSetting.B2B.JacTravelEnabled :
                                Alphareds.Module.Common.Core.SearchSupplierSetting.B2C.JacTravelEnabled,
                    HotelBeds = CustomPrincipal.IsAgent ? Alphareds.Module.Common.Core.SearchSupplierSetting.B2B.HotelBedsEnabled :
                                Alphareds.Module.Common.Core.SearchSupplierSetting.B2C.HotelBedsEnabled,
                    ExpediaTAAP = CustomPrincipal.IsAgent ? Alphareds.Module.Common.Core.SearchSupplierSetting.B2B.ExpediaTAAPEnabled :
                                Alphareds.Module.Common.Core.SearchSupplierSetting.B2C.ExpediaTAAPEnabled,
                    EANRapid = CustomPrincipal.IsAgent ? Alphareds.Module.Common.Core.SearchSupplierSetting.B2B.EANRapidEnabled :
                                Alphareds.Module.Common.Core.SearchSupplierSetting.B2C.EANRapidEnabled,
                },
            };

            // Set Session Here
            Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, searchHotel);
            Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, null);
            Alphareds.Module.Common.Core.SetSession(Enumeration.SessionName.UpdateFilterHotelResult, tripid, null);

            return RedirectToAction("Search", "Hotel", new { tripid });
        }

        public flightData[] MatchFlight(flightData[] oriFlightArray, FlightSegmentModels[] segments)
        {
            var _tempMatchedFlight = oriFlightArray.Where(x => x.pricedItineryModel.OriginDestinationOptions.SelectMany(seg => seg.FlightSegments).Count() == segments.Length)
                .AsEnumerable();

            var outBoundSeg = segments.Where(x => x.isOutBoundSeg).ToArray();
            var inBoundSeg = segments.Where(x => !x.isOutBoundSeg).ToArray();

            _tempMatchedFlight = _tempMatchedFlight.Where(x =>
            {
                bool outBound = true;

                List<bool> outMatch = new List<bool>();
                List<bool> inMatch = new List<bool>();

                foreach (var odo in x.pricedItineryModel.OriginDestinationOptions)
                {
                    int length = outBound ? outBoundSeg.Length : inBoundSeg.Length;

                    if (odo.FlightSegments.Length == length)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            var odoSeg = odo.FlightSegments[i];
                            var seg = outBound ? outBoundSeg[i] : inBoundSeg[i];

                            var flightNo = odoSeg.FlightNumber.Trim();
                            var trimZero = flightNo.TrimStart('0');
                            var odoFlightNo = flightNo.Length > 0 ? trimZero : "0";

                            var _segFlightNo = seg.flight_No.Trim();
                            var segTrimZero = _segFlightNo.TrimStart('0');
                            var segFlightNo = _segFlightNo.Length > 0 ? segTrimZero : "0";

                            var _match = odoSeg.DepartureAirportLocationCode == seg.ori &&
                            odoSeg.ArrivalAirportLocationCode == seg.des &&
                            odoSeg.ResBookDesigCode == seg.Class &&
                            odoSeg.AirlineCode == seg.airline_Code &&
                            odoFlightNo == segFlightNo &&
                            odoSeg.DepartureDateTime.ToString("yyyyMMddHHmm") == seg.departure_Date.ToString("yyyyMMddHHmm");

                            if (outBound)
                                outMatch.Add(_match);
                            else
                                inMatch.Add(_match);
                        }
                    }
                    else
                    {
                        if (outBound)
                            outMatch.Add(false);
                        else
                            inMatch.Add(false);
                    }

                    // After looping end, if got then change to false, for inBound usage
                    outBound = x.pricedItineryModel.OriginDestinationOptions.Length <= 1;
                }

                return !outMatch.Any(t => !t) && !inMatch.Any(t => !t);
            });

            return _tempMatchedFlight.ToArray();
        }

        private int CurrentUserID
        {
            get
            {
                int userid = 0;
                if (User.Identity.IsAuthenticated)
                {
                    int.TryParse(User.Identity.Name, out userid);
                }
                else if (Session["RegisteredUserId"] != null)
                {
                    userid = (int)Session["RegisteredUserId"];
                }
                return userid;
            }
        }

        private List<FlightSegmentModels> ToFlightSegmentModels(string segmentString, SearchFlightResultViewModel searchFlt)
        {
            List<FlightSegmentModels> segmentModel = new List<FlightSegmentModels>();

            //Split segmentString into array
            string[] segmemntList = segmentString.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            //Detect segment for mobile app which is required to use '-' instead of '|'
            if (segmentString.Contains("-"))
            {
                string[] segmemntList2 = segmentString.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                segmemntList = segmemntList2;
            }

            segmemntList.ForEach(x =>
            {
                string[] segment = x.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                // segments = KULSIN_O_MH_888_yyyyMMddHHmm
                switch (segment.Length - 1)
                {
                    case (0): // cabin class
                        throw new Exception("No Cabin Class Found.");
                    case (1): // airline code
                        throw new Exception("No Airline Code Found.");
                    case (2): // airline no.
                        throw new Exception("No Airline No.");
                    case (3): // segment departure time
                        throw new Exception("No Departure Time Information.");
                    case (4): // length valid, then verify format.
                        if (segment[0].Length != 6)
                            throw new Exception("Invalid Origin & Destination length. Format required XXXYYY. (ex. KULSIN) Data used: " + segment[0]);
                        if (segment[1].Length != 1)
                            throw new Exception("Invalid Cabin Class. Accept only 1 char(s). Data used: " + segment[1]);
                        if (segment[2].Length >= 5)
                            throw new Exception("Invalid Airline Code. Accept only maximum 5 char(s). Data used: " + segment[2]);

                        DateTime _dpTime = new DateTime();
                        bool validDPTime = DateTime.TryParseExact(segment[4], "yyyyMMddHHmm", new System.Globalization.CultureInfo("en-US"),
                              System.Globalization.DateTimeStyles.AdjustToUniversal, out _dpTime);

                        if (segment[4].Length != 12 || !validDPTime)
                            throw new Exception("Invalid Departure Time Format(white space included). Format required yyyyMMddHHmm. Data used: " + segment[4] + "  Length (" + segment[4].Length + ")");
                        break;
                    default:
                        break;
                }
            });

            int ib = 0;
            int ob = 0;
            bool isOutBound = true;

            //Generate flight segment list
            for (int i = 0; i < segmemntList.Length; i++)
            {
                string[] segment = segmemntList[i].Split('_');
                string oriDes = segment[0];
                string ori = oriDes.Substring(0, 3);
                string des = oriDes.Substring(3, 3);
                string cabinClass = segment[1];
                string airCode = segment[2];
                string flightNo = segment[3];
                DateTime departureDate = segment[4].ToDateTime();

                //DateTime depDate = DateTime.ParseExact(departureDate, formatString, null);

                // Check against true, avoid second inbound looping change to true
                isOutBound = searchFlt.isReturn ? (((ori == searchFlt.ArrivalStationCode || departureDate.Date.AddDays(1) > searchFlt.EndDate?.Date) && isOutBound) ? false : isOutBound) : true;
                segmentModel.Add(new FlightSegmentModels
                {
                    Class = cabinClass,
                    des = des,
                    ori = ori,
                    isOutBoundSeg = isOutBound,
                    index = isOutBound ? ob++ : ib++,
                    airline_Code = airCode,
                    flight_No = flightNo,
                    departure_Date = departureDate,
                });
            }

            return segmentModel;
        }

        private string ErrorMailTemplate(string link, string errorMsg, string additionalMsg = null)
        {
            string html = "";
            #region Html Template
            html += @"<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>
<html xmlns='http://www.w3.org/1999/xhtml'>

<head>
    <meta http-equiv='Content-Type' content='text/html; charset=UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1' />
    <title>Neopolitan Invitation Email</title>
    <!-- Designed by https://github.com/kaytcat -->
    <!-- Robot header image designed by Freepik.com -->
    <style type='text/css'>
        @import url(http://fonts.googleapis.com/css?family=Droid+Sans);
        /* Take care of image borders and formatting */
        
        img {
            max-width: 600px;
            outline: none;
            text-decoration: none;
            -ms-interpolation-mode: bicubic;
        }
        
        a {
            text-decoration: none;
            border: 0;
            outline: none;
            color: #fff;
        }
        
        a img {
            border: none;
        }
        /* General styling */
        
        td,
        h1,
        h2,
        h3 {
            font-family: Helvetica, Arial, sans-serif;
            font-weight: 400;
        }
        
        td {
            text-align: center;
        }
        
        body {
            -webkit-font-smoothing: antialiased;
            -webkit-text-size-adjust: none;
            width: 100%;
            height: 100%;
            color: #37302d;
            background: #ffffff;
            font-size: 16px;
        }
        
        table {
            border-collapse: collapse !important;
        }
        
        .headline {
            color: #ffffff;
            font-size: 36px;
        }
        
        .force-full-width {
            width: 100% !important;
        }
    </style>
    <style type='text/css' media='screen'>
        @media screen {
            /*Thanks Outlook 2013! http://goo.gl/XLxpyl*/
            td,
            h1,
            h2,
            h3 {
                font-family: 'Droid Sans', 'Helvetica Neue', 'Arial', 'sans-serif' !important;
            }
        }
    </style>
    <style type='text/css' media='only screen and (max-width: 480px)'>
        /* Mobile styles */
        
        @media only screen and (max-width: 480px) {
            table[class='w320'] {
                width: 320px !important;
            }
        }
    </style>
</head>

<body class='body' style='padding:0; margin:0; display:block; background:#ffffff; -webkit-text-size-adjust:none' bgcolor='#ffffff'>
    <table align='center' cellpadding='0' cellspacing='0' width='100%' height='100%'>
        <tr>
            <td align='center' valign='top' bgcolor='#ffffff' width='100%'>
                <center>
                    <table style='margin: 0 auto;' cellpadding='0' cellspacing='0' width='600' class='w320'>
                        <tr>
                            <td align='center' valign='top'>
                                <table style='margin: 0 auto;' cellpadding='0' cellspacing='0' width='100%' style='margin:0 auto;'>
                                    <tr>
                                        <td style='font-size: 20px; text-align:center;'>
                                            <br> Mayflower Affiliate Program Error
                                            <br>
                                            <br>
                                        </td>
                                    </tr>
                                </table>
                                <table style='margin: 0 auto;' cellpadding='0' cellspacing='0' width='100%' bgcolor='#4dbfbf'>
                                    <tr>
                                        <td>
                                            <br><img src='https://www.filepicker.io/api/file/XXpJ4RwWQqlVoWN8psfj' width='224' height='240' alt='robot picture'></td>
                                    </tr>
                                    <tr>
                                        <td class='headline'>Error is waiting you.</td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <center>
                                                <table style='margin: 0 auto;' cellpadding='0' cellspacing='0' width='60%'>
                                                    <tr>
                                                        <td style='color:#187272'>
                                                            <br>
                                                            Link used :
                                                            <br>
                                                            <a href='<#link>'><#link></a>
                                                            <br>
                                                        </td>
                                                    </tr>
                                                    <tr>
                                                        <td style='color:#187272;'>
                                                        <br>
                                                            Model Error Message :
                                                            <br>
                                                            <div style='color:#fff;''>
                                                            <#errmsg>	
                                                            </div>
                                                            
                                                            <br> </td>
                                                    </tr>
                                                    <tr>
                                                        <td style='color:#187272;'>
                                                        	<br>
                                                            Add. Msg :
                                                            <div style='color:#fff;''>
                                                            <#addmsg>
                                                            </div>
                                                            <br>
                                                            <br>
                                                            <br> </td>
                                                    </tr>
                                                </table>
                                            </center>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </center>
            </td>
        </tr>
    </table>
</body>

</html>";
            #endregion

            Hashtable ht = new Hashtable();
            ht.Add("<#link>", link);
            ht.Add("<#errmsg>", errorMsg);
            ht.Add("<#addmsg>", additionalMsg ?? "-");

            foreach (var key in ht.Keys)
            {
                html = html.Replace(key.ToString(), ht[key].ToString());
            }

            return html;
        }

        public void TrackAffiliateSearchCookie(HotelSearchCookie hotelviewedcookie)
        {
            List<HotelSearchCookie> cookielist = new List<HotelSearchCookie>();
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            string trackingcookie;
            if (Request.Cookies["AffiliateSearchCookie"] != null)
            {
                trackingcookie = Request.Cookies["AffiliateSearchCookie"].Value;
                cookielist = jsSerializer.Deserialize<List<HotelSearchCookie>>(trackingcookie)
                                .Where(x => x.lastsearch.AddDays(1) > DateTime.Now && x.TripID != hotelviewedcookie.TripID)
                                .ToList();
            }
            cookielist.Add(hotelviewedcookie);
            string myObjectJson = jsSerializer.Serialize(cookielist);
            var cookie = new System.Web.HttpCookie("AffiliateSearchCookie", myObjectJson)
            {
                Expires = DateTime.Now.AddDays(1)
            };
            HttpContext.Response.Cookies.Set(cookie);
        }

        /// <summary>
        /// Web Action set cookies
        /// </summary>
        /// <param name="authToken"></param>
        public void CheckAndSetAuthLoginToken(string authToken, MayFlower db)
        {
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                string decodeToken = null;
                authToken = authToken.Replace(" ", "+");

                if (Alphareds.Module.Cryptography.Cryptography.AES.TryDecrypt(authToken, out decodeToken))
                {
                    dynamic tokenInfo = JsonConvert.DeserializeObject(decodeToken);

                    if (tokenInfo != null && tokenInfo.id != null && tokenInfo.status == true)
                    {
                        int uid = tokenInfo.id;
                        string email = tokenInfo.user;

                        var _user = db.Users.FirstOrDefault(x => x.UserID == uid && x.Email == email);

                        if (_user != null)
                        {
                            LoginClass login = new LoginClass(_user, db);
                            UserData userData = login.UserData;

                            if (login.UserData.IsValidUser)
                            {
                                Alphareds.Module.Common.HttpResponseBaseExtensions.SetAuthCookie(Response, userData.UserId.ToString(), false, userData);
                            }
                        }
                    }

                }
            }
        }

        private string ModifyQueryStringValue(string p_Name, string p_NewValue)
        {
            var nameValues = HttpUtility.ParseQueryString(Request.QueryString.ToString());
            nameValues.Set(p_Name, p_NewValue);
            string url = Request.Url.AbsolutePath;
            string updatedQueryString = nameValues.ToString();
            return updatedQueryString;
        }
    }
}