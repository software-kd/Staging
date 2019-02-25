using Alphareds.Module.Common;
using Alphareds.Module.CarRentalWebService.CRWS;
using Alphareds.Module.Model;
using Alphareds.Module.ServiceCall;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web;
using PagedList;
using System.Linq;
using static Alphareds.Module.Model.ProductCarRental;
using System.Text;
using Alphareds.Module.Model.Database;

namespace Mayflower.Controllers
{
    [Filters.PreserveQueryStringFilter(QueryString = "tripid")]
    public class CarRentalController : AsyncController
    {
        private string tripid { get; set; }
        private Logger logger = LogManager.GetCurrentClassLogger();
        private Logger Logger { get; set; }
        private Alphareds.Module.Event.Function.DB EventDBFunc { get; set; }
        private Mayflower.General.CustomPrincipal CustomPrincipal => User as Mayflower.General.CustomPrincipal;


        public CarRentalController()
        {
            Logger = LogManager.GetCurrentClassLogger();
            EventDBFunc = new Alphareds.Module.Event.Function.DB(Logger);

            var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
            var routeValue = request.RequestContext.RouteData.Values["tripid"];
            string routeString = routeValue?.ToString();
            tripid = System.Web.HttpContext.Current.Request.QueryString["tripid"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["tripid"]);

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

        #region Step 2 - Search
        public ActionResult GetCarRentalSearch(ProductCarRental.SearchCriteria model)
        {
            string tripid = Guid.NewGuid().ToString();

            if (Request.Form["locationCodeCMS"] != null)
            {
                model.locationCode = Request.Form["locationCodeCMS"] == null ? null : Request.Form["locationCodeCMS"].ToString();
                model.PickupDateTime = DateTime.ParseExact(Request.Form["PickupDateTimeCMS"].ToString(), "dd-MMM-yyyy h:mm:ss tt", null);
                model.ReturnDateTime = DateTime.ParseExact(Request.Form["ReturnDateTimeCMS"].ToString(), "dd-MMM-yyyy h:mm:ss tt", null);
            }
            model.PickupDateTime = model.PickupDateTime == DateTime.MinValue ? DateTime.Now.AddDays(1) : model.PickupDateTime;
            model.ReturnDateTime = model.ReturnDateTime == DateTime.MinValue ? DateTime.Now.AddDays(2) : model.ReturnDateTime;
            model.RentalPeriod = model.PickupDateTime.ToString("M/dd") + " - " +  model.ReturnDateTime.ToString("M/dd");
            Core.SetSession(Enumeration.SessionName.CarRentalSearchReq, tripid, model);
            return RedirectToAction("Search", "CarRental", new { tripid });
        }

        public ActionResult Search(ProductCarRental.SearchCriteria model)
        {
            model = model ?? new ProductCarRental.SearchCriteria();
            FilterHotelResultModel filterResult = new FilterHotelResultModel();

            if (Core.GetSession(Enumeration.SessionName.CarRentalSearchReq, tripid) != null)
            {
                model = (ProductCarRental.SearchCriteria)Core.GetSession(Enumeration.SessionName.CarRentalSearchReq, tripid);
            }
            else if (model.PickupDateTime != DateTime.MinValue && model.ReturnDateTime != DateTime.MinValue && Core.GetSession(Enumeration.SessionName.CarRentalSearchReq, tripid) == null)
            {
                Core.SetSession(Enumeration.SessionName.CarRentalSearchReq, tripid, model);
            }
            else
            {
                // GO
                model.PickupDateTime = DateTime.Now.AddDays(double.Parse(Core.GetSettingValue("dayadvance")));
                // RETURN
                model.ReturnDateTime = model.PickupDateTime.AddDays(1);
            }

            #region If error from Reserve, show message.
            if (Core.GetSession(Enumeration.SessionName.ErrorMessage) != null)
            {
                ViewData.Add("ERRMSG", Core.GetSession(Enumeration.SessionName.ErrorMessage).ToString());
                Core.SetSession(Enumeration.SessionName.ErrorMessage, null);
            }
            #endregion

            CheckoutProduct checkoutmodel = new CheckoutProduct();
            if (Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid) != null)
            {
                checkoutmodel = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            }
            if (checkoutmodel.CarRental == null)
            {
                checkoutmodel.InsertProduct(new ProductCarRental
                {
                    SearchInfo = model,
                });
                Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkoutmodel);
            }
            var locationList = CarsRentalServiceCall.GetBranchList();
            ViewBag.locationList = locationList;
            return View(checkoutmodel);
        }

        [HttpPost]
        public async Task<ActionResult> GetCarRentalList(int? page, FormCollection collection, ProductCarRental.FilterCarParamModel filterParamModel, string tripid, string rType = null, string newsearch = null)
        {
            collection = collection ?? new FormCollection();
            CheckoutProduct CheckoutModel = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            CheckoutModel = CheckoutModel ?? new CheckoutProduct();
            var model = CheckoutModel.CarRental;
            model.SearchInfo.CurrentViewPage = (page ?? model.SearchInfo.CurrentViewPage);
            int pageNumber = model.SearchInfo.CurrentViewPage;
            int pageSize = 10;
            int.TryParse(Core.GetAppSettingValueEnhanced("RecordsPerPage"), out pageSize);

            try
            {
                if ((model.SearchInfo.locationCode != null && model.SearchInfo.PickupDateTime != DateTime.MinValue && model.SearchInfo.ReturnDateTime != DateTime.MinValue) && page == null && (model.Result == null || newsearch == "1"))
                {
                    List<string> pushEvErrMsg = new List<string>();
                    model.Result = await GetCarRentalListFromSearchInfo(model.SearchInfo);

                    if (model.Result?.Errors != null && model.Result?.Errors?.ErrorMessage != "No record found!")
                    {
                        var _jsonSetting = new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        };
                        Logger.Error(Environment.NewLine + "Error return from search car rental service." +
                            Environment.NewLine +
                            Environment.NewLine + JsonConvert.SerializeObject(model, Formatting.Indented, _jsonSetting));

                        ViewBag.SysErrMsg = "Sorry, unexpected error occur.";
                    }
                    else if (model.Result?.Errors?.ErrorMessage == "No record found!")
                    {
                        ViewBag.SysErrMsg = "No record found!";
                    }
                }

                FilterCarResultModel filtResult = new FilterCarResultModel();
                IPagedList<VehVendorAvail> IPagedModel = null;
                if(model.Result != null && model.Result.Success == "true")
                {
                    if (Core.GetSession(Enumeration.SessionName.FilterCarRentalResult, tripid) == null || filterParamModel != null && (filterParamModel.Rating != null || filterParamModel.minPrice != null ||
                        filterParamModel.maxPrice != null || filterParamModel.CarModel != null || filterParamModel.SortBy != null))
                    {
                        filtResult = UpdateFilter(model, collection, filterParamModel, tripid);
                        Core.SetSession(Enumeration.SessionName.FilterCarRentalResult, tripid, filtResult);
                        IPagedModel = filtResult.FilResult.ToPagedList(1, pageSize);  // every new filter also need reset page number to 1
                    }
                    else
                    {
                        filtResult = (FilterCarResultModel)Core.GetSession(Enumeration.SessionName.FilterCarRentalResult, tripid);
                        IPagedModel = filtResult.FilResult.ToPagedList(pageNumber, pageSize);
                    }
                    filtResult.MarkupInfo = GetMarkup(model.SearchInfo);

                    ViewData.Add("RESULT", filtResult.FilResult.Count > 0 ? filtResult.FilResult : null);
                    filtResult.IPagedCarList = IPagedModel;
                    model.FilterCarResult = filtResult;
                }
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Debug(ex, "GetCarRentalList()");
            }
            return Request.IsAjaxRequest() ? (ActionResult)PartialView("~/Views/CarRental/_CarRentalList.cshtml", model) : RedirectToAction("Search");
        }
        #endregion Step 2 - Search

        [HttpPost]
        public ActionResult ReserveCar(string data, string tripid, CarSelectedModel VehicleSelect)
        {
            try
            {
                var carAttr = Request.Form["key"]?.ToString() ?? "";

                string carSelectedSerialize = Request.QueryString["key"] ?? HttpUtility.UrlDecode(carAttr);
                CarSelectedModel carSelected = JsonConvert.DeserializeObject<CarSelectedModel>(carSelectedSerialize);

                int promoCodeId = 0;
                var checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
                checkout.CarRental.VehicleDetails = new VehicleDetail()
                {
                     VehicleID = carSelected.VehicleID.ToInt()
                };
                var insurance = checkout.CarRental.VehicleSelected.Insurance.FirstOrDefault();
                var vehicle = checkout.CarRental.VehicleSelected.VehAvails.VehAvailCore.Vehicle;
                var MarkupInfo = checkout.CarRental.FilterCarResult.MarkupInfo;
                int totalDays = Convert.ToInt32((checkout.CarRental.SearchInfo.ReturnDateTime - checkout.CarRental.SearchInfo.PickupDateTime).TotalDays);
                var TotalDiscountOrMarkup = MarkupInfo != null ? (MarkupInfo.MarkupPricingTypeCode == "PCT" ? MarkupInfo.DiscountOrMarkup * vehicle.VehicleCharge.TotalRentalFee.Value : (MarkupInfo.MarkupPricingTypeCode == "FIX" ? MarkupInfo.DiscountOrMarkup * totalDays : 0)) :0;
                var reservePricingDetail = new ProductPricingDetail
                {
                    Items = new List<ProductItem>()
                    {
                         new ProductItem()
                         {
                             ItemDetail = vehicle.VehicleName,
                             ItemQty = 1,
                             GST = 0,
                             Surcharge = 0,
                             BaseRate = vehicle.VehicleCharge.TotalRentalFee.Value + TotalDiscountOrMarkup,
                             Supplier_TotalAmt = vehicle.VehicleCharge.TotalRentalFee.Value,
                         },
                    },
                    Currency = "MYR",
                    Sequence = 6,
                };
                if(insurance != null)
                {
                    reservePricingDetail.Items.Add(new ProductItem()
                    {
                        ItemDetail = "Insurance (" + insurance.Description + ")",
                        ItemQty = totalDays,
                        BaseRate = insurance.InsurancePrice,
                        Supplier_TotalAmt = insurance.InsurancePrice * totalDays,
                    });
                }
                checkout.CheckoutStep = 3;
                checkout.PromoID = promoCodeId;
                checkout.IsRegister = false;
                checkout.ImFlying = false;
                checkout.RequireInsurance = false;
                checkout.BusinessType = IsAgentUser ? BusinessType.B2B : BusinessType.B2C;
                checkout.CarRental.PricingDetail = reservePricingDetail;
                Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkout);
                return RedirectToAction("GuestDetails", "Checkout", new { tripid });
            }
            catch (AggregateException ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, ex.ToString());
                return RedirectToAction("Search", "CarRental", new { reference = "error", tripid });
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(ex, "Error on ReserveRoom(FormCollection collection)");
                return RedirectToAction("Search", "CarRental", new { reference = "error", tripid });
            }
        }

        public ActionResult CarGuestDetails()
        {
            return View();
        }

        #region Car Rental List Filter
        private FilterCarResultModel UpdateFilter(ProductCarRental model, FormCollection collection, FilterCarParamModel filterParamModel, string tripid)
        {
            List<VehVendorAvail> carList = new List<VehVendorAvail>();
            var result = model.Result.VehAvailRSCore.VehVendorAvails.DeepCopy();
            carList.AddRange(result.VehVendorAvail);
            List<VehVendorAvail> _preProcessedList = carList;
            int totaldays = Convert.ToInt32((model.Result.VehAvailRSCore.VehRentalCore.ReturnDateTime.Value.Date - model.Result.VehAvailRSCore.VehRentalCore.PickUpDateTime.Value.Date).TotalDays);

            FilterCarResultModel filterModel = new FilterCarResultModel();
            if (Core.GetSession(Enumeration.SessionName.FilterHotelResult, tripid) != null)
            {
                filterModel = (FilterCarResultModel)Core.GetSession(Enumeration.SessionName.FilterCarRentalResult, tripid);
            }

            if(filterParamModel != null)
            {
                #region STAR rating
                if (!string.IsNullOrEmpty(filterParamModel.Rating))
                {
                    decimal _rateStarParse = filterParamModel.Rating.ToDecimal();
                    _preProcessedList = _preProcessedList.Where(x=>x.VehAvails.VehAvailCore.Vehicle.OctaneRating.ToString() == filterParamModel.Rating).ToList();
                }
                #endregion

                #region Property name
                if (!string.IsNullOrWhiteSpace(filterParamModel.CarModel))
                {
                    string PropertyName = filterParamModel.CarModel;
                    _preProcessedList = _preProcessedList.Where(x => x.VehAvails.VehAvailCore.Vehicle.VehicleName.ToLower().Contains(PropertyName.ToLower()) || x.VehAvails.VehAvailCore.Vehicle.VehicleModel.Model.ToLower().Contains(PropertyName.ToLower()) || x.VehAvails.VehAvailCore.Vehicle.VehicleGroup.GroupName.ToLower().Contains(PropertyName.ToLower())).ToList();
                }
                #endregion

                #region SortBy vehicle group
                if (!string.IsNullOrWhiteSpace(filterParamModel.SortBy))
                {
                    string vehicleGrp = filterParamModel.SortBy;
                    _preProcessedList = _preProcessedList.Where(x => x.VehAvails.VehAvailCore.Vehicle.VehicleGroup.GroupName == vehicleGrp).ToList();
                }
                #endregion

                #region Location Near Filter
                if (!string.IsNullOrWhiteSpace(filterParamModel.minPrice) && !string.IsNullOrWhiteSpace(filterParamModel.maxPrice))
                {
                    decimal MinPrice = filterParamModel.minPrice.ToDecimal();
                    decimal MaxPrice = filterParamModel.maxPrice.ToDecimal();
                    _preProcessedList = _preProcessedList.Where(x => x.VehAvails.VehAvailCore.Vehicle.VehicleGroup.SeatNumber >= MinPrice && x.VehAvails.VehAvailCore.Vehicle.VehicleGroup.SeatNumber <= MaxPrice).ToList();
                }
                #endregion

            }

            // Execute result
            filterModel.FilResult = _preProcessedList;
            filterModel.FilterSettings = filterParamModel;
            filterModel.FilterSettings.defaultMin = result.VehVendorAvail.Min(x => x.VehAvails.VehAvailCore.Vehicle.VehicleGroup.SeatNumber) ?? 0;
            filterModel.FilterSettings.defaultMax = result.VehVendorAvail.Max(x => x.VehAvails.VehAvailCore.Vehicle.VehicleGroup.SeatNumber) ?? 0;
            Core.SetSession(Enumeration.SessionName.FilterCarRentalResult, tripid, filterModel);

            return filterModel;
        }

        #endregion Car Rental List Filter

        private MarkupInfo GetMarkup(ProductCarRental.SearchCriteria model)
        {
            MayFlower db = new MayFlower();
            var markup = db.MarkupCarRentals.FirstOrDefault(x => x.IsActive && x.BookingDateFrom <= DateTime.Now && x.BookingDateTo >= DateTime.Now && x.TravelDateFrom <= model.PickupDateTime && x.TravelDateTo >= model.ReturnDateTime);
            MarkupInfo markupDtl = new MarkupInfo();
            if (markup != null)
            {
                markupDtl = new MarkupInfo()
                {
                    MarkupID = markup.MarkupCarRentalID,
                    DiscountOrMarkup = markup.DiscountOrMarkup,
                    MarkupPricingTypeCode = markup.MarkupPricingTypeCode
                };
            }
            return markupDtl;
        }


        #region Task Service Call
        public async Task<OTA_VehAvailRateMoreRS> GetCarRentalListFromSearchInfo(ProductCarRental.SearchCriteria model)
        {
            return await Task<OTA_VehAvailRateMoreRS>.Factory.StartNew(() =>
            {
                return CarsRentalServiceCall.GetCarRentalList(model);
            });
        }
        #endregion Task Service Call

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
    }
}