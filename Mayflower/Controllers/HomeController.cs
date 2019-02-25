using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using NLog;
using System.Text;
using Alphareds.Module.HomeController;
using Alphareds.Module.Model;
using Alphareds.Module.Common;
using Newtonsoft.Json;
using Alphareds.Module.Model.Database;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Web.Security;
using Alphareds.Module.Cryptography;

namespace Mayflower.Controllers
{
    [Mayflower.Filters.PreserveAllQueryStringFilter]
    public class HomeController : AsyncController
    {
        private static string tripid = System.Web.HttpContext.Current.Request.QueryString["tripid"] ?? (tripid ?? Guid.NewGuid().ToString());
        private string sessionNameBooking = Enumeration.SessionName.FlightBooking + tripid;

        public ActionResult Index(string errorMsg)
        {
            int dayAdvance = Convert.ToInt32(Core.GetSettingValue("dayadvance"));

            FlightBookingModel model = Session["FlightBooking"] == null ? null : (FlightBookingModel)Session["FlightBooking"];
            int userid = 0;

            if (Request.IsAuthenticated && Core.EnableCMS)
            {
                return RedirectToAction("RedirectAndPOST", "DynamicFormPostSurface", new { destinationUrl = System.Web.Configuration.WebConfigurationManager.AppSettings["AlphaReds.CMSUrl"], type = "SessionTransfer" });
            }
            else if (Core.EnableCMS)
            {

                string CMSUrl = Core.GetAppSettingValueEnhanced("AlphaReds.CMSUrl");
                string landingPageUrl = string.IsNullOrWhiteSpace(CMSUrl) ?
                "https://www.mayflower.com.my/" : CMSUrl;

                UriBuilder builder = new UriBuilder(landingPageUrl);
                var query = HttpUtility.ParseQueryString(builder.Query);

                if (Request.QueryString != null && Request.QueryString.Count > 0)
                {
                    foreach (var item in Request.QueryString)
                    {
                        query[item.ToString()] = Request.QueryString[item.ToString()];
                    }
                }

                builder.Query = query.ToString();

                Response.Redirect(builder.ToString());
            }
            /*else if (User.Identity.IsAuthenticated)
            {
                if (model == null)
                {
                    userid = Convert.ToInt32(User.Identity.Name);
                    //model = HomeServiceController.getHomeModelWhenIsUserLogin(userid, dayAdvance);
                }
            }*/

            // Initialize for Anonymous user //start validate
            model = Alphareds.Module.Public.Flight.Helper.GetHomeModelAnonymousUser(model, dayAdvance);

            List<UserSearchFHCookiesModel> UserCookies = new List<UserSearchFHCookiesModel>();
            UserSearchFHCookiesModel LatestFlightCookie = new UserSearchFHCookiesModel();
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            string trackingUserSearchCookiesList;
            string encryptCookies;
            if (Request.Cookies["SaveUserCookies"] != null)
            {
                encryptCookies = Request.Cookies["SaveUserCookies"].Value;
                trackingUserSearchCookiesList = Cryptography.AES.Decrypt(encryptCookies);

                UserCookies = jsSerializer.Deserialize<List<UserSearchFHCookiesModel>>(trackingUserSearchCookiesList).ToList();
                LatestFlightCookie = UserCookies.LastOrDefault(x => x.ProductType == "flight");
                if (LatestFlightCookie != null)
                {
                    if (LatestFlightCookie.ArrivalDate.HasValue && LatestFlightCookie.ArrivalDate.Value.ToLocalTime().AddDays(1) < DateTime.Now.AddDays(dayAdvance))
                    {
                        LatestFlightCookie = null;
                    }
                    if (LatestFlightCookie != null)
                    {
                        model = BindSearchCookies(model, LatestFlightCookie);
                    }
                }
            }
            var locationList = Alphareds.Module.ServiceCall.CarsRentalServiceCall.GetBranchList();
            ViewBag.locationList = locationList;
            return View(model);
        }

        [HttpPost]
        [Filters.PreserveQueryStringFilter(QueryString = "tripid")]
        public ActionResult Index(FlightBookingModel model, [Bind(Exclude = "SearchResults")]SearchFlightResultViewModel searchModel)
        {
            tripid = Guid.NewGuid().ToString();
            sessionNameBooking = Enumeration.SessionName.FlightBooking + tripid;
            #region 2017/02/28 - Development Region, For Get Result from Dummy Cloned Set
            bool useDumpResultList = false;
            bool.TryParse(Core.GetAppSettingValueEnhanced("UseDumpResultList"), out useDumpResultList);

            if (useDumpResultList)
            {
                var dumpModel = GetDumpFlightBookingModel("FlightListResultFullList.txt");
                Session[sessionNameBooking] = dumpModel;
                Session["FullFlightSearchResult"] = dumpModel.FlightSearchResultViewModel.FullFlightSearchResult;

                Session["FullODOResult"] = JsonConvert.DeserializeObject<FlightBookingModel>(JsonConvert.SerializeObject(dumpModel));
                Session.Remove("filterParam");
                return RedirectToAction("FlightSearchResult", "FlightSearch", new { tripid });
            }
            #endregion

            /*20170210 RECEIVE DATA PASSING FROM MAYFLOWER CMS - START */
            if (Request.Form["TripTypeCMS"] != null)
            {
                model.SearchFlightResultViewModel = new SearchFlightResultViewModel();

                DateTime beginDate = DateTime.ParseExact(Request.Form["BeginDateCMS"].ToString(), "dd-MMM-yyyy h:mm:ss tt", null);
                DateTime endDate = DateTime.ParseExact(Request.Form["EndDateCMS"].ToString(), "dd-MMM-yyyy h:mm:ss tt", null);

                model.SearchFlightResultViewModel.TripType = Request.Form["TripTypeCMS"].ToString();
                model.SearchFlightResultViewModel.DepartureTime = Request.Form["DepartureTimeCMS"].ToString();
                model.SearchFlightResultViewModel.ReturnTime = Request.Form["ReturnTimeCMS"].ToString();
                model.SearchFlightResultViewModel.DepartureStation = Request.Form["DepartureStationCMS"]?.ToString().ToUpper();
                model.SearchFlightResultViewModel.ArrivalStation = Request.Form["ArrivalStationCMS"]?.ToString().ToUpper();
                model.SearchFlightResultViewModel.BeginDate = beginDate;
                model.SearchFlightResultViewModel.EndDate = endDate;
                model.SearchFlightResultViewModel.PrefferedAirlineCode = Request.Form["PrefferedAirlineCodeCMS"].ToString();
                if (model.SearchFlightResultViewModel.PrefferedAirlineCode == "")
                {
                    model.SearchFlightResultViewModel.PrefferedAirlineCode = null;
                }
                model.SearchFlightResultViewModel.PromoCode = Request.Form["PromoCodeCMS"] == null ? null : Request.Form["PromoCodeCMS"].ToString();
                model.SearchFlightResultViewModel.Adults = Convert.ToInt32(Request.Form["AdultsCMS"].ToString());
                model.SearchFlightResultViewModel.Childrens = Convert.ToInt32(Request.Form["ChildrensCMS"].ToString());
                model.SearchFlightResultViewModel.Infants = Convert.ToInt32(Request.Form["InfantsCMS"].ToString());
                model.SearchFlightResultViewModel.CabinClass = Request.Form["CabinClassCMS"].ToString();
                model.SearchFlightResultViewModel.DirectFlight = Convert.ToBoolean(Request.Form["DirectFlightCMS"].ToString());
                model.SearchFlightResultViewModel.IsDynamic = Convert.ToBoolean(Request.Form["IsDynamic"] != null ? Request.Form["IsDynamic"].ToString() : "false");
                model.SearchFlightResultViewModel.NoOfRoom = Convert.ToInt32(Request.Form["DynamicRoomCMS"] != null ? Request.Form["DynamicRoomCMS"].ToString() : "1");
                model.SearchFlightResultViewModel.IsFixedPrice = Convert.ToBoolean(Request.Form["IsFixedPrice"] != null ? Request.Form["IsFixedPrice"].ToString() : "false");
                model.SearchFlightResultViewModel.MarketingMessage = Request.Form["MarketingMessage"];
            }
            /*20170210 RECEIVE DATA PASSING FROM MAYFLOWER CMS - END */

            AddFlightSearchCookie(model.SearchFlightResultViewModel);

            if (searchModel != null && !string.IsNullOrWhiteSpace(searchModel.DepartureStationCode) && searchModel.BeginDate != null)
            {
                model.SearchFlightResultViewModel = searchModel;
            }
            else
            {
                var stateItem = typeof(SearchFlightResultViewModel).GetProperties();
                foreach (var item in stateItem)
                {
                    if (ModelState[item.Name] != null)
                    {
                        ModelState[item.Name].Errors.Clear();
                    }
                }
            }

            Session[sessionNameBooking] = model;

            // 0 represent as guest account
            int userid = User.Identity.IsAuthenticated ? Convert.ToInt32(User.Identity.Name) : 0;

            try
            {
                if (ModelState.IsValid)
                {
                    if (model.SearchFlightResultViewModel.IsFixedPrice)
                    {
                        return RedirectToAction("GetFixedHotelSearch", "Hotel", new { tripid });
                    }
                    else
                    {
                        // 20161126 - For testing next page list flight START
                        return RedirectToAction("Search", "Flight", new { tripid });
                        // 20161126 - For testing next page list flight END
                    }
                }
                else
                {
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                UtilitiesService.NlogExceptionForBookingFlow(logger, model, ex, userid, "HomeSearchFlightError", DateTime.Now.ToString(), "");

                return RedirectToAction("Index", "Home");
            }
        }

        //[HttpPost]
        //public JsonResult AirportAutoComplete(string Prefix, string errorMsg, string origin = null, string destination = null)
        //{
        //    List<AirportModel> MatchCodeList = new List<AirportModel>();
        //    MatchCodeList = UtilitiesService.AirportAutoCompleteList(Prefix);

        //    if (!string.IsNullOrWhiteSpace(origin) && origin.Length >= 3 && !string.IsNullOrEmpty(destination))
        //    {
        //        MatchCodeList = MatchCodeList.Where(x => x.code.Substring(0, 3).ToUpper() != origin.Substring(0, 3).ToUpper()).ToList();
        //    }

        //    return Json(MatchCodeList.Select(x => new { label = x.airport, id = x.code, value = x.airport }).Distinct().ToArray(), JsonRequestBehavior.AllowGet);
        //}

        [OutputCache(Duration = 3600, VaryByParam = "none")]
        public JavaScriptResult AirportCompleteList()
        {
            // From DB
            using (MayFlower dbContext = new MayFlower())
            {
                var script = @"var completeAirport = ";
                script += JsonConvert.SerializeObject(dbContext.Stations.Where(x => x.CountryCode != "NA" && x.IsActive).Select(x => x.DisplayName));
                return JavaScript(script);
            }
        }

        [HttpPost]
        public JsonResult AirlineAutoComplete(string Prefix, string errorMsg)
        {
            List<AirlineModel> AirlineList = new List<AirlineModel>();
            AirlineList = UtilitiesService.AirlineAutoComplete(Prefix);

            return Json(AirlineList.Select(x => new { label = x.airline, id = x.code, value = x.airline }).Distinct().ToArray(), JsonRequestBehavior.AllowGet);
        }

        #region 2016/11/20, Get Hardcode Flight Search List, for Testing
        private void CloneFlightBookingModel(FlightBookingModel model)
        {
            var cloneFlightResult = Newtonsoft.Json.JsonConvert.SerializeObject(model);
            var fileName = System.IO.Path.GetFileName("FlightListResult" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
            var path = Server.MapPath("~/cache/");
            var fullPath = System.IO.Path.Combine(path, fileName);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            System.IO.File.WriteAllText(fullPath, cloneFlightResult);
        }

        private FlightBookingModel GetDumpFlightBookingModel(string fileName)
        {
            var path = Server.MapPath("~/cache/");
            var fullPath = System.IO.Path.Combine(path, System.IO.Path.GetFileName(fileName));
            var deResult = Newtonsoft.Json.JsonConvert.DeserializeObject<FlightBookingModel>(System.IO.File.ReadAllText(fullPath));
            return deResult;
        }
        #endregion

        public void AddFlightSearchCookie(SearchFlightResultViewModel model)
        {
            List<UserSearchFHCookiesModel> _userCookies = new List<UserSearchFHCookiesModel>();
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            string trackingUserSearchCookiesList;
            string encryptedCookies;

            if (Request.Cookies["SaveUserCookies"] != null)
            {
                encryptedCookies = Request.Cookies["SaveUserCookies"].Value;
                trackingUserSearchCookiesList = Cryptography.AES.Decrypt(encryptedCookies);

                _userCookies = jsSerializer.Deserialize<List<UserSearchFHCookiesModel>>(trackingUserSearchCookiesList).ToList();
            }

            UserSearchFHCookiesModel addUserSearchFlightCookies = new UserSearchFHCookiesModel()
            {
                From = model.DepartureStation,
                Destination = model.ArrivalStation,
                ArrivalDate = model.BeginDate,
                DepartureDate = model.EndDate,
                AdultNo = model.Adults,
                ChildrenNo = model.Childrens,
                InfantNo = model.Infants,
                PrefferedAirlineCode = model.PrefferedAirlineCode,
                RoomNo = model.NoOfRoom,
                TripType = model.TripType,
                CabinClass = model.CabinClass,
                ProductType = "flight"
            };

            UserSearchFHCookiesModel checkUserSearchFlightCookies = new UserSearchFHCookiesModel();
            bool sameWithLast = false;

            if (_userCookies != null)
            {
                checkUserSearchFlightCookies = _userCookies.LastOrDefault(x => x.ProductType == "flight");
                if (checkUserSearchFlightCookies != null)
                {
                    if (checkUserSearchFlightCookies.Destination == addUserSearchFlightCookies.Destination &&
                        checkUserSearchFlightCookies.From == addUserSearchFlightCookies.From &&
                        ((checkUserSearchFlightCookies.DepartureDate.HasValue && addUserSearchFlightCookies.DepartureDate.HasValue && 
                            checkUserSearchFlightCookies.DepartureDate.Value.ToLocalTime() == addUserSearchFlightCookies.DepartureDate.Value) ||
                            (!checkUserSearchFlightCookies.DepartureDate.HasValue && !addUserSearchFlightCookies.DepartureDate.HasValue)) &&
                        ((checkUserSearchFlightCookies.ArrivalDate.HasValue && addUserSearchFlightCookies.ArrivalDate.HasValue &&
                            checkUserSearchFlightCookies.ArrivalDate.Value.ToLocalTime() == addUserSearchFlightCookies.ArrivalDate.Value) ||
                            (!checkUserSearchFlightCookies.ArrivalDate.HasValue && !addUserSearchFlightCookies.ArrivalDate.HasValue)) &&
                        checkUserSearchFlightCookies.AdultNo == addUserSearchFlightCookies.AdultNo &&
                        checkUserSearchFlightCookies.ChildrenNo == addUserSearchFlightCookies.ChildrenNo &&
                        checkUserSearchFlightCookies.InfantNo == addUserSearchFlightCookies.InfantNo &&
                        checkUserSearchFlightCookies.ChildrenNo == addUserSearchFlightCookies.ChildrenNo &&
                        checkUserSearchFlightCookies.PrefferedAirlineCode == addUserSearchFlightCookies.PrefferedAirlineCode &&
                        checkUserSearchFlightCookies.TripType == addUserSearchFlightCookies.TripType &&
                        checkUserSearchFlightCookies.CabinClass == addUserSearchFlightCookies.CabinClass)
                    {
                        sameWithLast = true;
                    }
                }
            }

            //if same with last dont add
            if (!sameWithLast)
            {
                _userCookies.Add(addUserSearchFlightCookies);
            }

            if (_userCookies.Where(x => x.ProductType == "flight").Count() > 2)
            {
                var removeOldCookie = _userCookies.First(x => x.ProductType == "flight");
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

        public FlightBookingModel BindSearchCookies(FlightBookingModel model, UserSearchFHCookiesModel cookie)
        {
            if (cookie != null)
            {
                model.SearchFlightResultViewModel = new SearchFlightResultViewModel()
                {
                    DepartureStation = cookie.From,
                    ArrivalStation = cookie.Destination,
                    BeginDate = cookie.ArrivalDate.Value.ToLocalTime(),
                    EndDate = cookie.DepartureDate.Value.ToLocalTime(),
                    Adults = cookie.AdultNo,
                    Childrens = cookie.ChildrenNo,
                    Infants = cookie.InfantNo,
                    PrefferedAirlineCode = cookie.PrefferedAirlineCode,
                    NoOfRoom = cookie.RoomNo,
                    CabinClass = cookie.CabinClass,
                    TripType = cookie.TripType
                };
            }

            return model;
        }
    }
}
