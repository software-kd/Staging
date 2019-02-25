using Alphareds.Module.Common;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Alphareds.Module.Model;
using Alphareds.Module.ServiceCall;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Mayflower.Areas.SPAgent.Controllers
{
    public class SessionSetterController : AsyncController
    {
        private Logger logger { get; set; }
        private string tripid { get; set; }
        private SearchHotelModel searchModel { get; set; }
        private const string DumpListCacheKey = "HotelListCache";


        public SessionSetterController()
        {
            logger = LogManager.GetCurrentClassLogger();

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

        // GET: Agent/SessionSetter
        public async Task<ActionResult> Hotel(string usevs = "0")
        {
            if (searchModel == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            //if (usevs == "1" && searchModel.SearchProgress.Count == 0)
            //{
            //    if (GetCache(searchModel))
            //    {
            //        Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, searchModel);
            //        return Json(true, JsonRequestBehavior.AllowGet);
            //    };
            //}

            ESBHotelServiceCall.Search b2BSearch = new ESBHotelServiceCall.Search(searchModel);
            var supplierReflect = typeof(SearchSupplier).GetProperties();

            if (searchModel.SearchProgress.Count == 0)
            {
                InsertSelectedSearchSupplier(searchModel, supplierReflect, SearchProgress.Progress.New);
            }

            var normalSearch = Task.Run(async () =>
            {
                var _temp = searchModel.DeepCopy();
                _temp.SupplierIncluded = new SearchSupplier
                {
                    Expedia = true,
                };

                return await ESBHotelServiceCall.GetHotelListAsync(_temp);
            });

            var b2bSearch = Task.Run(async () =>
            {
                return await b2BSearch.GetB2BHotelListAsync();
            });

            List<Task> searchTask = new List<Task>();

            if (searchTask.Count == 0)
            {
                //searchTask.Add(normalSearch);
                //searchTask.Add(b2bSearch);

                if (searchModel.GetSearchProgress(Suppliers.Expedia) == SearchProgress.Progress.New)
                {
                    searchTask.Add(normalSearch);
                    searchModel.SetSearchProgress(Suppliers.Expedia, SearchProgress.Progress.Searching);
                }

                if (searchModel.SearchProgress.Any(x => x.GetProgress == SearchProgress.Progress.New))
                {
                    searchTask.Add(b2bSearch);
                }
            }

            if (searchTask.Count == 0)
            {
                return Json(true, JsonRequestBehavior.AllowGet);
            }

            try
            {
                int _tsk = Task.WaitAny(searchTask.ToArray());
                var modelType = searchTask[_tsk];


                string fullQName = modelType.GetType().FullName;
                if (fullQName == normalSearch.GetType().FullName)
                {
                    searchModel.Result = normalSearch.Result;
                    searchTask.RemoveAt(_tsk);
                    searchModel.SetSearchProgress(Suppliers.Expedia, SearchProgress.Progress.Complete);
                }
                else if (fullQName == b2bSearch.GetType().FullName)
                {
                    searchModel.B2BResult = b2bSearch.Result;
                    searchTask.RemoveAt(_tsk);
                    InsertSelectedSearchSupplier(searchModel, supplierReflect, SearchProgress.Progress.Complete);
                }

                SetCache(searchModel);

                Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, searchModel);

                return Json(true, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Content(ex.ToString());
            }
        }

        private void InsertSelectedSearchSupplier(SearchHotelModel _searchModel, System.Reflection.PropertyInfo[] supplierReflect, SearchProgress.Progress searchProgress)
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

            var _dumpCacheList = _GetCacheFromMem(DumpListCacheKey);

            if (_dumpCacheList == null)
            {
                List<SearchHotelModel> _cacheList = new List<SearchHotelModel>();

                _cacheList.Add(_SearchModel);

                System.Web.HttpContext.Current.Cache.Add(DumpListCacheKey, _cacheList, null, System.Web.Caching.Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(1), System.Web.Caching.CacheItemPriority.Default, null);
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


    }
}