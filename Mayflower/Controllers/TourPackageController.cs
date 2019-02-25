using Alphareds.Module.Common;
using Alphareds.Module.CommonController;
using Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Alphareds.Module.JacTravelWebService.JTWS;
using Alphareds.Module.HotelController;
using Alphareds.Module.MemberController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.PaymentController;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.TourplanWebService.TPWS;
using Alphareds.Module.HotelBedsWebService.HBWS;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Linq.Expressions;
using WebGrease.Css.Extensions;
using Mayflower.Filters;
using System.Data.SqlClient;
using System.Text;
using Alphareds.Module.BookingController;
using System.Web.Script.Serialization;
using Alphareds.Module.Cryptography;
using System.Collections;

namespace Mayflower.Controllers
{
    public class TourPackageController : AsyncController
    {
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Alphareds.Module.Event.Function.DB eventDBFunc = new Alphareds.Module.Event.Function.DB(logger);

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
        private static string affiliationId
        {
            get
            {
                var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
                var routeValue = request.RequestContext.RouteData.Values["affiliationId"];
                string routeString = routeValue != null ? routeValue.ToString() : null;

                string obj = System.Web.HttpContext.Current.Request.QueryString["affiliationId"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["affiliationId"]);
                return obj;
            }
        }

        public TourPackageController()
        {
        }

        // Hijack controller context from another controller for User principal usage.
        public TourPackageController(ControllerContext controllerContext)
        {
            this.ControllerContext = controllerContext;
        }

        public ActionResult GetTourPackage(int TourPackageID)
        {
            string tripid = Guid.NewGuid().ToString();
            var tour = new ProductTourPackage(TourPackageID);
            var langList = UtilitiesService.GetTourLanguageList(tour.TourPackageID);
            var entrances = tour.TourPackageDetails.EntranceTickets.ToList();
            var transportDetail = tour.TourPackageDetails.TransportPackages.ToList();
            if (!((langList != null && langList.Count > 0) || (entrances != null && entrances.Count > 0) || (transportDetail != null && transportDetail.Count > 0)))
            {
                tour.skipAddon = true;
            }
            CheckoutProduct checkout = new CheckoutProduct();
            checkout.InsertProduct(tour);
            Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkout);
            return RedirectToAction("Search", "TourPackage", new { tripid });
        }

        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Search(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            return View(checkout);
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Search(ProductTourPackage prodTour, string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            int TourPackageID = checkout.TourPackage.TourPackageID;
            var tourprod = new ProductTourPackage(TourPackageID);
            checkout.TourPackage.TourPackagesInfo = prodTour.TourPackagesInfo;

            if (tourprod.TourPackageDetails.PackageHotels.Count == 0 || tourprod.TourPackageDetails.PackageRoomTypes.Count == 0)
            {
                var tourProdDetail = checkout.TourPackage.TourPackageDetails;
                int NoOfPax = checkout.TourPackage.TourPackagesInfo.NoOfPax;

                List<ProductItem> tourproducts = new List<ProductItem>();
                tourproducts.Add(new ProductItem()
                {
                    ItemDetail = tourProdDetail.TourPackageName,
                    ItemQty = NoOfPax,
                    BaseRate = tourProdDetail.SellingPrice,
                    Surcharge = 0,
                    Supplier_TotalAmt = tourProdDetail.Cost * NoOfPax,
                    GST = 0,
                });

                if (checkout.TourPackage.TourPackagesInfo.ExtensionNight != 0)
                {
                    int totalextension = checkout.TourPackage.TourPackagesInfo.ExtensionNight;
                    tourproducts.Add(new ProductItem()
                    {
                        ItemDetail = "Extension",
                        ItemQty = totalextension,
                        BaseRate = tourProdDetail.ExtensionPrice ?? 0,
                        Surcharge = 0,
                        Supplier_TotalAmt = (tourProdDetail.ExtensionPrice ?? 0) * totalextension,
                        GST = 0,
                    });
                }

                ProductPricingDetail tourPricingDetail = new ProductPricingDetail
                {
                    Sequence = 5,
                    Currency = "MYR",
                    Items = tourproducts,
                    Discounts = new List<DiscountDetail>(),
                };
                ProductTourPackage producttour = new ProductTourPackage()
                {
                    TourPackageID = TourPackageID,
                    TourPackageDetails = checkout.TourPackage.TourPackageDetails,
                    TourPackagesInfo = checkout.TourPackage.TourPackagesInfo,
                    ProductSeq = 5,
                    PricingDetail = tourPricingDetail,
                };
                if (checkout.TourPackage != null)
                {
                    checkout.RemoveProduct(ProductTypes.TP);
                }
                checkout.InsertProduct(producttour);

                var langList = UtilitiesService.GetTourLanguageList(TourPackageID);
                var entrances = tourProdDetail.EntranceTickets.ToList();
                var transportDetail = tourProdDetail.TransportPackages.ToList();
                if (!((langList != null && langList.Count > 0) || (entrances != null && entrances.Count > 0) || (transportDetail != null && transportDetail.Count > 0)))
                {
                    checkout.TourPackage.skipAddon = true;
                }
                if (checkout.TourPackage.skipAddon || true)
                {
                    return RedirectToAction("Contact", "TourPackage", new { tripid, affiliationId });
                }
                else
                {
                    return RedirectToAction("Addon", "TourPackage", new { tripid, affiliationId });
                }
            }
            else
            {
                return RedirectToAction("Hotel", "TourPackage", new { tripid, affiliationId });
            }
        }

        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Hotel(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            return View(checkout);
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Hotel(string travelerdata, ProductTourPackage prodTour, string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            int TourPackageID = checkout.TourPackage.TourPackageID;
            int NoOfPax = checkout.TourPackage.TourPackagesInfo.NoOfPax;
            var tourProdDetail = checkout.TourPackage.TourPackageDetails;
            bool isEmptyHotelToken = string.IsNullOrWhiteSpace(travelerdata);

            List<TourRoomModel> TourRoomPackages = isEmptyHotelToken ? new List<TourRoomModel>() : JsonConvert.DeserializeObject<List<TourRoomModel>>(travelerdata);
            foreach (var room in TourRoomPackages)
            {
                room.RoomTypeName = tourProdDetail.PackageRoomTypes.FirstOrDefault(x => x.RoomTypeID == room.RoomTypeID).RoomTypeName;
            }
            checkout.TourPackage.TourPackagesInfo.TourRoomPackages = TourRoomPackages;
            checkout.TourPackage.TourPackagesInfo.HotelID = TourRoomPackages.FirstOrDefault().hotelID;
            checkout.TourPackage.TourPackagesInfo.RatingID = tourProdDetail.PackageHotels.FirstOrDefault(x => x.HotelID == checkout.TourPackage.TourPackagesInfo.HotelID).RatingID;

            List<PeakDates> PeakDateList = GetDates(checkout.TourPackage.TourPackagesInfo.TravelDateFrom, checkout.TourPackage.TourPackagesInfo.TravelDateTo, checkout.TourPackage.TourPackageDetails, checkout.TourPackage.TourPackagesInfo);
            List<ProductItem> tourproducts = new List<ProductItem>();
            tourproducts.Add(new ProductItem()
            {
                ItemDetail = tourProdDetail.TourPackageName,
                ItemQty = NoOfPax,
                BaseRate = tourProdDetail.SellingPrice,
                Surcharge = 0,
                Supplier_TotalAmt = tourProdDetail.Cost * NoOfPax,
                GST = 0,
            });
            foreach (var room in TourRoomPackages)
            {
                var roomdetail = tourProdDetail.PackageRoomTypes.Where(x => x.RoomTypeID == room.RoomTypeID).FirstOrDefault();
                tourproducts.Add(new ProductItem()
                {
                    ItemDetail = roomdetail.RoomTypeName + " room",
                    ItemQty = room.Qty,
                    BaseRate = roomdetail.PackageRoomPrices.FirstOrDefault(x=>x.RoomPriceID == room.RoomPriceID).SellingPrice,
                    Surcharge = 0,
                    Supplier_TotalAmt = roomdetail.Cost * room.Qty,
                    GST = 0,
                });
            }
            if (checkout.TourPackage.TourPackagesInfo.ExtensionNight != 0)
            {
                int totalextension = checkout.TourPackage.TourPackagesInfo.ExtensionNight;
                tourproducts.Add(new ProductItem()
                {
                    ItemDetail = "Extension",
                    ItemQty = totalextension,
                    BaseRate = tourProdDetail.ExtensionPrice ?? 0,
                    Surcharge = 0,
                    Supplier_TotalAmt = (tourProdDetail.ExtensionPrice ?? 0) * totalextension,
                    GST = 0,
                });
            }

            if (PeakDateList.Count > 0)
            {
                tourproducts.Add(new ProductItem()
                {
                    ItemDetail = "Peak season surcharge",
                    ItemQty = 1,
                    BaseRate = PeakDateList.FirstOrDefault().PeakSurcharge,
                    Surcharge = 0,
                    Supplier_TotalAmt = PeakDateList.FirstOrDefault().PeakSurcharge,
                    GST = 0,
                });
            }
          
            ProductPricingDetail tourPricingDetail = new ProductPricingDetail
            {
                Sequence = 5,
                Currency = "MYR",
                Items = tourproducts,
                Discounts = new List<DiscountDetail>(),
            };
            ProductTourPackage producttour = new ProductTourPackage()
            {
                TourPackageID = TourPackageID,
                ContactPerson = checkout.ContactPerson,
                TourPackageDetails = checkout.TourPackage.TourPackageDetails,
                TourPackagesInfo = checkout.TourPackage.TourPackagesInfo,
                ProductSeq = 5,
                PricingDetail = tourPricingDetail,
            };
            if (checkout.TourPackage != null)
            {
                checkout.RemoveProduct(ProductTypes.TP);
            }
            checkout.InsertProduct(producttour);

            var langList = UtilitiesService.GetTourLanguageList(TourPackageID);
            var entrances = tourProdDetail.EntranceTickets.ToList();
            var transportDetail = tourProdDetail.TransportPackages.ToList();
            if (!((langList != null && langList.Count > 0) || (entrances != null && entrances.Count > 0) || (transportDetail != null && transportDetail.Count > 0)))
            {
                checkout.TourPackage.skipAddon = true;
            }
            if (checkout.TourPackage.skipAddon || true)
            {
                return RedirectToAction("Contact", "TourPackage", new { tripid, affiliationId });
            }
            else
            {
                return RedirectToAction("Addon", "TourPackage", new { tripid, affiliationId });
            }
        }

        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Addon(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            return View(checkout);
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Addon(ProductTourPackage prodTour, string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            checkout.TourPackage.TourPackagesInfo.EntranceID = prodTour.TourPackagesInfo.EntranceID;
            checkout.TourPackage.TourPackagesInfo.LanguageID = prodTour.TourPackagesInfo.LanguageID;
            checkout.TourPackage.TourPackagesInfo.SpecialRequest = prodTour.TourPackagesInfo.SpecialRequest;
            checkout.TourPackage.TourPackagesInfo.TransportPackageID = prodTour.TourPackagesInfo.TransportPackageID;

            int NoOfPax = checkout.TourPackage.TourPackagesInfo.NoOfPax;
            var tourProdDetail = checkout.TourPackage.TourPackageDetails;
            checkout.TourPackage.PricingDetail.Items.RemoveAll(x => x.ItemDetail.Contains("Tour Guide:") || x.ItemDetail.Contains("Entrances Ticket:") || x.ItemDetail.Contains("Transport Package: "));
            if (checkout.TourPackage.TourPackagesInfo.LanguageID != 0)
            {
                var languages = UtilitiesService.GetTourLanguageList(checkout.TourPackage.TourPackagesInfo.LanguageID);
                var lang = languages.FirstOrDefault(x => x.LanguageID == checkout.TourPackage.TourPackagesInfo.LanguageID);
                checkout.TourPackage.PricingDetail.Items.Add(new ProductItem()
                {
                    ItemDetail = "Tour Guide: " + lang.Language,
                    ItemQty = 1,
                    BaseRate = lang.TourLanguagePrices.FirstOrDefault().SellingPrice,
                    Surcharge = 0,
                    Supplier_TotalAmt = lang.TourLanguagePrices.FirstOrDefault().Cost,
                    GST = 0,
                });
            }
            if (checkout.TourPackage.TourPackagesInfo.EntranceID != 0)
            {
                var entranceDetail = tourProdDetail.EntranceTickets.Where(x => x.EntranceID == checkout.TourPackage.TourPackagesInfo.EntranceID).FirstOrDefault();
                checkout.TourPackage.PricingDetail.Items.Add(new ProductItem()
                {
                    ItemDetail = "Entrances Ticket: " + entranceDetail.Ticket,
                    ItemQty = NoOfPax,
                    BaseRate = entranceDetail.SellingPrice,
                    Surcharge = 0,
                    Supplier_TotalAmt = entranceDetail.Cost * NoOfPax,
                    GST = 0,
                });
            }
            if (checkout.TourPackage.TourPackagesInfo.TransportPackageID != 0)
            {
                var transportDetail = tourProdDetail.TransportPackages.Where(x => x.TransportPackageID == checkout.TourPackage.TourPackagesInfo.TransportPackageID).FirstOrDefault();
                checkout.TourPackage.PricingDetail.Items.Add(new ProductItem()
                {
                    ItemDetail = "Transport Package: " + transportDetail.Description,
                    ItemQty = NoOfPax,
                    BaseRate = transportDetail.SellingPrice,
                    Surcharge = 0,
                    Supplier_TotalAmt = transportDetail.Cost * NoOfPax,
                    GST = 0,
                });
            }
            return RedirectToAction("Contact", "TourPackage", new { tripid, affiliationId });
        }

        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Contact(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            ViewBag.displayEnquiry = true;
            return View(checkout);
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Contact(ProductTourPackage prodTour, string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            checkout.TourPackage.ContactPerson = prodTour.ContactPerson;
            checkout.ContactPerson = prodTour.ContactPerson;
            checkout.TourPackage.TourPackagesInfo.ArriveFlightDetails = prodTour.TourPackagesInfo.ArriveFlightDetails;
            checkout.TourPackage.TourPackagesInfo.DepartFlightDetails = prodTour.TourPackagesInfo.DepartFlightDetails;

            return RedirectToAction("Payment", "Checkout", new { tripid, affiliationId });
        }

        [HttpPost]
        public bool TourPackageEnquiry(ProductTourPackage prodTour, string travelerdata, string enquiryQuestion, string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            bool successSend = false;
            try
            {
                if (checkout != null)
                {
                    checkout.TourPackage.ContactPerson = prodTour.ContactPerson;

                    var tourdtl = checkout.TourPackage.TourPackageDetails;
                    var tourinfo = checkout.TourPackage.TourPackagesInfo;
                    var hotelSelected = "";
                    if (tourinfo.RoomListSelected != null)
                    {
                        int star = UtilitiesService.GetTourRoomRating(tourinfo.RoomListSelected.FirstOrDefault().RoomTypeID);
                        hotelSelected = star + (star > 1 ? " stars" : " star") + "<br/>" + string.Join("<br/>", tourinfo.TourRoomPackages.Select(x => x.RoomTypeName + " x " + x.Qty));
                    }

                    decimal ttlprice = checkout.CheckOutSummary.GrandTtlAmt_BeforeDiscount;
                    decimal deposit = ttlprice;
                    decimal depositrate = tourdtl.DepositRate ?? 0;
                    if ((tourdtl.IsDepositOnly ?? false) && depositrate > 0)
                    {
                        deposit = tourdtl.DepositTypeCode == "FIX" ? depositrate * tourinfo.NoOfPax : ttlprice * depositrate / 100;
                    }

                    Hashtable ht = new Hashtable();
                    ht.Add("<#UserName>", prodTour.ContactPerson.GivenName + " " + prodTour.ContactPerson.Surname);
                    ht.Add("<#PackageName>", tourdtl.TourPackageName);
                    ht.Add("<#TravelDate>", tourinfo.TravelDateFrom.ToString("dd/MM/yyyy") + " - " + tourinfo.TravelDateTo.ToString("dd/MM/yyyy"));
                    ht.Add("<#HotelSelection>", hotelSelected);
                    ht.Add("<#TourCode>", tourdtl.TourPackageCode);
                    ht.Add("<#TtlPrice>", ttlprice.ToString("n2"));
                    ht.Add("<#Email>", checkout.TourPackage.ContactPerson.Email);
                    ht.Add("<#EnquiryQ>", enquiryQuestion);

                    var CSemail = Core.GetSettingValue("MayflowerCSEmail");
                    successSend = CommonServiceController.SendEmail(CSemail, "Ground Package Enquiry Email", Core.getMailTemplate("tourpackageenquiry", ht));
                    if (!successSend)
                    {
                        logger.Fatal("errmsg", "Tour Package send enquiry email error." + " - " + DateTime.Now.ToLoggerDateTime());
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Tour Package enquiry email error" + " - " + DateTime.Now.ToLoggerDateTime());
            }

            return successSend;
        }

        static public List<PeakDates> GetDates(DateTime start_date, DateTime end_date, TourPackageMaster tourpackage, TourPackageModels tourModel)
        {
            List<PeakDates> days_list = new List<PeakDates>();
            var hotelselected = tourpackage.PackageHotels.FirstOrDefault(x => x.HotelID == tourModel.HotelID);
            for (DateTime date = start_date; date <= end_date; date = date.AddDays(1))
            {
                foreach (var range in tourpackage.PackagePeakRanges)
                {
                    if (date >= range.DateStart && date <= range.DateEnd)
                    {
                        bool isRoomPeak = false;
                        range.HotelID = range.HotelID.Replace(" ", "");
                        range.RoomTypeID = range.RoomTypeID.Replace(" ", "");
                        List<string> hotelIDlist = range.HotelID.Split(',').ToList();
                        List<string> roomIDlist = range.RoomTypeID.Split(',').ToList();
                        foreach(var room in tourModel.TourRoomPackages)
                        {
                            isRoomPeak = roomIDlist.Contains(room.RoomTypeID.ToString());
                            if (isRoomPeak)
                            {
                                break;
                            }
                        }
                        if ((range.HotelID == "-" || hotelIDlist.Contains(tourModel.HotelID.ToString())) && (range.RoomTypeID == "-" || isRoomPeak))
                        {
                            days_list.Add(new PeakDates() { PeakDate = date, PeakSurcharge = range.PeakSurcharge });
                        }
                    }
                }
                if (days_list.Count == 0)
                {
                    var dayofweek = ((int)date.DayOfWeek).ToString();                   
                    if (hotelselected.PeakDays != null && hotelselected.PeakDays.Contains(dayofweek))
                    {
                        days_list.Add(new PeakDates() {  PeakDate = date, PeakSurcharge = hotelselected.PeakDaysSurcharge ?? 0 });
                    }
                }
            }

            return days_list;
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult UpdateTourPrice(string tripid, string Room = "")
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            dynamic obj = new System.Dynamic.ExpandoObject();

            bool isEmptyHotelToken = string.IsNullOrWhiteSpace(Room);
            List<TourRoomModel> TourRoomPackages = isEmptyHotelToken ? new List<TourRoomModel>() : JsonConvert.DeserializeObject<List<TourRoomModel>>(Room);
            var tourInfo = checkout.TourPackage.TourPackageDetails;
            int NoOfPax = checkout.TourPackage.TourPackagesInfo.NoOfPax;
            int extension = checkout.TourPackage.TourPackagesInfo.ExtensionNight;

            decimal TtlRoomPrice = 0;
            var displayhtml = "";
            decimal tourprice = tourInfo.SellingPrice * NoOfPax;
            decimal extensionPrice = extension * (tourInfo.ExtensionPrice ?? 0);
            int TtlPax = 0;
            if(TourRoomPackages.Count > 0)
            {
                var hotelname = tourInfo.PackageRoomTypes.FirstOrDefault(x => x.RoomTypeID == TourRoomPackages.FirstOrDefault().RoomTypeID).PackageHotel.HotelName;
                displayhtml += "<li class='reservation-room'><span>" + hotelname + "</span>";
                foreach (var roomtype in TourRoomPackages)
                {
                    decimal roomprice = tourInfo.PackageRoomTypes.FirstOrDefault(x => x.RoomTypeID == roomtype.RoomTypeID).PackageRoomPrices.FirstOrDefault(x => x.RoomPriceID == roomtype.RoomPriceID).SellingPrice * roomtype.Qty;
                    var RoomSelected = tourInfo.PackageRoomTypes.FirstOrDefault(x => x.RoomTypeID == roomtype.RoomTypeID);
                    TtlRoomPrice += roomprice;
                    TtlPax += RoomSelected.NoOfTravellers * roomtype.Qty;
                    displayhtml += "<li class='reservation-room'><span>" + RoomSelected.RoomTypeName + " room x " + roomtype.Qty + "</span>";
                    displayhtml += "<span>MYR" + roomprice.ToString("n2") + "</span></li>";
                }
            }
            
            displayhtml += "<li class='reservation-room'><span>Extension x " + extension + "</span>";
            displayhtml += "<span>MYR" + extensionPrice.ToString("n2") + "</span></li>";
            checkout.TourPackage.TourPackagesInfo.TtlOccupancy = TtlPax;

            decimal ttlprice = tourprice + TtlRoomPrice + extensionPrice;
            obj.ttl = ttlprice.ToString("n2");
            obj.ttlper = NoOfPax != 0 ? (ttlprice / NoOfPax).ToString("n2") : "0.00";
            obj.roomdesc = displayhtml;
            obj.ttlpax = TtlPax;
            return Content(JsonConvert.SerializeObject(obj), "application/json");
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult UpdateTourAddonPrice(string tripid, int transportID = 0, int entranceID = 0, int languageID = 0)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            dynamic obj = new System.Dynamic.ExpandoObject();

            var tourInfo = checkout.TourPackage.TourPackageDetails;
            int NoOfPax = checkout.TourPackage.TourPackagesInfo.NoOfPax;

            var displayhtml = "";
            decimal tourprice = checkout.TourPackage.PricingDetail.ProductTotalAmount;
            decimal entrancePrice = 0;
            decimal transportPrice = 0;
            decimal languagePrice = 0;

            if (transportID != 0)
            {
                var transportDetail = tourInfo.TransportPackages.Where(x => x.TransportPackageID == transportID).FirstOrDefault();
                transportPrice = transportDetail.SellingPrice * NoOfPax;
                displayhtml += "<li class='reservation-room tpaddon'><span>" + transportDetail.Description + " x " + NoOfPax + "</span>";
                displayhtml += "<span>MYR" + transportPrice.ToString("n2") + "</span></li>";
            }
            if (entranceID != 0)
            {
                var entranceDetail = tourInfo.EntranceTickets.Where(x => x.EntranceID == entranceID).FirstOrDefault();
                transportPrice = entranceDetail.SellingPrice * NoOfPax;
                displayhtml += "<li class='reservation-room addon'><span>" + entranceDetail.Ticket + " x " + NoOfPax + "</span>";
                displayhtml += "<span>MYR" + transportPrice.ToString("n2") + "</span></li>";
            }
            if (languageID != 0)
            {
                var languages = UtilitiesService.GetTourLanguageList(checkout.TourPackage.TourPackagesInfo.LanguageID);
                var lang = languages.FirstOrDefault(x => x.LanguageID == checkout.TourPackage.TourPackagesInfo.LanguageID);
                transportPrice = lang.TourLanguagePrices.FirstOrDefault().SellingPrice * NoOfPax;
                displayhtml += "<li class='reservation-room addon'><span>" + lang.Language + "</span>";
                displayhtml += "<span>MYR" + transportPrice.ToString("n2") + "</span></li>";
            }

            decimal ttlprice = tourprice + entrancePrice + languagePrice + transportPrice;
            obj.ttl = ttlprice.ToString("n2");
            obj.ttlper = NoOfPax != 0 ? (ttlprice / NoOfPax).ToString("n2") : "0.00";
            obj.roomdesc = displayhtml;
            return Content(JsonConvert.SerializeObject(obj), "application/json");
        }
    }
}