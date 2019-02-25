using Alphareds.Module.Common;
using Alphareds.Module.MemberController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.ServiceCall;
using AutoMapper;
using Newtonsoft.Json;
using NLog;
using PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using WebGrease.Css.Extensions;
using Alphareds.Module.Flight_Converter;
using Alphareds.Module.SabreWebService.SWS;
using System.Threading.Tasks;

namespace Mayflower.Controllers
{
    [Filters.PreserveQueryStringFilter(QueryString = "tripid, affiliationId")]
    public class FlightController : AsyncController
    {
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);
        private Logger logger { get; set; }

        private string tripid { get; set; }
        private string affiliationId { get; set; }

        private string sessionFlightBooking { get; set; }
        private string sessionFullODOResult { get; set; }
        private string sessionFullFlightSearchResult { get; set; }
        private string sessionFilterParam { get; set; }
        private string sessionMHFilterFlightModel { get; set; }
        private string sessionCheckOut { get; set; }

        private string sessionfareRuleList { get; set; }
        private string sessionAAgetAvRs { get; set; }

        public FlightController()
        {
            logger = NLog.LogManager.GetCurrentClassLogger();
            SetSessionTripId();
            SetAffiliateId();
            AllocatedSessionProperty();
        }

        private void SetSessionTripId()
        {
            var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
            var routeValue = request.RequestContext.RouteData.Values["tripid"];
            string routeString = routeValue?.ToString();
            tripid = System.Web.HttpContext.Current.Request.QueryString["tripid"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["tripid"]);

            var req = Request ?? System.Web.HttpContext.Current.Request.RequestContext.HttpContext.Request;

            if (!string.IsNullOrWhiteSpace(tripid))
            {
                tripid = tripid.Split(',')[0];
            }
            else
            {
                string _queryString = HttpUtility.UrlDecode(System.Web.HttpContext.Current.Request.QueryString.ToString())
                ?.Replace("{", "").Replace("}", "")
                .Replace("\"", "").Replace(":", "=");
                var _parsedQS = HttpUtility.ParseQueryString(_queryString ?? "");

                tripid = _parsedQS["tripid"];
            }
        }

        private void SetAffiliateId()
        {
            var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
            var routeValue = request.RequestContext.RouteData.Values["affiliationId"];
            string routeString = routeValue != null ? routeValue.ToString() : null;

            string obj = System.Web.HttpContext.Current.Request.QueryString["affiliationId"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["affiliationId"]);

            affiliationId = obj;
        }

        private void AllocatedSessionProperty()
        {
            sessionFlightBooking = Enumeration.SessionName.FlightBooking + tripid;
            sessionFullODOResult = Enumeration.SessionName.FullODOResult + tripid;
            sessionFullFlightSearchResult = Enumeration.SessionName.FullFlightSearchResult + tripid;
            sessionFilterParam = Enumeration.SessionName.FilterParam + tripid;
            sessionMHFilterFlightModel = Enumeration.SessionName.MHFilterFlightModel + tripid;
            sessionCheckOut = Enumeration.SessionName.CheckoutProduct + tripid;

            sessionfareRuleList = "fareRuleList" + tripid;
            sessionAAgetAvRs = "aaGetAvRs" + tripid;
        }

        // Hijack controller context from another controller for User principal usage.
        public FlightController(ControllerContext controllerContext)
        {
            this.ControllerContext = controllerContext;
            logger = NLog.LogManager.GetCurrentClassLogger();

            if (System.Web.HttpContext.Current == null)
            {
                //var ttt = new HttpContext()
                System.Web.HttpContext.Current = ControllerContext.HttpContext.ApplicationInstance.Context;
            }

            SetSessionTripId();
            SetAffiliateId();
            AllocatedSessionProperty();
        }

        #region Step 2 - ~/Flight/Search
        public ActionResult Search(int? page, string sort, string size, FilterFlightModel filter, string filterstatus, string tripid)
        {
            if (Session[sessionFlightBooking] == null)
            {
                /*
                 * Instead of return to home page, this section might need change to 
                 * redirect to no flight result page/error page notice that user session time out.
                 */

                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }

            // Get session from POST action at HomeController (Index)
            FlightBookingModel model = (FlightBookingModel)Session[sessionFlightBooking];
            bool isAgent = IsAgentUser;

            if (!Request.IsAjaxRequest())
            {
                return View(model);
            }
            else if (Request.IsAjaxRequest() && model.FlightSearchResultViewModel == null)
            {
                model.FlightSearchResultViewModel = new FlightSearchResultViewModel();

                if (model.SearchFlightResultViewModel == null)
                {
                    return RedirectToAction("Type", "Error", new { id = "session-error" });
                }

                //Get from ESBCom Tool
                Tuple<FlightBookingModel, string> resultFromService = Alphareds.Module.HomeController.HomeServiceController.getSearchFlightResult(model, CurrentUserID, isAgent);

                if (!string.IsNullOrEmpty(resultFromService.Item2))
                {
                    model = resultFromService.Item1;

                    model.FlightSearchResultViewModel = new FlightSearchResultViewModel()
                    {
                        FullFlightSearchResult = new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>()
                    };

                    // 20161209 - Remove session to ensure no previous flight list in filter panel
                    Session.Remove(sessionFullFlightSearchResult);
                    Session.Remove(sessionFullODOResult);
                    Session.Remove(sessionFilterParam);
                    Session.Remove(sessionMHFilterFlightModel);
                }
                else
                {
                    model = resultFromService.Item1;
                    if (resultFromService.Item1.FlightSearchResultViewModel.FullFlightSearchResult.Count == 0)
                    {
                        model.FlightSearchResultViewModel = new FlightSearchResultViewModel()
                        {
                            FullFlightSearchResult = new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>()
                        };
                        Session.Remove(sessionFullFlightSearchResult);
                        Session.Remove(sessionFullODOResult);
                        Session.Remove(sessionFilterParam);
                        Session.Remove(sessionMHFilterFlightModel);
                    }
                    else
                    {
                        //if (!Core.IsEnableB2B)
                        //{
                        //model.FlightSearchResultViewModel.FullFlightSearchResult = FlightSearchServiceController.bindGSTandServiceFeeToFlightResults(model.FlightSearchResultViewModel.FullFlightSearchResult, model.SearchFlightResultViewModel.isDomesticFlight, model.SearchFlightResultViewModel.CabinClass);
                        //}

                        #region Clone Flight Booking Model
                        bool isCloneDumpResultList = false;
                        bool.TryParse(Core.GetAppSettingValueEnhanced("CloneDumpResultList"), out isCloneDumpResultList);
                        if (isCloneDumpResultList)
                        {
                            CloneFlightBookingModel(model);
                        }
                        #endregion
                        if (User.Identity.IsAuthenticated && Core.IsEnablePayByPromoCode && !model.SearchFlightResultViewModel.IsPromoCodeUsed)
                        {
                            MayFlower db = new MayFlower();
                            var user = Alphareds.Module.Common.Core.GetUserInfo(User.Identity.Name, db);
                            int promoID = user.UserPromoes.FirstOrDefault(x => x.IsActive)?.PromoID ?? 0;
                            if (promoID != 0)
                            {
                                string promoCode = db.PromoCodeRules.FirstOrDefault(x => x.PromoID == promoID).PromoCode;
                                model.SearchFlightResultViewModel.PromoCode = promoCode;
                                var promoCodeRule = GetPromoCodeDiscountRule(model.SearchFlightResultViewModel, db);
                                if (promoCodeRule == null)
                                {
                                    model.SearchFlightResultViewModel.PromoCode = string.Empty;
                                    model.SearchFlightResultViewModel.PromoId = 0;
                                }
                            }
                        }
                        //flight only search for auto applied promo rule 
                        if (Core.IsEnablePayByPromoCode && !model.SearchFlightResultViewModel.IsPromoCodeUsed)
                        {
                            MayFlower db = new MayFlower();
                            int Frontendfunc = model.SearchFlightResultViewModel.IsDynamic ? (int)Alphareds.Module.Model.FrontendFunction.Enum.FrontendFunction.PackageAutoApplied : (int)Alphareds.Module.Model.FrontendFunction.Enum.FrontendFunction.FlightAutoApplied;
                            var promoCodeRule = GetPromoCodeFunctionRule(Frontendfunc, model.SearchFlightResultViewModel, db);
                            model.SearchFlightResultViewModel.PromoCode = promoCodeRule != null ? promoCodeRule.PromoCode : null;
                        }
                        #region Promo Code Section 
                        if (Core.IsEnablePayByPromoCode && model.SearchFlightResultViewModel.IsPromoCodeUsed)
                        {
                            try
                            {
                                MayFlower db = new MayFlower();
                                var promoCodeRule = GetPromoCodeDiscountRule(model.SearchFlightResultViewModel, db);

                                CheckoutProduct _checkoutProduct = new CheckoutProduct(); // use for check promo code functions
                                _checkoutProduct.PromoID = promoCodeRule?.PromoID ?? 0;
                                PromoCodeFunctions promoCodeFunctions = _checkoutProduct.PromoCodeFunctions;

                                // Overrided not comming from Affiliate Program
                                if (promoCodeFunctions.GetFrontendFunction.WaiveCreditCardFee)
                                {
                                    string _reffCode = model.SearchFlightResultViewModel.AffiliationId?.ToLower();
                                    var _validAffiliate = db.Affiliations.Any(x => x.UserCode == _reffCode);

                                    if (!_validAffiliate)
                                    {
                                        model.SearchFlightResultViewModel.PromoCode = null;
                                        model.SearchFlightResultViewModel.PromoId = 0;
                                        _checkoutProduct.PromoID = 0;
                                        promoCodeRule = null;
                                    }
                                }

                                if (((promoCodeRule != null && promoCodeRule.IsPackageOnly) || promoCodeFunctions.GetFrontendFunction.PackageAutoApplied) && !model.SearchFlightResultViewModel.IsDynamic)
                                {
                                    _checkoutProduct.PromoID = 0;
                                    promoCodeRule = null;
                                }

                                if (Core.IsEnablePackageDiscount && model.SearchFlightResultViewModel.IsDynamic)
                                {
                                    model.SearchFlightResultViewModel.DPPromoCode = model.SearchFlightResultViewModel.PromoCode;
                                }
                                if (promoCodeRule == null || (Core.IsEnablePackageDiscount && model.SearchFlightResultViewModel.IsDynamic))
                                {
                                    model.SearchFlightResultViewModel.PromoCode = string.Empty;
                                    model.SearchFlightResultViewModel.PromoId = 0;
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, $"Flight Search Promo Code Error - {DateTime.Now.ToLoggerDateTime()}");
                            }
                        }
                        #endregion

                        // Initialize new instance to avoid overwrite
                        List<Alphareds.Module.CompareToolWebService.CTWS.flightData> cloneResult = new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();
                        var preRequestResult = model.FlightSearchResultViewModel.FullFlightSearchResult.OrderBy(o => o.pricedItineryModel.PricingInfo.TotalAfterTax);
                        if (model.FlightSearchResultViewModel != null && model.FlightSearchResultViewModel.FullFlightSearchResult != null)
                        {
                            model.FlightSearchResultViewModel.FullFlightSearchResult = preRequestResult.ToList();
                        }
                        cloneResult = model.FlightSearchResultViewModel.FullFlightSearchResult;
                        Session[sessionFullFlightSearchResult] = cloneResult;
                        Session[sessionFullODOResult] = JsonConvert.DeserializeObject<FlightBookingModel>(JsonConvert.SerializeObject(resultFromService.Item1));
                        Session.Remove(sessionFilterParam);
                    }
                }
            }

            if (Request.IsAjaxRequest() && !string.IsNullOrEmpty(filterstatus) && filter.Airline.Length > 0)
            {
                Session[sessionFilterParam] = filter;

                if (Session[sessionFullFlightSearchResult] != null || filter.Airline.Any(x => string.IsNullOrWhiteSpace(x)))
                {
                    // for reset filter airline list
                    var fullFlightSearchResult = (List<Alphareds.Module.CompareToolWebService.CTWS.flightData>)Session[sessionFullFlightSearchResult];
                    if (model.FlightSearchResultViewModel.FullFlightSearchResult.Count != fullFlightSearchResult.Count)
                    {
                        model.FlightSearchResultViewModel.FullFlightSearchResult = fullFlightSearchResult;
                    }
                }

                // As queryable to defer result, and improve performance
                var preRequestResult = model.FlightSearchResultViewModel.FullFlightSearchResult.AsQueryable();

                int outDepartTimeMin, outArrivalTimeMin, inDepartTimeMin, inArrivalTimeMin = 0;
                int outDepartTimeMax, outArrivalTimeMax, inDepartTimeMax, inArrivalTimeMax = 1440;
                bool isValidOutDepartTimeMin = int.TryParse(filter.OutDepartureTimeMin, out outDepartTimeMin);
                bool isValidOutDepartTimeMax = int.TryParse(filter.OutDepartureTimeMax, out outDepartTimeMax);
                bool isValidOutArrivalTimeMin = int.TryParse(filter.OutArrivalTimeMin, out outArrivalTimeMin);
                bool isValidOutArrivalTimeMax = int.TryParse(filter.OutArrivalTimeMax, out outArrivalTimeMax);
                bool isValidInDepartTimeMin = int.TryParse(filter.InDepartureTimeMin, out inDepartTimeMin);
                bool isValidInDepartTimeMax = int.TryParse(filter.InDepartureTimeMax, out inDepartTimeMax);
                bool isValidInArrivalTimeMin = int.TryParse(filter.InArrivalTimeMin, out inArrivalTimeMin);
                bool isValidInArrivalTimeMax = int.TryParse(filter.InArrivalTimeMax, out inArrivalTimeMax);

                // For prepare future get exceed amount, to reduce query
                //MHFilterFlightModel filterInfo = (MHFilterFlightModel)Session[sessionMHFilterFlightModel];

                // Outbound
                if (outDepartTimeMin != 0 || outDepartTimeMax != 1440)
                {
                    preRequestResult = preRequestResult.Where(x =>
                    x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.First().DepartureDateTime.TimeOfDay.TotalMinutes >= outDepartTimeMin &&
                    x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.First().DepartureDateTime.TimeOfDay.TotalMinutes <= outDepartTimeMax
                    );
                }

                if (outArrivalTimeMin != 0 || outArrivalTimeMax != 1440)
                {
                    preRequestResult = preRequestResult.Where(x =>
                    x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.Last().ArrivalDateTime.TimeOfDay.TotalMinutes >= outArrivalTimeMin &&
                    x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.Last().ArrivalDateTime.TimeOfDay.TotalMinutes <= outArrivalTimeMax
                    );
                }

                if (preRequestResult.Any(x => x.pricedItineryModel.OriginDestinationOptions.Length >= 2))
                {
                    if (inDepartTimeMin != 0 || inDepartTimeMax != 1440)
                    {
                        preRequestResult = preRequestResult.Where(x =>
                        x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.First().DepartureDateTime.TimeOfDay.TotalMinutes >= inDepartTimeMin &&
                        x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.First().DepartureDateTime.TimeOfDay.TotalMinutes <= inDepartTimeMax
                        );
                    }

                    if (inArrivalTimeMin != 0 || inArrivalTimeMax != 1440)
                    {
                        preRequestResult = preRequestResult.Where(x =>
                        x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.Last().ArrivalDateTime.TimeOfDay.TotalMinutes >= inArrivalTimeMin &&
                        x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.Last().ArrivalDateTime.TimeOfDay.TotalMinutes <= inArrivalTimeMax
                        );
                    }
                }

                decimal filterPriceMin = 0m;
                decimal filterPriceMax = 0m;
                bool isValidPriceMin = decimal.TryParse(filter.PriceMin, out filterPriceMin);
                bool isValidPriceMax = decimal.TryParse(filter.PriceMax, out filterPriceMax);
                if (isValidPriceMin && isValidPriceMax)
                {
                    preRequestResult = preRequestResult
                        .Where(x => x.pricedItineryModel.PricingInfo.TotalAfterTax >= Convert.ToDecimal(filter.PriceMin) &&
                        x.pricedItineryModel.PricingInfo.TotalAfterTax <= Convert.ToDecimal(filter.PriceMax));
                }

                if (filter.StopList == null && filterPriceMax != 99999)
                {
                    preRequestResult = preRequestResult
                        .Where(x => false);
                }
                else if (filter.StopList != null)
                {
                    preRequestResult = preRequestResult
                        .Where(x => filter.StopList.Contains(x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.Length - 1 >= 2 ? 2 : x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.Length - 1) &&
                        filter.StopList.Contains(x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.Length - 1 >= 2 ? 2 : x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.Length - 1));
                }

                if (filter.Airline.Any(x => !string.IsNullOrWhiteSpace(x)))
                {

                    List<string> airlineFilter = filter.Airline.SelectMany(x => x.Split('-')).ToList();

                    if (airlineFilter.Count > 1)
                    {
                        preRequestResult = preRequestResult.Where(
                        x => x.pricedItineryModel.OriginDestinationOptions.SelectMany(seg => seg.FlightSegments.Select(a => a.AirlineCode)).Distinct().Count() > 1 &&
                        x.ServiceSource != Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia
                        );
                    }
                    else
                    {
                        //Need change to AirAsia
                        if (airlineFilter.Any(x => x == "AK"))
                        {
                            preRequestResult = preRequestResult.Where(x => x.ServiceSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia);
                        }
                        else
                        {
                            preRequestResult = preRequestResult.Where(
                                x => x.pricedItineryModel.OriginDestinationOptions.SelectMany(seg => seg.FlightSegments.Select(a => a.AirlineCode)).Distinct().Count() == 1
                                && x.pricedItineryModel.OriginDestinationOptions.SelectMany(seg => seg.FlightSegments.Select(a => a.AirlineCode)).Any(c => airlineFilter.Contains(c))
                                );
                        }
                    }
                }

                if (Request.IsAjaxRequest() && !string.IsNullOrEmpty(sort))
                {
                    Session["sortType" + tripid] = sort.ToLower();
                    switch (sort.ToLower())
                    {
                        case "priceasc":
                            preRequestResult = preRequestResult.OrderBy(o => o.pricedItineryModel.PricingInfo.TotalAfterTax);
                            break;
                        case "departuretimeasc":
                            preRequestResult = preRequestResult
                                .OrderBy(o => o.pricedItineryModel.OriginDestinationOptions.First().FlightSegments[0].DepartureDateTime)
                                .ThenBy(o => o.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments[0].DepartureDateTime)
                                .ThenBy(o => o.pricedItineryModel.PricingInfo.TotalAfterTax);
                            break;
                        case "durationasc":
                            if (preRequestResult.Where(x => x.pricedItineryModel.OriginDestinationOptions.Length == 1).Any())
                            {
                                preRequestResult = preRequestResult.OrderBy(o => o.pricedItineryModel.OriginDestinationOptions[0].TotalElapsedTime)
                                    .ThenBy(o => o.pricedItineryModel.PricingInfo.TotalAfterTax);
                            }
                            else
                            {
                                preRequestResult = preRequestResult.OrderBy(o => o.pricedItineryModel.OriginDestinationOptions[0].TotalElapsedTime + o.pricedItineryModel.OriginDestinationOptions[1].TotalElapsedTime)
                                    .ThenBy(o => o.pricedItineryModel.PricingInfo.TotalAfterTax);
                            }
                            break;
                        default:
                            break;
                    }
                }

                // .. assign back filtered result to list
                if (model.FlightSearchResultViewModel != null && model.FlightSearchResultViewModel.FullFlightSearchResult != null)
                {
                    model.FlightSearchResultViewModel.FullFlightSearchResult = preRequestResult.ToList();
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
                }
            }

            #region Mayflower IPagedGroupFullFlightSearchReseult Result
            int pageNumber = (page ?? 1);
            int pageSize = 5;
            int.TryParse(Core.GetAppSettingValueEnhanced("FlightListPageSize"), out pageSize);

            var IPagedModel = model.FlightSearchResultViewModel.GroupFullFlightSearchReseult2.ToPagedList(pageNumber, pageSize);
            model.FlightSearchResultViewModel.IPagedGroupFullFlightSearchReseult = IPagedModel;
            #endregion

            return Request.IsAjaxRequest()
                ? (ActionResult)PartialView("~/Views/Flight/Search/_FlightSearchResultList.cshtml", model)
                : View(model);
        }

        public ActionResult RedirectSearch(string tripid)
        {
            if (Session[sessionFlightBooking] == null)
            {
                /*
                 * Instead of return to home page, this section might need change to 
                 * redirect to no flight result page/error page notice that user session time out.
                 */

                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }
            FlightBookingModel flightBookingModel = (FlightBookingModel)Session[sessionFlightBooking] ?? new FlightBookingModel();
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid) ?? new CheckoutProduct();

            var searchFlightDetails = flightBookingModel.SearchFlightResultViewModel ?? checkout.Flight.SearchFlightInfo;

            tripid = Guid.NewGuid().ToString();
            sessionFlightBooking = Enumeration.SessionName.FlightBooking + tripid;

            FlightBookingModel model = new FlightBookingModel();
            model.SearchFlightResultViewModel = searchFlightDetails;
            Session[sessionFlightBooking] = model;

            return RedirectToAction("Search", "Flight", new { tripid, affiliationId });
        }

        [HttpPost]
        public ActionResult Search(FlightBookingModel model, string outbound, string inbound, string grpTag, string tripid)
        {
            int userid = CurrentUserID;

            if (Session[sessionFlightBooking] != null)
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }
            else
            {
                Session[sessionFlightBooking] = model;
            }

            try
            {
                // 2016/12/07 - 17:35 Merge Remark
                // check is selected result valid or not
                //model.FlightSearchResultViewModel.GroupFullFlightSearchReseult2.FirstOrDefault(x => x.GroupTag == grpTag))

                bool isOneWay = !model.SearchFlightResultViewModel.isReturn;

                // set selected flight here
                int counter = 0;
                List<int> _outboundIndex = new List<int>();
                List<int> _innboundIndex = new List<int>();
                model.FlightSearchResultViewModel = model.FlightSearchResultViewModel
                                                    ?? new FlightSearchResultViewModel();
                model.FlightSearchResultViewModel.FullFlightSearchResult = model.FlightSearchResultViewModel.FullFlightSearchResult
                                                                           ?? new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();

                foreach (var singleResult in model.FlightSearchResultViewModel.FullFlightSearchResult)
                {
                    var item = singleResult.pricedItineryModel;
                    string joinAirlineCode = string.Join("-", item.OriginDestinationOptions.SelectMany(x => x.FlightSegments.Select(s => s.AirlineCode)).Distinct());
                    if (joinAirlineCode + item.PricingInfo.TotalAfterTax.ToString().Replace(".", "") == grpTag)
                    {
                        int outboundSegLen, inboundSegLen = -1;
                        outboundSegLen = outbound.Split(',').Length;

                        var outboundODO = item.OriginDestinationOptions.First();
                        if (outboundSegLen == outboundODO.FlightSegments.Length)
                        {
                            var outSegTagList = item.OriginDestinationOptions.First().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + singleResult.ServiceSource.ToString());
                            bool outIsList = string.Join(",", outSegTagList) == outbound;

                            if (outIsList)
                            {
                                _outboundIndex.Add(counter);
                            }
                        }

                        if (inbound != "" || inbound != null)
                        {
                            inboundSegLen = inbound.Split(',').Length;

                            var inboundODO = item.OriginDestinationOptions.Last();
                            if (inboundSegLen == inboundODO.FlightSegments.Length)
                            {
                                var inSegTagList = item.OriginDestinationOptions.Last().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + singleResult.ServiceSource.ToString());
                                bool inIsList = string.Join(",", inSegTagList) == inbound;

                                if (inIsList)
                                {
                                    _innboundIndex.Add(counter);
                                }
                            }
                        }
                    }
                    counter++;
                }

                int odoIndex = -1;
                bool isPairable = _outboundIndex.Intersect(_innboundIndex).Any();

                if (isPairable || isOneWay)
                {
                    if (_innboundIndex.Count != 0)
                    {
                        odoIndex = _outboundIndex.Intersect(_innboundIndex).First();
                    }
                    else if (_outboundIndex.Count != 0)
                    {
                        odoIndex = _outboundIndex.FirstOrDefault();
                    }
                    else
                    {
                        return RedirectToAction("Search", new { status = "Invalid pairing result.", tripid, affiliationId });
                    }

                    //Twin - 2017/02/03 - Initialize all children and convert to sabre object
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

                    var selectedFlight = model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex];
                    Alphareds.Module.CompareToolWebService.CTWS.serviceSource serviceSource = selectedFlight.ServiceSource;
                    Alphareds.Module.SabreWebService.SWS.PricedItineryModel flightInfo = Mapper.Map<Alphareds.Module.SabreWebService.SWS.PricedItineryModel>(selectedFlight.pricedItineryModel);

                    model.FlightInformation = new FlightInformation
                    {
                        Supplier = serviceSource,
                        SupplierFlightInfo = flightInfo,
                        AvaSSR = null
                    };

                    List<FlightAvailableSSR> avaSSR = null;
                    //Get Available Baggage - AA/TCG/FRFY
                    if (serviceSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia ||
                        serviceSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.TCG ||
                        serviceSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.Firefly)
                    {
                        //avaBaggage = Alphareds.Module.BookingController.BookingServiceController.GetAvailableBaggage(model.SearchFlightResultViewModel, model.FlightInformation.SupplierFlightInfo, serviceSource, "MYR", General.Utilities.GetClientIP);
                        avaSSR = Alphareds.Module.BookingController.BookingServiceController.GetAvailableSSR(model.SearchFlightResultViewModel, model.FlightInformation.SupplierFlightInfo, serviceSource, "MYR", General.Utilities.GetClientIP);
                    }

                    model.FlightInformation.AvaSSR = avaSSR;
                    Session[sessionFlightBooking] = model;

                    #region Flight Verify Step after Selected Flight - As discussed earlier with KC.
                    SearchFlightResultViewModel searchModel = model.SearchFlightResultViewModel;
                    string postBackViewPath = "~/Views/Flight/Search.cshtml";
                    string serializeVerifyRq = string.Empty;
                    string serializeVerifyRs = string.Empty;
                    string errMsg = string.Empty;
                    string sessionID = string.Empty;
                    string currency = flightInfo.PricingInfo.Currency ?? "MYR";

                    Alphareds.Module.SabreWebService.SWS.PricedItineryModel sabreResult = flightInfo;
                    Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse rs = new Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse();

                    switch (serviceSource)
                    {
                        #region Sabre
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.SACS:
                            //var result = SabreServiceCall.SearchFlightBargainFinderMaxResponse(searchModel);
                            //var matchedFlight = SabreServiceCall.GetMatchedFlight(sabreResult, result);

                            break;
                        #endregion

                        #region AirAsia
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia:
                            #region Login - To get Session ID
                            sessionID = AAServiceCall.Logon();

                            if (string.IsNullOrEmpty(sessionID))
                            {
                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("AirAsia", "AirAsia - No session ID return");
                                break;
                            }
                            #endregion

                            #region Get Availability Verify
                            Alphareds.Module.AAWebService.AAWS.GetAvailabilityRequest getAvailabitlityRq = ServiceMapper.AAServiceMapper.GetAvailabilityRequestMap(searchModel, sessionID, currency, General.Utilities.GetClientIP);
                            Alphareds.Module.AAWebService.AAWS.GetAvailabilityResponse getAvailabitlityRs = AAServiceCall.GetAvailabilityVerify(getAvailabitlityRq);

                            if (getAvailabitlityRs == null || getAvailabitlityRs.Errors?.ErrorMessage != null)
                            {
                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("AA", getAvailabitlityRs.Errors?.ErrorMessage);
                                ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                return View(postBackViewPath, model);
                            }
                            else
                            {
                                serializeVerifyRq = JsonConvert.SerializeObject(getAvailabitlityRq);
                                serializeVerifyRs = JsonConvert.SerializeObject(getAvailabitlityRs);
                            }
                            #endregion
                            break;
                        #endregion

                        #region BritishAirways
                        case Alphareds.Module.CompareToolWebService.CTWS.serviceSource.BritishAirways:
                            #region BA GetAvailability Rq/Rs - Refresh Cache to Latest Result
                            Alphareds.Module.BAWebService.BAWS.GetFlightAvailabilityRQ BAGetFlightAvailabilityRQ = ServiceMapper.BAServiceMapper.MapBAGetFlightAvailabilityRequest(model.SearchFlightResultViewModel);
                            Alphareds.Module.BAWebService.BAWS.AirShoppingRS BAGetFlightAvailabilityRS = BAServiceCall.getFlightResp(BAGetFlightAvailabilityRQ);
                            #endregion

                            #region BA VerifyFlight Rq/Rs
                            Alphareds.Module.BAWebService.BAWS.VerifyFlightRQ BAVerifyFlightRQ = ServiceMapper.BAServiceMapper.MapBAVerifyRequest(model);
                            Alphareds.Module.BAWebService.BAWS.VerifyFlightRS BAVerifyFlightRS = BAServiceCall.getFlightResp(BAVerifyFlightRQ);

                            if (BAVerifyFlightRS.IsAvailableFlight == "false" || BAVerifyFlightRS.IsAvailableFlight == "false")
                            {
                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("BA", "No matched flight found");
                                ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                return View(postBackViewPath, model);
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
                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("TCG", "TCG Search Response Error - No flight result");
                                break;
                            }
                            #endregion

                            #region TCG Verify Rq/Rs
                            TCGVerifyModel.VerifyRequest tcgVerifyRq = new TCGVerifyModel.VerifyRequest();
                            TCGVerifyModel.VerifyResponse tcgVerifyRs = new TCGVerifyModel.VerifyResponse();

                            tcgVerifyRq = ServiceMapper.TCGServiceMapper.MapTCGVerificationRequest(tcgSearchRs, sabreResult, searchModel);
                            if (tcgVerifyRq.routing != null)
                            {
                                tcgVerifyRs = TCGServiceCall.TCGVerify(tcgVerifyRq);
                            }
                            else
                            {
                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("TCG", "TCG Verify Request Error - No match flight found.");
                                ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                return View(postBackViewPath, model);
                            }

                            if (tcgVerifyRs.msg != "success" && tcgVerifyRs.status != 0)
                            {
                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("TCG", "TCG Verify Response Error - Error Code: " + tcgVerifyRs.status + " - " + tcgVerifyRs.msg);
                                ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                return View(postBackViewPath, model);
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
                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("FRFY", "Login: No Session ID Returned.");
                                break;
                            }
                            #endregion

                            #region Get Availability Rq/Rs
                            Alphareds.Module.FYWebservice.FYWS.AvailabilityRequest fyAvailabilityRq = ServiceMapper.FYServiceMapper.MapAvailabilityRequest(searchModel, sessionID, currency);
                            Alphareds.Module.FYWebservice.FYWS.AvailabilityResponse fyAvailabitlityRs = FYServiceCall.GetAvailability(fyAvailabilityRq);

                            if (fyAvailabitlityRs.Error != null || fyAvailabitlityRs.Result?.Schedules[0][0].Journeys.Length == 0)
                            {
                                if (fyAvailabitlityRs.Error != null)
                                    errMsg = "GetAvailability Response Method: " + fyAvailabitlityRs.Error + ".";
                                else if (fyAvailabitlityRs.Result.Schedules[0][0].Journeys.Length == 0)
                                    errMsg = "GetAvailability Response Method: No Response Returned.";
                                else
                                    errMsg = "GetAvailability Response Method: Unknown Errors.";

                                rs.Header = UtilitiesService.GenerateSabreErrorHeader("FRFY", errMsg);
                                break;
                            }

                            bool isReturn = sabreResult.DirectionInd == PricedItineryModel.DirectionIndType.Return;
                            int scheduleDetailLength = isReturn ? 2 : 1;
                            List<Alphareds.Module.FYWebservice.FYWS.Journey1> matchedScheduleDetails = ServiceMapper.FYServiceMapper.GetMatchedFlight(sabreResult, fyAvailabitlityRs);

                            if (matchedScheduleDetails.Count > scheduleDetailLength)
                            {
                                ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                                return View(postBackViewPath, model);
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
                            rs.Header = UtilitiesService.GenerateSabreErrorHeader("Unknown Source", "Unknown Service Source");
                            break;
                            #endregion
                    }
                    #endregion

                    #region 2017-08-17, New model for multiple product approach
                    //Check Session Exist
                    CheckoutProduct checkoutProduct = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid) ?? new CheckoutProduct();
                    checkoutProduct.IsRegister = false; // reset last option
                    checkoutProduct.ImFlying = false;
                    checkoutProduct.IsDynamic = model.SearchFlightResultViewModel.IsDynamic;
                    checkoutProduct.IsFixedPrice = model.SearchFlightResultViewModel.IsFixedPrice;
                    checkoutProduct.DPPromoCode = model.SearchFlightResultViewModel.DPPromoCode;
                    checkoutProduct.RequireInsurance = false;
                    checkoutProduct.BusinessType = IsAgentUser ? BusinessType.B2B : BusinessType.B2C;

                    checkoutProduct.CheckoutStep = 1;

                    List<TravellerDetail> trvDtl = new List<TravellerDetail>();
                    PromoCodeRule promoCodeRule = model.SearchFlightResultViewModel.IsPromoCodeUsed ? GetPromoCodeDiscountRule(model.SearchFlightResultViewModel, null, null, selectedFlight.AirlineInfo.FirstOrDefault().Code) : null; //Pass Airlinecode to compare
                    bool isPromoFlightSupplier = isPromoFlightList(promoCodeRule, model.SearchFlightResultViewModel, serviceSource);
                    decimal ttlAmtDiscount = isPromoFlightSupplier ? CalcPromoCodeDiscount(promoCodeRule, flightInfo.PricingInfo.TotalBeforeTax) : 0m;

                    int ttlPsg = model.SearchFlightResultViewModel.Adults + model.SearchFlightResultViewModel.Childrens + model.SearchFlightResultViewModel.Infants;
                    var _initContactPerson = checkoutProduct?.Flight?.ContactPerson;
                    var _initTravellerDtls = checkoutProduct?.Flight?.TravellerDetails;

                    // For reassign back last filled record.
                    var _recentFilledCookies = Request.Cookies[Alphareds.Module.Model.Web.Cookies.Key.Mayflower_SpeedUpCFill.ToString()];
                    if (checkoutProduct?.Flight == null && _recentFilledCookies != null)
                    {
                        try
                        {
                            string _decVal = "";
                            Alphareds.Module.Cryptography.Cryptography.AES.TryDecrypt(_recentFilledCookies.Value, out _decVal);

                            var lastFilledInfo = JsonConvert.DeserializeObject<CheckoutProduct.LastFilledInfo>(_decVal);
                            _initContactPerson = lastFilledInfo.LastContactPerson;
                            _initContactPerson.TravellerGrpID = null;
                            checkoutProduct.ContactPerson = _initContactPerson;
                        }
                        catch
                        {
                            // Remove invalid cookies.
                            _recentFilledCookies.Value = null;
                            _recentFilledCookies.Expires = DateTime.Now.AddDays(-1);
                            Response.Cookies.Add(_recentFilledCookies);
                        }
                    }

                    /*if (checkoutProduct?.Flight == null)
                    {
                        for (int i = 0; i < ttlPsg; i++)
                        {
                            trvDtl.Add(new TravellerDetail { _DepartureDate = model.SearchFlightResultViewModel.BeginDate ?? DateTime.Now });
                        }
                    }*/

                    ProductFlight prdFlight = new ProductFlight()
                    {
                        ProductSeq = 1,
                        FlightInfo = new FlightInformation
                        {
                            Supplier = serviceSource,
                            SupplierFlightInfo = flightInfo,
                            AvaSSR = avaSSR
                        },
                        SearchFlightInfo = model.SearchFlightResultViewModel,
                        ContactPerson = _initContactPerson,
                        TravellerDetails = _initTravellerDtls ?? trvDtl,

                        BookSeatInformation_Supplier_Request_Json = string.Empty,
                        BookSeatInformation_Supplier_Response_Json = string.Empty,
                        Temp_Booking_Info = string.Empty,

                        FlightVerify_Request_Json = serializeVerifyRq,
                        FlightVerify_Response_Json = serializeVerifyRs,

                        PricingDetail = new ProductPricingDetail()
                        {
                            Ttl_MarkUp = flightInfo.PricingInfo.FareBreakDown.Sum(x => x.ServiceFee * x.PassengerTypeQuantity),
                            Currency = "MYR",
                            Discounts = promoCodeRule != null ? new List<DiscountDetail>()
                            {
                                new DiscountDetail {
                                    DiscType = DiscountType.CODE,
                                    Disc_Amt = ttlAmtDiscount,
                                    Disc_Desc = model.SearchFlightResultViewModel.PromoCode,
                                    PrdType = ProductTypes.Flight,
                                    Seq = 1
                                }
                            } : new List<DiscountDetail>(),
                            Items = flightInfo.PricingInfo.FareBreakDown.Select(x =>
                            {
                                if (checkoutProduct?.Flight == null)
                                {
                                    for (int i = 0; i < x.PassengerTypeQuantity; i++)
                                    {
                                        trvDtl.Add(new TravellerDetail
                                        {
                                            _DepartureDate = model.SearchFlightResultViewModel.BeginDate ?? DateTime.Now,
                                            PassengerType = x.PassengerTypeCode,
                                        });
                                    }
                                }

                                return new ProductItem()
                                {
                                    Supplier_TotalAmt = x.SourceTotalAfterTax * x.PassengerTypeQuantity,
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
                    Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                    checkoutProduct.RemoveProduct(ProductTypes.Flight);
                    checkoutProduct.InsertProduct(prdFlight);
                    if (promoCodeRule != null)
                    {
                        model.SearchFlightResultViewModel.PromoId = promoCodeRule.PromoID;
                        checkoutProduct.PromoID = promoCodeRule.PromoID;
                    }
                    #endregion

                    if (checkoutProduct.IsDynamic && !checkoutProduct.IsFixedPrice)
                    {
                        var flightarrival = checkoutProduct.Flight.FlightInfo.FlightDetailInfo.FlightTrip.Where(x => x.RouteType == RouteType.Outbound).FirstOrDefault()?.ArriveDateTime;

                        MayFlower db = new MayFlower();
                        SearchHotelModel SearchHotel = new SearchHotelModel();

                        SearchHotel.CustomerUserAgent = Request.UserAgent;
                        SearchHotel.CustomerIpAddress = General.Utilities.GetClientIP;
                        SearchHotel.CustomerSessionId = tripid;
                        SearchHotel.CurrencyCode = "MYR";
                        SearchHotel.ArrivalDate = (flightarrival ?? model.SearchFlightResultViewModel.BeginDate) ?? DateTime.Now;
                        SearchHotel.DepartureDate = model.SearchFlightResultViewModel.EndDate ?? DateTime.Now;

                        SearchHotel.IsDynamic = model.SearchFlightResultViewModel.IsDynamic;
                        SearchHotel.DynamicStationCode = model.SearchFlightResultViewModel.ArrivalStationCode;
                        SearchHotel.IsCrossSell = false; //TODO: If dynamic package need package rate then true
                        SearchHotel.IsB2B = CustomPrincipal.IsAgent;

                        var stationcityList = SearchHotel.DynamicStationCode != null ? StationCitySelect(SearchHotel.DynamicStationCode).ToList() : null;
                        var stationcity = stationcityList != null ? stationcityList.Where(x => x.IsDefault).FirstOrDefault()?.City : null;

                        SearchHotel.Destination = stationcity ?? model.SearchFlightResultViewModel.ArrivalStation.Split('-')[1];
                        SearchHotel.NoOfRoom = model.SearchFlightResultViewModel.NoOfRoom;
                        SearchHotel.NoOfAdult = model.SearchFlightResultViewModel.Adults;
                        SearchHotel.NoOfInfant = model.SearchFlightResultViewModel.Childrens + model.SearchFlightResultViewModel.Infants;

                        checkoutProduct.DPPromoCode = checkoutProduct.DPPromoCode ?? "BUNDLE";
                        SearchHotel.PromoCode = checkoutProduct.DPPromoCode == "BUNDLE" ? checkoutProduct.DPPromoCode : null;

                        Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, SearchHotel);
                        return RedirectToAction("Search", "Hotel", new { tripid, affiliationId });
                    }
                    else if (checkoutProduct.IsFixedPrice)
                    {
                        checkoutProduct.DPPromoCode = checkoutProduct.DPPromoCode ?? "BUNDLE";
                    }
                    return RedirectToAction("GuestDetails", "Checkout", new { tripid, affiliationId });
                }
                else
                {
                    return RedirectToAction("Search", new { status = "Invalid pairing result.", tripid, affiliationId });
                }
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                // 2017/01/05 - Second Level Logger
                logger.Debug(ex, "Step 2 Second Level Bug Logger");
                UtilitiesService.NlogExceptionForBookingFlow(logger, model, ex, userid, "HomeSearchFlightError", "", "");

                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// HTML Result - Redner filter panel partial view as AJAX.
        /// </summary>

        public ActionResult _FlightPackageDetail(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            if (checkout != null && checkout.IsFixedPrice && checkout.Hotel != null)
            {
                return PartialView("~/Views/Flight/Search/_PackageReservationDetails.cshtml", checkout);
            }
            return null;
        }

        [HttpPost]
        public ActionResult _FlightFilterPanel()
        {
            if (Session[sessionFullODOResult] != null)
            {
                FlightBookingModel model = (FlightBookingModel)Session[sessionFullODOResult];

                if (Session[sessionFilterParam] != null)
                {
                    model.FilterFlightModel = (FilterFlightModel)Session[sessionFilterParam];
                }

                if (model.FlightSearchResultViewModel != null && model.FlightSearchResultViewModel.GroupFullFlightSearchReseult2 != null)
                {
                    return PartialView("~/Views/Flight/Search/_FlightFilterPanel.cshtml", model);
                }
            }
            return null;
        }

        /// <summary>
        /// JSONResult - Based on full flight search list and generate filter base options.
        /// </summary>
        /// <returns>Filter Option</returns>
        [HttpPost]
        public JsonResult GetFullFlightSearchResultDetail()
        {
            var fullFlightResult = new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();

            try
            {
                if (Session[sessionFullFlightSearchResult] != null)
                {
                    var filteredModel = new FilterFlightModel();
                    fullFlightResult = (List<Alphareds.Module.CompareToolWebService.CTWS.flightData>)Session[sessionFullFlightSearchResult];

                    if (fullFlightResult == null)
                    {
                        Session.Remove(sessionFullFlightSearchResult);
                        return Json(null);
                    }

                    var outBound = fullFlightResult
                        .Select(x => x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments);

                    // By Time Of Day
                    string minOutDepartureTime = outBound.Min(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
                    string maxOutDepartureTime = outBound.Max(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
                    string minOutArrivalTime = outBound.Min(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();
                    string maxOutArrivalTime = outBound.Max(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();

                    // By Full Time
                    //minOutDepartureTime = outBound.Min(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
                    //maxOutDepartureTime = outBound.Max(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
                    //minOutArrivalTime = outBound.Min(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();
                    //maxOutArrivalTime = outBound.Max(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();

                    string minInDepartureTime = null;
                    string maxInDepartureTime = null;
                    string minInArrivalTime = null;
                    string maxInArrivalTime = null;

                    var pricingList = fullFlightResult.Select(x => x.pricedItineryModel.PricingInfo.TotalAfterTax);

                    if (fullFlightResult.First().pricedItineryModel.OriginDestinationOptions.Length > 1)
                    {
                        var inBound = fullFlightResult
                        .Select(x => x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments);

                        minInDepartureTime = inBound.Min(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
                        maxInDepartureTime = inBound.Max(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
                        minInArrivalTime = inBound.Min(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();
                        maxInArrivalTime = inBound.Max(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();

                        //maxInDepartureTime = inBound.Min(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
                        //maxInDepartureTime = inBound.Max(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
                        //minInArrivalTime = inBound.Min(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();
                        //maxInArrivalTime = inBound.Max(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();
                    }

                    MHFilterFlightModel filterModel = new MHFilterFlightModel
                    {
                        OutboundDepartureTimeMin = minOutDepartureTime,
                        OutboundDepartureTimeMax = maxOutDepartureTime,
                        OutboundArrivalTimeMin = minOutArrivalTime,
                        OutboundArrivalTimeMax = maxOutArrivalTime,
                        InboundDepartureTimeMin = minInDepartureTime,
                        InboundDepartureTimeMax = maxInDepartureTime,
                        InboundArrivalTimeMin = minInArrivalTime,
                        InboundArrivalTimeMax = maxInArrivalTime,
                        PriceMin = pricingList.Min().ToString(),
                        PriceMax = pricingList.Max().ToString(),
                        //Airline = [""],
                    };

                    // 20161209 - Reassign for js refresh usage
                    bool isNoFilter = Session[sessionFilterParam] == null;

                    filteredModel = (FilterFlightModel)Session[sessionFilterParam];

                    filterModel.OutboundDepartureTimeMinSelected = isNoFilter ? filterModel.OutboundDepartureTimeMin : filteredModel.OutDepartureTimeMin;
                    filterModel.OutboundDepartureTimeMaxSelected = isNoFilter ? filterModel.OutboundDepartureTimeMax : filteredModel.OutDepartureTimeMax;
                    filterModel.OutboundArrivalTimeMinSelected = isNoFilter ? filterModel.OutboundArrivalTimeMin : filteredModel.OutArrivalTimeMin;
                    filterModel.OutboundArrivalTimeMaxSelected = isNoFilter ? filterModel.OutboundArrivalTimeMax : filteredModel.OutArrivalTimeMax;

                    filterModel.InboundDepartureTimeMinSelected = isNoFilter ? filterModel.InboundDepartureTimeMin : filteredModel.InDepartureTimeMin;
                    filterModel.InboundDepartureTimeMaxSelected = isNoFilter ? filterModel.InboundDepartureTimeMax : filteredModel.InDepartureTimeMax;
                    filterModel.InboundArrivalTimeMinSelected = isNoFilter ? filterModel.InboundArrivalTimeMin : filteredModel.InArrivalTimeMin;
                    filterModel.InboundArrivalTimeMaxSelected = isNoFilter ? filterModel.InboundArrivalTimeMax : filteredModel.InArrivalTimeMax;

                    filterModel.PriceMinSelected = isNoFilter ? filterModel.PriceMin : filteredModel.PriceMin;
                    filterModel.PriceMaxSelected = isNoFilter ? filterModel.PriceMax : filteredModel.PriceMax;

                    Session[sessionMHFilterFlightModel] = filterModel;

                    return Json(filterModel);
                }
            }
            catch (Exception ex)
            {
                string formattedMsg = Environment.NewLine + Environment.NewLine;
                formattedMsg += "Session Model:" + sessionFullFlightSearchResult + Environment.NewLine;
                formattedMsg += JsonConvert.SerializeObject(fullFlightResult);

                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex.ToString() + formattedMsg);
            }

            return Json(new object());
        }

        /// <summary>
        /// JSON Result - Listing all flight pairing option.
        /// </summary>
        /// <returns>Pairing Option(Json format) or Session Expired page</returns>
        public ActionResult FlightResultOption()
        {
            FlightBookingModel model = new FlightBookingModel();

            if (Session[sessionFlightBooking] != null)
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }
            else
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }

            if (model.FlightSearchResultViewModel == null)
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }
            else if (model.FlightSearchResultViewModel.FullFlightSearchResult == null)
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }

            int counter = 0;
            var grpModel = from odo in model.FlightSearchResultViewModel.FullFlightSearchResult
                           group odo by new
                           {
                               Price = odo.pricedItineryModel.PricingInfo.TotalAfterTax,
                               Airline = string.Join("-", odo.pricedItineryModel.OriginDestinationOptions.SelectMany(x => x.FlightSegments.Select(seg => seg.AirlineCode)).Distinct()),
                               ServiceSource = odo.ServiceSource.ToString()
                           } into grp
                           select new
                           {
                               Index = counter++,
                               Price = grp.Key.Price,
                               Airline = grp.Key.Airline,
                               GroupTag = grp.Key.Airline + grp.Key.Price.ToString().Replace(".", ""),
                               OutBound = grp.Select(x => string.Join(",", x.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.Select(segment => (segment.AirlineCode ?? "") + (segment.FlightNumber ?? "") + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + x.ServiceSource))),
                               InBound = grp.Select(x => string.Join(",", x.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.Select(segment => (segment.AirlineCode ?? "") + (segment.FlightNumber ?? "") + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + x.ServiceSource))),
                               ServiceSource = grp.Key.ServiceSource
                           };

            return Json(grpModel, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// JSON Result - Check flight result is pariing able or not.
        /// </summary>
        /// <returns>boolean or Session Expired page</returns>
        public ActionResult IsFlightPairable(string outbound, string inbound, string grpTag, string tripid)
        {
            FlightBookingModel model = new FlightBookingModel();

            if (Session[sessionFlightBooking] != null)
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }
            else
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }

            int counter = 0;
            List<int> _outboundIndex = new List<int>();
            List<int> _innboundIndex = new List<int>();

            foreach (var item in model.FlightSearchResultViewModel.FullFlightSearchResult)
            {
                string joinAirlineCode = string.Join("-", item.pricedItineryModel.OriginDestinationOptions.SelectMany(x => x.FlightSegments.Select(s => s.AirlineCode)).Distinct());
                if (joinAirlineCode + item.pricedItineryModel.PricingInfo.TotalAfterTax.ToString().Replace(".", "") == grpTag)
                {
                    int outboundSegLen, inboundSegLen = -1;
                    outboundSegLen = outbound.Split(',').Length;

                    var outboundODO = item.pricedItineryModel.OriginDestinationOptions.First();
                    if (outboundSegLen == outboundODO.FlightSegments.Length)
                    {
                        var outSegTagList = item.pricedItineryModel.OriginDestinationOptions.First().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + item.ServiceSource.ToString());
                        bool outIsList = string.Join(",", outSegTagList) == outbound;

                        if (outIsList)
                        {
                            _outboundIndex.Add(counter);
                        }
                    }

                    if (inbound != "" || inbound != null)
                    {
                        inboundSegLen = inbound.Split(',').Length;

                        var inboundODO = item.pricedItineryModel.OriginDestinationOptions.Last();
                        if (inboundSegLen == inboundODO.FlightSegments.Length)
                        {
                            var inSegTagList = item.pricedItineryModel.OriginDestinationOptions.Last().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + item.ServiceSource.ToString());
                            bool inIsList = string.Join(",", inSegTagList) == inbound;

                            if (inIsList)
                            {
                                _innboundIndex.Add(counter);
                            }
                        }
                    }
                }
                counter++;
            }

            bool isPairable = _outboundIndex.Intersect(_innboundIndex).Any();

            return Json(isPairable);
        }

        [HttpPost]
        public ActionResult SaveFlightDtl(string outbound, string inbound, string grpTag, string tripid)
        {
            FlightBookingModel model = null;
            int userid = CurrentUserID;

            if (Session[sessionFlightBooking] != null)
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }
            else
            {
                return Json(new { saveSuccess = false });
            }

            try
            {
                bool isOneWay = !model.SearchFlightResultViewModel.isReturn;

                // set selected flight here
                int counter = 0;
                List<int> _outboundIndex = new List<int>();
                List<int> _innboundIndex = new List<int>();
                model.FlightSearchResultViewModel = model.FlightSearchResultViewModel ?? new FlightSearchResultViewModel();
                model.FlightSearchResultViewModel.FullFlightSearchResult = model.FlightSearchResultViewModel.FullFlightSearchResult
                                                                                           ?? new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();

                foreach (var singleResult in model.FlightSearchResultViewModel.FullFlightSearchResult)
                {
                    var item = singleResult.pricedItineryModel;
                    string joinAirlineCode = string.Join("-", item.OriginDestinationOptions.SelectMany(x => x.FlightSegments.Select(s => s.AirlineCode)).Distinct());
                    if (joinAirlineCode + item.PricingInfo.TotalAfterTax.ToString().Replace(".", "") == grpTag)
                    {
                        int outboundSegLen, inboundSegLen = -1;
                        outboundSegLen = outbound.Split(',').Length;

                        var outboundODO = item.OriginDestinationOptions.First();
                        if (outboundSegLen == outboundODO.FlightSegments.Length)
                        {
                            var outSegTagList = item.OriginDestinationOptions.First().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + singleResult.ServiceSource.ToString());
                            bool outIsList = string.Join(",", outSegTagList) == outbound;

                            if (outIsList)
                            {
                                _outboundIndex.Add(counter);
                            }
                        }

                        if (inbound != "" || inbound != null)
                        {
                            inboundSegLen = inbound.Split(',').Length;

                            var inboundODO = item.OriginDestinationOptions.Last();
                            if (inboundSegLen == inboundODO.FlightSegments.Length)
                            {
                                var inSegTagList = item.OriginDestinationOptions.Last().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + singleResult.ServiceSource.ToString());
                                bool inIsList = string.Join(",", inSegTagList) == inbound;

                                if (inIsList)
                                {
                                    _innboundIndex.Add(counter);
                                }
                            }
                        }
                    }
                    counter++;
                }

                int odoIndex = -1;
                bool isPairable = _outboundIndex.Intersect(_innboundIndex).Any();
                if (isPairable || isOneWay)
                {
                    if (_innboundIndex.Count != 0)
                    {
                        odoIndex = _outboundIndex.Intersect(_innboundIndex).First();
                    }
                    else if (_outboundIndex.Count != 0)
                    {
                        odoIndex = _outboundIndex.FirstOrDefault();
                    }
                    else
                    {
                        return Json(new { saveSuccess = false });
                    }

                    var selectedFlight = model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex];
                    var searchInfo = model.SearchFlightResultViewModel;

                    //Save flight detail to db
                    using (var dbCtx = new MayFlower())
                    {
                        using (var trans = dbCtx.Database.BeginTransaction())
                        {
                            try
                            {
                                SavedSearch userSearchDtl = new SavedSearch
                                {
                                    CreatedByID = userid,
                                    CreatedDate = DateTime.Now,
                                    IsActive = true,
                                    ModifiedByID = userid,
                                    ModifiedDate = DateTime.Now,
                                    Type = "FLT",
                                    UserID = userid
                                };
                                FlightSearch fSearch = new FlightSearch
                                {
                                    Adult = searchInfo.Adults,
                                    Child = searchInfo.Childrens,
                                    Infant = searchInfo.Infants,
                                    ArrivalStation = searchInfo.ArrivalStationCode,
                                    CabinType = searchInfo.CabinClass,
                                    CreatedByID = userid,
                                    CreatedDate = DateTime.Now,
                                    DepartStation = searchInfo.DepartureStationCode,
                                    DepartureDate = searchInfo.BeginDate ?? DateTime.MinValue,
                                    DirectFlight = searchInfo.DirectFlight,
                                    IsActive = true,
                                    ModifiedByID = userid,
                                    ModifiedDate = DateTime.Now,
                                    ReturnDate = searchInfo.EndDate ?? searchInfo.BeginDate,
                                    SourceFlightPrice = selectedFlight.pricedItineryModel.PricingInfo.SourceTotalAfterTax,
                                    TotalFlightPrice = selectedFlight.pricedItineryModel.PricingInfo.TotalAfterTax,
                                    TripTypeID = searchInfo.isReturn ? Convert.ToByte(2) : Convert.ToByte(1),
                                    SupplierCode = UtilitiesService.ServiceSourceToDBServiceSourceName(selectedFlight.ServiceSource),
                                };
                                List<FlightSearchDetail> fSearchDtls = new List<FlightSearchDetail>();
                                for (int i = 0; i < selectedFlight.pricedItineryModel.OriginDestinationOptions.Length; i++)
                                {
                                    string segOrder = i == 0 ? "O" : "I";
                                    for (int j = 0; j < selectedFlight.pricedItineryModel.OriginDestinationOptions[i].FlightSegments.Length; j++)
                                    {
                                        var seg = selectedFlight.pricedItineryModel.OriginDestinationOptions[i].FlightSegments[j];
                                        fSearchDtls.Add(new FlightSearchDetail
                                        {
                                            AirlineCode = seg.AirlineCode,
                                            ArrivalDateTime = seg.ArrivalDateTime,
                                            ArrivalStation = seg.ArrivalAirportLocationCode,
                                            DepartureDateTime = seg.DepartureDateTime,
                                            DepartureStation = seg.DepartureAirportLocationCode,
                                            CreatedByID = userid,
                                            CreatedDate = DateTime.Now,
                                            IsActive = true,
                                            ModifiedByID = userid,
                                            ModifiedDate = DateTime.Now,
                                            ResBookDesignCode = seg.ResBookDesigCode,
                                            SegmentOrder = segOrder + (j + 1).ToString(),
                                            FlightNumber = seg.FlightNumber,
                                        });
                                    }
                                }
                                fSearch.FlightSearchDetails = fSearchDtls;
                                userSearchDtl.FlightSearches.Add(fSearch);
                                dbCtx.SavedSearches.Add(userSearchDtl);
                                dbCtx.SaveChanges();
                                trans.Commit();
                            }
                            catch (Exception ex)
                            {
                                trans.Rollback();
                                throw ex;
                            }
                        }
                    }

                    return Json(new { saveSuccess = true });
                }
                else
                {
                    return Json(new { saveSuccess = false });
                }

            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Debug(ex.Message);

                return Json(new { saveSuccess = false });
            }
        }
        #endregion

        [HttpPost]
        public ActionResult GenerateFlightLink(string outbound, string inbound, string grpTag, string tripid)
        {
            FlightBookingModel model = null;
            int userid = CurrentUserID;

            if (Session[sessionFlightBooking] != null)
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }
            else
            {
                return Json(new { saveSuccess = false });
            }

            try
            {
                bool isOneWay = !model.SearchFlightResultViewModel.isReturn;

                // set selected flight here
                int counter = 0;
                List<int> _outboundIndex = new List<int>();
                List<int> _innboundIndex = new List<int>();
                model.FlightSearchResultViewModel = model.FlightSearchResultViewModel ?? new FlightSearchResultViewModel();
                model.FlightSearchResultViewModel.FullFlightSearchResult = model.FlightSearchResultViewModel.FullFlightSearchResult
                                                                                           ?? new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();

                foreach (var singleResult in model.FlightSearchResultViewModel.FullFlightSearchResult)
                {
                    var item = singleResult.pricedItineryModel;
                    string joinAirlineCode = string.Join("-", item.OriginDestinationOptions.SelectMany(x => x.FlightSegments.Select(s => s.AirlineCode)).Distinct());
                    if (joinAirlineCode + item.PricingInfo.TotalAfterTax.ToString().Replace(".", "") == grpTag)
                    {
                        int outboundSegLen, inboundSegLen = -1;
                        outboundSegLen = outbound.Split(',').Length;

                        var outboundODO = item.OriginDestinationOptions.First();
                        if (outboundSegLen == outboundODO.FlightSegments.Length)
                        {
                            var outSegTagList = item.OriginDestinationOptions.First().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + singleResult.ServiceSource.ToString());
                            bool outIsList = string.Join(",", outSegTagList) == outbound;

                            if (outIsList)
                            {
                                _outboundIndex.Add(counter);
                            }
                        }

                        if (inbound != "" || inbound != null)
                        {
                            inboundSegLen = inbound.Split(',').Length;

                            var inboundODO = item.OriginDestinationOptions.Last();
                            if (inboundSegLen == inboundODO.FlightSegments.Length)
                            {
                                var inSegTagList = item.OriginDestinationOptions.Last().FlightSegments.Select(segment => segment.AirlineCode + segment.FlightNumber + "-" + segment.DepartureDateTime.ToString("yyyyMMddHHmm") + "-" + singleResult.ServiceSource.ToString());
                                bool inIsList = string.Join(",", inSegTagList) == inbound;

                                if (inIsList)
                                {
                                    _innboundIndex.Add(counter);
                                }
                            }
                        }
                    }
                    counter++;
                }

                int odoIndex = -1;
                bool isPairable = _outboundIndex.Intersect(_innboundIndex).Any();
                if (isPairable || isOneWay)
                {
                    if (_innboundIndex.Count != 0)
                    {
                        odoIndex = _outboundIndex.Intersect(_innboundIndex).First();
                    }
                    else if (_outboundIndex.Count != 0)
                    {
                        odoIndex = _outboundIndex.FirstOrDefault();
                    }
                    else
                    {
                        return Json(new { saveSuccess = false });
                    }

                    var selectedFlight = model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex];
                    var searchInfo = model.SearchFlightResultViewModel;
                    var link = "";
                    try
                    {
                        FlightLinkSearch fSearch = new FlightLinkSearch
                        {
                            Adult = searchInfo.Adults,
                            Child = searchInfo.Childrens,
                            Infant = searchInfo.Infants,
                            ArrivalStation = searchInfo.ArrivalStationCode,
                            CabinType = searchInfo.CabinClass,
                            DepartStation = searchInfo.DepartureStationCode,
                            DepartureDate = searchInfo.BeginDate ?? DateTime.MinValue,
                            DirectFlight = searchInfo.DirectFlight,
                            ReturnDate = searchInfo.EndDate ?? searchInfo.BeginDate,
                            SourceFlightPrice = selectedFlight.pricedItineryModel.PricingInfo.SourceTotalAfterTax,
                            TotalFlightPrice = selectedFlight.pricedItineryModel.PricingInfo.TotalAfterTax,
                            TripTypeID = searchInfo.isReturn ? Convert.ToByte(2) : Convert.ToByte(1),
                            SupplierCode = UtilitiesService.ServiceSourceToDBServiceSourceName(selectedFlight.ServiceSource),
                        };
                        List<FlightLinkSearchDetail> fSearchDtls = new List<FlightLinkSearchDetail>();
                        for (int i = 0; i < selectedFlight.pricedItineryModel.OriginDestinationOptions.Length; i++)
                        {
                            string segOrder = i == 0 ? "O" : "I";
                            for (int j = 0; j < selectedFlight.pricedItineryModel.OriginDestinationOptions[i].FlightSegments.Length; j++)
                            {
                                var seg = selectedFlight.pricedItineryModel.OriginDestinationOptions[i].FlightSegments[j];
                                fSearchDtls.Add(new FlightLinkSearchDetail
                                {
                                    AirlineCode = seg.AirlineCode,
                                    ArrivalDateTime = seg.ArrivalDateTime,
                                    ArrivalStation = seg.ArrivalAirportLocationCode,
                                    DepartureDateTime = seg.DepartureDateTime,
                                    DepartureStation = seg.DepartureAirportLocationCode,
                                    ResBookDesignCode = seg.ResBookDesigCode,
                                    SegmentOrder = segOrder + (j + 1).ToString(),
                                    FlightNumber = seg.FlightNumber,
                                });
                            }
                        }
                        fSearch.FlightSearchDetails = fSearchDtls;
                        var JSONString = JsonConvert.SerializeObject(fSearch);
                        link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, String.Empty);
                        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(JSONString);
                        var fltInfo = Convert.ToBase64String(plainTextBytes);
                        link += "/Flight/FlightLink?FltLink=" + fltInfo;
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                    return Json(new { saveSuccess = true, link = link });
                }
                else
                {
                    return Json(new { saveSuccess = false });
                }

            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Debug(ex.Message);

                return Json(new { saveSuccess = false });
            }
        }

        public ActionResult FlightLink(string FltLink)
        {
            MayFlower dbCtx = new MayFlower();

            try
            {
                CheckoutProduct checkoutProduct = new CheckoutProduct();
                string tripid = Guid.NewGuid().ToString();
                var link = Request.QueryString["FltLink"];
                var FltBytes = Convert.FromBase64String(link);
                var FltJson = System.Text.Encoding.UTF8.GetString(FltBytes);
                var FlightSearches = JsonConvert.DeserializeObject<FlightSearch>(FltJson);
                var fltSearch = FlightSearches;
                var fltDtl = fltSearch.FlightSearchDetails;

                SearchFlightResultViewModel searchModel = new SearchFlightResultViewModel
                {
                    Adults = fltSearch.Adult ?? 0,
                    ArrivalStation = fltSearch.ArrivalStation,
                    BeginDate = fltSearch.DepartureDate,
                    CabinClass = fltSearch.CabinType,
                    Childrens = fltSearch.Child ?? 0,
                    DepartureStation = fltSearch.DepartStation,
                    DirectFlight = fltSearch.DirectFlight ?? false,
                    EndDate = fltSearch.ReturnDate,
                    Infants = fltSearch.Infant ?? 0,
                    TripType = fltSearch.TripTypeID == 1 ? "OneWay" : "Return" //TripTypeID == 1 is OneWay, 2 is Return
                };
                List<FlightSegmentModels> segModel = new List<FlightSegmentModels>();
                int index = 1;
                foreach (var dtl in fltDtl)
                {
                    segModel.Add(new FlightSegmentModels
                    {
                        airline_Code = dtl.AirlineCode,
                        Class = dtl.ResBookDesignCode,
                        departure_Date = dtl.DepartureDateTime ?? DateTime.Now,
                        des = dtl.ArrivalStation,
                        flight_No = dtl.FlightNumber,
                        index = index++,
                        isOutBoundSeg = dtl.SegmentOrder.Contains("O"),
                        ori = dtl.DepartureStation
                    });
                }

                var flightRes = Alphareds.Module.ServiceCall.CompareToolServiceCall.RequestFlight(searchModel);

                if (flightRes.Errors == null || flightRes.FlightData.Length > 0)
                {
                    var flightList = flightRes.FlightData.Where(x => x.ServiceSource == Alphareds.Module.Common.UtilitiesService.GetSupplier(fltSearch.SupplierCode)).ToList();
                    if (!Alphareds.Module.Common.Core.IsEnableB2B)
                    {
                        flightList = Alphareds.Module.FlightSearchController.FlightSearchServiceController.bindGSTandServiceFeeToFlightResults(flightList, searchModel.isDomesticFlight, searchModel.CabinClass);
                    }

                    var matchedFlight = new Alphareds.Module.FlightSearchController.FlightSearchServiceController().MatchFlight(flightRes.FlightData, segModel.ToArray());
                    if (matchedFlight != null && matchedFlight.Length > 0)
                    {
                        var selectedFlight = matchedFlight.FirstOrDefault();
                        var ttlNewPrice = selectedFlight.pricedItineryModel.PricingInfo.TotalAfterTax;

                        ProductFlight prdFlight = Alphareds.Module.FlightSearchController.FlightSearchServiceController.GenerateFlightProduct(
                            selectedFlight.pricedItineryModel, searchModel, selectedFlight.ServiceSource, General.Utilities.GetClientIP);

                        checkoutProduct.IsRegister = false; // reset last option
                        checkoutProduct.ImFlying = false;
                        checkoutProduct.RequireInsurance = false;
                        checkoutProduct.RemoveProduct(ProductTypes.Flight);
                        checkoutProduct.InsertProduct(prdFlight);

                        Alphareds.Module.Common.Core.SetSession(Alphareds.Module.Common.Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
                        return RedirectToAction("GuestDetails", "Checkout", new { tripid });
                    }
                    else
                    {
                        return RedirectToAction("Type", "Error", new { id = "session-error" });
                    }
                }
                else
                {
                    throw new Exception("Flight didn't exist");
                }
            }
            catch (Exception ex)
            {
                Logger log = LogManager.GetCurrentClassLogger();
                log.Debug(ex.Message);
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }
        }
        #region Step 5 : Order History
        public ActionResult OrderHistory(string bookingID, bool isRegister = false)
        {
            if (string.IsNullOrWhiteSpace(bookingID))
            {
                return RedirectToAction("NotFound", "Error");
            }

            string superPNRNo = bookingID;

            ViewBag.ReturnUrl = Url.Action("OrderHistory", "Flight");

            BookingConfirmationDetail model = new BookingConfirmationDetail();
            int userid = CurrentUserID;

            try
            {
                List<string> statusToDisplayBk = new List<string> { "CON", "QPL", "TKI", "RHI", "HTP" };
                MayFlower db = new MayFlower();
                Booking fltBk = db.Bookings.FirstOrDefault(x => x.SuperPNRNo == superPNRNo);
                SuperPNROrder sPO = fltBk.SuperPNR.SuperPNROrders.FirstOrDefault(x => x.OrderID == fltBk.OrderID);
                model.ConfirmationOutputDb = fltBk;

                if ((!IsSelfBookingOrGuest(userid, fltBk.UserID)) && !isRegister )
                {
                    string statusQuery = Request.QueryString["status"] != null ? Request.QueryString["status"].ToString() : string.Empty;

                    if (string.IsNullOrEmpty(statusQuery) || statusQuery != "success")
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                if (statusToDisplayBk.Any(x => x == sPO.BookingStatusCode))
                {
                    model = Alphareds.Module.BookingController.BookingServiceController.getBookingDetailPage(model, userid, db);

                    if (sPO.BookingStatusCode == "HTP" && fltBk.SupplierCode == "SBRE")
                    {
                        sPO.SuperPNR.LoadPaymentDetailList(true);
                        var pDtl = sPO.SuperPNR.PaymentDetailsList.FirstOrDefault();
                        ViewBag.PaymentDetails = new PaymentCheckout()
                        {
                            AvailableCredit = MemberServiceController.ChkCreditAmtRemain.ByUserId(CustomPrincipal.UserId, CustomPrincipal.UserTypeCode ?? "GT"),
                            PaymentMethod = "IPAFPX",
                            CreditTerm = CustomPrincipal.CreditTerm,
                            CreditCard = new CreditCard(),
                            PaymentCurrencyCode = pDtl.CurrencyCode,
                            GrandTotalForPayment = pDtl.TotalPendingAmount,
                        };

                        var _tempBook = fltBk.Temp_BookingInfo.LastOrDefault(x => x.Method == "response");
                        Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse _previousOutput = null;

                        try
                        {
                            _previousOutput = JsonConvert.DeserializeObject<Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse>(_tempBook.BookingInfo);
                            bool expiredBooking = false;

                            // Reduce 30 minutes from last ticket time.
                            if (_previousOutput.Output.IsAirlineReturnedLastTicketingDate)
                            {
                                expiredBooking = DateTime.Now.AddMinutes(-30) > _previousOutput.Output.LastTicketingDate;
                                // Service provided date.
                                ViewBag.ServiceRemarkMsg = $"Last ticketing date: { _previousOutput.Output.LastTicketingDate.ToString("dd MMM yyyy HH:mm")}";
                            }
                            else
                            {
                                // Mayflower system generated expiry date.
                                var _dumpDate = fltBk.BookingExpiryDate ?? fltBk.CreateDateTime.AddMinutes(30);
                                expiredBooking = DateTime.Now.AddMinutes(-30) > _dumpDate;
                                ViewBag.ServiceRemarkMsg = $"Last ticketing date: { _dumpDate.ToString("dd MMM yyyy HH:mm")}";
                            }

                            if (expiredBooking)
                            {
                                fltBk.BookingStatusCode = "EXP";
                                fltBk.ModifiedDate = DateTime.Now;
                                fltBk.ModifiedDateUTC = DateTime.UtcNow;

                                fltBk.SuperPNR.LoadPaymentDetailList(true, "MYR");
                                var _allPayment = fltBk.SuperPNR.PaymentDetailsList.FirstOrDefault(); // converted currency only one record
                                if (_allPayment.TotalPaidAmount == 0)
                                {
                                    fltBk.SuperPNR.SuperPNROrders.ForEach(x =>
                                    {
                                        x.BookingStatusCode = "EXP";
                                        x.ModifiedDate = DateTime.Now;
                                        x.ModifiedDateUTC = DateTime.UtcNow;
                                    });
                                }

                                db.SaveChanges();
                                return RedirectToAction("upcomingtrips", "account", new { repaystatus = "flight-expired" });
                            }
                        }
                        catch
                        {
                        }
                    }

                    // 2014/04/09 - Cross-sell model initialize.
                    var hotelModel = model.ConfirmationOutputDb.SuperPNR.BookingHotels;
                    BookedProductView viewModel = new BookedProductView
                    {
                        Flight = model,
                        Hotel = hotelModel != null ? hotelModel.FirstOrDefault() : null,
                    };
                    return View("OrderHistory", viewModel);
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                Tuple<string, string> errorCodes = UtilitiesService.NlogExceptionForBookingFlow(logger, null, ex, userid, "homesearchflighterror", "", "",
                    $"Requested Flight SuperPNRNo - {bookingID}");

                return RedirectToRoute("BookingPayment");
            }
        }
        #endregion

        #region Common ActionResult Function
        [OutputCache(Location = System.Web.UI.OutputCacheLocation.Server, Duration = 0, VaryByParam = "none")]
        public ActionResult _FareRulesView(int outInIndicator, string serviceSource, string superPNR, int? odoIndex = null)
        {
            StartingPoint:
            FlightBookingModel model = new FlightBookingModel();

            if (!string.IsNullOrEmpty(superPNR) && superPNR != "undefined")
            {
                model.FlightInformation = new FlightInformation
                {
                    SupplierFlightInfo = Alphareds.Module.BookingController.BookingServiceController.GetSabreFlightFromDB(superPNR, new MayFlower())
                };

                var result = model.FlightInformation.SupplierFlightInfo;

                if (result == null || result.PricingInfo == null || result.PricingInfo.FareBreakDown == null)
                {
                    superPNR = null;
                    goto StartingPoint;
                }

                var adt = result.PricingInfo.FareBreakDown.FirstOrDefault(x => x.PassengerTypeCode.IsStringSame("ADT"));
                var cnn = result.PricingInfo.FareBreakDown.FirstOrDefault(x => x.PassengerTypeCode.IsStringSame("CNN"));
                var inf = result.PricingInfo.FareBreakDown.FirstOrDefault(x => x.PassengerTypeCode.IsStringSame("INF"));
                var firstOdo = result.OriginDestinationOptions.FirstOrDefault();
                var lastOdo = result.OriginDestinationOptions.LastOrDefault();
                var firstSeg = firstOdo.FlightSegments.FirstOrDefault();
                var lastSeg = firstOdo.FlightSegments.LastOrDefault();
                model.SearchFlightResultViewModel = new SearchFlightResultViewModel
                {
                    Adults = adt != null ? adt.PassengerTypeQuantity : 0,
                    Childrens = cnn != null ? cnn.PassengerTypeQuantity : 0,
                    Infants = inf != null ? inf.PassengerTypeQuantity : 0,
                    ArrivalStation = lastSeg.ArrivalAirportLocationCode,
                    DepartureStation = firstSeg.DepartureAirportLocationCode,
                    BeginDate = firstSeg.DepartureDateTime,
                    EndDate = lastOdo.FlightSegments.FirstOrDefault().DepartureDateTime
                };

                switch (serviceSource)
                {
                    case "GATE":
                        serviceSource = "GATE_INT";
                        break;
                    case "SBRE":
                        serviceSource = "SACS";
                        break;
                    case "TCG":
                        serviceSource = "TCG";
                        break;
                    case "AASA":
                        serviceSource = "AirAsia";
                        break;
                    case "FRFY":
                        serviceSource = "Firefly";
                        break;
                    case "BRAW":
                        serviceSource = "BritishAirways";
                        break;
                }

                serviceSource = General.CustomizeBaseEncoding.CodeBase64(serviceSource);
            }
            else
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }

            if (model?.FlightSearchResultViewModel != null || model?.FlightInformation != null)
            {
                serviceSource = odoIndex == null ? General.CustomizeBaseEncoding.DeCodeBase64(serviceSource) : serviceSource;
                Alphareds.Module.SabreWebService.SWS.PricedItineryModel pricedItinery = new Alphareds.Module.SabreWebService.SWS.PricedItineryModel();
                Alphareds.Module.SabreWebService.SWS.OriginDestinationOption odo = new Alphareds.Module.SabreWebService.SWS.OriginDestinationOption();
                List<FlightSegmentFareRule> fareRulesViewList = new List<FlightSegmentFareRule>();
                int flightSegmentLength = 0;
                int farelistIndex = 0;
                bool isOutbound = outInIndicator == 0;
                string sessionID = string.Empty;

                try
                {
                    //Compare Tool - Change to Compare Tool
                    if (odoIndex != null)
                    {
                        //Twin - 2017/02/03 - Initialize all children and convert to sabre object
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

                        pricedItinery = Mapper.Map<Alphareds.Module.SabreWebService.SWS.PricedItineryModel>(model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex ?? 0].pricedItineryModel);
                    }
                    else
                    {
                        pricedItinery = model.FlightInformation.SupplierFlightInfo;
                    }

                    odo = pricedItinery.OriginDestinationOptions[outInIndicator];

                    if (serviceSource.IsStringSame("GATE_CHN")
                        || serviceSource.IsStringSame("GATE_INT"))
                    {
                        sessionID = GateServiceCall.Login(serviceSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.GATE_Int.ToString());
                        fareRulesViewList = new Alphareds.Module.BookingController.BookingServiceController().GATE_GetFareRule(model.SearchFlightResultViewModel, pricedItinery, sessionID, isOutbound);
                    }
                    else if (serviceSource.IsStringSame("SACS"))
                    {
                        #region Sabre
                        if (outInIndicator == 1)
                        {
                            int outboundLen = pricedItinery.OriginDestinationOptions[0].FlightSegments.Length;
                            int inboundLen = odo.FlightSegments.Length;
                            flightSegmentLength = outboundLen + inboundLen;
                            farelistIndex = outboundLen;
                        }
                        else if (outInIndicator == 0)
                        {
                            int outboundLen = odo.FlightSegments.Length;
                            flightSegmentLength = outboundLen;
                            farelistIndex = 0;
                        }

                        fareRulesViewList = Alphareds.Module.BookingController.BookingServiceController.GetFareRuleList(farelistIndex, flightSegmentLength, pricedItinery, odo);
                        #endregion
                    }
                    else if (serviceSource.IsStringSame("AirAsia"))
                    {
                        string clientIP = General.Utilities.GetClientIP;
                        string currencyCode = "MYR";
                        sessionID = AAServiceCall.Logon();
                        Alphareds.Module.AAWebService.AAWS.GetAvailabilityResponse getAvRs = Session[sessionAAgetAvRs] == null ?
                                                                                             Alphareds.Module.BookingController.BookingServiceController.AA_GetAvRs(model.SearchFlightResultViewModel, currencyCode, clientIP, sessionID)
                                                                                             : (Alphareds.Module.AAWebService.AAWS.GetAvailabilityResponse)Session[sessionAAgetAvRs];

                        fareRulesViewList = Alphareds.Module.BookingController.BookingServiceController.AA_GetFareRule(isOutbound, getAvRs, pricedItinery, odo, clientIP, currencyCode, sessionID);
                    }
                    else if (serviceSource.IsStringSame("BritishAirways"))
                    {
                        Alphareds.Module.BAWebService.BAWS.FlightPriceRS getFpRs = Alphareds.Module.BookingController.BookingServiceController.BA_GetFpRs(pricedItinery, model.SearchFlightResultViewModel);

                        fareRulesViewList = Alphareds.Module.BookingController.BookingServiceController.BA_GetFareRule(getFpRs, pricedItinery, odo);
                    }
                }
                catch (Exception ex)
                {
                    int userid = CurrentUserID;
                    Logger logger = LogManager.GetCurrentClassLogger();

                    // 2017/01/05 - Second Level Logger
                    logger.Debug(ex, "FareRulesView - Second Level Bug Logger");

                    //Return Item1 as System Error Code, Item2 as Sabre Error Code
                    Tuple<string, string> errorCodes = UtilitiesService.NlogExceptionForBookingFlow(logger, model, ex, userid, "SystemError", "", "");
                }

                Session[sessionfareRuleList] = fareRulesViewList;
                return View("~/Views/Flight/Shared/_FareRulesView.cshtml", fareRulesViewList);
            }
            else
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }
        }

        [OutputCache(Location = System.Web.UI.OutputCacheLocation.Server, Duration = 0, VaryByParam = "none")]
        public ActionResult _FareRulesPartialView(int ruleIndex)
        {
            try
            {
                List<FlightSegmentFareRule> flightSegmentRules = (List<FlightSegmentFareRule>)Session[sessionfareRuleList];

                return View("~/Views/Flight/Shared/_FareRulesPartialView.cshtml", flightSegmentRules[ruleIndex]);
            }
            catch (Exception ex)
            {
                int userid = CurrentUserID;
                Logger logger = LogManager.GetCurrentClassLogger();

                //Return Item1 as System Error Code, Item2 as Sabre Error Code
                Tuple<string, string> errorCodes = UtilitiesService.NlogExceptionForBookingFlow(logger, null, ex, userid, "systemgeneralerror", "", "");
            }

            return null;
        }
        #endregion

        #region Insert CheckOut Product
        private ProductFlight GenerateFlightProduct(Alphareds.Module.SabreWebService.SWS.PricedItineryModel flightInfo
                                                         , SearchFlightResultViewModel searchCriteria
                                                         , List<FlightAvailableSSR> avaSSR
                                                         , Alphareds.Module.CompareToolWebService.CTWS.serviceSource svcSrc)
        {
            return new ProductFlight()
            {
                ProductSeq = 1,
                FlightInfo = new FlightInformation
                {
                    Supplier = svcSrc,
                    SupplierFlightInfo = flightInfo,
                    AvaSSR = avaSSR
                },
                SearchFlightInfo = searchCriteria,
                ContactPerson = null,
                TravellerDetails = null,
                PricingDetail = new ProductPricingDetail()
                {
                    Currency = "MYR",
                    Discounts = null,
                    Items = flightInfo.PricingInfo.FareBreakDown.Select(x =>
                    {
                        return new ProductItem()
                        {
                            BaseRate = x.TotalBeforeTax,
                            GST = x.GoodsAndServicesTax,
                            ItemDetail = x.PassengerTypeCode,
                            ItemQty = x.PassengerTypeQuantity,
                            Surcharge = x.TaxBreakDown.Where(a =>
                                        (!a.Key.IsStringSame("D8") || !a.Key.IsStringSame("GST")) && a.Value >= 0)
                                        .Sum(a => a.Value)
                        };
                    }).ToList(),
                    Sequence = 1
                }
            };
        }
        #endregion

        #region Javascript Result
        [HttpPost]
        public ActionResult CheckSearchCriteria([Bind(Exclude = "SearchResults")]SearchFlightResultViewModel searchModel)
        {
            FlightBookingModel model = new FlightBookingModel();
            if (Session[sessionFlightBooking] != null)
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }
            else
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }

            var currSearch = model.SearchFlightResultViewModel;
            var prevSearch = searchModel;
            bool isSame = currSearch.BeginDate == prevSearch.BeginDate && (currSearch.EndDate.HasValue == prevSearch.EndDate.HasValue && currSearch.EndDate.Value == prevSearch.EndDate.Value) &&
                currSearch.DepartureStationCode == prevSearch.DepartureStationCode && currSearch.ArrivalStationCode == prevSearch.ArrivalStationCode &&
                currSearch.Adults == prevSearch.Adults &&
                currSearch.Childrens == prevSearch.Childrens &&
                currSearch.Infants == prevSearch.Infants &&
                currSearch.TripType == prevSearch.TripType &&
                currSearch.PrefferedAirlineCode == prevSearch.PrefferedAirlineCode &&
                currSearch.DirectFlight == prevSearch.DirectFlight &&
                currSearch.CabinClass == prevSearch.CabinClass
                ;

            if (!isSame)
            {
                return RedirectToAction("Type", "Error", new { id = "session-conflict" });
            }

            return Json(isSame);
        }

        public JavaScriptResult SearchCriteria(string tripid)
        {
            FlightBookingModel model = new FlightBookingModel();
            if (Session[sessionFlightBooking] != null)
            {
                model = (FlightBookingModel)Session[sessionFlightBooking];
            }
            else
            {
                return null;
            }

            string script = @"var searchCriteria = ";
            script += JsonConvert.SerializeObject(model.SearchFlightResultViewModel);
            return JavaScript(script);
        }

        public JavaScriptResult DepartureDate(string tripid)
        {
            FlightBookingModel model = (FlightBookingModel)Session[sessionFlightBooking];

            if (model != null && model.SearchFlightResultViewModel != null && model.SearchFlightResultViewModel.BeginDate.HasValue)
            {
                string dateTime = model.SearchFlightResultViewModel.BeginDate.Value.ToString("ddd MMM dd yyyy HH:mm:ss") + " GMT+0800 (Malay Peninsula Standard Time)";
                string scripts = @"var ServerDateTime = ";
                scripts += "";
                scripts += "new Date('" + dateTime + "')";
                scripts += "";

                DateTime endDate = model.SearchFlightResultViewModel.EndDate ?? model.SearchFlightResultViewModel.BeginDate.Value;

                string arriveTime = endDate.AddDays(1).ToString("ddd MMM dd yyyy HH:mm:ss") + " GMT+0800 (Malay Peninsula Standard Time)";
                scripts = @"var ServerEndDateTime = ";
                scripts += "";
                scripts += "new Date('" + arriveTime + "')";
                scripts += "";

                return JavaScript(scripts);
            }
            else
            {
                return null;
            }
        }
        #endregion

        #region Private Utitlies Helper Method
        public PromoCodeRule GetPromoCodeDiscountRule(SearchFlightResultViewModel searchInfo, MayFlower dbCtx = null, string DPPromoCode = null, string airlineCode = null, List<string> airlineCodeList = null)
        {
            dbCtx = dbCtx ?? new MayFlower();

            var result = dbCtx.PromoCodeRules.Where(
                            x => x.IsActive && DateTime.Now >= x.EffectiveFrom && DateTime.Now <= x.EffectiveTo
                            && searchInfo.EndDate >= x.TravelDateFrom && searchInfo.BeginDate <= x.TravelDateTo
                            && (x.PromoCode == searchInfo.PromoCode || (DPPromoCode != null && x.PromoCode == DPPromoCode))
                            && x.FlightPromo // Suggest use another table to indicate product rather than use column
                            );

            #region check if the promoCode is for specific airline , return null if not match airline
            var checkSpecificAirlineCode = result.FirstOrDefault(x => x.PromoCode == searchInfo.PromoCode)?.IsSpecificAirline;
            if ((checkSpecificAirlineCode ?? false) && (airlineCode != null || airlineCodeList != null)) //need check
            {
                var promoCodeSpecificAirlineDetails = GetPromoCodeSpecificAirlineList(searchInfo.PromoCode, searchInfo.TripType, dbCtx);
                if (promoCodeSpecificAirlineDetails != null && promoCodeSpecificAirlineDetails.Count > 0)
                {
                    if (airlineCode != null)
                    {
                        if (!promoCodeSpecificAirlineDetails.Any(x => x.AirlineCode == airlineCode))
                        {
                            return null;
                        }
                    }
                    else if (airlineCodeList != null)
                    {
                        bool checkIsAllow = false;
                        foreach (var item in promoCodeSpecificAirlineDetails)
                        {
                            if (airlineCodeList.All(x => x == item.AirlineCode))
                            {
                                checkIsAllow = true;
                                break;
                            }
                        }
                        if (!checkIsAllow)
                        {
                            return null;
                        }
                    }
                }
            }
            #endregion

            //Any Airport = "-"
            var departureCountryCode = UtilitiesService.GetDepartureCountry(searchInfo.DepartureStationCode);
            var arrivalCountryCode = UtilitiesService.GetDepartureCountry(searchInfo.ArrivalStationCode);

            Expression<Func<PromoCodeRule, bool>> anyOrigin = (x => x.PromoFlightDestinations.Any(d => (d.DepartureStation == "-" || (d.DepartureStation == "XXX" && d.DepartureCountryCode == departureCountryCode))
                                                              && d.ArrivalStation == searchInfo.ArrivalStationCode && d.Active));
            Expression<Func<PromoCodeRule, bool>> anyDestination = (x => x.PromoFlightDestinations.Any(d => (d.ArrivalStation == "-" || (d.ArrivalStation == "XXX" && d.ArrivalCountryCode == arrivalCountryCode))
                                                                   && d.DepartureStation == searchInfo.DepartureStationCode && d.Active));
            Expression<Func<PromoCodeRule, bool>> anyDesAndOri = (x => x.PromoFlightDestinations.Any(d => (d.ArrivalStation == "-" || (d.ArrivalStation == "XXX" && d.ArrivalCountryCode == arrivalCountryCode))
                                                                  && (d.DepartureStation == "-" || (d.DepartureStation == "XXX" && d.DepartureCountryCode == departureCountryCode)) && d.Active));

            bool anyOriginOK = result.Any(anyOrigin);
            bool anyDestinationOK = result.Any(anyDestination);
            bool anyDesOriOK = result.Any(anyDesAndOri);

            if (anyDesOriOK)
            {
                return result.FirstOrDefault(anyDesAndOri);
            }
            else if (anyOriginOK)
            {
                return result.FirstOrDefault(anyOrigin);
            }
            else if (anyDestinationOK)
            {
                return result.FirstOrDefault(anyDestination);
            }
            else
            {
                return result.FirstOrDefault(x => x.PromoFlightDestinations
                       .Any(d => d.DepartureStation == searchInfo.DepartureStationCode
                                 && d.ArrivalStation == searchInfo.ArrivalStationCode && d.Active));
            }
        }

        public PromoCodeRule GetPromoCodeFunctionRule(int Frontendfunc, SearchFlightResultViewModel model, MayFlower dbContext = null)
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

        public decimal CalcPromoCodeDiscount(PromoCodeRule promocode, decimal amt)
        {
            decimal discountAmt = 0;
            if (promocode != null)
            {
                bool isPct = promocode.PricingTypeCode == "PCT";
                decimal maxDisc = promocode.MaxDiscountAmt;
                discountAmt = isPct ? amt * (promocode.DiscountAmtOrPCT / 100) : promocode.DiscountAmtOrPCT;
                if (discountAmt > maxDisc && maxDisc > 0)
                {
                    discountAmt = maxDisc;
                }
            }

            return discountAmt.RoundToDecimalPlace();
        }

        public List<PromoCodeSpecificAirlineDetail> GetPromoCodeSpecificAirlineList(string promoCode, string tripType, MayFlower dbContext = null)
        {
            dbContext = dbContext ?? new MayFlower();
            var list = dbContext.PromoCodeSpecificAirlines.FirstOrDefault(x => x.PromoCode == promoCode && x.isActive && (x.TripType == "ALL" || x.TripType == tripType));

            if (list != null)
            {
                if (list.PromoCodeSpecificAirlineDetails.Count > 0)
                {
                    return list.PromoCodeSpecificAirlineDetails.Where(x => x.isActive).ToList();
                }
            }
            return new List<PromoCodeSpecificAirlineDetail>();
        }

        public bool isPromoFlightList(PromoCodeRule promocode, SearchFlightResultViewModel model, Alphareds.Module.CompareToolWebService.CTWS.serviceSource serviceSource)
        {
            var FltSupplierCode = UtilitiesService.ServiceSourceToDBServiceSourceName(serviceSource);
            var promoFlightList = promocode != null && promocode.PromoFlightDestinations.Count > 0 ? promocode.PromoFlightDestinations.FirstOrDefault(x => (x.ArrivalStation == model.ArrivalStationCode || x.ArrivalStation == "-") && (x.DepartureStation == model.DepartureStationCode || x.DepartureStation == "-") && x.Active)?.PromoFlightLists : null;
            bool isPromoFlightSupplier = promocode != null && promoFlightList != null && promoFlightList.Count > 0 && promoFlightList.Any(d => (d.SupplierCode.Contains(FltSupplierCode) || d.SupplierCode == "ALL") && d.Active);

            return isPromoFlightSupplier;
        }

        public Task<FlightBookingModel> GetFlightFixedSearchAsync(FlightBookingModel model, System.Security.Principal.IPrincipal User, bool isAgent)
        {
            return Task.Factory.StartNew(() =>
            {
                return GetFlightFixedSearch(model, User, isAgent);
            });
        }

        public FlightBookingModel GetFlightFixedSearch(FlightBookingModel model, System.Security.Principal.IPrincipal User, bool isAgent)
        {
            model.FlightSearchResultViewModel = new FlightSearchResultViewModel();
            Tuple<FlightBookingModel, string> resultFromService = Alphareds.Module.HomeController.HomeServiceController.getSearchFlightResult(model, User.Identity.Name.ToInt(), isAgent);

            if (!string.IsNullOrEmpty(resultFromService.Item2))
            {
                model = resultFromService.Item1;

                model.FlightSearchResultViewModel = new FlightSearchResultViewModel()
                {
                    FullFlightSearchResult = new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>()
                };

                // 20161209 - Remove session to ensure no previous flight list in filter panel
                Core.SetSession(Enumeration.SessionName.FullFlightSearchResult, tripid, null);
                Core.SetSession(Enumeration.SessionName.FullODOResult, tripid, null);
                Core.SetSession(Enumeration.SessionName.FilterParam, tripid, null);
                Core.SetSession(Enumeration.SessionName.MHFilterFlightModel, tripid, null);
            }
            else
            {
                model = resultFromService.Item1;
                if (resultFromService.Item1.FlightSearchResultViewModel.FullFlightSearchResult.Count == 0)
                {
                    model.FlightSearchResultViewModel = new FlightSearchResultViewModel()
                    {
                        FullFlightSearchResult = new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>()
                    };
                    Core.SetSession(Enumeration.SessionName.FullFlightSearchResult, tripid, null);
                    Core.SetSession(Enumeration.SessionName.FullODOResult, tripid, null);
                    Core.SetSession(Enumeration.SessionName.FilterParam, tripid, null);
                    Core.SetSession(Enumeration.SessionName.MHFilterFlightModel, tripid, null);
                }
                else
                {
                    #region Clone Flight Booking Model
                    bool isCloneDumpResultList = false;
                    bool.TryParse(Core.GetAppSettingValueEnhanced("CloneDumpResultList"), out isCloneDumpResultList);
                    if (isCloneDumpResultList)
                    {
                        CloneFlightBookingModel(model);
                    }
                    #endregion
                    if (User.Identity.IsAuthenticated && Core.IsEnablePayByPromoCode && !model.SearchFlightResultViewModel.IsPromoCodeUsed)
                    {
                        MayFlower db = new MayFlower();
                        var user = Alphareds.Module.Common.Core.GetUserInfo(User.Identity.Name, db);
                        int promoID = user.UserPromoes.FirstOrDefault(x => x.IsActive)?.PromoID ?? 0;
                        if (promoID != 0)
                        {
                            string promoCode = db.PromoCodeRules.FirstOrDefault(x => x.PromoID == promoID).PromoCode;
                            model.SearchFlightResultViewModel.PromoCode = promoCode;
                            var promoCodeRule = GetPromoCodeDiscountRule(model.SearchFlightResultViewModel, db);
                            if (promoCodeRule == null)
                            {
                                model.SearchFlightResultViewModel.PromoCode = string.Empty;
                                model.SearchFlightResultViewModel.PromoId = 0;
                            }
                        }
                    }
                    //flight only search for auto applied promo rule 
                    if (Core.IsEnablePayByPromoCode && !model.SearchFlightResultViewModel.IsPromoCodeUsed)
                    {
                        MayFlower db = new MayFlower();
                        int Frontendfunc = model.SearchFlightResultViewModel.IsDynamic ? (int)Alphareds.Module.Model.FrontendFunction.Enum.FrontendFunction.PackageAutoApplied : (int)Alphareds.Module.Model.FrontendFunction.Enum.FrontendFunction.FlightAutoApplied;
                        var promoCodeRule = GetPromoCodeFunctionRule(Frontendfunc, model.SearchFlightResultViewModel, db);
                        model.SearchFlightResultViewModel.PromoCode = promoCodeRule != null ? promoCodeRule.PromoCode : null;
                    }
                    #region Promo Code Section 
                    if (Core.IsEnablePayByPromoCode && model.SearchFlightResultViewModel.IsPromoCodeUsed)
                    {
                        try
                        {
                            MayFlower db = new MayFlower();
                            var promoCodeRule = GetPromoCodeDiscountRule(model.SearchFlightResultViewModel, db);

                            CheckoutProduct _checkoutProduct = new CheckoutProduct(); // use for check promo code functions
                            _checkoutProduct.PromoID = promoCodeRule?.PromoID ?? 0;
                            PromoCodeFunctions promoCodeFunctions = _checkoutProduct.PromoCodeFunctions;

                            // Overrided not comming from Affiliate Program
                            if (promoCodeFunctions.GetFrontendFunction.WaiveCreditCardFee)
                            {
                                string _reffCode = model.SearchFlightResultViewModel.AffiliationId?.ToLower();
                                var _validAffiliate = db.Affiliations.Any(x => x.UserCode == _reffCode);

                                if (!_validAffiliate)
                                {
                                    model.SearchFlightResultViewModel.PromoCode = null;
                                    model.SearchFlightResultViewModel.PromoId = 0;
                                    _checkoutProduct.PromoID = 0;
                                    promoCodeRule = null;
                                }
                            }

                            if (((promoCodeRule != null && promoCodeRule.IsPackageOnly) || promoCodeFunctions.GetFrontendFunction.PackageAutoApplied) && !model.SearchFlightResultViewModel.IsDynamic)
                            {
                                _checkoutProduct.PromoID = 0;
                                promoCodeRule = null;
                            }

                            if (Core.IsEnablePackageDiscount && model.SearchFlightResultViewModel.IsDynamic)
                            {
                                model.SearchFlightResultViewModel.DPPromoCode = model.SearchFlightResultViewModel.PromoCode;
                            }
                            if (promoCodeRule == null || (Core.IsEnablePackageDiscount && model.SearchFlightResultViewModel.IsDynamic))
                            {
                                model.SearchFlightResultViewModel.PromoCode = string.Empty;
                                model.SearchFlightResultViewModel.PromoId = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Flight Search Promo Code Error - {DateTime.Now.ToLoggerDateTime()}");
                        }
                    }
                    #endregion

                    // Initialize new instance to avoid overwrite
                    List<Alphareds.Module.CompareToolWebService.CTWS.flightData> cloneResult = new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();
                    var preRequestResult = model.FlightSearchResultViewModel.FullFlightSearchResult.OrderBy(o => o.pricedItineryModel.PricingInfo.TotalAfterTax);
                    if (model.FlightSearchResultViewModel != null && model.FlightSearchResultViewModel.FullFlightSearchResult != null)
                    {
                        model.FlightSearchResultViewModel.FullFlightSearchResult = preRequestResult.ToList();
                    }
                    model.SearchFlightResultViewModel.FixedPriceFrom = preRequestResult.FirstOrDefault()?.pricedItineryModel.PricingInfo.TotalAfterTax ?? 0;
                    cloneResult = model.FlightSearchResultViewModel.FullFlightSearchResult;
                    //Core.SetSession(Enumeration.SessionName.FullFlightSearchResult, tripid, cloneResult);
                    //Core.SetSession(Enumeration.SessionName.FullODOResult, tripid, JsonConvert.DeserializeObject<FlightBookingModel>(JsonConvert.SerializeObject(resultFromService.Item1)));
                    //Core.SetSession(Enumeration.SessionName.FilterParam, tripid, null);
                    Session[sessionFullFlightSearchResult] = cloneResult;
                    Session[sessionFullODOResult] = JsonConvert.DeserializeObject<FlightBookingModel>(JsonConvert.SerializeObject(resultFromService.Item1));
                    Session.Remove(sessionFilterParam);
                }
            }
            return model;
        }

        private string GetUserIP()
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

        private bool IsSelfBookingOrGuest(int userLogged, int userIDBooking)
        {
            return userIDBooking == 0 || userLogged == userIDBooking ? true : false;
        }

        private void CloneFlightBookingModel(FlightBookingModel model)
        {
            var cloneFlightResult = JsonConvert.SerializeObject(model);
            var fileName = System.IO.Path.GetFileName("FlightListResult" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
            var path = Server.MapPath("~/cache/");
            var fullPath = System.IO.Path.Combine(path, fileName);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            System.IO.File.WriteAllText(fullPath, cloneFlightResult);
        }

        private bool IsAgentUser
        {
            get
            {
                return CustomPrincipal.IsAgent;
            }
        }

        public System.Data.Entity.Core.Objects.ObjectResult<usp_StationCitySelect1_Result> StationCitySelect(string stationcode, MayFlower dbContext = null)
        {
            List<EventMaster> eventList = new List<EventMaster>();
            dbContext = dbContext ?? new MayFlower();
            Logger logger = LogManager.GetCurrentClassLogger();

            try
            {
                return dbContext.usp_StationCitySelect1(stationcode);
            }
            catch (AggregateException ae)
            {
                logger.Warn(ae.GetBaseException(), "Error while getting station city select list. (AggregateException)");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Error while getting station city select list. (All Exception)");
            }

            return null;
        }

        #endregion
    }
}