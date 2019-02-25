using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Alphareds.Module.Common;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Alphareds.Module.Model;
using Alphareds.Module.ServiceCall;
using PagedList;
using NLog;
using WebGrease.Css.Extensions;
using System.Dynamic;
using Newtonsoft.Json;

namespace Mayflower.Areas.SPAgent.Controllers
{
    [RouteArea("Agent")]
    [Filters.PreserveQueryStringFilter(QueryString = "tripid")]
    [SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
    public class AHotelController : AsyncController
    {
        private Logger logger { get; set; }
        private Alphareds.Module.Event.Function.DB eventDBFunc { get; set; }
        private string tripid { get; set; }
        private SearchHotelModel searchModel { get; set; }
        private const string DumpListCacheKey = "HotelListCache";

        // Constructor
        public AHotelController()
        {
            logger = LogManager.GetCurrentClassLogger();
            Alphareds.Module.Event.Function.DB eventDBFunc = new Alphareds.Module.Event.Function.DB(logger);

            var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
            var routeValue = request.RequestContext.RouteData.Values["tripid"];
            string routeString = routeValue != null ? routeValue.ToString() : null;
            tripid = System.Web.HttpContext.Current.Request.QueryString["tripid"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["tripid"]);

            if (!string.IsNullOrWhiteSpace(tripid))
            {
                tripid = tripid.Split(',')[0];
            }

            var _session = Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);
            searchModel = _session != null ? (SearchHotelModel)_session : new SearchHotelModel
            {

            };
        }

        // GET: Agent/Hotel
        public async Task<ActionResult> Index()
        {
            SearchHotelModel searchModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);

            ESBHotelServiceCall.Search b2BSearch = new ESBHotelServiceCall.Search(searchModel);

            var resp = await b2BSearch.GetB2BHotelListAsync();

            return View();
        }

        public async Task<ActionResult> Search(int? page)
        {
            //var _session = Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);
            //SearchHotelModel searchModel = (SearchHotelModel)_session;

            searchModel.CurrentViewPage = (page ?? searchModel.CurrentViewPage);
            int pageNumber = searchModel.CurrentViewPage;
            int pageSize = 10;
            int.TryParse(Core.GetAppSettingValueEnhanced("RecordsPerPage"), out pageSize);

            if (searchModel.Result != null || searchModel.B2BResult != null)
            {
                var IPagedModel = searchModel.Result?.HotelList.ToPagedList(pageNumber, pageSize);
                var B2BIPagedModel = searchModel.B2BResult?.HotelList?.ToPagedList(pageNumber, pageSize);
                searchModel.IPagedHotelList = IPagedModel;
                searchModel.IPagedB2BHotelList = B2BIPagedModel;
            }
            else
            {
                if (searchModel.SearchProgress.Count == 0)
                {
                    SessionSetterController sControl = new SessionSetterController();
                    sControl.GetCache(searchModel);
                }
            }

            return View(searchModel);
        }

        public async Task<ActionResult> _LoopResult(int? page)
        {
            //var _session = Core.GetSession(Enumeration.SessionName.SearchRequest, tripid);
            //SearchHotelModel searchModel = (SearchHotelModel)_session;

            searchModel.CurrentViewPage = (page ?? searchModel.CurrentViewPage);
            int pageNumber = searchModel.CurrentViewPage;
            int pageSize = 10;
            int.TryParse(Core.GetAppSettingValueEnhanced("RecordsPerPage"), out pageSize);

            if (searchModel.Result != null || searchModel.B2BResult != null)
            {
                var IPagedModel = searchModel.Result?.HotelList.ToPagedList(pageNumber, pageSize);
                var B2BIPagedModel = searchModel.B2BResult?.HotelList?.ToPagedList(pageNumber, pageSize);
                searchModel.IPagedHotelList = IPagedModel;
                searchModel.IPagedB2BHotelList = B2BIPagedModel;
            }

            return View(searchModel);
        }

        // Get Price
        public ActionResult _GtPc(string htid)
        {
            List<IDictionary<string, Object>> _priceObj = new List<IDictionary<string, object>>();

            if (searchModel?.Result?.HotelList != null && searchModel.Result.HotelList.Length > 0
                && searchModel.B2BResult == null)
            {
                ICollection<B2BList> _B2BHotelListResponses = new List<B2BList>();

                foreach (var item in searchModel.Result.HotelList)
                {
                    B2BList _dumpB2BHotelList = new B2BList();
                    _dumpB2BHotelList.SupplierHotels = new HotelInformation[1] { item };
                    _B2BHotelListResponses.Add(_dumpB2BHotelList);
                }

                searchModel.B2BResult = new B2BHotelListResponse
                { HotelList = _B2BHotelListResponses.ToArray() };
            }

            DisplayHotelSetting displayHotelSetting = searchModel?.DisplayHotelSetting ?? new DisplayHotelSetting();

            searchModel?.B2BResult?.HotelList?.FirstOrDefault(x =>
            {
                var item = x.SupplierHotels.FirstOrDefault(y => y.hotelId.ToLower() == htid?.ToLower());

                if (item != null)
                {
                    foreach (var supp in x.SupplierHotels)
                    {
                        decimal lowRate = 999999m;

                        var _roomList = supp.RoomRateDetailsList?.SelectMany(r => r.RateInfos) ?? new List<RateInfo> { };
                        IEnumerable<RateInfo> _updateRoomRatePerRoom = null;

                        CheckPointRatePerRoom:
                        // Use updated List to reloop the result.
                        _roomList = _updateRoomRatePerRoom != null ? _updateRoomRatePerRoom : _roomList;

                        bool isSingleRoomQuote = item.hotelSupplier != HotelSupplier.Expedia;

                        foreach (var _room in _roomList)
                        {
                            var rateRoom = _room.chargeableRateInfo.RatePerRoom;

                            if (rateRoom == null)
                            {
                                _updateRoomRatePerRoom = Alphareds.Module.HotelController.HotelServiceController.CalcRatePerRoom(_roomList, searchModel.TotalStayDays, searchModel.NoOfRoom, isSingleRoomQuote);
                                goto CheckPointRatePerRoom;
                            }

                            if (displayHotelSetting.AsAllNight && displayHotelSetting.AsIncludedTax)
                            {
                                lowRate = lowRate > rateRoom.AllInRate ? rateRoom.AllInRate : lowRate;
                            }
                            else if (displayHotelSetting.AsAllNight)
                            {
                                lowRate = lowRate > rateRoom.AllNightRate ? rateRoom.AllNightRate : lowRate;
                            }
                            else if (displayHotelSetting.AsIncludedTax)
                            {
                                lowRate = lowRate > rateRoom.IncludeTaxRate ? rateRoom.IncludeTaxRate : lowRate;
                            }
                            else
                            {
                                lowRate = lowRate > rateRoom.AvgRate ? rateRoom.AvgRate : lowRate;
                            }
                        }

                        var _outputObj = new ExpandoObject() as IDictionary<string, Object>;
                        _outputObj.Add("HID", supp.hotelId);
                        _outputObj.Add("Curr", supp.rateCurrencyCode);
                        _outputObj.Add("Price", lowRate != 999999m ? lowRate : supp.lowRate);
                        _outputObj.Add("Source", Alphareds.Module.Cryptography.Cryptography.AES.Encrypt(supp.hotelSupplier.ToString()));

                        _priceObj.Add(_outputObj);
                    }

                    return true;
                }

                return false;
            });

            if (_priceObj.Count == 0)
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }

            string srObj = JsonConvert.SerializeObject(_priceObj);
            return Content(srObj, "json");
        }

        private void ParamChecker()
        {
            /*
             * 1) Check Destination
             * 2) If not exist cache any ignore date pax
             * 3) Display dump result first
             * 4) Perform search
             * 6) Await search complete
             * 7) Replace result at frontend
             */

            var _dumpCacheList = System.Web.HttpContext.Current.Cache[DumpListCacheKey];

            if (_dumpCacheList == null)
            {
                List<SearchHotelModel> _cacheList = new List<SearchHotelModel>();

                _cacheList.Add(searchModel);

                System.Web.HttpContext.Current.Cache.Add(DumpListCacheKey, _cacheList, null, System.Web.Caching.Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(30), System.Web.Caching.CacheItemPriority.Default, null);
            }
            else
            {
                var _converted = (List<SearchHotelModel>)_dumpCacheList;
            }
        }
    }
}