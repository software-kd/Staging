using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Xml;
using System.Configuration;
using System.Data;
using PagedList;
using System.Web.Caching;
using NLog;
using System.Web.UI;
using Alphareds.Module.Common;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.HotelController;
using Alphareds.Module.FlightSearchController;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Data.SqlClient;
using Alphareds.Module.MemberController;
using AutoMapper;
using Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Alphareds.Module.ServiceCall;

namespace Mayflower.Controllers
{
    // 2016/11/07 - Mayflower allow guest booking
    //[Authorize]
    [Filters.ControllerAccess.CheckOnConfiguration(AppKey = "OldFlightSearchController")]
    [Filters.PreserveQueryStringFilter(QueryString = "tripid")]
    public class FlightSearchController : AsyncController
    {
        private static string tripid
        {
            get
            {
                var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
                var routeValue = request.RequestContext.RouteData.Values["tripid"];
                string routeString = routeValue != null ? routeValue.ToString() : null;

                string obj = System.Web.HttpContext.Current.Request.QueryString["tripid"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["tripid"]);
                return obj;
            }
        }
        private string sessionFlightBooking = Enumeration.SessionName.FlightBooking + tripid;
        private string sessionFullODOResult = Enumeration.SessionName.FullODOResult + tripid;
        private string sessionFullFlightSearchResult = Enumeration.SessionName.FullFlightSearchResult + tripid;
        private string sessionFilterParam = Enumeration.SessionName.FilterParam + tripid;
        private string sessionMHFilterFlightModel = Enumeration.SessionName.MHFilterFlightModel + tripid;

        /// <summary>
        /// For Iframe usage, will cause not postback to external page. Not use at the moments.
        /// </summary>
        /// <returns></returns>
        public ActionResult _ModifySearchIframe()
        {
            FlightBookingModel model = Session[sessionFlightBooking] == null ? null : (FlightBookingModel)Session[sessionFlightBooking];
            return View(model);
        }

        public ActionResult FlightSearchResult(string errorMsg, int? page, string sort, string size, FilterFlightModel filter, string filterstatus, string tripid)
        {
            int userID = CurrentUserID;
            bool isAgent = IsAgentUser;

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
            int userid = CurrentUserID;

            // 20161126 - Testing
            if (!Request.IsAjaxRequest())
            {
                return View(model);
            }
            else if (Request.IsAjaxRequest() && model.FlightSearchResultViewModel == null)
            {
                model.FlightSearchResultViewModel = new FlightSearchResultViewModel();

                Tuple<FlightBookingModel, string> resultFromService = Alphareds.Module.HomeController.HomeServiceController.getSearchFlightResult(model, userID);

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

                    //if (!isAgent)
                    //{
                    //    model.FlightSearchResultViewModel.FullFlightSearchResult = FlightSearchServiceController.bindGSTandServiceFeeToFlightResults(model.FlightSearchResultViewModel.FullFlightSearchResult, model.SearchFlightResultViewModel.isDomesticFlight, model.SearchFlightResultViewModel.CabinClass);
                    //}

                    #region Clone Flight Booking Model
                    bool isCloneDumpResultList = false;
                    bool.TryParse(Core.GetAppSettingValueEnhanced("CloneDumpResultList"), out isCloneDumpResultList);
                    if (isCloneDumpResultList)
                    {
                        CloneFlightBookingModel(model);
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

            #region 2016/11/09 - Hide From Phase 1, Sort/Filter based on User Preferences
            /*
            #region SortBasedOnUserPreferences
            UserPreference userPreference = UtilitiesService.GetCurrentUserPreferences(userid);
            model.UserPreferencesModel = new UserPreferencesModel();
            model.UserPreferencesModel.UserPreference = userPreference;

            if (!Request.IsAjaxRequest())
            {
                TempData["UserPreferenceSort"] = userPreference.DisplayResultSortCode;

                List<PricedItineryModel> userPreferenceSortList = new List<PricedItineryModel>();
                userPreferenceSortList = model.FlightSearchResultViewModel.FullFlightSearchResult.ToList();

                if (userPreference.DisplayResultSortCode.Equals("D"))
                {
                    if (userPreferenceSortList[0].OriginDestinationOptions.Count() == 1)
                    {
                        userPreferenceSortList = userPreferenceSortList.OrderBy(o => o.OriginDestinationOptions[0].TotalElapsedTime).ToList();
                    }
                    else
                    {
                        userPreferenceSortList = userPreferenceSortList.OrderBy(o => o.OriginDestinationOptions[0].TotalElapsedTime + o.OriginDestinationOptions[1].TotalElapsedTime).ToList();
                    }
                }
                else if (userPreference.DisplayResultSortCode.Equals("P"))
                {
                    userPreferenceSortList = userPreferenceSortList.OrderBy(o => o.PricingInfo.TotalAfterTax).ToList();
                }
                else if (userPreference.DisplayResultSortCode.Equals("DT"))
                {
                    userPreferenceSortList = userPreferenceSortList.OrderBy(o => o.OriginDestinationOptions[0].FlightSegments[0].DepartureDateTime).ToList();
                }

                model.FlightSearchResultViewModel.FullFlightSearchResult = userPreferenceSortList;
            }
            #endregion

            #region FilterBasedOnUserPreferences
            if (!Request.IsAjaxRequest())
            {
                List<PricedItineryModel> listfilteruserPreferences = new List<PricedItineryModel>();
                listfilteruserPreferences = model.FlightSearchResultViewModel.FullFlightSearchResult.ToList();

                if (!userPreference.AirlineCode1.Equals("-") || !userPreference.AirlineCode2.Equals("-") || !userPreference.AirlineCode3.Equals("-"))
                {
                    bool withairlineflight = true;
                    for (int i = listfilteruserPreferences.Count() - 1; i >= 0; i--)
                    {
                        for (int a = 0; a < listfilteruserPreferences[i].OriginDestinationOptions.Count(); a++)
                        {
                            for (int b = 0; b < listfilteruserPreferences[i].OriginDestinationOptions[a].FlightSegments.Count(); b++)
                            {
                                if (userPreference.AirlineCode1.Equals(listfilteruserPreferences[i].OriginDestinationOptions[a].FlightSegments[b].AirlineCode) || userPreference.AirlineCode2.Equals(listfilteruserPreferences[i].OriginDestinationOptions[a].FlightSegments[b].AirlineCode) || userPreference.AirlineCode3.Equals(listfilteruserPreferences[i].OriginDestinationOptions[a].FlightSegments[b].AirlineCode))
                                {
                                    withairlineflight = true;
                                    break;
                                }
                                else
                                {
                                    withairlineflight = false;
                                }

                                if (withairlineflight)
                                    break;
                            }

                            if (withairlineflight)
                                break;
                        }
                        if (!withairlineflight)
                            listfilteruserPreferences.RemoveAt(i);
                    }

                    //if (listfilteruserPreferences.Count() == 0)
                    //{
                    //    string errorMessage = "According your display preferences, no flight result found.";
                    //    return RedirectToAction("Index", "Home", new { errorMsg = errorMessage });
                    //}

                    //Session["Listfiltered"] = listfilteruserPreferences;
                }

                if (userPreference.MaximumStop < 10)
                {
                    if (!model.SearchFlightResultViewModel.DirectFlight)
                    {
                        for (int i = listfilteruserPreferences.Count() - 1; i >= 0; i--)
                        {
                            for (int a = 0; a < listfilteruserPreferences[i].OriginDestinationOptions.Count(); a++)
                            {
                                if (listfilteruserPreferences[i].OriginDestinationOptions[a].FlightSegments.Count() > (userPreference.MaximumStop + 1))
                                {
                                    listfilteruserPreferences.RemoveAt(i);
                                    break;
                                }
                            }
                        }

                        //if (listfilteruserPreferences.Count() == 0)
                        //{
                        //    string errorMessage = "According your display preferences, no flight result found.";
                        //    return RedirectToAction("Index", "Home", new { errorMsg = errorMessage });
                        //}

                        //Session["Listfiltered"] = listfilteruserPreferences;
                    }
                }

                if (listfilteruserPreferences.Count() == 0)
                {
                    Session["Listfiltered"] = model.FlightSearchResultViewModel.FullFlightSearchResult.ToList();
                }
                else
                {
                    Session["Listfiltered"] = listfilteruserPreferences;
                }
            }
            #endregion
            */
            #endregion

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

                // Inbound
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
                            x => x.pricedItineryModel.OriginDestinationOptions.SelectMany(seg => seg.FlightSegments.Select(a => a.AirlineCode)).Distinct().Count() > 1
                            );
                    }
                    else
                    {
                        preRequestResult = preRequestResult.Where(
                            x => x.pricedItineryModel.OriginDestinationOptions.SelectMany(seg => seg.FlightSegments.Select(a => a.AirlineCode)).Distinct().Count() == 1
                            && x.pricedItineryModel.OriginDestinationOptions.SelectMany(seg => seg.FlightSegments.Select(a => a.AirlineCode)).Any(c => airlineFilter.Contains(c))
                            );
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
                ? (ActionResult)PartialView("FlightSearchResultList", model)
                : View(model);
        }

        [HttpPost]
        public ActionResult FlightSearchResult(FlightBookingModel model, string outbound, string inbound, string grpTag, string tripid)
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

                // set selected flight here
                int counter = 0;
                List<int> _outboundIndex = new List<int>();
                List<int> _innboundIndex = new List<int>();
                model.FlightSearchResultViewModel = model.FlightSearchResultViewModel ?? new FlightSearchResultViewModel();
                model.FlightSearchResultViewModel.FullFlightSearchResult = model.FlightSearchResultViewModel.FullFlightSearchResult ?? new List<Alphareds.Module.CompareToolWebService.CTWS.flightData>();

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
                if (_innboundIndex.Count != 0)
                {
                    bool isPairable = _outboundIndex.Intersect(_innboundIndex).Any();

                    if (isPairable)
                    {
                        odoIndex = isPairable ? _outboundIndex.Intersect(_innboundIndex).First() : odoIndex;
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

                        Alphareds.Module.CompareToolWebService.CTWS.serviceSource serviceSource = model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex].ServiceSource;
                        //model.FlightSearchResultViewModel.SelectedSource = serviceSource;
                        //model.FlightSearchResultViewModel.Result = Mapper.Map<Alphareds.Module.SabreWebService.SWS.PricedItineryModel>(model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex].pricedItineryModel);

                        //model.FlightSearchResultViewModel.FlightSelected = model.FlightSearchResultViewModel.Result.ConvertToFlightSelectedViewModel(serviceSource);
                        //model.BookingSummaryViewModel = FlightSearchServiceController.populateBookingSummaryViewModel(model.FlightSearchResultViewModel.Result, model.FlightSearchResultViewModel.FlightSelected.OriginStationCode, model.FlightSearchResultViewModel.FlightSelected.DestinationStationCode, null, serviceSource.ToString(), model.SearchFlightResultViewModel.CabinClass);

                        Session[sessionFlightBooking] = model;

                        IEnumerable<CrossSaleRule> crossSaleAvailaible = null;
                        //if (Core.IsEnableHotelCrossSales)
                        //{
                        //    crossSaleAvailaible = CheckIsCrossSalesHotelAvailaible(model.FlightSearchResultViewModel.FlightSelected);
                        //    var crossSaleRuleHotel = crossSaleAvailaible != null ? crossSaleAvailaible.SelectMany(x => x.CrossSaleRuleHotels) : null;
                        //    Session["CrossSaleRules" + tripid] = crossSaleRuleHotel;
                        //}

                        //Get Available Baggage - AA / TCG
                        //if (model.FlightSearchResultViewModel.SelectedSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia
                        //    || model.FlightSearchResultViewModel.SelectedSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.TCG)
                        //{
                        //    model.FlightSearchResultViewModel.BaggageList = Alphareds.Module.BookingController.BookingServiceController.GetAvailableBaggage(model.SearchFlightResultViewModel, model.FlightSearchResultViewModel.Result, model.FlightSearchResultViewModel.SelectedSource, "MYR", General.Utilities.GetClientIP);
                        //}

                        return Core.IsEnableHotelCrossSales && crossSaleAvailaible != null ?
                        RedirectToAction("AddOn", new { tripid }) : RedirectToAction("FlightDetail", new { tripid });
                    }
                    else
                    {
                        return RedirectToAction("FlightSearchResult", new { status = "Invalid pairing result.", tripid });
                    }
                }
                else if (_outboundIndex.Count != 0)
                {
                    odoIndex = _outboundIndex.FirstOrDefault();

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

                    //Alphareds.Module.CompareToolWebService.CTWS.serviceSource serviceSource = model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex].ServiceSource;
                    //model.FlightSearchResultViewModel.SelectedSource = serviceSource;
                    //model.FlightSearchResultViewModel.Result = Mapper.Map<Alphareds.Module.SabreWebService.SWS.PricedItineryModel>(model.FlightSearchResultViewModel.FullFlightSearchResult[odoIndex].pricedItineryModel);

                    //model.FlightSearchResultViewModel.FlightSelected = model.FlightSearchResultViewModel.Result.ConvertToFlightSelectedViewModel(serviceSource);
                    //model.BookingSummaryViewModel = FlightSearchServiceController.populateBookingSummaryViewModel(model.FlightSearchResultViewModel.Result, model.SearchFlightResultViewModel.DepartureStationCode, model.SearchFlightResultViewModel.ArrivalStationCode, null, model.FlightSearchResultViewModel.SelectedSource.ToString(), model.SearchFlightResultViewModel.CabinClass);
                    //Session[sessionFlightBooking] = model;

                    ////Get Available Baggage - AA / TCG
                    //if (model.FlightSearchResultViewModel.SelectedSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia
                    //    || model.FlightSearchResultViewModel.SelectedSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.TCG)
                    //{
                    //    model.FlightSearchResultViewModel.BaggageList = Alphareds.Module.BookingController.BookingServiceController.GetAvailableBaggage(model.SearchFlightResultViewModel, model.FlightSearchResultViewModel.Result, model.FlightSearchResultViewModel.SelectedSource, "MYR", General.Utilities.GetClientIP);
                    //}

                    return RedirectToAction("FlightDetail", new { tripid });
                }
                else
                {
                    return RedirectToAction("FlightSearchResult", new { status = "Invalid pairing result.", tripid });
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

        [Filters.SessionFilter(SessionName = "CrossSaleRules")]
        public async Task<ActionResult> AddOn(string tripid, IEnumerable<CrossSaleRuleHotel> crossSaleRule = null)
        {
            var model = (FlightBookingModel)Session[sessionFlightBooking];
            //var flightSelected = model.FlightSearchResultViewModel.FlightSelected;
            //double stayTotalDays = flightSelected.ReturnDate.Date.Subtract(flightSelected.DepartureDate.Date).TotalDays;
            //bool lessThan28Days = stayTotalDays < 28;
            //string info = lessThan28Days ? null : "longtrip";
            crossSaleRule = crossSaleRule ?? (IEnumerable<CrossSaleRuleHotel>)Session["CrossSaleRules" + tripid];

            bool forceCrossSale = false;

            //if (model.FlightSearchResultViewModel.SelectedSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia)
            //{
            //    forceCrossSale = true;
            //}

            //if (crossSaleRule != null)
            //{
            //    crossSaleRule = crossSaleRule.OrderBy(x => x.CrossSaleRule.BookingDateFrom);
            //    //.Where(x => Regex.IsMatch(x.HotelID, @"^\d+$", RegexOptions.Compiled));

            //    if (lessThan28Days && stayTotalDays != 0 && crossSaleRule.Count() > 0)
            //    {
            //        crossSaleRule = crossSaleRule.Take(3);
            //        Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse hotelList = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse();
            //        List<string> expediaHotelID = crossSaleRule.Select(x => x.HotelID).ToList();
            //        List<string> supplierCode = crossSaleRule.Select(x => x.HotelSupplierCode).Distinct().ToList();

            //        HotelController hc = new HotelController();
            //        SearchHotelModel searchHotelReq = new SearchHotelModel
            //        {
            //            ArrivalDate = flightSelected.DepartureDate.Date,
            //            DepartureDate = flightSelected.ReturnDate.Date,
            //            CurrencyCode = "MYR",
            //            CustomerIpAddress = Request.UserHostAddress,
            //            CustomerUserAgent = Request.UserAgent,
            //            CustomerSessionId = tripid,
            //            NoOfAdult = model.SearchFlightResultViewModel.Adults,
            //            NoOfInfant = model.SearchFlightResultViewModel.Childrens,
            //            //NoOfAdult = 1,
            //            //NoOfInfant = 1,
            //            NoOfRoom = 1,
            //            Destination = flightSelected.OriginStationCode,

            //            SupplierIncluded = new SearchSupplier()
            //            {
            //                Expedia = supplierCode.Contains("EAN") ? true : false,
            //                JacTravel = supplierCode.Contains("JAC") ? true : false,
            //                Tourplan = supplierCode.Contains("TP") ? true : false
            //            }
            //        };

            //        int attemp = 0;
            //        attemArea:
            //        hotelList = await getHotelFromSearchModel(searchHotelReq, expediaHotelID);
            //        searchHotelReq.Result = hotelList;
            //        bool isErrorDuringGetHotel = (hotelList == null || hotelList.Errors != null || hotelList.HotelList == null || (hotelList.HotelList != null && hotelList.HotelList.Length == 0));
            //        if (attemp <= 3 && isErrorDuringGetHotel)
            //        {
            //            attemp++;
            //            goto attemArea;
            //        }
            //        else if (attemp > 2 && isErrorDuringGetHotel)
            //        {
            //            string msg = hotelList != null && hotelList.Errors != null ? hotelList.Errors.ErrorMessage : "Service no respond.";
            //            Logger logger = LogManager.GetCurrentClassLogger();
            //            logger.Error("Error on Flight Search Cross Sales get hotel. " + msg);
            //            //return View(hotelList.HotelList);
            //        }

            //        if (!isErrorDuringGetHotel)
            //        {
            //            Core.SetSession(Enumeration.SessionName.HotelList, tripid, searchHotelReq);
            //            hotelList.HotelList = HotelServiceController.ProcessDiscountCalculation(hotelList.HotelList, crossSaleRule);
            //            hotelList.HotelList = hotelList.HotelList.OrderBy(x => x.lowRate).ToArray();
            //            CrossSellModels crossSellModels = new CrossSellModels
            //            {
            //                ForceCrossSell = forceCrossSale,
            //                CrossSellRules = crossSaleRule,
            //                HotelInformation = hotelList.HotelList,
            //            };

            //            return View(crossSellModels);
            //        }
            //        else
            //        {
            //            Session["isErrorDuringGetHotel" + tripid] = isErrorDuringGetHotel;
            //        }
            //    }
            //}

            return RedirectToAction("FlightDetail", "FlightSearch", new
            {
                tripid,
                //info
            });
        }

        private async Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> getHotelFromSearchModel(SearchHotelModel searchModel)
        {
            return await Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
            {
                return ESBHotelServiceCall.GetHotelList(searchModel);
            });
        }

        public Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> getHotelFromSearchModel(SearchHotelModel searchModel, List<string> hotelID)
        {
            var emptyResult = Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
            {
                return null;
            });

            try
            {
                System.Threading.CancellationTokenSource tokenSource = new System.Threading.CancellationTokenSource();
                tokenSource.CancelAfter(5000);
                var task = Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
                    {
                        return Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelList(searchModel, hotelID);
                    });

                Task[] taskList = new Task[] { task };

                Task.WaitAny(taskList, (int)5000, tokenSource.Token);
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

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult FlightDetail(FlightBookingModel model2, string tripid)
        {
            //string sessionNameBooking = "FlightBooking"; // + tripid;
            if (Session[sessionFlightBooking] == null)
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }

            FlightBookingModel model = (FlightBookingModel)Session[sessionFlightBooking];
            HotelCheckoutModel checkoutModel = (HotelCheckoutModel)Core.GetSession(Enumeration.SessionName.HotelCheckOut, tripid);
            var crossSaleHotel = Session["CrossSaleRules" + tripid];
            bool crossSaleExist = crossSaleHotel != null;
            bool? isErrorDuringGetHotel = Session["isErrorDuringGetHotel" + tripid] == null ? (bool?)null : (bool)Session["isErrorDuringGetHotel" + tripid];

            //Check is force cross sale
            if (UtilitiesService.IsForceCrossSaleError(model, checkoutModel, crossSaleExist) && (!isErrorDuringGetHotel ?? false))
            {
                return RedirectToAction("Index", "Home");
            }

            //if (model.FlightSearchResultViewModel.Result == null)
            //{
            //    return RedirectToAction("FlightSearchResult", "FlightSearch", new { tripid });
            //}

            int userid = CurrentUserID;
            model = FlightSearchServiceController.getPassengerDetailPage(model, userid);

            return Request.IsAjaxRequest()
                ? (ActionResult)PartialView("_FlightDetailPassengers", model)
                : View(model);
        }

        [HttpPost]
        public ActionResult FlightDetail(FlightBookingModel postbackModel, string btnSubmit, string tripid)
        {
            //string sessionNameBooking = "FlightBooking"; // + tripid;

            if (Request.IsAjaxRequest())
            {
                if (Session[sessionFlightBooking] == null)
                {
                    return RedirectToAction("Index", "Home");
                }

                FlightBookingModel model = new FlightBookingModel();
                model = (FlightBookingModel)Session[sessionFlightBooking];
                model.PassengerDetailBooking = postbackModel.PassengerDetailBooking;
                string userid = CurrentUserID.ToString();

                model = FlightSearchServiceController.getPassengerDetail(model, postbackModel, userid);

                return (ActionResult)PartialView("_FlightDetailPassengers", model);
            }
            else
            {
                FlightBookingModel sessionModel = new FlightBookingModel();
                sessionModel = (FlightBookingModel)Session[sessionFlightBooking];
                sessionModel.PassengerDetailBooking = postbackModel.PassengerDetailBooking.Select(x => { x._DepartureDate = sessionModel.SearchFlightResultViewModel.BeginDate.Value; return x; }).ToList();
                sessionModel.MemberRegisterModels = postbackModel.MemberRegisterModels;
                sessionModel.isRegister = postbackModel.isRegister;
                //var errors = ModelState.Values.SelectMany(v => v.Errors);

                bool isValidPassportExpiryDate = true;
                var passengers = sessionModel.PassengerDetailBooking.Where(x => x.PassportExpiryDate != null);
                if (sessionModel.SearchFlightResultViewModel.TripType.ToLower() == "oneway")
                {
                    isValidPassportExpiryDate = passengers.Any() ? passengers.Any(x => x.PassportExpiryDate >= sessionModel.SearchFlightResultViewModel.BeginDate) : true;
                    if (!isValidPassportExpiryDate)
                        ModelState.AddModelError("passportValidation", "Oops, look like your Passport Expiry Date early than your Departure Date ("
                            + sessionModel.SearchFlightResultViewModel.BeginDate.Value.ToString("dd-MMM-yy, ddd") + ").");
                }
                else
                {
                    isValidPassportExpiryDate = passengers.Any() ? passengers.Any(x => x.PassportExpiryDate >= sessionModel.SearchFlightResultViewModel.EndDate) : true;
                    if (!isValidPassportExpiryDate)
                        ModelState.AddModelError("passportValidation", "Oops, look like your Passport Expiry Date early than your Return Date ("
                            + sessionModel.SearchFlightResultViewModel.EndDate.Value.ToString("dd-MMM-yy, ddd") + ").");
                }

                if (false)
                {
                    bool isValidPassportNumber = passengers.Any() ? passengers.Any(x => !string.IsNullOrEmpty(x.PassportNumber)) : true;
                    if (!isValidPassportNumber)
                        ModelState.AddModelError("passportValidation", "Oops, look like your Passport Number is empty");

                    bool isNotNullPassportExpiryDate = passengers.Any() ? passengers.Any(x => x.PassportExpiryDate != null) : true;
                    if (!isNotNullPassportExpiryDate)
                        ModelState.AddModelError("passportValidation", "Oops, look like your Passport Expiry Date is empty");
                }

                if (!string.IsNullOrWhiteSpace(postbackModel.BookingContactPerson.Phone2) && string.IsNullOrWhiteSpace(postbackModel.BookingContactPerson.Phone2Prefix))
                {
                    ModelState.AddModelError("BookingContactPerson.Phone2", "Please select the Secondary Phone Number country code.");
                }

                // Preparation for Check Children & Infrant DOB
                //if(model.SearchFlightResultViewModel.Childrens > 0 || model.SearchFlightResultViewModel.Infants > 0)
                //{

                //}

                //Twin - 2016/12/07 - remove validation on MemberRegistration
                if (!postbackModel.isRegister)
                {
                    var errorList = ModelState.Where(x => x.Value.Errors.Count > 0 && x.Key.Contains("MemberRegisterModels"));
                    foreach (var item in errorList)
                    {
                        ModelState[item.Key].Errors.Clear();
                    }
                }
                else
                {
                    var errorList2 = ModelState.Where(x => x.Value.Errors.Count > 0 && x.Key.Contains("MemberRegisterModels") && !x.Key.Contains("Password"));
                    foreach (var item in errorList2)
                    {
                        ModelState[item.Key].Errors.Clear();
                    }

                    using (MayFlower db = new MayFlower())
                    {
                        bool isEmailAvailable = db.Users.Any(x => x.Email != postbackModel.BookingContactPerson.Email);
                        if (!isEmailAvailable)
                        {
                            ModelState.AddModelError("MemberRegisterModels.Email", "Oops, look like your email had registered.");
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    string contactEmail = postbackModel.BookingContactPerson.Email.ToLower();

                    //Copy phone number and email pass to sabre
                    //sessionModel.BookingContactPerson.Email = contactEmail;
                    sessionModel.BookingContactPerson = postbackModel.BookingContactPerson;
                    sessionModel.PassengerDetailBooking[0].PassengerEmail = contactEmail;
                    sessionModel.PassengerDetailBooking[0].Phone1 = string.IsNullOrEmpty(postbackModel.BookingContactPerson.Phone1) ? string.Empty : postbackModel.BookingContactPerson.Phone1PrefixNo + postbackModel.BookingContactPerson.Phone1.Replace("+", "");
                    sessionModel.PassengerDetailBooking[0].Phone2 = string.IsNullOrEmpty(postbackModel.BookingContactPerson.Phone2) ? string.Empty : postbackModel.BookingContactPerson.Phone2PrefixNo + postbackModel.BookingContactPerson.Phone2.Replace("+", "");
                    sessionModel.PassengerDetailBooking[0].Phone1UseType = string.IsNullOrEmpty(postbackModel.BookingContactPerson.Phone1UseType) ? "M" : postbackModel.BookingContactPerson.Phone1UseType;
                    sessionModel.PassengerDetailBooking[0].Phone2UseType = string.IsNullOrEmpty(postbackModel.BookingContactPerson.Phone2UseType) ? "M" : postbackModel.BookingContactPerson.Phone2UseType;
                    // 2016/12/27 - Move to use model retrieve PhoneLocationCode
                    //model.PassengerDetailBooking[0].Phone1LocationCode = "KUL";

                    string sessionNameHotel = Enumeration.SessionName.HotelCheckOut + tripid;
                    if (Session[sessionNameHotel] != null)
                    {
                        var hotelCheckoutModel = (HotelCheckoutModel)Session[sessionNameHotel];
                        hotelCheckoutModel.ReserveRoomModel.contactDetail = new ContactDetailModel
                        {
                            Email = contactEmail,
                            Title = sessionModel.BookingContactPerson.Title,
                            DateOfMonth = sessionModel.BookingContactPerson.DOB.ToDateOfBirthModel(),
                            FirstName = sessionModel.BookingContactPerson.GivenName,
                            FamilyName = sessionModel.BookingContactPerson.Surname,
                            PrimaryPhoneNo = sessionModel.BookingContactPerson.Phone1,
                            PrimaryPhoneNoPrefix = sessionModel.BookingContactPerson.Phone1Prefix,
                            SecondaryPhoneNo = sessionModel.BookingContactPerson.Phone2,
                            SecondaryPhoneNoPrefix = sessionModel.BookingContactPerson.Phone2Prefix,
                            Address1 = sessionModel.BookingContactPerson.Address1,
                            Address2 = sessionModel.BookingContactPerson.Address2,
                            Address3 = null,
                            City = sessionModel.BookingContactPerson.City,
                            StateOfProvince = sessionModel.BookingContactPerson.State,
                            CountryCode = sessionModel.BookingContactPerson.Country,
                            PostalCode = sessionModel.BookingContactPerson.PostalCode,
                        };

                        foreach (var room in hotelCheckoutModel.ReserveRoomModel.GuestRooms)
                        {
                            room.Title = sessionModel.BookingContactPerson.Title;
                            room.FirstName = sessionModel.BookingContactPerson.GivenName;
                            room.FamilyName = sessionModel.BookingContactPerson.Surname;
                        }

                        Session[sessionNameHotel] = hotelCheckoutModel;
                    }

                    if (sessionModel.isRegister)
                    {
                        SqlCommand command = new SqlCommand();

                        sessionModel.MemberRegisterModels = populateMemberRegisterModel(sessionModel.BookingContactPerson, sessionModel.MemberRegisterModels);
                        Session["RegisteredUserId"] = MemberServiceController.InsertMember(sessionModel.MemberRegisterModels, command);

                        command.Transaction.Commit();

                        MemberController mc = new MemberController();
                        //mc.GenerateSimpleActivateMail(sessionModel.MemberRegisterModels.Email, true);
                        mc.GenerateActivateMail(sessionModel.MemberRegisterModels.FirstName, sessionModel.MemberRegisterModels.Email);
                    }

                    ////Bind SSR price here
                    //if (sessionModel.FlightSearchResultViewModel.SelectedSource == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia
                    //    && sessionModel.PassengerDetailBooking.Any(x => x.OutboundSSR.Any(a => !string.IsNullOrEmpty(a.Value) || x.InboundSSR.Any(b => !string.IsNullOrEmpty(b.Value)))))
                    //{
                    //    bool isReturn = sessionModel.SearchFlightResultViewModel.TripType == "Return";
                    //    sessionModel.BookingSummaryViewModel.FareSummary.SSRPriceList = new List<SSRModel>();
                    //    foreach (var psg in sessionModel.PassengerDetailBooking.Where(x => x.PassengerType != "INF"))
                    //    {
                    //        SSRModel ssrModel = new SSRModel();
                    //        AvailableBaggage baggageSelected = null;
                    //        for (int i = 0; i < (isReturn ? 2 : 1); i++)
                    //        {
                    //            bool isOutBound = i == 0;
                    //            foreach (var ssr in (isOutBound ? psg.OutboundSSR : psg.InboundSSR))
                    //            {
                    //                if (ssr.Key == "baggage")
                    //                {
                    //                    baggageSelected = sessionModel.FlightSearchResultViewModel.BaggageList.FirstOrDefault(x => x.Code == ssr.Value);
                    //                    if (baggageSelected != null)
                    //                    {
                    //                        sessionModel.BookingSummaryViewModel.FareSummary.SSRPriceList.Add(
                    //                            ssrModel = new SSRModel
                    //                            {
                    //                                Item = ssr.Key,
                    //                                NettPrice = baggageSelected.NettPrice.RoundToDecimalPlace(),
                    //                                Tax = baggageSelected.Tax.RoundToDecimalPlace(),
                    //                                AddtionalInfo = baggageSelected.AddtionalInfo,
                    //                                Label = UtilitiesService.GetSSRDesc(ssr.Key) + " " + baggageSelected.AddtionalInfo.FirstOrDefault(x => x.Key == "weight").Value + " " + baggageSelected.AddtionalInfo.FirstOrDefault(x => x.Key == "unit").Value
                    //                            });
                    //                    }
                    //                }
                    //            }
                    //        }
                    //    }

                    //    sessionModel.BookingSummaryViewModel.FareSummary.GST = sessionModel.BookingSummaryViewModel.FareSummary.SourceGST + sessionModel.BookingSummaryViewModel.FareSummary.SSRPriceList.Sum(x => x.Tax);
                    //    sessionModel.BookingSummaryViewModel.FareSummary.TotalPrice = sessionModel.BookingSummaryViewModel.FareSummary.SourceTotalPrice + sessionModel.BookingSummaryViewModel.FareSummary.SSRPriceList.Sum(x => x.TotalPrice);
                    //}

                    return RedirectToAction("Payment", "Booking", new { tripid });
                    //return RedirectToAction("ReviewBooking", "Booking", new { tripid });
                }

                // 2017/01/04 - Assign session to avoid become null after postback
                Session[sessionFlightBooking] = sessionModel;
                return View(sessionModel);
            }
        }

        [OutputCache(Location = OutputCacheLocation.Server, Duration = 0, VaryByParam = "none")]
        public ActionResult FlightSearchResultListDetailPopUp(string value)
        {
            FlightBookingModel model = (FlightBookingModel)Session[sessionFlightBooking];

            return View(model.FlightSearchResultViewModel.FlightSearchResult[Convert.ToInt32(value) - 1]);
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
                    return PartialView(model);
                }
            }
            return null;
            //return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }


        //Compare Tool - Change to Compare Tool
        //[HttpPost]
        //public JsonResult GetFullFlightSearchResultDetail()
        //{
        //    if (Session[sessionFullFlightSearchResult] != null)
        //    {
        //        var fullFlightResult = new List<PricedItineryModel>();
        //        var filteredModel = new FilterFlightModel();
        //        fullFlightResult = (List<PricedItineryModel>)Session[sessionFullFlightSearchResult];

        //        if (fullFlightResult == null)
        //        {
        //            Session.Remove(sessionFullFlightSearchResult);
        //            return Json(null);
        //        }

        //        var outBound = fullFlightResult
        //            .Select(x => x.OriginDestinationOptions.First().FlightSegments);

        //        // By Time Of Day
        //        string minOutDepartureTime = outBound.Min(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
        //        string maxOutDepartureTime = outBound.Max(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
        //        string minOutArrivalTime = outBound.Min(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();
        //        string maxOutArrivalTime = outBound.Max(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();

        //        // By Full Time
        //        //minOutDepartureTime = outBound.Min(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
        //        //maxOutDepartureTime = outBound.Max(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
        //        //minOutArrivalTime = outBound.Min(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();
        //        //maxOutArrivalTime = outBound.Max(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();

        //        string minInDepartureTime = null;
        //        string maxInDepartureTime = null;
        //        string minInArrivalTime = null;
        //        string maxInArrivalTime = null;

        //        var pricingList = fullFlightResult.Select(x => x.PricingInfo.TotalAfterTax);

        //        if (fullFlightResult.First().OriginDestinationOptions.Length > 1)
        //        {
        //            var inBound = fullFlightResult
        //            .Select(x => x.OriginDestinationOptions.Last().FlightSegments);

        //            minInDepartureTime = inBound.Min(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
        //            maxInDepartureTime = inBound.Max(seg => seg.First().DepartureDateTime.TimeOfDay.TotalMinutes).ToString();
        //            minInArrivalTime = inBound.Min(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();
        //            maxInArrivalTime = inBound.Max(seg => seg.Last().ArrivalDateTime.TimeOfDay.TotalMinutes).ToString();

        //            //maxInDepartureTime = inBound.Min(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
        //            //maxInDepartureTime = inBound.Max(seg => seg.First().DepartureDateTime).TimeOfDay.TotalMinutes.ToString();
        //            //minInArrivalTime = inBound.Min(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();
        //            //maxInArrivalTime = inBound.Max(seg => seg.Last().ArrivalDateTime).TimeOfDay.TotalMinutes.ToString();
        //        }

        //        MHFilterFlightModel filterModel = new MHFilterFlightModel
        //        {
        //            OutboundDepartureTimeMin = minOutDepartureTime,
        //            OutboundDepartureTimeMax = maxOutDepartureTime,
        //            OutboundArrivalTimeMin = minOutArrivalTime,
        //            OutboundArrivalTimeMax = maxOutArrivalTime,
        //            InboundDepartureTimeMin = minInDepartureTime,
        //            InboundDepartureTimeMax = maxInDepartureTime,
        //            InboundArrivalTimeMin = minInArrivalTime,
        //            InboundArrivalTimeMax = maxInArrivalTime,
        //            PriceMin = pricingList.Min().ToString(),
        //            PriceMax = pricingList.Max().ToString(),
        //            //Airline = [""],
        //        };

        //        // 20161209 - Reassign for js refresh usage
        //        bool isNoFilter = Session[sessionFilterParam] == null;

        //        filteredModel = (FilterFlightModel)Session[sessionFilterParam];

        //        filterModel.OutboundDepartureTimeMinSelected = isNoFilter ? filterModel.OutboundDepartureTimeMin : filteredModel.OutDepartureTimeMin;
        //        filterModel.OutboundDepartureTimeMaxSelected = isNoFilter ? filterModel.OutboundDepartureTimeMax : filteredModel.OutDepartureTimeMax;
        //        filterModel.OutboundArrivalTimeMinSelected = isNoFilter ? filterModel.OutboundArrivalTimeMin : filteredModel.OutArrivalTimeMin;
        //        filterModel.OutboundArrivalTimeMaxSelected = isNoFilter ? filterModel.OutboundArrivalTimeMax : filteredModel.OutArrivalTimeMax;

        //        filterModel.InboundDepartureTimeMinSelected = isNoFilter ? filterModel.InboundDepartureTimeMin : filteredModel.InDepartureTimeMin;
        //        filterModel.InboundDepartureTimeMaxSelected = isNoFilter ? filterModel.InboundDepartureTimeMax : filteredModel.InDepartureTimeMax;
        //        filterModel.InboundArrivalTimeMinSelected = isNoFilter ? filterModel.InboundArrivalTimeMin : filteredModel.InArrivalTimeMin;
        //        filterModel.InboundArrivalTimeMaxSelected = isNoFilter ? filterModel.InboundArrivalTimeMax : filteredModel.InArrivalTimeMax;

        //        filterModel.PriceMinSelected = isNoFilter ? filterModel.PriceMin : filteredModel.PriceMin;
        //        filterModel.PriceMaxSelected = isNoFilter ? filterModel.PriceMax : filteredModel.PriceMax;

        //        Session[sessionMHFilterFlightModel] = filterModel;

        //        return Json(filterModel);
        //    }
        //    return Json(new object());
        //}
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

        public ActionResult FlightResultOption()
        {
            //string sessionNameBooking = "FlightBooking"; // + tripid;

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

        public ActionResult IsFlightPairable(string outbound, string inbound, string grpTag, string tripid)
        {
            //string sessionNameBooking = "FlightBooking"; // + tripid;

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

        #region 2017/03/20 - Cross-Sell
        private IEnumerable<CrossSaleRule> CheckIsCrossSalesHotelAvailaible(FlightSelectedViewModel flightSelected, MayFlower dbContext = null)
        {
            dbContext = dbContext ?? new MayFlower();
            var result = dbContext.CrossSaleRules
                .Where(x => x.IsActive && DateTime.Now >= x.BookingDateFrom && DateTime.Now <= x.BookingDateTo
                        && x.IsActive && flightSelected.DepartureDate >= x.TravelDateFrom && flightSelected.ReturnDate <= x.TravelDateTo
                        && x.Destination == flightSelected.DestinationStationCode)
                .AsEnumerable();

            bool anyAirlineOK = result.Any(x => x.AirlineCode != "-");
            if ((result.Where(x => x.AirlineCode == flightSelected.AirlineInclude.First()).Count() == 0) || flightSelected.AirlineInclude.Count > 1)
            {
                result = result.Where(x => x.AirlineCode == "-");
            }
            else if (anyAirlineOK && flightSelected.AirlineInclude.Count == 1)
            {
                // For specified airline
                result = result.Where(x => x.AirlineCode == flightSelected.AirlineInclude.First());
            }



            return result.Count() > 0 ? result : null;
        }
        #endregion

        [HttpPost]
        public ActionResult CheckSearchCriteria([Bind(Exclude = "SearchResults")]SearchFlightResultViewModel searchModel)
        {
            //string sessionNameBooking = "FlightBooking"; // + tripid;

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
            //string sessionNameBooking = "FlightBooking"; // + tripid;

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
                string arriveTime = model.SearchFlightResultViewModel.EndDate.Value.AddDays(1).ToString("ddd MMM dd yyyy HH:mm:ss") + " GMT+0800 (Malay Peninsula Standard Time)";
                string scripts = @"var ServerDateTime = ";
                scripts += "";
                scripts += "new Date('" + dateTime + "')";
                scripts += "";
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

        #region Utilities
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
        #endregion

        private string GetNumberFromString(string text)
        {
            return new string(text.Where(char.IsDigit).ToArray());
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

        private bool IsAgentUser
        {
            get
            {
                Alphareds.Module.Model.Database.User user = null;

                if (User.Identity.IsAuthenticated)
                {
                    user = Alphareds.Module.Common.Core.GetUserInfo(User.Identity.Name);
                }

                return user == null ? false : user.UserTypeCode == "AGT";
            }
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

        private MemberRegisterModels populateMemberRegisterModel(BookingContactPerson contactPerson, MemberRegisterModels member)
        {
            string PasswordSalt = Core.GeneratePasswordSalt();

            return new MemberRegisterModels
            {
                Password = member.Password,
                ConfirmPassword = member.ConfirmPassword,
                TitleCode = contactPerson.Title,
                FirstName = contactPerson.GivenName,
                LastName = contactPerson.Surname,
                Email = contactPerson.Email,
                DOB = contactPerson.DOB.HasValue ? contactPerson.DOB.Value : DateTime.MinValue,
                PrimaryPhone = contactPerson.Phone1,
                SecondaryPhone = contactPerson.Phone2,
                Address1 = contactPerson.Address1,
                Address2 = contactPerson.Address2,
                City = contactPerson.City,
                Postcode = contactPerson.PostalCode,
                AddressProvinceState = contactPerson.State,
                CountryCode = contactPerson.Country,
                IsActive = true,
                CreatedByID = CurrentUserID,
                ModifiedByID = CurrentUserID
            };
        }
    }
}
