using Alphareds.Module.Common;
using Alphareds.Module.CommonController;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Alphareds.Module.HotelController;
using Alphareds.Module.MemberController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.PaymentController;
using Alphareds.Module.ServiceCall;
using Newtonsoft.Json;
using NLog;
using Mayflower.Filters;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using PagedList;
using System.Text.RegularExpressions;
using AutoMapper;
using System.Linq.Expressions;
using WebGrease.Css.Extensions;
using System.Web.Script.Serialization;
using Alphareds.Module.Cryptography;
using System.Web;
using System.Dynamic;

namespace Mayflower.Controllers
{
    [Filters.PreserveQueryStringFilter(QueryString = "tripid")]
    public class HotelController : AsyncController
    {
        private string tripid { get; set; }
        private Logger Logger { get; set; }
        private Alphareds.Module.Event.Function.DB EventDBFunc { get; set; }
        private string DumpListCacheKey { get; set; } = "HotelListCache";
        private SearchHotelModel _SearchHotelModel { get; set; }
        private Mayflower.General.CustomPrincipal CustomPrincipal => User as Mayflower.General.CustomPrincipal;

        private string _ExecutedController { get; set; }
        private string _ExecutedAction { get; set; }
        private bool IsUseV2Layout { get; set; } = false;

        private void InitBaseProperty()
        {
            Logger = LogManager.GetCurrentClassLogger();
            EventDBFunc = new Alphareds.Module.Event.Function.DB(Logger);

            var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
            var routeValue = request.RequestContext.RouteData.Values["tripid"];
            string routeString = routeValue?.ToString();
            tripid = System.Web.HttpContext.Current.Request.QueryString["tripid"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["tripid"]);

            _ExecutedController = ControllerContext?.RouteData?.Values["controller"]?.ToString().ToLower();
            _ExecutedAction = ControllerContext?.RouteData?.Values["action"]?.ToString().ToLower();

            var req = Request ?? System.Web.HttpContext.Current.Request.RequestContext.HttpContext.Request;
            IsUseV2Layout = Core.Setting.Layout.IsUseV2Layout || (Core.IsForStaging && req?.Cookies["version"]?.Value == "v2");

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

            var _session = Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);
            _SearchHotelModel = _session != null ? (SearchHotelModel)_session : new SearchHotelModel
            {

            };
        }

        public HotelController()
        {
            InitBaseProperty();
        }

        // Hijack controller context from another controller for User principal usage.
        public HotelController(ControllerContext controllerContext)
        {
            this.ControllerContext = controllerContext;
            InitBaseProperty();
        }

        #region Step 2 - Search
        public ActionResult SetSearchInfo()
        {
            var m = _SearchHotelModel;

            string _srObj = SerializeHotelSearchToken(m.ArrivalDate, m.DepartureDate,
                m.NoOfRoom, m.NoOfAdult, m.NoOfInfant, m.IsCrossSell, (User.Identity.IsAuthenticated ? CustomPrincipal.IsAgent : false),
                m.RType, m.CurrencyCode);

            return Content(_srObj, "json");
        }

        public static string SerializeHotelSearchToken(DateTime dtFrom, DateTime dtTo,
            int rooms, int adults, int infants, bool isCrossSell, bool isB2B,
            RateType rateType = RateType.MerchantStandard, string currencyCode = "MYR")
        {
            IDictionary<string, object> _dynamicObj = new ExpandoObject() as IDictionary<string, Object>;

            _dynamicObj.Add("CheckIn", dtFrom.ToString("yyyyMMdd"));
            _dynamicObj.Add("CheckOut", dtTo.ToString("yyyyMMdd"));
            _dynamicObj.Add("Curr", currencyCode);

            int roomCount = rooms;
            int adultCount = adults;
            int childCount = infants;

            List<int> _adultIncluded = new List<int>();
            List<int> _infantIncluded = new List<int>();

            List<int> adultAssignToRoom = ESBHotelServiceCall.CustomerTypeRoomAssign(adultCount, roomCount).ReplaceZeroWithOne();
            List<int> childAssignToRoom = ESBHotelServiceCall.CustomerTypeRoomAssign(childCount, roomCount);
            childAssignToRoom.Reverse();

            _dynamicObj.Add("Adult", string.Join("|", adultAssignToRoom));
            _dynamicObj.Add("Child", string.Join("|", childAssignToRoom));

            Search.Hotel.SearchInfo searchInfo = new Search.Hotel.SearchInfo
            {
                RateType = rateType,
                IsB2B = isB2B,
                IsCrossSell = isCrossSell,
            };

            _dynamicObj.Add("token", Cryptography.AES.Encrypt(JsonConvert.SerializeObject(searchInfo)));

            string _srObj = JsonConvert.SerializeObject(_dynamicObj);
            return _srObj;
        }

        public ActionResult GetHotelSearch(SearchHotelModel model, string rType, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier supplier = null)
        {
            string tripid = Guid.NewGuid().ToString();
            model.CustomerUserAgent = Request.UserAgent;
            model.CustomerIpAddress = GetUserIP();
            model.CustomerSessionId = tripid;
            model.CurrencyCode = "MYR";
            model.ArrivalDate = model.ArrivalDate.Date;
            model.DepartureDate = model.DepartureDate.Date;

            model.SupplierIncluded = CheckIsEmptySupplier(supplier) ?? GetDefaultSearchSupplier(model.SupplierIncluded, CustomPrincipal.IsAgent);

            if (Request.Form["DestinationCMS"] != null)
            {
                model.Destination = Request.Form["AdvancedSearchKeywordsCMS"] == null ? Request.Form["DestinationCMS"].ToString() : Request.Form["AdvancedSearchKeywordsCMS"].ToString();
                model.AdvancedSearchKeywords = Request.Form["AdvancedSearchKeywordsCMS"] == null ? null : Request.Form["AdvancedSearchKeywordsCMS"].ToString();
                model.ArrivalDate = DateTime.ParseExact(Request.Form["ArrivalDateCMS"].ToString(), "dd-MMM-yyyy h:mm:ss tt", null);
                model.DepartureDate = DateTime.ParseExact(Request.Form["DepartureDateCMS"].ToString(), "dd-MMM-yyyy h:mm:ss tt", null);
                model.NoOfRoom = Convert.ToInt32(Request.Form["NoOfRoomCMS"].ToString());
                model.NoOfAdult = Convert.ToInt32(Request.Form["NoOfAdultCMS"].ToString());
                model.NoOfInfant = Convert.ToInt32(Request.Form["NoOfInfantCMS"].ToString());
                model.Star = Convert.ToInt32(Request.Form["StarCMS"].ToString());
                model.Bundle = Request.Form["BundleCMS"] == null ? null : Request.Form["BundleCMS"].ToString();
                model.PromoCode = Request.Form["HotelPromoCodeCMS"] == null ? null : Request.Form["HotelPromoCodeCMS"].ToString();
            }
            else
            {
                model.NoOfRoom = model.NoOfRoom;
                model.Destination = model.AdvancedSearchKeywords ?? model.Destination;
            }

            AddHotelSearchCookie(model);

            if (IsUseV2Layout)
            {
                Response.Cookies.Add(new HttpCookie("version", "v2"));
            }

            if (IsUseV2Layout)
            {

            }
            else
            {
                GetCache(model);
            }

            #region 2017/02/15 - David added portion, store in session for invoking GTM.
            TimeSpan timeDiff = model.DepartureDate - model.ArrivalDate;
            string GTM_value = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                                                CurrentUserID, //0,
                                                model.Destination,  //1
                                                model.ArrivalDate.ToString("dd-MMM-yyyy"),  //2
                                                model.DepartureDate.ToString("dd-MMM-yyyy"),  //3
                                                timeDiff.TotalDays.ToString(),   //4
                                                (model.NoOfAdult + model.NoOfInfant),   //5
                                                model.NoOfRoom, //6
                                                (model.Star > 5 ? "5" : model.Star.ToString()) //7
                                            );
            Core.SetSession(Enumeration.SessionName.GTM_trackHotelSearchCriteria, tripid, GTM_value);
            #endregion

            Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, model);
            Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, null);
            Core.SetSession(Enumeration.SessionName.UpdateFilterHotelResult, tripid, null);

            //return RedirectToAction("Search", "AHotel", new { tripid, area = "agent" });

            return RedirectToAction("Search", "Hotel", new { tripid });
        }

        [HttpPost]
        public ActionResult GetDPHotelSearch(SearchHotelModel Model, FormCollection collection, string tripid)
        {
            if (Core.GetSession(Enumeration.SessionName.SearchRequest, tripid) != null)
            {
                Model = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);
                //Model.IsAddOnCrossSell = CustomPrincipal.IsAgent; // TODO: 2018/10/31 - Check Fixed Price need to get package rate or not
                //Model.AgentCrossSale = CustomPrincipal.IsAgent;
            }
            if (!string.IsNullOrEmpty(collection["DPDestination"]) || !string.IsNullOrEmpty(collection["Destination"]))
            {
                Model.Destination = !string.IsNullOrEmpty(collection["DPDestination"]) ? collection["DPDestination"].ToString() : collection["Destination"].ToString();
                Model.Result = null;
                Model.IPagedHotelList = null;
            }

            Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, Model);
            Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, null);
            Core.SetSession(Enumeration.SessionName.UpdateFilterHotelResult, tripid, null);
            return RedirectToAction("Search", "Hotel", new { tripid });
        }

        public ActionResult GetFixedHotelSearch(SearchFlightResultViewModel ModifySearchModel, string Destination, FormCollection collection, string tripid)
        {
            SearchHotelModel SearchHotel = new SearchHotelModel();
            FlightBookingModel model = new FlightBookingModel();
            string sessionFlightBooking = Enumeration.SessionName.FlightBooking + tripid;
            if (!string.IsNullOrEmpty(ModifySearchModel.ArrivalStation) && !string.IsNullOrEmpty(ModifySearchModel.DepartureStation))
            {
                model.SearchFlightResultViewModel = ModifySearchModel;
                Core.SetSession(Enumeration.SessionName.FlightBooking, tripid, model);
            }

            if (Session[sessionFlightBooking] == null)
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }
            model = (FlightBookingModel)Session[sessionFlightBooking];

            SearchHotel.CustomerUserAgent = Request.UserAgent;
            SearchHotel.CustomerIpAddress = General.Utilities.GetClientIP;
            SearchHotel.CustomerSessionId = tripid;
            SearchHotel.CurrencyCode = "MYR";
            SearchHotel.ArrivalDate = model.SearchFlightResultViewModel.BeginDate ?? DateTime.Now;
            SearchHotel.DepartureDate = model.SearchFlightResultViewModel.EndDate ?? DateTime.Now;
            SearchHotel.IsCrossSell = true; // TODO: 2018/10/31 - Check Fixed Price need to get package rate or not
            SearchHotel.IsB2B = CustomPrincipal.IsAgent;

            SearchHotel.IsDynamic = true;
            SearchHotel.DynamicStationCode = model.SearchFlightResultViewModel.ArrivalStationCode;
            FlightController fc = new FlightController(this.ControllerContext);
            if (string.IsNullOrEmpty(Destination))
            {
                var stationcityList = SearchHotel.DynamicStationCode != null ? fc.StationCitySelect(SearchHotel.DynamicStationCode).ToList() : null;
                var stationcity = stationcityList != null ? stationcityList.Where(x => x.IsDefault).FirstOrDefault()?.City : null;
                SearchHotel.Destination = stationcity ?? model.SearchFlightResultViewModel.ArrivalStation.Split('-')[1];
            }
            else
            {
                SearchHotel.Destination = Destination;
            }
            SearchHotel.NoOfRoom = model.SearchFlightResultViewModel.NoOfRoom;
            SearchHotel.NoOfAdult = model.SearchFlightResultViewModel.Adults;
            SearchHotel.NoOfInfant = model.SearchFlightResultViewModel.Childrens + model.SearchFlightResultViewModel.Infants;
            SearchHotel.PromoCode = model.SearchFlightResultViewModel.DPPromoCode;
            SearchHotel.IsFixedPrice = true;

            Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, SearchHotel);
            Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, null);
            Core.SetSession(Enumeration.SessionName.UpdateFilterHotelResult, tripid, null);
            Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, null);
            return RedirectToAction("SearchHotel", "Hotel", new { tripid });
        }

        public ActionResult SearchHotel(SearchHotelModel model, string rType, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier supplier = null)
        {
            ViewBag.isFixed = true;
            if (Core.GetSession(Enumeration.SessionName.SearchRequest, tripid) == null)
            {
                ViewBag.sessionExp = true;
            }
            return Search(null, null);
        }


        public ActionResult Search(SearchHotelModel model, string rType, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier supplier = null)
        {
            #region Dropdown list in Modify Search
            List<SelectListItem> numberOfAdults = new List<SelectListItem>();
            for (int i = 1; i <= 32; i++)
            {
                numberOfAdults.Add(new SelectListItem
                {
                    Text = string.Format("{0} Adult{1}", i, (i > 1 ? "s" : "")),
                    Value = i.ToString(),
                    Selected = (i.Equals(2) ? true : false)
                });
            }
            ViewData.Add("ADULT", numberOfAdults);

            List<SelectListItem> numberOfInfants = new List<SelectListItem>();
            for (int i = 0; i <= 16; i++)
            {
                numberOfInfants.Add(new SelectListItem
                {
                    Text = string.Format("{0} Child{1}", i, (i > 1 ? "ren" : "")),
                    Value = i.ToString(),
                    Selected = (i.Equals(0) ? true : false)
                });
            }
            ViewData.Add("CHILD", numberOfInfants);

            List<SelectListItem> numberOfRooms = new List<SelectListItem>();
            for (int i = 1; i <= 8; i++)
            {
                numberOfRooms.Add(new SelectListItem
                {
                    Text = string.Format("{0} Room{1}", i, (i > 1 ? "s" : "")),
                    Value = i.ToString(),
                    Selected = (i.Equals(1) ? true : false)
                });
            }
            ViewData.Add("ROOM", numberOfRooms);
            #endregion

            /* 
             * 2017/06/10 - If not clear the model state item, will cause destination step 2 display "Destination Required" issues.
            */
            ModelState.Clear();

            model = model ?? new SearchHotelModel();
            FilterHotelResultModel filterResult = new FilterHotelResultModel();

            if (Core.GetSession(Enumeration.SessionName.SearchRequest, tripid) != null)
            {
                model = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);
            }
            else if (model.DepartureDate != DateTime.MinValue && model.ArrivalDate != DateTime.MinValue && Core.GetSession(Enumeration.SessionName.SearchRequest, tripid) == null)
            {
                model.CustomerUserAgent = Request.UserAgent;
                model.CustomerIpAddress = GetUserIP();
                model.CustomerSessionId = tripid;
                model.CurrencyCode = "MYR";

                Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, model);
            }
            else
            {
                // GO
                model.ArrivalDate = DateTime.Now.AddDays(double.Parse(Core.GetSettingValue("dayadvance")));
                // RETURN
                model.DepartureDate = model.ArrivalDate.AddDays(1);
                model.IsFixedPrice = ViewBag.isFixed ?? false;
                ViewBag.sessionExp = ViewBag.sessionExp;
            }

            // Check PagedModel is null or not, if null then create paged object
            if (model?.Result?.HotelList?.Length > 0 && model?.IPagedHotelList == null)
            {
                int pageSize = 10;
                int.TryParse(Core.GetAppSettingValueEnhanced("RecordsPerPage"), out pageSize);

                CalculateHotelListRoomRate(model, model.Result.HotelList);

                model.IPagedHotelList = model.Result.HotelList.ToPagedList(1, pageSize);
            }

            if (Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid) != null)
            {
                filterResult = (FilterHotelResultModel)Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid);
            }

            #region Show selected STAR rating
            switch (model.Star)
            {
                case 1: ViewData.Add("STAR", "1 Star"); break;
                case 2: ViewData.Add("STAR", "2 Stars"); break;
                case 3: ViewData.Add("STAR", "3 Stars"); break;
                case 4: ViewData.Add("STAR", "4 Stars"); break;
                case 5: ViewData.Add("STAR", "5 Stars"); break;
                default: ViewData.Add("STAR", "All Stars"); model.Star = 10; break;
            }
            #endregion

            ViewData.Add("VALUEADDS", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_ValueAdds.xml"));
            ViewData.Add("PAXES", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_Paxes.xml"));
            ViewData.Add("PARTNERID", System.Configuration.ConfigurationManager.AppSettings.Get("PartnerID"));
            ViewData.Add("ARRIVALDATE", model.ArrivalDate.ToString("dd-MMM-yyyy"));
            ViewData.Add("DEPARTDATE", model.DepartureDate.ToString("dd-MMM-yyyy"));

            #region find total nights selected
            TimeSpan timeDiff = model.DepartureDate.Date - model.ArrivalDate.Date;
            model.totalDays = timeDiff.TotalDays.ToString();
            ViewData.Add("TOTALNIGHTS", timeDiff.TotalDays.ToString());
            #endregion

            #region If error from Reserve, show message.
            if (Core.GetSession(Enumeration.SessionName.ErrorMessage) != null)
            {
                ViewData.Add("ERRMSG", Core.GetSession(Enumeration.SessionName.ErrorMessage).ToString());
                Core.SetSession(Enumeration.SessionName.ErrorMessage, null);
            }
            #endregion

            if (model.BundleExist())
            {
                HotelBundleModel bundleInfo = null;
                GetBundleHotelId(model.Bundle, model.ArrivalDate, model.DepartureDate, out bundleInfo);
                model.BundleInfo = bundleInfo;
            }
            MayFlower _db = null;
            CheckoutProduct checkoutmodel = new CheckoutProduct();
            if (Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid) != null)
            {
                checkoutmodel = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
                if (!string.IsNullOrEmpty(checkoutmodel.DPPromoCode))
                {
                    //display promo code applied in hotel list for package
                    _db = _db ?? new MayFlower();
                    var _rule = GetPromoCodeDiscountRule(model, _db, checkoutmodel.DPPromoCode);
                    model.PromoCode = _rule != null && _rule.IsPackageOnly ? checkoutmodel.DPPromoCode : model.PromoCode;
                }
            }
            if (checkoutmodel.Hotel == null)
            {
                checkoutmodel.InsertProduct(new ProductHotel
                {
                    SearchHotelInfo = model,
                });
                checkoutmodel.IsFixedPrice = model.IsFixedPrice;
            }

            // Override Search Pax Dropdown
;
            if (model.IsPromoCodeUsed)
            {
                _db = _db ?? new MayFlower();
                var _rule = GetPromoCodeDiscountRule(model, _db);

                if (_rule != null)
                {
                    checkoutmodel.PromoID = _rule.PromoID;
                    PromoCodeFunctions promoCodeFunctions = checkoutmodel.PromoCodeFunctions;

                    if ((_rule.IsPackageOnly || promoCodeFunctions.GetFrontendFunction.PackageAutoAppliedHotel) && !checkoutmodel.IsDynamic)
                    {
                        model.PromoCode = null;
                        model.PromoId = 0;
                        checkoutmodel.PromoID = 0;
                    }

                    // Overrided not comming from Affiliate Program
                    if (promoCodeFunctions.GetFrontendFunction.WaiveCreditCardFee)
                    {
                        string _reffCode = model.AffiliateID?.ToLower();
                        var _validAffiliate = _db.Affiliations.Any(x => x.UserCode == _reffCode);

                        if (!_validAffiliate)
                        {
                            model.PromoCode = null;
                            model.PromoId = 0;
                            checkoutmodel.PromoID = 0;
                        }
                    }

                    if (promoCodeFunctions.GetFrontendFunction.LimitSearchPax)
                    {
                        numberOfAdults.RemoveAll(x => x.Value.ToInt() > 2);
                        numberOfInfants.RemoveAll(x => x.Value.ToInt() > 0);
                    }
                }
            }
            if (model.IsFixedPrice)
            {
                string sessionFlightBooking = Enumeration.SessionName.FlightBooking + tripid;
                FlightBookingModel FlightModel = null;
                if (Session[sessionFlightBooking] != null)
                {
                    FlightModel = (FlightBookingModel)Session[sessionFlightBooking];
                    ViewBag.sessionExp = ViewBag.sessionExp ?? false;
                }
                else
                {
                    FlightModel = new FlightBookingModel()
                    {
                        SearchFlightResultViewModel = new SearchFlightResultViewModel()
                        {
                            BeginDate = DateTime.Now.AddDays(double.Parse(Core.GetSettingValue("dayadvance"))),
                            EndDate = model.ArrivalDate.AddDays(1),
                            CabinClass = "Y",
                        }
                    };
                    ViewBag.sessionExp = true;
                }

                if (checkoutmodel.Flight == null)
                {
                    checkoutmodel.InsertProduct(new ProductFlight
                    {
                        SearchFlightInfo = FlightModel.SearchFlightResultViewModel,
                    });
                }
            }
            if (IsUseV2Layout)
            {
                if (checkoutmodel.CheckoutStep < 2)
                {
                    checkoutmodel.CheckoutStep = 2;
                }
                return View("~/Views/Hotel/v2/Search.cshtml", checkoutmodel);
            }
            else
            {
                return View("~/Views/Hotel/Search.cshtml", checkoutmodel);
            }
        }

        [HttpPost]
        public async Task<ActionResult> GetHotelList(int? page, FormCollection collection, FilterHotelParamModel filterParamModel, string tripid, string rType = null,
            SearchSupplier supplier = null, string newsearch = null)
        {
            IEnumerable<PromoHotelList> featuredPromoHotelList = null;
            collection = collection ?? new FormCollection();
            SearchHotelModel model = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);
            model = model ?? new SearchHotelModel();
            model.CurrentViewPage = (page ?? model.CurrentViewPage);
            int pageNumber = model.CurrentViewPage;
            int pageSize = 10;
            int.TryParse(Core.GetAppSettingValueEnhanced("RecordsPerPage"), out pageSize);

            supplier = CheckIsEmptySupplier(supplier);

            MayFlower db = null;
            PromoCodeRule promoCodeRule = null;
            bool pCodeEnabledAndUsed = Core.IsEnablePayByPromoCode && model.IsPromoCodeUsed;

            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    // Pass in all value logged in LoginID into model.
                    model.UserLoginID = CustomPrincipal.LoginID;
                    model.IsB2B = CustomPrincipal.IsAgent;

                    var user = Alphareds.Module.Common.Core.GetUserInfo(CustomPrincipal.UserId.ToString(), db);
                    if (CustomPrincipal.IsAgent)
                    {
                        //model.Result = null; //2018 NOV 11, SET NULL WILL CAUSE HOTEL DISPLAY ISSUES, AND TRIGGER RESEARCH
                        model.IsCrossSell = (model.SupplierIncluded?.EANRapid ?? false) && true; // EPSRapid - Is agent then can display package rate.

                        var userTieringGroup = user.Organization.OrganizationInTieringGroups.Select(x =>
                        {
                            if (!model.IsAgentGroupDisplayAllSupplier && x.TieringGroup.IsDisplaySupplier)
                            {
                                model.IsAgentGroupDisplayAllSupplier = true;
                            }
                            return x.TieringGroupID;
                        });

                        List<int> tieringGroupID = userTieringGroup.Distinct().ToList();
                        model.SupplierIncluded = GetTieringGroupSupplier(tieringGroupID, model);
                    }

                    if (Core.IsEnablePayByPromoCode && !model.IsPromoCodeUsed && !model.IsDynamic)
                    {
                        db = db ?? new MayFlower();
                        var _userPromo = user.UserPromoes.LastOrDefault(x => x.IsActive)?.PromoCodeRule;

                        if (_userPromo != null)
                        {
                            model.PromoCode = _userPromo.PromoCode;
                            promoCodeRule = GetPromoCodeDiscountRule(model, db);
                            if (promoCodeRule == null)
                            {
                                model.PromoCode = string.Empty;
                                model.PromoId = 0;
                            }
                        }
                    }
                }

                if ((model.Destination != null && model.DepartureDate != DateTime.MinValue && model.ArrivalDate != DateTime.MinValue &&
                    model.NoOfRoom > 0) && page == null && (model.Result == null || newsearch == "1"))
                {
                    // async laod HotelList
                    model.SupplierIncluded = model.SupplierIncluded ?? supplier;

                    // hide not promo featured hotel
                    IEnumerable<string> promoSpecifiedHotel = new List<string>();
                    bool withPromoEvent = false;
                    bool soldOutEventTicket = false;
                    bool invalidEventDate = false;
                    List<string> pushEvErrMsg = new List<string>();

                    //hotel only search for auto applied promo rule 
                    if (Core.IsEnablePayByPromoCode && !model.IsPromoCodeUsed && !model.IsDynamic)
                    {
                        db = db ?? new MayFlower();
                        int Frontendfunc = (int)Alphareds.Module.Model.FrontendFunction.Enum.FrontendFunction.HotelAutoApplied;
                        promoCodeRule = GetPromoCodeFunctionRule(Frontendfunc, model, db);
                        model.PromoCode = promoCodeRule != null ? promoCodeRule.PromoCode : null;
                    }
                    else if (model.IsFixedPrice)
                    {
                        model.PromoCode = model.PromoCode ?? "BUNDLE";
                    }
                    pCodeEnabledAndUsed = Core.IsEnablePayByPromoCode && model.IsPromoCodeUsed;
                    if (pCodeEnabledAndUsed)
                    {
                        db = db ?? new MayFlower();
                        promoCodeRule = promoCodeRule ?? (pCodeEnabledAndUsed ? GetPromoCodeDiscountRule(model, db) : null);

                        featuredPromoHotelList = promoCodeRule?.PromoHotelDestinations.SelectMany(x => x.PromoHotelLists);

                        if (featuredPromoHotelList != null)
                        {
                            db = db ?? new MayFlower();
                            model.PromoId = promoCodeRule.PromoID;
                            var promoCodeFunctions = new PromoCodeFunctions(model.PromoId, db);
                            bool overLimitPax = promoCodeFunctions.GetFrontendFunction.LimitSearchPax
                                && (model.NoOfAdult > 2 || model.NoOfInfant > 0);

                            withPromoEvent = promoCodeFunctions.GetFrontendFunction.DisplayPromoEvent;
                            bool withinPromoEventDateRange = true;
                            if (withPromoEvent)
                            {
                                var _eventProduct = EventDBFunc.GetEventProductList(model.ArrivalDate, model.DepartureDate, "Hotel", null,
                                    model.Destination, (model.NoOfAdult + model.NoOfInfant), withPromoEvent, null, db);

                                var promoEvList = _eventProduct.HeaderInfo.Where(x => x.IsPromoEvent);

                                foreach (var item in promoEvList)
                                {
                                    bool isNotInDate = _eventProduct.DetailsInfo.Any(x =>
                                    {
                                        bool validId = x.EventID == item.EventID;
                                        bool validDate = model.ArrivalDate <= x.EventDate && model.DepartureDate >= x.EventDate;
                                        bool validInv = x.TicketBalance > 0;
                                        bool __valid = validId && validDate && validInv;

                                        if (validId && !validDate)
                                        {
                                            invalidEventDate = true;
                                            pushEvErrMsg.Add(string.Format("Check out date must be on or later than {0}.", x.EventDate.ToString("dd-MMM-yyyy")));
                                        }
                                        else if (validId && validDate && !validDate)
                                        {
                                            soldOutEventTicket = true;
                                        }

                                        return __valid;
                                    });

                                    if (!isNotInDate)
                                    {
                                        withinPromoEventDateRange = false;
                                        break;
                                    }
                                }
                            }


                            if (!overLimitPax && promoCodeFunctions.GetFrontendFunction.FilterHotel && withinPromoEventDateRange)
                            {
                                promoSpecifiedHotel = featuredPromoHotelList.Select(x => x.HotelID);
                            }
                        }
                    }

                    List<Task> searchTask = new List<Task>();

                    if (model.IsFixedPrice)
                    {
                        string sessionFlightBooking = Enumeration.SessionName.FlightBooking + tripid;
                        if (Session[sessionFlightBooking] == null)
                        {
                            return RedirectToAction("Type", "Error", new { id = "session-error" });
                        }
                        FlightBookingModel FlightModel = (FlightBookingModel)Session[sessionFlightBooking];
                        if (FlightModel.FlightSearchResultViewModel == null)
                        {
                            FlightController fc = new FlightController(this.ControllerContext);
                            searchTask.Add(fc.GetFlightFixedSearchAsync(FlightModel, User, IsAgentUser).ContinueWith((res) =>
                            {
                                FlightModel = res.Result;

                                if (FlightModel.SearchFlightResultViewModel.FixedPriceFrom > 0)
                                {
                                    model.IsCrossSell = true; // TODO: Fixed Price is get package rate.
                                    model.FixedPriceFrom = FlightModel.SearchFlightResultViewModel.FixedPriceFrom;
                                }
                                else
                                {
                                    throw new Exception("No flight result");
                                }
                            }));
                        }
                    }

                    if (withPromoEvent)
                    {
                        model.Result = await getHotelFromEBSSearchModel(model, promoSpecifiedHotel.ToList());

                        if (invalidEventDate)
                        {
                            ViewData["PromoErrMsg"] = "Sorry, package not available for your selected duration.";
                            ViewData["PromoErrDetail"] = pushEvErrMsg.Distinct();
                        }
                        else if (soldOutEventTicket)
                        {
                            ViewData["PromoErrMsg"] = "Sorry, package item has been sold out.";
                        }
                    }
                    else if (model.BundleType() == BundleTypes.TPConcert && model.BundleInfo != null)
                    {
                        model.Result = await getHotelFromEBSSearchModelTP(model, model.BundleInfo.HotelID);
                    }
                    else
                    {
                        var supplierReflect = typeof(SearchSupplier).GetProperties();
                        if (model.SearchProgress.Count == 0)
                        {
                            InsertSelectedSearchSupplier(model, supplierReflect, SearchProgress.Progress.New);
                        }

                        if (User.Identity.IsAuthenticated && CustomPrincipal.IsAgent)
                        {
                            ESBHotelServiceCall.Search b2BSearch = new ESBHotelServiceCall.Search(model);

                            searchTask.Add(b2BSearch.GetB2BHotelListAsync().ContinueWith(res =>
                            {
                                model.B2BResult = res.Result;
                                model.Result = model.B2BResult.ConvertToHotelListResponse();
                            }));
                        }
                        else
                        {
                            searchTask.Add(getHotelFromEBSSearchModel(model).ContinueWith(res =>
                            {
                                model.Result = res.Result;
                                model.B2BResult = null;
                            }));
                        }

                        InsertSelectedSearchSupplier(model, supplierReflect, SearchProgress.Progress.Complete);

                        model.SetSearchProgress(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.Suppliers.Expedia, SearchProgress.Progress.Complete);
                        SetCache(model);
                    }

                    // Handle async task for Flight+Hotel / Hotel only
                    await Task.WhenAll(searchTask);

                    if (model.Result?.Errors != null || model.B2BResult?.Errors != null)
                    {
                        var _jsonSetting = new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        };
                        Logger.Error(Environment.NewLine + "Error return from search ESB Hotel service." +
                            Environment.NewLine +
                            Environment.NewLine + JsonConvert.SerializeObject(model, Formatting.Indented, _jsonSetting));

                        ViewBag.SysErrMsg = "Sorry, unexpected error occur.";
                    }
                    else if (model.Result != null && (model.Result.HotelList == null || model.Result.HotelList.Length == 0) && model.Result.Errors == null)
                    {
                        ViewBag.SysErrMsg = "Sorry, no result for hotel.";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Debug(ex, "GetHotelList()");
            }

            // Move up for get filter price setting usage.
            if (model.IsFixedPrice || !string.IsNullOrEmpty(collection["hidAllNights"]) || !string.IsNullOrEmpty(collection["hidTax"]))
            {
                FilterHotelResultModel filterModel = null;
                model.DisplayHotelSetting = new DisplayHotelSetting();

                if (Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid) != null)
                {
                    filterModel = (FilterHotelResultModel)Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid);
                }
                else
                {
                    filterModel = new FilterHotelResultModel();
                }
                if (!string.IsNullOrEmpty(collection["hidTax"]) || model.IsFixedPrice)
                {
                    filterModel.IncludeTax = model.IsFixedPrice ? true : Convert.ToBoolean(collection["hidTax"]);
                    model.DisplayHotelSetting.AsIncludedTax = filterModel.IncludeTax;
                }
                if (!string.IsNullOrEmpty(collection["hidAllNights"]) || model.IsFixedPrice)
                {
                    filterModel.IncludeAllNights = model.IsFixedPrice ? true : Convert.ToBoolean(collection["hidAllNights"]);
                    model.DisplayHotelSetting.AsAllNight = filterModel.IncludeAllNights;
                }
                Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, filterModel);
            }


            if (model.Result != null && model.Result.HotelList != null && model.Result.HotelList.Length > 0)
            {
                // 20170708 - Mayday Bundle Hotfix
                if (model.BundleType() == BundleTypes.TPConcert)
                {
                    model.Result.HotelList = model.Result.HotelList.Where(x => x.hotelSupplier == HotelSupplier.Tourplan).ToArray();
                }

                db = db ?? new MayFlower();
                var hotelResult = model.Result.HotelList.ToList();
                //List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> hotelInformationToRemove = new List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation>();

                if (newsearch == "1")
                {
                    var featuredHotelList = db.HotelFeatureLists.Where(x => x.IsActive);
                    hotelResult.ForEach(x =>
                    {
                        x.lowRate = x.lowRate == 0 ? x.RoomRateDetailsList.First().RateInfos.First().chargeableRateInfo.averageRate.ToDecimal().RoundToDecimalPlace(2) : x.lowRate.RoundToDecimalPlace(2);
                        x.highRate = x.highRate.RoundToDecimalPlace(2);


                        /*if (x.RoomRateDetailsList == null || x.RoomRateDetailsList.Length == 0)
                        {
                            hotelInformationToRemove.Add(x);
                        }*/


                        if (featuredPromoHotelList != null)
                        {
                            foreach (var pcodeHotel in featuredPromoHotelList)
                            {
                                if (pcodeHotel.SupplierCode == x.hotelSupplierCode && pcodeHotel.HotelID == x.hotelId)
                                {
                                    x.FrontEnd_SortOrder = -1;
                                    break;
                                }
                            }
                        }

                        foreach (var hotelFeatured in featuredHotelList)
                        {
                            if (hotelFeatured.SupplierCode == x.hotelSupplierCode && hotelFeatured.HotelID == x.hotelId)
                            {
                                x.FrontEnd_SortOrder = hotelFeatured.SortOrder;
                                x.FrontEnd_StarBuyFlag = hotelFeatured.StarBuyFlag;
                                x.FrontEnd_MarketingMsg = hotelFeatured.MarketingMessage;
                                break;
                            }
                        }
                    });
                }

                // remove no room hotel
                /*foreach (var item in hotelInformationToRemove)
                {
                    hotelResult.Remove(item);
                }*/

                #region PromoCode Section
                if (pCodeEnabledAndUsed && (Core.GetSession(Enumeration.SessionName.HotelList, tripid) == null))
                {
                    promoCodeRule = GetPromoCodeDiscountRule(model, model.Result.HotelList, db);
                    if (promoCodeRule != null)
                    {
                        model.PromoId = promoCodeRule.PromoID; //below need passin the search date
                        HotelServiceController.ProcessDiscountCalculation(model.Result.HotelList, promoCodeRule, model.Destination, model.NoOfRoom, model);
                    }
                }
                #endregion

                CalculateHotelListRoomRate(model, hotelResult);

                model.Result.HotelList = SortingHoteResultList(hotelResult, collection, tripid, true).ToArray();

                #region 2017/02/15 - Session for sorting.
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

                string FilterName = "";
                if (filterParamModel == null || (filterParamModel != null && string.IsNullOrEmpty(filterParamModel.property) && string.IsNullOrEmpty(filterParamModel.hidRating)))
                { FilterName = "All Hotels"; }
                else
                {
                    if (!string.IsNullOrEmpty(filterParamModel.hidRating))
                    {
                        FilterName = (!string.IsNullOrEmpty(FilterName) ? ", Rating" : "Rating");
                    }
                    if (!string.IsNullOrEmpty(filterParamModel.property))
                    {
                        FilterName = (!string.IsNullOrEmpty(FilterName) ? ", Property Name" : "Property Name");
                    }
                }

                int positionCounter = 1;

                List<Alphareds.Module.Model.GTM_HotelProductListModel> GTMList = new List<GTM_HotelProductListModel>();
                foreach (Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation hotel in model.Result.HotelList)
                {
                    GTMList.Add(new GTM_HotelProductListModel
                    {
                        name = "Hotel - " + hotel?.Addresses?.city ?? "undefined",
                        id = hotel.hotelId,
                        price = hotel.lowRate.ToString("0.00"), // need format without comma
                        hotelName = System.Web.HttpUtility.HtmlEncode(hotel.name ?? "undefined"),
                        roomType = hotel.RoomRateDetailsList.FirstOrDefault()?.roomDescription ?? "undefined",
                        list = FilterName + " - " + SortingName,
                        position = (positionCounter++).ToString() // item counter instead of page no
                    });
                }
                #endregion

                FilterHotelResultModel filtResult = new FilterHotelResultModel();
                IPagedList<HotelInformation> IPagedModel = null;

                if (Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid) == null || model.IsFixedPrice || filterParamModel != null && (filterParamModel.hidRating != null || filterParamModel.minPrice != null ||
                    filterParamModel.maxPrice != null || filterParamModel.property != null))
                {
                    filtResult = UpdateFilter(model, collection, filterParamModel);
                    filtResult.Result = SortingHoteResultList(filtResult.Result, collection, tripid);
                    Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, filtResult);
                    IPagedModel = model.IsFixedPrice ? filtResult.Result.ToPagedList(pageNumber, pageSize) : filtResult.Result.ToPagedList(1, pageSize);  // every new filter also need reset page number to 1
                }
                else
                {
                    filtResult = (FilterHotelResultModel)Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid);
                    IPagedModel = filtResult.Result.ToPagedList(pageNumber, pageSize);
                }

                ViewData.Add("RESULT", filtResult.Result.Count > 0 ? filtResult.Result : null);

                #region Mayflower IPagedGroupFullHotelSearchReseult Result
                filtResult.IPagedHotelList = IPagedModel;
                model.IPagedHotelList = IPagedModel; //to set filtered result into model, when search different result, this will be used to test

                var gtm_filterd = GTMList.Where(x => IPagedModel.Any(y => y.hotelId == x.id)).OrderBy(x => IPagedModel.Any(y => y.hotelId == x.id));
                Core.SetSession(Enumeration.SessionName.GTM_trackHotelSearchResults, tripid, JsonConvert.SerializeObject(gtm_filterd));
                #endregion
            }

            Core.SetSession(Enumeration.SessionName.HotelList, tripid, model);
            Core.SetSession(Enumeration.SessionName.RoomAvail, tripid, null);

            if (IsUseV2Layout)
            {
                return Request.IsAjaxRequest() ? (ActionResult)PartialView("~/Views/Hotel/v2/_HotelList.cshtml", model) : RedirectToAction("Search");
            }
            else
            {
                ViewData.Add("VALUEADDS", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_ValueAdds.xml"));
                ViewData.Add("PAXES", System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Hotel_Paxes.xml"));
                ViewData.Add("PARTNERID", System.Configuration.ConfigurationManager.AppSettings.Get("PartnerID"));
                ViewData.Add("ARRIVALDATE", model.ArrivalDate.ToString("dd-MMM-yyyy"));
                ViewData.Add("DEPARTDATE", model.DepartureDate.ToString("dd-MMM-yyyy"));

                return Request.IsAjaxRequest() ? (ActionResult)PartialView("_HotelList", model) : RedirectToAction("Search");
            }
        }

        private void CalculateHotelListRoomRate(SearchHotelModel model, IEnumerable<HotelInformation> hotelResult)
        {
            foreach (var item in hotelResult)
            {
                bool isSingleRoomQuote = item.hotelSupplier != HotelSupplier.Expedia;
                foreach (var _rooms in item?.RoomRateDetailsList?.ToList() ?? new List<RoomRateDetails>())
                {
                    _rooms.RateInfos = HotelServiceController.CalcRatePerRoom(_rooms.RateInfos, model.TotalStayDays, model.NoOfRoom, isSingleRoomQuote).ToArray();
                }
            }
        }

        [HttpPost]
        public ActionResult GetHotelPanel(string tripid)
        {
            if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
            {
                SearchHotelModel model = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);

                if (model.Result != null && model.Result.HotelList != null)
                {
                    return PartialView("_HotelFilter", model);
                }
            }
            return null;
        }

        public ActionResult GetFilterPanelInfo()
        {
            decimal minRate = decimal.MaxValue;
            decimal maxRate = decimal.MinValue;
            int totalProperty = 0;

            #region Current Filter Value
            string filterStar = "1,2,3,4,5";
            string filterProperty = null;
            string filterLoc = null;
            decimal filterMin = minRate;
            decimal filterMax = maxRate;
            decimal fixedfilterMin = minRate;
            decimal fixedfilterMax = maxRate;
            decimal fixedminRate = minRate;
            decimal fixedmaxRate = maxRate;
            decimal fltPriceFrom = minRate;
            string sortType = null;
            string sortSq = null;
            bool isIncludeTax = false;
            bool isAllNight = false;
            #endregion

            totalProperty = _SearchHotelModel?.Result?.HotelList?.Length ?? 0;

            /*foreach (var item in _SearchHotelModel?.Result?.HotelList ?? new HotelInformation[] { })
            {
                if (item.lowRate < minRate)
                {
                    minRate = item.lowRate;
                }

                if (item.lowRate > maxRate)
                {
                    maxRate = item.lowRate;
                }

                totalProperty++;
            }*/

            if (totalProperty == 0)
            {
                minRate = 0;
                maxRate = 999999;
                fixedminRate = 0;
                fixedmaxRate = 999999;
                filterMin = minRate;
                filterMax = maxRate;
                fixedfilterMin = minRate;
                fixedfilterMax = maxRate;
            }

            // sorting
            string sortSeq = Core.GetSession(Enumeration.SessionName.Sorting, tripid)?.ToString();
            switch (sortSeq)
            {
                case "1": sortType = "TripAdvisor Rating"; break;
                case "2": sortType = "Best Deal"; break;
                case "3": sortType = "Property Ratings"; break;
                default: sortType = "TripAdvisor Rating"; break;
            }

            string sortSeqWithAscDesc = Core.GetSession(Enumeration.SessionName.SortWithAscDesc, tripid)?.ToString();
            if (!string.IsNullOrWhiteSpace(sortSeqWithAscDesc))
            {
                var _sortSq = sortSeqWithAscDesc.Split('_')?.Last();
                sortSq = _sortSq == "1" ? "desc" : "asc";
            }

            // Filter
            FilterHotelResultModel filterProperties = new FilterHotelResultModel();
            if (Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid) != null)
            {
                filterProperties = (FilterHotelResultModel)Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid);
            }

            minRate = filterProperties.DisplayMinPrice;
            maxRate = filterProperties.DisplayMaxPrice;
            fixedminRate = filterProperties.TotalPax > 0 ? ((filterProperties.DisplayMinPrice * filterProperties.NoOfRoom) + filterProperties.FltPriceFrom) / filterProperties.TotalPax : 0;
            fixedmaxRate = filterProperties.TotalPax > 0 ? ((filterProperties.DisplayMaxPrice * filterProperties.NoOfRoom) + filterProperties.FltPriceFrom) / filterProperties.TotalPax : 0;
            filterMin = filterProperties.MinPrice.ToDecimalNullable() ?? minRate;
            filterMax = filterProperties.MaxPrice.ToDecimalNullable() ?? maxRate;
            fixedfilterMin = filterProperties.TotalPax > 0 ? (((filterProperties.MinPrice.ToDecimalNullable() ?? minRate) * filterProperties.NoOfRoom) + filterProperties.FltPriceFrom) / filterProperties.TotalPax : 0;
            fixedfilterMax = filterProperties.TotalPax > 0 ? (((filterProperties.MaxPrice.ToDecimalNullable() ?? maxRate) * filterProperties.NoOfRoom) + filterProperties.FltPriceFrom) / filterProperties.TotalPax : 0;
            isAllNight = filterProperties.IncludeAllNights;
            isIncludeTax = filterProperties.IncludeTax;
            filterStar = filterProperties.Rating == "1,2,3,4,5" || filterProperties.Rating == "10" ? null : filterProperties.Rating;
            filterLoc = filterProperties.LocationNearBy;
            fltPriceFrom = filterProperties.FltPriceFrom;

            var starList = filterProperties.Result //_SearchHotelModel?.Result?.HotelList?
                ?.Select(x => x.hotelRating)
                .GroupBy(x => Math.Floor(x.ToDecimal()))
                .Select(x => new { R = x.Key.ToString("n0"), C = x.Count() })
                .OrderBy(x => x.R)
                ?.ToList();

            var locDesc = _SearchHotelModel?.Result?.HotelList?.Select(x =>
                (!string.IsNullOrWhiteSpace(x.locationDescription) ? x.locationDescription : x.Addresses?.city))?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().OrderBy(x => x) ?? new List<string>().OrderBy(x => x);

            var _obj = new
            {
                minRate,
                maxRate,
                totalProperty,
                filter = new { filterStar, filterProperty, filterMin, filterMax, filterLoc },
                sortType,
                sortSq,
                isIncludeTax,
                isAllNight,
                starList,
                locDesc,
                fltPriceFrom,
                fixedminRate,
                fixedmaxRate,
                fixedfilterMin,
                fixedfilterMax
            };

            return Json(_obj);
        }

        public ActionResult ResetFilter(string tripid)
        {
            Core.SetSession(Enumeration.SessionName.FilterHotelResult, tripid, null);
            Core.SetSession(Enumeration.SessionName.UpdateFilterHotelResult, tripid, null);
            Core.SetSession(Enumeration.SessionName.SelectedPage, tripid, null);

            return GetHotelList(1, null, null, tripid).Result;
            //return RedirectToAction("SelectedPage", "Hotel", new { page = 1 });
        }

        private FilterHotelResultModel UpdateFilter(SearchHotelModel model, FormCollection collection, FilterHotelParamModel filterParamModel)
        {
            string tripid = model.CustomerSessionId;
            List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> hotelList = model.Result.HotelList.ToList();
            IEnumerable<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> _preProcessedList = hotelList;
            FilterHotelResultModel filterModel = new FilterHotelResultModel();
            if (Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid) != null)
            {
                filterModel = (FilterHotelResultModel)Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid);
                filterModel.Result = hotelList;
            }

            #region STAR rating
            if (filterParamModel != null && !string.IsNullOrEmpty(filterParamModel.hidRating))
            {
                if (!string.IsNullOrWhiteSpace(filterParamModel.hidRating) && !filterParamModel.hidRating.Equals("10") && !filterParamModel.hidRating.Equals("1,2,3,4,5"))
                {
                    var ratingList = filterParamModel.hidRating.Split(',');
                    var ratings = ratingList.Where(r => !string.IsNullOrEmpty(r) && !r.Equals("10")).Distinct().OrderBy(r => r);
                    List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> existingHotelList = new List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation>();
                    foreach (string _star in ratings)
                    {
                        decimal _rateStarParse = _star.ToDecimal();
                        var starHotelList = _preProcessedList.Where(r => Math.Floor(r.hotelRating.ToDecimal()) == _rateStarParse)?.ToList() ?? new List<HotelInformation>();
                        if (starHotelList.Count > 0)
                        {
                            existingHotelList.AddRange(starHotelList);
                        }
                    }
                    filterModel.Rating = string.Join(",", ratings.ToArray());
                    _preProcessedList = existingHotelList;
                }
                else
                {
                    filterModel.Rating = "1,2,3,4,5";   //all stars
                }
            }
            #endregion

            #region Property name
            if (filterParamModel != null && !string.IsNullOrWhiteSpace(filterParamModel.property))
            {
                string PropertyName = filterParamModel.property;
                _preProcessedList = _preProcessedList.Where(r => r.name.ToLower().Contains(PropertyName.ToLower()));
                filterModel.PropertyName = PropertyName;
            }
            else
            {
                filterModel.PropertyName = "";
            }
            #endregion

            #region Location Near Filter
            if (filterParamModel != null && !string.IsNullOrWhiteSpace(filterParamModel.LocationNearBy) &&
                filterParamModel.LocationNearBy != "-")
            {
                _preProcessedList = _preProcessedList.Where(r => r.locationDescription == filterParamModel.LocationNearBy ||
                 r.Addresses?.city == filterParamModel.LocationNearBy);
                filterModel.LocationNearBy = filterParamModel.LocationNearBy;
            }
            else
            {
                filterModel.LocationNearBy = null;
            }
            #endregion

            #region Minimum and maximum price
            string MinimumPrice = null, MaximumPrice = null;
            if (filterParamModel != null && collection == null)
            {
                MinimumPrice = filterParamModel.minPrice;
                MaximumPrice = filterParamModel.maxPrice;
            }
            else if (collection != null)
            {
                MinimumPrice = collection["SelectedMinPrice"] ?? filterModel.MinPrice;
                MaximumPrice = collection["SelectedMaxPrice"] ?? filterModel.MaxPrice;
            }

            model.DisplayHotelSetting = model.DisplayHotelSetting ?? new DisplayHotelSetting
            {
                AsIncludedTax = model.IsB2B
            };

            if (collection["clrPrVal"] == "1")
            {
                MinimumPrice = null;
                MaximumPrice = null;
            }

            if (MinimumPrice == MaximumPrice && !string.IsNullOrWhiteSpace(MinimumPrice) && !string.IsNullOrWhiteSpace(MaximumPrice))
            {
                MinimumPrice = (MinimumPrice.ToDecimal() - 1m).ToString();
                MaximumPrice = (MaximumPrice.ToDecimal() + 1m).ToString();
            }

            decimal _lowestRate = 999999;
            decimal _highestRate = 0;
            List<int> _indexLowest = new List<int>();

            decimal _filterMinPrice = string.IsNullOrEmpty(MinimumPrice) ? 0 : MinimumPrice.ToDecimal();
            decimal _filterMaxPrice = string.IsNullOrEmpty(MaximumPrice) ? 999999 : MaximumPrice.ToDecimal();
            List<HotelInformation> _hotelMatched = new List<HotelInformation>();

            if (_filterMinPrice > _filterMaxPrice)
            {
                _filterMinPrice = _filterMaxPrice - 1m;
                _filterMaxPrice = _filterMaxPrice + 1m;
            }

            foreach (var _hotel in _preProcessedList)
            {
                List<bool> isFilterRangeHotel = new List<bool>();

                foreach (var _room in _hotel.RoomRateDetailsList)
                {
                    foreach (var _rateInfo in _room.RateInfos)
                    {
                        var _rtRoom = _rateInfo.chargeableRateInfo.RatePerRoom;

                        if (model.DisplayHotelSetting.AsAllNight && model.DisplayHotelSetting.AsIncludedTax)
                        {
                            _lowestRate = _rtRoom.AllInRate < _lowestRate ? _rtRoom.AllInRate : _lowestRate;
                            _highestRate = _rtRoom.AllInRate > _highestRate ? _rtRoom.AllInRate : _highestRate;

                            isFilterRangeHotel.Add(_rtRoom.AllInRate >= _filterMinPrice && _rtRoom.AllInRate <= _filterMaxPrice);
                        }
                        else if (model.DisplayHotelSetting.AsAllNight)
                        {
                            _lowestRate = _rtRoom.AllNightRate < _lowestRate ? _rtRoom.AllNightRate : _lowestRate;
                            _highestRate = _rtRoom.AllNightRate > _highestRate ? _rtRoom.AllNightRate : _highestRate;

                            isFilterRangeHotel.Add(_rtRoom.AllNightRate >= _filterMinPrice && _rtRoom.AllNightRate <= _filterMaxPrice);
                        }
                        else if (model.DisplayHotelSetting.AsIncludedTax)
                        {
                            _lowestRate = _rtRoom.IncludeTaxRate < _lowestRate ? _rtRoom.IncludeTaxRate : _lowestRate;
                            _highestRate = _rtRoom.IncludeTaxRate > _highestRate ? _rtRoom.IncludeTaxRate : _highestRate;

                            isFilterRangeHotel.Add(_rtRoom.IncludeTaxRate >= _filterMinPrice && _rtRoom.IncludeTaxRate <= _filterMaxPrice);
                        }
                        else
                        {
                            _lowestRate = _rtRoom.AvgRate < _lowestRate ? _rtRoom.AvgRate : _lowestRate;
                            _highestRate = _rtRoom.AvgRate > _highestRate ? _rtRoom.AvgRate : _highestRate;

                            isFilterRangeHotel.Add(_rtRoom.AvgRate >= _filterMinPrice && _rtRoom.AvgRate <= _filterMaxPrice);
                        }
                    }
                }

                if (isFilterRangeHotel.Any(x => x))
                {
                    _hotelMatched.Add(_hotel);
                }
            }

            _filterMinPrice = _filterMinPrice == 0 ? _lowestRate : _filterMinPrice;
            _filterMaxPrice = _filterMaxPrice == 999999 ? _highestRate : _filterMaxPrice;

            filterModel.DisplayMinPrice = _lowestRate;
            filterModel.DisplayMaxPrice = _highestRate;
            filterModel.MinPrice = _filterMinPrice.ToString();
            filterModel.MaxPrice = _filterMaxPrice.ToString();
            filterModel.NoOfRoom = model.NoOfRoom;
            _preProcessedList = _hotelMatched;

            /* 
             // disabled and use manual looping for achieve advance result
                // only sort from lowest price, as frontend display as LowestPrice
                decimal MinPrice = Convert.ToDecimal(string.IsNullOrEmpty(MinimumPrice) ? model.Result.HotelList.Min(r => r.lowRate).ToString() : MinimumPrice.ToString());
                decimal MaxPrice = Convert.ToDecimal(string.IsNullOrEmpty(MaximumPrice) ? model.Result.HotelList.Max(r => r.lowRate).ToString() : MaximumPrice.ToString());

                _preProcessedList = _preProcessedList.Where(r => Convert.ToDecimal(r.lowRate) >= MinPrice && Convert.ToDecimal(r.lowRate) <= MaxPrice);

                filterModel.MinPrice = MinPrice.ToString();
                filterModel.MaxPrice = MaxPrice.ToString();
            */
            #endregion

            // Execute result
            filterModel.Result = _preProcessedList.ToList();
            filterModel.TotalProperties = filterModel.Result.Count;

            if (filterModel.Result.Count > 0)
            {
                filterModel.PriceFrom = filterModel.Result.Min(r => r.lowRate).ToString();
                filterModel.FltPriceFrom = model.FixedPriceFrom;
                filterModel.TotalPax = model.IsFixedPrice ? model.NoOfAdult + model.NoOfInfant : 0;
            }

            Core.SetSession(Enumeration.SessionName.UpdateFilterHotelResult, tripid, filterModel);

            return filterModel;
        }

        private List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> SortingHoteResultList(List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> filterHotelResultModel, FormCollection collection, string tripid, bool isDefaultSort = false)
        {
            collection = collection ?? new FormCollection();
            if (collection["ddlSorting"] == null && Core.GetSession(Enumeration.SessionName.Sorting, tripid) == null)
            {
                string filterKey = collection["IsFixedPrice"] == "True" ? "2" : "1";
                collection.Add("ddlSorting", filterKey);
            }
            else if (collection["ddlSorting"] == null && Core.GetSession(Enumeration.SessionName.Sorting, tripid) != null)
            {
                string filterKey = Core.GetSession(Enumeration.SessionName.Sorting, tripid).ToString();
                collection.Add("ddlSorting", filterKey);

            }

            if (collection["sortSeq"] == null)
            {
                string sortSqAscDesc = Core.GetSession(Enumeration.SessionName.SortWithAscDesc, tripid)?.ToString();

                if (string.IsNullOrWhiteSpace(sortSqAscDesc))
                {
                    collection.Add("sortSeq", "0");
                }
                else
                {
                    string sqSplit = sortSqAscDesc.Split('_')?.LastOrDefault();
                    collection.Add("sortSeq", sqSplit ?? "0");
                }
            }

            if (collection["ddlSorting"] != null && collection["ddlSorting"].ToString().Equals("1") || isDefaultSort)    //trip advisor rating
            {
                filterHotelResultModel = filterHotelResultModel
                    .OrderByDescending(x => x.FrontEnd_SortOrder != null)
                    .ThenBy(x => x.FrontEnd_SortOrder)
                    .ThenByDescending(x => x.FrontEnd_PromoId != 0)
                    .ThenByDescending(x => Convert.ToDecimal(x.tripAdvisor != null ? x.tripAdvisor.tripAdvisorRating * x.tripAdvisor.tripAdvisorReviewCount : (double?)0.0)).ThenBy(x => x.lowRate).ToList();
                Core.SetSession(Enumeration.SessionName.Sorting, tripid, "1");
            }
            else if (collection["ddlSorting"] != null && collection["ddlSorting"].ToString().Equals("2"))   //cheapest price
            {
                filterHotelResultModel = filterHotelResultModel.OrderBy(x => x.lowRate).ToList();
                Core.SetSession(Enumeration.SessionName.Sorting, tripid, "2");
            }
            else if (collection["ddlSorting"] != null && collection["ddlSorting"].ToString().Equals("3"))   //hotel star rating
            {
                filterHotelResultModel = filterHotelResultModel.OrderByDescending(x => Convert.ToInt16(Math.Truncate(Convert.ToDouble(x.hotelRating)))).ThenBy(x => x.lowRate).ToList();
                Core.SetSession(Enumeration.SessionName.Sorting, tripid, "3");
            }

            if (collection["sortSeq"]?.ToString() == "1")
            {
                filterHotelResultModel.Reverse();
                Core.SetSession(Enumeration.SessionName.SortWithAscDesc, tripid, $"{collection["ddlSorting"]?.ToString()}_{collection["sortSeq"]?.ToString()}");
            }
            else
            {
                Core.SetSession(Enumeration.SessionName.SortWithAscDesc, tripid, $"{collection["ddlSorting"]?.ToString()}_0");
            }


            return filterHotelResultModel;
        }

        [HttpPost]
        public async Task<ActionResult> CheckInventory(string data, string tripid, List<RoomSelectedModel> roomSelected, bool cs = false)   //RoomTypeCode=RateCode=TotalRooms|RoomTypeCode=RateCode=TotalRooms
        {
            //tripid = !cs ? null : tripid;
            Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, null);

            List<SearchRoomModel> RoomModelList = new List<SearchRoomModel>();

            bool roomEnough = true;
            string flag = "NOTENOUGH";

            try
            {
                int TotalRoomSelected = roomSelected.Sum(x => x.Qty);
                int TotalMaxPax = 0;
                int TotalRoomSearched = 0;

                if (TotalRoomSelected > 8)
                {
                    throw new Exception("EXCEED");
                }

                if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
                {
                    SearchHotelModel searchHotelModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                    TotalRoomSearched = searchHotelModel.NoOfRoom;

                    if (TotalRoomSelected > TotalRoomSearched)
                    {
                        throw new Exception("EXCEEDSEARCH");
                    }

                    SearchHotelModel searchHotelModel_latest = searchHotelModel.DeepCopy();
                    List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails> requotedRoom = new List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails>();

                    var hotelGrp = roomSelected.GroupBy(x => new { x.Hotel, x.EncSupp });
                    searchHotelModel_latest.NoOfRoom = TotalRoomSelected;
                    List<GTM_HotelProductModel> _GTM_addToCartList = new List<GTM_HotelProductModel>();

                    foreach (var hotel in hotelGrp)
                    {
                        SearchRoomModel roomModel = new SearchRoomModel
                        {
                            ArrivalDate = searchHotelModel.ArrivalDate,
                            CurrencyCode = searchHotelModel.CurrencyCode,
                            CustomerIpAddress = searchHotelModel.CustomerIpAddress,
                            CustomerSessionId = searchHotelModel.CustomerSessionId,
                            CustomerUserAgent = searchHotelModel.CustomerUserAgent,
                            DepartureDate = searchHotelModel.DepartureDate,
                            HotelID = hotel.Key.Hotel,
                            SelectedNoOfRoomType = TotalRoomSelected
                        };

                        string decSupplierCode = Cryptography.AES.Decrypt(hotel.Key.EncSupp);
                        Func<HotelInformation, bool> queryFunc = (s => s.hotelSupplier.ToString() == decSupplierCode && s.hotelId == hotel.Key.Hotel);

                        var hotelSelected = searchHotelModel.Result.HotelList.FirstOrDefault(queryFunc) ??
                            searchHotelModel.B2BResult?.HotelList?.FirstOrDefault(x => x.SupplierHotels.Any(queryFunc))?.SupplierHotels?.FirstOrDefault(queryFunc);
                        SearchRoomModel roomCache = (SearchRoomModel)Core.GetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + hotel.Key.Hotel);
                        roomCache = roomCache.DeepCopy();
                        roomCache.SelectedNoOfRoomType = TotalRoomSelected;
                        roomCache.HotelID = roomModel.HotelID;

                        foreach (var room in hotel)
                        {
                            var p = roomCache.Result.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList).FirstOrDefault(y => y.propertyId == room.PropertyId &&
                            (y.roomTypeCode == room.TypeCode || y.jacTravelBookingToken == room.TypeCode || y.jacTravelPropertyRoomTypeID == room.TypeCode) &&
                            (y.rateCode == room.RateCode || room.RateCode == "0")
                            && (hotelSelected.hotelSupplier != HotelSupplier.EANRapid || (hotelSelected.hotelSupplier == HotelSupplier.EANRapid && y.BetTypes.Any(b => b.id == room.RateToken)))
                            );

                            TotalMaxPax += Convert.ToInt16(p?.rateOccupancyPerRoom ?? "1") * room.Qty;

                            if (hotelSelected.hotelSupplier == HotelSupplier.Expedia)
                            {
                                // 2017/10/21 - This cause final rate not accurate (ex. Not include ExtraPersonFees when more pax).
                                //searchHotelModel_latest.NoOfAdult = 1;
                                //searchHotelModel_latest.NoOfRoom = room.Qty;
                                //roomModel.SelectedNoOfRoomType = room.Qty;

                                roomModel.Result = await ESBHotelServiceCall.GetRoomAvailabilityAsync(roomModel, searchHotelModel_latest, hotelSelected.hotelSupplier);

                                #region PromoCode Section
                                if (Core.IsEnablePayByPromoCode && searchHotelModel.IsPromoCodeUsed &&
                                    roomModel.Result != null && roomModel.Result.HotelRoomInformationList != null && roomModel.Result.HotelRoomInformationList.Length > 0)
                                {
                                    MayFlower db = new MayFlower();
                                    var promoCodeRule = GetPromoCodeDiscountRule(searchHotelModel_latest, searchHotelModel_latest.Result.HotelList, db);
                                    if (promoCodeRule != null)
                                    {
                                        HotelServiceController.ProcessDiscountCalculation(roomModel.Result.HotelRoomInformationList, promoCodeRule, searchHotelModel_latest.Destination, roomModel.SelectedNoOfRoomType, searchHotelModel_latest);
                                    }
                                }
                                #endregion

                                roomModel.Result.customerSessionId = searchHotelModel.CustomerSessionId;
                                Func<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails, bool> checkBy = (x => x.rateCode == room.RateCode && x.roomTypeCode == room.TypeCode);
                                if (roomModel.Result != null && roomModel.Result.HotelRoomInformationList != null && roomModel.Result.HotelRoomInformationList.Length > 0 &&
                                    roomModel.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.Any(checkBy))
                                {
                                    var r = roomModel.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.Where(checkBy);
                                    requotedRoom.AddRange(r);
                                    //roomModel.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList = roomModel.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.Where(checkBy).ToArray();
                                    //RoomModelList.Add(roomModel);
                                }
                                else
                                {
                                    //if (roomModel.Result != null && roomModel.Result.Errors != null)
                                    //if (roomModel.Result.Errors.Category == "SOLD_OUT")
                                    throw new Exception("Oops. We don't have so many room(s) available for this room type. Pick another one?");
                                }
                            }

                            if (p != null)
                            {
                                var ChargePerNight = p.RateInfos[0].chargeableRateInfo.NightlyRatesPerRoom;
                                foreach (var charge in ChargePerNight.GroupBy(grp => grp.baseRate))
                                {
                                    _GTM_addToCartList.Add(new GTM_HotelProductModel
                                    {
                                        name = "Hotel - " + hotelSelected.Addresses.city,
                                        hotelName = System.Web.HttpUtility.HtmlEncode(hotelSelected.name),
                                        id = room.Hotel,
                                        price = charge.Key.ToString(), //baseRate
                                        roomType = room.Name,
                                        quantity = room.Qty * charge.Count(),
                                        numberOfRooms = room.Qty,
                                        numberOfNights = charge.Count(),
                                    });
                                }
                            }
                        }

                        // Epxedia calculate in PreBook JacTraveServiceCall.cs
                        if ((roomModel.Result != null && roomModel.Result.HotelRoomInformationList == null) ||
                            (hotelSelected.hotelSupplier != HotelSupplier.JacTravel
                            && hotelSelected.hotelSupplier != HotelSupplier.HotelBeds
                            && hotelSelected.hotelSupplier != HotelSupplier.EANRapid // EANRapid no need check pax, because it is based on quote book by TOKEN.
                            && (searchHotelModel.NoOfInfant + searchHotelModel.NoOfAdult) > TotalMaxPax)
                            //&& false
                            )
                        {
                            throw new Exception("NOTENOUGH");
                        }

                        if (hotelSelected.hotelSupplier == HotelSupplier.Expedia)
                        {
                            if (RoomModelList.Sum(x => x.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.First().rateOccupancyPerRoom.ToDecimal()) > TotalMaxPax)
                            { Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, RoomModelList); flag = "ENOUGH"; }
                        }
                        else if (hotelSelected.hotelSupplier == HotelSupplier.Tourplan)
                        {
                            var tpRoomAvailable = TourplanServiceCall.GetHotelAvailability(roomCache, searchHotelModel_latest);

                            if (tpRoomAvailable == null || tpRoomAvailable.OptionInfoReply == null || tpRoomAvailable.OptionInfoReply.Option == null)
                            {
                                throw new Exception("Opps... Unknow error occur, please try again.");
                            }

                            var roomAvaiSelected = tpRoomAvailable.OptionInfoReply.Option.Where(x => roomSelected.Any(r => r.RateCode == x.Opt));

                            roomEnough = roomAvaiSelected.Count() == roomSelected.Select(x => x.RateCode).Distinct().Count() && roomAvaiSelected.All(x =>
                            {
                                var roomGrp = roomSelected.GroupBy(grp => grp.RateCode);

                                List<bool> enoughList = new List<bool>();

                                foreach (var item in roomGrp)
                                {
                                    var rNo = x.OptDetailedAvails.OptDetailedAvail.OptAvail.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    var checking = rNo.Any(q => q != "-3" && Convert.ToInt32(q) >= item.Sum(s => s.Qty));
                                    enoughList.Add(checking);

                                    //return checking;
                                }

                                //var isEnoughResult = roomGrp.All(grp => grp.All(r => r.RateCode == x.Opt &&
                                //x.OptDetailedAvails.OptDetailedAvail.OptAvail.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Any(q => q != "-3" && Convert.ToInt32(q) >= grp.Sum(s => s.Qty))
                                ////&& x.OptDetailedAvails.OptDetailedAvail.UnitType.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Any(q => q.ToLower() == r.TypeCode.ToLower())
                                //));

                                return enoughList.All(t => t == true);
                            });
                        }
                        else if (hotelSelected.hotelSupplier == HotelSupplier.JacTravel)
                        {
                            var tpRoomAvailable = JacTravelServiceCall.PreBookHotelRooms(roomCache, searchHotelModel_latest, roomSelected);
                            roomEnough = tpRoomAvailable.ReturnStatus != null && tpRoomAvailable.ReturnStatus.Success.ToBoolean();
                            roomCache.PreBookToken = roomEnough ? tpRoomAvailable.PreBookingToken : null;
                        }
                        else if (hotelSelected.hotelSupplier == HotelSupplier.HotelBeds)
                        {
                            if (roomSelected.Any(x => x.RateType == "RECHECK"))
                            {
                                var _chkRateList = roomCache.Result.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList)
                                    .Where(x => roomSelected.Any(r => r.TypeCode == x.roomTypeCode));
                                var chkResList = await HotelBedServiceCall.GetHotelRatesAsync(_chkRateList.SelectMany(x => x.RateInfos).SelectMany(x => x.Rooms).Select(x => x.rateKey));

                                roomEnough = chkResList.All(x => x.Errors == null && x.Hotel != null) && _chkRateList.Count() > 0;
                            }
                            else
                            {
                                roomEnough = true;
                            }
                        }
                        else if (hotelSelected.hotelSupplier == HotelSupplier.ExpediaTAAP)
                        {
                            if (RoomModelList.Sum(x => x.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.First().rateOccupancyPerRoom.ToDecimal()) > TotalMaxPax)
                            { Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, RoomModelList); flag = "ENOUGH"; }
                        }
                        else if (hotelSelected.hotelSupplier == HotelSupplier.EANRapid)
                        {
                            // check quoted inventory
                            int roomSelectedQuotePax = 0;
                            List<RoomAvailabilityDetails> _roomSelectedRapid = new List<RoomAvailabilityDetails>();
                            foreach (var roomItem in roomCache.Result.HotelRoomInformationList)
                            {
                                var roomList = roomItem.roomAvailabilityDetailsList;
                                //var roomToken = roomList.SelectMany(s => s.BetTypes);

                                foreach (var item in roomList)
                                {
                                    foreach (var _roomP in roomSelected)
                                    {
                                        if (item.BetTypes.Any(b => b.id == _roomP.RateToken))
                                        {
                                            roomSelectedQuotePax += item.quotedOccupancy.ToInt();
                                            _roomSelectedRapid.Add(item);
                                        }
                                    }
                                }
                            }

                            if (roomSelectedQuotePax <= 0)
                            {
                                throw new Exception("You are out of luck, please select another room.");
                            }
                            //if (RoomModelList.Sum(x => x.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.First().rateOccupancyPerRoom.ToDecimal()) > TotalMaxPax)
                            //{ Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, RoomModelList); flag = "ENOUGH"; }
                        }
                        else
                        {
                            throw new Exception("You are out of luck, please try again later.");
                        }

                        Core.SetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + roomCache.HotelID, roomCache); //to save PreBookToken into Session RoomAvail for crosssell
                        int pplAssinged = hotelSelected.hotelSupplier == HotelSupplier.Expedia ? 1 : searchHotelModel.NoOfAdult;
                        List<int> adultAssignToRoom = ESBHotelServiceCall.CustomerTypeRoomAssign(pplAssinged, TotalRoomSelected).ReplaceZeroWithOne();
                        List<int> childAssignToRoom = ESBHotelServiceCall.CustomerTypeRoomAssign(searchHotelModel.NoOfInfant, TotalRoomSelected) ?? new List<int>();

                        roomCache.GuestInRoomDetails = roomCache.GuestInRoomDetails ?? new List<GuestRoomDetails>();
                        roomCache.GuestInRoomDetails.Clear();
                        for (int i = 0; i < adultAssignToRoom.Count; i++)
                        {
                            roomCache.GuestInRoomDetails.Add(new GuestRoomDetails
                            {
                                Adults = adultAssignToRoom[i],
                                Childrens = childAssignToRoom[i],
                                //Infants = infantAssignToRoom[i],
                            });
                        }

                        if (roomEnough && hotelSelected.hotelSupplier == HotelSupplier.Expedia)
                        {
                            roomCache.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList = requotedRoom.ToArray();
                            RoomModelList.Add(roomCache);
                        }
                        else if (roomEnough && hotelSelected.hotelSupplier != HotelSupplier.Expedia)
                        {
                            RoomModelList.Add(roomCache);
                        }
                    }

                    if (RoomModelList.Count > 0)
                    {
                        Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, RoomModelList);
                        //flag = "ENOUGH";
                        _GTM_addToCartList.Add(new GTM_HotelProductModel()
                        {
                            id = "ENOUGH"
                        });
                        return Json(_GTM_addToCartList, JsonRequestBehavior.DenyGet);

                    }
                    else if (!roomEnough && RoomModelList.Count == 0)
                    {
                        throw new Exception("Oops. We don't have so many room(s) available for this room type. Pick another one?");
                    }

                    return Json(flag, JsonRequestBehavior.DenyGet);

                    #region Previous Method For Disabled for Testing new Method By MVC Model Binding
                    /*
                    string[] arrData = data.Split('|'); //HotelId=RoomTypeCode=RateCode=TotalRooms=RoomName

                    List<string> roomTypeList = new List<string>();
                    List<int> roomTypeNo = new List<int>();
                    for (int i = 0; i < arrData.Length; i++)
                    {
                        string[] arrDataDetails = arrData[i].Split('=');
                        if (arrDataDetails.Length == 5 || arrDataDetails.Length == 6 || arrDataDetails.Length == 7)
                        {
                            int emptyarr = arrDataDetails.Count(s => s == "");
                            string HotelID = arrDataDetails[0];
                            string roomTypeCode = arrDataDetails.Length == 5 ? arrDataDetails[1] : arrDataDetails[1] + (emptyarr == 1 ? "=" : "==");
                            string rateCode = arrDataDetails.Length == 5 ? arrDataDetails[2] : arrDataDetails[emptyarr + 2];
                            int TotalRooms = arrDataDetails.Length == 5 ? Convert.ToInt16(arrDataDetails[3]) : Convert.ToInt16(arrDataDetails[emptyarr + 3]);
                            string roomName = arrDataDetails.Length == 5 ? arrDataDetails[4] : arrDataDetails[emptyarr + 4];

                            searchHotelModel_latest.NoOfRoom = TotalRooms;
                            searchHotelModel_latest.JacRoomTypeCode = roomTypeCode;

                            SearchRoomModel roomModel = new SearchRoomModel
                            {
                                ArrivalDate = searchHotelModel.ArrivalDate,
                                CurrencyCode = searchHotelModel.CurrencyCode,
                                CustomerIpAddress = searchHotelModel.CustomerIpAddress,
                                CustomerSessionId = searchHotelModel.CustomerSessionId,
                                CustomerUserAgent = searchHotelModel.CustomerUserAgent,
                                DepartureDate = searchHotelModel.DepartureDate,
                                HotelID = HotelID,
                                SelectedNoOfRoomType = TotalRooms
                            };

                            roomModel.Result = ESBHotelServiceCall.GetRoomAvailability(roomModel, searchHotelModel_latest);
                            roomModel.Result.customerSessionId = searchHotelModel.CustomerSessionId;

                            if (roomModel.Result != null && roomModel.Result.HotelRoomInformationList != null)
                            {
                                if (roomModel.Result.HotelRoomInformationList.FirstOrDefault().hotelSupplier.ToString() == "Tourplan")
                                {
                                    var tproomavailable = TourplanServiceCall.GetHotelAvailability(roomModel, searchHotelModel_latest);
                                    var eachroom = tproomavailable.OptionInfoReply.Option[0].OptDetailedAvails.OptDetailedAvail.OptAvail.Split(' ').ToList();
                                    foreach (var each in eachroom)
                                    {
                                        if (each != "-3" && each != "" && Convert.ToInt32(each) < TotalRoomSelected)
                                        {
                                            roomEnough = false;
                                        }
                                    }
                                }
                                else if (roomModel.Result.HotelRoomInformationList.FirstOrDefault().hotelSupplier.ToString() == "JacTravel")
                                {
                                    roomtypeno.Insert(jacroomtype, searchHotelModel_latest.NoOfRoom);
                                    roomtypelist.Insert(jacroomtype, searchHotelModel_latest.Jacroomtypecode);
                                    jacroomtype++;
                                    if (i == arrData.Length - 1)
                                    {
                                        var tproomavailable = JacTravelServiceCall.PreBookHotelRooms(roomModel, searchHotelModel_latest, roomtypeno, roomtypelist);
                                        if (tproomavailable.ReturnStatus.Success != "true")
                                        {
                                            roomenough = false;
                                        }
                                        else
                                        {
                                            roomModel.PreBookingToken = tproomavailable.PreBookingToken;
                                        }
                                    }
                                }
                                var details = roomModel.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.Where(r => r.roomTypeCode.Equals(roomTypeCode) && r.rateCode.Equals(rateCode));

                                if (roomModel.Result.HotelRoomInformationList.FirstOrDefault().hotelSupplier.ToString() == "JacTravel")
                                {
                                    details = roomModel.Result.HotelRoomInformationList.First().roomAvailabilityDetailsList.Where(r => roomTypeList.Contains(r.jacTravelPropertyRoomTypeID) || roomTypeList.Contains(r.jacTravelBookingToken));
                                }

                                if (details.Count() > 0 && roomEnough)
                                {
                                    //quotedOccupancy = max pax without extra pax charge
                                    TotalMaxPax += Convert.ToInt16(details.FirstOrDefault().rateOccupancyPerRoom) * TotalRooms;

                                    RoomModelList.Add(roomModel);
                                }
                                else { throw new Exception(string.Format("{0} Room(s) for {1} not available betwen {2} until {3}", TotalRooms, roomName, searchHotelModel_latest.ArrivalDate.ToString("dd-MMM-yyyy"), searchHotelModel_latest.DepartureDate.ToString("dd-MMM-yyyy"))); }
                            }
                            else { throw new Exception(string.Format("{0} Room(s) for {1} not available betwen {2} until {3}", TotalRooms, roomName, searchHotelModel_latest.ArrivalDate.ToString("dd-MMM-yyyy"), searchHotelModel_latest.DepartureDate.ToString("dd-MMM-yyyy"))); }
                        }
                    }

                    if (searchHotelModel.NoOfAdult > TotalMaxPax && RoomModelList[0].Result.HotelRoomInformationList.FirstOrDefault().hotelSupplier.ToString() != "JacTravel") { flag = "NOTENOUGH"; }
                    else if (RoomModelList.Count > 0) { Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, RoomModelList); flag = "ENOUGH"; }
                    */
                    #endregion
                }
                else { flag = "TIMEOUT"; }
            }
            catch (Exception ex) { flag = ex.Message; }
            return Json(flag, JsonRequestBehavior.DenyGet);
        }
        #endregion

        #region Step 2 - Prepare model to Step 3
        [HttpPost]
        [SessionFilter(SessionName = "HotelList,RoomAvailList")] // add RoomAvailList ensure not null
        public ActionResult ReserveRoom(string tripid)
        {
            try
            {
                MayFlower db = new MayFlower();

                //string oriQueryString = Uri.UnescapeDataString(Request.QueryString.ToString());
                //var parseNameCollection = System.Web.HttpUtility.ParseQueryString(oriQueryString);
                //string modelString = parseNameCollection["key"].ToString();

                var roomAttr = Request.Form["key"]?.ToString() ?? "";

                string roomSelectedSerialize = Request.QueryString["key"] ?? HttpUtility.UrlDecode(roomAttr);
                List<RoomSelectedModel> roomSelected = JsonConvert.DeserializeObject<List<RoomSelectedModel>>(roomSelectedSerialize);
                foreach (var roomSelect in roomSelected)
                {
                    roomSelect.PropertyId = roomSelect.PropertyId != "" ? roomSelect.PropertyId : null;
                }
                Core.SetSession(Enumeration.SessionName.HotelRoomListSelected, tripid, roomSelected);

                if (string.IsNullOrWhiteSpace(roomSelectedSerialize) || roomSelected.Sum(x => x.Qty) <= 0)
                {
                    Core.SetSession(Enumeration.SessionName.ErrorMessage, "Please select room first.");
                    return RedirectToAction("Search", "Hotel");
                }
                else if (roomSelected.Sum(x => x.Qty) > 8)
                {
                    Core.SetSession(Enumeration.SessionName.ErrorMessage, "EXCEED");
                    return RedirectToAction("Search", "Hotel");
                }

                List<GTM_HotelProductModel> _GTM_addToCartList = new List<GTM_HotelProductModel>();
                HotelServiceController.InitializeModel hc = new HotelServiceController.InitializeModel(tripid, Request.UserAgent, GetUserIP());
                //ReserveRoomModel _reserveModel = new ReserveRoomModel();
                List<RoomDetail> _RoomDetails = new List<RoomDetail>();

                List<SearchRoomModel> _searchRoomModelList = (List<SearchRoomModel>)Core.GetSession(Enumeration.SessionName.RoomAvailList, tripid);
                SearchHotelModel requestModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                SearchRoomModel _searchRoomModel = _searchRoomModelList.FirstOrDefault();

                HotelInformation _hotelSelected = null;

                int promoCodeId = 0;
                _RoomDetails = hc.InitializeRoomDetailModel(roomSelected, _searchRoomModelList, requestModel.IsDynamic, out _GTM_addToCartList, out promoCodeId);
                //_reserveModel = hc.InitializeReserveRoomModel(roomSelected, _searchRoomModelList, out _GTM_addToCartList);

                var roomType = "";
                foreach (var HotelRoom in _GTM_addToCartList)
                {
                    roomType += (roomType != "" ? "," + HotelRoom.roomType : HotelRoom.roomType);
                }

                var hotelImage = "";

                if (requestModel != null)
                {
                    var hSupplier = _searchRoomModel?.Result?.HotelRoomInformationList?.FirstOrDefault()?.hotelSupplier;
                    Func<HotelInformation, bool> _chkExpression = (a => a.hotelId == _searchRoomModel?.HotelID && hSupplier == a.hotelSupplier);

                    _hotelSelected = requestModel.Result.HotelList.FirstOrDefault(_chkExpression) ??
                                        requestModel.B2BResult?.HotelList?.SelectMany(a => a.SupplierHotels)?.FirstOrDefault(_chkExpression);
                    hotelImage = _hotelSelected?.imagesURL?.Big_350x350 ?? "~/Images_hotel/no-img-01.png";
                }

                HotelViewedCookie Hotelviewedcookie = new HotelViewedCookie
                {
                    HotelID = _searchRoomModel.HotelID,
                    Name = _searchRoomModel?.Result?.HotelRoomInformationList?.FirstOrDefault()?.hotelName,
                    Date = _searchRoomModel?.ArrivalDate.ToString("dd-MMM-yyyy") + " - " + _searchRoomModel?.DepartureDate.ToString("dd-MMM-yyyy"),
                    Image = hotelImage,
                    room = roomType,
                    lastsearch = DateTime.Now
                };

                Trackhotelviewedcookie(Hotelviewedcookie);

                #region Redirect to Checkout/GuestDetails
                var hotelCheckoutModel = initHotelCheckoutFromStep2Reserve(_searchRoomModel.Result, _searchRoomModelList);
                var reservePricingDetail = new ProductPricingDetail
                {
                    Currency = requestModel.CurrencyCode,
                    Items = _RoomDetails.GroupBy(x => new { x.RoomTypeCode, x.RoomTypeName, x.TotalBaseRate, x.TotalTaxAndServices, x.TotalGST }).Select(x => new ProductItem
                    {
                        ItemDetail = x.Key.RoomTypeName,
                        ItemQty = x.Count(),
                        BaseRate = x.Key.TotalBaseRate,
                        Surcharge = x.Key.TotalTaxAndServices,
                        Supplier_TotalAmt = x.Sum(s => s.TotalBaseRate_Source) + x.Sum(s => s.TotalTaxAndServices_Source) + x.Sum(s => s.TotalGST_Source),
                        GST = x.Key.TotalGST,
                    }).ToList(),
                    Sequence = 2,
                };
                if (promoCodeId != 0)
                {
                    reservePricingDetail.DiscountInsert(new DiscountDetail
                    {
                        Seq = 2,
                        DiscType = DiscountType.CODE,
                        PrdType = ProductTypes.Hotel,
                        Disc_Amt = PaymentServiceController.CalcPromoDiscAmount(promoCodeId, _RoomDetails.Sum(s => s.TotalBaseRate), requestModel.TotalStayDays), // calculate discount amount here
                        Disc_Desc = "Promo Code Discount",
                    });
                }

                //check have bundleTicket at the selected hotel or not
                var hotelBundleTicketSets = db.HotelBundleTicketSets.FirstOrDefault(x => x.HotelID == _hotelSelected.hotelId &&
                                            x.isActive == true &&
                                            ((x.TravelStart <= requestModel.ArrivalDate && x.TravelEnd >= requestModel.ArrivalDate) ||
                                                (x.TravelStart <= requestModel.DepartureDate && x.TravelEnd >= requestModel.DepartureDate)));

                var checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
                checkout = checkout ?? new CheckoutProduct();

                checkout.CheckoutStep = 3;

                checkout.PromoID = promoCodeId;
                checkout.IsRegister = false;
                checkout.ImFlying = false;
                checkout.RequireInsurance = false;
                checkout.RemoveProduct(ProductTypes.Hotel);
                checkout.BusinessType = IsAgentUser ? BusinessType.B2B : BusinessType.B2C;
                checkout.InsertProduct(new ProductHotel()
                {
                    HotelSelected = new List<HotelInformation> { _hotelSelected },
                    RoomAvailabilityResponse = hotelCheckoutModel.RoomAvailabilityResponse,
                    SearchHotelInfo = requestModel,
                    SearchRoomList = hotelCheckoutModel.SearchRoomModelList,
                    RoomDetails = _RoomDetails,

                    PricingDetail = reservePricingDetail,
                    ProductSeq = 2,
                    HasHotelBundleTicket = hotelBundleTicketSets != null ? true : false
                });
                if (checkout.Flight != null && checkout.Flight.TravellerDetails != null)
                {
                    foreach (var psg in checkout.Flight.TravellerDetails)
                    {
                        psg.HotelSpecialRequest = new SpecialRequestTraveller
                        {
                            CheckInMode = CheckInModeEnum.Either,
                            SmokingPreferences = SmokingPreferencesEnum.Either,
                        };
                    }
                }
                checkout.IsDynamic = requestModel.IsDynamic;
                checkout.IsFixedPrice = requestModel.IsFixedPrice;
                Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkout);
                Core.SetSession(Enumeration.SessionName.GTM_trackAddToCart, tripid, JsonConvert.SerializeObject(_GTM_addToCartList));
                return checkout.Hotel.SearchHotelInfo.IsFixedPrice ? RedirectToAction("Search", "Flight", new { tripid }) : RedirectToAction("GuestDetails", "Checkout", new { tripid });
                #endregion
            }
            catch (AggregateException ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Hotel Step 2 Reserve Room Action Error - " + DateTime.Now.ToLoggerDateTime());

                foreach (var error in ex.InnerExceptions)
                {
                    sb.AppendLine().AppendLine();
                    sb.AppendLine("Error on ReserveRoom(FormCollection collection)");
                    sb.AppendFormat("{0,-25}: {1}", "Exceptions Message", error.Message).AppendLine();
                    sb.AppendFormat("{0,-25}: {1}", "Inner Exception Message", error.GetInnerExceptionMsg()).AppendLine();
                }

                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, sb.ToString());
                return RedirectToAction("Search", "Hotel", new { reference = "error", tripid });
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error on ReserveRoom(FormCollection collection)");
                return RedirectToAction("Search", "Hotel", new { reference = "error", tripid });
            }
        }
        #endregion

        #region Service Call: Get hotel, rooms information
        /// <summary>
        /// Prepare for sessionless get hotel list.
        /// </summary>
        /// <param name="hotel"></param>
        /// <param name="currentCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult> GtHotel(Search.Hotel hotel, string currentCode = "MYR")
        {
            var hotelReqInit = new HotelListRequest
            {
                ArrivalDate = hotel.DateFrom.Date, // setter by user pass in date
                DepartureDate = hotel.DateTo.Date, // setter by user pass in date
                CrossSale = (_ExecutedController == "checkout" && _ExecutedAction == "addon"), // setter by controller action, if addon then true, else false
                CurrencyCode = currentCode, // setter by global currency select by customer
                IsB2B = (User.Identity.IsAuthenticated ? CustomPrincipal.IsAgent : false), // setter by check user is login as Agent
                rateType = RateType.MerchantStandard, // setter by application default
                UserLoginID = (User.Identity.IsAuthenticated && CustomPrincipal.IsAgent ? CustomPrincipal.LoginID :
                                User.Identity.IsAuthenticated ? CustomPrincipal.Email : null), // setter by agent user login id
                Search_by = Alternative_Searching.Search_By_Destination,
            };

            hotelReqInit.Search_by_Destination = new Search_By_Destination
            {
                Destination = hotel.Destination, // setter by frontend user type in destination
                CountryCode = null, // setter by frontend user type in destination country
            };

            hotelReqInit.CustomerUserAgent = Request.UserAgent;
            hotelReqInit.CustomerSessionId = Guid.NewGuid().ToString();
            hotelReqInit.CustomerIpAddress = GetUserIP();

            string _sToken = SerializeHotelSearchToken(hotel.DateFrom, hotel.DateTo, hotel.NoRooms, hotel.Adults, hotel.Kids,
                hotelReqInit.CrossSale, hotelReqInit.IsB2B, hotelReqInit.rateType, hotelReqInit.CurrencyCode);

            var _room = JsonConvert.DeserializeObject<Search.Hotel.Room>(_sToken);
            _room.InitESBRoomRequest(true);
            hotelReqInit.NumberOfRoom = _room.ESBRoomRequest.NumberOfRoom;

            ESBHotelManagerClient webservice = new ESBHotelManagerClient();
            var req = await webservice.GetHotelListAsync(hotelReqInit);

            ViewBag.TripID = hotelReqInit.CustomerSessionId;

            return PartialView("~/Views/Hotel/v2/_HotelList.cshtml", req);
        }

        [SessionFilter(SessionName = "HotelList")]
        public async Task<ActionResult> GetFixedRoom(Search.Hotel.Room room, string tripid)
        {
            return await GtRoom(room);
        }

        public async Task<ActionResult> GtRoom(Search.Hotel.Room room)
        {
            ESBHotelManagerClient webservice = new ESBHotelManagerClient();
            MayFlower _db = new MayFlower();

            bool isB2BDualPricing = false;
            RoomAvailabilityRequest b2bRoomRequest = null;
            room.InitESBRoomRequest(true);
            room.ESBRoomRequest.CustomerUserAgent = Request.UserAgent;
            room.ESBRoomRequest.CustomerSessionId = tripid;
            room.ESBRoomRequest.CustomerIpAddress = Request.IsLocal ? "1.1.1.1" : GetUserIP();
            room.ESBRoomRequest.UserLoginID = User.Identity.IsAuthenticated ? CustomPrincipal.LoginID : null;

            #region EPSRapid B2B Special Dual Price Function
            if (CustomPrincipal.IsAgent && room.ESBRoomRequest.HotelSuppliers == Suppliers.EANRapid)
            {
                // For get package rate and standalone rate usage.
                room.ESBRoomRequest.CrossSale = false;

                // Update for display dual pricing for EANRapid
                isB2BDualPricing = (room.ESBRoomRequest.HotelSuppliers == Suppliers.EANRapid) &&
                                    room.ESBRoomRequest.IsB2B && !room.ESBRoomRequest.CrossSale;

                // If is B2B then clone one set out first
                if (CustomPrincipal.IsAgent)
                {
                    b2bRoomRequest = JsonConvert.DeserializeObject<RoomAvailabilityRequest>(JsonConvert.SerializeObject(room.ESBRoomRequest));
                    b2bRoomRequest.CrossSale = true;
                }

                // Check IsAgentGroupDisplayAllSupplier get rate token pass in or not, if not then get from model.
                if (string.IsNullOrWhiteSpace(room.ESBRoomRequest.OptionalCode))
                {
                    var _hotelSelected = _SearchHotelModel.Result.HotelList.FirstOrDefault(x =>
                                            x.hotelSupplier.ToString() == room.ESBRoomRequest.HotelSuppliers.ToString()
                                            && x.hotelId == room.ESBRoomRequest.HotelID);

                    room.ESBRoomRequest.OptionalCode = _hotelSelected?.OptionalCode;
                }

                /*
                 * This is chain feature function from GetHotelList() to _HotelList.cshtml to GtRoom()
                 * If B2C token is null then call get hotellist to get EPSRapid B2C RateToken
                 */
                if (isB2BDualPricing)
                {
                    var tempHotelModel = _SearchHotelModel.DeepCopy();
                    tempHotelModel.IsCrossSell = false;
                    tempHotelModel.SupplierIncluded = new SearchSupplier { EANRapid = true };

                    var hotelRes = await getHotelFromEBSSearchModel(tempHotelModel, new List<string> { b2bRoomRequest.HotelID });
                    room.ESBRoomRequest.OptionalCode = hotelRes.HotelList[0].OptionalCode;
                }
            }
            #endregion

            Alphareds.Module.EANRapidHotels.RapidServices.GetContentResponse hotelContent = null;
            RoomAvailabilityResponse respond = null;
            RoomAvailabilityResponse respondB2B = null;

            List<Task> tasks = new List<Task>
            {
                webservice.GetRoomAvailabilityAsync(room.ESBRoomRequest)
                .ContinueWith((res) => respond = res.Result ),
            };

            if (room.ESBRoomRequest.HotelSuppliers == Suppliers.EANRapid)
            {
                tasks.Add(EANRapidHotelServiceCall.GetPropertyContentAsync(room.ESBRoomRequest.CustomerIpAddress,
                room.ESBRoomRequest.CustomerUserAgent, tripid, room.ESBRoomRequest.HotelID)
                .ContinueWith((res) => hotelContent = res.Result));

                //TODO: For call 2 times API to get normal rate and package rate.
                if (isB2BDualPricing)
                {
                    tasks.Add(webservice.GetRoomAvailabilityAsync(b2bRoomRequest)
                        .ContinueWith((res) => respondB2B = res.Result));
                }
            }

            await Task.WhenAll(tasks);

            if (isB2BDualPricing && respondB2B != null)
            {
                List<RoomAvailabilityDetails> _packageRate = new List<RoomAvailabilityDetails>();
                var normalSearch = respond.HotelRoomInformationList.SelectMany(x => x.roomAvailabilityDetailsList).ToList();
                var rateList = normalSearch.SelectMany(x => x.RateInfos);

                foreach (var item in respondB2B.HotelRoomInformationList)
                {
                    foreach (var itemRoom in item.roomAvailabilityDetailsList)
                    {
                        itemRoom.IsPackageRate = true;
                        bool isRateExists = itemRoom.RateInfos.Any(x => rateList.Any(a => a.nonRefundable == x.nonRefundable
                                                && a.chargeableRateInfo.total == x.chargeableRateInfo.total));

                        if (!isRateExists)
                        {
                            normalSearch.Add(itemRoom);
                        }
                    }
                }

                respond.HotelRoomInformationList[0].roomAvailabilityDetailsList = normalSearch.ToArray();
            }

            if (respond?.Errors?.ErrorMessage?.Length > 0 && (respond?.HotelRoomInformationList == null || respond?.HotelRoomInformationList?.Length == 0))
            {
                Logger.Error($"Error when get room from service {DateTime.Now.ToLoggerDateTime()}"
                    + Environment.NewLine + Environment.NewLine + respond?.Errors?.ErrorMessage
                    + Environment.NewLine + Environment.NewLine + JsonConvert.SerializeObject(room.ESBRoomRequest, Formatting.Indented,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError);
            }
            else if (hotelContent != null && hotelContent.result?.Count > 0)
            {
                // Bind hotel check-in instruction here
                var _hotelInfo = hotelContent.result.FirstOrDefault().Value;

                respond._CheckInInstruc = _hotelInfo?.checkin?.instructions;
                respond._SpecialCheckInInstruc = _hotelInfo?.checkin?.special_instructions;
            }

            /* Set session, if want sessionless, remove this, due to overall site use session, 
             * so remove this will cause unable proceed to /checkout/guestdetail page.
            */
            var _oldRoomModel = InitRoomSearchModelSession(room, tripid);

            #region PromoCode Section
            if (Core.IsEnablePayByPromoCode && _SearchHotelModel?.PromoId > 0)
            {
                MayFlower db = new MayFlower();
                var promoCodeRule = GetPromoCodeDiscountRule(_SearchHotelModel, _SearchHotelModel.Result.HotelList, db);
                if (promoCodeRule != null)
                {
                    HotelServiceController.ProcessDiscountCalculation(respond?.HotelRoomInformationList ?? new HotelRoomInformation[] { },
                        promoCodeRule, _SearchHotelModel.Destination, _SearchHotelModel.NoOfRoom, _SearchHotelModel);
                }
            }
            #endregion
            _oldRoomModel.Result = respond;
            int noOfRoomReq = room?.ESBRoomRequest?.NumberOfRoom?.Length ?? 1;
            noOfRoomReq = noOfRoomReq == 0 ? 1 : noOfRoomReq;
            respond.numberOfRoomsRequested = respond.numberOfRoomsRequested ?? noOfRoomReq.ToString();
            int roomPairOptionGrp = 0;

            foreach (var item in _oldRoomModel?.Result?.HotelRoomInformationList ?? new HotelRoomInformation[] { })
            {
                bool isSingleRoomQuote = item.hotelSupplier != HotelSupplier.Expedia;
                int totalNightStay = (item.departureDate.ToDateTime().Date - item.arrivalDate.ToDateTime().Date).TotalDays.ToString().ToInt();
                string lblroomPairGrp = $"RoomGrp_{roomPairOptionGrp++}";

                foreach (var roomList in item?.roomAvailabilityDetailsList ?? new RoomAvailabilityDetails[] { })
                {
                    roomList.RoomOptionGroup = item.hotelSupplier == HotelSupplier.EANRapid ? lblroomPairGrp : null;
                    var _castRateInf = HotelServiceController.CalcRatePerRoom(roomList.RateInfos, totalNightStay, noOfRoomReq, isSingleRoomQuote)?.ToList() ?? new List<RateInfo>();

                    // For JacTravel Book usage.
                    roomList.roomTypeCode = !string.IsNullOrWhiteSpace(roomList.roomTypeCode) ?
                        roomList.roomTypeCode : roomList.jacTravelPropertyRoomTypeID == "0" ? roomList.jacTravelBookingToken : roomList.jacTravelPropertyRoomTypeID;

                    if (_castRateInf.Count > 0)
                    {
                        for (int i = 0; i < roomList.RateInfos.Length; i++)
                        {
                            roomList.RateInfos[i].chargeableRateInfo = _castRateInf[i].chargeableRateInfo;
                        }
                    }
                }
            }

            //check if any HotelBundleTicket and take out the description
            DateTime arrivalDate = DateTime.ParseExact(room.CheckIn, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            DateTime departureDate = DateTime.ParseExact(room.CheckOut, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            var hotelBundleTicket = _db.HotelBundleTicketSets.FirstOrDefault(x => x.isActive == true && x.HotelID == room.HID && ((x.TravelStart <= arrivalDate && x.TravelEnd >= arrivalDate) || (x.TravelStart <= departureDate && x.TravelEnd >= departureDate)));
            if (hotelBundleTicket != null)
            {
                respond._HotelBundleTicketDesc = "* " + hotelBundleTicket.Description + " -- " + hotelBundleTicket.TimeSlotDesc;
            }

            ViewBag.AsIncludedTax = _SearchHotelModel?.DisplayHotelSetting?.AsIncludedTax ?? false;
            ViewBag.AsAllNight = _SearchHotelModel?.DisplayHotelSetting?.AsAllNight ?? false;
            ViewBag.IsFixedPrice = _SearchHotelModel.IsFixedPrice;
            ViewBag.NoOfPax = _SearchHotelModel.NoOfAdult + _SearchHotelModel.NoOfInfant;
            ViewBag.FixedPriceFrom = _SearchHotelModel.FixedPriceFrom;
            ViewBag.lowRate = room.lowRate.ToDecimal();
            ViewBag.IsDynamic = _SearchHotelModel.IsDynamic;

            return PartialView("~/Views/Hotel/v2/_RoomList.cshtml", respond);
        }

        private SearchRoomModel InitRoomSearchModelSession(Search.Hotel.Room room, string tripid)
        {
            room.InitESBRoomRequest(true);

            SearchRoomModel _searchRoomModel = new SearchRoomModel
            {
                ArrivalDate = room.ESBRoomRequest.ArrivalDate,
                DepartureDate = room.ESBRoomRequest.DepartureDate,
                CurrencyCode = room.ESBRoomRequest.CurrencyCode,
                CustomerUserAgent = Request.UserAgent,
                CustomerIpAddress = GetUserIP(),
                CustomerSessionId = tripid,
                SelectedNoOfRoomType = room?.ESBRoomRequest?.NumberOfRoom?.Length ?? 1,
                HotelID = room?.ESBRoomRequest?.HotelID,
            };

            Core.SetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + room.ESBRoomRequest.HotelID, _searchRoomModel);

            return _searchRoomModel;
        }

        [HttpGet]
        [SessionFilter(SessionName = "HotelList")]
        public async Task<ActionResult> GetRoomInfo(Search.Hotel.Room roomReq, string data, bool cs = false)
        {
            //tripid = cs ? tripid : null;
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
                    model.CustomerSessionId = Guid.NewGuid().ToString();

                    if (User.Identity.IsAuthenticated)
                    {
                        var user = Alphareds.Module.Common.Core.GetUserInfo(CurrentUserID.ToString());
                        model2.UserLoginID = user.UserLoginID;
                    }
                    #region 2/8/17 ZY - tracking cookie for hotel viewed
                    var hotelviewed = model2.Result.HotelList.FirstOrDefault(x => x.hotelId == roomReq.HID);
                    if (hotelviewed != null)
                    {
                        HotelViewedCookie Hotelviewedcookie = new HotelViewedCookie
                        {
                            HotelID = hotelviewed.hotelId,
                            Name = hotelviewed.name,
                            Date = model2.ArrivalDate.ToString("dd-MMM-yyyy") + " - " + model.DepartureDate.ToString("dd-MMM-yyyy"),
                            Image = hotelviewed.imagesURL != null ? hotelviewed.imagesURL.Big_350x350 : "~/Images_hotel/no-img-01.png",
                            lastsearch = DateTime.Now
                        };
                        Trackhotelviewedcookie(Hotelviewedcookie);
                    }
                    #endregion
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
                    roomReq.ESBRoomRequest.CustomerSessionId = tripid;
                    roomReq.ESBRoomRequest.CustomerIpAddress = GetUserIP();
                    roomReq.ESBRoomRequest.UserLoginID = User.Identity.IsAuthenticated ? CustomPrincipal.LoginID : null;

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
                            HotelServiceController.ProcessDiscountCalculation(model.Result.HotelRoomInformationList, promoCodeRule, model2.Destination, model2.NoOfRoom, model2);
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
                return PartialView("_ShowMoreRoom");
            else
                return PartialView("_PopupRoomInfo");
        }

        [HttpGet]
        //[SessionFilter(SessionName = "HotelList")] // If check session will caused last step OrderHistory throw session expired.
        public ActionResult GetHotelInfo(string data, string sr, string tripid)
        {
            try
            {
                SearchHotelModel searchHotelInfoModel = new SearchHotelModel();
                string hotelsupplier = "";
                string supplierCode = General.CustomizeBaseEncoding.DeCodeBase64(sr ?? "");
                ViewBag.HotelSupplierCode = supplierCode;

                // Attempt get searchModel from session (usage on Step 2, 3, 4)
                SearchHotelModel searchModel = Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null ?
                    (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid) : new SearchHotelModel();

                //if (searchModel.ValidBundle())
                //    ViewBag.Bundle = "1";

                // Initialize get hotel information model
                GetHotelInformationModel model = new GetHotelInformationModel()
                {
                    HotelID = supplierCode == "TP" && data.Length >= 6 ? data.Remove(data.Length - 6, 6) + "??????" : data,
                    CurrencyCode = searchModel.Result != null ? searchModel.CurrencyCode : "MYR", // Hardcode CurrencyCode for OrderHistory Page usage
                    CustomerUserAgent = Request.UserAgent,
                    CustomerIpAddress = GetUserIP(),
                    CustomerSessionId = Guid.NewGuid().ToString()
                };

                /*
                 * Step 5 - Usage
                 * If unable get searchModel from session, then initialize dummy get hotellist with greater travel date
                 * For tourplan call GetHotelList only usage for get Policy Details
                */
                #region Step 5
                if (searchModel.Result == null)
                {
                    List<string> hotelIDs = new List<string>();

                    SearchSupplier searchSupplier = string.IsNullOrWhiteSpace(sr) ? null : new SearchSupplier()
                    {
                        Expedia = supplierCode == "EAN",
                        Tourplan = supplierCode == "TP",
                        JacTravel = supplierCode == "JAC",
                        HotelBeds = supplierCode == "HB",
                        ExpediaTAAP = supplierCode == "TAAP",
                        EANRapid = supplierCode == "RAP",
                    };

                    SearchHotelModel dumpSearchModel = InitDummySearchHotelModel(searchSupplier);
                    hotelIDs.Add(model.HotelID);
                    searchHotelInfoModel.Result = ESBHotelServiceCall.GetHotelList(dumpSearchModel, hotelIDs);
                }
                else
                {
                    searchHotelInfoModel = searchModel;
                    searchHotelInfoModel.Result = searchModel.Result;
                }
                #endregion

                if (hotelsupplier == "Expedia" || supplierCode == "EAN")
                {
                    model.Result = ExpediaHotelsServiceCall.GetHotelInformation(model);
                    ViewData.Add("HOTELINFO", model.Result);
                }
                else if (hotelsupplier == "Tourplan" || supplierCode == "TP")
                {
                    model.ResultTP = TourplanServiceCall.GetHotelList(model);

                    ViewData.Add("HOTELINFOTP", model.ResultTP);
                    ViewData.Add("HOTELINFOTP2", searchHotelInfoModel.Result);
                }
                else if (hotelsupplier == "JacTravel" || supplierCode == "JAC")
                {
                    model.JacTravelPropertyID = searchHotelInfoModel.Result.HotelList.FirstOrDefault(x => x.hotelId == model.HotelID).jacTravelPropertyID;
                    model.ResultJT = JacTravelServiceCall.GetPropertyDetails(model);

                    ViewData.Add("HOTELINFOJT", model.ResultJT);
                    ViewData.Add("HOTELINFOJT2", searchHotelInfoModel.Result);
                }
                else if (hotelsupplier == "HotelBeds" || supplierCode == "HB")
                {
                    model.ResultHB = HotelBedServiceCall.HotelInfo(model);
                    ViewData.Add("HOTELINFOHB", model.ResultHB);
                    ViewData.Add("HOTELINFOHB2", searchHotelInfoModel.Result);
                }
                else if (hotelsupplier == "ExpediaTAAP" || supplierCode == "TAAP")
                {
                    model.ResultET = ExpediaTAAPHotelsServiceCall.GetHotelList(searchHotelInfoModel, Convert.ToInt32(model.HotelID));
                    ViewData.Add("HOTELINFOTAAP", model.ResultET);
                }
                #region 2017/02/22 - David added portion, store in session for invoking GTM.
                if (model.Result != null || model.ResultTP != null || model.ResultJT != null || model.ResultHB != null)
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
                    if (filterResult != null)
                    {
                        if (filterResult.Result == null) { FilterName = "All Hotels"; }
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
                    }

                    #endregion

                    #region Selected hotel position
                    int positionCounter = 1;
                    string RoomTypeName = "", AveragePrice = "";
                    if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
                    {
                        SearchHotelModel searchHotelModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                        foreach (Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation hotelInfo in searchHotelModel.Result.HotelList)
                        {
                            if (hotelInfo.hotelId.Equals(model.HotelID))
                            {
                                RoomTypeName = hotelInfo.RoomRateDetailsList.First().roomDescription;
                                AveragePrice = hotelInfo.RoomRateDetailsList.First().RateInfos.First().chargeableRateInfo.averageRate;
                                break;
                            }
                            positionCounter++;
                        }
                    }
                    #endregion

                    Alphareds.Module.Model.GTM_HotelProductListModel GTMList = new GTM_HotelProductListModel
                    {
                        name = "Hotel - " + (model.Result != null ? model.Result.hotelInformation.hotelSummary.city : (model.ResultJT != null ? model.ResultJT.TownCity : (model.ResultTP != null ? model.ResultTP.OptionInfoReply.Option.FirstOrDefault().OptGeneral.Address4 : model.ResultHB.Hotel.City))),
                        id = (model.Result != null ? model.Result.hotelInformation.hotelSummary.hotelId : (model.ResultJT != null ? model.ResultJT.PropertyID : (model.ResultTP != null ? model.ResultTP.OptionInfoReply.Option.FirstOrDefault().Opt : model.ResultHB.Hotel.Code))),
                        price = AveragePrice,
                        hotelName = (model.Result != null ? model.Result.hotelInformation.hotelSummary.name : (model.ResultJT != null ? model.ResultJT.PropertyName : (model.ResultTP != null ? model.ResultTP.OptionInfoReply.Option.FirstOrDefault().OptGeneral.SupplierName : model.ResultHB.Hotel.Name))),
                        roomType = RoomTypeName,
                        list = FilterName + " - " + SortingName,
                        position = (positionCounter).ToString()
                    };
                    Core.SetSession(Enumeration.SessionName.GTM_trackHotelDetail, tripid, JsonConvert.SerializeObject(GTMList));
                }
                #endregion

            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error on ReserveRoom(FormCollection collection)");
                return RedirectToAction("Search", "Hotel", new { reference = "error", tripid });
            }

            return PartialView("_HotelInformation");
        }

        [HttpGet]
        public ActionResult GetLocationOnMap(string name, string city)
        {
            string destination = "";
            if (!string.IsNullOrEmpty(name))
                destination += name;

            if (!string.IsNullOrEmpty(city))
                destination += (destination.Length > 0 ? "," + city : city);

            ViewData.Add("MAP", ExpediaHotelsServiceCall.GetGoogleMapURL(System.Configuration.ConfigurationManager.AppSettings.Get("GoogleMapID"), Server.UrlEncode(destination)));
            return PartialView("~/Views/Hotel/_GoogleMap.cshtml");
        }

        [HttpGet]
        public ActionResult GetHotelInfo_v2(string id, string sr, string tripid, bool isFlightPage = false)
        {
            Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.GetHotelInformationResponse informationResponse = null;
            SearchHotelModel SearchModel = null;
            try
            {
                string decSupplier = "";
                Cryptography.AES.TryDecrypt(sr, out decSupplier);
                HotelSupplier supplierCode = HotelSupplier.Expedia;
                Enum.TryParse(decSupplier, out supplierCode);

                if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
                {
                    SearchModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                }

                var model = new GetHotelInformationModel
                {
                    CurrencyCode = "MYR",
                    CustomerIpAddress = GetUserIP(),
                    CustomerSessionId = tripid,
                    HotelID = id,
                    JacTravelPropertyID = id,
                    CustomerUserAgent = Request.UserAgent,
                };

                if (supplierCode == HotelSupplier.Expedia)
                {
                    informationResponse = ExpediaHotelsServiceCall.GetHotelInformation(model);
                }
                else if (supplierCode == HotelSupplier.Tourplan)
                {
                    var _res = TourplanServiceCall.GetHotelList(model);
                    var _res2 = SearchModel?.Result?.HotelList?.FirstOrDefault(x => x.hotelId.StartsWith(model.HotelID) && x.hotelSupplier == HotelSupplier.Tourplan);

                    var noteList = _res.OptionInfoReply.Option
                        .Where(x => x.OptionNotes?.OptionNote.Length > 0)
                        .SelectMany(x => x.OptionNotes.OptionNote);
                    string _knowBeforeYouGoDescription = null;

                    foreach (var item in noteList)
                    {
                        _knowBeforeYouGoDescription += $"<b>{(item.NoteCategory ?? "-")}</b><br />" +
                            $"{(item.NoteText ?? "-")}<br /><i><small>Last Update: {(item.LastUpdate ?? "-")}</small></i><br />";
                    }

                    informationResponse = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.GetHotelInformationResponse
                    {
                        cacheKey = _res.cacheKey,
                        cacheLocation = _res.cacheLocation,
                        customerSessionId = _res.customerSessionId,
                        hotelInformation = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelBiodata
                        {
                            hotelDetails = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelDetails
                            {
                                knowBeforeYouGoDescription = _knowBeforeYouGoDescription,
                                checkInTime = _res2?.hotelDetail?.checkInTime,
                                checkOutTime = _res2?.hotelDetail?.checkOutTime,
                                locationDescription = _res2?.hotelDetail?.locationDescription,
                                hotelPolicy = _res2?.hotelDetail?.hotelPolicy
                            },
                            HotelImages = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelImage[]
                            {
                            },
                            hotelSummary = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelSummary
                            {
                                address1 = _res2?.Addresses?.address1,
                                address2 = _res2?.Addresses?.address2,
                                address3 = _res2?.Addresses?.address3,
                                postalCode = _res2?.Addresses?.postalCode,
                            },
                            PropertyAmenities = _res.OptionInfoReply?.Option?.Length > 0 ? _res.OptionInfoReply.Option.Where(x => x.Amenities?.Amenity?.Length > 0)
                                .SelectMany(x => x.Amenities.Amenity).Select(x => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.Amenity
                                {
                                    amenity = x.AmenityDescription,
                                    amenityId = x.AmenityCode,
                                })?.ToArray() : null,
                        }
                    };
                }
                else if (supplierCode == HotelSupplier.JacTravel)
                {
                    var _res = JacTravelServiceCall.GetPropertyDetails(model);
                    var _res2 = SearchModel?.Result?.HotelList?.FirstOrDefault(x => x.hotelId == model.HotelID && x.hotelSupplier == HotelSupplier.JacTravel);

                    informationResponse = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.GetHotelInformationResponse
                    {
                        cacheKey = _res.cacheKey,
                        cacheLocation = _res.cacheLocation,
                        customerSessionId = _res.customerSessionId,
                        hotelInformation = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelBiodata
                        {
                            hotelSummary = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelSummary
                            {
                                address1 = _res2?.Addresses?.address1,
                                address2 = _res2?.Addresses?.address2,
                                address3 = _res2?.Addresses?.address3,
                                postalCode = _res2?.Addresses?.postalCode,
                                city = _res2?.Addresses?.city
                            },
                            hotelDetails = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelDetails
                            {
                                propertyDescription = _res.Description ?? _res2?.shortDescription,
                                hotelPolicy = string.Join(" ", _res2?.errata?.Select(x => x.Description)),
                            },
                            HotelImages = _res.Images?.Image?.Length > 0 ? _res.Images.Image.Select(x => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelImage
                            {
                                TypesOfImage = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.ImagesURL
                                {
                                    Big_350x350 = x.ImageLink
                                }
                            }).ToArray() : null,
                            PropertyAmenities = _res.Facilities?.Facility?.Length > 0 ? _res.Facilities.Facility.Select(x => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.Amenity
                            {
                                amenity = x.FacilityDesc,
                                amenityId = x.FacilityID
                            }).ToArray() : null,
                            RoomTypeList = _res.PropertyRoomTypes?.PropertyRoomType?.Length > 0 ? _res.PropertyRoomTypes.PropertyRoomType.Select(x => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.RoomType
                            {
                                description = x.RoomType,
                                descriptionLong = x.RoomType,
                                roomTypeId = x.PropertyRoomTypeID,
                                roomAmenities = x.Facilities?.Facility.Length > 0 ? x.Facilities.Facility.Select(a => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.Amenity
                                {
                                    amenity = a.FacilityDesc,
                                    amenityId = a.FacilityID,
                                }).ToArray() : null,
                            }).ToArray() : null,
                        },
                    };
                }
                else if (supplierCode == HotelSupplier.HotelBeds)
                {
                    var _res = HotelBedServiceCall.HotelInfo(model);

                    #region Address Merge
                    List<string> addressList = new List<string>();
                    if (_res.Hotel?.Address != null)
                    {
                        string _preProcess = _res.Hotel.Address.Trim();
                        if (_preProcess.LastIndexOf(",") != -1)
                        {
                            addressList.Add(_preProcess.Substring(0, _preProcess.Length - 1));
                        }
                        else
                        {
                            addressList.Add(_preProcess);
                        }
                    }

                    if (_res.Hotel?.PostalCode != null)
                    {
                        addressList.Add(_res.Hotel.PostalCode);
                    }

                    if (_res.Hotel?.City != null)
                    {
                        addressList.Add(_res.Hotel.City);
                    }

                    if (_res.Hotel?.Country?.Description != null)
                    {
                        addressList.Add(_res.Hotel.Country.Description);
                    }
                    #endregion

                    informationResponse = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.GetHotelInformationResponse
                    {
                        cacheKey = _res.cacheKey,
                        cacheLocation = _res.cacheLocation,
                        customerSessionId = _res.customerSessionId,
                        hotelInformation = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelBiodata
                        {
                            hotelSummary = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelSummary
                            {
                                address1 = string.Join(", ", addressList),
                                name = _res.Hotel?.Name
                            },
                            hotelDetails = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelDetails
                            {
                                propertyDescription = _res.Hotel?.Description,
                            },
                            HotelImages = _res.Hotel?.Images?.Image?.Length > 0 ? _res.Hotel.Images.Image.Select(x => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelImage
                            {
                                TypesOfImage = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.ImagesURL
                                {
                                    Big_350x350 = $"http://photos.hotelbeds.com/giata/{x.Path}",
                                }
                            }).ToArray() : null,
                            PropertyAmenities = _res.Hotel?.Facilities?.Facility?.Length > 0 ? _res.Hotel.Facilities.Facility.Select(x => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.Amenity
                            {
                                amenity = x.Description,
                                amenityId = x.FacilityCode
                            }).ToArray() : null,
                            RoomTypeList = _res.Hotel?.Rooms?.Room?.Length > 0 ? _res.Hotel.Rooms.Room.Select(x => new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.RoomType
                            {
                                description = x.Description,
                                descriptionLong = x.Description,
                                roomAmenities = x.RoomFacilities?.RoomFacility?.Description?.Length > 0 ? new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.Amenity[] {
                                    new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.Amenity
                                    {
                                        amenity = x.RoomFacilities.RoomFacility.Description,
                                    }
                                } : null,
                            })?.ToArray() : null,
                        },
                    };
                }
                else if (supplierCode == HotelSupplier.ExpediaTAAP)
                {
                    //var _res = ExpediaTAAPHotelsServiceCall.GetHotelList(searchHotelInfoModel, Convert.ToInt32(model.HotelID));
                    //informationResponse = new Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.GetHotelInformationResponse
                    //{

                    //};
                }
                else if (supplierCode == HotelSupplier.EANRapid)
                {
                    var clientResult = EANRapidHotelServiceCall.GetPropertyContent(GetUserIP(), Request.UserAgent, tripid, id);
                    var _res = clientResult.result.FirstOrDefault().Value ?? new Alphareds.Module.EANRapidHotels.RapidServices.Result { };
                    informationResponse = _res.ToExpediaHotelContent(tripid);
                }
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error on ReserveRoom(FormCollection collection)");
                return PartialView("~/Views/Hotel/v2/_PopHotelInfo.cshtml", informationResponse);
            }

            return isFlightPage ? PartialView("~/Views/Flight/Search/_PopHotelInfo.cshtml", informationResponse) : PartialView("~/Views/Hotel/v2/_PopHotelInfo.cshtml", informationResponse);
        }

        public ActionResult GetRoomInfo_v2()
        {
            return View();
        }

        [HttpGet]
        public ActionResult GetLocationOnMap_v2(string name, string city)
        {
            string destination = "";
            if (!string.IsNullOrEmpty(name))
                destination += name;

            if (!string.IsNullOrEmpty(city))
                destination += (destination.Length > 0 ? "," + city : city);

            ViewData.Add("MAP", ExpediaHotelsServiceCall.GetGoogleMapURL(System.Configuration.ConfigurationManager.AppSettings.Get("GoogleMapID"), Server.UrlEncode(destination)));
            return PartialView("~/Views/Hotel/v2/_GoogleMap.cshtml");
        }

        public ActionResult GetTripAdvisorReview(string lid, string lat, string lon, string sr, string id)
        {
            ViewBag.HotelID = id;
            ViewBag.LocationID = lid;
            ViewBag.Latitude = lat;
            ViewBag.Longtitude = lon;

            string decSupplier = "";
            Cryptography.AES.TryDecrypt(sr, out decSupplier);
            HotelSupplier supplierCode = HotelSupplier.Expedia;
            Enum.TryParse(decSupplier, out supplierCode);

            return PartialView("~/Views/Hotel/v2/TripAdvisor/_ReviewsBox.cshtml");
        }
        public ActionResult GetTripAdvisorNearby(string lid, string lat, string lon, string sr, string id)
        {
            ViewBag.HotelID = id;
            ViewBag.LocationID = lid;
            ViewBag.Latitude = lat;
            ViewBag.Longtitude = lon;

            string decSupplier = "";
            Cryptography.AES.TryDecrypt(sr, out decSupplier);
            HotelSupplier supplierCode = HotelSupplier.Expedia;
            Enum.TryParse(decSupplier, out supplierCode);

            return PartialView("~/Views/Hotel/v2/TripAdvisor/_NearbyBox.cshtml");
        }

        private T GetTripAdvisorLocationID<T>(string lat, string lon)
        {
            return SendTripAdvisorGETRequest<T>(lat, lon);
        }

        private T SendTripAdvisorGETRequest<T>(string lat, string lon)
        {
            using (var client = new HttpClient())
            {
                string _url = $"http://api.tripadvisor.com/api/partner/2.0/map/{lat},{lon}";

                var builder = new UriBuilder(_url);

                var query = HttpUtility.ParseQueryString(builder.Query);
                query.Add("key", "e136cea4-485b-4cdd-a429-dbbb96671d33");

                builder.Query = query.ToString();
                string url = builder.ToString();

                client.BaseAddress = new Uri(url);
                //HTTP GET
                var responseTask = client.GetAsync(url);
                responseTask.Wait();

                var result = responseTask.Result;
                if (result.IsSuccessStatusCode)
                {
                    var readTask = result.Content.ReadAsStringAsync();
                    readTask.Wait();

                    try
                    {
                        return JsonConvert.DeserializeObject<T>(readTask.Result);
                    }
                    catch
                    {
                        return default(T);
                    }
                }
                else //web api sent error response 
                {
                    //log response status here..
                    return default(T);
                }
            }

        }
        #endregion

        #region Auto-complete for hotel
        [Filters.LocalhostFilter]
        [HttpGet]
        public string getDestination(string data)
        {
            string text = "";

            object obj = System.Web.HttpContext.Current.Cache.Get(Enumeration.SessionName.AutoCompleteHotel.ToString());
            List<AutoCompleteModel> Destinations = new List<AutoCompleteModel>();
            if (obj == null)
            {
                Alphareds.Module.HotelController.HotelServiceController.GetHotelDestinationList(ref Destinations);
                if (Destinations.Count > 0)
                {
                    System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString(),
                                                                Destinations);
                }
            }
            else
            {
                Destinations = (List<AutoCompleteModel>)obj;
            }

            List<AutoCompleteModel> list = Destinations.Where(x => x.label.ToLower().StartsWith(data.ToLower())).OrderBy(r => r.label).Take(30).ToList();
            text = JsonConvert.SerializeObject(list);
            return text;
        }

        [HttpPost]
        public ActionResult getDestination(string data, string keywords, bool advancedSearch = false)
        {
            string[] splitKeywordsText;
            object obj = !advancedSearch ? System.Web.HttpContext.Current.Cache.Get(Enumeration.SessionName.AutoCompleteHotel.ToString()) :
                System.Web.HttpContext.Current.Cache.Get(Enumeration.SessionName.AutoCompleteHotelStaying.ToString());
            List<EnhancedAutoCompleteHotelModel> destinations = new List<EnhancedAutoCompleteHotelModel>();
            if (obj == null)
            {
                destinations = !advancedSearch ? Alphareds.Module.HotelController.HotelServiceController.GetGoingToList() :
                    Alphareds.Module.HotelController.HotelServiceController.GetStayingAtList();

                if (destinations.Count > 0)
                {
                    System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString(),
                                                                destinations);
                }
            }
            else
            {
                var myobj = ((List<EnhancedAutoCompleteHotelModel>)obj).AsQueryable();
                bool emptyData = string.IsNullOrWhiteSpace(data);
                bool emptyKeywords = string.IsNullOrWhiteSpace(keywords);

                if (emptyData && !advancedSearch)
                {
                    //myobj = myobj.Where(x => x.type == "City" || x.type == "Country" || x.type == "Neighborhood");
                    myobj = myobj.OrderByDescending(x => Guid.NewGuid()).Take(myobj.Count() >= 10 ? 10 : myobj.Count());
                }
                else if (!emptyData && emptyKeywords && !advancedSearch)
                {
                    //myobj = myobj.Where(x => x.type == "City" || x.type == "Country" || x.type == "Neighborhood");
                    myobj = myobj.Where(x => x.label.ToLower().Contains(data.ToLower()));
                    string[] splitDataText = data.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    //myobj = myobj.Where(x => x.type == "Hotel" || x.type == "Point of Interest");
                    myobj = myobj.Where(x => x.label.ToLower().Contains(data.ToLower()) && x.label.ToLower().Contains(keywords.ToLower()));
                    splitKeywordsText = keywords.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                }


                #region Prepare for ignore word sequence search
                //var testing = from item in myobj
                //              let w = item.label.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                //              //where w.Distinct().Intersect(splitDataText).Count() == splitDataText.Count() || 
                //              //where w.Contains(data)
                //              //where w.Any(x => splitDataText.Any(y => y.Contains(x))) || 
                //              where splitDataText.Any(x => w.Any(y => y.Contains(x)))
                //              select item;

                //myobj = myobj.Where(key => splitDataText.Any(x => key.label.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Any(y => y.Contains(x))));
                //myobj = myobj.OrderByDescending(x => x.label.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct().Intersect(splitDataText).Count() <= splitDataText.Count());
                #endregion

                int resCounter = data.Length < 5 ? 10 : 50;
                myobj = myobj.OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.type == "Country").ThenByDescending(x => x.type == "City").ThenByDescending(x => x.type == "Neighborhood")
                    .ThenByDescending(x => !emptyKeywords && x.label.ToLower().StartsWith(keywords.ToLower()))
                    .ThenByDescending(x => !emptyData && x.label.ToLower().StartsWith(data.ToLower()))
                    .ThenBy(x => x.label);
                myobj = myobj.Take(resCounter);
                myobj = myobj.GroupBy(x => x.label).Select(x => new EnhancedAutoCompleteHotelModel
                {
                    label = x.Key,
                    city = x.FirstOrDefault().city,
                    country = x.FirstOrDefault().country,
                    type = x.FirstOrDefault().type
                });
                destinations = myobj.ToList();
            }
            return Json(destinations);
        }

        [HttpPost]
        public ActionResult GetDestination_v2(string data, string keywords, string stype = "Country", bool advancedSearch = false)
        {
            string[] splitKeywordsText;
            object obj = !advancedSearch ? System.Web.HttpContext.Current.Cache.Get(Enumeration.SessionName.AutoCompleteHotel.ToString()) :
                System.Web.HttpContext.Current.Cache.Get(Enumeration.SessionName.AutoCompleteHotelStaying.ToString());
            List<EnhancedAutoCompleteHotelModel> destinations = new List<EnhancedAutoCompleteHotelModel>();
            if (obj == null)
            {
                destinations = !advancedSearch ? Alphareds.Module.HotelController.HotelServiceController.GetGoingToList() :
                    Alphareds.Module.HotelController.HotelServiceController.GetStayingAtList();

                if (destinations.Count > 0)
                {
                    System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString(),
                                                                destinations);
                }
            }
            else
            {
                var myobj = ((List<EnhancedAutoCompleteHotelModel>)obj).AsQueryable();
                bool emptyData = string.IsNullOrWhiteSpace(data);
                bool emptyKeywords = string.IsNullOrWhiteSpace(keywords);

                if (emptyData && !advancedSearch)
                {
                    //myobj = myobj.Where(x => x.type == "City" || x.type == "Country" || x.type == "Neighborhood");
                    myobj = myobj.OrderByDescending(x => Guid.NewGuid()).Take(myobj.Count() >= 10 ? 10 : myobj.Count());
                }
                else if (!emptyData && emptyKeywords && !advancedSearch)
                {
                    //myobj = myobj.Where(x => x.type == "City" || x.type == "Country" || x.type == "Neighborhood");
                    myobj = myobj.Where(x => x.label.ToLower().Contains(data.ToLower()));
                    string[] splitDataText = data.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    //myobj = myobj.Where(x => x.type == "Hotel" || x.type == "Point of Interest");
                    myobj = myobj.Where(x => x.label.ToLower().Contains(data.ToLower()) && x.label.ToLower().Contains(keywords.ToLower()));
                    splitKeywordsText = keywords.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                }


                #region Prepare for ignore word sequence search
                //var testing = from item in myobj
                //              let w = item.label.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                //              //where w.Distinct().Intersect(splitDataText).Count() == splitDataText.Count() || 
                //              //where w.Contains(data)
                //              //where w.Any(x => splitDataText.Any(y => y.Contains(x))) || 
                //              where splitDataText.Any(x => w.Any(y => y.Contains(x)))
                //              select item;

                //myobj = myobj.Where(key => splitDataText.Any(x => key.label.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Any(y => y.Contains(x))));
                //myobj = myobj.OrderByDescending(x => x.label.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct().Intersect(splitDataText).Count() <= splitDataText.Count());
                #endregion

                int resCounter = data.Length < 5 ? 10 : 50;
                myobj = myobj.OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.type == "Country").ThenByDescending(x => x.type == "City").ThenByDescending(x => x.type == "Neighborhood")
                    .ThenByDescending(x => !emptyKeywords && x.label.ToLower().StartsWith(keywords.ToLower()))
                    .ThenByDescending(x => !emptyData && x.label.ToLower().StartsWith(data.ToLower()))
                    .ThenBy(x => x.label);
                myobj = myobj.Take(resCounter);


                var pushObj = new ExpandoObject() as IDictionary<string, Object>;
                var _pre_Output = myobj.GroupBy(x => x.type);

                foreach (var item in _pre_Output)
                {
                    var _grp = item.Select(x => x.label).GroupBy(x => x).Select(x => x.Key);
                    pushObj.Add(item.Key, _grp);
                }

                var output_Data = pushObj.ToDictionary(item => item.Key, item => item.Value);

                var _obj = new
                {
                    status = true,
                    error = (string)null,
                    data = output_Data
                };

                return Json(_obj);
            }

            return Json(destinations);
        }

        [HttpPost]
        public ActionResult GetFltDestination_v2(string data)
        {
            object obj = System.Web.HttpContext.Current.Cache.Get(Enumeration.SessionName.AutoCompleteFlight.ToString());
            List<EnhancedAutoCompleteHotelModel> destinations = new List<EnhancedAutoCompleteHotelModel>();
            if (obj == null)
            {
                destinations = Alphareds.Module.HotelController.HotelServiceController.GetFlightToList();

                if (destinations.Count > 0)
                {
                    System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteFlight.ToString(), destinations);
                }
            }
            else
            {
                var myobj = ((List<EnhancedAutoCompleteHotelModel>)obj).AsQueryable();
                bool emptyData = string.IsNullOrWhiteSpace(data);

                if (emptyData)
                {
                    myobj = myobj.OrderByDescending(x => Guid.NewGuid()).Take(myobj.Count() >= 10 ? 10 : myobj.Count());
                }
                else
                {
                    myobj = myobj.Where(x => x.label.ToLower().Contains(data.ToLower()));
                }
                int resCounter = data.Length < 5 ? 10 : 50;
                myobj = myobj.OrderByDescending(x => !emptyData && x.label.ToLower().StartsWith(data.ToLower()))
                    .ThenBy(x => x.label);
                myobj = myobj.Take(resCounter);


                var pushObj = new ExpandoObject() as IDictionary<string, Object>;

                var _pre_Output = myobj.GroupBy(x => x.type);

                foreach (var item in _pre_Output)
                {
                    var _grp = item.Select(x => x.label).GroupBy(x => x).Select(x => x.Key);
                    pushObj.Add(item.Key, _grp);
                }

                var output_Data = pushObj.ToDictionary(item => item.Key, item => item.Value);

                var _obj = new
                {
                    status = true,
                    error = (string)null,
                    data = output_Data
                };

                return Json(_obj);
            }

            return Json(destinations);
        }

        public ActionResult GetHotel_Country()
        {
            StartPoint:
            object obj = System.Web.HttpContext.Current.Cache.Get(Enumeration.SessionName.AutoCompleteHotel.ToString());

            if (obj == null)
            {
                List<EnhancedAutoCompleteHotelModel> _list = new List<EnhancedAutoCompleteHotelModel>();
                _list = Alphareds.Module.HotelController.HotelServiceController.GetGoingToList();

                if (_list.Count > 0)
                {
                    System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString(), _list);
                    goto StartPoint;
                }
            }
            else
            {
                var myobj = ((List<EnhancedAutoCompleteHotelModel>)obj).AsQueryable();

                myobj = myobj.OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.type == "Country").ThenByDescending(x => x.type == "City").ThenByDescending(x => x.type == "Neighborhood")
                    .ThenBy(x => x.label);

                var _output = myobj.GroupBy(x => x.country)
                    .OrderBy(x => x.Min(s => s.SortOrder))
                    .Select(x => x.Key);

                var _obj = new
                {
                    status = true,
                    error = (string)null,
                    data = new
                    {
                        Country = _output
                    }
                };

                return Json(_obj);
            }

            return Json(new List<string>());
        }


        [HttpGet]
        public string GetPropertyName(string data, string tripid)
        {
            string text = "";
            bool emptyData = string.IsNullOrWhiteSpace(data);
            List<string> Properties = new List<string>();
            if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
            {
                Alphareds.Module.Model.SearchHotelModel searchHotelModel = (Alphareds.Module.Model.SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                if (searchHotelModel.Result != null)
                {
                    if (searchHotelModel.Result.HotelList != null && searchHotelModel.Result.HotelList.Count() > 0)
                    {
                        var Hotels = searchHotelModel.Result.HotelList.Where(x => x.name.ToLower().Contains(data.ToLower()));
                        if (emptyData)
                        {
                            Hotels = searchHotelModel.Result.HotelList.OrderByDescending(x => Guid.NewGuid()).Take(Hotels.Count() >= 20 ? 20 : Hotels.Count());
                        }
                        if (Hotels != null && Hotels.Count() > 0)
                        {
                            List<AutoCompleteModel> list = new List<AutoCompleteModel>();
                            foreach (var hotel in Hotels.GroupBy(x => x.name))
                            {
                                list.Add(new AutoCompleteModel { label = hotel.Key });
                            }
                            text = JsonConvert.SerializeObject(list.Distinct());
                        }
                    }
                }
            }

            if (text.Length == 0)
            {
                AutoCompleteModel model = new AutoCompleteModel { label = "No matches found" };
                List<AutoCompleteModel> list = new List<AutoCompleteModel> { model };
                text = JsonConvert.SerializeObject(list);
            }

            return text;
        }

        [HttpGet]
        [SessionFilter(SessionName = "HotelList")]
        public ActionResult GetPropertyName_v2(string data)
        {
            List<string> properties = new List<string>();
            bool emptyData = string.IsNullOrWhiteSpace(data);
            //var pushObj = new ExpandoObject() as IDictionary<string, object>;
            List<IDictionary<string, object>> batchObj = new List<IDictionary<string, object>>();

            if (_SearchHotelModel?.Result?.HotelList != null && _SearchHotelModel.Result.HotelList.Length > 0)
            {
                var hotels = _SearchHotelModel.Result.HotelList
                    .Where(x => x.name.ToLower().Contains(data.ToLower()));

                if (emptyData)
                {
                    hotels = _SearchHotelModel.Result.HotelList.OrderByDescending(x => Guid.NewGuid()).Take(hotels.Count() >= 20 ? 20 : hotels.Count());
                }

                if (hotels != null && hotels.Count() > 0)
                {
                    foreach (var hotelCity in hotels.GroupBy(x => new { x.name }))
                    {
                        var _pushObj = new ExpandoObject() as IDictionary<string, object>;

                        var _grp = hotelCity.Select(x => x.name).Distinct();
                        _pushObj.Add("Name", hotelCity.Key.name);
                        _pushObj.Add("City", hotelCity.FirstOrDefault().Addresses?.city);

                        batchObj.Add(_pushObj.ToDictionary(item => item.Key, item => item.Value));
                    }
                }
            }

            var _obj = new
            {
                status = batchObj.Count > 0,
                error = (string)null,
                data = batchObj
            };

            //var output_Data = pushObj.ToDictionary(item => item.Key, item => item.Value);
            //var _obj = new
            //{
            //    status = pushObj.Count > 0,
            //    error = (string)null,
            //    data = output_Data
            //};

            return Json(batchObj, JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region Save hotel function
        [HttpPost]
        public ActionResult SaveHotel(string data, string tripid, List<RoomSelectedModel> roomSelected, bool cs = false)   //RoomTypeCode=RateCode=TotalRooms|RoomTypeCode=RateCode=TotalRooms
        {
            Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, null);
            string flag = "SAVEFAIL";

            try
            {
                int TotalRoomSelected = roomSelected.Sum(x => x.Qty);
                int TotalRoomSearched = 0;

                if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
                {
                    SearchHotelModel searchHotelModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                    TotalRoomSearched = searchHotelModel.NoOfRoom;

                    if (TotalRoomSelected > TotalRoomSearched)
                    {
                        throw new Exception("EXCEEDSEARCH");
                    }

                    SearchRoomModel roomCache = (SearchRoomModel)Core.GetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + roomSelected.FirstOrDefault().Hotel);
                    var hotelSelected = searchHotelModel.Result.HotelList.FirstOrDefault(x => x.hotelId == roomSelected.FirstOrDefault().Hotel);
                    var roomselect = roomCache.Result.HotelRoomInformationList.FirstOrDefault().roomAvailabilityDetailsList.Where(x => roomSelected.Any(h => h.TypeCode == x.roomTypeCode)).ToList();
                    int userID = CurrentUserID;

                    try
                    {
                        SavedSearch userSearchDtl = new SavedSearch
                        {
                            CreatedByID = userID,
                            CreatedDate = DateTime.Now,
                            IsActive = true,
                            ModifiedByID = userID,
                            ModifiedDate = DateTime.Now,
                            Type = "HTL",
                            UserID = userID
                        };

                        HotelSearch hSearch = new HotelSearch
                        {
                            CheckInDateTime = searchHotelModel.ArrivalDate,
                            CheckOutDateTime = searchHotelModel.DepartureDate,
                            Adult = searchHotelModel.NoOfAdult,
                            Child = searchHotelModel.NoOfInfant,
                            CreatedByID = userID,
                            CreatedDate = DateTime.Now,
                            IsActive = true,
                            ModifiedByID = userID,
                            ModifiedDate = DateTime.Now,
                        };
                        List<HotelSearchDetail> hSearchDtls = new List<HotelSearchDetail>();
                        foreach (var room in roomselect)
                        {
                            var roomqty = roomSelected.FirstOrDefault(x => x.TypeCode == room.roomTypeCode).Qty;
                            hSearchDtls.Add(new HotelSearchDetail
                            {
                                SupplierCode = hotelSelected.hotelSupplierCode,
                                HotelID = hotelSelected.hotelId,
                                HotelAddress = hotelSelected.Addresses.address1 + (!string.IsNullOrEmpty(hotelSelected.Addresses.address2) ? ", " + hotelSelected.Addresses.address2 : ""),
                                HotelCity = searchHotelModel.Destination,
                                CountryCode = hotelSelected.Addresses.countryCode.Length == 2 ? UtilitiesService.ConvertCountryCode(hotelSelected.Addresses.countryCode) : hotelSelected.Addresses.countryCode,
                                HotelName = hotelSelected.name,
                                HotelPostalCode = hotelSelected.Addresses.postalCode,
                                HotelRating = hotelSelected.hotelRating,
                                HotelPrice = Convert.ToDecimal(room.RateInfos.FirstOrDefault()?.chargeableRateInfo.total) * roomqty,
                                RoomTypeDescription = room.description,
                                RoomTypeCode = room.roomTypeCode,
                                NoOfRoom = roomqty,
                                CreatedByID = userID,
                                CreatedDate = DateTime.Now,
                                IsActive = true,
                                ModifiedByID = userID,
                                ModifiedDate = DateTime.Now,
                            });
                        }
                        hSearch.HotelSearchDetails = hSearchDtls;
                        userSearchDtl.HotelSearches.Add(hSearch);
                        SqlCommand command = new SqlCommand();
                        HotelServiceController.InsertHotelSavedSearch(userSearchDtl, command);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    flag = "SAVESUCCESS";
                    return Json(flag, JsonRequestBehavior.DenyGet);
                }
                else { flag = "SAVEFAIL"; }
            }
            catch (Exception ex) { flag = ex.Message; }
            return Json(flag, JsonRequestBehavior.DenyGet);
        }

        [HttpPost]
        public ActionResult GenerateHotelLink(string data, string tripid, List<RoomSelectedModel> roomSelected, bool cs = false)   //RoomTypeCode=RateCode=TotalRooms|RoomTypeCode=RateCode=TotalRooms
        {
            Core.SetSession(Enumeration.SessionName.RoomAvailList, tripid, null);
            string flag = "SAVEFAIL";

            try
            {
                int TotalRoomSelected = roomSelected.Sum(x => x.Qty);
                int TotalRoomSearched = 0;

                if (Core.GetSession(Enumeration.SessionName.HotelList, tripid) != null)
                {
                    SearchHotelModel searchHotelModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);
                    TotalRoomSearched = searchHotelModel.NoOfRoom;

                    if (TotalRoomSelected > TotalRoomSearched)
                    {
                        throw new Exception("EXCEEDSEARCH");
                    }

                    SearchRoomModel roomCache = (SearchRoomModel)Core.GetSession(Enumeration.SessionName.RoomAvail, tripid + "_" + roomSelected.FirstOrDefault().Hotel);
                    var hotelSelected = searchHotelModel.Result.HotelList.FirstOrDefault(x => x.hotelId == roomSelected.FirstOrDefault().Hotel && x.hotelSupplier.ToString() == Cryptography.AES.Decrypt(roomSelected.FirstOrDefault().EncSupp));
                    var roomselect = roomCache.Result.HotelRoomInformationList.FirstOrDefault().roomAvailabilityDetailsList.Where(x => roomSelected.Any(h => h.TypeCode == x.roomTypeCode && h.Name == x.description && (h.RateCode == x.rateCode || x.rateCode == null))).ToList();
                    int userID = CurrentUserID;
                    var link = "";

                    try
                    {
                        HotelLinkSearch hSearch = new HotelLinkSearch
                        {
                            CheckInDateTime = searchHotelModel.ArrivalDate,
                            CheckOutDateTime = searchHotelModel.DepartureDate,
                            Adult = searchHotelModel.NoOfAdult,
                            Child = searchHotelModel.NoOfInfant,
                        };
                        List<HotelLinkSearchDetail> hSearchDtls = new List<HotelLinkSearchDetail>();
                        foreach (var room in roomselect)
                        {
                            var roomqty = roomSelected.FirstOrDefault(x => x.TypeCode == room.roomTypeCode).Qty;
                            hSearchDtls.Add(new HotelLinkSearchDetail
                            {
                                SupplierCode = hotelSelected.hotelSupplierCode,
                                HotelID = hotelSelected.hotelId,
                                HotelCity = searchHotelModel.Destination,
                                HotelName = hotelSelected.name,
                                HotelPrice = Convert.ToDecimal(room.RateInfos.FirstOrDefault()?.chargeableRateInfo.total) * roomqty,
                                RoomTypeDescription = room.description,
                                RoomTypeCode = room.roomTypeCode,
                                RateCode = room.rateCode,
                                NoOfRoom = roomqty,
                            });
                        }
                        hSearch.HotelSearchDetails = hSearchDtls;
                        var JSONString = JsonConvert.SerializeObject(hSearch);
                        link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, String.Empty);
                        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(JSONString);
                        var htlInfo = Convert.ToBase64String(plainTextBytes);
                        link += "/Hotel/HotelLink?HtlLink=" + htlInfo;
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    flag = "SAVESUCCESS";
                    return Json(new { flag = flag, link = link }, JsonRequestBehavior.DenyGet);
                }
                else { flag = "SAVEFAIL"; }
            }
            catch (Exception ex) { flag = ex.Message; }
            return Json(new { flag = flag }, JsonRequestBehavior.DenyGet);
        }

        public ActionResult HotelLink(string HtlLink)
        {
            MayFlower dbCtx = new MayFlower();

            try
            {
                CheckoutProduct checkoutProduct = new CheckoutProduct();
                string tripid = Guid.NewGuid().ToString();
                var link = Request.QueryString["HtlLink"];
                var HtlBytes = Convert.FromBase64String(link);
                var HtlJson = System.Text.Encoding.UTF8.GetString(HtlBytes);
                var HotelSearches = JsonConvert.DeserializeObject<HotelLinkSearch>(HtlJson);

                var htlSearch = HotelSearches;
                var htlDtl = htlSearch.HotelSearchDetails;
                var supplierCode = htlDtl.FirstOrDefault().SupplierCode;

                SearchHotelModel searchModel = new SearchHotelModel
                {
                    ArrivalDate = htlSearch.CheckInDateTime,
                    DepartureDate = htlSearch.CheckOutDateTime,
                    CurrencyCode = "MYR",
                    Destination = htlDtl.FirstOrDefault().HotelCity,
                    CustomerUserAgent = Request.UserAgent,
                    CustomerIpAddress = Request.UserHostAddress,
                    CustomerSessionId = Guid.NewGuid().ToString(),
                    NoOfAdult = htlSearch.Adult,
                    NoOfInfant = htlSearch.Child,
                    NoOfRoom = htlDtl.FirstOrDefault()?.NoOfRoom ?? 0,
                    SupplierIncluded = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier()
                    {
                        Expedia = supplierCode.Contains("EAN") ? true : false,
                        JacTravel = supplierCode.Contains("JAC") ? true : false,
                        Tourplan = supplierCode.Contains("TP") ? true : false,
                        HotelBeds = supplierCode.Contains("HB") ? true : false,
                        EANRapid = supplierCode.Contains("RAP"),
                    }
                };
                SearchRoomModel searchRoomModel = new SearchRoomModel
                {
                    ArrivalDate = searchModel.ArrivalDate,
                    DepartureDate = searchModel.DepartureDate,
                    CurrencyCode = searchModel.CurrencyCode,
                    CustomerUserAgent = searchModel.CustomerUserAgent,
                    CustomerIpAddress = searchModel.CustomerIpAddress,
                    CustomerSessionId = searchModel.CustomerSessionId,
                    HotelID = htlDtl.FirstOrDefault().HotelID,
                    SelectedNoOfRoomType = searchModel.NoOfRoom,
                };

                List<string> HotelId = new List<string>();
                HotelId.Add(htlDtl.FirstOrDefault().HotelID);
                searchModel.Result = Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelList(searchModel, HotelId);
                searchRoomModel.Result = Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetRoomAvailability(searchRoomModel, searchModel);
                if (searchRoomModel.Result != null && searchRoomModel.Result.Errors == null && searchRoomModel.Result.HotelRoomInformationList.Length > 0)
                {
                    var matchedHotel = searchRoomModel.Result.HotelRoomInformationList.FirstOrDefault().roomAvailabilityDetailsList.Where(x => htlDtl.Any(y => (x.propertyId == y.RoomTypeCode || x.roomTypeCode == y.RoomTypeCode || x.jacTravelBookingToken == y.RoomTypeCode || x.jacTravelPropertyRoomTypeID == y.RoomTypeCode) && y.RoomTypeDescription == x.description && y.RateCode == x.rateCode)).ToList();

                    if (matchedHotel != null && matchedHotel.Count > 0)
                    {
                        List<RoomSelectedModel> roomSelectedList = new List<RoomSelectedModel>();
                        foreach (var room in htlDtl)
                        {
                            int roomqty = room.NoOfRoom;
                            var roomdtl = matchedHotel.FirstOrDefault();
                            if (roomdtl != null)
                            {
                                roomSelectedList.Add(new RoomSelectedModel()
                                {
                                    Hotel = room.HotelID,
                                    Name = room.HotelName,
                                    TypeCode = room.RoomTypeCode,
                                    Qty = roomqty,
                                    PropertyId = roomdtl.propertyId,
                                    RateCode = roomdtl.rateCode,
                                    RateType = roomdtl.RateInfos[0].rateType,
                                    EncSupp = Cryptography.AES.Encrypt(searchModel.Result.HotelList.FirstOrDefault().hotelSupplier.ToString()),
                                });
                            }
                        }

                        Alphareds.Module.HotelController.HotelServiceController.InitializeModel hc = new Alphareds.Module.HotelController.HotelServiceController.InitializeModel(tripid, Request.UserAgent, searchModel.CustomerIpAddress);
                        List<SearchRoomModel> searchRoomModelList = new List<SearchRoomModel> { searchRoomModel };
                        List<GTM_HotelProductModel> _GTM_addToCartList = new List<GTM_HotelProductModel>();
                        List<RoomDetail> _roomDetails = hc.InitializeRoomDetailModel(roomSelectedList, searchRoomModelList, out _GTM_addToCartList);

                        ProductPricingDetail hotelPricingDetail = new ProductPricingDetail
                        {
                            Sequence = 2,
                            Currency = searchRoomModel.CurrencyCode,
                            Items = _roomDetails.GroupBy(x => new { x.RoomTypeCode, x.RoomTypeName, x.TotalBaseRate, x.TotalTaxAndServices, x.TotalGST }).Select(x => new ProductItem
                            {
                                ItemDetail = x.Key.RoomTypeName,
                                ItemQty = x.Count(),
                                BaseRate = x.Key.TotalBaseRate,
                                Surcharge = x.Key.TotalTaxAndServices,
                                Supplier_TotalAmt = x.Sum(s => s.TotalBaseRate_Source) + x.Sum(s => s.TotalTaxAndServices_Source) + x.Sum(s => s.TotalGST_Source),
                                GST = x.Key.TotalGST,
                            }).ToList(),
                        };

                        ProductHotel prdHotel = new ProductHotel
                        {
                            ContactPerson = null,
                            SearchHotelInfo = searchModel,
                            RoomDetails = _roomDetails,
                            RoomAvailabilityResponse = searchRoomModel.Result,
                            SearchRoomList = searchRoomModelList,
                            ProductSeq = 2,
                            PricingDetail = hotelPricingDetail,
                            HotelSelected = searchModel.Result.HotelList.Where(x => _roomDetails.Any(y => y.HotelId == x.hotelId))?.ToList(),
                        };
                        checkoutProduct.IsRegister = false; // reset last option
                        checkoutProduct.ImFlying = false;
                        checkoutProduct.RequireInsurance = false;
                        checkoutProduct.RemoveProduct(ProductTypes.Hotel);
                        checkoutProduct.InsertProduct(prdHotel);

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
                    throw new Exception("Hotel didn't exist");
                }
            }
            catch (Exception ex)
            {
                Logger log = LogManager.GetCurrentClassLogger();
                log.Debug(ex.Message);
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }
        }
        #endregion

        /// <param name="confirmid">SuperPNRNo - Use at AccountCountroller, named as Upcoming Travel/Pass Travel</param>
        public async Task<ActionResult> OrderHistory(string confirmid)
        {
            // 2017/01/24 - Change from BookingID to SuperPNRNo
            //string decodeConfirmid = General.CustomizeBaseEncoding.DeCodeBase64(confirmid);
            if (!isSelfBookingOrGuest(confirmid))
            {
                string statusQuery = Request.QueryString["status"] != null ? Request.QueryString["status"].ToString() : string.Empty;

                if (string.IsNullOrEmpty(statusQuery) || statusQuery != "success")
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            MayFlower db = new MayFlower();

            var hotelBooking = await Task<BookingHotel>.Factory.StartNew(() =>
            {
                return db.BookingHotels.FirstOrDefault(x => x.SuperPNRNo == confirmid);
            });

            bool onlyHotelBooking = hotelBooking != null && hotelBooking.SuperPNR.Bookings.FirstOrDefault() == null;
            Alphareds.Module.Model.Database.Booking flightBooking = hotelBooking.SuperPNR.Bookings.FirstOrDefault();

            return onlyHotelBooking ? (IsUseV2Layout ? View("~/views/checkout/confirmation.cshtml", hotelBooking.SuperPNR) : View(hotelBooking)) :
                (hotelBooking != null && flightBooking != null) ? RedirectToAction("OrderHistory", "Flight", new { BookingId = flightBooking.SuperPNRNo }) :
                (ActionResult)RedirectToAction("NotFound", "Error");
        }

        #region Task Service Call - Get HotelList
        public async Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> getHotelFromEBSSearchModel(SearchHotelModel searchModel)
        {
            // 2017/06/30 - Tourplan doesn't sell as dry sell, as dynamic configurable will change to config base if override turned on.
            var IsAgent = IsAgentUser;
            searchModel.SupplierIncluded = Core.SearchSupplierSetting.IsOverrideHotelSupplier ? new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
            {
                Expedia = IsAgent ? Core.SearchSupplierSetting.B2B.ExpediaEnabled : Core.SearchSupplierSetting.B2C.ExpediaEnabled
                            && (IsAgent ? searchModel.SupplierIncluded.Expedia : true),
                Tourplan = IsAgent ? Core.SearchSupplierSetting.B2B.TourplanEnabled : Core.SearchSupplierSetting.B2C.TourplanEnabled
                            && (IsAgent ? searchModel.SupplierIncluded.Tourplan : true),
                JacTravel = IsAgent ? Core.SearchSupplierSetting.B2B.JacTravelEnabled : Core.SearchSupplierSetting.B2C.JacTravelEnabled
                            && (IsAgent ? searchModel.SupplierIncluded.JacTravel : true),
                HotelBeds = IsAgent ? Core.SearchSupplierSetting.B2B.HotelBedsEnabled : Core.SearchSupplierSetting.B2C.HotelBedsEnabled
                            && (IsAgent ? searchModel.SupplierIncluded.HotelBeds : true),
                ExpediaTAAP = IsAgent ? Core.SearchSupplierSetting.B2B.ExpediaTAAPEnabled : Core.SearchSupplierSetting.B2C.ExpediaTAAPEnabled
                            && (IsAgent ? searchModel.SupplierIncluded.ExpediaTAAP : true),
                EANRapid = IsAgent ? Core.SearchSupplierSetting.B2B.EANRapidEnabled : Core.SearchSupplierSetting.B2C.EANRapidEnabled
                            && (IsAgent ? searchModel.SupplierIncluded.EANRapid : true),
            } : searchModel.SupplierIncluded;

            return await Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
            {
                return ESBHotelServiceCall.GetHotelList(searchModel);
            });
        }

        public async Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> getHotelFromEBSSearchModel(SearchHotelModel searchModel, List<string> hotelId)
        {
            // 2017/06/30 - Tourplan doesn't sell as dry sell, as dynamic configurable will change to config base if override turned on.
            searchModel.SupplierIncluded = Core.SearchSupplierSetting.IsOverrideHotelSupplier ? new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier
            {
                Expedia = CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.ExpediaEnabled : Core.SearchSupplierSetting.B2C.ExpediaEnabled,
                Tourplan = CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.TourplanEnabled : Core.SearchSupplierSetting.B2C.TourplanEnabled,
                JacTravel = CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.JacTravelEnabled : Core.SearchSupplierSetting.B2C.JacTravelEnabled,
                HotelBeds = CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.HotelBedsEnabled : Core.SearchSupplierSetting.B2C.HotelBedsEnabled,
                ExpediaTAAP = CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.ExpediaTAAPEnabled : Core.SearchSupplierSetting.B2C.ExpediaTAAPEnabled,
                EANRapid = CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.EANRapidEnabled : Core.SearchSupplierSetting.B2C.EANRapidEnabled,
            } : searchModel.SupplierIncluded;

            return await Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
            {
                return ESBHotelServiceCall.GetHotelList(searchModel, hotelId);
            });
        }

        private SearchSupplier GetDefaultSearchSupplier(SearchSupplier hijackSearch, bool isB2B)
        {
            bool isOverrideDefaultSearch = Core.SearchSupplierSetting.IsOverrideHotelSupplier;
            var _defaultSetting = new Core.SearchSupplierSetting();
            PropertyInfo[] _configReflect = isB2B ? typeof(Core.SearchSupplierSetting.B2B).GetProperties() : typeof(Core.SearchSupplierSetting.B2C).GetProperties();

            SearchSupplier _supplier = hijackSearch ?? new SearchSupplier();

            var supplierReflect = typeof(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier).GetProperties();

            foreach (var itemRelf in supplierReflect)
            {
                if (itemRelf.PropertyType.Name == "Boolean")
                {
                    var lastValue = (bool)itemRelf.GetValue(_supplier);
                    bool _configForceDisabledSupplier = false;

                    if (isOverrideDefaultSearch)
                    {
                        var _configSetupOverride = _configReflect.FirstOrDefault(x => x.PropertyType.Name == "Boolean" &&
                        itemRelf.Name.ToLower() == x.Name.ToLower().Replace("enabled", ""));
                        if (_configSetupOverride != null)
                        {
                            _configForceDisabledSupplier = !((bool)_configSetupOverride.GetValue(_defaultSetting));
                        }
                    }

                    if (!lastValue && !_configForceDisabledSupplier)
                    {
                        itemRelf.SetValue(_supplier, true);
                    }
                }
            }

            return _supplier;
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
        #endregion

        #region Hotel Service Related Function
        private SearchHotelModel InitDummySearchHotelModel(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier supp = null)
        {
            // Initialize Value here for testing
            SearchHotelModel searchHotelModel = new SearchHotelModel
            {
                ArrivalDate = DateTime.Now.AddDays(15),
                DepartureDate = DateTime.Now.AddDays(16),
                CurrencyCode = "MYR",
                CustomerIpAddress = GetUserIP(),
                CustomerUserAgent = Request.UserAgent,
                CustomerSessionId = new Guid().ToString(),
                NoOfAdult = 1,
                NoOfInfant = 1,
                NoOfRoom = 1,
                Star = 10,

                SupplierIncluded = supp
            };

            return searchHotelModel;
        }
        #endregion

        #region Function & Utilities
        /// <summary>
        /// Distinct room type with lowest rate.
        /// </summary>
        /// <param name="model">Search Room Model</param>
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

        private bool isSelfBookingOrGuest(string superPNRNo)
        {
            MayFlower dbContext = new MayFlower();
            var res = dbContext.BookingHotels.FirstOrDefault(x => x.SuperPNRNo == superPNRNo);
            return res != null ? res.UserID == 0 || res.UserID == CurrentUserID : false;
        }

        private HotelCheckoutModel initHotelCheckoutFromStep2Reserve(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityResponse roomavailabilityresponse, List<SearchRoomModel> searchRoomModelList)
        {
            HotelCheckoutModel step3_checkout = new HotelCheckoutModel
            {
                RoomAvailabilityResponse = roomavailabilityresponse,
                SearchRoomModelList = searchRoomModelList,
            };
            return step3_checkout;
        }

        #region 2017/05/08 - Promo Code Section
        public PromoCodeRule GetPromoCodeDiscountRule(SearchHotelModel searchReq, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation[] hotelList,
            MayFlower dbContext = null, string DPPromoCode = null, string hotelIdSelected = null, string hotelSupplierSelected = null, List<string> airlineCodeList = null, string tripType = null)
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

            #region block promo code
            /*
             * REMARKS: For Payment Apply Code Block
             * TODO: Check Exception Flight/Hotel Supplier / Destination
             * If TRUE - return invalid promo code OR return message promo code not valid on selected blarblarblar
             *          OR return null
             * If FALSE - continue next statement
             * use searchReq to check/ hotel
            */
            //at here should be destination (hotelID also need hotelIdSelected != null then use that)

            var fullPromoExcludeRELists = dbContext.PromoRulesExcludeLists.Where(x => x.isActive == true && x.ProductType == "Hotel" && ((x.TravelFrom <= searchReq.ArrivalDate && x.TravelTo >= searchReq.ArrivalDate) || (x.TravelFrom <= searchReq.DepartureDate && x.TravelTo >= searchReq.DepartureDate)));//update to block the promo if one of the date is between
            var promoCodeBlockByHotelID = fullPromoExcludeRELists.Where(x => x.HotelID != null && x.isActive == true && DateTime.Now > x.StartDate && DateTime.Now < x.EndDate);
            var promoCodeBlockByDest = fullPromoExcludeRELists.Where(x => x.Destination != null && x.isActive == true && DateTime.Now > x.StartDate && DateTime.Now < x.EndDate);
            var promoCodeBlockBySupplier = fullPromoExcludeRELists.Where(x => x.HotelSupplierCode != null && x.isActive == true && DateTime.Now > x.StartDate && DateTime.Now < x.EndDate);

            if (promoCodeBlockByDest.Count() > 0) //block by desc
            {
                if (promoCodeBlockByDest.Any(x => searchReq.Destination.StartsWith(x.Destination.ToLower())))
                    return null;
            }

            if (hotelIdSelected != null && promoCodeBlockByHotelID.Count() > 0) //block for ApplyCode "Payment page"
            {
                if (promoCodeBlockByHotelID.Any(x => hotelIdSelected.ToLower() == x.HotelID.ToLower()))
                    return null;
            }

            if (hotelSupplierSelected != null && promoCodeBlockBySupplier.Count() > 0)
            {
                if (promoCodeBlockBySupplier.Any(x => hotelSupplierSelected.ToLower() == x.HotelSupplierCode.ToLower()))
                    return null;
            }

            #endregion

            #region
            var checkSpecificAirlineCode = result.FirstOrDefault(x => x.PromoCode == searchReq.PromoCode)?.IsSpecificAirline;
            if ((checkSpecificAirlineCode ?? false) && airlineCodeList != null) //need check
            {
                FlightController fc = new FlightController(this.ControllerContext);
                var promoCodeSpecificAirlineDetails = fc.GetPromoCodeSpecificAirlineList(searchReq.PromoCode, tripType, dbContext);
                if (promoCodeSpecificAirlineDetails != null && promoCodeSpecificAirlineDetails.Count > 0)
                {
                    if (airlineCodeList != null)
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
            else if ((checkSpecificAirlineCode ?? false) && airlineCodeList == null)
            {
                return null;
            }
            #endregion
            Expression<Func<PromoCodeRule, bool>> anyDestinationCondition = (x => x.PromoHotelDestinations.Any(d => d.Destination == "-" && d.Active));
            Expression<Func<PromoCodeRule, bool>> anyHotelCondition = (x => x.PromoHotelDestinations.Any(d => (searchReq.Destination.StartsWith(d.Destination) || searchReq.Destination.EndsWith(d.Destination)) && d.PromoHotelLists.Any(p => p.HotelID == "-" && p.Active)));
            Expression<Func<PromoCodeRule, bool>> blockB2BAgent = (x => x.PromoCode == "hotel1111");

            bool anyDestinationOK = result.Any(anyDestinationCondition);
            bool anyHotelOK = result.Any(anyHotelCondition);
            bool blockB2B = result.Any(blockB2BAgent);

            if (anyDestinationOK)
            {
                return result.FirstOrDefault(anyDestinationCondition);
            }
            else if (anyHotelOK)
            {
                Expression<Func<PromoCodeRule, bool>> anyHotelConditionForSpecificDestination = (x => x.PromoHotelDestinations.Any(d => (searchReq.Destination.StartsWith(d.Destination) || searchReq.Destination.EndsWith(d.Destination)) && d.Active && d.PromoHotelLists.Any(p => p.HotelID == "-" && p.Active)));
                return result.FirstOrDefault(anyHotelConditionForSpecificDestination);
            }
            else if (blockB2B && CustomPrincipal.IsAgent)
            {
                return null;
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

        private int CurrentUserID
        {
            get
            {
                int userid = 0;
                int.TryParse(User.Identity.Name, out userid);
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

        private string GetXML(System.Type oType, object Content)
        {
            System.Text.StringBuilder result = new System.Text.StringBuilder(4096);
            try
            {
                System.Xml.Serialization.XmlSerializer XmlSerializer;
                XmlSerializer = new System.Xml.Serialization.XmlSerializer(oType);
                System.IO.MemoryStream MemoryStream = new System.IO.MemoryStream();
                XmlSerializer.Serialize(MemoryStream, Content);
                foreach (byte oByte in MemoryStream.ToArray())
                    result.Append((char)oByte);
                MemoryStream.Close();
                MemoryStream.Dispose();
                MemoryStream = null;
                XmlSerializer = null;
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Hotel Controller - GetXML()");
            }
            return result.ToString();
        }
        #endregion

        private Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier CheckIsEmptySupplier(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier supplier)
        {
            if (supplier == null)
            {
                return null;
            };

            var supplierReflect = typeof(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier).GetProperties();
            List<bool> checkIsNotPassSupplier = new List<bool>();
            foreach (var item in supplierReflect)
            {
                if (item.PropertyType.Name == "Boolean")
                    checkIsNotPassSupplier.Add((bool)item.GetValue(supplier));
            }

            return (checkIsNotPassSupplier.Count(x => x.Equals(true)) > 0) ? supplier : null;
        }

        private List<string> GetBundleHotelId(string bundleId, DateTime checkInDate, DateTime checkoutDate, out HotelBundleModel bundleInfoModel)
        {
            System.Xml.XmlDocument xml = new System.Xml.XmlDocument();
            string path = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/HotelBundlePackage.xml");
            xml.Load(path);
            System.Xml.XmlNodeList childNode = xml.SelectNodes("HotelBundles/Package");
            List<string> hotelId = new List<string>();
            bundleInfoModel = null;

            foreach (System.Xml.XmlNode item in childNode)
            {
                string id = item.Attributes["id"].InnerText;
                DateTime checkInStart = item.Attributes["startDate"].InnerText.ToDateTime();
                DateTime checkInEnd = item.Attributes["endDate"].InnerText.ToDateTime();
                DateTime checkOutStart = item.Attributes["outStartDate"].InnerText.ToDateTime();
                DateTime checkOutEnd = item.Attributes["outEndDate"].InnerText.ToDateTime();
                string eventDesc = null;
                BundleTypes bundleType = BundleTypes.NA;
                List<DateTime> eventDates = new List<DateTime>();

                if (item.Attributes["BundleType"] != null)
                    Enum.TryParse(item.Attributes["BundleType"].InnerText, true, out bundleType);

                if (item.Attributes["eventDesc"] != null)
                    eventDesc = item.Attributes["eventDesc"].InnerText;

                if (item.Attributes["eventDate"] != null)
                {
                    var dateList = item.Attributes["eventDate"].InnerText.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var date in dateList)
                    {
                        var _dt = date.ToDateTimeNullable();
                        if (_dt.HasValue)
                        {
                            eventDates.Add(_dt.Value);
                        }
                    }

                    if (eventDates.Count == 0)
                    {
                        eventDates.Add(checkInEnd);
                    }
                }

                // only matched record will initialize HotelBundleModel
                if (id == bundleId)
                {
                    if (checkInStart <= checkInDate && checkInEnd >= checkInDate &&
                    checkOutStart <= checkoutDate && checkOutEnd >= checkoutDate)
                    {
                        foreach (System.Xml.XmlNode child in item.SelectNodes("hotelId"))
                        {
                            hotelId.Add(child.InnerText);
                        }
                    }

                    bundleInfoModel = new HotelBundleModel
                    {
                        BundleID = id,
                        BundleType = bundleType,
                        CheckInStart = checkInStart,
                        CheckInEnd = checkInEnd,
                        CheckOutStart = checkOutStart,
                        CheckOutEnd = checkOutEnd,
                        HotelID = hotelId,
                        EventDesc = eventDesc,
                        EventDates = eventDates,
                    };
                }
            }

            return hotelId;
        }

        private SearchSupplier GetTieringGroupSupplier(List<int> tieringGrpId, SearchHotelModel model)
        {
            MayFlower db = new MayFlower();
            var db_SearchSuppliers = db.TieringGroupSuppliers.Where(x => tieringGrpId.Contains(x.TieringGroupID) && x.IsActive)?.Select(x => x.SupplierCode)
                ?.ToList() ?? new List<string>();
            bool isOverrideDefaultSearch = Core.SearchSupplierSetting.IsOverrideHotelSupplier;
            var _EAN = (model.SupplierIncluded == null || model.SupplierIncluded.Expedia);
            var _TAAP = (model.SupplierIncluded == null || model.SupplierIncluded.ExpediaTAAP);
            var _HB = (model.SupplierIncluded == null || model.SupplierIncluded.HotelBeds);
            var _JAC = (model.SupplierIncluded == null || model.SupplierIncluded.JacTravel);
            var _TP = (model.SupplierIncluded == null || model.SupplierIncluded.Tourplan);
            var _RAP = (model.SupplierIncluded == null || model.SupplierIncluded.EANRapid);


            SearchSupplier output_SearchSuppliers = isOverrideDefaultSearch ? new SearchSupplier()
            {
                Expedia = (CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.ExpediaEnabled : Core.SearchSupplierSetting.B2C.ExpediaEnabled)
                            && db_SearchSuppliers.Contains("EAN") && _EAN,
                ExpediaTAAP = (CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.ExpediaTAAPEnabled : Core.SearchSupplierSetting.B2C.ExpediaTAAPEnabled)
                            && db_SearchSuppliers.Contains("TAAP") && _TAAP,
                HotelBeds = (CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.HotelBedsEnabled : Core.SearchSupplierSetting.B2C.HotelBedsEnabled)
                            && db_SearchSuppliers.Contains("HB") && _HB,
                JacTravel = (CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.JacTravelEnabled : Core.SearchSupplierSetting.B2C.JacTravelEnabled)
                            && db_SearchSuppliers.Contains("JAC") && _JAC,
                Tourplan = (CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.TourplanEnabled : Core.SearchSupplierSetting.B2C.TourplanEnabled)
                            && db_SearchSuppliers.Contains("TP") && _TP,
                EANRapid = (CustomPrincipal.IsAgent ? Core.SearchSupplierSetting.B2B.EANRapidEnabled : Core.SearchSupplierSetting.B2C.EANRapidEnabled)
                            && db_SearchSuppliers.Contains("RAP") && _RAP,
            } : new SearchSupplier()
            {
                Expedia = db_SearchSuppliers.Contains("EAN") && _EAN,
                ExpediaTAAP = db_SearchSuppliers.Contains("TAAP") && _TAAP,
                HotelBeds = db_SearchSuppliers.Contains("HB") && _HB,
                JacTravel = db_SearchSuppliers.Contains("JAC") && _JAC,
                Tourplan = db_SearchSuppliers.Contains("TP") && _TP,
                EANRapid = db_SearchSuppliers.Contains("RAP") && _RAP,
            };
            return output_SearchSuppliers;
        }
        #region Temp Tiering Group Setup
        /// <summary>
        /// Multi tiering group will merge with ALL setting with true .
        /// </summary>
        private Temp_TieringGroupSearch GetTieringGroupSearchSupplier(List<int> tieringGrpId)
        {
            System.Xml.XmlDocument xml = new System.Xml.XmlDocument();
            string path = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Temp_TieringGroup.xml");
            xml.Load(path);
            System.Xml.XmlNodeList childNode = xml.SelectNodes("TieringGroup/Group");
            Temp_TieringGroupSearch group = new Temp_TieringGroupSearch()
            {
                SearchSuppliers = new SearchSupplier(),
            };

            bool isOverrideDefaultSearch = Core.SearchSupplierSetting.IsOverrideHotelSupplier;
            /**/
            var _defaultSetting = new Core.SearchSupplierSetting();
            var _configReflect = typeof(Core.SearchSupplierSetting).GetProperties();

            foreach (System.Xml.XmlNode item in childNode)
            {
                string id = item.Attributes["id"]?.InnerText;
                string productTypeCode = item.Attributes["ProductTypeCode"]?.InnerText;

                // If group match with XML
                if (tieringGrpId.Any(x => x.ToString() == id))
                {
                    group.TieringGroupID = group.TieringGroupID ?? new List<int> { id.ToInt() };
                    group.ProductTypeCode = productTypeCode;

                    var isTieringGrpExist = group.TieringGroupID.Any(x => x.ToString() == id);

                    if (!isTieringGrpExist)
                        group.TieringGroupID.Add(id.ToInt());

                    if (productTypeCode == "HTL")
                    {
                        foreach (System.Xml.XmlNode child in item.SelectNodes("Supplier"))
                        {
                            SearchSupplier _supplier = group.SearchSuppliers ?? new SearchSupplier();

                            var supplierReflect = typeof(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.SearchSupplier).GetProperties();

                            foreach (var itemRelf in supplierReflect)
                            {
                                if (itemRelf.PropertyType.Name == "Boolean" && itemRelf.Name == child.InnerText)
                                {
                                    var lastValue = (bool)itemRelf.GetValue(_supplier);
                                    bool _configForceDisabledSupplier = false;

                                    if (isOverrideDefaultSearch)
                                    {
                                        var _configSetupOverride = _configReflect.FirstOrDefault(x => x.PropertyType.Name == "Boolean" &&
                                        x.Name.ToLower().Contains(child.InnerText.ToLower()));
                                        if (_configSetupOverride != null)
                                        {
                                            _configForceDisabledSupplier = !((bool)_configSetupOverride.GetValue(_defaultSetting));
                                        }
                                    }

                                    /* If last tiering SearchSupplier is FALSE, 
                                     * then will update to TRUE when other TieringGroup if supplier allow.
                                       but not override default setting. */
                                    if (!lastValue && !_configForceDisabledSupplier)
                                    {
                                        itemRelf.SetValue(group.SearchSuppliers, true);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return group;
        }

        public class Temp_TieringGroupSearch
        {
            public List<int> TieringGroupID { get; set; }
            public string ProductTypeCode { get; set; }
            public SearchSupplier SearchSuppliers { get; set; }
        }
        #endregion

        class PersonComparer : IEqualityComparer<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails>
        {
            public bool Equals(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails p1, Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails p2)
            {
                return p1.jacTravelPropertyRoomTypeID == p2.jacTravelPropertyRoomTypeID && p1.jacTravelBookingToken == p2.jacTravelBookingToken;
            }

            public int GetHashCode(Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.RoomAvailabilityDetails p)
            {
                return Convert.ToInt32(p.jacTravelPropertyRoomTypeID);
            }
        }

        private string ConvertTypeValueToString<T>(T value)
        {
            var prop = typeof(T).GetProperties();
            string displayText = null;

            foreach (var item in prop.OrderByDescending(x => x.Name.Contains("Title")).ThenByDescending(x => x.Name.Contains("name")))
            {
                if (item.PropertyType.Name == "String")
                {
                    displayText += string.Format("{0} : {1}<br>", item.Name, (string)item.GetValue(value));
                }
            }
            return displayText;
        }

        #region Preparation Future Function
        // Prepare for future
        private void AsyncGetHotelInfo(ref IPagedList<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> hotelList)
        {
            var _resTaskList = new List<Task<Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.GetHotelInformationResponse>>();
            foreach (var hotel in hotelList)
            {
                GetHotelInformationModel model = new GetHotelInformationModel()
                {
                    HotelID = hotel.hotelId,
                    CurrencyCode = "MYR",
                    CustomerUserAgent = Request == null ? System.Web.HttpContext.Current.Request.UserAgent : Request.UserAgent,
                    CustomerIpAddress = GetUserIP(),
                    CustomerSessionId = Guid.NewGuid().ToString()
                };

                _resTaskList.Add(ExpediaHotelsServiceCall.GetHotelInformationAsync(model));
            }

            var res = Task.WhenAll(_resTaskList).GetAwaiter().GetResult();

            foreach (var infoResponse in res)
            {
                var h = hotelList.FirstOrDefault(x => x.hotelId == infoResponse.hotelInformation.hotelSummary.hotelId);

                h.hotelDetail = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelDetails
                {
                    checkInTime = infoResponse.hotelInformation.hotelDetails.checkInTime,
                    checkOutTime = infoResponse.hotelInformation.hotelDetails.checkOutTime,
                    locationDescription = infoResponse.hotelInformation.hotelDetails.locationDescription,
                    numberOfRooms = infoResponse.hotelInformation.hotelDetails.numberOfRooms,
                    numberOfFloors = infoResponse.hotelInformation.hotelDetails.numberOfFloors,
                    extraPersonCharge = infoResponse.hotelInformation.hotelDetails.extraPersonCharge,
                    propertyInformation = infoResponse.hotelInformation.hotelDetails.propertyInformation,
                    areaInformation = infoResponse.hotelInformation.hotelDetails.areaInformation,
                    propertyDescription = infoResponse.hotelInformation.hotelDetails.propertyDescription,
                    hotelPolicy = infoResponse.hotelInformation.hotelDetails.hotelPolicy,
                    depositCreditCardsAccepted = infoResponse.hotelInformation.hotelDetails.depositCreditCardsAccepted,
                    roomInformation = infoResponse.hotelInformation.hotelDetails.roomInformation,
                    drivingDirections = infoResponse.hotelInformation.hotelDetails.drivingDirections,
                    checkInInstructions = infoResponse.hotelInformation.hotelDetails.checkInInstructions,
                    knowBeforeYouGoDescription = infoResponse.hotelInformation.hotelDetails.knowBeforeYouGoDescription,
                    roomFeesDescription = infoResponse.hotelInformation.hotelDetails.roomFeesDescription,
                    diningDescription = infoResponse.hotelInformation.hotelDetails.diningDescription,
                    amenitiesDescription = infoResponse.hotelInformation.hotelDetails.amenitiesDescription,
                    businessAmenitiesDescription = infoResponse.hotelInformation.hotelDetails.businessAmenitiesDescription,
                    roomDetailDescription = infoResponse.hotelInformation.hotelDetails.roomDetailDescription,
                    specialCheckInInstructions = infoResponse.hotelInformation.hotelDetails.specialCheckInInstructions,
                    mandatoryFeesDescription = infoResponse.hotelInformation.hotelDetails.mandatoryFeesDescription,
                    nationalRatingsDescription = infoResponse.hotelInformation.hotelDetails.nationalRatingsDescription,
                    renovationsDescription = infoResponse.hotelInformation.hotelDetails.renovationsDescription
                };
            }

            //return 0;
        }
        #endregion

        #region Tracking Cookies
        public void Trackhotelviewedcookie(HotelViewedCookie Hotelviewedcookie)
        {
            try
            {
                List<HotelViewedCookie> cookielist = new List<HotelViewedCookie>();
                JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
                string trackingcookie;
                if (Request.Cookies["F1hotelcookie"] != null)
                {
                    trackingcookie = Request.Cookies["F1hotelcookie"].Value;
                    cookielist = jsSerializer.Deserialize<List<HotelViewedCookie>>(trackingcookie).Where(x => x.lastsearch.AddDays(7).Date >= DateTime.Now.Date && x.HotelID != Hotelviewedcookie.HotelID).ToList();
                }
                cookielist.Add(Hotelviewedcookie);
                string myObjectJson = jsSerializer.Serialize(cookielist);
                var cookie = new System.Web.HttpCookie("F1hotelcookie", myObjectJson)
                {
                    Expires = DateTime.Now.AddDays(7)
                };
                HttpContext.Response.Cookies.Add(cookie);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Track Hotel Error {DateTime.Now.ToLoggerDateTime()}");

                try
                {
                    Request?.Cookies?.Remove("F1hotelcookie");
                }
                catch { }
            }
        }

        public void AddHotelSearchCookie(SearchHotelModel model)
        {
            List<UserSearchFHCookiesModel> _userCookies = new List<UserSearchFHCookiesModel>();
            string encryptCookies;
            string trackingUserSearchCookiesList;
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            if (Request.Cookies["SaveUserCookies"] != null)
            {
                encryptCookies = Request.Cookies["SaveUserCookies"].Value;
                trackingUserSearchCookiesList = Cryptography.AES.Decrypt(encryptCookies);

                _userCookies = jsSerializer.Deserialize<List<UserSearchFHCookiesModel>>(trackingUserSearchCookiesList).ToList();
            }

            UserSearchFHCookiesModel addUserSearchHotelCookies = new UserSearchFHCookiesModel()
            {
                Destination = model.Destination,
                ArrivalDate = model.ArrivalDate,
                DepartureDate = model.DepartureDate,
                AdultNo = model.NoOfAdult,
                ChildrenNo = model.NoOfInfant,
                RoomNo = model.NoOfRoom,
                StarNo = model.Star,
                ProductType = "hotel"
            };

            UserSearchFHCookiesModel checkUserSearchHotelCookies = new UserSearchFHCookiesModel();
            bool sameWithLast = false;
            if (_userCookies != null)
            {
                checkUserSearchHotelCookies = _userCookies.LastOrDefault(x => x.ProductType == "hotel");
                if (checkUserSearchHotelCookies != null)
                {
                    if (checkUserSearchHotelCookies.Destination == addUserSearchHotelCookies.Destination &&
                        checkUserSearchHotelCookies.ArrivalDate.Value.ToLocalTime() == addUserSearchHotelCookies.ArrivalDate.Value &&
                        checkUserSearchHotelCookies.DepartureDate.Value.ToLocalTime() == addUserSearchHotelCookies.DepartureDate.Value &&
                        checkUserSearchHotelCookies.AdultNo == addUserSearchHotelCookies.AdultNo &&
                        checkUserSearchHotelCookies.ChildrenNo == addUserSearchHotelCookies.ChildrenNo &&
                        checkUserSearchHotelCookies.RoomNo == addUserSearchHotelCookies.RoomNo &&
                        checkUserSearchHotelCookies.StarNo == addUserSearchHotelCookies.StarNo)
                    {
                        sameWithLast = true;
                    }
                }
            }

            if (!sameWithLast)
            {
                _userCookies.Add(addUserSearchHotelCookies);
            }

            if (_userCookies.Where(x => x.ProductType == "hotel").Count() > 2)
            {
                var removeOldCookie = _userCookies.First(x => x.ProductType == "hotel");
                _userCookies.Remove(removeOldCookie);
            }

            string SerializeUserCookies = jsSerializer.Serialize(_userCookies);
            string encryptSerializeUserCookies = Cryptography.AES.Encrypt(SerializeUserCookies);
            var UserCookie = new System.Web.HttpCookie("SaveUserCookies", encryptSerializeUserCookies)
            {
                Expires = DateTime.Now.AddDays(15)
            };
            HttpContext.Response.Cookies.Add(UserCookie);
        }
        #endregion

        private void InsertSelectedSearchSupplier(SearchHotelModel _searchModel, System.Reflection.PropertyInfo[] supplierReflect, SearchProgress.Progress searchProgress)
        {
            if (_searchModel != null && _searchModel.SupplierIncluded != null)
            {
                foreach (var item in supplierReflect)
                {
                    if (item.PropertyType.Name == "Boolean")
                    {
                        if ((bool)item.GetValue(_searchModel.SupplierIncluded))
                        {
                            var supp = (Suppliers)
                            Enum.Parse(typeof(Suppliers), item.Name);

                            _searchModel.SetSearchProgress(supp, searchProgress);
                        }
                    }
                }
            }
        }

        #region Function for Temp Memory Cache Result
        public void SetCache(SearchHotelModel _SearchModel)
        {
            /*
             * 1) Check Destination
             * 2) If not exist cache any ignore date pax
             * 3) Display dump result first
             * 4) Perform search
             * 6) Await search complete
             * 7) Replace result at frontend
             */

            if (_SearchModel?.Result?.HotelList?.Length <= 0)
            {
                return;
            }

            var _dumpCacheList = _GetCacheFromMem(DumpListCacheKey);

            if (_dumpCacheList == null)
            {
                List<SearchHotelModel> _cacheList = new List<SearchHotelModel>();

                _cacheList.Add(_SearchModel);

                System.Web.HttpContext.Current.Cache.Add(DumpListCacheKey, _cacheList, null, System.Web.Caching.Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(30), System.Web.Caching.CacheItemPriority.Default, null);
            }
            else
            {
                var _converted = (List<SearchHotelModel>)_dumpCacheList;

                var _output = _converted.FirstOrDefault(x => x.Destination.ToLower() == _SearchModel.Destination.ToLower());

                if (_output == null)
                {
                    _converted.Add(_SearchModel);
                }
                else
                {
                    // Any null then dump result inside first

                    if (_output.Result == null)
                    {
                        _output.Result = _SearchModel.Result;
                    }

                    if (_output.B2BResult == null)
                    {
                        _output.B2BResult = _SearchModel.B2BResult;
                    }
                }
            }
        }

        public bool GetCache(SearchHotelModel _SearchModel)
        {
            var _dumpCacheList = _GetCacheFromMem(DumpListCacheKey);

            if (_dumpCacheList != null)
            {
                var _converted = (List<SearchHotelModel>)_dumpCacheList;

                var _output = _converted.FirstOrDefault(x => x.Destination.ToLower() == _SearchModel.Destination.ToLower());

                if (_output != null)
                {
                    _SearchModel.Result = _output.Result;
                    _SearchModel.B2BResult = _output.B2BResult;
                    return true;
                }
            }

            return false;
        }

        internal protected object _GetCacheFromMem(string cacheKey)
        {
            return System.Web.HttpContext.Current.Cache[DumpListCacheKey];
        }

        #endregion
    }
}