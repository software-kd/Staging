using NLog;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Newtonsoft.Json;

namespace Mayflower.Areas.SessionLess.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.Disabled)]
    public class SHotelController : AsyncController
    {
        private string tripid { get; set; }
        private Logger Logger { get; set; }

        private Mayflower.General.CustomPrincipal CustomPrincipal => User as Mayflower.General.CustomPrincipal;

        private string _ExecutedController { get; set; }
        private string _ExecutedAction { get; set; }

        public SHotelController()
        {
            Logger = LogManager.GetCurrentClassLogger();
            var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
            var routeValue = request.RequestContext.RouteData.Values["tripid"];
            string routeString = routeValue?.ToString();
            tripid = System.Web.HttpContext.Current.Request.QueryString["tripid"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["tripid"]);

            _ExecutedController = ControllerContext?.RouteData?.Values["controller"]?.ToString().ToLower();
            _ExecutedAction = ControllerContext?.RouteData?.Values["action"]?.ToString().ToLower();

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
                UserLoginID = (User.Identity.IsAuthenticated && CustomPrincipal.IsAgent ? CustomPrincipal.LoginID : null), // setter by agent user login id
                Search_by = Alternative_Searching.Search_By_HotelIDs,
                SearchSuppliers = new SearchSupplier
                {
                    Expedia = true
                }
            };

            MayFlower dbCtx = new MayFlower();
            //var featuredHotel = dbCtx.HotelFeatureLists.Where(x => x.IsActive && x.SupplierCode == "EAN")
            //    .OrderByDescending(s => s.SortOrder).Take(3)
            //    .Select(s => s.HotelID)?.ToArray() ?? new string[] { };

            int dummyCount = 5;
            string _destinationTrim = hotel.Destination.Split(',').FirstOrDefault() ?? "";
            var hotSellHotel = dbCtx.BookingHotels
                .Where(x => x.HotelID != null && x.BookingStatusCode == "CON" && x.SupplierCode == "EAN"
                && x.HotelCity.ToLower().StartsWith(_destinationTrim.ToLower()))
                .GroupBy(x => x.HotelID)
                .OrderBy(x => x.Count())
                .Take(dummyCount)
                .Select(x => x.Key)?.ToArray() ?? new string[] { };

            if (hotSellHotel.Length < dummyCount)
            {
                int _remainDummyRowCount = dummyCount - hotSellHotel.Length;
                var _list2 = dbCtx.HotelLists.Where(x => x.IsActive && x.City.ToLower().StartsWith(_destinationTrim.ToLower()))
                    .Take(_remainDummyRowCount)
                    .Select(x => x.EANHotelID.ToString())?.ToList() ?? new List<string>();

                hotSellHotel = hotSellHotel.ToList().Concat(_list2).ToArray();
            }

            hotelReqInit.Search_by_HotelIDs = new Search_By_HotelIDs();
            hotelReqInit.Search_by_HotelIDs.HotelIDs = hotSellHotel; //featuredHotel;

            hotelReqInit.CustomerUserAgent = Request.UserAgent;
            hotelReqInit.CustomerSessionId = Guid.NewGuid().ToString();
            hotelReqInit.CustomerIpAddress = GetUserIP();
            hotelReqInit.UserLoginID = User.Identity.IsAuthenticated && CustomPrincipal.IsAgent ? CustomPrincipal.LoginID : null;

            string _sToken = Mayflower.Controllers.HotelController.SerializeHotelSearchToken(hotel.DateFrom, hotel.DateTo, hotel.NoRooms, hotel.Adults, hotel.Kids,
                hotelReqInit.CrossSale, hotelReqInit.IsB2B, hotelReqInit.rateType, hotelReqInit.CurrencyCode);

            var _room = JsonConvert.DeserializeObject<Search.Hotel.Room>(_sToken);
            _room.InitESBRoomRequest(true);
            hotelReqInit.NumberOfRoom = _room.ESBRoomRequest.NumberOfRoom;

            ESBHotelManagerClient webservice = new ESBHotelManagerClient();
            var req = await webservice.GetHotelListAsync(hotelReqInit);

            ViewBag.TripID = hotelReqInit.CustomerSessionId;

            return PartialView("~/Views/Hotel/v2/_HotelListSessionless.cshtml", req);
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
    }
}