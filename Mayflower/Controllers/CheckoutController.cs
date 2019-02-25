using Alphareds.Module.Common;
using Alphareds.Module.CommonController;
using Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;
using Alphareds.Module.HotelController;
using Alphareds.Module.MemberController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.PaymentController;
using Alphareds.Module.ServiceCall;
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
    public class CheckoutController : AsyncController
    {
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Alphareds.Module.Event.Function.DB eventDBFunc = new Alphareds.Module.Event.Function.DB(logger);

        private bool IsUseV2Layout { get; set; } = false;

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
        private string sessionFlightBooking = Enumeration.SessionName.FlightBooking + tripid;

        public CheckoutController()
        {
            var req = Request ?? System.Web.HttpContext.Current.Request.RequestContext.HttpContext.Request;
            IsUseV2Layout = Core.Setting.Layout.IsUseV2Layout || (Core.IsForStaging && req?.Cookies["version"]?.Value == "v2");
        }

        // Hijack controller context from another controller for User principal usage.
        public CheckoutController(ControllerContext controllerContext)
        {
            this.ControllerContext = controllerContext;
        }

        // GET: Checkout
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult GuestDetails(string tripid, string affiliationId)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            if (checkout == null)
            {
                checkout = new CheckoutProduct();
                checkout.ReferralCode = affiliationId;
            }
            else
            {
                checkout.ContactPerson = GetPassengerDetailPage(checkout.ContactPerson, CurrentUserID, CustomPrincipal);
                checkout.ReferralCode = affiliationId;
                MayFlower db = null;

                // Retrieve PromoCode Functions
                if (checkout.PromoID != 0)
                {
                    db = db ?? new MayFlower();
                    checkout.PromoCodeFunctions = new PromoCodeFunctions(checkout.PromoID, db);
                }

                if (checkout.IsDynamic)
                {
                    ApplyPCode(checkout.DPPromoCode, tripid, null, null, 3);
                }
                checkout.ContactPerson.IdentityDocuments = checkout.ContactPerson.IdentityDocuments ?? new ContactPerson.IdentityDocument();
                checkout.ContactPerson.LicenseInfo = checkout.ContactPerson.LicenseInfo ?? new LicenseInfo();

                if (User.Identity.IsAuthenticated) // only login user
                {
                    db = db ?? new MayFlower();

                    UserDetail userDtl = db.UserDetails.FirstOrDefault(x => x.UserID == CurrentUserID);
                    ViewData.Add("PASSPORTNO", userDtl.PassportNo);
                    ViewData.Add("PASSPORTEXP", userDtl.PassportExpiryDate);
                    ViewData.Add("PASSPORTCOUNTRY", userDtl.PassportIssueCountryCode);

                    if (userDtl.User.UserTypeCode == "AGT")
                    {
                        CustomizedField userCustomizedFld = userDtl.User.Organization.CustomizedFields.FirstOrDefault();
                        ViewData.Add("CUSTOMIZEDFIELD1", userCustomizedFld != null ? userCustomizedFld.CustomizedField1 : null);
                        ViewData.Add("CUSTOMIZEDFIELD2", userCustomizedFld != null ? userCustomizedFld.CustomizedField2 : null);
                        ViewData.Add("CUSTOMIZEDFIELD3", userCustomizedFld != null ? userCustomizedFld.CustomizedField3 : null);
                        ViewData.Add("CUSTOMIZEDFIELD4", userCustomizedFld != null ? userCustomizedFld.CustomizedField4 : null);
                    }
                }
            }

            if (checkout.Hotel != null)
            {
                if (checkout.Hotel.HasHotelBundleTicket)
                {
                    MayFlower db = new MayFlower();
                    string hotelid = checkout.Hotel.RoomDetails.First().HotelId;  
                    var hotelBundleTicketSet = db.HotelBundleTicketSets.FirstOrDefault(x => x.HotelID.ToString() == hotelid && x.isActive == true); // .HotelSelected.FirstOrDefault().hotelId
                    List<HotelBundleTicketTimeSlot> timeSlots = hotelBundleTicketSet.HotelBundleTicketTimeSlots.ToList();

                    //filter option
                    List<string> day = new List<string>();
                    for(var i = checkout.Hotel.SearchHotelInfo.ArrivalDate; i < checkout.Hotel.SearchHotelInfo.DepartureDate; i = i.AddDays(1))
                    {
                        day.Add(i.DayOfWeek.ToString());
                    }
                    hotelBundleTicketSet.HotelBundleTicketTimeSlots = hotelBundleTicketSet.HotelBundleTicketTimeSlots.Where(x => day.Any(y => y == x.Day)).ToList();

                    if(hotelBundleTicketSet.HotelBundleTicketTimeSlots.Count() > 0)
                    {
                        ViewBag.HotelBundleTicketSet = hotelBundleTicketSet;
                    }
                }
            }

            if (IsUseV2Layout || (checkout.AddOnProduct != null && checkout.Hotel == null && checkout.Flight == null && checkout.TourPackage == null))
            {
                return View("~/Views/Checkout/CustomerDetails.cshtml", checkout);
            }
            else
            {
                return View(checkout);
            }
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult GuestDetails(ProductHotel productHotel, ProductFlight productFlight,
             CheckoutProduct postCheckoutForm, string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            var contact = postCheckoutForm.ContactPerson;
            checkout.ContactPerson = contact ?? checkout.ContactPerson;

            if (checkout.Flight != null)
            {
                checkout.Flight.BookSeatInformation_Supplier_Request_Json = string.Empty;
                checkout.Flight.BookSeatInformation_Supplier_Response_Json = string.Empty;
                checkout.Flight.Temp_Booking_Info = string.Empty;
            }

            string postBackViewPath = IsUseV2Layout ? "~/Views/Checkout/CustomerDetails.cshtml" : "~/Views/Checkout/GuestDetails.cshtml";

            #region Binding POST data here
            if (checkout.Flight != null)
            {
                checkout.Flight.ContactPerson = contact;
                var passengerDetails = productFlight.TravellerDetails;

                for (int i = 0; i < checkout.Flight.TravellerDetails.Count; i++)
                {
                    var psg = checkout.Flight.TravellerDetails[i];

                    #region Flight Details (more and more... continue binding here)
                    // Frontend layout don't have these field which required to place book, so make it same as contact
                    psg.Email = contact.Email;
                    psg.Phone1 = contact.Phone1;
                    psg.Phone1LocationCode = contact.Phone1LocationCode;
                    psg.Phone2 = contact.Phone2;
                    psg.Phone2LocationCode = contact.Phone2LocationCode;

                    psg.Title = passengerDetails[i].Title;
                    psg.GivenName = passengerDetails[i].GivenName;
                    psg.Surname = passengerDetails[i].Surname;
                    psg.Address1 = passengerDetails[i].Address1;
                    psg.Address2 = passengerDetails[i].Address2;
                    psg.Address3 = passengerDetails[i].Address3;
                    psg.City = passengerDetails[i].City;
                    psg.PostalCode = passengerDetails[i].PostalCode;
                    psg.CountryCode = passengerDetails[i].CountryCode;
                    psg.Nationality = passengerDetails[i].Nationality;
                    psg.DOB = passengerDetails[i].DOB;

                    psg.IdentityNumber = passengerDetails[i].IdentityNumber;
                    psg.PassportNumber = passengerDetails[i].PassportNumber;
                    psg.PassportIssueCountry = passengerDetails[i].PassportIssueCountry;
                    psg.PassportExpiryDate = passengerDetails[i].PassportExpiryDate;
                    psg.FrequrntFlyerNoAirline = passengerDetails[i].FrequrntFlyerNoAirline;
                    psg.FrequentFlyerNo = passengerDetails[i].FrequentFlyerNo;
                    psg.InBoundSSR = passengerDetails[i].InBoundSSR;
                    psg.OutBoundSSR = passengerDetails[i].OutBoundSSR;

                    psg.PassengerType = passengerDetails[i].PassengerType;
                    psg.DOB = passengerDetails[i].DOB;
                    if (passengerDetails[i].HotelSpecialRequest != null)
                    {
                        passengerDetails[i].HotelSpecialRequest.IsAssign = false; // for bind multiple of same room type
                    }
                    psg.HotelSpecialRequest = passengerDetails[i].HotelSpecialRequest;
                    psg.CustomizedField1 = passengerDetails[i].CustomizedField1;
                    psg.CustomizedField2 = passengerDetails[i].CustomizedField2;
                    psg.CustomizedField3 = passengerDetails[i].CustomizedField3;
                    psg.CustomizedField4 = passengerDetails[i].CustomizedField4;
                    // more and more... binding here
                    #endregion
                }
                if (checkout.Hotel != null && checkout.Hotel.RoomDetails != null)
                {
                    //bind hotel contact and guest detail for dynamic packaging
                    checkout.Hotel.ContactPerson = contact;
                    // Frontend layout don't have these field which required to place book, so make it same as contact
                    for (int i = 0; i < checkout.Hotel.RoomDetails.Count; i++)
                    {
                        var guest = checkout.Hotel.RoomDetails[i];

                        #region Hotel Guest Details (more and more... continue binding here)
                        // Tourplan duplicate name hotfix
                        string nameNumeric = checkout.Hotel.RoomSelected.HotelRoomInformationList.FirstOrDefault().hotelSupplier == HotelSupplier.Tourplan ? "_" + i : "";
                        var flightpsg = passengerDetails.Where(x => x.HotelSpecialRequest != null && x.HotelSpecialRequest.ImStaying == true && x.HotelSpecialRequest.IsAssign == false && x.HotelSpecialRequest.RoomType.StartsWith(guest.RoomTypeCode)).ToList();
                        if (flightpsg.Count > 0)
                        {
                            // bind passenger form if I'm Staying checked
                            foreach (var psg in flightpsg.GroupBy(x => x.HotelSpecialRequest.RoomType))
                            {
                                guest.Title = psg.FirstOrDefault().Title;
                                guest.GivenName = psg.FirstOrDefault().GivenName;
                                guest.Surname = psg.FirstOrDefault().Surname + (i > 0 ? nameNumeric : "");
                                guest.DateOfBirth = psg.FirstOrDefault().DOB.ToDateOfBirthModel();
                                guest.SpecialRequest = new SpecialRequestModel
                                {
                                    SmokingPreferences = psg.FirstOrDefault().HotelSpecialRequest.SmokingPreferences,
                                    AdditionalRequest = psg.FirstOrDefault().HotelSpecialRequest.AdditionalRequest,
                                };
                                guest.CheckInMode = psg.FirstOrDefault().HotelSpecialRequest.CheckInMode;
                                guest.AdditionalRequest = psg.FirstOrDefault().HotelSpecialRequest.AdditionalRequest;
                                foreach (var assignpsg in psg)
                                {
                                    assignpsg.HotelSpecialRequest.IsAssign = true; //set to true after bind to hotel room
                                }
                                break;
                            }
                        }
                        else
                        {
                            // Use first guest as booking person.
                            var firstGuest = passengerDetails.First();

                            guest.Title = firstGuest.Title;
                            guest.GivenName = firstGuest.GivenName;
                            guest.Surname = firstGuest.Surname + (i > 0 ? nameNumeric : "");
                            guest.DateOfBirth = firstGuest.DOB.ToDateOfBirthModel();
                        }
                        // more and more... binding here
                        #endregion
                    }
                }

                //Bind SSR price here
                if (checkout.Flight.FlightInfo.Supplier == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia
                    && passengerDetails.Any(x => x.FlightSSRBooked))
                {
                    var avaSSR = checkout.Flight.FlightInfo.AvaSSR.SelectMany(x => x.FlightSegmentSSR);
                    var psgWithSSR = passengerDetails.Where(a => a.FlightSSRBooked);
                    bool isReturn = checkout.Flight.SearchFlightInfo.isReturn;
                    List<SSRModel> ssrList = new List<SSRModel>();
                    var ssrSelected = psgWithSSR.SelectMany(x =>
                    {
                        var selectedSSR = x.CheckOutSSR.SelectMany(a => a.TravellerSSR);

                        List<SegmentAvaiableSSR> ssrs = new List<SegmentAvaiableSSR>();

                        foreach (var selectSSR in selectedSSR)
                        {
                            var ssrSegment = avaSSR.FirstOrDefault(a => a.DepartureStation == selectSSR.DepartureStation && a.ArrivalStation == selectSSR.ArrivalStation);
                            var validSSR = ssrSegment?.SSR.FirstOrDefault(a => a.SSRCode == selectSSR.SSRCode);
                            ssrs.Add(validSSR);
                        }

                        return ssrs;
                    });

                    var ssrGroup = ssrSelected.GroupBy(x => new { ssrCode = x.SSRCode, nettPrice = x.NettPrice });

                    //Temporary fixes for back from step 4 causing duplicate SSR
                    List<string> psgType = new List<string> { "ADT", "CNN", "INF" };
                    checkout.Flight.PricingDetail.Items.RemoveAll(x => !psgType.Any(a => a == x.ItemDetail));

                    foreach (var ssr in ssrGroup)
                    {
                        checkout.Flight.PricingDetail.ItemInsert(new ProductItem
                        {
                            BaseRate = ssr.FirstOrDefault()?.NettPrice ?? 0,
                            GST = ssr.FirstOrDefault()?.Tax ?? 0,
                            ItemDetail = (ssr.FirstOrDefault().SSRType == FlightSSR.Baggage ? "Checked Baggage " : "") + ssr.FirstOrDefault().SSRLabel,
                            ItemQty = ssr.Count(),
                            Surcharge = 0,
                            Discountable = false,
                        });
                    }
                }

                var errorList = ModelState.Where(x => (x.Value.Errors.Count > 0 && x.Key.Contains("TravellerDetails[")) || (x.Value.Errors.Count > 0 && x.Key.Contains("TravellerDetails[")));
                foreach (var item in errorList)
                {
                    ModelState[item.Key].Errors.Clear();
                }

                var res = TryUpdateModel(checkout.Flight.TravellerDetails);
            }

            if (checkout.Hotel != null && productHotel.RoomDetails != null && productHotel.RoomDetails.Count > 0)
            {
                var roomListSubmit = productHotel.RoomDetails;
                bool isEANRapid = checkout.Hotel.HotelSelected.Any(a => a.hotelSupplier == HotelSupplier.EANRapid);
                checkout.Hotel.ApplyInfo = productHotel.ApplyInfo;
                checkout.Hotel.ContactPerson = contact;

                for (int i = 0; i < checkout.Hotel.RoomDetails.Count; i++)
                {
                    var rooms = checkout.Hotel.RoomDetails[i];

                    rooms.Title = roomListSubmit[i].Title;
                    rooms.GivenName = roomListSubmit[i].GivenName;
                    rooms.Surname = roomListSubmit[i].Surname;
                    rooms.GuestDOB = roomListSubmit[i].GuestDOB;
                    rooms.DateOfBirth = roomListSubmit[i].GuestDOB == null ? roomListSubmit[i].DateOfBirth : roomListSubmit[i].GuestDOB.ToDateOfBirthModel();
                    rooms.SpecialRequest = roomListSubmit[i].SpecialRequest;
                    rooms.CheckInMode = roomListSubmit[i].CheckInMode;
                    rooms.AdditionalRequest = roomListSubmit[i].AdditionalRequest;
                    rooms.CustomizedField1 = roomListSubmit[i].CustomizedField1;
                    rooms.CustomizedField2 = roomListSubmit[i].CustomizedField2;
                    rooms.CustomizedField3 = roomListSubmit[i].CustomizedField3;
                    rooms.CustomizedField4 = roomListSubmit[i].CustomizedField4;
                    rooms.HotelBundleTicketSelected = roomListSubmit[i].HotelBundleTicketSelected;

                    if (isEANRapid && rooms.SpecialRequest?.BetTypeID != null)
                    {
                        // Selected bed type used to recheck price and pre book.
                        rooms.RateToken = rooms.SpecialRequest.BetTypeID;
                    }
                }

                // If is hotel without addon then get mandatory fees
                GetAndAssignHotelInformation(tripid, checkout);
            }
            if (checkout.CarRental != null)
            {
                checkout.CarRental.ContactPerson = contact;
                checkout.CarRental.VehicleDetails.ExpiryDate = contact.LicenseInfo.ExpiryDate;
                checkout.CarRental.VehicleDetails.DrivingLicenseNo = contact.LicenseInfo.DrivingLicenseNo;
            }
            checkout.ImFlying = postCheckoutForm.ImFlying;
            checkout.IsRegister = postCheckoutForm.IsRegister;

            #endregion

            #region Model Validation Section
            var roomErrorList = ModelState.Where(x => (x.Value.Errors.Count > 0 &&
            (x.Key.Contains(".DateOfMonth") || x.Key.Contains(".DateOfBirth")) && !x.Key.StartsWith("TravellerDetails[")));
            foreach (var item in roomErrorList)
            {
                ModelState[item.Key].Errors.Clear();
            }

            #region Optional Register Validation Region
            if (!checkout.IsRegister)
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
                    bool isEmailNotAvailable = db.Users.Any(x => x.Email.ToLower() == checkout.ContactPerson.Email.ToLower());
                    if (isEmailNotAvailable)
                    {
                        ModelState.AddModelError("MemberRegisterModels.Email", "Oops, look like your email had registered.");
                    }
                }
            }
            #endregion

            #region Add if User IsAuthenticated, then restrict to use other email
            if (User.Identity.IsAuthenticated && contact.Email.ToLower() != CustomPrincipal.Email)
            {
                ModelState.AddModelError("Email", "Abuse modification begin detected.");
            }
            #endregion

            // For debug check error usage
            //var perror = ModelState.Where(x => x.Value.Errors.Count > 0);
            #endregion

            #region Set Cookies for Last Filled Form Information
            checkout.LastFilledDetails = new CheckoutProduct.LastFilledInfo();
            checkout.LastFilledDetails.LastContactPerson = JsonConvert.DeserializeObject<ContactPerson>(JsonConvert.SerializeObject(checkout.ContactPerson));

            if (checkout.Flight != null)
            {
                checkout.LastFilledDetails.LastTravellerDetails = JsonConvert.DeserializeObject<List<TravellerDetail>>(JsonConvert.SerializeObject(checkout.Flight.TravellerDetails));
            }

            if (checkout.Hotel != null)
            {
                checkout.LastFilledDetails.LastRoomDetails = JsonConvert.DeserializeObject<List<RoomDetail>>(JsonConvert.SerializeObject(checkout.Hotel.RoomDetails));

            }
            Response.Cookies.Set(new HttpCookie(Alphareds.Module.Model.Web.Cookies.Key.Mayflower_SpeedUpCFill.ToString(), Cryptography.AES.Encrypt(
                JsonConvert.SerializeObject(checkout.LastFilledDetails, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })))
            {
                Expires = DateTime.Now.AddDays(7)
            });
            #endregion

            if (ModelState.IsValid)
            {
                #region Optional Register Insert Member
                SqlCommand command = new SqlCommand();
                if (checkout.IsRegister && MemberServiceController.IsMemberEmailAvailable(checkout.ContactPerson.Email))
                {
                    try
                    {
                        checkout.MemberRegisterModels = postCheckoutForm.MemberRegisterModels;
                        var optionalMemberRegister = PopulateMemberRegisterModel(checkout.ContactPerson, checkout.MemberRegisterModels);
                        int registeredUserId = MemberServiceController.InsertSimpleMember(optionalMemberRegister, command);
                        MemberServiceController.UpdateSimpleMember(optionalMemberRegister, command, registeredUserId);
                        Session["RegisteredUserId"] = registeredUserId;
                        checkout.BookingUserID = registeredUserId;
                        command.Transaction.Commit();
                        MemberController mc = new MemberController();

                        bool sendMailStatus = false;
                        for (int i = 0; i <= 3; i++)
                        {
                            sendMailStatus = mc.GenerateActivateMail(optionalMemberRegister.FirstName, registeredUserId);
                            if (sendMailStatus)
                                break;
                        }

                        if (!sendMailStatus)
                            logger.Error("Guest Details page register send activate mail. "
                                + "UserId:" + registeredUserId
                                + "Email: " + checkout.ContactPerson.Email);
                    }
                    catch (Exception ex)
                    {
                        if (command != null && command.Transaction != null)
                            command.Transaction.Rollback();

                        logger.Fatal(ex, "Hotel Checkout Optional Register Error" + " - " + DateTime.Now.ToLoggerDateTime());

                        if (ex.InnerException != null)
                            logger.Fatal(ex.GetBaseException(), "(Base Exception) Hotel Checkout Optional Register Error" + " - " + DateTime.Now.ToLoggerDateTime());

                        return RedirectToAction("GuestDetails", "Checkout", new { tripid, reference = "registererror" });
                    }
                }
                #endregion

                // set checkout step
                if (checkout.CheckoutStep < 3)
                {
                    checkout.CheckoutStep = 4;
                }

                Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkout);

                if (checkout.AddOnProduct != null && checkout.Hotel == null && checkout.Flight == null && checkout.TourPackage == null)
                {
                    return RedirectToAction("Payment", "Checkout", new { tripid, affiliationId });
                }
                else if (checkout.CarRental != null)
                {
                    checkout.CarRental.VehicleDetails.OptionalServiceID = 0;
                    return RedirectToAction("Payment", "Checkout", new { tripid, affiliationId });
                }
                else
                {
                    return RedirectToAction("AddOn", "Checkout", new { tripid, affiliationId });
                }
                //return RedirectToAction("Payment", "Checkout", new { tripid, affiliationId });
            }
            else
            {
                // If not retrieve back column name, when postback this textbox will missed.
                if (CustomPrincipal.IsAgent && checkout.Flight != null)
                {
                    MayFlower db = new MayFlower();
                    UserDetail userDtl = db.UserDetails.FirstOrDefault(x => x.UserID == CurrentUserID);
                    CustomizedField userCustomizedFld = userDtl.User.Organization.CustomizedFields.FirstOrDefault();

                    ViewData.Add("CUSTOMIZEDFIELD1", userCustomizedFld != null ? userCustomizedFld.CustomizedField1 : null);
                    ViewData.Add("CUSTOMIZEDFIELD2", userCustomizedFld != null ? userCustomizedFld.CustomizedField2 : null);
                    ViewData.Add("CUSTOMIZEDFIELD3", userCustomizedFld != null ? userCustomizedFld.CustomizedField3 : null);
                    ViewData.Add("CUSTOMIZEDFIELD4", userCustomizedFld != null ? userCustomizedFld.CustomizedField4 : null);
                }

                return View(postBackViewPath, checkout);
            }

        }

        #region Step 3 Guest Details
        [HttpPost]
        public ActionResult GetLastTravelDetails()
        {
            var _readCookies = Request.Cookies[Alphareds.Module.Model.Web.Cookies.Key.Mayflower_SpeedUpCFill.ToString()];

            if (_readCookies != null)
            {
                string _decVal = "";
                Cryptography.AES.TryDecrypt(_readCookies.Value, out _decVal);

                try
                {
                    var _lastInfo = JsonConvert.DeserializeObject<CheckoutProduct.LastFilledInfo>(_decVal);
                    return Json(new
                    {
                        count = _lastInfo.LastTravellerDetails.Count,
                        trv = _lastInfo.LastTravellerDetails,
                    });
                }
                catch
                {
                    _readCookies.Value = null;
                    _readCookies.Expires = DateTime.Now.AddDays(-1);
                    Response.Cookies.Add(_readCookies);
                }
            }

            return Json(new { count = 0 });
        }

        [HttpGet]
        [Authorize]
        public ActionResult GetTravellerGrpdetails(int grpID = -1)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            MayFlower db = new MayFlower();
            bool acceptNotSelfCreateUser = false; // CustomPrincipal.IsAgent;

            List<TravellerList> travellerList = db.TravellerGroups.Where(x => x.GroupID == grpID && x.IsActive && x.TravellerList.IsActive
                && (acceptNotSelfCreateUser || x.CreatedByID == CurrentUserID))
                ?.Select(x => x.TravellerList)?.ToList();

            if (travellerList != null && travellerList.Count > 0)
            {
                try
                {
                    return Json(travellerList.Select(x => x.TravellerID), JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    logger.Debug(ex);
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Unknow error occur."); ;
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Traveler Group not found."); ;
        }

        [HttpGet]
        [Authorize]
        public ActionResult GetTravellerFlyerdetails(int travellerID = -1, int guestIndex = 0)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            MayFlower db = new MayFlower();
            bool acceptNotSelfCreateUser = false; // CustomPrincipal.IsAgent;

            var travellerDtl = db.TravellerLists.FirstOrDefault(x => x.TravellerID == travellerID && x.IsActive
                && (acceptNotSelfCreateUser || x.CreatedByID == CurrentUserID));

            if (travellerDtl != null)
            {
                try
                {
                    var traveller = checkout.Flight.TravellerDetails[guestIndex];

                    traveller.Title = travellerDtl.Title.TitleCode;
                    traveller.GivenName = travellerDtl.FirstName;
                    traveller.Surname = travellerDtl.FamilyName;
                    traveller.DOB = travellerDtl.DOB;
                    traveller.Nationality = travellerDtl.Nationality;
                    traveller.PassportNumber = travellerDtl.Passport;
                    traveller.PassportExpiryDate = travellerDtl.PassportExpiryDate;
                    traveller.PassportIssueCountry = travellerDtl.PassportIssuePlace;
                    traveller.FrequentTravellerID = travellerID.ToString();

                    return Json(traveller, JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    logger.Debug(ex);
                    return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Unknow error occur."); ;
                }
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Traveler not found in Frequent Travel List.");
        }

        private ContactPerson GetPassengerDetailPage(ContactPerson model, int userid, Mayflower.General.CustomPrincipal _customPrincipal, MayFlower dbContext = null)
        {
            if (model == null && userid != 0)
            {
                model = PopulateUserLoginToContactPerson(userid, dbContext);
            }
            else if (model != null && string.IsNullOrWhiteSpace(model.GivenName) && userid != 0)
            {
                model = PopulateUserLoginToContactPerson(userid, dbContext);
            }
            else if (model != null && model.Email != _customPrincipal.Email && userid != 0) // prevent cookies provide wrong email
            {
                model = PopulateUserLoginToContactPerson(userid, dbContext);
            }

            return model ?? new ContactPerson();
        }

        private static ContactPerson PopulateUserLoginToContactPerson(int userid, MayFlower dbContext = null)
        {
            ContactPerson person = new ContactPerson();

            // 2017/01/03 - Initialize new DBContext instance to able to retrieve latest information
            dbContext = dbContext ?? new MayFlower();

            UserDetail userDtl = dbContext.UserDetails.FirstOrDefault(x => x.UserID == userid);
            Organization org = userDtl.User.Organization;

            bool _IsAgent = userDtl.User.UserTypeCode == "AGT";
            if (!_IsAgent)
            {
                person.Title = userDtl.TitleCode == "-" ? null : userDtl.TitleCode;
                person.GivenName = userDtl.FirstName == "-" ? null : userDtl.FirstName;
                person.Surname = userDtl.LastName == "-" ? null : userDtl.LastName;
                person.Email = userDtl.User.Email;
                person.DOB = userDtl.DOB.HasValue ? userDtl.DOB : null;

                person.Address1 = userDtl.Address1 == "-" ? null : userDtl.Address1;
                person.Address2 = userDtl.Address2 == "-" ? null : userDtl.Address2;
                person.City = userDtl.City == "-" ? null : userDtl.City;
                person.PostalCode = userDtl.Postcode == "-" ? null : userDtl.Postcode;
                person.State = userDtl.AddressProvinceState == "-" ? null : userDtl.AddressProvinceState;
                person.CountryCode = userDtl.Country != null ? userDtl.Country.CountryCode : null;

                person.Phone1 = userDtl.PrimaryPhone == "-" ? null : userDtl.PrimaryPhone;
                person.Phone1LocationCode = userDtl.PrimaryPhoneCountryCode;

                person.Phone2LocationCode = userDtl.SecondaryPhoneCountryCode;
                person.Phone2 = userDtl.SecondaryPhone == "-" ? null : userDtl.SecondaryPhone;

                person.IdentityDocuments = person.IdentityDocuments ?? new ContactPerson.IdentityDocument();
                person.IdentityDocuments.Nationality = userDtl.NationalityCode;
                person.IdentityDocuments.PassportNumber = userDtl.PassportNo;
                person.IdentityDocuments.PassportIssueCountry = userDtl.PassportIssueCountryCode;
                person.IdentityDocuments.PassportExpiryDate = userDtl.PassportExpiryDate;
            }
            else
            {
                person.Title = userDtl.TitleCode;
                person.GivenName = userDtl.FirstName;
                person.Surname = userDtl.LastName;
                person.Email = userDtl.User.Email;
                person.DOB = userDtl.DOB.HasValue ? userDtl.DOB : null;

                person.Address1 = org.Address1;
                person.Address2 = org.Address2;
                person.City = org.City;
                person.PostalCode = org.PostCode;
                person.State = org.ProvinceState;
                person.CountryCode = org.Country != null ? org.Country.CountryCode : null;

                person.Phone1 = org.ContactNo1;
                person.Phone1LocationCode = org.OfficeNoCountryCode;
                person.Phone2LocationCode = org.MobileNoCountryCode;
                person.Phone2 = org.ContactNo2;
            }
            return person;
        }

        private MemberRegisterModels PopulateMemberRegisterModel(ContactPerson contactPerson, MemberRegisterModels member)
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
                DOB = contactPerson.DOB ?? DateTime.MinValue, // Handle with InsertMember when MinValue will null
                PhoneCode1 = contactPerson.Phone1LocationCode,
                PrimaryPhone = contactPerson.Phone1,
                PhoneCode2 = contactPerson.Phone2LocationCode,
                SecondaryPhone = contactPerson.Phone2,
                Address1 = contactPerson.Address1,
                Address2 = contactPerson.Address2,
                City = contactPerson.City,
                Postcode = contactPerson.PostalCode,
                AddressProvinceState = contactPerson.State,
                CountryCode = contactPerson.CountryCode,
                IsActive = true,
                CreatedByID = CurrentUserID,
                ModifiedByID = CurrentUserID
            };
        }
        #endregion

        #region Step 3.5 - ~/Checkout/AddOn
        [SessionFilter(SessionName = "CheckoutProduct")]
        public async Task<ActionResult> AddOn(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            bool anyAddOnProduct = false;
            FlightInformation flightInfo = null;
            FlightMasterInfo flightDetailInfo = null;
            DateTime? searchDateFrom = null;
            DateTime? searchDateTo = null;
            string pType = null;
            string flightOrigin = null;
            string destination = null;
            int totalQtyAcceptable = 1;
            MayFlower db = null;

            if (checkout.Flight != null)
            {
                db = db ?? new MayFlower();

                // Get Depart Trip Arrival DateTime
                searchDateFrom = checkout.Flight.FlightInfo.SupplierFlightInfo.OriginDestinationOptions.FirstOrDefault()?.
                    FlightSegments.LastOrDefault()?.ArrivalDateTime.Date ?? checkout.Flight.SearchFlightInfo.BeginDate.Value.Date;
                searchDateTo = checkout.Flight.SearchFlightInfo.EndDate;
                pType = "Flight";
                flightInfo = checkout.Flight.FlightInfo;
                flightDetailInfo = checkout.Flight.FlightInfo.FlightDetailInfo;
                flightOrigin = checkout.Flight.SearchFlightInfo.DepartureStationCode;
                destination = checkout.Flight.SearchFlightInfo.ArrivalStationCode;
                totalQtyAcceptable = checkout.Flight.SearchFlightInfo.Adults + checkout.Flight.SearchFlightInfo.Childrens;
                bool departFromMYS = db.Stations.FirstOrDefault(x => x.StationCode == flightOrigin)?.CountryCode == "MYS";

                //checking destination support insurance or not
                string arrivalCountryCode = db.Stations.FirstOrDefault(x => x.StationCode == destination)?.CountryCode;
                //bool arrivalCountryHaveInsurance = false;
                var insuranceStartDate = checkout.Flight.SearchFlightInfo.BeginDate.Value.Date;
                var insuranceEndDate = checkout.Flight.FlightInfo.SupplierFlightInfo.OriginDestinationOptions.LastOrDefault()?.
                    FlightSegments.LastOrDefault()?.ArrivalDateTime.Date ?? checkout.Flight.SearchFlightInfo.EndDate;
                var totalTravelDay = insuranceEndDate - insuranceStartDate; //"searchDateFrom" will be different when flight reach on next day
                bool totalTravelDaysLessThan45 = checkout.Flight.SearchFlightInfo.isReturn ? totalTravelDay.Value.TotalDays < 45 : true; //more than 45 days no offer insurance
                //arrivalCountryHaveInsurance = UtilitiesService.CheckInsuranceSupportCountry(arrivalCountryCode);

                // TODO Code: Insurance Code Here
                if (Core.IsEnableFlightInsurance && departFromMYS && totalTravelDaysLessThan45) /*arrivalCountryHaveInsurance*/
                {
                    QuoteInsurance(ref checkout);
                }
            }

            if (checkout.Hotel == null && checkout.Flight != null)
            {
                double stayTotalDays = flightInfo.FlightDetailInfo.ReturnDate.Date.Subtract(searchDateFrom.Value).TotalDays;
                bool lessThan28Days = stayTotalDays < 28;
                string info = lessThan28Days ? null : "longtrip";
                bool isAgent = IsAgentUser;
                bool forceCrossSale = false;

                #region Query DB is any Cross Sell Hotel Rules
                bool isOneWay = !flightDetailInfo.IsReturnTrip;
                IEnumerable<CrossSaleRule> crossSaleAvailaible = null;
                List<CrossSaleRuleHotel> crossSaleRuleHotel = null;
                if (Core.IsEnableHotelCrossSales && !isOneWay)
                {
                    crossSaleAvailaible = CheckIsCrossSalesHotelAvailaible(flightDetailInfo, db);
                    crossSaleRuleHotel = crossSaleAvailaible != null ? crossSaleAvailaible.SelectMany(x => x.CrossSaleRuleHotels)?.ToList() : null;
                    Session["CrossSaleRules" + tripid] = crossSaleRuleHotel;
                }
                #endregion

                if (crossSaleRuleHotel != null)
                {
                    crossSaleRuleHotel = crossSaleRuleHotel.OrderBy(x => x.CrossSaleRule.BookingDateFrom).ToList();

                    if (lessThan28Days && stayTotalDays != 0 && crossSaleRuleHotel.Count > 0)
                    {
                        //crossSaleRuleHotel = crossSaleRuleHotel.ToList();
                        Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse hotelList = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse();
                        List<string> supplierCode = crossSaleRuleHotel.Select(x => x.HotelSupplierCode).Distinct().ToList();

                        SearchHotelModel searchHotelReq = new SearchHotelModel
                        {
                            ArrivalDate = checkout.Flight.FlightInfo.FlightDetailInfo.FlightTrip.FirstOrDefault().ArriveDateTime.Date,
                            DepartureDate = checkout.Flight.FlightInfo.FlightDetailInfo.ReturnDate.Date,
                            CurrencyCode = "MYR",
                            CustomerIpAddress = Request.UserHostAddress,
                            CustomerUserAgent = Request.UserAgent,
                            CustomerSessionId = tripid,
                            NoOfAdult = checkout.Flight.SearchFlightInfo.Adults,
                            NoOfInfant = checkout.Flight.SearchFlightInfo.Childrens,
                            NoOfRoom = 1,
                            Destination = checkout.Flight.FlightInfo.FlightDetailInfo.Destination,
                            IsB2B = isAgent,
                            IsCrossSell = false, // For EANRapid cannot display package rate

                            SupplierIncluded = new SearchSupplier()
                            {
                                Expedia = supplierCode.Contains("EAN") ? true : false,
                                JacTravel = supplierCode.Contains("JAC") ? true : false,
                                Tourplan = supplierCode.Contains("TP") ? true : false,
                                HotelBeds = supplierCode.Contains("HB") ? true : false,
                                EANRapid = supplierCode.Contains("RAP"),
                            }
                        };

                        int attemp = 0;
                        attemArea:
                        hotelList = await GetHotelFromSearchModel(searchHotelReq, crossSaleRuleHotel);
                        searchHotelReq.Result = hotelList;
                        bool isErrorDuringGetHotel = (hotelList == null || hotelList.Errors != null || hotelList.HotelList == null || (hotelList.HotelList != null && hotelList.HotelList.Length == 0));
                        if (attemp <= 3 && isErrorDuringGetHotel)
                        {
                            attemp++;
                            goto attemArea;
                        }
                        else if (attemp > 2 && isErrorDuringGetHotel)
                        {
                            string msg = hotelList != null && hotelList.Errors != null ? hotelList.Errors.ErrorMessage : "Service no respond.";
                            logger = LogManager.GetCurrentClassLogger();
                            logger.Error("Error on Flight Search Cross Sales get hotel. " + msg);
                        }

                        if (!isErrorDuringGetHotel)
                        {
                            /*checkout.InsertProduct(new ProductHotel
                            {
                                SearchHotelInfo = searchHotelReq,
                            });*/

                            Core.SetSession(Enumeration.SessionName.SearchRequest, tripid, searchHotelReq);
                            Core.SetSession(Enumeration.SessionName.HotelList, tripid, searchHotelReq);
                            //hotelList.HotelList = HotelServiceController.ProcessDiscountCalculation(hotelList.HotelList, crossSaleRuleHotel);
                            // 2017/09/07 - Direct use markup from compare tool service
                            hotelList.HotelList = hotelList.HotelList.OrderBy(x => x.lowRate).ToArray();
                            CrossSellModels crossSellModels = new CrossSellModels
                            {
                                ForceCrossSell = forceCrossSale,
                                CrossSellRules = crossSaleRuleHotel,
                                HotelInformation = hotelList.HotelList,
                            };

                            checkout.SellItemsAvailable.Hotels = crossSellModels;
                            anyAddOnProduct = true;
                        }
                        else
                        {
                            Session["isErrorDuringGetHotel" + tripid] = isErrorDuringGetHotel;
                        }
                    }
                }
            }

            if (checkout.Hotel != null && checkout.Flight == null)
            {
                searchDateFrom = checkout.Hotel.SearchHotelInfo.ArrivalDate;
                searchDateTo = checkout.Hotel.SearchHotelInfo.DepartureDate;
                pType = "Hotel";
                destination = checkout.Hotel.SearchHotelInfo.Destination; //.Split(',')[0];
                totalQtyAcceptable = checkout.Hotel.SearchHotelInfo.NoOfAdult + checkout.Hotel.SearchHotelInfo.NoOfInfant;
            }

            if (Core.IsEnableEventProduct && DateTime.Now >= new DateTime(2017, 10, 21, 12, 0, 0))
            {
                db = db ?? new MayFlower();
                bool withPromoEvent = checkout.PromoCodeFunctions.GetFrontendFunction.DisplayPromoEvent;
                var mainProductAmt = checkout[checkout.MainProductType]?.PricingDetail?.ProductTotalAmount;

                var eventList = eventDBFunc.GetEventProductList(searchDateFrom, searchDateTo, pType, flightOrigin, destination,
                    totalQtyAcceptable, withPromoEvent, mainProductAmt, db);
                if (eventList != null)
                {
                    // TODO: Ticket Code Here
                    /* Temp method, since db store procedure not ready for other product yet
                     * Actual need filter EventTypeCode (ex. CT, TH)
                     * Search 28-01-2017 to 30-01-2017
                     */
                    if (eventList.DetailsInfo.Count > 0 && eventList.HeaderInfo.Count > 0)
                    {
                        eventList.SearchInfo = new SearchInfo
                        {
                            DateFrom = searchDateFrom,
                            DateTo = searchDateTo,
                            Origin = flightOrigin,
                            Destination = destination,
                            ProductType = pType == ProductTypes.Hotel.ToString() ? ProductTypes.Hotel : ProductTypes.Flight,
                        };
                        checkout.SellItemsAvailable.EventProducts = eventList;

                        anyAddOnProduct = true;
                    }
                    else
                    {
                        eventList = null;
                        checkout.SellItemsAvailable.EventProducts = eventList;
                    }
                }
            }

            if (anyAddOnProduct || (checkout.SellItemsAvailable != null &&
                (checkout.SellItemsAvailable.Hotels != null || checkout.SellItemsAvailable.EventProducts != null || checkout.SellItemsAvailable.Insurance != null)))
            {
                if (flightInfo != null)
                {
                    //Need compulsory buy add on on specific airline. As Boss mentioned, any add on :)
                    string[] airlineForceAddOn = new string[] { };
                    string airlinesString = Core.GetAppSettingValueEnhanced("AirlineToForceAddOn");
                    airlineForceAddOn = airlinesString.Split(',');
                    if ((!checkout.Products.Any(x => x.ProductType == ProductTypes.Hotel))
                        && (flightInfo.FlightDetailInfo.Airline.Any(x => airlineForceAddOn.Any(a => a == x))))
                    {
                        checkout.SellItemsAvailable.ForceCrossSell = true;
                    }
                }

                // Control addon by promo code override other setting
                if (checkout.PromoCodeFunctions.GetFrontendFunction.ForceAddOn)
                {
                    checkout.SellItemsAvailable.ForceCrossSell = true;
                }
                else if (checkout.PromoCodeFunctions.GetFrontendFunction.SkipAddOn)
                {
                    checkout.SellItemsAvailable.ForceCrossSell = false;
                    return RedirectToAction("Payment", "Checkout", new { tripid, affiliationId, @ref = "pskip-addon" });
                }

                ViewBag.HasCrossSell = true;
                if (IsUseV2Layout)
                {
                    return View("~/Views/Checkout/AddOn_v2.cshtml", checkout);
                }
                else
                {
                    return View(checkout);
                }
            }
            else if (checkout.CarRental != null)
            {
                return View("~/Views/Checkout/AddOn_v2.cshtml", checkout);
            }
            else
            {
                return RedirectToAction("Payment", "Checkout", new { tripid, affiliationId });
            }
        }

        [HttpPost]
        [Filters.SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult AddOn(string dataToken, string ConcertTicket, string tripid, bool? requireInsurance, string CarOptServices)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            string postBackViewPath = IsUseV2Layout ? "~/Views/Checkout/AddOn_v2.cshtml" : "~/Views/Checkout/AddOn.cshtml";

            bool isEmptyHotelToken = string.IsNullOrWhiteSpace(dataToken);
            bool isEmptyConcert = string.IsNullOrWhiteSpace(ConcertTicket);
            bool isEmptyCarOpt = string.IsNullOrWhiteSpace(CarOptServices);
            bool isEmptyInsurance = !requireInsurance.HasValue || requireInsurance == false;
            string sessionHotelCheckOut = Enumeration.SessionName.HotelCheckOut + tripid;
            string roomSelectedSerialize = dataToken;
            List<RoomSelectedModel> roomSelectedList = isEmptyHotelToken ? new List<RoomSelectedModel>() : JsonConvert.DeserializeObject<List<RoomSelectedModel>>(roomSelectedSerialize);
            //List<AddonTicketPOSTBack> ticketList = isEmptyConcert ? new List<AddonTicketPOSTBack>() : JsonConvert.DeserializeObject<List<AddonTicketPOSTBack>>(ConcertTicket, new Newtonsoft.Json.Converters.IsoDateTimeConverter { DateTimeFormat = "dd-MM-yyyy" });
            List<AddonTicketPOSTBack> ticketList = new List<AddonTicketPOSTBack>();
            List<CarOptServicePOSTBack> carOptServiceList = isEmptyCarOpt ? new List<CarOptServicePOSTBack>() : JsonConvert.DeserializeObject<List<CarOptServicePOSTBack>>(CarOptServices);

            if (checkout.SellItemsAvailable.EventProducts != null)
            {
                var eventList = checkout.SellItemsAvailable.EventProducts.HeaderInfo.Select(x => x.EventTypeCode).Distinct();

                foreach (var item in eventList)
                {
                    var formItem = Request.Form[item];

                    if (!string.IsNullOrWhiteSpace(formItem))
                    {
                        isEmptyConcert = false;
                        ticketList.AddRange(JsonConvert.DeserializeObject<List<AddonTicketPOSTBack>>(formItem, new Newtonsoft.Json.Converters.IsoDateTimeConverter { DateTimeFormat = "dd-MM-yyyy" }));
                    }
                }
            }

            string pType = null;
            string flightOrigin = null;
            string destination = null;

            if (isEmptyHotelToken && checkout.Flight != null && !checkout.IsDynamic)
                checkout.RemoveProduct(ProductTypes.Hotel);

            if (isEmptyConcert)
                checkout.RemoveProduct(ProductTypes.AddOnProducts);

            if (isEmptyInsurance)
                checkout.RemoveProduct(ProductTypes.Insurance);

            if (requireInsurance == true)
            {
                checkout.RemoveProduct(ProductTypes.Insurance);
                AddInsuranceToPayment(ref checkout);
            }

            SearchRoomModel searchRoomModel = new SearchRoomModel();
            bool isAirAsia = checkout.Flight != null && checkout.Flight.FlightInfo.Supplier == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.AirAsia;
            int TotalRooms = 0;
            int totalConcertTicket = 0;

            //if (((isEmptyHotelToken && checkout.Flight != null) || isEmptyConcert) && !isAirAsia) // remove !AirAsia, it cannot add concert when airasia flight selected
            if (isEmptyHotelToken && checkout.Flight != null && isEmptyConcert)
            {
                checkout.CheckoutStep = 5;

                Session.Remove(sessionHotelCheckOut);
                return RedirectToAction("Payment", "Checkout", new { tripid, @ref = "skip-crosssell" });
            }
            else if ((roomSelectedList.Sum(x => x.Qty) > 8 || (roomSelectedList.Sum(x => x.Qty) <= 0 && isAirAsia) && (ticketList.Sum(x => x.Qty) <= 0 && isAirAsia)) ||
                (checkout.SellItemsAvailable?.EventProducts != null && checkout.SellItemsAvailable.EventProducts.HeaderInfo.All(a => ticketList.Where(x => x.Type == a.EventTypeCode).Sum(x => x.Qty) > a.TotalQtyAcceptable)) ||
                (checkout.SellItemsAvailable?.EventProducts != null && checkout.SellItemsAvailable.EventProducts.DetailsInfo.All(a => ticketList.Where(x => x.Item == a.EventDetailsID.ToString() && x.Date.Date == a.EventDate.Date).Sum(x => x.Qty) > a.TicketBalance)))
            {
                return RedirectToAction("AddOn", "Checkout", new { tripid });
            }
            else if (!isEmptyHotelToken && checkout.SellItemsAvailable?.Hotels != null)
            {
                TotalRooms = roomSelectedList.Sum(x => x.Qty);
            }

            if (!isEmptyConcert && checkout.SellItemsAvailable?.EventProducts != null)
            {
                totalConcertTicket = ticketList.Sum(x => x.Qty);
            }
            if (!isEmptyCarOpt)
            {
                ProductItem VehiclePricingDetail = checkout.CarRental.PricingDetail.Items.FirstOrDefault();
                var insurance = checkout.CarRental.VehicleSelected.Insurance.FirstOrDefault();
                checkout.CarRental.PricingDetail = new ProductPricingDetail()
                {
                    Items = new List<ProductItem>() { VehiclePricingDetail },
                };

                int totalDays = Convert.ToInt32((checkout.CarRental.SearchInfo.ReturnDateTime - checkout.CarRental.SearchInfo.PickupDateTime).TotalDays);
                checkout.CarRental.PricingDetail.ItemInsert(new ProductItem
                {
                    BaseRate = insurance.InsurancePrice,
                    ItemQty = totalDays,
                    ItemDetail = "Insurance",
                    Supplier_TotalAmt = insurance.InsurancePrice * totalDays,
                    ProductDate = DateTime.Now,
                });
                foreach (var item in carOptServiceList)
                {
                    checkout.CarRental.PricingDetail.ItemInsert(new ProductItem
                    {
                        BaseRate = item.price.ToDecimal(),
                        ItemQty = totalDays,
                        ItemDetail = item.name,
                        Supplier_TotalAmt = item.price.ToDecimal() * totalDays,
                        ProductDate = DateTime.Now,
                    });
                    checkout.CarRental.VehicleDetails.OptionalServiceID = item.id;
                }
                checkout.CarRental.PricingDetail.Currency = "MYR";
            }
            if (TotalRooms > 0)
            {
                var room = roomSelectedList.FirstOrDefault();
                searchRoomModel = (SearchRoomModel)Session[Enumeration.SessionName.RoomAvail + tripid + "_" + room.Hotel];

                HotelServiceController.InitializeModel hc = new HotelServiceController.InitializeModel(tripid, Request.UserAgent, GetUserIP());
                List<SearchRoomModel> searchRoomModelList = new List<SearchRoomModel> { searchRoomModel };
                List<GTM_HotelProductModel> _GTM_addToCartList = new List<GTM_HotelProductModel>();
                List<RoomDetail> _roomDetails = hc.InitializeRoomDetailModel(roomSelectedList, searchRoomModelList, out _GTM_addToCartList, checkout.IsDynamic);
                var supplierSelected = searchRoomModel.Result.HotelRoomInformationList.First().hotelSupplier;

                SearchHotelModel requestModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);

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
                    Discounts = new List<DiscountDetail>(),
                };

                int _roomNameCounter = 0;
                foreach (var _room in _roomDetails)
                {
                    // Tourplan duplicate name hotfix
                    string nameNumeric = supplierSelected == HotelSupplier.Tourplan ? "_" + _roomNameCounter : "";

                    _room.Title = checkout.ContactPerson.Title;
                    _room.GivenName = checkout.ContactPerson.GivenName;
                    _room.Surname = checkout.ContactPerson.Surname + (_roomNameCounter > 0 ? nameNumeric : "");
                    _room.DateOfBirth = checkout.ContactPerson.DOB.ToDateOfBirthModel();

                    _roomNameCounter++;
                }

                ProductHotel productHotel = new ProductHotel
                {
                    ContactPerson = checkout.ContactPerson,
                    SearchHotelInfo = requestModel,
                    RoomDetails = _roomDetails,
                    RoomAvailabilityResponse = searchRoomModel.Result,
                    HotelSelected = requestModel.Result.HotelList.Where(x => _roomDetails.Any(y => y.HotelId == x.hotelId))?.ToList(),
                    SearchRoomList = searchRoomModelList,
                    ProductSeq = 2,
                    PricingDetail = hotelPricingDetail,
                };
                if (checkout.Hotel != null)
                {
                    checkout.RemoveProduct(ProductTypes.Hotel);
                }
                checkout.InsertProduct(productHotel);

                // If is cross sale then get mandatory fees
                if (checkout.Hotel != null)
                {
                    pType = "Hotel";
                    destination = checkout.Hotel.SearchHotelInfo.Destination;
                    // Get notification and fee
                    #region Get Notification and Fees
                    try
                    {
                        GetHotelInformationModel hotelInfo = new GetHotelInformationModel()
                        {
                            CurrencyCode = checkout.CheckOutSummary.CurrencyCode,
                            CustomerUserAgent = Request.UserAgent,
                            CustomerIpAddress = General.Utilities.GetClientIP,
                            CustomerSessionId = Guid.NewGuid().ToString(),
                            HotelID = checkout.Hotel.RoomDetails.FirstOrDefault()?.HotelId ?? "0",
                            JacTravelPropertyID = checkout.Hotel.TravelPropertyID,
                            MoreOptions = new MoreInformationOptions
                            {
                                Options = InformationOptions.HOTEL_DETAILS
                            }
                        };

                        if (supplierSelected == HotelSupplier.Expedia)
                        {
                            hotelInfo.Result = ExpediaHotelsServiceCall.GetHotelInformation(hotelInfo);
                        }
                        else if (supplierSelected == HotelSupplier.JacTravel)
                        {
                            hotelInfo.ResultJT = JacTravelServiceCall.GetPropertyDetails(hotelInfo);
                        }
                        else if (supplierSelected == HotelSupplier.Tourplan)
                        {
                            hotelInfo.ResultTP = TourplanServiceCall.GetHotelList(hotelInfo);
                        }
                        if (hotelInfo.Result != null)
                        {
                            if (hotelInfo.Result.hotelInformation != null && hotelInfo.Result.hotelInformation.hotelDetails != null)
                            {
                                Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelDetails hotelDtl = hotelInfo.Result.hotelInformation.hotelDetails;
                                checkout.Hotel.HotelInstrusction = new ProductHotel.CheckoutHotelInformation
                                {
                                    KnowBeforeYouGoDesc = string.IsNullOrEmpty(hotelDtl.knowBeforeYouGoDescription) ? null : hotelDtl.knowBeforeYouGoDescription,
                                    MandatoryFee = string.IsNullOrEmpty(hotelDtl.mandatoryFeesDescription) ? null : hotelDtl.mandatoryFeesDescription,
                                    NotificationFee = string.IsNullOrEmpty(hotelDtl.roomFeesDescription) ? null : hotelDtl.roomFeesDescription,
                                    SpecialCheckInInstruction = string.IsNullOrEmpty(hotelDtl.specialCheckInInstructions) ? null : hotelDtl.specialCheckInInstructions
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    #endregion
                }
                Core.SetSession(Enumeration.SessionName.GTM_trackAddToCart, tripid, JsonConvert.SerializeObject(_GTM_addToCartList));
            }

            if (totalConcertTicket > 0)
            {
                if (checkout.Flight != null)
                {
                    pType = "Flight";
                    flightOrigin = checkout.Flight.SearchFlightInfo.DepartureStationCode;
                    destination = checkout.Flight.SearchFlightInfo.ArrivalStationCode;
                }

                bool withPromoEvent = checkout.PromoCodeFunctions.GetFrontendFunction.DisplayPromoEvent;

                var ticketSelectedInfo = ticketList.SelectMany(xx =>
                {
                    var dateGotEvent = eventDBFunc.GetEventMaster(xx.Date, xx.Date, pType, flightOrigin, destination, withPromoEvent);

                    if (dateGotEvent != null && dateGotEvent.Any(d => string.IsNullOrWhiteSpace(d.EventDetailsID)))
                    {
                        return new List<ProductEventTicket.TicketInformation>();
                    }

                    int eventId = xx.Master.ToInt();
                    IEnumerable<int> productIdList = xx.Item.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.ToInt());
                    var _productInfo = checkout.SellItemsAvailable.EventProducts.DetailsInfo.Where(d => productIdList.Any(p => p == d.EventProductID)
                    && d.TicketCategoryCode == xx.Cat && ((d.EventDate == xx.Date && (xx.Type == "GT" || xx.Type == "CR" || xx.Type == "TH")) || (xx.Type != "GT" && xx.Type != "TH")));
                    var _productMaster = checkout.SellItemsAvailable.EventProducts.HeaderInfo.FirstOrDefault(d => d.EventID == eventId)
                    ?? new usp_EventMasterSelect_Result();

                    return _productInfo.Select(x => new ProductEventTicket.TicketInformation
                    {
                        OrderedDateTime = x.EventDate,
                        OrderedQty = xx.Qty,
                        EventID = eventId,
                        EventProductID = x.EventProductID,
                        TicketCategoryCode = xx.Cat,
                        EntranceGate = x.EntranceGate,
                        TicketBalance = x.TicketBalance,
                        TicketCategory = x.TicketCategory,
                        TicketCost = x.TicketCost,
                        TicketMarkup = x.TicketMarkup,
                        TicketProcessingFee = x.TicketProcessingFee,
                        TicketQty = x.TicketQty,
                        TicketSellingPrice = x.TicketSellingPrice,
                        TicketStatus = x.TicketStatus,
                        ItemDesc = _productMaster.EventName,
                        EventDate = x.EventDate,
                        EventStartTime = x.EventStartTime,
                        EventEndTime = x.EventEndTime,
                        EventTypeCode = x.EventTypeCode,
                        EventDetailsID = x.EventDetailsID,
                    });
                }).ToList();

                if (ticketSelectedInfo.Count <= 0)
                {
                    ModelState.AddModelError("Error", "Please try again later, product not found.");
                    return View(postBackViewPath, checkout);
                }
                else if (ticketSelectedInfo.Any(x => x.EventID == -1 || x.EventProductID == -1))
                {
                    ModelState.AddModelError("Error", "Please try again later, product not found.");
                    return View(postBackViewPath, checkout);
                }
                else if (ticketSelectedInfo.Any(x => x.OrderedQty > x.TicketBalance))
                {
                    ModelState.AddModelError("Error", "Concert Ticket selected has been sold out, please select another category.");
                    return View(postBackViewPath, checkout);
                }

                ProductPricingDetail eventProductsPricingDetail = new ProductPricingDetail()
                {
                    Currency = checkout.CheckOutSummary.CurrencyCode // db now don't have currency code
                };
                List<GTM_AddOnProductModel> GTM_TrackAddOnList = new List<GTM_AddOnProductModel>();
                foreach (var item in ticketSelectedInfo.GroupBy(grp => new
                {
                    grp.ItemDesc,
                    grp.EventTypeCode,
                    grp.TicketCategory,
                    grp.OrderedDateTime,
                    grp.OrderedQty
                }))
                {
                    eventProductsPricingDetail.ItemInsert(new ProductItem
                    {
                        BaseRate = item.Sum(s => s.FrontEndTicketPrice),
                        ItemQty = item.Key.OrderedQty,
                        ItemDetail = (item.Key.EventTypeCode == "CT" || item.Key.EventTypeCode == "GT") ? string.Format("{0} {1}", item.Key.ItemDesc, item.Key.TicketCategory)
                        : (/*item.Key.EventTypeCode == "TH" ||*/ item.Key.EventTypeCode == "WF" || item.Key.EventTypeCode == "CR" || item.Key.EventTypeCode == "DR") ? string.Format("{0}", item.Key.ItemDesc)
                        : string.Format("{0} - {1}", item.Key.ItemDesc, item.Key.OrderedDateTime.ToString("dd-MMM-yy, ddd")),
                        Supplier_TotalAmt = item.Sum(x => x.TicketCost) * item.Key.OrderedQty,
                        ProductDate = item.Key.OrderedDateTime,
                    });
                    //Add On GTM
                    GTM_TrackAddOnList.Add(new GTM_AddOnProductModel
                    {
                        name = item.Key.ItemDesc,
                        id = item.FirstOrDefault().EventProductID.ToString(),
                        price = item.FirstOrDefault().FrontEndTicketPrice.ToString("0.00"), //baseRate
                        quantity = item.Key.OrderedQty
                    });
                }

                var searchInfo = checkout.SellItemsAvailable.EventProducts.SearchInfo;

                var concert = new ProductEventTicket(searchInfo.DateFrom, searchInfo.DateTo, searchInfo.ProductType, searchInfo.Destination)
                {
                    ContactPerson = checkout.ContactPerson,
                    ProductSeq = 4,
                    TicketInfo = ticketSelectedInfo,
                    PricingDetail = eventProductsPricingDetail,
                };

                if (checkout.AddOnProduct != null)
                {
                    checkout.RemoveProduct(ProductTypes.AddOnProducts);
                }
                checkout.InsertProduct(concert);
                Core.SetSession(Enumeration.SessionName.GTM_trackAddOnSelected, tripid, JsonConvert.SerializeObject(GTM_TrackAddOnList));
            }

            checkout.CheckoutStep = 5;

            //return RedirectToAction("GuestDetails", new { tripid, affiliationId });
            return RedirectToAction("Payment", "Checkout", new { tripid, affiliationId });
        }

        [HttpPost]
        public ActionResult AddOnMini(string dataToken, string ConcertTicket, string tripid, bool? requireInsurance, string CarOptServices)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            string postBackViewPath = IsUseV2Layout ? "~/Views/Checkout/AddOn_v2.cshtml" : "~/Views/Checkout/AddOn.cshtml";

            bool isEmptyHotelToken = string.IsNullOrWhiteSpace(dataToken);
            bool isEmptyConcert = string.IsNullOrWhiteSpace(ConcertTicket);
            bool isEmptyCarOpt = string.IsNullOrWhiteSpace(CarOptServices);
            bool isEmptyInsurance = !requireInsurance.HasValue || requireInsurance == false;
            string sessionHotelCheckOut = Enumeration.SessionName.HotelCheckOut + tripid;
            string roomSelectedSerialize = dataToken;
            List<RoomSelectedModel> roomSelectedList = isEmptyHotelToken ? new List<RoomSelectedModel>() : JsonConvert.DeserializeObject<List<RoomSelectedModel>>(roomSelectedSerialize);
            //List<AddonTicketPOSTBack> ticketList = isEmptyConcert ? new List<AddonTicketPOSTBack>() : JsonConvert.DeserializeObject<List<AddonTicketPOSTBack>>(ConcertTicket, new Newtonsoft.Json.Converters.IsoDateTimeConverter { DateTimeFormat = "dd-MM-yyyy" });
            List<AddonTicketPOSTBack> ticketList = new List<AddonTicketPOSTBack>();
            List<CarOptServicePOSTBack> carOptServiceList = isEmptyCarOpt ? new List<CarOptServicePOSTBack>() : JsonConvert.DeserializeObject<List<CarOptServicePOSTBack>>(CarOptServices);

            if (checkout.SellItemsAvailable.EventProducts != null)
            {
                var eventList = checkout.SellItemsAvailable.EventProducts.HeaderInfo.Select(x => x.EventTypeCode).Distinct();

                foreach (var item in eventList)
                {
                    var formItem = Request.Form[item];

                    if (!string.IsNullOrWhiteSpace(formItem))
                    {
                        isEmptyConcert = false;
                        ticketList.AddRange(JsonConvert.DeserializeObject<List<AddonTicketPOSTBack>>(formItem, new Newtonsoft.Json.Converters.IsoDateTimeConverter { DateTimeFormat = "dd-MM-yyyy" }));
                    }
                }
            }

            string pType = null;
            string flightOrigin = null;
            string destination = null;

            if (isEmptyHotelToken && checkout.Flight != null && !checkout.IsDynamic)
                checkout.RemoveProduct(ProductTypes.Hotel);

            if (isEmptyConcert)
                checkout.RemoveProduct(ProductTypes.AddOnProducts);

            if (isEmptyInsurance)
                checkout.RemoveProduct(ProductTypes.Insurance);

            if (requireInsurance == true)
            {
                checkout.RemoveProduct(ProductTypes.Insurance);
                AddInsuranceToPayment(ref checkout);
            }

            SearchRoomModel searchRoomModel = new SearchRoomModel();
            bool isAirAsia = false;
            int TotalRooms = 0;
            int totalConcertTicket = 0;

            //if (((isEmptyHotelToken && checkout.Flight != null) || isEmptyConcert) && !isAirAsia) // remove !AirAsia, it cannot add concert when airasia flight selected
            if (isEmptyHotelToken && checkout.Flight != null && isEmptyConcert)
            {
                checkout.CheckoutStep = 5;

                Session.Remove(sessionHotelCheckOut);
                return RedirectToAction("MiniScreenPayment", "Checkout", new { tripid, @ref = "skip-crosssell" });
            }
            else if ((roomSelectedList.Sum(x => x.Qty) > 8 || (roomSelectedList.Sum(x => x.Qty) <= 0 && isAirAsia) && (ticketList.Sum(x => x.Qty) <= 0 && isAirAsia)) ||
                (checkout.SellItemsAvailable?.EventProducts != null && checkout.SellItemsAvailable.EventProducts.HeaderInfo.All(a => ticketList.Where(x => x.Type == a.EventTypeCode).Sum(x => x.Qty) > a.TotalQtyAcceptable)) ||
                (checkout.SellItemsAvailable?.EventProducts != null && checkout.SellItemsAvailable.EventProducts.DetailsInfo.All(a => ticketList.Where(x => x.Item == a.EventDetailsID.ToString() && x.Date.Date == a.EventDate.Date).Sum(x => x.Qty) > a.TicketBalance)))
            {
                return RedirectToAction("AddOn", "Checkout", new { tripid });
            }
            else if (!isEmptyHotelToken && checkout.SellItemsAvailable?.Hotels != null)
            {
                TotalRooms = roomSelectedList.Sum(x => x.Qty);
            }

            if (!isEmptyConcert && checkout.SellItemsAvailable?.EventProducts != null)
            {
                totalConcertTicket = ticketList.Sum(x => x.Qty);
            }
            if (!isEmptyCarOpt)
            {
                ProductItem VehiclePricingDetail = checkout.CarRental.PricingDetail.Items.FirstOrDefault();
                var insurance = checkout.CarRental.VehicleSelected.Insurance.FirstOrDefault();
                checkout.CarRental.PricingDetail = new ProductPricingDetail()
                {
                    Items = new List<ProductItem>() { VehiclePricingDetail },
                };

                int totalDays = Convert.ToInt32((checkout.CarRental.SearchInfo.ReturnDateTime - checkout.CarRental.SearchInfo.PickupDateTime).TotalDays);
                checkout.CarRental.PricingDetail.ItemInsert(new ProductItem
                {
                    BaseRate = insurance.InsurancePrice,
                    ItemQty = totalDays,
                    ItemDetail = "Insurance",
                    Supplier_TotalAmt = insurance.InsurancePrice * totalDays,
                    ProductDate = DateTime.Now,
                });
                foreach (var item in carOptServiceList)
                {
                    checkout.CarRental.PricingDetail.ItemInsert(new ProductItem
                    {
                        BaseRate = item.price.ToDecimal(),
                        ItemQty = totalDays,
                        ItemDetail = item.name,
                        Supplier_TotalAmt = item.price.ToDecimal() * totalDays,
                        ProductDate = DateTime.Now,
                    });
                    checkout.CarRental.VehicleDetails.OptionalServiceID = item.id;
                }
                checkout.CarRental.PricingDetail.Currency = "MYR";
            }
            if (TotalRooms > 0)
            {
                var room = roomSelectedList.FirstOrDefault();
                searchRoomModel = (SearchRoomModel)Session[Enumeration.SessionName.RoomAvail + tripid + "_" + room.Hotel];

                HotelServiceController.InitializeModel hc = new HotelServiceController.InitializeModel(tripid, Request.UserAgent, GetUserIP());
                List<SearchRoomModel> searchRoomModelList = new List<SearchRoomModel> { searchRoomModel };
                List<GTM_HotelProductModel> _GTM_addToCartList = new List<GTM_HotelProductModel>();
                List<RoomDetail> _roomDetails = hc.InitializeRoomDetailModel(roomSelectedList, searchRoomModelList, out _GTM_addToCartList, checkout.IsDynamic);
                var supplierSelected = searchRoomModel.Result.HotelRoomInformationList.First().hotelSupplier;

                SearchHotelModel requestModel = (SearchHotelModel)Core.GetSession(Enumeration.SessionName.HotelList, tripid);

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
                    Discounts = new List<DiscountDetail>(),
                };

                int _roomNameCounter = 0;
                foreach (var _room in _roomDetails)
                {
                    // Tourplan duplicate name hotfix
                    string nameNumeric = supplierSelected == HotelSupplier.Tourplan ? "_" + _roomNameCounter : "";

                    _room.Title = checkout.ContactPerson.Title;
                    _room.GivenName = checkout.ContactPerson.GivenName;
                    _room.Surname = checkout.ContactPerson.Surname + (_roomNameCounter > 0 ? nameNumeric : "");
                    _room.DateOfBirth = checkout.ContactPerson.DOB.ToDateOfBirthModel();

                    _roomNameCounter++;
                }

                ProductHotel productHotel = new ProductHotel
                {
                    ContactPerson = checkout.ContactPerson,
                    SearchHotelInfo = requestModel,
                    RoomDetails = _roomDetails,
                    RoomAvailabilityResponse = searchRoomModel.Result,
                    HotelSelected = requestModel.Result.HotelList.Where(x => _roomDetails.Any(y => y.HotelId == x.hotelId))?.ToList(),
                    SearchRoomList = searchRoomModelList,
                    ProductSeq = 2,
                    PricingDetail = hotelPricingDetail,
                };
                if (checkout.Hotel != null)
                {
                    checkout.RemoveProduct(ProductTypes.Hotel);
                }
                checkout.InsertProduct(productHotel);

                // If is cross sale then get mandatory fees
                if (checkout.Hotel != null)
                {
                    pType = "Hotel";
                    destination = checkout.Hotel.SearchHotelInfo.Destination;
                    // Get notification and fee
                    #region Get Notification and Fees
                    try
                    {
                        GetHotelInformationModel hotelInfo = new GetHotelInformationModel()
                        {
                            CurrencyCode = checkout.CheckOutSummary.CurrencyCode,
                            CustomerUserAgent = Request.UserAgent,
                            CustomerIpAddress = General.Utilities.GetClientIP,
                            CustomerSessionId = Guid.NewGuid().ToString(),
                            HotelID = checkout.Hotel.RoomDetails.FirstOrDefault()?.HotelId ?? "0",
                            JacTravelPropertyID = checkout.Hotel.TravelPropertyID,
                            MoreOptions = new MoreInformationOptions
                            {
                                Options = InformationOptions.HOTEL_DETAILS
                            }
                        };

                        if (supplierSelected == HotelSupplier.Expedia)
                        {
                            hotelInfo.Result = ExpediaHotelsServiceCall.GetHotelInformation(hotelInfo);
                        }
                        else if (supplierSelected == HotelSupplier.JacTravel)
                        {
                            hotelInfo.ResultJT = JacTravelServiceCall.GetPropertyDetails(hotelInfo);
                        }
                        else if (supplierSelected == HotelSupplier.Tourplan)
                        {
                            hotelInfo.ResultTP = TourplanServiceCall.GetHotelList(hotelInfo);
                        }
                        if (hotelInfo.Result != null)
                        {
                            if (hotelInfo.Result.hotelInformation != null && hotelInfo.Result.hotelInformation.hotelDetails != null)
                            {
                                Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelDetails hotelDtl = hotelInfo.Result.hotelInformation.hotelDetails;
                                checkout.Hotel.HotelInstrusction = new ProductHotel.CheckoutHotelInformation
                                {
                                    KnowBeforeYouGoDesc = string.IsNullOrEmpty(hotelDtl.knowBeforeYouGoDescription) ? null : hotelDtl.knowBeforeYouGoDescription,
                                    MandatoryFee = string.IsNullOrEmpty(hotelDtl.mandatoryFeesDescription) ? null : hotelDtl.mandatoryFeesDescription,
                                    NotificationFee = string.IsNullOrEmpty(hotelDtl.roomFeesDescription) ? null : hotelDtl.roomFeesDescription,
                                    SpecialCheckInInstruction = string.IsNullOrEmpty(hotelDtl.specialCheckInInstructions) ? null : hotelDtl.specialCheckInInstructions
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    #endregion
                }
                Core.SetSession(Enumeration.SessionName.GTM_trackAddToCart, tripid, JsonConvert.SerializeObject(_GTM_addToCartList));
            }

            if (totalConcertTicket > 0)
            {
                if (checkout.Flight != null)
                {
                    pType = "Flight";
                    flightOrigin = checkout.Flight.SearchFlightInfo.DepartureStationCode;
                    destination = checkout.Flight.SearchFlightInfo.ArrivalStationCode;
                }

                bool withPromoEvent = checkout.PromoCodeFunctions.GetFrontendFunction.DisplayPromoEvent;

                var ticketSelectedInfo = ticketList.SelectMany(xx =>
                {
                    var dateGotEvent = eventDBFunc.GetEventMaster(xx.Date, xx.Date, pType, flightOrigin, destination, withPromoEvent);

                    if (dateGotEvent != null && dateGotEvent.Any(d => string.IsNullOrWhiteSpace(d.EventDetailsID)))
                    {
                        return new List<ProductEventTicket.TicketInformation>();
                    }

                    int eventId = xx.Master.ToInt();
                    IEnumerable<int> productIdList = xx.Item.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.ToInt());
                    var _productInfo = checkout.SellItemsAvailable.EventProducts.DetailsInfo.Where(d => productIdList.Any(p => p == d.EventProductID)
                    && d.TicketCategoryCode == xx.Cat && ((d.EventDate == xx.Date && (xx.Type == "GT" || xx.Type == "CR" || xx.Type == "TH")) || (xx.Type != "GT" && xx.Type != "TH")));
                    var _productMaster = checkout.SellItemsAvailable.EventProducts.HeaderInfo.FirstOrDefault(d => d.EventID == eventId)
                    ?? new usp_EventMasterSelect_Result();

                    return _productInfo.Select(x => new ProductEventTicket.TicketInformation
                    {
                        OrderedDateTime = x.EventDate,
                        OrderedQty = xx.Qty,
                        EventID = eventId,
                        EventProductID = x.EventProductID,
                        TicketCategoryCode = xx.Cat,
                        EntranceGate = x.EntranceGate,
                        TicketBalance = x.TicketBalance,
                        TicketCategory = x.TicketCategory,
                        TicketCost = x.TicketCost,
                        TicketMarkup = x.TicketMarkup,
                        TicketProcessingFee = x.TicketProcessingFee,
                        TicketQty = x.TicketQty,
                        TicketSellingPrice = x.TicketSellingPrice,
                        TicketStatus = x.TicketStatus,
                        ItemDesc = _productMaster.EventName,
                        EventDate = x.EventDate,
                        EventStartTime = x.EventStartTime,
                        EventEndTime = x.EventEndTime,
                        EventTypeCode = x.EventTypeCode,
                        EventDetailsID = x.EventDetailsID,
                    });
                }).ToList();

                if (ticketSelectedInfo.Count <= 0)
                {
                    ModelState.AddModelError("Error", "Please try again later, product not found.");
                    return View(postBackViewPath, checkout);
                }
                else if (ticketSelectedInfo.Any(x => x.EventID == -1 || x.EventProductID == -1))
                {
                    ModelState.AddModelError("Error", "Please try again later, product not found.");
                    return View(postBackViewPath, checkout);
                }
                else if (ticketSelectedInfo.Any(x => x.OrderedQty > x.TicketBalance))
                {
                    ModelState.AddModelError("Error", "Concert Ticket selected has been sold out, please select another category.");
                    return View(postBackViewPath, checkout);
                }

                ProductPricingDetail eventProductsPricingDetail = new ProductPricingDetail()
                {
                    Currency = checkout.CheckOutSummary.CurrencyCode // db now don't have currency code
                };
                List<GTM_AddOnProductModel> GTM_TrackAddOnList = new List<GTM_AddOnProductModel>();
                foreach (var item in ticketSelectedInfo.GroupBy(grp => new
                {
                    grp.ItemDesc,
                    grp.EventTypeCode,
                    grp.TicketCategory,
                    grp.OrderedDateTime,
                    grp.OrderedQty
                }))
                {
                    eventProductsPricingDetail.ItemInsert(new ProductItem
                    {
                        BaseRate = item.Sum(s => s.FrontEndTicketPrice),
                        ItemQty = item.Key.OrderedQty,
                        ItemDetail = (item.Key.EventTypeCode == "CT" || item.Key.EventTypeCode == "GT") ? string.Format("{0} {1}", item.Key.ItemDesc, item.Key.TicketCategory)
                        : (/*item.Key.EventTypeCode == "TH" ||*/ item.Key.EventTypeCode == "WF" || item.Key.EventTypeCode == "CR" || item.Key.EventTypeCode == "DR") ? string.Format("{0}", item.Key.ItemDesc)
                        : string.Format("{0} - {1}", item.Key.ItemDesc, item.Key.OrderedDateTime.ToString("dd-MMM-yy, ddd")),
                        Supplier_TotalAmt = item.Sum(x => x.TicketCost) * item.Key.OrderedQty,
                        ProductDate = item.Key.OrderedDateTime,
                    });
                    //Add On GTM
                    GTM_TrackAddOnList.Add(new GTM_AddOnProductModel
                    {
                        name = item.Key.ItemDesc,
                        id = item.FirstOrDefault().EventProductID.ToString(),
                        price = item.FirstOrDefault().FrontEndTicketPrice.ToString("0.00"), //baseRate
                        quantity = item.Key.OrderedQty
                    });
                }

                var searchInfo = checkout.SellItemsAvailable.EventProducts.SearchInfo;

                var concert = new ProductEventTicket(searchInfo.DateFrom, searchInfo.DateTo, searchInfo.ProductType, searchInfo.Destination)
                {
                    ContactPerson = checkout.ContactPerson,
                    ProductSeq = 4,
                    TicketInfo = ticketSelectedInfo,
                    PricingDetail = eventProductsPricingDetail,
                };

                if (checkout.AddOnProduct != null)
                {
                    checkout.RemoveProduct(ProductTypes.AddOnProducts);
                }
                checkout.InsertProduct(concert);
                Core.SetSession(Enumeration.SessionName.GTM_trackAddOnSelected, tripid, JsonConvert.SerializeObject(GTM_TrackAddOnList));
            }

            checkout.CheckoutStep = 5;

            //return RedirectToAction("GuestDetails", new { tripid, affiliationId });
            return RedirectToAction("MiniScreenPayment", "CheckOut", new { tripid });
        }


        private IEnumerable<CrossSaleRule> CheckIsCrossSalesHotelAvailaible(FlightMasterInfo flightSelected, MayFlower dbContext = null)
        {
            dbContext = dbContext ?? new MayFlower();
            var result = dbContext.CrossSaleRules
                .Where(x => x.IsActive && DateTime.Now >= x.BookingDateFrom && DateTime.Now <= x.BookingDateTo
                        && x.IsActive && flightSelected.DepartDate >= x.TravelDateFrom && flightSelected.ReturnDate <= x.TravelDateTo
                        && x.Destination == flightSelected.Destination)
                .Include(x => x.CrossSaleRuleHotels)
                .AsEnumerable();

            bool anyAirlineOK = result.Any(x => x.AirlineCode != "-");
            if ((result.Where(x => x.AirlineCode == flightSelected.Airline.First()).Count() == 0) || flightSelected.Airline.Count > 1)
            {
                result = result.Where(x => x.AirlineCode == "-");
            }
            else if (anyAirlineOK && flightSelected.Airline.Count == 1)
            {
                // For specified airline
                result = result.Where(x => x.AirlineCode == flightSelected.Airline.First());
            }

            return result.Count() > 0 ? result : null;
        }

        private async Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> GetHotelFromSearchModel(SearchHotelModel searchModel)
        {
            return await Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
            {
                return ESBHotelServiceCall.GetHotelList(searchModel);
            });
        }

        private Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> GetHotelFromSearchModel(SearchHotelModel searchModel, List<string> hotelID)
        {
            var emptyResult = Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
            {
                return null;
            });

            try
            {
                System.Threading.CancellationTokenSource tokenSource = new System.Threading.CancellationTokenSource();
                tokenSource.CancelAfter(5000);
                //return Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelListAsync(searchModel, hotelID);
                var task = Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>.Factory.StartNew(() =>
                {
                    return Alphareds.Module.ServiceCall.ESBHotelServiceCall.GetHotelList(searchModel, hotelID);
                }, tokenSource.Token);

                //Task[] taskList = new Task[] { task };

                //Task.WaitAny(taskList, (int)5000, tokenSource.Token);
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

        #region Cross sell - call ESB for the 3 hotel id separately if different hotel supplier 
        private async Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse> GetHotelFromSearchModel(SearchHotelModel searchModel, List<CrossSaleRuleHotel> hotelCrossSaleRule,
            bool getAsGroup = false)
        {
            Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse hotelList = new Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse();
            List<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelInformation> hotelInfo = null;

            if (getAsGroup)
            {
                foreach (var eachSupplier in hotelCrossSaleRule.GroupBy(x => x.HotelSupplierCode))
                {
                    SearchHotelModel tempSearchModel = searchModel.DeepCopy();
                    List<string> hotelID = new List<string>();

                    hotelID.AddRange(eachSupplier.Select(x => x.HotelID).ToList());
                    tempSearchModel.SupplierIncluded = new SearchSupplier()
                    {
                        Expedia = eachSupplier.Key == "EAN",
                        ExpediaTAAP = eachSupplier.Key == "TAAP",
                        HotelBeds = eachSupplier.Key == "HB",
                        JacTravel = eachSupplier.Key == "JAC",
                        Tourplan = eachSupplier.Key == "TP",
                        EANRapid = eachSupplier.Key == "RAP",
                    };

                    var getHotel = await GetHotelFromSearchModel(tempSearchModel, hotelID);

                    if (getHotel?.HotelList != null && getHotel.HotelList.Length > 0)
                    {
                        if (hotelInfo == null)
                        {
                            hotelInfo = getHotel.HotelList.Take(3).ToList();
                            hotelList = getHotel;
                        }
                        else
                        {
                            int remainHotelCount = 3 - getHotel.HotelList.Length;

                            if (remainHotelCount > 0)
                            {
                                hotelInfo.AddRange(getHotel.HotelList.Take(remainHotelCount).ToList());
                            }
                            else
                            {
                                break; // exit loop
                            }
                        }
                    }
                    else
                    {
                        hotelList.Errors = getHotel.Errors ?? hotelList.Errors;
                    }
                }

                if (hotelList != null && hotelInfo != null && hotelInfo.Count > 0)
                {
                    hotelList.HotelList = hotelInfo.ToArray();
                }
            }
            else
            {
                int hotelTaken = 0;
                List<Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>> _taskHotelList =
                    new List<Task<Alphareds.Module.ESBHotelComparisonWebService.ESBHotel.HotelListResponse>>();

                foreach (var hotel in hotelCrossSaleRule)
                {
                    SearchHotelModel tempSearchModel = searchModel.DeepCopy();
                    List<string> hotelID = new List<string>();

                    hotelID.Add(hotel.HotelID);
                    tempSearchModel.SupplierIncluded = new SearchSupplier()
                    {
                        Expedia = hotel.HotelSupplierCode == "EAN",
                        ExpediaTAAP = hotel.HotelSupplierCode == "TAAP",
                        HotelBeds = hotel.HotelSupplierCode == "HB",
                        JacTravel = hotel.HotelSupplierCode == "JAC",
                        Tourplan = hotel.HotelSupplierCode == "TP",
                        EANRapid = hotel.HotelSupplierCode == "RAP",
                    };

                    if (User.Identity.IsAuthenticated)
                    {
                        tempSearchModel.UserLoginID = CustomPrincipal.LoginID;
                    }

                    _taskHotelList.Add(GetHotelFromSearchModel(tempSearchModel, hotelID));
                }

                for (int i = 0; i < _taskHotelList.Count; i++)
                {
                    if (hotelTaken >= 3)
                        break; // exit loop if enough 3 hotel

                    // Take first completed hotel result from API
                    int ixTaskCompleted = Task.WaitAny(_taskHotelList.ToArray());

                    var _tskCompleted = _taskHotelList[ixTaskCompleted];
                    if (!_tskCompleted.IsCanceled && !_tskCompleted.IsFaulted && _tskCompleted.IsCompleted)
                    {
                        var _result = _tskCompleted.Result;
                        bool isNotNull = hotelList?.HotelList != null;
                        hotelList = hotelList?.HotelList == null ? _result : hotelList;

                        if (_result?.HotelList?.Length > 0 && isNotNull)
                        {
                            hotelList.HotelList = hotelList.HotelList.Concat(_result.HotelList).ToArray();
                        }

                        hotelTaken += _result?.HotelList?.Length ?? 0;
                    }

                    if (_taskHotelList.Remove(_tskCompleted))
                    {
                        // Minus for loop index.
                        i = i - 1;
                    }
                }
            }

            return hotelList;
        }
        #endregion

        protected void QuoteInsurance(ref CheckoutProduct checkoutModel)
        {
            List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote> res = null;

            MayFlower db = new MayFlower();

            try
            {
                res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetTravelQuote(checkoutModel);

                checkoutModel.SellItemsAvailable.Insurance = new CrossSellItemsAvailable.InsuranceInformation
                {
                    ServiceRespond = new List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote>(),
                    QuotedInformations = new List<CrossSellItemsAvailable.InsuranceInformation.QuotedInformation>(),
                };

                foreach (var item in res)
                {
                    if (item.Errors != null)
                    {
                        // No log Not Supported Error Message
                        if ((item.Errors.ErrorMessage == null ||
                            (item.Errors.ErrorMessage != null && !item.Errors.ErrorMessage.ToLower().Contains("not supported"))))
                        {
                            logger.Error("Error return when quote insurance. "
                                + "Destination - From :" + checkoutModel.Flight.SearchFlightInfo.DepartureStationCode.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.ArrivalStationCode.ToString() + Environment.NewLine
                                + "Date - From :" + checkoutModel.Flight.SearchFlightInfo.BeginDate.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.EndDate.ToString()
                                + Environment.NewLine + Environment.NewLine
                                + JsonConvert.SerializeObject(item, Formatting.Indented));
                        }

                        // Break out avoid continue to next line code while looping
                        break;
                    }

                    var quoteStatus = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.MsgStatus;
                    bool isQuoteSuccess = quoteStatus.MsgStatusCd == "Success";

                    //in case have no "item.Errors" but Quote not accepted
                    if (isQuoteSuccess)
                    {
                        checkoutModel.SellItemsAvailable.Insurance.ServiceRespond = res;
                        var resQuotedInfo = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo;

                        #region calculate insurance cost
                        var insuranceMarkup = db.Markups.FirstOrDefault(x => x.MarkupText == "InsuranceCostPercentage");
                        decimal insuranceCostPercentage = insuranceMarkup?.MarkupPrice ?? 0;
                        decimal totalInsuranceCost = 0;
                        if (insuranceCostPercentage != 0 && insuranceMarkup?.MarkupType?.MarkupType1 == "Insurance")
                        {
                            totalInsuranceCost = (resQuotedInfo.InsuredFullToBePaidAmt.Amt.ToDecimal() * (insuranceCostPercentage / 100)).RoundToDecimalPlace();
                        }
                        #endregion

                        CrossSellItemsAvailable.InsuranceInformation.QuotedInformation quotedInformation = new CrossSellItemsAvailable.InsuranceInformation.QuotedInformation
                        {
                            CurrencyCode = resQuotedInfo.InsuredFullToBePaidAmt.CurCd,
                            Price = resQuotedInfo.InsuredFullToBePaidAmt.Amt.ToDecimal(),
                            EffectiveDate = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.EffectiveDt.ToDateTime(),
                            ExpirationDate = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.ExpirationDt.ToDateTime(),
                            TermsConditions = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo.CoverageConditionsInd,
                            InsurancedAddress = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.InsuredOrPrincipal.GeneralPartyInfo.Addr.Addr1,
                            PlanType = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_Plan.PlanDesc,
                            Package = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_InsuredPackage.InsuredPackageDesc,

                            insuranceCost = totalInsuranceCost
                        };
                        checkoutModel.SellItemsAvailable.Insurance.QuotedInformations.Add(quotedInformation);
                    }
                    else
                    {
                        logger.Error("Quote Insurance not success. "
                                + "Destination - From :" + checkoutModel.Flight.SearchFlightInfo.DepartureStationCode.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.ArrivalStationCode.ToString() + Environment.NewLine
                                + "Date - From :" + checkoutModel.Flight.SearchFlightInfo.BeginDate.ToString() + " , To :" + checkoutModel.Flight.SearchFlightInfo.EndDate.ToString()
                                + "Status Message :" + quoteStatus.MsgStatusDesc);
                    }
                }
            }
            catch (AggregateException ae)
            {
                logger.Error(ae.GetBaseException(), "Error when quote insurance.");
            }
            catch (Exception ex)
            {
                logger.Error(ex.GetBaseException(), "Error when quote insurance.");
            }
            finally
            {
                if (checkoutModel.SellItemsAvailable.Insurance.QuotedInformations.Count == 0)
                {
                    checkoutModel.SellItemsAvailable.Insurance = null;
                }
            }
        }

        protected void AddInsuranceToPayment(ref CheckoutProduct checkoutModel)
        {
            var productinsurance = new ProductInsurance()
            {
                ContactPerson = checkoutModel.ContactPerson,
                ProductSeq = 3,
                PricingDetail = checkoutModel.SellItemsAvailable.Insurance.PricingDetail,
                TotalQuotePax = checkoutModel.SellItemsAvailable.Insurance.ServiceRespond.FirstOrDefault().Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.InsuredOrPrincipal.GeneralPartyInfo.NameInfo.Count() - 1,
                PlanDesc = checkoutModel.SellItemsAvailable.Insurance.QuotedInformations.FirstOrDefault().PlanType
            };
            checkoutModel.InsertProduct(productinsurance);
        }


        protected ProductReserve.BookingRespond ConfirmInsuranceQuotation(BookingInsurance bookingInsurance, string superPNRNo)
        {
            MayFlower db = new MayFlower();
            var res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetTravelPolicy(bookingInsurance, superPNRNo);
            List<Tuple<string, bool>> bookStatus = new List<Tuple<string, bool>>();
            foreach (var x in res)
            {
                try
                {
                    var policyDetails = x.Body?.GetTravelPolicyResponse?.ACORD?.InsuranceSvcRs?.PersPkgPolicyAddRs;
                    var insuredPeopleName = policyDetails.InsuredOrPrincipal?.GeneralPartyInfo?.NameInfo[0].PersonName;
                    var paxInfo = bookingInsurance.InsurancePaxes.FirstOrDefault(p =>
                    p.GivenName.Trim() == insuredPeopleName.GivenName.Trim() && p.SurName.Trim() == insuredPeopleName.Surname.Trim());

                    var paxPersons = policyDetails.InsuredOrPrincipal?.GeneralPartyInfo?.NameInfo;
                    //int paxIdCheck = 0;

                    if (x.Errors != null && policyDetails != null
                        && policyDetails.MsgStatus.MsgStatusCd != "Success")
                    {
                        bookingInsurance.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        bookingInsurance.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;

                        logger.Error("Reserve Insurance Failed on SuperPNRNo " + superPNRNo + " , Named: " + paxInfo.GivenName + " " + paxInfo.SurName);
                        bookStatus.Add(new Tuple<string, bool>("Reserve Insurance Failed - " + policyDetails.MsgStatus.MsgStatusCd + Environment.NewLine + Environment.NewLine +
                        insuredPeopleName?.GivenName?.Trim() + " " + insuredPeopleName?.Surname?.Trim(), false));
                    }
                    else if (policyDetails.MsgStatus.MsgStatusCd == "Success")
                    {
                        // TODO: Update reserveed information to dbContext

                        bookingInsurance.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        bookingInsurance.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;

                        #region  Currenly now no need GetPolicyDetail
                        //try
                        //{
                        //    #region GetPolicyDetail if result from GetTravelPolicy is success //need do checking?
                        //    var flag = "Settlement"; //hard code settlement for GetPolicyDetail #checkcheck
                        //    Alphareds.Module.ACEInsuranceWebService.ACEIns.Flag sentFlag = Alphareds.Module.ACEInsuranceWebService.ACEIns.Flag.Settlement;
                        //    Enum.TryParse(flag, true, out sentFlag);
                        //    var testGetPolicyDetail = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.GetPolicyDetails(paxInfo.PolicyNumber, sentFlag);
                        //    var MsgStatus = testGetPolicyDetail.Body.GetTravelPolicyDetailsResponse.ArrayOfACORD_PolicyResp.ACORD.InsuranceSvcRs.PersPkgPolicyAddRs.MsgStatus;

                        //    if (testGetPolicyDetail.Errors != null && MsgStatus.MsgStatusCd != "Success")
                        //    {
                        //        paxInfo.PolicyStatus = testGetPolicyDetail.Body.GetTravelPolicyDetailsResponse.ArrayOfACORD_PolicyResp.ACORD.InsuranceSvcRs.PersPkgPolicyAddRs.PersPolicy.PolicyStatusCd;
                        //        logger.Error("Insurance Policy Detail Failed on SuperPNRNo " + superPNRNo + " , Named: " + paxInfo.GivenName + " " + paxInfo.SurName);
                        //        bookStatus.Add(new Tuple<string, bool>("Reserve PolicyDetail Failed - " + policyDetails.MsgStatus.MsgStatusCd + Environment.NewLine + Environment.NewLine +
                        //            insuredPeopleName?.GivenName?.Trim() + " " + insuredPeopleName?.Surname?.Trim(), false));
                        //    }
                        //    else if (MsgStatus.MsgStatusCd == "Success")
                        //    {
                        //        paxInfo.PolicyStatus = testGetPolicyDetail.Body.GetTravelPolicyDetailsResponse.ArrayOfACORD_PolicyResp.ACORD.InsuranceSvcRs.PersPkgPolicyAddRs.PersPolicy.PolicyStatusCd;
                        //        bookStatus.Add(new Tuple<string, bool>(insuredPeopleName.GivenName ?? "", true));
                        //    }
                        //    else
                        //    {
                        //        paxInfo.PolicyStatus = testGetPolicyDetail.Body.GetTravelPolicyDetailsResponse.ArrayOfACORD_PolicyResp.ACORD.InsuranceSvcRs.PersPkgPolicyAddRs.PersPolicy.PolicyStatusCd;
                        //        logger.Error("Unknow Status on Insurance Policy Detail Failed on SuperPNRNo " + superPNRNo + " , Named: " + paxInfo.GivenName + " " + paxInfo.SurName);
                        //        bookStatus.Add(new Tuple<string, bool>("Unknow Status on Reserve PolicyDetail Failed - " + policyDetails.MsgStatus.MsgStatusCd + Environment.NewLine + Environment.NewLine +
                        //            insuredPeopleName?.GivenName?.Trim() + " " + insuredPeopleName?.Surname?.Trim(), false));
                        //    }
                        //    #endregion
                        //}
                        //catch (Exception ex)
                        //{
                        //    logger.Error(ex, "Error When GetPolicyDetail" + DateTime.Today.ToLoggerDateTime());
                        //    bookStatus.Add(new Tuple<string, bool>("Catch ex when GetPolicyDetail - " + policyDetails.MsgStatus.MsgStatusCd + Environment.NewLine + Environment.NewLine +
                        //            insuredPeopleName?.GivenName?.Trim() + " " + insuredPeopleName?.Surname?.Trim(), false));
                        //}
                        #endregion
                        bookStatus.Add(new Tuple<string, bool>(insuredPeopleName.GivenName ?? "", true));
                    }
                    else
                    {
                        bookingInsurance.PolicyNumber = policyDetails.PersPolicy.PolicyNumber;
                        bookingInsurance.PolicyStatus = policyDetails.PersPolicy.PolicyStatusCd;

                        bookStatus.Add(new Tuple<string, bool>("Unknow status - " + policyDetails.MsgStatus.MsgStatusCd + Environment.NewLine + Environment.NewLine +
                        insuredPeopleName?.GivenName?.Trim() + " " + insuredPeopleName?.Surname?.Trim(), false));
                    }
                }
                catch (Exception ex)
                {
                    string displayErrorMessage = res.FirstOrDefault().Errors.ErrorMessage ?? "errorMessageField is null";
                    logger.Error(ex, "An error occured when attemp to get insurance policy, Error message: " + displayErrorMessage + ". At date:" + DateTime.Today.ToLoggerDateTime());
                    bookStatus.Add(new Tuple<string, bool>("Catch ex when ConfirmInsuranceQuotation on" + superPNRNo, false));
                }
            }

            return new ProductReserve.BookingRespond
            {
                SuperPNRNo = superPNRNo,
                BatchBookResult = bookStatus.All(x => x.Item2) ? ProductReserve.BookResultType.AllSuccess
                : bookStatus.All(x => !x.Item2) ? ProductReserve.BookResultType.AllFail
                : ProductReserve.BookResultType.PartialSuccess,
            };
        }
        #endregion

        #region EventBundle and reserveEventBundle standalone
        [HttpGet]
        public ActionResult EventBundle(string rcode = null)
        {
            string environment = Core.GetAppSettingValueEnhanced("Apps.Environment");
            string tripid = Guid.NewGuid().ToString();

            MayFlower db = new MayFlower();
            CheckoutProduct checkoutProduct = new CheckoutProduct();

            //add filter make production display the event not yet start
            if (environment?.ToLower() == "production" && (DateTime.Now < new DateTime(2018, 9, 4, 11, 0, 0) && DateTime.Now > new DateTime(2018, 9, 2, 15, 0, 0)))
            {
                //ModelState.AddModelError("Error", "Oops! Deal will be starting soon on  2/9/2018 12pm!" + Environment.NewLine + " Kindly register as member now to speed up your purchasing experience on ticket selling date!");
                ViewBag.EMassage = "Register as a member now to speed up your checkout process. ";
                return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
            }

            try
            {
                #region filter Agent
                if (IsAgentUser)
                {
                    //ModelState.AddModelError("Error", "Sorry Currently EventBundle are not available for Agent.");
                    ViewBag.EMassage = "Sorry Currently EventBundle are not available for Agent.";
                    return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
                }
                #endregion

                if (rcode == null)
                {
                    //add ticket into checkoutProduct
                    checkoutProduct = addEventTicketTW(checkoutProduct);
                    if (checkoutProduct.SellItemsAvailable.EventProducts == null)
                    {
                        ViewBag.EMassage = "Oops! The sales is End";
                        return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
                    }
                }
                else
                {
                    //add reserve item
                    var reserveEB = db.ReserveEventSets.FirstOrDefault(x => x.ReserveCode == rcode);
                    if (reserveEB == null)
                    {
                        ViewBag.EMassage = "Invalid Code";
                        return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
                    }
                    else if (reserveEB.IsUsed == true)
                    {
                        ViewBag.EMassage = "The Code is already Used";
                        return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
                    }
                    else if (reserveEB.IsActive != true)
                    {
                        ViewBag.EMassage = "Inactive Code";
                        return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
                    }

                    checkoutProduct.EventBundleReserveCode = rcode;
                    var reserveEBDetail = reserveEB.ReserveEventDetails.ToList();
                    var lololo = reserveEBDetail.FirstOrDefault().EventProduct.EventProductID;//test can get the ID or not
                    checkoutProduct = addReserveEBTicket(checkoutProduct, rcode);
                    if (checkoutProduct.SellItemsAvailable.EventProducts == null)
                    {
                        ViewBag.EMassage = "Oops! The sales is End";
                        return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.GetBaseException(), "Error when get Event from DB.");
            }
            ViewBag.tripid = tripid;

            Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkoutProduct);
            //return View("~/Views/EventBundle/EventBundle.cshtml", checkoutProduct);
            return View("~/Views/Checkout/AddOn_v2.cshtml", checkoutProduct);

        }

        [HttpPost]
        [Filters.SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult EventBundle(string dataToken, string ConcertTicket, string tripid) //if need buy alot then need list
        {
            CheckoutProduct checkoutProduct = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            List<AddonTicketPOSTBack> ticketList = new List<AddonTicketPOSTBack>();
            bool isEmptyConcert = string.IsNullOrWhiteSpace(ConcertTicket);
            int totalConcertTicket = 0;
            string pType = "Flight";
            string flightOrigin = "XXX";
            string destination = "XXX";

            try
            {
                if (checkoutProduct.SellItemsAvailable.EventProducts != null)
                {
                    var eventList = checkoutProduct.SellItemsAvailable.EventProducts.HeaderInfo.Select(x => x.EventTypeCode).Distinct();

                    foreach (var item in eventList)
                    {
                        var formItem = Request.Form[item];

                        if (!string.IsNullOrWhiteSpace(formItem))
                        {
                            isEmptyConcert = false;
                            ticketList.AddRange(JsonConvert.DeserializeObject<List<AddonTicketPOSTBack>>(formItem, new Newtonsoft.Json.Converters.IsoDateTimeConverter { DateTimeFormat = "dd-MM-yyyy" }));
                        }
                    }
                }
                if (!isEmptyConcert && checkoutProduct.SellItemsAvailable?.EventProducts != null)
                {
                    totalConcertTicket = ticketList.Sum(x => x.Qty);
                }

                #region add Concert and TW to product
                if (totalConcertTicket > 0)
                {
                    bool withPromoEvent = checkoutProduct.PromoCodeFunctions.GetFrontendFunction.DisplayPromoEvent;

                    var ticketSelectedInfo = ticketList.SelectMany(xx =>
                    {
                        var dateGotEvent = eventDBFunc.GetEventMaster(xx.Date, xx.Date, pType, flightOrigin, destination, withPromoEvent);

                        if (dateGotEvent != null && dateGotEvent.Any(d => string.IsNullOrWhiteSpace(d.EventDetailsID)))
                        {
                            return new List<ProductEventTicket.TicketInformation>();
                        }

                        int eventId = xx.Master.ToInt();
                        IEnumerable<int> productIdList = xx.Item.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.ToInt());
                        var _productInfo = checkoutProduct.SellItemsAvailable.EventProducts.DetailsInfo.Where(d => productIdList.Any(p => p == d.EventProductID)
                        && d.TicketCategoryCode == xx.Cat && ((d.EventDate == xx.Date && (xx.Type == "GT" || xx.Type == "CR" || xx.Type == "TH" || xx.Type == "CT")) || (xx.Type != "GT" && xx.Type != "TH")));
                        var _productMaster = checkoutProduct.SellItemsAvailable.EventProducts.HeaderInfo.FirstOrDefault(d => d.EventID == eventId)
                        ?? new usp_EventMasterSelect_Result();

                        return _productInfo.Select(x => new ProductEventTicket.TicketInformation
                        {
                            OrderedDateTime = x.EventDate,
                            OrderedQty = xx.Qty,
                            EventID = eventId,
                            EventProductID = x.EventProductID,
                            TicketCategoryCode = xx.Cat,
                            EntranceGate = x.EntranceGate,
                            TicketBalance = x.TicketBalance,
                            TicketCategory = x.TicketCategory,
                            TicketCost = x.TicketCost,
                            TicketMarkup = x.TicketMarkup,
                            TicketProcessingFee = x.TicketProcessingFee,
                            TicketQty = x.TicketQty,
                            TicketSellingPrice = x.TicketSellingPrice,
                            TicketStatus = x.TicketStatus,
                            ItemDesc = _productMaster.EventName,
                            EventDate = x.EventDate,
                            EventStartTime = x.EventStartTime,
                            EventEndTime = x.EventEndTime,
                            EventTypeCode = x.EventTypeCode,
                            EventDetailsID = x.EventDetailsID,
                        });
                    }).ToList();

                    if (ticketSelectedInfo.Count > 0)
                    {
                        MayFlower _db = new MayFlower();

                        if (checkoutProduct.EventBundleReserveCode == null)
                        {
                            List<int> evId = new List<int>();
                            foreach (var item in ticketSelectedInfo)
                            {
                                evId.Add(item.EventID);
                            }
                            var bundleList = _db.EventBundles.Where(x => evId.Any(a => a == x.EventID) && x.IsActive == true); //this part need check

                            foreach (var bundle in bundleList)
                            {

                                foreach (var bundleDetail in bundle.EventBundleDetails)
                                {
                                    var eventDetail = bundleDetail.EventProduct;
                                    ticketSelectedInfo.Add(new ProductEventTicket.TicketInformation
                                    {
                                        OrderedDateTime = ticketSelectedInfo.FirstOrDefault().OrderedDateTime,
                                        OrderedQty = ticketSelectedInfo.Where(x => x.EventTypeCode == "CT").Sum(x => x.OrderedQty), //TW KC say need per ticket
                                        EventID = eventDetail.EventDetail.EventID,
                                        EventProductID = eventDetail.EventProductID,
                                        TicketCategoryCode = eventDetail.TicketCategory.TicketCategoryCode,
                                        EntranceGate = eventDetail.EntranceGate,
                                        TicketBalance = eventDetail.TicketBalance,
                                        TicketCategory = eventDetail.TicketCategory.TicketCategory1,
                                        TicketCost = eventDetail.TicketCost,
                                        TicketMarkup = eventDetail.TicketMarkup,
                                        TicketProcessingFee = eventDetail.TicketProcessingFee,
                                        TicketQty = eventDetail.TicketQty,
                                        TicketSellingPrice = eventDetail.TicketSellingPrice,
                                        TicketStatus = eventDetail.TicketBalance > 0 ? "Available" : "Sold Out",
                                        ItemDesc = eventDetail.EventDetail.EventMaster.EventName,
                                        EventDate = eventDetail.EventDetail.EventMaster.EventStartDate, //not sure on this
                                        EventStartTime = eventDetail.EventDetail.EventStartTime,
                                        EventEndTime = eventDetail.EventDetail.EventEndTime,
                                        EventTypeCode = "TW",
                                        EventDetailsID = eventDetail.EventDetailsID,
                                    });
                                }
                            }
                        }
                        else
                        {
                            //have code
                            //use the code find back TW event
                            var reserveEB = _db.ReserveEventSets.FirstOrDefault(x => x.ReserveCode == checkoutProduct.EventBundleReserveCode);
                            var reserveEBDetail = reserveEB.ReserveEventDetails.ToList();
                            //for TW eventDetail
                            var TWEventDetail = reserveEBDetail.FirstOrDefault(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode == "TW").EventProduct;

                            ticketSelectedInfo.Add(new ProductEventTicket.TicketInformation
                            {
                                OrderedDateTime = ticketSelectedInfo.FirstOrDefault().OrderedDateTime,
                                OrderedQty = ticketSelectedInfo.Where(x => x.EventTypeCode == "CT").Sum(x => x.OrderedQty), //TW KC say need per ticket
                                EventID = TWEventDetail.EventDetail.EventID,
                                EventProductID = TWEventDetail.EventProductID,
                                TicketCategoryCode = TWEventDetail.TicketCategory.TicketCategoryCode,
                                EntranceGate = TWEventDetail.EntranceGate,
                                TicketBalance = TWEventDetail.TicketBalance,
                                TicketCategory = TWEventDetail.TicketCategory.TicketCategory1,
                                TicketCost = TWEventDetail.TicketCost,
                                TicketMarkup = TWEventDetail.TicketMarkup,
                                TicketProcessingFee = TWEventDetail.TicketProcessingFee,
                                TicketQty = TWEventDetail.TicketQty,
                                TicketSellingPrice = TWEventDetail.TicketSellingPrice,
                                TicketStatus = TWEventDetail.TicketBalance > 0 ? "Available" : "Sold Out",
                                ItemDesc = TWEventDetail.EventDetail.EventMaster.EventName,
                                EventDate = TWEventDetail.EventDetail.EventMaster.EventStartDate, //not sure on this
                                EventStartTime = TWEventDetail.EventDetail.EventStartTime,
                                EventEndTime = TWEventDetail.EventDetail.EventEndTime,
                                EventTypeCode = "TW",
                                EventDetailsID = TWEventDetail.EventDetailsID,
                            });
                        }

                        #region no rcode before edit
                        //List<int> evId = new List<int>();
                        //foreach (var item in ticketSelectedInfo)
                        //{
                        //    evId.Add(item.EventID);
                        //}
                        //var bundleList = _db.EventBundles.Where(x => evId.Any(a => a == x.EventID) && x.IsActive == true); //this part need check

                        //foreach (var bundle in bundleList)
                        //{

                        //    foreach(var bundleDetail in bundle.EventBundleDetails)
                        //    {
                        //        var eventDetail = bundleDetail.EventProduct;
                        //        ticketSelectedInfo.Add(new ProductEventTicket.TicketInformation
                        //        {
                        //            OrderedDateTime = ticketSelectedInfo.FirstOrDefault().OrderedDateTime,
                        //            OrderedQty = ticketSelectedInfo.Where(x => x.EventTypeCode == "CT").Sum(x => x.OrderedQty), //TW KC say need per ticket
                        //            EventID = eventDetail.EventDetail.EventID,
                        //            EventProductID = eventDetail.EventProductID,
                        //            TicketCategoryCode = eventDetail.TicketCategory.TicketCategoryCode,
                        //            EntranceGate = eventDetail.EntranceGate,
                        //            TicketBalance = eventDetail.TicketBalance,
                        //            TicketCategory = eventDetail.TicketCategory.TicketCategory1,
                        //            TicketCost = eventDetail.TicketCost,
                        //            TicketMarkup = eventDetail.TicketMarkup,
                        //            TicketProcessingFee = eventDetail.TicketProcessingFee,
                        //            TicketQty = eventDetail.TicketQty,
                        //            TicketSellingPrice = eventDetail.TicketSellingPrice,
                        //            TicketStatus = eventDetail.TicketBalance > 0 ? "Available" : "Sold Out",
                        //            ItemDesc = eventDetail.EventDetail.EventMaster.EventName,
                        //            EventDate = eventDetail.EventDetail.EventMaster.EventStartDate, //not sure on this
                        //            EventStartTime = eventDetail.EventDetail.EventStartTime,
                        //            EventEndTime = eventDetail.EventDetail.EventEndTime,
                        //            EventTypeCode = "TW",
                        //            EventDetailsID = eventDetail.EventDetailsID,
                        //        });
                        //    }
                        //}
                        #endregion
                    }

                    if (ticketSelectedInfo.Count <= 0)
                    {
                        ModelState.AddModelError("Error", "Please try again later, product not found.");
                        return View(checkoutProduct);
                    }
                    else if (ticketSelectedInfo.Any(x => x.EventID == -1 || x.EventProductID == -1))
                    {
                        ModelState.AddModelError("Error", "Please try again later, product not found.");
                        return View(checkoutProduct);
                    }
                    else if (ticketSelectedInfo.Any(x => x.OrderedQty > x.TicketBalance))
                    {
                        ModelState.AddModelError("Error", "Concert Ticket selected has been sold out, please select another category.");
                        return View(checkoutProduct);
                    }

                    ProductPricingDetail eventProductsPricingDetail = new ProductPricingDetail()
                    {
                        Currency = "MYR"// checkoutProduct.CheckOutSummary.CurrencyCode // db now don't have currency code
                    };
                    List<GTM_AddOnProductModel> GTM_TrackAddOnList = new List<GTM_AddOnProductModel>();
                    foreach (var item in ticketSelectedInfo.GroupBy(grp => new
                    {
                        grp.ItemDesc,
                        grp.EventTypeCode,
                        grp.TicketCategory,
                        grp.OrderedDateTime,
                        grp.OrderedQty
                    }))
                    {
                        eventProductsPricingDetail.ItemInsert(new ProductItem
                        {
                            BaseRate = item.Sum(s => s.FrontEndTicketPrice),
                            ItemQty = item.Key.OrderedQty,
                            ItemDetail = (item.Key.EventTypeCode == "CT" || item.Key.EventTypeCode == "GT") ? string.Format("{0} {1}", item.Key.ItemDesc, item.Key.TicketCategory)
                            : (/*item.Key.EventTypeCode == "TH" ||*/ item.Key.EventTypeCode == "WF" || item.Key.EventTypeCode == "CR" || item.Key.EventTypeCode == "DR" || item.Key.EventTypeCode == "TW") ? string.Format("{0}", item.Key.ItemDesc)
                            : string.Format("{0} - {1}", item.Key.ItemDesc, item.Key.OrderedDateTime.ToString("dd-MMM-yy, ddd")),
                            Supplier_TotalAmt = item.Sum(x => x.TicketCost) * item.Key.OrderedQty,
                            ProductDate = item.Key.OrderedDateTime,
                        });
                    }

                    var eventSearchInfo = checkoutProduct.SellItemsAvailable.EventProducts.SearchInfo;

                    var eventBundle = new ProductEventTicket(eventSearchInfo.DateFrom, eventSearchInfo.DateTo, eventSearchInfo.ProductType, eventSearchInfo.Destination)
                    {
                        ContactPerson = checkoutProduct.ContactPerson,
                        ProductSeq = 4,
                        TicketInfo = ticketSelectedInfo,
                        PricingDetail = eventProductsPricingDetail,
                    };

                    if (checkoutProduct.AddOnProduct != null)
                    {
                        checkoutProduct.RemoveProduct(ProductTypes.AddOnProducts);
                    }

                    checkoutProduct.InsertProduct(eventBundle);
                }
                else
                {
                    ModelState.AddModelError("Error", "No Ticket selected");
                    return View(checkoutProduct);
                }

                #endregion
            }
            catch (Exception ex)
            {
                logger.Error(ex.GetBaseException(), "Error when EventBundle POST addProduct to checkoutProduct.");
            }
            ViewBag.UsePopupLoginBox = true;
            //need go to guest detail page
            return RedirectToAction("GuestDetails", "Checkout", new { tripid, affiliationId });
        }

        public CheckoutProduct addEventTicketTW(CheckoutProduct checkoutProduct)
        {
            MayFlower db = new MayFlower();

            var eventBundlelist = db.EventBundles.Where(x => x.IsActive == true);
            string eventDetailsID = "";
            List<int> checkEventID = new List<int>();
            List<usp_EventMasterSelect_Result> HeaderList = new List<usp_EventMasterSelect_Result>();

            foreach (var eventBundle in eventBundlelist)
            {
                foreach (var item in eventBundle.EventMaster.EventDetails)
                {

                    //add eventList if no add before
                    if (checkEventID.Any(x => x == item.EventID))
                    {

                    }
                    else
                    {
                        usp_EventMasterSelect_Result addHeader = new usp_EventMasterSelect_Result()
                        {
                            EventID = item.EventID,
                            EventCode = eventBundle.EventMaster.EventCode,
                            EventName = eventBundle.EventMaster.EventName,
                            EventTypeCode = eventBundle.EventMaster.EventTypeCode,
                            EventType = eventBundle.EventMaster.EventType.EventType1,
                            VenueName = eventBundle.EventMaster.VenueName,
                            VenueAddress = eventBundle.EventMaster.VenueAddress,
                            EventStartDate = eventBundle.EventMaster.EventStartDate,
                            EventEndDate = eventBundle.EventMaster.EventEndDate,
                            EventTicketSellDate = eventBundle.EventMaster.EventTicketSellDate,
                            CancellationRule = eventBundle.EventMaster.CancellationRule,
                            TermsAndConditions = eventBundle.EventMaster.TermsAndConditions,
                            EventImageWeb = eventBundle.EventMaster.EventImageWeb,
                            SeatingLayoutImageWeb = eventBundle.EventMaster.SeatingLayoutImageWeb,
                            EventImageBannerPhone = eventBundle.EventMaster.EventImageBannerPhone,
                            SeatingLayoutImagePhone = eventBundle.EventMaster.SeatingLayoutImagePhone,
                            EventImageTablet = eventBundle.EventMaster.EventImageTablet,
                            NearbyPublicTransport = eventBundle.EventMaster.NearbyPublicTransport,
                            EventDetailsID = item.EventDetailsID.ToString(),
                            RedemptionVenue = eventBundle.EventMaster.RedemptionVenue,
                            RedemptionDateFrom = eventBundle.EventMaster.RedemptionStartDateTime.Value,
                            RedemptionDateTo = eventBundle.EventMaster.RedemptionEndDateTime.Value,
                            IsPromoEvent = eventBundle.EventMaster.IsPromoEvent,
                            TotalQtyAcceptable = eventBundle.MaxSellTicketQtyPerBook,
                        };
                        HeaderList.Add(addHeader);
                        checkEventID.Add(item.EventID);
                    }
                    //usp_EventMasterSelect_Result addHeader = new usp_EventMasterSelect_Result()
                    //{
                    //    EventID = item.EventID,
                    //    EventCode = eventBundle.EventMaster.EventCode,
                    //    EventName = eventBundle.EventMaster.EventName,
                    //    EventTypeCode = eventBundle.EventMaster.EventTypeCode,
                    //    EventType = eventBundle.EventMaster.EventType.EventType1,
                    //    VenueName = eventBundle.EventMaster.VenueName,
                    //    VenueAddress = eventBundle.EventMaster.VenueAddress,
                    //    EventStartDate = eventBundle.EventMaster.EventStartDate,
                    //    EventEndDate = eventBundle.EventMaster.EventEndDate,
                    //    EventTicketSellDate = eventBundle.EventMaster.EventTicketSellDate,
                    //    CancellationRule = eventBundle.EventMaster.CancellationRule,
                    //    TermsAndConditions = eventBundle.EventMaster.TermsAndConditions,
                    //    EventImageWeb = eventBundle.EventMaster.EventImageWeb,
                    //    SeatingLayoutImageWeb = eventBundle.EventMaster.SeatingLayoutImageWeb,
                    //    EventImageBannerPhone = eventBundle.EventMaster.EventImageBannerPhone,
                    //    SeatingLayoutImagePhone = eventBundle.EventMaster.SeatingLayoutImagePhone,
                    //    EventImageTablet = eventBundle.EventMaster.EventImageTablet,
                    //    NearbyPublicTransport = eventBundle.EventMaster.NearbyPublicTransport,
                    //    EventDetailsID = item.EventDetailsID.ToString(),
                    //    RedemptionVenue = eventBundle.EventMaster.RedemptionVenue,
                    //    RedemptionDateFrom = eventBundle.EventMaster.RedemptionStartDateTime.Value,
                    //    RedemptionDateTo = eventBundle.EventMaster.RedemptionEndDateTime.Value,
                    //    IsPromoEvent = eventBundle.EventMaster.IsPromoEvent,
                    //    TotalQtyAcceptable = eventBundle.MaxSellTicketQtyPerBook,
                    //};
                    //HeaderList.Add(addHeader);
                    //checkEventID.Add(item.EventID);

                    if (string.IsNullOrEmpty(eventDetailsID))
                    {
                        eventDetailsID = item.EventDetailsID.ToString();
                    }
                    else
                    {
                        eventDetailsID = eventDetailsID + "," + item.EventDetailsID.ToString();
                    }

                }
            }

            if (!string.IsNullOrWhiteSpace(eventDetailsID))
            {
                var ticketsRes = eventDBFunc.GetEventProduct(eventDetailsID, DateTime.Now, DateTime.Now.AddYears(20), db)?.ToList();

                foreach (var item in HeaderList.Where(x => x.EventTypeCode != "TW"))
                {
                    item.BundleTWPrice = eventBundlelist.FirstOrDefault(x => x.EventID == item.EventID).EventBundleDetails.FirstOrDefault(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode == "TW").EventProduct.TicketSellingPrice;
                }

                if (ticketsRes != null && ticketsRes.Count() > 0)
                {
                    var evTypeList = HeaderList.Select(x => x.EventID).Distinct();
                    var noDtlList = evTypeList.Except(ticketsRes.Where(y => y.TicketBalance != 0).Select(x => x.EventID).Distinct());
                    HeaderList.RemoveAll(x => noDtlList.Any(d => d == x.EventID));

                    var eventList = new CrossSellItemsAvailable.EventProductInformation
                    {
                        HeaderInfo = HeaderList,
                        DetailsInfo = ticketsRes
                    };
                    if (eventList.DetailsInfo.Count > 0 && eventList.HeaderInfo.Count > 0)
                    {
                        eventList.SearchInfo = new SearchInfo
                        {
                            DateFrom = System.DateTime.Now,
                            DateTo = System.DateTime.Now.AddYears(20),
                            Origin = "XXX",
                            Destination = "XXX",
                            ProductType = ProductTypes.AddOnProducts,
                        };
                        checkoutProduct.SellItemsAvailable.EventProducts = eventList;
                        checkoutProduct.SellItemsAvailable.ForceCrossSell = true;
                    }
                }
                else
                {
                    checkoutProduct.SellItemsAvailable.EventProducts = null;
                }
            }
            else
            {
                checkoutProduct.SellItemsAvailable.EventProducts = null;
            }
            return checkoutProduct;
        }

        public CheckoutProduct addReserveEBTicket(CheckoutProduct checkoutProduct, string code)
        {
            MayFlower db = new MayFlower();
            var reserveEB = db.ReserveEventSets.FirstOrDefault(x => x.ReserveCode == code);
            var reserveEBDetail = reserveEB.ReserveEventDetails.ToList();
            //for TW eventDetail
            var TWEventDetail = reserveEBDetail.FirstOrDefault(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode == "TW").EventProduct;

            List<int> EventIDList = new List<int>();
            string eventDetailsID = "";
            List<int> eventProductIDList = new List<int>();
            foreach (var item in reserveEBDetail)/*.Where(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode != "TW")*/
            {
                eventProductIDList.Add(item.EventProductID);
            }


            int totalTicketQty = reserveEBDetail.Where(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode != "TW").Sum(y => y.TicketQty); //.Where(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode != "TW")

            List<usp_EventMasterSelect_Result> HeaderList = new List<usp_EventMasterSelect_Result>();

            foreach (var item in reserveEBDetail.Where(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode != "TW")) //.Where(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode != "TW")
            {
                bool EIDalreadyAdd = EventIDList.Any(x => x == item.EventProduct.EventDetail.EventMaster.EventID);
                var eventMaster = item.EventProduct.EventDetail.EventMaster;
                if (!EIDalreadyAdd)
                {
                    EventIDList.Add(item.EventProduct.EventDetail.EventMaster.EventID);
                    usp_EventMasterSelect_Result addHeader = new usp_EventMasterSelect_Result()
                    {
                        EventID = eventMaster.EventID,
                        EventCode = eventMaster.EventCode,
                        EventName = eventMaster.EventName,
                        EventTypeCode = eventMaster.EventTypeCode,
                        EventType = eventMaster.EventType.EventType1,
                        VenueName = eventMaster.VenueName,
                        VenueAddress = eventMaster.VenueAddress,
                        EventStartDate = eventMaster.EventStartDate,
                        EventEndDate = eventMaster.EventEndDate,
                        EventTicketSellDate = eventMaster.EventTicketSellDate,
                        CancellationRule = eventMaster.CancellationRule,
                        TermsAndConditions = eventMaster.TermsAndConditions,
                        EventImageWeb = eventMaster.EventImageWeb,
                        SeatingLayoutImageWeb = eventMaster.SeatingLayoutImageWeb,
                        EventImageBannerPhone = eventMaster.EventImageBannerPhone,
                        SeatingLayoutImagePhone = eventMaster.SeatingLayoutImagePhone,
                        EventImageTablet = eventMaster.EventImageTablet,
                        NearbyPublicTransport = eventMaster.NearbyPublicTransport,
                        EventDetailsID = item.EventProduct.EventDetailsID.ToString(),
                        RedemptionVenue = eventMaster.RedemptionVenue,
                        RedemptionDateFrom = eventMaster.RedemptionStartDateTime.Value,
                        RedemptionDateTo = eventMaster.RedemptionEndDateTime.Value,
                        IsPromoEvent = eventMaster.IsPromoEvent,
                        TotalQtyAcceptable = totalTicketQty,
                    };
                    HeaderList.Add(addHeader);
                    if (string.IsNullOrEmpty(eventDetailsID))
                    {
                        eventDetailsID = item.EventProduct.EventDetailsID.ToString();
                    }
                    else
                    {
                        eventDetailsID = eventDetailsID + "," + item.EventProduct.EventDetailsID.ToString();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(eventDetailsID))
            {
                var ticketsRes = eventDBFunc.GetEventProduct(eventDetailsID, DateTime.Now, DateTime.Now.AddYears(20), db)?.ToList();

                //no need anymore if display TW
                //foreach (var item in HeaderList.Where(x => x.EventTypeCode != "TW"))
                //{
                //    item.BundleTWPrice = reserveEBDetail.Where(x => x.EventProduct.EventDetail.EventMaster.EventTypeCode == "TW").FirstOrDefault().EventProduct.TicketSellingPrice;
                //}

                if (ticketsRes != null && ticketsRes.Count() > 0)
                {
                    ticketsRes.RemoveAll(x => !eventProductIDList.Any(y => y == x.EventProductID)); //need test
                    var evTypeList = HeaderList.Select(x => x.EventID).Distinct();

                    var noDtlList = evTypeList.Except(ticketsRes.Select(x => x.EventID).Distinct());

                    foreach (var ticket in ticketsRes)
                    {
                        ticket.ReservedQty = reserveEBDetail.FirstOrDefault(x => x.EventProductID == ticket.EventProductID).TicketQty;
                    }

                    HeaderList.RemoveAll(x => noDtlList.Any(d => d == x.EventID) && x.EventTypeCode != "TW");

                    foreach (var item in HeaderList.Where(x => x.EventTypeCode != "TW"))
                    {
                        item.BundleTWPrice = TWEventDetail.TicketSellingPrice;
                    }

                    var eventList = new CrossSellItemsAvailable.EventProductInformation
                    {
                        HeaderInfo = HeaderList,
                        DetailsInfo = ticketsRes
                    };
                    if (eventList.DetailsInfo.Count > 0 && eventList.HeaderInfo.Count > 0)
                    {
                        eventList.SearchInfo = new SearchInfo
                        {
                            DateFrom = System.DateTime.Now,
                            DateTo = System.DateTime.Now.AddYears(20),
                            Origin = "XXX",
                            Destination = "XXX",
                            ProductType = ProductTypes.AddOnProducts,
                        };
                        checkoutProduct.SellItemsAvailable.EventProducts = eventList;
                        checkoutProduct.SellItemsAvailable.ForceCrossSell = true;
                    }
                }
                else
                {
                    checkoutProduct.SellItemsAvailable.EventProducts = null;
                }
            }
            else
            {
                checkoutProduct.SellItemsAvailable.EventProducts = null;
            }


            return checkoutProduct;
        }
        #endregion

        [HttpPost]
        [Filters.SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult CheckInsurancePassport(List<TravellerDetail> travellers)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            if (checkout.Flight?.TravellerDetails != null)
            {
                var paxList = checkout.Flight.TravellerDetails;
                List<string> paxUnableQuote = new List<string>();

                if (paxList.Count(x => !x.IsIdentityOk) > 0)
                {
                    dynamic model = new System.Dynamic.ExpandoObject();
                    model.PaxList = paxList;
                    model.tripid = tripid;

                    string obj = PDFEngineServiceCall.ViewRenderer.RenderPartialView("~/Views/Checkout/UpdatePartials/_SetInsurancePassport.cshtml", model);

                    return Content(obj);
                }
            }

            return Json(new
            {
                status = true,
            });
        }

        [HttpPost]
        [Filters.SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult CheckInsuranceUpdate(FormCollection fc)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            List<string> errorList = new List<string>();

            if (checkout.Flight?.TravellerDetails != null)
            {
                var paxList = checkout.Flight.TravellerDetails;

                for (int i = 0; i < paxList.Count; i++)
                {
                    string _identityTypeValue = fc.GetValue(string.Format("{0}_itype", i))?.AttemptedValue;
                    string _identityNoValue = fc.GetValue(string.Format("{0}_identity", i))?.AttemptedValue;
                    string _passportDay = fc.GetValue(string.Format("{0}_passportExpiryDay", i))?.AttemptedValue;
                    string _passportMonth = fc.GetValue(string.Format("{0}_passportExpiryMonth", i))?.AttemptedValue;
                    string _passportYear = fc.GetValue(string.Format("{0}_passportExpiryYear", i))?.AttemptedValue;
                    string _passportCountry = fc.GetValue(string.Format("{0}_passportCountry", i))?.AttemptedValue;
                    DateTime? _passportExpiryDate = null;

                    try
                    {
                        _passportExpiryDate = new DateTime(_passportYear.ToInt(), _passportMonth.ToInt(), _passportDay.ToInt());
                    }
                    catch
                    {
                    }

                    if (_identityTypeValue != null)
                    {
                        switch (_identityTypeValue.ToLower())
                        {
                            case "passport":
                                paxList[i].PassportNumber = string.IsNullOrWhiteSpace(_identityNoValue) ? null : _identityNoValue;
                                paxList[i].PassportExpiryDate = _passportExpiryDate;
                                paxList[i].PassportIssueCountry = string.IsNullOrWhiteSpace(_passportCountry) ? null : _passportCountry;
                                paxList[i].SetPassportDateFromDayMonthYear();

                                if (paxList[i].PassportNumber == null || paxList[i].PassportNumber == "")
                                    errorList.Add("Passport No. for Traveller " + (i + 1).ToString() + " invalid.");
                                if (paxList[i].PassportIssueCountry == null || paxList[i].PassportIssueCountry == "")
                                    errorList.Add("Passport Issue Country for Traveller " + (i + 1).ToString() + " invalid.");
                                if (!paxList[i].PassportExpiryDate.HasValue)
                                    errorList.Add("Passport Date for Traveller " + (i + 1).ToString() + " invalid.");
                                break;
                            case "ic":
                                paxList[i].IdentityNumber = string.IsNullOrWhiteSpace(_identityNoValue) ? null : _identityNoValue;
                                if (paxList[i].IdentityNumber == null)
                                    errorList.Add("Identity No. for Traveller " + (i + 1).ToString() + " invalid.");
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            if (errorList.Count > 0)
            {
                return Json(errorList);
            }
            else
            {
                return JavaScript("$('#crossSale').submit()");
            }
        }

        //For FrequentFlyer
        [HttpGet]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult GetFrequentFlyerPopOut(string filter, string airlinetype)
        {
            var airline = UtilitiesService.GetAllAirlines;

            //check airlinetype if is airasia then only return airasia
            if (airlinetype == "AK")
            {
                airline = airline.Where(x => x.code.Contains(airlinetype));
                return View("~/Views/Checkout/SharedPartials/_FrequentFlyerPopOutList.cshtml", airline);
            }
            else
            {
                if (filter == "")
                {
                    airline = airline.Take(10);
                    return View("~/Views/Checkout/SharedPartials/_FrequentFlyerPopOutList.cshtml", airline);
                }
                else
                {
                    airline = airline.Where(x => x.airline.Contains(filter));
                    return View("~/Views/Checkout/SharedPartials/_FrequentFlyerPopOutList.cshtml", airline);
                }
            }
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult GetConcertInfo(DateTime sd, DateTime ed, int id)
        {

            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            string type = checkout.Flight != null ? "Flight" : (checkout.Hotel != null ? "Hotel" : null);
            var searchInfo = checkout.SellItemsAvailable.EventProducts.SearchInfo;

            MayFlower db = new MayFlower();

            CrossSellItemsAvailable.EventProductInformation eventProductInformation = null;
            bool withPromoEvent = checkout.PromoCodeFunctions.GetFrontendFunction.DisplayPromoEvent;
            eventProductInformation = eventDBFunc.GetEventProductList(sd, ed, type, searchInfo.Origin, searchInfo.Destination, 0, withPromoEvent, null, db);

            //if eventProductInformation is null then get EventProducts from checkout
            if (eventProductInformation == null)
            {
                eventProductInformation = checkout.SellItemsAvailable.EventProducts;
            }

            string productType = eventProductInformation.HeaderInfo.FirstOrDefault(x => x.EventID == id)?.EventTypeCode;

            CrossSellItemsAvailable.EventProductInformation matchEventProduct = new CrossSellItemsAvailable.EventProductInformation()
            {
                HeaderInfo = eventProductInformation.HeaderInfo.Where(x => x.EventID == id).ToList(),
                DetailsInfo = eventProductInformation.DetailsInfo.Where(x => x.EventID == id).ToList()
            };

            #region for map
            string origin = "";
            if (checkout.Hotel == null)
            {
                if (checkout.Flight != null)
                {
                    if (!string.IsNullOrEmpty(checkout.Flight.SearchFlightInfo.ArrivalStation))
                    {
                        origin = checkout.Flight.SearchFlightInfo.ArrivalStation;
                    }

                }
            }
            else if (checkout.Hotel != null)
            {
                if (!string.IsNullOrEmpty(checkout.Hotel.RoomSelected.HotelRoomInformationList.First().hotelName))
                {
                    origin += checkout.Hotel.RoomSelected.HotelRoomInformationList.First().hotelName;
                }
                if (!string.IsNullOrEmpty(checkout.Hotel.RoomSelected.HotelRoomInformationList.First().hotelCity))
                {
                    origin += (origin.Length > 0 ? "," + checkout.Hotel.RoomSelected.HotelRoomInformationList.First().hotelCity : checkout.Hotel.RoomSelected.HotelRoomInformationList.First().hotelCity);
                }

            }


            string destination = "";
            if (!string.IsNullOrEmpty(matchEventProduct.HeaderInfo.First().VenueName))
                destination += matchEventProduct.HeaderInfo.First().VenueName;

            if (!string.IsNullOrEmpty(matchEventProduct.HeaderInfo.First().VenueAddress))
                destination += (destination.Length > 0 ? "," + matchEventProduct.HeaderInfo.First().VenueAddress : matchEventProduct.HeaderInfo.First().VenueAddress);


            if (origin == "")
            {
                ViewData.Add("CONCERTMAP", ExpediaHotelsServiceCall.GetGoogleMapURL(System.Configuration.ConfigurationManager.AppSettings.Get("GoogleMapID"), Server.UrlEncode(destination)));
            }
            else
            {
                ViewData.Add("CONCERTMAP", ExpediaHotelsServiceCall.GetGoogleMapDirectionURL(System.Configuration.ConfigurationManager.AppSettings.Get("GoogleMapID"), Server.UrlEncode(origin), Server.UrlEncode(destination)));
            }
            //ViewData.Add("CONCERTMAP", ExpediaHotelsServiceCall.GetGoogleMapDirectionURL(System.Configuration.ConfigurationManager.AppSettings.Get("GoogleMapID"), Server.UrlEncode(origin), Server.UrlEncode(destination)));
            #endregion

            if (productType == "CT")
            {
                if (IsUseV2Layout || (checkout.Flight == null && checkout.Hotel == null && checkout.TourPackage == null && checkout.SellItemsAvailable != null))
                {
                    return View("~/Views/Checkout/AddOnPartials/_ConcertInformation_v2.cshtml", matchEventProduct);
                }
                else
                {
                    return View("~/Views/Checkout/AddOnPartials/_ConcertInformation.cshtml", matchEventProduct);
                }
            }
            else
            {
                return View("~/Views/Checkout/AddOnPartials/EtcProducts/_PromptInfo.cshtml", matchEventProduct);
            }
        }

        #region Step 4 - Payment
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult Payment(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            MayFlower db = new MayFlower();
            //if (checkout.Flight is FORCE CROSS SELL AND checkout.Hotel is NULL AND !NOT Any PromoCode Skip Cross)
            //{
            //    return RedirectToAction("AddOn", "Checkout", new { tripid });
            //}
            if (checkout.IsDynamic && Core.IsEnablePackageDiscount)
            {
                decimal discAmt = checkout.TotalPrdDisc?.FirstOrDefault(x => x.Discounts.FirstOrDefault()?.DiscType == DiscountType.CODE)?.Disc_Amt ?? 0;
                dynamic obj = new System.Dynamic.ExpandoObject();
                obj.valid = true;
                ApplyInstantDisc(checkout, obj, discAmt);
            }

            //Refresh Promo Code Function incase any direct change from DB
            checkout.PromoID = checkout.PromoID;

            string currencyCode = checkout?.CheckOutSummary?.CurrencyCode ?? "MYR";
            SqlCommand _command = new SqlCommand();
            bool isAgent = IsAgentUser;
            decimal availableCredit = CurrentUserID != 0 ? (isAgent ? MemberServiceController.ChkCreditAmtRemain.Agent(CurrentUserID.ToString(), currencyCode, _command) :
            MemberServiceController.ChkCreditAmtRemain.Member(CurrentUserID.ToString(), currencyCode, _command)) : 0m;
            decimal CashCreditBalace = CurrentUserID != 0 ? MemberServiceController.GetUserCashCredit(CurrentUserID, _command) : 0;
            _command?.Connection?.Close();

            int creditTerm = 1;
            if (isAgent)
            {
                creditTerm = AgentCreditTerm(CurrentUserID, db);
            }

            if (checkout.PaymentDetails == null)
            {
                checkout.PaymentDetails = new PaymentCheckout()
                {
                    AvailableCredit = availableCredit,
                    DisplayBalCreditAmt = availableCredit,

                    PaymentMethod = "IPAFPX",
                    CreditTerm = creditTerm,
                    PaymentCurrencyCode = checkout.CheckOutSummary.CurrencyCode,
                    EWallet = new EWallet
                    {
                        BalanceAmt = CashCreditBalace,
                        DisplayBalAmt = CashCreditBalace,
                    }
                };
            }
            else
            {
                checkout.PaymentDetails.AvailableCredit = availableCredit;
                checkout.PaymentDetails.CreditTerm = creditTerm;
                checkout.PaymentDetails.EWallet.BalanceAmt = CashCreditBalace;

                if (checkout.CheckOutSummary.DiscountDetails.Count > 0)
                {
                    UpdatePayment(checkout.PaymentDetails.PaymentMethod, tripid, checkout.PaymentDetails.UseCredit, checkout.PaymentDetails.EWallet?.UseWallet ?? false);
                }

                // display balance before deduction
                checkout.PaymentDetails.DisplayBalCreditAmt = availableCredit;
                checkout.PaymentDetails.EWallet.DisplayBalAmt = CashCreditBalace;
            }

            checkout.PaymentDetails.CreditCard = new Alphareds.Module.Model.CreditCard();
            if (checkout.SellItemsAvailable != null && (checkout.SellItemsAvailable.EventProducts != null || checkout.SellItemsAvailable.Hotels != null))
            {
                ViewBag.HasCrossSell = true;
            }

            bool isEventBundle = false;
            if (checkout.AddOnProduct != null && checkout.Flight == null && checkout.Hotel == null && checkout.TourPackage == null)
            {
                ViewBag.isEventBundle = true;
                isEventBundle = true;
            }

            //for check the selected product have Exclude any discount payment method
            if (checkout.Hotel != null)
            {
                var htlDiscountExcludeResult = HotelCheckDiscountMethodExclude(checkout.Hotel.HotelSelected.FirstOrDefault().hotelId, checkout.Hotel.SearchHotelInfo.ArrivalDate, checkout.Hotel.SearchHotelInfo.DepartureDate);

                checkout.PaymentDetails.NotAllowUsingTC = htlDiscountExcludeResult.NotAllowTC ?? false;
                checkout.PaymentDetails.NotAllowUsingPromoCode = htlDiscountExcludeResult.NotAllowPromoCode ?? false;
            
                // check hotel information is null or not, if null then try get info and bind
                if (checkout.Hotel.HotelInstrusction == null)
                {
                    GetAndAssignHotelInformation(tripid, checkout);
                }
            }

            if (IsUseV2Layout || (checkout.TourPackage != null || isEventBundle))
            {
                return View("~/Views/Checkout/Payment_v2.cshtml", checkout);
            }
            else
            {
                return View("~/Views/Checkout/Payment.cshtml", checkout);
            }
        }


        public ActionResult MiniScreenPayment(string tripid)
        {
            IsUseV2Layout = false;

            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            MayFlower db = new MayFlower();
            //if (checkout.Flight is FORCE CROSS SELL AND checkout.Hotel is NULL AND !NOT Any PromoCode Skip Cross)
            //{
            //    return RedirectToAction("AddOn", "Checkout", new { tripid });
            //}
            if (checkout.IsDynamic && !checkout.IsFixedPrice && Core.IsEnablePackageDiscount)
            {
                decimal discAmt = checkout.TotalPrdDisc?.FirstOrDefault(x => x.Discounts.FirstOrDefault()?.DiscType == DiscountType.CODE)?.Disc_Amt ?? 0;
                dynamic obj = new System.Dynamic.ExpandoObject();
                obj.valid = true;
                ApplyInstantDisc(checkout, obj, discAmt);
            }

            //Refresh Promo Code Function incase any direct change from DB
            checkout.PromoID = checkout.PromoID;

            bool isAgent = IsAgentUser;
            decimal availableCredit = CurrentUserID != 0 ? (isAgent ? MemberServiceController.ChkCreditAmtRemain.Agent(CurrentUserID.ToString()) :
            MemberServiceController.ChkCreditAmtRemain.Member(CurrentUserID.ToString())) : 0m;
            decimal CashCreditBalace = CurrentUserID != 0 ? MemberServiceController.GetUserCashCredit(CurrentUserID) : 0;
            int creditTerm = 1;
            if (isAgent)
            {
                creditTerm = AgentCreditTerm(CurrentUserID);
            }

            if (checkout.PaymentDetails == null)
            {
                checkout.PaymentDetails = new PaymentCheckout()
                {
                    AvailableCredit = availableCredit,
                    DisplayBalCreditAmt = availableCredit,

                    PaymentMethod = "IPAFPX",
                    CreditTerm = creditTerm,
                    PaymentCurrencyCode = checkout.CheckOutSummary.CurrencyCode,
                    EWallet = new EWallet
                    {
                        BalanceAmt = CashCreditBalace,
                        DisplayBalAmt = CashCreditBalace,
                    }
                };
            }
            else
            {
                checkout.PaymentDetails.AvailableCredit = availableCredit;
                checkout.PaymentDetails.CreditTerm = creditTerm;
                checkout.PaymentDetails.EWallet.BalanceAmt = CashCreditBalace;

                if (checkout.CheckOutSummary.DiscountDetails.Count > 0)
                {
                    UpdatePayment(checkout.PaymentDetails.PaymentMethod, tripid, checkout.PaymentDetails.UseCredit, checkout.PaymentDetails.EWallet?.UseWallet ?? false);
                }

                // display balance before deduction
                checkout.PaymentDetails.DisplayBalCreditAmt = availableCredit;
                checkout.PaymentDetails.EWallet.DisplayBalAmt = CashCreditBalace;
            }

            checkout.PaymentDetails.CreditCard = new Alphareds.Module.Model.CreditCard();
            if (checkout.SellItemsAvailable != null && (checkout.SellItemsAvailable.EventProducts != null || checkout.SellItemsAvailable.Hotels != null))
            {
                ViewBag.HasCrossSell = true;
            }

            bool isEventBundle = false;
            if (checkout.AddOnProduct != null && checkout.Flight == null && checkout.Hotel == null && checkout.TourPackage == null)
            {
                ViewBag.isEventBundle = true;
                isEventBundle = true;
            }

            return View("~/Views/AddOnMiniScreen/Payment.cshtml", checkout);
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public async Task<ActionResult> Payment(PaymentCheckout payment, Alphareds.Module.Model.CreditCard creditCardPost, string tripid, string paymentLater,
            string ccn = null)
        {
            CheckoutProduct checkoutModel = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            //for back to payment page the viewBag is gone 
            if (checkoutModel.AddOnProduct != null && checkoutModel.Flight == null && checkoutModel.Hotel == null && checkoutModel.TourPackage == null)
            {
                ViewBag.isEventBundle = true;
            }

            // postback view path
            string postBackViewPath = IsUseV2Layout || (checkoutModel.TourPackage != null || (checkoutModel.AddOnProduct != null || checkoutModel.Hotel == null || checkoutModel.Flight == null)) ?
                "~/Views/Checkout/Payment_v2.cshtml" : "~/Views/Checkout/Payment.cshtml";

            // Remove unnecessary model error, if not specificed payment method.
            string _pMethod = payment.PaymentMethod?.ToLower();
            if (_pMethod == null || (_pMethod != null && !_pMethod.Contains("adyen")) || Request.Form["adyen-encrypted-data"] != null)
            {
                // Reflection get credit card property to remove model validation
                var ccModel = typeof(Alphareds.Module.Model.CreditCard);

                foreach (var item in ccModel.GetProperties())
                {
                    // Remove model validtion
                    ModelState[item.Name]?.Errors.Clear();
                }
            }

            // If member not verify email, block place booking.
            if (User.Identity.IsAuthenticated && !CustomPrincipal.IsProfileActive && !CustomPrincipal.IsAgent)
            {
                ModelState.AddModelError("Error", "Please verify your account first, before place order.");
                return View(postBackViewPath, checkoutModel);
            }

            bool isAgent = IsAgentUser;
            checkoutModel.PaymentDetails.CreditCard = creditCardPost;
            var creditModel = checkoutModel.PaymentDetails;
            decimal availableCredit = 0m;
            decimal availableCashCredit = 0m;

            if (User.Identity.IsAuthenticated)
            {
                SqlCommand _sqlCommand = new SqlCommand();
                availableCredit = MemberServiceController.ChkCreditAmtRemain.ByUserId(CurrentUserID, CustomPrincipal.UserTypeCode, _sqlCommand);
                availableCashCredit = MemberServiceController.GetUserCashCredit(CurrentUserID, _sqlCommand);
                _sqlCommand.Connection.Close();
            }

            checkoutModel.BookingUserID = CurrentUserID;
            // Check if Payment method is empty, then is full credit payment.

            checkoutModel.PaymentDetails = new PaymentCheckout
            {
                AvailableCredit = CurrentUserID != 0 ? checkoutModel.PaymentDetails.AvailableCredit : 0m,
                CreditUsed = payment.UseCredit ? checkoutModel.PaymentDetails.CreditUsed : 0m,
                EWallet = new EWallet
                {
                    BalanceAmt = CurrentUserID != 0 ? checkoutModel.PaymentDetails.EWallet.BalanceAmt : 0m,
                    UseAmt = payment.EWallet.UseWallet ? checkoutModel.PaymentDetails.EWallet.UseAmt : 0m,
                    UseWallet = payment.EWallet.UseWallet,
                },
                PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMethod) ?
                                (payment.EWallet.UseWallet ? "TW" : (payment.UseCredit ? (isAgent ? "AC" : "SC") : "IPAFPX")) // allocate some default payment method in no tick use credit from frontend
                                : payment.PaymentMethod,
                Policy = payment.Policy,
                TnC = payment.TnC,
                UseCredit = payment.UseCredit,
                CreditCard = creditCardPost,
                CreditTerm = CustomPrincipal.CreditTerm,
                PaymentCurrencyCode = checkoutModel.CheckOutSummary.CurrencyCode,
            };

            payment.PaymentMethod = checkoutModel.PaymentDetails.PaymentMethod;

            // Checking to avoid user make aggression or abuse action. [PROMO CODE]
            #region Disable Promo Code Restriction
            /*
                if (CurrentUserID == 0 && checkoutModel.PromoID != 0)
                {
                    ModelState.AddModelError("Error", "Please register as member before proceed to use the promo code.");
                    return View(postBackViewPath, checkoutModel);
                }
                else if (CurrentUserID != 0 && checkoutModel.PromoID != 0)
                {
                    MayFlower db = new MayFlower();
                    bool checkCodeUsed = db.BookingHotels.Any(x => x.PromoID == checkoutModel.PromoID && x.UserID == CurrentUserID && (x.BookingStatusCode == "RHI" || x.BookingStatusCode == "CON"))
                        || db.Bookings.Any(x => x.PromoID == checkoutModel.PromoID && x.UserID == CurrentUserID && (x.BookingStatusCode == "TKI" || x.BookingStatusCode == "QPL" || x.BookingStatusCode == "RHI" || x.BookingStatusCode == "CON"));

                    if (checkCodeUsed)
                    {
                        ModelState.AddModelError("Error", "You had redeem this Promo Code.");
                        return View(postBackViewPath, checkoutModel);
                    }
                }
             */
            #endregion

            // 2018/01/12 - KC Request: Not allow double discount, check also promo code
            var _insertedDiscRC = checkoutModel.CheckOutSummary.DiscountDetails.FirstOrDefault(x => x.DiscType == DiscountType.CODE);
            if (checkoutModel.PromoID != 0 || _insertedDiscRC != null)
            {
                MayFlower _db = new MayFlower();
                var pRule = _db.PromoCodeRules.FirstOrDefault(x => x.PromoID == checkoutModel.PromoID);
                var pFunc = checkoutModel.PromoCodeFunctions.GetFrontendFunction;

                if (_insertedDiscRC != null && checkoutModel.PromoID == 0)
                {
                    foreach (var item in checkoutModel.Products)
                    {
                        item.PricingDetail.Discounts.RemoveAll(x => x.DiscType == DiscountType.CODE);
                    }

                    ModelState.AddModelError("Error", "Unexpected error please try again.");
                    return View(postBackViewPath, checkoutModel);
                }
                else if (pRule != null && pRule.DiscountAmtOrPCT > 0 && !pFunc.AllowWithTC &&
                    checkoutModel.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.TC))
                {
                    ModelState.AddModelError("Error", "Travel Credit not available for discounted booking.");
                    return View(postBackViewPath, checkoutModel);
                }
                else if (pRule != null && pRule.PromoBankBinRanges.Count > 0)
                {
                    // Check is payment with Credit Card Or Not
                    if (payment.PaymentMethod?.ToLower() != "adyenc")
                    {
                        ModelState.AddModelError("Error", "This Promo Code must use with specified card payment method.");
                        return View(postBackViewPath, checkoutModel);
                    }

                    string errMsg = null;
                    bool isValidBIN = IsValidCardForPromo(pRule, ccn, ref errMsg);

                    // Check Used Promo BIN valid or not.
                    if (!isValidBIN)
                    {
                        ModelState.AddModelError("Error", errMsg ?? "Unexpected error please try again later.");
                        return View(postBackViewPath, checkoutModel);
                    }
                }
            }

            //checking the hotel is allow to use TC or not
            if (checkoutModel.Hotel != null)
            {
                DiscountPayMethodExcludeList htlDiscountExcludeResult = HotelCheckDiscountMethodExclude(checkoutModel.Hotel.HotelSelected.FirstOrDefault().hotelId, checkoutModel.Hotel.SearchHotelInfo.ArrivalDate, checkoutModel.Hotel.SearchHotelInfo.DepartureDate);
                if (checkoutModel.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.TC) && htlDiscountExcludeResult != null && htlDiscountExcludeResult.NotAllowTC == true)
                {
                    ModelState.AddModelError("Error", "Travel Credit not available for this discounted hotel booking.");
                    return View(postBackViewPath, checkoutModel);
                }
            }

            /* 2018/03/20 - Restrict lsuki booking abuse use Travel Credit
             * Not allow to change email other than registered email.
            */
            if (User.Identity.IsAuthenticated && checkoutModel.ContactPerson.Email != CustomPrincipal.Email)
            {
                ModelState.AddModelError("Error", "Contact email different from registered account email.");
                return View(postBackViewPath, checkoutModel);
            }

            // Checking to avoid user make aggression or abuse action. [TRAVEL CREDIT]
            if (checkoutModel.PaymentDetails.UseCredit && (creditModel == null || availableCredit < creditModel.CreditUsed))
            {
                ModelState.AddModelError("Error", "Insufficient of travel credit.");
                return View(postBackViewPath, checkoutModel);
            }
            else if (checkoutModel.PaymentDetails.EWallet.UseWallet && (creditModel == null || availableCashCredit < creditModel.EWallet.UseAmt))
            {
                ModelState.AddModelError("Error", "Insufficient of travel wallet.");
                return View(postBackViewPath, checkoutModel);
            }
            else if ((!checkoutModel.PaymentDetails.UseCredit && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "sc" || checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "ac")) ||
                (CurrentUserID == 0 && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "sc" || checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "ac")) ||
                (!checkoutModel.PaymentDetails.EWallet.UseWallet && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "tw")) || (CurrentUserID == 0 && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "tw")))
            {
                ModelState.AddModelError("Error", "Opps, somethings wrong, please try again later.");
                return View(postBackViewPath, checkoutModel);
            }
            else if ((checkoutModel.PaymentDetails.UseCredit || checkoutModel.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.TC))
                && !CustomPrincipal.IsAgent)
            {
                // recalculate on postback action.
                decimal totalAmtAllowToCalcUseableTC = (checkoutModel.CheckOutSummary.SubTtl + checkoutModel.CheckOutSummary.DiscountDetails.GetTtlDiscAmtWithoutCredit())
                    - (IsAgentUser || Core.IsForStaging ? 0m : checkoutModel.CheckOutSummary.TtlAddOnAmount);

                var _tcDiscDtl = checkoutModel.CheckOutSummary.DiscountDetails.FirstOrDefault(x => x.DiscType == DiscountType.TC) ?? new DiscountDetail();

                decimal reCalc = CalcTravelCreditUsable(totalAmtAllowToCalcUseableTC, availableCredit, availableCashCredit, DiscountType.TC, checkoutModel.MainProductType.ToString());
                if (checkoutModel.PaymentDetails.CreditUsed > reCalc || _tcDiscDtl.Disc_Amt > reCalc)
                {
                    decimal _amtUsed = checkoutModel.PaymentDetails.CreditUsed > reCalc ? checkoutModel.PaymentDetails.CreditUsed : _tcDiscDtl.Disc_Amt;
                    ModelState.AddModelError("Error", "Opps, looks like the travel credit used over specified limit. - " +
                        $"Exact amount used: {_amtUsed.ToString("n2")}{Environment.NewLine}" +
                        $"Amount can be use in this transaction: {reCalc}");
                    return View(postBackViewPath, checkoutModel);
                }
            }
            else if (!IsEventInventoryEnough(checkoutModel.AddOnProduct, null))
            {
                ModelState.AddModelError("Error", "Sorry, some of the Add On Products has been sold out.");
                return View(postBackViewPath, checkoutModel);
            }
            ///////here add check code //checkoutModel.EventBundleReserveCode == null ? false : 
            else if (isEBReserveCodeUsed(checkoutModel.EventBundleReserveCode))
            {
                ModelState.AddModelError("Error", "Sorry, the reserve code is already used.");
                return View(postBackViewPath, checkoutModel);
            }

            // Reset SuperPNRID stored in CheckoutProduct object.
            if (checkoutModel.SuperPNRID > 0 || !string.IsNullOrEmpty(checkoutModel.SuperPNRNo) || checkoutModel.OrderID > 0)
            {
                checkoutModel.SuperPNRID = 0;
                checkoutModel.SuperPNRNo = string.Empty;
                checkoutModel.OrderID = 0;
            }

            foreach (var _product in checkoutModel.Products)
            {
                // Flight - Pre book the seat before payment
                if (_product.ProductType == ProductTypes.Flight)
                {
                    #region get Price Yield
                    PriceYield priceYield = UtilitiesService.GetPriceYield(checkoutModel.Flight.FlightInfo, checkoutModel.Flight.SearchFlightInfo.CabinClass, isAgent);
                    checkoutModel.Flight.PricingTicketing = priceYield.PricingTicketing;
                    checkoutModel.Flight.QueueNumber = priceYield.QueueNo;
                    checkoutModel.Flight.TicketingQueueNumber = priceYield.TicketingQueueNo;
                    #endregion

                    var fltBookSeatRes = BookingServiceController.HoldFlightSeat(checkoutModel.Flight, General.Utilities.GetClientIP, checkoutModel.Flight.PricingDetail.Currency);

                    if (fltBookSeatRes.Header != null && fltBookSeatRes.Header.Error != null && !string.IsNullOrEmpty(fltBookSeatRes.Header.Error.ErrorMessage))
                    {
                        //Item1 = System error code, Item2 = Sabre error code
                        //Tuple<string, string> errorCodes = UtilitiesService.NlogExceptionForBookingFlow(logger, model, null, userid, "sabreerror", updatedInfo.Item2, updatedInfo.Item3);

                        logger.Fatal(fltBookSeatRes.Header.Error.ErrorMessage + Newtonsoft.Json.JsonConvert.SerializeObject(checkoutModel.Flight, new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore }));

                        //No Seat Error
                        bool NoSeatError = fltBookSeatRes.Header.Error.ErrorMessage.Any();

                        if (NoSeatError)
                        {
                            ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                        }

                        //if (checkoutModel.Flight.FlightInfo.Supplier == Alphareds.Module.CompareToolWebService.CTWS.serviceSource.TCG)
                        //{
                        //    ModelState.AddModelError("Error", "Oops...the fare you chosen is no longer available.. Please try to search again.");
                        //    //return RedirectToAction("Search", "Flight", new { tripid, affiliationId, research = true });
                        //}
                        //else
                        //{   
                        //    //No Seat Error
                        //    bool NoSeatError = fltBookSeatRes.Header.Error.ErrorMessage.Any();

                        //    if (NoSeatError)
                        //    {
                        //        ModelState.AddModelError("Error", "Oops...your selected flight choices is not available. Please try to book again.");
                        //    }
                        //}

                        return View(postBackViewPath, checkoutModel);
                    }

                    bool isDiscount = priceYield.DiscountOrMarkup <= 0;
                    string checkCost = BookingServiceController.CompareCostFareAndSellingFare(fltBookSeatRes.Output, checkoutModel.Flight, isDiscount);
                    if (!string.IsNullOrEmpty(checkCost))
                    {
                        logger.Fatal(checkCost + Newtonsoft.Json.JsonConvert.SerializeObject(checkoutModel.Flight));
                        ModelState.AddModelError("Error", "Oops...your selected flight choices is not available. Please try to book again.");

                        return View(postBackViewPath, checkoutModel);
                    }
                    checkoutModel.Flight.SeatInformation = fltBookSeatRes.Output;
                    checkoutModel.Flight.DiscountOrMarkupAmount = priceYield.DiscountOrMarkup;
                    checkoutModel.Flight.MarkUpPricingType = priceYield.MarkupPricingTypeCode;

                    if (checkoutModel.Flight.SearchFlightInfo.isReturn &&
                        ((checkoutModel.Flight.FlightInfo.FlightDetailInfo.FlightTrip.SelectMany(x => x.FlightRouteInfo).Count() != checkoutModel.Flight.SeatInformation.OriginDestinationOptions.SelectMany(x => x.FlightSegments).Count())))
                    {
                        logger.Fatal($"Customer selected Round Trip but hold seat return single segment: {Environment.NewLine + Environment.NewLine} {JsonConvert.SerializeObject(checkoutModel.Flight)}");
                        ModelState.AddModelError("Error", "Oops...your selected flight choices is not available.");

                        return View(postBackViewPath, checkoutModel);
                    }
                }
                // Hotel - Pre book check hotel rate and availaible
                else if (_product.ProductType == ProductTypes.Hotel && checkoutModel.Hotel.HotelSelected.FirstOrDefault().hotelSupplier == HotelSupplier.EANRapid)
                {
                    var chkPrice = await EANRapidHotelServiceCall.CheckRoomPriceAvailaible((ProductHotel)_product, GetUserIP(), tripid, Request.UserAgent);

                    if (chkPrice.IsPriceDifferent)
                    {
                        //TODO: Add error message stop continue flow
                        ModelState.AddModelError("Error", chkPrice.Messages);
                        return View(postBackViewPath, checkoutModel);
                    }
                }
            }

            //todo: check why BookingUserID 0
            //if eventBundle booking and BookingUserID is 0 then not allow continue booking
            if (checkoutModel.Flight == null && checkoutModel.Hotel == null && checkoutModel.AddOnProduct != null)
            {
                if (checkoutModel.BookingUserID == 0)
                {
                    ModelState.AddModelError("Error", "Need login for the EventBundle Booking, Please Try again.");
                    return View(postBackViewPath, checkoutModel);
                }
                
            }
            // Start insert product list here.
            var insertRes = await checkoutModel.Products.InsertDBRecord(checkoutModel);
            checkoutModel = insertRes.Item1;

            if (insertRes.Item2.Any(x => x.Value == false))
            {
                //add error return from SP InsertEventBooking into ModelState
                if (insertRes.Item2.Any(x => x.Key.StartsWith(typeof(ProductEventTicket).ToString()) && x.Value == false))
                {
                    ModelState.AddModelError("Error", insertRes.Item2.FirstOrDefault(x => x.Key.StartsWith(typeof(ProductEventTicket).ToString())).Key.Split(':', '\r')[2]);
                }
                else
                {
                    ModelState.AddModelError("Error", "An error occured, please try again later.");
                }
                List<string> errorKey = new List<string>();

                foreach (var item in insertRes.Item2)
                {
                    errorKey.Add(item.Key + " - " + item.Value.ToString());
                }

                logger.Fatal("Error occured when insert record to databse:" +
                    Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, errorKey));

                return View(postBackViewPath, checkoutModel);
            }

            MayFlower db = new MayFlower();
            if (checkoutModel.TourPackage != null)
            {
                var tourbook = checkoutModel.TourPackage.TourPackagesInfo.TourPackageBooking;
                var tourbookingDB = db.TourPackageBookings.FirstOrDefault(x => x.BookingID == tourbook.BookingID);

                tourbook = tourbookingDB;
                checkoutModel.SuperPNRID = tourbookingDB.SuperPNRID;
                checkoutModel.SuperPNRNo = tourbookingDB.SuperPNRNo;
                checkoutModel.OrderID = tourbookingDB.OrderID;
            }
            var superPNR = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRID == checkoutModel.SuperPNRID);

            if (checkoutModel.PaymentDetails != null)
            {
                List<int> paymentOrderInserted = new List<int>();
                SqlCommand command = new SqlCommand();

                try
                {
                    string currencyCode = checkoutModel.CheckOutSummary.CurrencyCode;

                    #region Insert/Update Processing Fee
                    var feeChargeOrder = db.FeeChargeOrders.FirstOrDefault(x => x.SuperPNROrder.SuperPNRID == checkoutModel.SuperPNRID);
                    var pf = checkoutModel.CheckOutSummary.ProcessingFee;

                    var latestFeeCharge = new FeeChargeOrder
                    {
                        OrderID = checkoutModel.OrderID,
                        CreatedByID = checkoutModel.BookingUserID,
                        CurrencyCode = checkoutModel.CheckOutSummary.CurrencyCode,
                        FeeChargeAmount = pf.Amt,
                        FeeCode = string.IsNullOrWhiteSpace(pf.FeeCode) ? checkoutModel.PaymentDetails.PaymentMethod : pf.FeeCode,
                        TaxCode = string.IsNullOrWhiteSpace(pf.TaxCode) ? "ES" : pf.TaxCode,
                        TaxAmount = pf.GST,
                        ModifiedByID = CurrentUserID,
                    };

                    if (feeChargeOrder == null)
                    {
                        PaymentServiceController.InsertFeeChargeOrder(latestFeeCharge, command);
                    }
                    else
                    {
                        PaymentServiceController.UpdateFeeChargeOrder(latestFeeCharge, command);
                    }
                    #endregion


                    #region Insert Payment

                    #region Insert Payment Order (Payment Details)
                    // Travel Credit 
                    List<CreditTypes> creditTypeList = new List<CreditTypes>();
                    PaymentServiceController paymentServiceController = null;

                    // Insert TC/TW
                    foreach (var item in checkoutModel.CheckOutSummary.DiscountDetails)
                    {
                        bool isVisualPaymentRC = false;
                        paymentServiceController = paymentServiceController ?? new PaymentServiceController();

                        if (item.DiscType == DiscountType.TC && item.Disc_Amt > 0)
                        {
                            #region Insert Travel Credit ()
                            //Insert temp credit records
                            if (!isAgent)
                            {
                                string mainProductType = checkoutModel.Flight != null ? "FLT" :
                                (checkoutModel.Flight == null && checkoutModel.Hotel != null ? "HTL" : "OTH");

                                Temp_UserCreditRedeem tempUserCredit = paymentServiceController.TempUserCreditRedeemPopulate(checkoutModel.OrderID, CurrentUserID, creditModel.CreditUsed, mainProductType, currencyCode);
                                paymentServiceController.TempCreditRedeemInsert(tempUserCredit, command);
                            }
                            #endregion

                            isVisualPaymentRC = true;
                        }
                        else if (item.DiscType == DiscountType.TW && item.Disc_Amt > 0)
                        {
                            #region Insert Travel Wallet ()
                            //Insert temp credit records
                            Temp_UserCashCreditRedeem tempUserCashCredit = paymentServiceController.TempUserCashCreditRedeemPopulate(CurrentUserID, checkoutModel.OrderID, checkoutModel.SuperPNRID, checkoutModel.SuperPNRNo, creditModel.EWallet.UseAmt, currencyCode);
                            paymentServiceController.TempCashCreditRedeemInsert(tempUserCashCredit, command);
                            #endregion

                            isVisualPaymentRC = true;
                        }

                        if (isVisualPaymentRC)
                        {
                            creditTypeList.Add(new CreditTypes
                            {
                                CreditType = item.DiscType,
                                CreditUsed = true,
                            });

                            PaymentOrder _paymentOrderInsert = new PaymentOrder
                            {
                                OrderID = checkoutModel.OrderID,
                                PaymentDate = DateTime.Now,
                                PaymentMethodCode = item.DiscType == DiscountType.TC || item.DiscType == DiscountType.AC ? (isAgent ? "AC" : "SC") : "TW",
                                PaymentStatusCode = "PEND",
                                CurrencyCode = currencyCode,
                                PaymentAmount = item.Disc_Amt,
                                ImagePath = string.Empty,
                                Ipay88RefNo = string.Empty,
                                Ipay88TransactionID = string.Empty,
                                CreatedByID = CurrentUserID,
                                ModifiedByID = CurrentUserID
                            };

                            paymentOrderInserted.Add(PaymentServiceController.InsertSuperPNRPaymentOrder(_paymentOrderInsert, command));
                        }
                    }

                    // Normal Payment
                    var contactPerson = checkoutModel.ContactPerson;
                    var creditCard = creditModel.CreditCard;
                    decimal paymentAmt = checkoutModel.TourPackage != null && checkoutModel.PaymentDetails != null ? checkoutModel.PaymentDetails.DepositAmt : checkoutModel.CheckOutSummary.GrandTtlAmt;
                    PaymentSubmitModels iPayModel = PaymentController.PopulatePaymentSubmitModel(DateTime.Now, checkoutModel.SuperPNRID, checkoutModel.SuperPNRNo, currencyCode, paymentAmt, checkoutModel.PaymentDetails.PaymentMethod, contactPerson.Phone1, contactPerson.Email, contactPerson.FullName);
                    string iPay88RefNo = checkoutModel.SuperPNRID + " - " + checkoutModel.SuperPNRNo; // Important, cannot simply change, will cause cannot requery fail.

                    // Insert for Not Full Store Credit/Cash Wallet
                    if (checkoutModel.CheckOutSummary.GrandTtlAmt > 0)
                    {
                        PaymentOrder paymentOrder = PaymentServiceController.PopulatePaymentPaymentOrder(checkoutModel.OrderID, currencyCode, iPay88RefNo, 0,
                        paymentAmt, "PEND", checkoutModel.PaymentDetails.PaymentMethod, CurrentUserID);
                        paymentOrderInserted.Add(PaymentServiceController.InsertSuperPNRPaymentOrder(paymentOrder, command));
                    }
                    #endregion

                    #region Insert Flight Booking Progress
                    foreach (var booking in superPNR.Bookings)
                    {
                        Alphareds.Module.BookingController.BookingServiceController.InsertBookingProgress(Enumeration.SMCBookingActivityType.PDP, booking.BookingID, "iPay88", CurrentUserID, command);
                    }
                    #endregion

                    // DB Transaction Commit and Close Here.
                    command.Transaction.Commit();

                    #endregion
                    if (checkoutModel.IsDynamic || checkoutModel.IsFixedPrice)
                    {
                        foreach(var pnrorder in superPNR.SuperPNROrders)
                        {
                            pnrorder.IsFixed = checkoutModel.IsFixedPrice;
                            pnrorder.InstantDiscountAmt = Math.Abs(checkoutModel.TotalPrdDisc?.FirstOrDefault().Discounts?.FirstOrDefault(x => x.DiscType == DiscountType.PD)?.Disc_Amt ?? 0);
                        }
                        await db.SaveChangesAsync();
                    }
                    #region Dicision Redirect for Hold To Payment - HTP
                    if (User.Identity.IsAuthenticated && CustomPrincipal.IsAgent &&
                        !string.IsNullOrWhiteSpace(paymentLater) && paymentLater == "1" &&
                        (checkoutModel.Flight != null && checkoutModel.Hotel == null))
                    {
                        db = db.DisposeAndRefresh();
                        var _superPNRInserted = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRID == checkoutModel.SuperPNRID);
                        UpdateBookingFlights(ref _superPNRInserted, "HTP");
                        UpdateSuperPNROrders(ref _superPNRInserted, "HTP", "PEND", paymentOrderInserted);
                        db.SaveChanges();

                        Session.Remove(Enumeration.SessionName.CheckoutProduct.ToString() + tripid);
                        return RedirectToAction("OrderHistory", "Flight", new { tripid, bookingID = checkoutModel.SuperPNRNo, status = "payment-later" });
                    }
                    #endregion

                    #region Payment Gateway Redirect

                    PaymentController pc = new PaymentController();
                    string clientIP = HttpContext.Request.UserHostAddress;
                    string paymentMethod = checkoutModel.PaymentDetails.PaymentMethod.ToUpper();
                    string token = checkoutModel.SuperPNRID.ToString() + "," + checkoutModel.SuperPNRNo;
                    string encToken = General.CustomizeBaseEncoding.CodeBase64(token);
                    string encPaymentOrderIDList = Cryptography.AES.Encrypt(paymentOrderInserted.JoinToString(","));

                    FormCollection form = new FormCollection();
                    adyenCaptureResponseModels captureResponseModels2 = new adyenCaptureResponseModels();
                    iPayCaptureResponseModels captureResponseModels = new iPayCaptureResponseModels
                    { Status = "1", Amount = iPayModel.PaymentAmount, TransId = "" };

                    switch (checkoutModel.PaymentDetails.PaymentMethod.ToLower())
                    {
                        case "sc":
                            form.Add("Status", captureResponseModels.Status);
                            form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                            return await PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList);
                        case "ac":
                            form.Add("Status", captureResponseModels.Status);
                            form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                            return await PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList);
                        case "tw":
                            form.Add("Status", captureResponseModels.Status);
                            form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                            return await PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList);
                        case "ipacc":
                            return pc.iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, isRegister = checkoutModel.IsRegister, eventRCode = checkoutModel.EventBundleReserveCode ?? null }, Request.Url.Scheme), iPayModel, true);
                        case "ipafpx":
                            return pc.iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, isRegister = checkoutModel.IsRegister, eventRCode = checkoutModel.EventBundleReserveCode ?? null }, Request.Url.Scheme), iPayModel);
                        case "adyenc":
                            AdyenCardPaymentModels adyenModel = PaymentController.PopulateAdyenPaymentSubmitModel(checkoutModel.SuperPNRID, Request.Url.Scheme, checkoutModel.SuperPNRNo, currencyCode, paymentAmt, contactPerson.Email, creditCard);
                            return pc.AdyenCheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, isRegister = checkoutModel.IsRegister, eventRCode = checkoutModel.EventBundleReserveCode ?? null }, Request.Url.Scheme), adyenModel, Request.Form);
                        case "boost":
                            BoostPaymentModels boostModel = PaymentController.PopulateBoostPaymentSubmitModel(checkoutModel.SuperPNRID, checkoutModel.SuperPNRNo, Request.Url.Scheme, contactPerson.Phone1LocationCode + contactPerson.Phone1,paymentAmt);
                            return pc.BoostCheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, isRegister = checkoutModel.IsRegister, eventRCode = checkoutModel.EventBundleReserveCode ?? null }, Request.Url.Scheme), boostModel);
                        default:
                            ModelState.AddModelError("Error", "Payment Method Not Found.");
                            break;
                    }

                    #endregion

                }
                catch (Exception ex)
                {
                    command?.Transaction?.Rollback();

                    logger.Fatal(ex, $"Payment POST Error [{checkoutModel.SuperPNRID}: {checkoutModel.SuperPNRNo}] - {DateTime.Now.ToLoggerDateTime()}");
                    ModelState.AddModelError("Error", "Unexpected error occured, please try again later. If this keep happen please contact support.");
                }
            }

            return View(postBackViewPath, checkoutModel);
        }

        [HttpPost]
        public async Task<ActionResult> PaymentMini(PaymentCheckout payment, Alphareds.Module.Model.CreditCard creditCardPost, string tripid, string paymentLater,
          string ccn = null)
        {
            CheckoutProduct checkoutModel = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            BookedProductView viewModel = (BookedProductView)Core.GetSession(Enumeration.SessionName.BookedProductView);
            //for back to payment page the viewBag is gone 
            if (checkoutModel.AddOnProduct != null && checkoutModel.Flight == null && checkoutModel.Hotel == null && checkoutModel.TourPackage == null)
            {
                ViewBag.isEventBundle = true;
            }

            // postback view path
            IsUseV2Layout = false;
            string postBackViewPath = IsUseV2Layout || (checkoutModel.TourPackage != null || (checkoutModel.AddOnProduct != null || checkoutModel.Hotel == null || checkoutModel.Flight == null)) ?
                "~/Views/AddOnMiniScreen/Payment.cshtml" : "~/Views/AddOnMiniScreen/Payment.cshtml";

            // Remove unnecessary model error, if not specificed payment method.
            string _pMethod = payment.PaymentMethod?.ToLower();
            if (_pMethod == null || (_pMethod != null && !_pMethod.Contains("adyen")) || Request.Form["adyen-encrypted-data"] != null)
            {
                // Reflection get credit card property to remove model validation
                var ccModel = typeof(Alphareds.Module.Model.CreditCard);

                foreach (var item in ccModel.GetProperties())
                {
                    // Remove model validtion
                    ModelState[item.Name]?.Errors.Clear();
                }
            }

            //// If member not verify email, block place booking.
            //if (User.Identity.IsAuthenticated && !CustomPrincipal.IsProfileActive && !CustomPrincipal.IsAgent)
            //{
            //    ModelState.AddModelError("Error", "Please verify your account first, before place order.");
            //    return View(postBackViewPath, checkoutModel);
            //}

            bool isAgent = IsAgentUser;
            checkoutModel.PaymentDetails.CreditCard = creditCardPost;
            var creditModel = checkoutModel.PaymentDetails;
            decimal availableCredit = 0m;
            decimal availableCashCredit = 0m;

            if (User.Identity.IsAuthenticated)
            {
                SqlCommand _sqlCommand = new SqlCommand();
                availableCredit = MemberServiceController.ChkCreditAmtRemain.ByUserId(CurrentUserID, CustomPrincipal.UserTypeCode, _sqlCommand);
                availableCashCredit = MemberServiceController.GetUserCashCredit(CurrentUserID, _sqlCommand);
                _sqlCommand.Connection.Close();
            }

            checkoutModel.BookingUserID = CurrentUserID;
            // Check if Payment method is empty, then is full credit payment.

            checkoutModel.PaymentDetails = new PaymentCheckout
            {
                AvailableCredit = CurrentUserID != 0 ? checkoutModel.PaymentDetails.AvailableCredit : 0m,
                CreditUsed = payment.UseCredit ? checkoutModel.PaymentDetails.CreditUsed : 0m,
                EWallet = new EWallet
                {
                    BalanceAmt = CurrentUserID != 0 ? checkoutModel.PaymentDetails.EWallet.BalanceAmt : 0m,
                    UseAmt = payment.EWallet.UseWallet ? checkoutModel.PaymentDetails.EWallet.UseAmt : 0m,
                    UseWallet = payment.EWallet.UseWallet,
                },
                PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMethod) ?
                                (payment.EWallet.UseWallet ? "TW" : (payment.UseCredit ? (isAgent ? "AC" : "SC") : "IPAFPX")) // allocate some default payment method in no tick use credit from frontend
                                : payment.PaymentMethod,
                Policy = payment.Policy,
                TnC = payment.TnC,
                UseCredit = payment.UseCredit,
                CreditCard = creditCardPost,
                CreditTerm = CustomPrincipal.CreditTerm,
                PaymentCurrencyCode = checkoutModel.CheckOutSummary.CurrencyCode,
            };

            payment.PaymentMethod = checkoutModel.PaymentDetails.PaymentMethod;

            // Checking to avoid user make aggression or abuse action. [PROMO CODE]
            #region Disable Promo Code Restriction
            /*
                if (CurrentUserID == 0 && checkoutModel.PromoID != 0)
                {
                    ModelState.AddModelError("Error", "Please register as member before proceed to use the promo code.");
                    return View(postBackViewPath, checkoutModel);
                }
                else if (CurrentUserID != 0 && checkoutModel.PromoID != 0)
                {
                    MayFlower db = new MayFlower();
                    bool checkCodeUsed = db.BookingHotels.Any(x => x.PromoID == checkoutModel.PromoID && x.UserID == CurrentUserID && (x.BookingStatusCode == "RHI" || x.BookingStatusCode == "CON"))
                        || db.Bookings.Any(x => x.PromoID == checkoutModel.PromoID && x.UserID == CurrentUserID && (x.BookingStatusCode == "TKI" || x.BookingStatusCode == "QPL" || x.BookingStatusCode == "RHI" || x.BookingStatusCode == "CON"));

                    if (checkCodeUsed)
                    {
                        ModelState.AddModelError("Error", "You had redeem this Promo Code.");
                        return View(postBackViewPath, checkoutModel);
                    }
                }
             */
            #endregion

            // 2018/01/12 - KC Request: Not allow double discount, check also promo code
            var _insertedDiscRC = checkoutModel.CheckOutSummary.DiscountDetails.FirstOrDefault(x => x.DiscType == DiscountType.CODE);
            if (checkoutModel.PromoID != 0 || _insertedDiscRC != null)
            {
                MayFlower _db = new MayFlower();
                var pRule = _db.PromoCodeRules.FirstOrDefault(x => x.PromoID == checkoutModel.PromoID);
                var pFunc = checkoutModel.PromoCodeFunctions.GetFrontendFunction;

                if (_insertedDiscRC != null && checkoutModel.PromoID == 0)
                {
                    foreach (var item in checkoutModel.Products)
                    {
                        item.PricingDetail.Discounts.RemoveAll(x => x.DiscType == DiscountType.CODE);
                    }

                    ModelState.AddModelError("Error", "Unexpected error please try again.");
                    return View(postBackViewPath, checkoutModel);
                }
                else if (pRule != null && pRule.DiscountAmtOrPCT > 0 && !pFunc.AllowWithTC &&
                    checkoutModel.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.TC))
                {
                    ModelState.AddModelError("Error", "Travel Credit not available for discounted booking.");
                    return View(postBackViewPath, checkoutModel);
                }
                else if (pRule != null && pRule.PromoBankBinRanges.Count > 0)
                {
                    // Check is payment with Credit Card Or Not
                    if (payment.PaymentMethod?.ToLower() != "adyenc")
                    {
                        ModelState.AddModelError("Error", "This Promo Code must use with specified card payment method.");
                        return View(postBackViewPath, checkoutModel);
                    }

                    string errMsg = null;
                    bool isValidBIN = IsValidCardForPromo(pRule, ccn, ref errMsg);

                    // Check Used Promo BIN valid or not.
                    if (!isValidBIN)
                    {
                        ModelState.AddModelError("Error", errMsg ?? "Unexpected error please try again later.");
                        return View(postBackViewPath, checkoutModel);
                    }
                }
            }

            /* 2018/03/20 - Restrict lsuki booking abuse use Travel Credit
             * Not allow to change email other than registered email.
            */
            //if (User.Identity.IsAuthenticated && checkoutModel.ContactPerson.Email != CustomPrincipal.Email)
            //{
            //    ModelState.AddModelError("Error", "Contact email different from registered account email.");
            //    return View(postBackViewPath, checkoutModel);
            //}

            // Checking to avoid user make aggression or abuse action. [TRAVEL CREDIT]
            if (checkoutModel.PaymentDetails.UseCredit && (creditModel == null || availableCredit < creditModel.CreditUsed))
            {
                ModelState.AddModelError("Error", "Insufficient of travel credit.");
                return View(postBackViewPath, checkoutModel);
            }
            else if (checkoutModel.PaymentDetails.EWallet.UseWallet && (creditModel == null || availableCashCredit < creditModel.EWallet.UseAmt))
            {
                ModelState.AddModelError("Error", "Insufficient of travel wallet.");
                return View(postBackViewPath, checkoutModel);
            }
            else if ((!checkoutModel.PaymentDetails.UseCredit && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "sc" || checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "ac")) ||
                (CurrentUserID == 0 && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "sc" || checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "ac")) ||
                (!checkoutModel.PaymentDetails.EWallet.UseWallet && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "tw")) || (CurrentUserID == 0 && (checkoutModel.PaymentDetails.PaymentMethod.ToLower() == "tw")))
            {
                ModelState.AddModelError("Error", "Opps, somethings wrong, please try again later.");
                return View(postBackViewPath, checkoutModel);
            }
            else if ((checkoutModel.PaymentDetails.UseCredit || checkoutModel.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.TC))
                && !CustomPrincipal.IsAgent)
            {
                // recalculate on postback action.
                decimal totalAmtAllowToCalcUseableTC = (checkoutModel.CheckOutSummary.SubTtl + checkoutModel.CheckOutSummary.DiscountDetails.GetTtlDiscAmtWithoutCredit())
                    - (IsAgentUser || Core.IsForStaging ? 0m : checkoutModel.CheckOutSummary.TtlAddOnAmount);

                var _tcDiscDtl = checkoutModel.CheckOutSummary.DiscountDetails.FirstOrDefault(x => x.DiscType == DiscountType.TC) ?? new DiscountDetail();

                decimal reCalc = CalcTravelCreditUsable(totalAmtAllowToCalcUseableTC, availableCredit, availableCashCredit, DiscountType.TC, checkoutModel.MainProductType.ToString());
                if (checkoutModel.PaymentDetails.CreditUsed > reCalc || _tcDiscDtl.Disc_Amt > reCalc)
                {
                    decimal _amtUsed = checkoutModel.PaymentDetails.CreditUsed > reCalc ? checkoutModel.PaymentDetails.CreditUsed : _tcDiscDtl.Disc_Amt;
                    ModelState.AddModelError("Error", "Opps, looks like the travel credit used over specified limit. - " +
                        $"Exact amount used: {_amtUsed.ToString("n2")}{Environment.NewLine}" +
                        $"Amount can be use in this transaction: {reCalc}");
                    return View(postBackViewPath, checkoutModel);
                }
            }
            else if (!IsEventInventoryEnough(checkoutModel.AddOnProduct, null))
            {
                ModelState.AddModelError("Error", "Sorry, some of the Add On Products has been sold out.");
                return View(postBackViewPath, checkoutModel);
            }

            // Reset SuperPNRID stored in CheckoutProduct object.
            if (checkoutModel.SuperPNRID > 0 || !string.IsNullOrEmpty(checkoutModel.SuperPNRNo) || checkoutModel.OrderID > 0)
            {
                checkoutModel.SuperPNRID = 0;
                checkoutModel.SuperPNRNo = string.Empty;
                checkoutModel.OrderID = 0;
            }

            // Start insert product list here.
            var insertRes = await checkoutModel.Products.InsertDBRecord(checkoutModel);
            checkoutModel = insertRes.Item1;

            if (insertRes.Item2.Any(x => x.Value == false))
            {
                //add error return from SP InsertEventBooking into ModelState
                if (insertRes.Item2.Any(x => x.Key.StartsWith(typeof(ProductEventTicket).ToString()) && x.Value == false))
                {
                    ModelState.AddModelError("Error", insertRes.Item2.FirstOrDefault(x => x.Key.StartsWith(typeof(ProductEventTicket).ToString())).Key.Split(':', '\r')[2]);
                }
                else
                {
                    ModelState.AddModelError("Error", "An error occured, please try again later.");
                }
                List<string> errorKey = new List<string>();

                foreach (var item in insertRes.Item2)
                {
                    errorKey.Add(item.Key + " - " + item.Value.ToString());
                }

                logger.Fatal("Error occured when insert record to databse:" +
                    Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, errorKey));

                return View(postBackViewPath, checkoutModel);
            }

            MayFlower db = new MayFlower();
            if (checkoutModel.TourPackage != null)
            {
                var tourbook = checkoutModel.TourPackage.TourPackagesInfo.TourPackageBooking;
                var tourbookingDB = db.TourPackageBookings.FirstOrDefault(x => x.BookingID == tourbook.BookingID);

                tourbook = tourbookingDB;
                checkoutModel.SuperPNRID = tourbookingDB.SuperPNRID;
                checkoutModel.SuperPNRNo = tourbookingDB.SuperPNRNo;
                checkoutModel.OrderID = tourbookingDB.OrderID;
            }
            var superPNR = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRID == checkoutModel.SuperPNRID);

            if (checkoutModel.PaymentDetails != null)
            {
                List<int> paymentOrderInserted = new List<int>();
                SqlCommand command = new SqlCommand();

                try
                {
                    string currencyCode = checkoutModel.CheckOutSummary.CurrencyCode;

                    #region Insert/Update Processing Fee
                    var feeChargeOrder = db.FeeChargeOrders.FirstOrDefault(x => x.SuperPNROrder.SuperPNRID == checkoutModel.SuperPNRID);
                    var pf = checkoutModel.CheckOutSummary.ProcessingFee;

                    var latestFeeCharge = new FeeChargeOrder
                    {
                        OrderID = checkoutModel.OrderID,
                        CreatedByID = checkoutModel.BookingUserID,
                        CurrencyCode = checkoutModel.CheckOutSummary.CurrencyCode,
                        FeeChargeAmount = pf.Amt,
                        FeeCode = string.IsNullOrWhiteSpace(pf.FeeCode) ? checkoutModel.PaymentDetails.PaymentMethod : pf.FeeCode,
                        TaxCode = string.IsNullOrWhiteSpace(pf.TaxCode) ? "ES" : pf.TaxCode,
                        TaxAmount = pf.GST,
                        ModifiedByID = CurrentUserID,
                    };

                    if (feeChargeOrder == null)
                    {
                        PaymentServiceController.InsertFeeChargeOrder(latestFeeCharge, command);
                    }
                    else
                    {
                        PaymentServiceController.UpdateFeeChargeOrder(latestFeeCharge, command);
                    }
                    #endregion


                    #region Insert Payment

                    #region Insert Payment Order (Payment Details)
                    // Travel Credit 
                    List<CreditTypes> creditTypeList = new List<CreditTypes>();
                    PaymentServiceController paymentServiceController = null;

                    // Insert TC/TW
                    foreach (var item in checkoutModel.CheckOutSummary.DiscountDetails)
                    {
                        bool isVisualPaymentRC = false;
                        paymentServiceController = paymentServiceController ?? new PaymentServiceController();

                        if (item.DiscType == DiscountType.TC && item.Disc_Amt > 0)
                        {
                            #region Insert Travel Credit ()
                            //Insert temp credit records
                            if (!isAgent)
                            {
                                string mainProductType = checkoutModel.Flight != null ? "FLT" :
                                (checkoutModel.Flight == null && checkoutModel.Hotel != null ? "HTL" : "OTH");

                                Temp_UserCreditRedeem tempUserCredit = paymentServiceController.TempUserCreditRedeemPopulate(checkoutModel.OrderID, CurrentUserID, creditModel.CreditUsed, mainProductType, currencyCode);
                                paymentServiceController.TempCreditRedeemInsert(tempUserCredit, command);
                            }
                            #endregion

                            isVisualPaymentRC = true;
                        }
                        else if (item.DiscType == DiscountType.TW && item.Disc_Amt > 0)
                        {
                            #region Insert Travel Wallet ()
                            //Insert temp credit records
                            Temp_UserCashCreditRedeem tempUserCashCredit = paymentServiceController.TempUserCashCreditRedeemPopulate(CurrentUserID, checkoutModel.OrderID, checkoutModel.SuperPNRID, checkoutModel.SuperPNRNo, creditModel.EWallet.UseAmt, currencyCode);
                            paymentServiceController.TempCashCreditRedeemInsert(tempUserCashCredit, command);
                            #endregion

                            isVisualPaymentRC = true;
                        }

                        if (isVisualPaymentRC)
                        {
                            creditTypeList.Add(new CreditTypes
                            {
                                CreditType = item.DiscType,
                                CreditUsed = true,
                            });

                            PaymentOrder _paymentOrderInsert = new PaymentOrder
                            {
                                OrderID = checkoutModel.OrderID,
                                PaymentDate = DateTime.Now,
                                PaymentMethodCode = item.DiscType == DiscountType.TC || item.DiscType == DiscountType.AC ? (isAgent ? "AC" : "SC") : "TW",
                                PaymentStatusCode = "PEND",
                                CurrencyCode = currencyCode,
                                PaymentAmount = item.Disc_Amt,
                                ImagePath = string.Empty,
                                Ipay88RefNo = string.Empty,
                                Ipay88TransactionID = string.Empty,
                                CreatedByID = CurrentUserID,
                                ModifiedByID = CurrentUserID
                            };

                            paymentOrderInserted.Add(PaymentServiceController.InsertSuperPNRPaymentOrder(_paymentOrderInsert, command));
                        }
                    }

                    // Normal Payment
                    var contactPerson = checkoutModel.ContactPerson;
                    var creditCard = creditModel.CreditCard;
                    decimal paymentAmt = checkoutModel.TourPackage != null && checkoutModel.PaymentDetails != null ? checkoutModel.PaymentDetails.DepositAmt : checkoutModel.CheckOutSummary.GrandTtlAmt;
                    PaymentSubmitModels iPayModel = PaymentController.PopulatePaymentSubmitModel(DateTime.Now, checkoutModel.SuperPNRID, checkoutModel.SuperPNRNo, currencyCode, paymentAmt, checkoutModel.PaymentDetails.PaymentMethod, contactPerson.Phone1, contactPerson.Email, contactPerson.FullName);
                    string iPay88RefNo = checkoutModel.SuperPNRID + " - " + checkoutModel.SuperPNRNo; // Important, cannot simply change, will cause cannot requery fail.

                    // Insert for Not Full Store Credit/Cash Wallet
                    if (checkoutModel.CheckOutSummary.GrandTtlAmt > 0)
                    {
                        PaymentOrder paymentOrder = PaymentServiceController.PopulatePaymentPaymentOrder(checkoutModel.OrderID, currencyCode, iPay88RefNo, 0,
                        paymentAmt, "PEND", checkoutModel.PaymentDetails.PaymentMethod, CurrentUserID);
                        paymentOrderInserted.Add(PaymentServiceController.InsertSuperPNRPaymentOrder(paymentOrder, command));
                    }
                    #endregion

                    #region Insert Flight Booking Progress
                    foreach (var booking in superPNR.Bookings)
                    {
                        Alphareds.Module.BookingController.BookingServiceController.InsertBookingProgress(Enumeration.SMCBookingActivityType.PDP, booking.BookingID, "iPay88", CurrentUserID, command);
                    }
                    #endregion

                    // DB Transaction Commit and Close Here.
                    command.Transaction.Commit();

                    #endregion
                    if (checkoutModel.IsFixedPrice)
                    {
                        superPNR.SuperPNROrders.FirstOrDefault().IsFixed = checkoutModel.IsFixedPrice;
                        await db.SaveChangesAsync();
                    }
                    #region Dicision Redirect for Hold To Payment - HTP
                    if (User.Identity.IsAuthenticated && CustomPrincipal.IsAgent &&
                        !string.IsNullOrWhiteSpace(paymentLater) && paymentLater == "1" &&
                        (checkoutModel.Flight != null && checkoutModel.Hotel == null))
                    {
                        db = db.DisposeAndRefresh();
                        var _superPNRInserted = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRID == checkoutModel.SuperPNRID);
                        UpdateBookingFlights(ref _superPNRInserted, "HTP");
                        UpdateSuperPNROrders(ref _superPNRInserted, "HTP", "PEND", paymentOrderInserted);
                        db.SaveChanges();

                        Session.Remove(Enumeration.SessionName.CheckoutProduct.ToString() + tripid);
                        return RedirectToAction("OrderHistory", "Flight", new { tripid, bookingID = checkoutModel.SuperPNRNo, status = "payment-later" });
                    }
                    #endregion

                    #region Payment Gateway Redirect

                    PaymentController pc = new PaymentController();
                    string clientIP = HttpContext.Request.UserHostAddress;
                    string paymentMethod = checkoutModel.PaymentDetails.PaymentMethod.ToUpper();
                    string token = checkoutModel.SuperPNRID.ToString() + "," + checkoutModel.SuperPNRNo;
                    string encToken = General.CustomizeBaseEncoding.CodeBase64(token);
                    string encPaymentOrderIDList = Cryptography.AES.Encrypt(paymentOrderInserted.JoinToString(","));

                    FormCollection form = new FormCollection();
                    adyenCaptureResponseModels captureResponseModels2 = new adyenCaptureResponseModels();
                    iPayCaptureResponseModels captureResponseModels = new iPayCaptureResponseModels
                    { Status = "1", Amount = iPayModel.PaymentAmount, TransId = "" };

                    switch (checkoutModel.PaymentDetails.PaymentMethod.ToLower())
                    {
                        case "sc":
                            form.Add("Status", captureResponseModels.Status);
                            form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                            return await PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList);
                        case "ac":
                            form.Add("Status", captureResponseModels.Status);
                            form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                            return await PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList);
                        case "tw":
                            form.Add("Status", captureResponseModels.Status);
                            form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                            return await PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList);
                        case "ipacc":
                            return pc.iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, returnURL = "&minipayment=true", isRegister = checkoutModel.IsRegister }, Request.Url.Scheme), iPayModel, true);
                        case "ipafpx":
                            return pc.iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, returnURL = "&minipayment=true", isRegister = checkoutModel.IsRegister }, Request.Url.Scheme), iPayModel);
                        case "adyenc":
                            AdyenCardPaymentModels adyenModel = PaymentController.PopulateAdyenPaymentSubmitModel(checkoutModel.SuperPNRID, Request.Url.Scheme, checkoutModel.SuperPNRNo, currencyCode, paymentAmt, contactPerson.Email, creditCard);
                            return pc.AdyenCheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, returnURL = "&minipayment=true", isRegister = checkoutModel.IsRegister }, Request.Url.Scheme), adyenModel, Request.Form);
                        default:
                            ModelState.AddModelError("Error", "Payment Method Not Found.");
                            break;
                    }

                    #endregion

                }
                catch (Exception ex)
                {
                    command?.Transaction?.Rollback();

                    logger.Fatal(ex, $"Payment POST Error [{checkoutModel.SuperPNRID}: {checkoutModel.SuperPNRNo}] - {DateTime.Now.ToLoggerDateTime()}");
                    ModelState.AddModelError("Error", "Unexpected error occured, please try again later. If this keep happen please contact support.");
                }
            }

            return View(postBackViewPath, checkoutModel);
        }

        [HttpPost]
        public async Task<ActionResult> PaymentCheckOut(FormCollection form, iPayCaptureResponseModels responseModel, adyenCaptureResponseModels responseModel2, string token, string tripid,
            string paymentOdToken = null, bool fromRepay = false, string returnURL = null, bool isRegister = false, string eventRCode = null)
        {
            MayFlower db = null;
            string decToken = General.CustomizeBaseEncoding.DeCodeBase64(token);
            string decPaymentOdToken = null;
            List<int> _thisTransPaymentOrderID = new List<int>();
            List<int> _thisTransOrderID = new List<int>();
            Cryptography.AES.TryDecrypt(paymentOdToken, out decPaymentOdToken);

            if (decPaymentOdToken != null)
            {
                var _id = decPaymentOdToken.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var _itemId in _id)
                {
                    _thisTransPaymentOrderID.Add(_itemId.ToInt());
                }
            }

            string[] orderInfo = decToken.Split(',');

            bool isRequestError = form["TechErrDesc"] != null && form["TechErrDesc"].ToString() != null && (form["Status"].ToString() == "0" || form["Status"].ToString() == "C");
            bool isPaymentError = form["ErrDesc"] != null && form["ErrDesc"].ToString() != null && (form["Status"].ToString() == "0" || form["Status"].ToString() == "C" || form["Status"].ToString() == "F"); //boost is using "C" Cancel, "F" Fail

            // For capture payment amount base on Bank Response.

            decimal iPayResponseAmount = form["Amount"] != null && form["Amount"].ToString() != null ? Convert.ToDecimal(form["Amount"]) : 0m;
            decimal adyenResponseAmount = form["AmountValue"] != null && form["AmountValue"].ToString() != null ? Convert.ToDecimal(form["AmountValue"]) : 0m;
            decimal boostResponseAmount = form["Amount"] != null && form["Amount"].ToString() != null ? Convert.ToDecimal(form["Amount"]) : 0m;

            bool isUseAuthCapture = form["PaymentId"] != null && form["PaymentId"].ToString() != null && form["PaymentId"].ToString() == "55";

            if (responseModel2.ShopperReference != null && responseModel2.PspReference != null)
            {
                isUseAuthCapture = true;
            }

            Dictionary<string, ProductReserve.BookResultType> reserveStatus = new Dictionary<string, ProductReserve.BookResultType>();

            /* CON - Confirmed */
            /* PCP - Pending Company Payment */
            /* PPA - Pending Payment */
            string bookStatus = isRequestError || isPaymentError ? "EXP" : "CON";
            string paymentStatus = isRequestError || isPaymentError ? "FAIL" : "PAID";
            string urlPaymentStatus = isRequestError || isPaymentError ? "fail" : "success";

            List<Exception> error = new List<Exception>();
            bool errorDuringUpdate = false;

            try
            {
                Dictionary<string, string> captureResponse = new Dictionary<string, string>();
                foreach (string item in form)
                {
                    if (!item.ToLower().Contains("viewstate") || !item.ToLower().Contains("Status"))
                    {
                        captureResponse.Add(item, form[item]);

                        if (item.ToLower() == "errdesc" && form[item].Contains("Customer Cancel Transaction"))
                        {
                            urlPaymentStatus = "payment-cancel";
                        }
                        else if (item.ToLower() == "errdesc" && form[item].Contains("Transaction Timeout"))
                        {
                            urlPaymentStatus = "payment-timeout";
                        }
                    }
                }
                
                string iPayTransactionID = captureResponse.FirstOrDefault(x => x.Key == "TransId").Value;
                string iPayDescription = captureResponse.FirstOrDefault(x => x.Key == "ErrDesc").Value;
                string adyenShopperReference = captureResponse.FirstOrDefault(x => x.Key == "ShopperReference").Value;
                string adyenPspReference = captureResponse.FirstOrDefault(x => x.Key == "PspReference").Value;
                string adyenDescription = captureResponse.FirstOrDefault(x => x.Key == "ErrDesc").Value;

                //string boostOnlineRefNum = captureResponse.FirstOrDefault(x => x.Key == "OnlineRefNum").Value; //SuperPnr
                //string boostPaymentRefNum = captureResponse.FirstOrDefault(x => x.Key == "boostPaymentRefNum").Value; //boost ref
                
                iPayCaptureResponseModels iPayCaptureResponse = null;
                adyenCaptureResponseModels adyenCaptureResponse = null;
                //BoostCaptureResponseModels boostCaptureResponse = null;

                db = new MayFlower();
                string superPNRId_Resp = orderInfo[0];
                string superPNRNo_Resp = orderInfo[1];
                Expression<Func<SuperPNR, bool>> expression = (x => x.SuperPNRID.ToString() == superPNRId_Resp && x.SuperPNRNo == superPNRNo_Resp);
                var superPNR = db.SuperPNRs.FirstOrDefault(expression);

                if (superPNR == null)
                {
                    string mailToSend = Core.IsForStaging ? Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailStaging") :
                        Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailLive");
                    string iPayRespondSerialize = JsonConvert.SerializeObject(captureResponse);
                    string adyenRespondSerialize = JsonConvert.SerializeObject(captureResponse);

                    CommonServiceController.SendEmail(mailToSend, "[WARNING] Mayflower After Payment Success Error", DateTime.Now.ToLoggerDateTime() + " SuperPNR Not Found after payment success (" + decToken + "). " + iPayRespondSerialize + adyenRespondSerialize);
                    goto PaymentEndpoint;
                }

                // take order id to update
                _thisTransOrderID = superPNR.SuperPNROrders.SelectMany(x => x.PaymentOrders)
                    .Where(x => _thisTransPaymentOrderID.Any(s => s == x.PaymentID))
                    .Select(x => x.OrderID)
                    .Distinct()
                    .ToList();

                bool hasEventProduct = false;
                bool hasTourBooking = false;
                bool hasFlightBooking = superPNR.Bookings
                    .Any(x => _thisTransOrderID.Any(s => s == x.OrderID));
                bool hasHotelBooking = superPNR.BookingHotels
                    .Any(x => _thisTransOrderID.Any(s => s == x.OrderID));
                bool hasInsuranceBooking = superPNR.BookingInsurances
                    .Any(x => _thisTransOrderID.Any(s => s == x.OrderID));
                bool isFixedPackage = superPNR.SuperPNROrders.FirstOrDefault().IsFixed ?? false;
                bool hasCarRental = superPNR.CarRentalBookings.Any(x => _thisTransOrderID.Any(s => s == x.OrderID));
                //bool hasEventProduct = false;
                //bool hasFlightBooking = superPNR.Bookings.Count > 0;
                //bool hasHotelBooking = superPNR.BookingHotels.Count > 0;
                //bool hasInsuranceBooking = superPNR.BookingInsurances.Count > 0;
                string currency = superPNR.SuperPNROrders.FirstOrDefault().CurrencyCode;
                bool anyManualConfirmBookingRules = false;

                try
                {
                    hasTourBooking = superPNR.TourPackageBookings.Any(x => _thisTransOrderID.Any(s => s == x.OrderID));
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex, "Error on PaymentCheckOut action when attemp to read TourPackageBookings in database.");
                }

                try
                {
                    hasEventProduct = superPNR.EventBookings.Any(x => _thisTransOrderID.Any(s => s == x.OrderID));
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex, "Error on PaymentCheckOut action when attemp to read EventBooking in database.");
                }

                if (!(isPaymentError || isRequestError))
                {
                    decimal totalBookedAmt = 0m;
                    List<string> RHIActionRequired = new List<string>();
                    List<int> promoIDUsed = new List<int>();

                    if (hasFlightBooking)
                    {
                        foreach (var record in superPNR.Bookings)
                        {
                            try
                            {
                                ProductReserve.BookResultType bookResult = ProductReserve.BookResultType.AllFail;
                                string potentialErrMsg = null;
                                bool res = HotelServiceController.PlaceBooking(record, CurrentUserID, GetUserIP(), out potentialErrMsg, out bookResult);

                                if (potentialErrMsg != null)
                                {
                                    RHIActionRequired.Add(potentialErrMsg);
                                }

                                if (bookResult == ProductReserve.BookResultType.AllSuccess)
                                {
                                    string environment = Core.GetAppSettingValueEnhanced("Apps.Environment");

                                    bool requiredToBreak = false;
                                    foreach (var item in record.SuperPNR.SuperPNROrders)
                                    {
                                        foreach (var orderID in _thisTransOrderID)
                                        {
                                            if (item.OrderID == orderID)
                                            {
                                                // TODO: Implement tracking cookies here
                                                if (environment?.ToLower() == "production" && item.Affiliation.Description == "Skyscanner")
                                                {
                                                    var cookie = new System.Web.HttpCookie("trackingcookies_sat", "true")
                                                    {
                                                        Expires = DateTime.Now.AddMinutes(5)
                                                    };
                                                    HttpContext.Response.Cookies.Add(cookie);
                                                }
                                                else if (item.Affiliation.Description == "Any Other Supplier")
                                                {

                                                }
                                                requiredToBreak = true;
                                                break;
                                            }
                                        }

                                        if (requiredToBreak)
                                            break;
                                    }
                                }


                                totalBookedAmt += ((bookResult == ProductReserve.BookResultType.AllSuccess || bookResult == ProductReserve.BookResultType.PartialSuccess) ? record.TotalBookingAmt : 0m);
                                reserveStatus.Add(string.Format("Flight ({0} - {1})", record.SuperPNRID, record.SuperPNRNo), bookResult);
                            }
                            catch (AggregateException ae)
                            {
                                logger.Error(ae.GetBaseException(), "Reserve Fail in Flight Booking - " + DateTime.Now.ToLoggerDateTime());
                                reserveStatus.Add(string.Format("Flight ({0} - {1})", record.SuperPNRID, record.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                break; // exit looping prevent continue book
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, "Reserve Fail in Flight Booking - " + DateTime.Now.ToLoggerDateTime());
                                reserveStatus.Add(string.Format("Flight ({0} - {1})", record.SuperPNRID, record.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                break; // exit looping prevent continue book
                            }
                        }
                    }
                    // 2017/10/11 - TODO: Send Insurance Order Here, If have Flight need Flight Success only continue
                    #region purchase insurance 
                    if ((hasFlightBooking && hasInsuranceBooking && reserveStatus.All(x => x.Value == ProductReserve.BookResultType.AllSuccess))
                        || (!hasFlightBooking && hasInsuranceBooking))
                    {
                        foreach (var insurance in superPNR.BookingInsurances.Where(x => _thisTransOrderID.Any(s => s == x.OrderID)))
                        {
                            try
                            {
                                var bookRespond = ConfirmInsuranceQuotation(insurance, superPNR.SuperPNRNo);

                                bool res = bookRespond?.BatchBookResult == ProductReserve.BookResultType.AllSuccess || bookRespond?.BatchBookResult == ProductReserve.BookResultType.PartialSuccess;
                                reserveStatus.Add(string.Format("Insurance ({0} - {1})", insurance.SuperPNRID, insurance.SuperPNRNo), bookRespond?.BatchBookResult ?? ProductReserve.BookResultType.AllFail);
                                totalBookedAmt += (res ? insurance.TotalBookingAmt.Value : 0m);

                            }
                            catch (AggregateException ae)
                            {
                                logger.Error(ae.GetBaseException(), "Reserve Fail in Insurance Booking - " + DateTime.Now.ToLoggerDateTime());
                                reserveStatus.Add(string.Format("Insurance ({0} - {1})", insurance.SuperPNRID, insurance.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                break; // exit looping prevent continue book
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, "Reserve Fail in Insurance Booking - " + DateTime.Now.ToLoggerDateTime());
                                reserveStatus.Add(string.Format("Insurance ({0} - {1})", insurance.SuperPNRID, insurance.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                break; // exit looping prevent continue book
                            }
                        }
                    }
                    #endregion

                    if (hasHotelBooking && reserveStatus.All(x => x.Value == ProductReserve.BookResultType.AllSuccess))
                    {
                        string hostURL = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HostURL");

                        foreach (var record in superPNR.BookingHotels)
                        {
                            ProductReserve.BookingRespond bookRespond = null;
                            string errorList = null;

                            try
                            {
                                switch (record.SupplierCode.ToLower())
                                {
                                    case "ean":
                                        HotelBookingHandler.BookingQuery.Expedia supplierEAN = new HotelBookingHandler.BookingQuery.Expedia();
                                        bookRespond = await supplierEAN.CheckoutReserveRoom(record.BookingID, bookStatus, hostURL, db, isUseAuthCapture);
                                        break;
                                    case "tp":
                                        HotelBookingHandler.BookingQuery.Tourplan supplierTP = new HotelBookingHandler.BookingQuery.Tourplan();
                                        bookRespond = await supplierTP.CheckoutReserveRoom(record.BookingID, bookStatus, hostURL, db, isUseAuthCapture);
                                        break;
                                    case "jac":
                                        HotelBookingHandler.BookingQuery.JacTravel supplierJAC = new HotelBookingHandler.BookingQuery.JacTravel();
                                        bookRespond = await supplierJAC.CheckoutReserveRoom(record.BookingID, bookStatus, hostURL, db, isUseAuthCapture);
                                        break;
                                    case "hb":
                                        HotelBookingHandler.BookingQuery.HotelBeds supplierHB = new HotelBookingHandler.BookingQuery.HotelBeds();
                                        bookRespond = await supplierHB.CheckoutReserveRoom(record.BookingID, bookStatus, hostURL, db, isUseAuthCapture);
                                        break;
                                    case "taap":
                                        HotelBookingHandler.BookingQuery.ExpediaTAAP supplierTAAP = new HotelBookingHandler.BookingQuery.ExpediaTAAP();
                                        bookRespond = await supplierTAAP.CheckoutReserveRoom(record.BookingID, bookStatus, hostURL, db, isUseAuthCapture);
                                        break;
                                    case "rap":
                                        HotelBookingHandler.BookingQuery.EANRapid supplierEANRapid = new HotelBookingHandler.BookingQuery.EANRapid(Request?.UserAgent, GetUserIP(), tripid);
                                        bookRespond = await supplierEANRapid.CheckoutReserveRoom(record.BookingID, bookStatus, hostURL, db, isUseAuthCapture);
                                        break;
                                        // TODO: New Supplier Code Here
                                }

                                if (bookRespond?.ErrorLog?.Count > 0)
                                {
                                    errorList = JsonConvert.SerializeObject(bookRespond.ErrorLog, new JsonSerializerSettings
                                    {
                                        NullValueHandling = NullValueHandling.Ignore,
                                        Formatting = Formatting.Indented,
                                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                                    });
                                }

                                bool res = bookRespond?.BatchBookResult == ProductReserve.BookResultType.AllSuccess || bookRespond?.BatchBookResult == ProductReserve.BookResultType.PartialSuccess;
                                reserveStatus.Add(string.Format("Hotel ({0} - {1})", record.SuperPNRID, record.SuperPNRNo)
                                    + (errorList != null ? $"{Environment.NewLine} [{errorList}]" : null), bookRespond?.BatchBookResult ?? ProductReserve.BookResultType.AllFail);
                                totalBookedAmt += (res ? record.TotalBookingAmt : 0m);

                                // TODO: MH request Tourplan hotel need manually confirm.
                                if (res && record.SupplierCode == "TP" && !anyManualConfirmBookingRules)
                                {
                                    anyManualConfirmBookingRules = true;
                                }

                                #region set cookie and url for Trivago Tracking Pixel use
                                if (res && Request.Cookies["AffiliateSearchCookie"] != null)
                                {
                                    TrivagoTrackingPixel(superPNR.BookingHotels.FirstOrDefault(), tripid);
                                }
                                #endregion
                            }
                            catch (AggregateException ae)
                            {
                                logger.Error(ae.GetBaseException(), $"Reserve Fail in Hotel Booking - {DateTime.Now.ToLoggerDateTime()}"
                                    + (errorList != null ? $"{Environment.NewLine} [{errorList}]" : null));
                                reserveStatus.Add(string.Format("Hotel ({0} - {1})", record.SuperPNRID, record.SuperPNRNo)
                                    + (errorList != null ? $"{Environment.NewLine} [{errorList}]" : null), ProductReserve.BookResultType.AllFail);
                                break; // exit looping prevent continue book
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, $"Reserve Fail in Hotel Booking - {DateTime.Now.ToLoggerDateTime()}"
                                    + (errorList != null ? $"{Environment.NewLine} [{errorList}]" : null));
                                reserveStatus.Add(string.Format("Hotel ({0} - {1})", record.SuperPNRID, record.SuperPNRNo)
                                    + (errorList != null ? $"{Environment.NewLine} [{errorList}]" : null), ProductReserve.BookResultType.AllFail);
                                break; // exit looping prevent continue book
                            }

                            if (record.PromoID != 0)
                            {
                                promoIDUsed.Add(record.PromoID);
                            }
                        }
                    }
                    if (hasTourBooking && !hasHotelBooking && !hasFlightBooking)
                    {
                        var tourRecord = superPNR.TourPackageBookings.FirstOrDefault();
                        try
                        {
                            decimal bankpaymentAmt = superPNR.SuperPNROrders.FirstOrDefault().PaymentOrders.Sum(x => x.PaymentAmount);
                            totalBookedAmt += bankpaymentAmt;

                            decimal ttlAmtCharges = tourRecord.TotalBookingAmt + superPNR.SuperPNROrders
                                    .Where(x => _thisTransOrderID.Any(s => s == x.OrderID))
                                    .Sum(s =>
                                    {
                                        return
                                        +s.FeeChargeOrders.Sum(ss => ss.FeeChargeAmount + ss.TaxAmount);
                                    });
                            tourRecord.BookingStatusCode = totalBookedAmt == ttlAmtCharges ? "CON" : "DPT";
                            reserveStatus.Add(string.Format("Tour ({0} - {1})", tourRecord.SuperPNRID, tourRecord.SuperPNRNo), ProductReserve.BookResultType.AllSuccess);
                        }
                        catch (AggregateException ae)
                        {
                            logger.Error(ae.GetBaseException(), "Update Fail in Tour Package Booking - " + DateTime.Now.ToLoggerDateTime());
                            reserveStatus.Add(string.Format("Tour ({0} - {1})", tourRecord.SuperPNRID, tourRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "Update Fail in Tour Package Booking - " + DateTime.Now.ToLoggerDateTime());
                            reserveStatus.Add(string.Format("Tour ({0} - {1})", tourRecord.SuperPNRID, tourRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                        }
                    }
                    if (hasCarRental)
                    {
                        var rentalRecord = superPNR.CarRentalBookings.FirstOrDefault();
                        CheckoutProduct checkout = new CheckoutProduct();
                        checkout.SuperPNRID = superPNR.SuperPNRID;
                        checkout.SuperPNRNo = superPNR.SuperPNRNo;
                        checkout.PromoID = rentalRecord.PromoID ?? 0;

                        ProductCarRental model = rentalRecord.ToProductCarRental();
                        try
                        {
                            model.Result = CarsRentalServiceCall.GetCarRentalList(model.SearchInfo);
                            if (model.Result != null && model.Result.Success == "true" && model.VehicleSelected != null)
                            {
                                checkout.InsertProduct(model);
                                try
                                {
                                    Alphareds.Module.CarRentalWebService.CRWS.OTA_BookCarRentalRS reserveResultAll = CarsRentalServiceCall.BookCarRentalList(checkout);
                                    bool isAllReserveSuccess = reserveResultAll != null && reserveResultAll.Errors != null && reserveResultAll.Success == "true";

                                    #region Action - Reserve Result Error/Fail
                                    if (isAllReserveSuccess || reserveResultAll.Errors != null)
                                    {
                                        string errorMsg = string.Format("Success - " + reserveResultAll.Success + "; Error - " + reserveResultAll.Errors.ErrorMessage);
                                        logger.Error(errorMsg, "Book Car Rental Fail in PaymentCheckout - " + DateTime.Now.ToLoggerDateTime());
                                        reserveStatus.Add(string.Format("CarRental ({0} - {1})", rentalRecord.SuperPNRID, rentalRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                    }
                                    else
                                    {
                                        decimal bankpaymentAmt = superPNR.SuperPNROrders.FirstOrDefault().PaymentOrders.Sum(x => x.PaymentAmount);
                                        totalBookedAmt += bankpaymentAmt;

                                        rentalRecord.BookingStatusCode = "CON";
                                        rentalRecord.BookingNo = reserveResultAll.BookCarRentalRSCore.BookingID.Value.ToString();
                                        reserveStatus.Add(string.Format("CarRental ({0} - {1})", rentalRecord.SuperPNRID, rentalRecord.SuperPNRNo), ProductReserve.BookResultType.AllSuccess);
                                    }
                                    #endregion
                                }
                                catch (AggregateException ae)
                                {
                                    logger.Error(ae.GetBaseException(), "Reserve Fail in Car Rental Booking - " + DateTime.Now.ToLoggerDateTime());
                                    reserveStatus.Add(string.Format("CarRental ({0} - {1})", rentalRecord.SuperPNRID, rentalRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, "Reserve Fail in Car Rental Booking - " + DateTime.Now.ToLoggerDateTime());
                                    reserveStatus.Add(string.Format("CarRental ({0} - {1})", rentalRecord.SuperPNRID, rentalRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                }
                            }
                            else
                            {
                                string errorMsg = string.Format("Success - " + model.Result.Success + "; Error - " + model.Result.Errors?.ErrorMessage);
                                logger.Error(errorMsg, "Get Car Rental List Fail in PaymentCheckout - " + DateTime.Now.ToLoggerDateTime());
                                reserveStatus.Add(string.Format("CarRental ({0} - {1})", rentalRecord.SuperPNRID, rentalRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                            }
                        }
                        catch (AggregateException ae)
                        {
                            logger.Error(ae.GetBaseException(), "Get Car Rental List Fail in PaymentCheckout - " + DateTime.Now.ToLoggerDateTime());
                            reserveStatus.Add(string.Format("Car Rental ({0} - {1})", rentalRecord.SuperPNRID, rentalRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "Get Car Rental List Fail in PaymentCheckout - " + DateTime.Now.ToLoggerDateTime());
                            reserveStatus.Add(string.Format("Car Rental ({0} - {1})", rentalRecord.SuperPNRID, rentalRecord.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                        }
                    }

                    bool finalReserveResult = reserveStatus.All(x => x.Value == ProductReserve.BookResultType.AllSuccess);
                    bookStatus = finalReserveResult ? "CON" : (reserveStatus.All(x => x.Value == ProductReserve.BookResultType.AllFail) ? "EXP" : "RHI");

                    /* Usage: Update instant to get latest information updated by store procedure.
                     * Main Reason: Inside requery need to move out [await db.SaveChangesAsync()]. And save over Program Area. */
                    //Save Changes and Update instant to get latest information updated by store procedure.
                    await db.SaveChangesAsync();
                    db = new MayFlower();
                    superPNR = db.SuperPNRs.FirstOrDefault(expression);
                    var paymentRecords = superPNR.SuperPNROrders.SelectMany(x => x.PaymentOrders)
                        .Where(x => _thisTransPaymentOrderID.Any(a => a == x.PaymentID)).ToList();
                    string _PaymentMethodCode = "";

                    foreach (var _payment in paymentRecords)
                    {
                        string _method = _payment.PaymentMethodCode.ToLower();
                        if (_method.StartsWith("ipa"))
                        {
                            _PaymentMethodCode = _method;
                        }
                        else if (_method.StartsWith("adyen"))
                        {
                            _PaymentMethodCode = _method;
                        }
                        else if (_method.StartsWith("boost"))
                        {
                            _PaymentMethodCode = _method;
                        }
                    }

                    // Full credit usage.
                    if (string.IsNullOrWhiteSpace(_PaymentMethodCode))
                    {
                        _PaymentMethodCode = paymentRecords.LastOrDefault(x => x.PaymentMethodCode == "SC" || x.PaymentMethodCode == "AC" || x.PaymentMethodCode == "TW")?.PaymentMethodCode ?? "";
                    }

                    string bookBasicInfo = string.Format("{0} - {1} [Amount: {2}]", superPNR.SuperPNRID, superPNR.SuperPNRNo, superPNR.SuperPNROrders.Sum(s => s.PaymentOrders.Sum(x => x.PaymentAmount)).ToString("n2"));

                    if (bookStatus == "CON" || bookStatus == "RHI" || bookStatus == "EXP")
                    {
                        UpdateSuperPNROrders(ref superPNR, bookStatus, paymentStatus, _thisTransPaymentOrderID, adyenPspReference);
                        UpdateStatusFromReserve(ref superPNR, reserveStatus, _thisTransOrderID);

                        if (bookStatus == "EXP" && hasEventProduct)
                        {
                            // Add back ticket amount into inventory
                            HotelServiceController.UpdateEventBooking(superPNR, bookStatus);
                        }
                        // Add Event Product Amount Here
                        // Also Deduct Event Product Allotment
                        if (bookStatus == "CON" || bookStatus == "RHI")
                        {
                            if (hasEventProduct)
                            {
                                try
                                {
                                    if (bookStatus == "CON")
                                    {
                                        UpdateEventBookings(ref superPNR, bookStatus); // update status to CON first then only ticket will deduct

                                        reserveStatus.Add(string.Format("Event ({0} - {1})", superPNR.SuperPNRID, superPNR.SuperPNRNo), ProductReserve.BookResultType.AllSuccess);
                                        totalBookedAmt += superPNR.EventBookings.Sum(s => s.TotalBookingAmt); // add on products amount
                                    }
                                    else
                                    {
                                        UpdateEventBookings(ref superPNR, bookStatus);

                                        //Add back ticket amount into inventory 
                                        HotelServiceController.UpdateEventBooking(superPNR, bookStatus);
                                        logger.Error("Add back ticket amount into inventory. Will not add amount into booked totalAmt. " + bookBasicInfo);
                                        reserveStatus.Add(string.Format("Event ({0} - {1})", superPNR.SuperPNRID, superPNR.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                    }
                                }
                                catch (AggregateException ae)
                                {
                                    logger.Error(ae.GetBaseException(), "Error when attemp to update EventBooking via SP. At Status " + bookStatus
                                        + Environment.NewLine + Environment.NewLine + bookBasicInfo);
                                    reserveStatus.Add(string.Format("Event ({0} - {1})", superPNR.SuperPNRID, superPNR.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, "Error when attemp to update EventBooking via SP. At Status " + bookStatus
                                        + Environment.NewLine + Environment.NewLine + bookBasicInfo);
                                    reserveStatus.Add(string.Format("Event ({0} - {1})", superPNR.SuperPNRID, superPNR.SuperPNRNo), ProductReserve.BookResultType.AllFail);
                                }
                            }

                            // Get SuperPNROrders Status after add new Book Records.
                            RefreshStatus(ref finalReserveResult, ref bookStatus, reserveStatus);
                        }

                        // Auth & Capture Code Here
                        // Set Status Here
                        if (isUseAuthCapture && totalBookedAmt == 0)
                        {
                            try
                            {
                                // Void Full Payment Here       
                                /* TODO: code here */
                                //Ipay Amount need to like 1,234.00, have to use ToString("n2") - Note
                                if (_PaymentMethodCode.StartsWith("adyen"))
                                {
                                    adyenCaptureResponse = PaymentServiceController.Adyen.VoidPayment(adyenShopperReference, adyenPspReference, currency, adyenResponseAmount.ToString("n2"));
                                }
                                else if (_PaymentMethodCode.StartsWith("ipacc"))
                                {
                                    iPayCaptureResponse = PaymentServiceController.iPay88.VoidPayment(iPayTransactionID, currency, iPayResponseAmount.ToString("n2"));
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Fatal(ex, DateTime.Now.ToLoggerDateTime() + " Cannot void payment. Total Booking Amount : " + totalBookedAmt.ToString()
                                + " SuperPNR : " + superPNR.SuperPNRNo + "<br/><br/>" + JsonConvert.SerializeObject(iPayCaptureResponse) + bookBasicInfo
                                + " SuperPNR : " + superPNR.SuperPNRNo + "<br/><br/>" + JsonConvert.SerializeObject(adyenCaptureResponse) + bookBasicInfo);
                            }

                            if (((iPayCaptureResponse == null || iPayCaptureResponse.Status != "1") && _PaymentMethodCode.StartsWith("ipa"))
                                || ((adyenCaptureResponse == null || adyenCaptureResponse.Status != "1") && _PaymentMethodCode.StartsWith("adyen")))
                            {
                                bookStatus = "RHI";
                                paymentStatus = "AUTH";
                                // Need send email inform customer service if void fail then redirect back to payment page inform user.
                                logger.Fatal(DateTime.Now.ToLoggerDateTime() + " Cannot void payment. SuperPNR " + superPNR.SuperPNRNo + "<br/><br/>" + JsonConvert.SerializeObject(iPayCaptureResponse));
                                //logger.Fatal(DateTime.Now.ToLoggerDateTime() + " Cannot void payment. SuperPNR " + superPNR.SuperPNRNo + "<br/><br/>" + JsonConvert.SerializeObject(adyenCaptureResponse));
                            }
                            else if (iPayCaptureResponse?.Status == "1" || adyenCaptureResponse?.Status == "1")
                            {
                                bookStatus = "EXP";
                                paymentStatus = "VOID";
                                //ModelState.AddModelError("Error", "Your payment was cancelled succesfully. We will not charge any chargers on it. TQ");
                            }
                        }
                        else if (isUseAuthCapture && totalBookedAmt != 0)
                        {
                            // Capture Success Reserved Item Total Amount
                            /* TODO: code here */
                            //test on here debugg
                            try
                            {
                                decimal _captureAmt = 0;

                                decimal ttlAmtCharges = hasTourBooking ? totalBookedAmt : totalBookedAmt + superPNR.SuperPNROrders
                                    .Where(x => _thisTransOrderID.Any(s => s == x.OrderID))
                                    .Sum(s =>
                                {
                                    return //-Math.Abs(s.CreditAmount)
                                    +-Math.Abs(s.DiscountAmt)
                                    + s.FeeChargeOrders.Sum(ss => ss.FeeChargeAmount + ss.TaxAmount)
                                    + -Math.Abs(s.PaymentOrders.Where(p =>
                                        _thisTransPaymentOrderID.Any(a => a == p.PaymentID) &&
                                        (p.PaymentMethodCode == "SC" || p.PaymentMethodCode == "TW")).Sum(ps => ps.PaymentAmount))
                                    ;
                                });

                                decimal amtAuthorized = superPNR.SuperPNROrders.SelectMany(s => s.PaymentOrders)
                                    .Where(p => _thisTransPaymentOrderID.Any(a => a == p.PaymentID) &&
                                        p.PaymentMethodCode != "SC" && p.PaymentMethodCode != "TW")
                                    .Sum(p => p.PaymentAmount);

                                if (bookStatus == "CON")
                                {
                                    _captureAmt = amtAuthorized;
                                    if (amtAuthorized < ttlAmtCharges)
                                    {
                                        logger.Warn("Total Amount Charges is more than amount authorized. Please check this booking. SuperPNR : " + superPNR.SuperPNRNo);
                                    }
                                }
                                else
                                {
                                    _captureAmt = ttlAmtCharges;
                                }

                                //Ipay Amount need to like 1,234.00, have to use ToString("n2") - Note
                                if (_PaymentMethodCode.StartsWith("ipacc"))
                                {
                                    iPayCaptureResponse = PaymentServiceController.iPay88.CapturePayment(iPayTransactionID, currency, _captureAmt.ToString("n2"));
                                }
                                else
                                {
                                    adyenCaptureResponse = PaymentServiceController.Adyen.CapturePayment(adyenShopperReference, adyenPspReference, currency, _captureAmt.ToString("n2"));
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Fatal(ex, DateTime.Now.ToLoggerDateTime() + " Cannot capture partial payment. Total Booking Amount : " + totalBookedAmt.ToString()
                                + " SuperPNR : " + superPNR.SuperPNRNo + "<br/><br/>" + JsonConvert.SerializeObject(iPayCaptureResponse) + bookBasicInfo
                                + " SuperPNR : " + superPNR.SuperPNRNo + "<br/><br/>" + JsonConvert.SerializeObject(adyenCaptureResponse) + bookBasicInfo);
                            }

                            if (((iPayCaptureResponse == null || iPayCaptureResponse.Status != "1") && _PaymentMethodCode.StartsWith("ipa"))
                                || ((adyenCaptureResponse == null || adyenCaptureResponse.Status != "1") && _PaymentMethodCode.StartsWith("adyen")))
                            {
                                bookStatus = "RHI";
                                paymentStatus = "AUTH";
                            }
                            else if (iPayCaptureResponse?.Status == "1" || adyenCaptureResponse?.Status == "1")
                            {
                                // If capture success then captured partial
                                paymentStatus = "CAPT";
                                //test on here debug
                                /* TODO: Compare against Capture Amount Same with SuperPNROrders Total Amount Or Not 
                                 * If NOT SAME then need revert SuperPNROrders status to RHI.
                                 */
                                var sumOfSuperPNROrders = superPNR.SuperPNROrders.SelectMany(x => x.PaymentOrders)
                                    .Where(x => _thisTransPaymentOrderID.Any(s => s == x.PaymentID) && x.PaymentMethodCode != null &&
                                            x.PaymentMethodCode.ToLower() != "sc" && x.PaymentMethodCode.ToLower() != "tw")
                                    .Sum(s => s.PaymentAmount);
                                var adyenSumOfSuperPNROrders = Core.IsForStaging ? 500 : sumOfSuperPNROrders;
                                if ((iPayCaptureResponse?.Status == "1" && sumOfSuperPNROrders > iPayCaptureResponse.Amount)
                                    || (adyenCaptureResponse?.Status == "1" && adyenSumOfSuperPNROrders > adyenCaptureResponse.AmountValue))
                                {
                                    bookStatus = "RHI";
                                }
                            }
                        }
                        else if (!isUseAuthCapture)
                        {
                            if (totalBookedAmt == 0 && bookStatus == "EXP" && (_PaymentMethodCode == "AC" || _PaymentMethodCode == "SC" || _PaymentMethodCode == "TW"))
                            {
                                paymentStatus = "FAIL";
                            }
                            else if (totalBookedAmt == 0 || (bookStatus == "RHI" || bookStatus == "EXP"))
                            {
                                // If all book failed, update SuperPNROrders to RHI. (FPX or Travel Credit)
                                bookStatus = "RHI";
                                paymentStatus = "PAID";
                            }
                            else if (bookStatus == "CON")
                            {
                                paymentStatus = "PAID";
                            }
                        }

                        /* Update Status Here */
                        try
                        {
                            if (totalBookedAmt == 0 && paymentRecords.All(s => s.PaymentMethodCode.ToLower() == "sc" || s.PaymentMethodCode.ToLower() == "ac" || s.PaymentMethodCode.ToLower() == "tw"))
                            {
                                // If Payment is Travel Credit and all Booking Failed, then update booking to FAIL.
                                // TODO: Need Refund failed total amount used.
                                UpdateSuperPNRAllStatus(ref superPNR, "EXP", "FAIL", _thisTransPaymentOrderID, _thisTransOrderID, null); // full credit payment no gateway reference no.
                                await db.SaveChangesAsync();
                            }
                            else
                            {
                                UpdateSuperPNROrders(ref superPNR, bookStatus, paymentStatus, _thisTransPaymentOrderID);
                                UpdateUserCredit(ref superPNR, bookStatus, paymentStatus, _thisTransPaymentOrderID, db);

                                //insert TW after success payment and update
                                if (bookStatus == "CON" && superPNR.EventBookings.Any(x => _thisTransOrderID.Any(a => a == x.OrderID && x.EventProduct.TicketCategory.EventTypeCode == "TW")))
                                {
                                    var eventTW = superPNR.EventBookings.Where(x => _thisTransOrderID.Any(a => a == x.OrderID && x.EventProduct.TicketCategory.EventTypeCode == "TW"));
                                    decimal amountTW = eventTW.Sum(x => x.TotalBookingAmt);
                                    insertCashCredit(superPNR.CreatedByID, eventTW.FirstOrDefault().CurrencyCode, amountTW, "insert TW for EventBundle");
                                }

                                UpdateStatusFromReserve(ref superPNR, reserveStatus, _thisTransOrderID);
                                await db.SaveChangesAsync();
                            }
                        }
                        catch (System.Data.Entity.Validation.DbEntityValidationException ee)
                        {
                            #region DB Change Track
                            var serializeSetting = new JsonSerializerSettings
                            {
                                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                                NullValueHandling = NullValueHandling.Ignore,
                                MissingMemberHandling = MissingMemberHandling.Ignore,
                            };

                            try
                            {
                                string statusMsg = JsonConvert.SerializeObject(reserveStatus, Formatting.None, serializeSetting);
                                string entryErrorMsg = JsonConvert.SerializeObject(ee.EntityValidationErrors.SelectMany(a => a.ValidationErrors), Formatting.None, serializeSetting);
                                List<string> newValue = new List<string>();
                                bool anySuccessBooked = reserveStatus.Any(a => a.Value == ProductReserve.BookResultType.AllSuccess || a.Value == ProductReserve.BookResultType.PartialSuccess);

                                foreach (var item in ee.EntityValidationErrors)
                                {
                                    foreach (var dbEntryError in item.ValidationErrors)
                                    {
                                        var usedValue = item.Entry.CurrentValues.GetValue<object>(dbEntryError.PropertyName)?.ToString();
                                        newValue.Add($"{dbEntryError.PropertyName}:  {usedValue}");

                                        if (anySuccessBooked)
                                        {
                                            // revert back to original value for save booked record.
                                            var _property = item.Entry.Property(dbEntryError.PropertyName);
                                            var _oriValue = item.Entry.OriginalValues.GetValue<object>(dbEntryError.PropertyName);
                                            _property.CurrentValue = _oriValue;

                                            item.Entry.CurrentValues.SetValues(_property);
                                        }
                                    }
                                }

                                logger.Log(LogLevel.Fatal, ee.GetBaseException(), "[EntityException] Error on CheckoutController.cs when attemp to payment status. "
                                    + (isUseAuthCapture ? "iPay88 status = " + iPayCaptureResponse?.Status ?? "" : "") + Environment.NewLine + Environment.NewLine + bookBasicInfo
                                    + (isUseAuthCapture ? "Adyen status = " + adyenCaptureResponse?.Status ?? "" : "") + Environment.NewLine + Environment.NewLine + bookBasicInfo
                                    + Environment.NewLine + Environment.NewLine + statusMsg
                                    + Environment.NewLine + Environment.NewLine + entryErrorMsg
                                    + string.Join(Environment.NewLine, newValue));
                            }
                            catch (Exception ex)
                            {
                                logger.Log(LogLevel.Debug, ex);
                            }
                            finally
                            {
                                await db.SaveChangesAsync();
                            }
                            #endregion
                        }
                        catch (Exception ex)
                        {
                            logger.Fatal(ex, DateTime.Now.ToLoggerDateTime() + " Cannot update status at CodeBlock Update Status Here : " + totalBookedAmt.ToString()
                            + Environment.NewLine + Environment.NewLine + bookBasicInfo
                            + Environment.NewLine + Environment.NewLine + "<br/><br/>" + JsonConvert.SerializeObject(iPayCaptureResponse)
                            + Environment.NewLine + Environment.NewLine + (isUseAuthCapture ? "iPay88 status = " + iPayCaptureResponse?.Status ?? "" : "")
                            + Environment.NewLine + Environment.NewLine
                            + Environment.NewLine + Environment.NewLine + "<br/><br/>" + JsonConvert.SerializeObject(adyenCaptureResponse)
                            + Environment.NewLine + Environment.NewLine + (isUseAuthCapture ? "Adyen status = " + adyenCaptureResponse?.Status ?? "" : "")
                            + Environment.NewLine + Environment.NewLine + string.Format("Status Reference: {0}", JsonConvert.SerializeObject(reserveStatus)));

                            bookStatus = "RHI";
                            UpdateSuperPNROrders(ref superPNR, bookStatus, paymentStatus, _thisTransPaymentOrderID);
                            await db.SaveChangesAsync();
                        }

                        /* Only success booked AND collected money send PDF. */
                        if (bookStatus == "CON" && ((iPayCaptureResponse?.Status == "1" || adyenCaptureResponse?.Status == "1") || !isUseAuthCapture))
                        {
                            if (promoIDUsed.Count > 0)
                            {
                                foreach (var _promoId in promoIDUsed.Distinct())
                                {
                                    PromoCodeFunctions promoCodeFunctions = new PromoCodeFunctions(_promoId, db);
                                    if (promoCodeFunctions.GetFrontendFunction.ManualConfirmBooking)
                                    {
                                        anyManualConfirmBookingRules = true;
                                        break;
                                    }
                                }
                            }

                            if (!anyManualConfirmBookingRules)
                            {
                                #region Email PDF Section
                                bool sendStatus = false;
                                try
                                {
                                    for (int _attemp = 0; _attemp < 3; _attemp++)
                                    {
                                        SendPDFQueueHandler.Functions.CallAPI callPDFWebAPI = new SendPDFQueueHandler.Functions.CallAPI();
                                        var sendResp = callPDFWebAPI.SendPDFAfterSuccess(superPNR);
                                        sendStatus = sendResp?.SendStatus != null && sendResp.SendStatus;

                                        //sendStatus = Alphareds.Module.BookingController.BookingServiceController.SendBookingItineraryEmailAfterSuccess(superPNR);
                                        if (sendStatus)
                                            break;
                                    }

                                    //send ConfirmedQueuePlacesStatusEmail when PDF fail to send
                                    if (!sendStatus)
                                    {
                                        var ContactEmailAndNameResult = GetContactEmailAndName(superPNR, hasFlightBooking, hasHotelBooking);
                                        var userEmail = ContactEmailAndNameResult.FirstOrDefault(x => x.Key == "email").Value;
                                        var userName = ContactEmailAndNameResult.FirstOrDefault(x => x.Key == "name").Value;
                                        
                                        //SendConfirmedQueuePlacesStatusEmail(superPNR.UserID, db, hasFlightBooking ? ProductTypes.Flight : ProductTypes.Undefined);
                                        SendConfirmedQueuePlacesStatusEmail(userEmail, userName, db, hasFlightBooking ? ProductTypes.Flight : ProductTypes.Undefined);
                                    }

                                    //for EU flight send email
                                    if (hasFlightBooking && DateTime.Now < new DateTime(2020,1,1))
                                    {
                                        var destination = superPNR.Bookings.FirstOrDefault().Destination;

                                        bool checkEUFlightEmailArea = UtilitiesService.CheckEUFlightEmailArea(destination);
                                        if (checkEUFlightEmailArea)
                                        {
                                            string email = "";
                                            if (superPNR.UserID == 0)
                                            {
                                                email = superPNR.Bookings.FirstOrDefault().Paxes.FirstOrDefault(x => x.IsContactPerson == true).PassengerEmail;
                                            }
                                            else
                                            {
                                                var user = db.Users.FirstOrDefault(x => x.UserID == superPNR.UserID); //if no login
                                                email = user.Email;
                                            }
                                            Hashtable ht = new Hashtable();
                                            string mailTemp = Core.getMailTemplate("london_designer_outlet", ht);

                                            for (int _attemp = 0; _attemp < 3; _attemp++)
                                            {
                                                bool isSuccessSendLondonEmail = CommonServiceController.SendEmail(email, "Extra 10% off at London Designer Outlet!", mailTemp);

                                                if (isSuccessSendLondonEmail)
                                                {
                                                    break;
                                                }
                                            }
                                        }

                                    }

                                    //SendPDFQueueHandler.Functions.CallAPI callPDFWebAPI = new SendPDFQueueHandler.Functions.CallAPI();
                                    //var sendResp = callPDFWebAPI.SendPDF(superPNR, true);
                                    //sendStatus = sendResp.SendStatus;
                                }
                                catch (AggregateException ae)
                                {
                                    logger.Warn(ae, "Send booking success PDF email fail - " + DateTime.Today.ToLoggerDateTime()
                                        + Environment.NewLine + Environment.NewLine + bookBasicInfo);
                                    RHIActionRequired.Add("Send Booking Success PDF Email. " + bookBasicInfo);
                                }
                                catch (Exception ex)
                                {
                                    logger.Warn(ex, "Send booking success PDF email fail - " + DateTime.Today.ToLoggerDateTime()
                                        + Environment.NewLine + Environment.NewLine + bookBasicInfo);
                                    RHIActionRequired.Add("Send Booking Success PDF Email. " + bookBasicInfo);
                                }
                                #endregion
                            }
                            else
                            {
                                try
                                {
                                    superPNR.SuperPNROrders.ForEach(x =>
                                    {
                                        x.BookingStatusCode = "RHI";
                                        x.PaymentOrders.ForEach(p => p.RequeryStatusCode = "MAN");
                                    });

                                    db.SaveChanges();

                                    //SendConfirmedQueuePlacesStatusEmail(superPNR.UserID, db, hasFlightBooking ? ProductTypes.Flight : ProductTypes.Undefined);
                                    
                                    var ContactEmailAndNameResult = GetContactEmailAndName(superPNR, hasFlightBooking, hasHotelBooking);
                                    var userEmail = ContactEmailAndNameResult.FirstOrDefault(x => x.Key == "email").Value;
                                    var userName = ContactEmailAndNameResult.FirstOrDefault(x => x.Key == "name").Value;

                                    SendConfirmedQueuePlacesStatusEmail(userEmail, userName, db, hasFlightBooking ? ProductTypes.Flight : ProductTypes.Undefined);
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, "Error on PaymentCheckOut while override book status on {anyManualConfirmBookingRules}.");
                                }
                            }
                        }

                        /* Exception Email Handle Section */
                        if (reserveStatus.Any(x => x.Value == ProductReserve.BookResultType.PartialSuccess) ||
                            (reserveStatus.Any(x => x.Value == ProductReserve.BookResultType.AllFail) && (!isUseAuthCapture && _PaymentMethodCode != "AC")) ||
                            (bookStatus == "RHI" && !anyManualConfirmBookingRules))
                        {
                            #region Exception Email Handle Section
                            string mailToSend = Core.IsForStaging ? Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailStaging") : Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailLive");

                            string emailSubject = "Mayflower RHI Booking TODO Action - " + DateTime.Now.ToLoggerDateTime();
                            string reserveResultJSON = JsonConvert.SerializeObject(reserveStatus, Formatting.Indented);

                            StringBuilder plainTextContent = new StringBuilder();
                            plainTextContent.AppendLine("Dear Support,").AppendLine();
                            plainTextContent.Append("There are errors while customer attemp to reserve the booking. Further action below required: ");
                            plainTextContent.AppendLine();
                            plainTextContent.AppendLine().AppendLine("Additional Info:");
                            plainTextContent.AppendLine(bookBasicInfo).AppendLine();
                            plainTextContent.AppendLine(string.Join(", " + Environment.NewLine, RHIActionRequired));
                            plainTextContent.AppendLine(reserveResultJSON).AppendLine();

                            logger.Log(LogLevel.Error, bookBasicInfo + reserveResultJSON);
                            CommonServiceController.SendEmail(mailToSend.Trim(), emailSubject, plainTextContent.ToString(), null, false);
                            #endregion
                            
                            //send ConfirmedQueuePlacesStatusEmail when RHI
                            //SendConfirmedQueuePlacesStatusEmail(superPNR.UserID, db, hasFlightBooking ? ProductTypes.Flight : ProductTypes.Undefined);

                            var ContactEmailAndNameResult = GetContactEmailAndName(superPNR, hasFlightBooking, hasHotelBooking);
                            var userEmail = ContactEmailAndNameResult.FirstOrDefault(x => x.Key == "email").Value;
                            var userName = ContactEmailAndNameResult.FirstOrDefault(x => x.Key == "name").Value;              
                            
                            SendConfirmedQueuePlacesStatusEmail(userEmail, userName, db, hasFlightBooking ? ProductTypes.Flight : ProductTypes.Undefined);
                        }

                        /* Handle Route After All Action Done */
                        if ((paymentStatus == "AUTH" || paymentStatus == "CAPT" || paymentStatus == "PAID") &&
                            (bookStatus == "RHI" || bookStatus == "CON"))
                        {
                            if (hasFlightBooking)
                            {
                                foreach (var fltBooking in superPNR.Bookings)
                                {
                                    try
                                    {
                                        if (fltBooking.SupplierCode == "SBRE" && (fltBooking.BookingStatusCode != "RHI" && fltBooking.BookingStatusCode != "EXP"))
                                        {
                                            string pricingTicketing = fltBooking.QueuePCC ?? string.Empty;
                                            string queueNo = fltBooking.QueueNumber.ToString() ?? string.Empty;
                                            string ticketingQueueNo = fltBooking.TicketingQueueNo.ToString() ?? string.Empty;

                                            bool isSuccessQueue = HotelServiceController.PlaceQueueToSabre(fltBooking.SupplierBookingNo, pricingTicketing, queueNo);

                                            if (isSuccessQueue)
                                            {
                                                bool isSecondSuccessQueue = HotelServiceController.PlaceQueueToSabre(fltBooking.SupplierBookingNo, pricingTicketing, ticketingQueueNo);

                                                if (isSecondSuccessQueue)
                                                {
                                                    fltBooking.BookingStatusCode = "QPL";
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        logger.Log(LogLevel.Warn, string.Format("SuperPNR No: {0}, Flight Supplier No: {1}", fltBooking.SuperPNRNo, fltBooking.SupplierBookingNo));
                                    }
                                }
                                await db.SaveChangesAsync();
                            }

                            // Temp solution here, suspose need to return to one route intelligent display.
                            if (returnURL == "&minipayment=true")
                            {
                                return RedirectToAction("AddOnRegister", "AddOnMiniScreen", new { tripid });
                            }
                            else
                            {
                                if (hasEventProduct)
                                {
                                    if (superPNR.EventBookings.Any(x => x.EventProduct.EventDetail.EventID == 156))
                                    {
                                        var cookiesForJokerXue = new HttpCookie("jokerxue")
                                        {
                                            Expires = DateTime.Now.AddMinutes(5),
                                            Value = "1"
                                        };
                                        Response.SetCookie(cookiesForJokerXue);
                                    }
                                }
                                
                                if (hasCarRental || hasTourBooking || isFixedPackage || (hasEventProduct && !hasFlightBooking && !hasHotelBooking))
                                {
                                    return RedirectToAction("Confirmation", "Checkout", new { tripid, confirmid = superPNR.SuperPNRNo, status = bookStatus.ToLower(), isRegister });
                                }
                                else if (hasFlightBooking || hasInsuranceBooking)
                                {
                                    return RedirectToAction("OrderHistory", "Flight", new { tripid, bookingID = superPNR.SuperPNRNo, status = bookStatus.ToLower(), isRegister });
                                }
                                else if (hasHotelBooking)
                                {
                                    return RedirectToAction("confirmation", "checkout", new { tripid, confirmid = superPNR.SuperPNRNo, status = bookStatus.ToLower(), isRegister });
                                }
                            }
                        }
                        else if (paymentStatus == "VOID" && bookStatus == "EXP")
                        {
                            if (returnURL != null)
                            {
                                returnURL += "&errorCode=pv";
                                return Redirect(returnURL);
                            }
                            return RedirectToAction("Payment", "Checkout", new { tripid, status = "payment-void" });
                        }
                        else if (paymentStatus == "FAIL" && bookStatus == "EXP" && fromRepay)
                        {
                            if (returnURL != null)
                            {
                                returnURL += "&errorCode=pf";
                                return Redirect(returnURL);
                            }
                            return RedirectToAction("upcomingtrips", "account", new { status = "failed" });
                        }
                    }
                }
                else
                {
                    // Here use for iPay88 postback respond payment fail only.
                    // Update Payment/Booking Status Fail Here

                    // 2018/03/26 - Handle repay usage.
                    if (superPNR.SuperPNROrders.Any(x => x.BookingStatusCode == "HTP"))
                    {
                        UpdateSuperPNROrders(ref superPNR, "HTP", "FAIL", _thisTransPaymentOrderID, null, true);
                        await db.SaveChangesAsync();

                        if (hasFlightBooking)
                        {
                            return RedirectToAction("OrderHistory", "Flight", new { tripid, bookingID = superPNR.SuperPNRNo, status = "repay-fail" });
                        }
                        else if (hasHotelBooking)
                        {
                            return RedirectToAction("OrderHistory", "Hotel", new { tripid, confirmid = superPNR.SuperPNRNo, status = "repay-fail" });
                        }
                    }
                    else
                    {
                        if (hasEventProduct)
                        {
                            HotelServiceController.UpdateEventBooking(superPNR, "EXP");
                            //set the event reserve code back to false
                            if (eventRCode != null)
                            {
                                HotelServiceController.UpdateReserveEventSet(eventRCode, false, superPNR.SuperPNRNo, null, true);
                            }
                        }

                        string _PaymentMethodCode = "";
                        foreach (var paymentRecord in superPNR.SuperPNROrders.SelectMany(x => x.PaymentOrders)
                            .Where(x => x.PaymentMethodCode.ToLower() != "sc" && _thisTransPaymentOrderID.Any(a => a == x.OrderID)))
                        {
                            // loop out current payment record payment method.
                            _PaymentMethodCode = paymentRecord.PaymentMethodCode;
                        }

                        UpdateSuperPNRAllStatus(ref superPNR, bookStatus, paymentStatus, _thisTransPaymentOrderID, _thisTransOrderID, _PaymentMethodCode.StartsWith("ipa") ? iPayTransactionID : adyenPspReference);

                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ee)
            {
                #region Handle some unexpected data length which might cause save db fail.
                var serializeSetting = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };

                try
                {
                    string statusMsg = JsonConvert.SerializeObject(reserveStatus, Formatting.None, serializeSetting);
                    string entryErrorMsg = JsonConvert.SerializeObject(ee.EntityValidationErrors.SelectMany(a => a.ValidationErrors), Formatting.None, serializeSetting);
                    List<string> newValue = new List<string>();
                    bool anySuccessBooked = reserveStatus.Any(a => a.Value == ProductReserve.BookResultType.AllSuccess || a.Value == ProductReserve.BookResultType.PartialSuccess);

                    foreach (var item in ee.EntityValidationErrors)
                    {
                        foreach (var dbEntryError in item.ValidationErrors)
                        {
                            var usedValue = item.Entry.CurrentValues.GetValue<object>(dbEntryError.PropertyName)?.ToString();
                            newValue.Add($"{dbEntryError.PropertyName}:  {usedValue}");

                            if (anySuccessBooked)
                            {
                                // revert back to original value for save booked record.
                                var _property = item.Entry.Property(dbEntryError.PropertyName);
                                var _oriValue = item.Entry.OriginalValues.GetValue<object>(dbEntryError.PropertyName);
                                _property.CurrentValue = _oriValue;

                                item.Entry.CurrentValues.SetValues(_property);
                            }
                        }
                    }

                    logger.Log(LogLevel.Fatal, ee.GetBaseException(), "[EntityException] Error on CheckoutController.cs when attemp to update status. "
                    + ("Payment Info: " + decToken) + Environment.NewLine + Environment.NewLine
                    + Environment.NewLine + Environment.NewLine + statusMsg
                    + Environment.NewLine + Environment.NewLine + entryErrorMsg
                    + string.Join(Environment.NewLine, newValue));

                    foreach (var payment in db.PaymentOrders.Where(x => _thisTransPaymentOrderID.Any(a => a == x.PaymentID)))
                    {
                        payment.SuperPNROrder.BookingStatusCode = "RHI";
                        payment.RequeryStatusCode = "MAN";
                    }
                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Debug, ex);
                }
                finally
                {
                    await db.SaveChangesAsync();
                }
                #endregion

                errorDuringUpdate = true;
            }
            catch (AggregateException ae)
            {
                string allExcptMsg = "";
                foreach (var item in ae.InnerExceptions)
                {
                    allExcptMsg += item.ToString() + Environment.NewLine + Environment.NewLine;
                }
                logger.Warn(ae.GetBaseException(), "Unable to place booking. - " + DateTime.Today.ToLoggerDateTime()
                    + Environment.NewLine + Environment.NewLine + allExcptMsg);
                errorDuringUpdate = true;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Unable to place booking. - " + DateTime.Today.ToLoggerDateTime());
                errorDuringUpdate = true;
            }

            if (errorDuringUpdate)
            {
                /* Check gateway payment respond, in case db connection suddenly down but payment success. 
                 * Notice user wait for 15 mins for scheduler rebook. */
                if (!(isPaymentError || isRequestError))
                {
                    decimal _amtReceived = iPayResponseAmount > 0 ? adyenResponseAmount : iPayResponseAmount;
                    string _msgToPush = "There have some unexpected error during placing booking, please try again later." + "<br>" +
                        $"We had captured payment - <b>{_amtReceived.ToString("n2")}</b> from payment gateway." + "<br>" +
                        $"Please check your booking list after 15 minutes. <br>" +
                        "Store and wallet credit will hold while waiting.";

                    #region Exception Email Handle Section
                    try
                    {
                        string mailToSend = Core.IsForStaging ? Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailStaging") : Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailLive");

                        string emailSubject = "Mayflower TODO Action - " + DateTime.Now.ToLoggerDateTime();
                        string reserveResultJSON = JsonConvert.SerializeObject(reserveStatus, Formatting.Indented);

                        StringBuilder plainTextContent = new StringBuilder();
                        plainTextContent.AppendLine("Dear Support,").AppendLine();
                        plainTextContent.Append("There are errors while customer attemp to reserve the booking.");
                        plainTextContent.AppendLine().AppendLine("Additional Info:");
                        plainTextContent.AppendLine(string.Join(", " + Environment.NewLine, orderInfo));
                        plainTextContent.AppendLine(reserveResultJSON).AppendLine();

                        CommonServiceController.SendEmail(mailToSend.Trim(), emailSubject, plainTextContent.ToString(), null, false);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                    }
                    #endregion

                    return Interruption(_msgToPush);
                }
            }

            if (isRequestError)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("Technical issues, please check with your merchant and developer.");
                sb.AppendLine(responseModel.TechErrDesc);
                //sb.AppendLine(responseModel.ErrDesc);
                sb.AppendLine();

                logger.Error(new Exception("Payment Checkout technical error."), sb.ToString());
            }

            PaymentEndpoint:
            if (returnURL != null)
            {
                returnURL += "&errorCode=pcfail";
                return Redirect(returnURL);
            }
            urlPaymentStatus = urlPaymentStatus == "success" && bookStatus == "EXP" ? "fail" : urlPaymentStatus;
            return RedirectToAction("Payment", "Checkout", new { tripid, status = urlPaymentStatus });
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult UpdatePayment(string method, string tripid, bool useCredit = false, bool useCashCredit = false)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            bool isAgent = IsAgentUser;
            decimal creditUserAvail = 0m;
            decimal cashCreditBalance = 0m;

            if (User.Identity.IsAuthenticated)
            {
                SqlCommand _sqlCommand = new SqlCommand();
                creditUserAvail = MemberServiceController.ChkCreditAmtRemain.ByUserId(CurrentUserID, CustomPrincipal.UserTypeCode, _sqlCommand);
                cashCreditBalance = MemberServiceController.GetUserCashCredit(CurrentUserID, _sqlCommand);
                _sqlCommand.Connection.Close();
            }

            // Update Payment Details Information
            if (checkout.PaymentDetails == null)
            {
                checkout.PaymentDetails = new PaymentCheckout()
                {
                    PaymentMethod = "IPAFPX",
                };
            }
            checkout.PaymentDetails.AvailableCredit = creditUserAvail;
            checkout.PaymentDetails.UseCredit = useCredit;
            checkout.PaymentDetails.CreditUsed = 0;
            checkout.PaymentDetails.PaymentMethod = method;
            checkout.PaymentDetails.EWallet = new EWallet
            {
                BalanceAmt = cashCreditBalance,
                UseAmt = 0,
                UseWallet = useCashCredit,
            };

            if (Core.IsEnablePackageDiscount && checkout.IsDynamic)
            {
                if (checkout.TotalPrdDisc == null)
                {
                    checkout.TotalPrdDisc = new List<ProductPricingDetail>();
                    checkout.TotalPrdDisc.Add(new ProductPricingDetail()
                    {
                        Discounts = new List<DiscountDetail>()
                    });
                }
                else
                {
                    checkout.TotalPrdDisc.FirstOrDefault().Discounts.RemoveAll(x => x.DiscType == DiscountType.TW || x.DiscType == DiscountType.TC);
                }
            }
            foreach (var product in checkout.Products)
            {
                product.PricingDetail.Discounts.RemoveAll(x => x.DiscType == DiscountType.TC || x.DiscType == DiscountType.TW);
            }

            bool isSingleDiscount = false;
            if ((!checkout.PromoCodeFunctions.GetFrontendFunction.WaiveCreditCardFee) // Special promo code, must TRUE
                && (!checkout.PromoCodeFunctions.GetFrontendFunction.AllowWithTC && useCredit)
                && (checkout.PromoID != 0 || checkout.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.CODE)))
            {
                //Block travel credit when having promo code
                isSingleDiscount = true;
                checkout.PaymentDetails.UseCredit = false;
            }
            else
            {
                /* rule for travel wallet calculation(confirmed by MH side)
                 * 1. deduct travel wallet (100 % utilization)
                 * 2. deduct travel credit(20 % of total remainder after 1)
                 * 3. deduct FPX/CC (grand total remainder after 1 and 2)
                */
                List<CreditTypes> creditTypeList = new List<CreditTypes>();
                creditTypeList.Add(new CreditTypes { CreditType = DiscountType.TW, CreditUsed = useCashCredit });
                creditTypeList.Add(new CreditTypes { CreditType = DiscountType.TC, CreditUsed = useCredit });

                foreach (var creditType in creditTypeList.Where(x => x.CreditUsed))
                {
                    if (CurrentUserID != 0 && creditType.CreditUsed)
                    {
                        bool TCIncAddon = IsAgentUser || Core.IsForStaging;
                        // check is booking able to full payment and change payment method to sc
                        decimal totalAmtAllowToCalcUseableTC = checkout.TourPackage != null ? checkout.CheckOutSummary.DepositAmt + checkout.CheckOutSummary.TTlDisc_Amt : (checkout.CheckOutSummary.SubTtl + checkout.CheckOutSummary.TTlDisc_Amt)
                            - (TCIncAddon ? 0m : checkout.CheckOutSummary.TtlAddOnAmount);

                        if (creditType.CreditType == DiscountType.TW)
                        {
                            // add back deducted add on amount
                            if (checkout.CheckOutSummary.TtlAddOnAmount > cashCreditBalance)
                            {
                                totalAmtAllowToCalcUseableTC += cashCreditBalance;
                            }
                            else
                            {
                                totalAmtAllowToCalcUseableTC += (TCIncAddon ? 0m : checkout.CheckOutSummary.TtlAddOnAmount);
                            }
                        }

                        decimal thisBookUsableAmt = CalcTravelCreditUsable(totalAmtAllowToCalcUseableTC, creditUserAvail, cashCreditBalance, creditType.CreditType, checkout.MainProductType.ToString());

                        if (thisBookUsableAmt > 0)
                        {
                            if (creditUserAvail >= thisBookUsableAmt && creditType.CreditUsed && ((checkout.TourPackage != null ? checkout.CheckOutSummary.DepositAmt : (checkout.CheckOutSummary.SubTtl + checkout.CheckOutSummary.TTlDisc_Amt)) <= thisBookUsableAmt))
                            {
                                checkout.PaymentDetails.PaymentMethod = creditType.CreditType == DiscountType.TC || creditType.CreditType == DiscountType.AC ?
                                                                            (isAgent ? "ac" : "sc") : "tw";
                            }
                            thisBookUsableAmt = checkout.TourPackage != null && thisBookUsableAmt > checkout.CheckOutSummary.DepositAmt ? checkout.CheckOutSummary.DepositAmt : thisBookUsableAmt;
                            var tcDiscountDetail = new DiscountDetail
                            {
                                Seq = 1,
                                DiscType = creditType.CreditType,
                                Disc_Desc = creditType.CreditType.ToDescription(),
                                Disc_Amt = thisBookUsableAmt
                            };

                            if (Core.IsEnablePackageDiscount && checkout.IsDynamic)
                            {
                                checkout.TotalPrdDisc.FirstOrDefault().DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkout.Flight != null) // flight only
                            {
                                tcDiscountDetail.PrdType = ProductTypes.Flight;
                                checkout.Flight.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkout.Flight == null && checkout.Hotel != null) // hotel only
                            {
                                tcDiscountDetail.PrdType = ProductTypes.Hotel;
                                checkout.Hotel.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkout.TourPackage != null) // hotel only
                            {
                                tcDiscountDetail.PrdType = ProductTypes.TP;
                                checkout.TourPackage.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkout.AddOnProduct != null)
                            {
                                tcDiscountDetail.PrdType = ProductTypes.AddOnProducts;
                                checkout.AddOnProduct.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkout.CarRental != null)
                            {
                                tcDiscountDetail.PrdType = ProductTypes.CR;
                                checkout.CarRental.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }

                            if (creditType.CreditType == DiscountType.TC)
                            {
                                // Update Travel Credit Used
                                checkout.PaymentDetails.AvailableCredit -= Math.Abs(tcDiscountDetail.Disc_Amt);
                                checkout.PaymentDetails.CreditUsed = Math.Abs(tcDiscountDetail.Disc_Amt);
                            }
                            else
                            {
                                // Update Travel Wallet Used
                                checkout.PaymentDetails.EWallet.BalanceAmt -= Math.Abs(tcDiscountDetail.Disc_Amt);
                                checkout.PaymentDetails.EWallet.UseAmt = Math.Abs(tcDiscountDetail.Disc_Amt);
                            }
                        }
                    }
                }
            }

            string _returnMsg = isSingleDiscount && useCredit ? "Unable use travel credit with promo code"
                      : (useCredit && CurrentUserID == 0 ? "Please relogin your account." : null);
            var obj = GetPaymentDetailsJson(checkout, null, _returnMsg);

            return Content(JsonConvert.SerializeObject(obj), "application/json");
        }

        public decimal CalcTravelCreditUsable(decimal bookingAmount, decimal availableCredit, decimal cashCreditBalance, DiscountType DiscType, string BookingFlow = "", bool fullCreditPay = false)
        {
            decimal creditUse = 0m;
            decimal maxCapPercentage = 0m;
            bool isAgent = IsAgentUser;

            if (isAgent)
            {
                string tcPercentage = fullCreditPay ? "100" : Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("AgentTCPaymentPercentage");
                decimal.TryParse(tcPercentage, out maxCapPercentage);
            }
            else
            {
                string tcPercentage = fullCreditPay ? "100" : (BookingFlow == "Flight" ? Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("FlightTCPaymentPercentage") : (BookingFlow == "Hotel" ? Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("HotelTCPaymentPercentage") : Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("PackageTCPaymentPercentage")));
                decimal.TryParse(tcPercentage, out maxCapPercentage);
            }
            if (DiscType == DiscountType.TW)
            {
                decimal payWithCashCredit = bookingAmount >= cashCreditBalance ? cashCreditBalance : bookingAmount;
                creditUse = payWithCashCredit;
                availableCredit = availableCredit - payWithCashCredit;
            }
            else
            {
                // calculation here
                decimal payWithCredit = (bookingAmount * (maxCapPercentage / 100)).RoundToDecimalPlace();
                bool enoughTC = payWithCredit <= availableCredit;

                if (enoughTC)
                {
                    creditUse = payWithCredit;
                    availableCredit = availableCredit - payWithCredit;
                }
                else
                {
                    creditUse = availableCredit;
                    availableCredit = 0;
                }
            }


            return creditUse;
        }

        public DiscountPayMethodExcludeList HotelCheckDiscountMethodExclude(string HotelID, DateTime travelFrom, DateTime travelTo,
            MayFlower db = null)
        {
            bool isInsideOpen = db == null;

            db = db ?? new MayFlower();
            DiscountPayMethodExcludeList list = new DiscountPayMethodExcludeList();

            var result = db.DiscountPayMethodExcludeLists.Where(x => x.isActive == true && x.HotelID == HotelID && x.StartDate < DateTime.Now && x.EndDate > DateTime.Now && ((x.TravelFrom <= travelFrom && x.TravelTo >= travelFrom) || (x.TravelFrom <= travelTo && x.TravelTo >= travelTo))); //update to block the discount payment method if one of the date is between
            if (result.Count() > 0)
            {
                list.NotAllowTC = result.Any(x => x.NotAllowTC == true);
                list.NotAllowTW = result.Any(x => x.NotAllowTW == true);
                list.NotAllowPromoCode = result.Any(x => x.NotAllowPromoCode == true);
            }

            if (isInsideOpen)
            {
                db?.Dispose();
            }

            return list;
        }

        public dynamic ApplyInstantDisc(CheckoutProduct checkoutProduct, dynamic obj, decimal discAmt = 0)
        {
            IList<ICheckoutProduct> allPrd = checkoutProduct.Products.OrderBy(x => x.ProductSeq).ToList();

            var TtlBaseRate_SellingPrice = allPrd.Sum(x => x.PricingDetail.ProductTotalAmount);
            var TtlBaseRate_CostPrice = allPrd.Sum(x => x.PricingDetail.ProductTtlAmount_SupplierSource);
            int noPax = checkoutProduct.Flight.SearchFlightInfo.Adults + checkoutProduct.Flight.SearchFlightInfo.Childrens + checkoutProduct.Flight.SearchFlightInfo.Infants;
            decimal minMargin = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings.Get("MinimumMargin"));
            decimal ttlMargin = TtlBaseRate_SellingPrice - TtlBaseRate_CostPrice + discAmt;
            var MarginPricingType = System.Configuration.ConfigurationManager.AppSettings.Get("MarginPricingType");
            if (MarginPricingType == "PCT")
            {
                //decimal ttlMarginbeforedisc = TtlBaseRate_SellingPrice - TtlBaseRate_CostPrice;
                minMargin = TtlBaseRate_SellingPrice * minMargin / 100;
            }
            else
            {
                minMargin = minMargin * noPax;
            }
            if (ttlMargin > minMargin)
            {
                discAmt = -Math.Abs(ttlMargin - minMargin);
                if (discAmt != 0)
                {
                    DiscountDetail discDtl = new DiscountDetail();
                    if (!obj.valid || checkoutProduct.TotalPrdDisc == null)
                    {
                        checkoutProduct.TotalPrdDisc = new List<ProductPricingDetail>();
                        checkoutProduct.TotalPrdDisc.Add(new ProductPricingDetail()
                        {
                            Discounts = new List<DiscountDetail>()
                        });
                    }

                    checkoutProduct.TotalPrdDisc.FirstOrDefault().Discounts.RemoveAll(x => x.DiscType == DiscountType.PD);
                    checkoutProduct.TotalPrdDisc.FirstOrDefault().DiscountInsert(new DiscountDetail()
                    {
                        DiscType = DiscountType.PD,
                        Disc_Amt = discAmt.RoundToDecimalPlace(),
                        Disc_Desc = "Instant Discount",
                        PrdType = ProductTypes.Undefined,
                        Seq = 1
                    });

                    if (checkoutProduct.TotalPrdDisc.FirstOrDefault().Discounts.Count > 1)
                    {
                        obj.desc2 = "Instant Discount";
                        obj.amount2 = discAmt;
                    }
                    else
                    {
                        discDtl = checkoutProduct.TotalPrdDisc.FirstOrDefault().Discounts.FirstOrDefault(x => x.DiscType == DiscountType.PD);

                        obj.desc = "Instant Discount";
                        obj.code = "Instant Discount";
                        obj.cur = checkoutProduct.CheckOutSummary.CurrencyCode;
                        obj.amount = discDtl.Disc_Amt;
                    }
                }
            }
            return obj;
        }

        public ActionResult ApplyPCode(string c, string tripid, string token, string ccn = null, int step = 0)
        {
            MayFlower dbCtx = new MayFlower();
            CheckoutProduct checkoutProduct = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            if (checkoutProduct != null)
            {
                IList<ICheckoutProduct> allPrd = checkoutProduct.Products.OrderBy(x => x.ProductSeq).ToList();
                ICheckoutProduct mainPrd = checkoutProduct.Products.OrderBy(x => x.ProductSeq).FirstOrDefault();
                dynamic obj = new System.Dynamic.ExpandoObject();
                PromoCodeRule promoCode = null;

                if (c != null)
                {
                    foreach (var prd in allPrd)
                    {
                        mainPrd = prd;
                        switch (prd.ProductType)
                        {
                            case ProductTypes.Flight:
                                FlightController fc = new FlightController(this.ControllerContext);
                                checkoutProduct.Flight.SearchFlightInfo.PromoCode = c;
                                var AirlineCodeList = checkoutProduct.Flight.FlightInfo.FlightDetailInfo.Airline ?? new List<string>();
                                promoCode = fc.GetPromoCodeDiscountRule(checkoutProduct.Flight.SearchFlightInfo, dbCtx,null,null, AirlineCodeList);
                                break;
                            case ProductTypes.Hotel:
                                HotelController hc = new HotelController(this.ControllerContext);
                                checkoutProduct.Hotel.SearchHotelInfo.PromoCode = c;
                                var triptype = checkoutProduct.IsFixedPrice ? checkoutProduct.Flight.SearchFlightInfo.TripType : null;
                                var airlinecode = checkoutProduct.IsFixedPrice ? checkoutProduct.Flight.FlightInfo.FlightDetailInfo.Airline : null;
                                promoCode = hc.GetPromoCodeDiscountRule(checkoutProduct.Hotel.SearchHotelInfo, null, dbCtx, null, checkoutProduct.Hotel.HotelSelected.FirstOrDefault().hotelId, checkoutProduct.Hotel.HotelSelected.FirstOrDefault().hotelSupplierCode, airlinecode, triptype);
                                break;
                            case ProductTypes.Insurance:
                                break;
                            default:
                                break;
                        }
                        if (promoCode != null)
                            break;
                    }
                }

                PromoCodeFunctions _thisApplyCodeFunc = new PromoCodeFunctions();

                if (promoCode != null)
                    _thisApplyCodeFunc = new PromoCodeFunctions(promoCode.PromoID, dbCtx);

                bool isSingleDiscount = (checkoutProduct.PaymentDetails != null && checkoutProduct.PaymentDetails.CreditUsed > 0 && !_thisApplyCodeFunc.GetFrontendFunction.AllowWithTC)
                    || (checkoutProduct.PromoID == 0 && checkoutProduct.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.CODE));

                if (isSingleDiscount)
                {
                    promoCode = null;
                }

                string overrideMsg = null; // need declare before Reset Area, prevent message override
                ResetArea:
                obj.valid = promoCode != null;

                if (promoCode == null)
                {
                    obj.msg = isSingleDiscount ? "Unable apply this promo code with travel credit." : (overrideMsg ?? "Invalid promo code.");
                    checkoutProduct.PromoID = 0; // Ensure no promo id inserted if promo code invalid.

                    if (checkoutProduct.Flight != null && checkoutProduct.Flight.SearchFlightInfo != null)
                    {
                        checkoutProduct.Flight.SearchFlightInfo.PromoCode = null;
                    }
                    if (checkoutProduct.Hotel != null && checkoutProduct.Hotel.SearchHotelInfo != null)
                    {
                        checkoutProduct.Hotel.SearchHotelInfo.PromoCode = null;
                    }
                    if (checkoutProduct.IsFixedPrice && checkoutProduct.TotalPrdDisc != null)
                    {
                        checkoutProduct.TotalPrdDisc.FirstOrDefault().Discounts.RemoveAll(x => x.DiscType == DiscountType.CODE);
                    }
                    if (Core.IsEnablePackageDiscount && checkoutProduct.IsDynamic)
                    {
                        obj = ApplyInstantDisc(checkoutProduct, obj);
                    }
                }
                else
                {
                    checkoutProduct.PromoID = promoCode.PromoID;

                    foreach (var item in checkoutProduct.Products)
                    {
                        item.PricingDetail.Discounts.RemoveAll(x => x.DiscType == DiscountType.CODE ||
                        (!checkoutProduct.PromoCodeFunctions.GetFrontendFunction.AllowWithTC && x.DiscType == DiscountType.TC));
                    }

                    // Check promo function here
                    if ((promoCode.IsPackageOnly || checkoutProduct.PromoCodeFunctions.GetFrontendFunction.PackageAutoAppliedHotel) && !checkoutProduct.IsDynamic)
                    {
                        promoCode = null;
                        goto ResetArea;
                    }

                    if (checkoutProduct.PromoCodeFunctions.GetFrontendFunction.PromoCodeDisableManualApply)
                    {
                        promoCode = null;
                        overrideMsg = "This Promo Code can use on specificied page only.";
                        goto ResetArea;
                    }

                    if (checkoutProduct.PromoCodeFunctions.GetFrontendFunction.WaiveCreditCardFee)
                    {
                        string _reffCode = checkoutProduct.ReferralCode?.ToLower();
                        bool validAffiliate = dbCtx.Affiliations.Any(x => x.UserCode.ToLower() == _reffCode);

                        if (!validAffiliate)
                        {
                            promoCode = null;
                            goto ResetArea;
                        }
                    }

                    if (step == 4 && promoCode.PromoBankBinRanges.Count > 0)
                    {
                        if (string.IsNullOrWhiteSpace(ccn))
                        {
                            promoCode = null;
                            overrideMsg = "This Promo Code must use with Card Payment Method.";
                            goto ResetArea;
                        }
                        else
                        {
                            bool isValidBIN = IsValidCardForPromo(promoCode, ccn, ref overrideMsg);

                            if (!isValidBIN)
                            {
                                promoCode = null;
                                //overrideMsg = "Invalid Bank Card used.";
                                goto ResetArea;
                            }
                            else
                            {
                                if (checkoutProduct.PaymentDetails.PaymentMethod?.ToLower() != "adyenc")
                                {
                                    obj.promptMsg = "This Promo Code must use with Card Payment Method. " +
                                        "Please change you payment method in order to proceed payment.";
                                }
                            }
                        }
                    }

                    var pricingDetail = mainPrd.PricingDetail;
                    decimal discAmt = 0m;

                    if (Core.IsEnablePackageDiscount && checkoutProduct.IsDynamic)
                    {
                        checkoutProduct.DPPromoCode = promoCode.PromoCode;
                        var TtlBaseRate_BeforeDisc = promoCode.FlightPromo && promoCode.HotelPromo ? allPrd.Where(s => s.ProductType == ProductTypes.Flight || s.ProductType == ProductTypes.Hotel).Sum(x => x.PricingDetail.BaseRate_BeforeDisc_Discountable)
                            : promoCode.FlightPromo ? allPrd.Where(s => s.ProductType == ProductTypes.Flight).Sum(x => x.PricingDetail.BaseRate_BeforeDisc_Discountable)
                            : promoCode.HotelPromo ? allPrd.Where(s => s.ProductType == ProductTypes.Hotel).Sum(x => x.PricingDetail.BaseRate_BeforeDisc_Discountable)
                            : 0m;

                        discAmt = promoCode.PricingTypeCode == "PCT"
                                                    ? TtlBaseRate_BeforeDisc * (promoCode.DiscountAmtOrPCT / 100)
                                                    : promoCode.DiscountAmtOrPCT;
                        discAmt = -Math.Abs(discAmt > promoCode.MaxDiscountAmt ? promoCode.MaxDiscountAmt : discAmt);
                    }
                    else
                    {
                        switch (mainPrd.ProductType)
                        {
                            case ProductTypes.Flight:
                                bool isPromoFlightSupplier = isPromoFlightList(promoCode, checkoutProduct.Flight.SearchFlightInfo, checkoutProduct.Flight.FlightInfo.Supplier);
                                discAmt = isPromoFlightSupplier ? (promoCode.PricingTypeCode == "PCT"
                                                    ? pricingDetail.BaseRate_BeforeDisc_Discountable * (promoCode.DiscountAmtOrPCT / 100)
                                                    : promoCode.DiscountAmtOrPCT) : 0m;
                                discAmt = -Math.Abs(discAmt > promoCode.MaxDiscountAmt ? promoCode.MaxDiscountAmt : discAmt);
                                break;
                            case ProductTypes.Hotel:
                                var isPromoSpecificHotel = isPromoHotelList(promoCode, checkoutProduct.Hotel);
                                HotelServiceController.ProcessDiscountCalculation(checkoutProduct.Hotel.SearchHotelInfo.Result.HotelList, promoCode, checkoutProduct.Hotel.SearchHotelInfo.Destination, 1, checkoutProduct.Hotel.SearchHotelInfo);

                                if (!isPromoSpecificHotel)
                                {
                                    promoCode = null;
                                    overrideMsg = "This Promo Code cannot use on this Destination/Hotel.";
                                    goto ResetArea;
                                }

                                discAmt = isPromoSpecificHotel ? PaymentServiceController.CalcPromoDiscAmount(promoCode.PromoID, checkoutProduct.Hotel.RoomDetails.Sum(s => s.TotalBaseRate), checkoutProduct.Hotel.SearchHotelInfo.TotalStayDays) : 0m;
                                break;
                            case ProductTypes.Insurance:
                                break;
                            case ProductTypes.AddOnProducts:
                                break;
                            default:
                                break;
                        }
                    }

                    var tcAppllied = new DiscountDetail();
                    DiscountDetail discDtl = new DiscountDetail();
                    if (Core.IsEnablePackageDiscount && checkoutProduct.IsDynamic)
                    {
                        if (checkoutProduct.TotalPrdDisc == null)
                        {
                            checkoutProduct.TotalPrdDisc = new List<ProductPricingDetail>();
                            checkoutProduct.TotalPrdDisc.Add(new ProductPricingDetail()
                            {
                                Discounts = new List<DiscountDetail>()
                            });
                        }
                        else
                        {
                            checkoutProduct.TotalPrdDisc.FirstOrDefault().Discounts.RemoveAll(x => x.DiscType == DiscountType.CODE);
                        }
                        checkoutProduct.TotalPrdDisc.FirstOrDefault().DiscountInsert(new DiscountDetail()
                        {
                            DiscType = DiscountType.CODE,
                            Disc_Amt = discAmt.RoundToDecimalPlace(),
                            Disc_Desc = promoCode.PromoCode,
                            PrdType = ProductTypes.Undefined,
                            Seq = 1
                        });
                        discDtl = checkoutProduct.TotalPrdDisc.FirstOrDefault().Discounts.FirstOrDefault(x => x.DiscType == DiscountType.CODE);
                        if (Core.IsEnablePackageDiscount)
                        {
                            obj = ApplyInstantDisc(checkoutProduct, obj, discAmt);
                        }
                    }
                    else
                    {
                        discDtl = new DiscountDetail()
                        {
                            DiscType = DiscountType.CODE,
                            Disc_Amt = discAmt.RoundToDecimalPlace(),
                            Disc_Desc = promoCode.PromoCode,
                            PrdType = mainPrd.ProductType,
                            Seq = 1
                        };

                        mainPrd.PricingDetail.DiscountInsert(discDtl);
                        tcAppllied = mainPrd.PricingDetail.Discounts.FirstOrDefault(x => x.DiscType == DiscountType.TC);
                    }

                    string _paymentMethod = checkoutProduct.CheckOutSummary.GrandTtlAmt <= 0 ? null : checkoutProduct.CheckOutSummary.PaymentMethod;

                    UpdatePayment(_paymentMethod, tripid, checkoutProduct.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.TC && x.Disc_Amt != 0),
                        checkoutProduct.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.TW && x.Disc_Amt != 0));
                    obj = GetPaymentDetailsJson(checkoutProduct, obj, null);

                    obj.desc = $"Promo Code ({discDtl.Disc_Desc}{(promoCode?.DisplayPromoName == null ? null : $" - {promoCode.DisplayPromoName}")})" + @" <a id='promo-remove' href='javascript:;'>Remove</a>";
                    obj.code = mainPrd.ProductType + " - " + promoCode.PromoCode;
                    obj.awc = checkoutProduct.PromoCodeFunctions.GetFrontendFunction.AllowWithTC;
                    obj.amount = discDtl.Disc_Amt;
                }

                return Content(JsonConvert.SerializeObject(obj), "application/json");
            }

            //Return Session Time Out
            return RedirectToAction("Type", "Error", new { id = "session-error" });
        }

        public ActionResult RemovePCode(string tripid, string token)
        {
            CheckoutProduct checkoutProduct = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            if (checkoutProduct != null)
            {
                dynamic obj = new System.Dynamic.ExpandoObject();
                ProductPricingDetail pricingDetail = checkoutProduct.Products.OrderBy(x => x.ProductSeq).FirstOrDefault().PricingDetail;
                pricingDetail.DiscountRemove(pricingDetail.Discounts.FirstOrDefault(x => x.DiscType == DiscountType.CODE));

                checkoutProduct.PromoID = 0;
                checkoutProduct.DPPromoCode = null;

                #region Reset Promo Code in Search Form
                if (checkoutProduct.Flight != null && checkoutProduct.Flight.SearchFlightInfo != null)
                {
                    checkoutProduct.Flight.SearchFlightInfo.PromoCode = null;
                }
                if (checkoutProduct.Hotel != null && checkoutProduct.Hotel.SearchHotelInfo != null)
                {
                    checkoutProduct.Hotel.SearchHotelInfo.PromoCode = null;
                }
                #endregion
                if (checkoutProduct.IsDynamic)
                {
                    checkoutProduct.TotalPrdDisc = null;
                }

                UpdatePayment(checkoutProduct.PaymentDetails.PaymentMethod, tripid, checkoutProduct.PaymentDetails.UseCredit, checkoutProduct.PaymentDetails.EWallet.UseWallet);

                // Reset step 2 display discount amount
                if (checkoutProduct.Hotel != null)
                {
                    var hotelProduct = checkoutProduct.Hotel;
                    foreach (var item in hotelProduct.SearchHotelInfo.Result.HotelList)
                    {
                        item.highRate = item.highRate_Source;
                        item.lowRate = item.lowRate_Source;

                        foreach (var room in item.RoomRateDetailsList)
                        {
                            foreach (var roomRate in room.RateInfos)
                            {
                                roomRate.chargeableRateInfo = roomRate.ChargeableRateInfo_FromService;
                            }
                        }
                    }

                    foreach (var item in hotelProduct.SearchRoomList)
                    {
                        foreach (var room in item.Result.HotelRoomInformationList)
                        {
                            foreach (var roomDetail in room.roomAvailabilityDetailsList)
                            {
                                foreach (var roomCharge in roomDetail.RateInfos)
                                {
                                    roomCharge.chargeableRateInfo = roomCharge.ChargeableRateInfo_FromService ?? roomCharge.chargeableRateInfo;
                                }
                            }
                        }
                    }
                }


                obj = GetPaymentDetailsJson(checkoutProduct, obj);

                obj.valid = true;

                if (Core.IsEnablePackageDiscount && checkoutProduct.IsDynamic)
                {
                    obj = ApplyInstantDisc(checkoutProduct, obj);
                }
                return Content(JsonConvert.SerializeObject(obj), "application/json");
            }

            //Return Session Time Out
            return RedirectToAction("Type", "Error", new { id = "session-error" });
        }

        private void UpdateSuperPNRAllStatus(ref SuperPNR superPNR, string bookStatus, string paymentStatus, List<int> paymentOrdersID, List<int> _thisTransOrderID, string iPayTransactionID = "")
        {
            UpdateSuperPNROrders(ref superPNR, bookStatus, paymentStatus, paymentOrdersID, iPayTransactionID);
            UpdateBookingFlights(ref superPNR, bookStatus, _thisTransOrderID);
            UpdateBookingHotels(ref superPNR, bookStatus, _thisTransOrderID);
            UpdateEventBookings(ref superPNR, bookStatus, _thisTransOrderID);
            UpdateInsuranceBookings(ref superPNR, bookStatus, _thisTransOrderID);
            UpdateCarRentalBookings(ref superPNR, bookStatus, _thisTransOrderID);
            UpdateUserCredit(ref superPNR, bookStatus, paymentStatus, paymentOrdersID);
        }

        private void UpdateSuperPNROrders(ref SuperPNR superPNR, string bookStatus, string paymentStatus, List<int> paymentOrdersID, string iPayTransactionID = "",
             bool useOriginalBookStatus = false)
        {
            foreach (var item in superPNR.SuperPNROrders)
            {

                // Update only corresponding order, avoid update not this transaction record or failed record.
                foreach (var record in item.PaymentOrders.Where(x => paymentOrdersID.Any(a => a == x.PaymentID)))
                {
                    if (!useOriginalBookStatus)
                    {
                        item.BookingStatusCode = bookStatus;
                    }

                    record.PaymentStatusCode = paymentStatus;
                    record.Ipay88TransactionID = string.IsNullOrEmpty(iPayTransactionID) ? record.Ipay88TransactionID : iPayTransactionID;
                }
            }
        }

        private void UpdateOfflinePaymentOrder(ref OfflinePayment record, string paymentStatus, List<int> paymentOrdersID, string iPayTransactionID = "")
        {
            record.PaymentStatusCode = paymentStatus;
            record.Reference = string.IsNullOrEmpty(iPayTransactionID) ? record.Reference : iPayTransactionID;
        }

        private void UpdateBookingFlights(ref SuperPNR superPNR, string status, List<int> _thisTransOrderID = null)
        {
            if (_thisTransOrderID != null)
            {
                foreach (var item in superPNR.Bookings.Where(x => _thisTransOrderID.Any(a => a == x.OrderID)))
                {
                    item.BookingStatusCode = status;
                }
            }
            else
            {
                foreach (var item in superPNR.Bookings)
                {
                    item.BookingStatusCode = status;
                }
            }
        }

        private void UpdateBookingHotels(ref SuperPNR superPNR, string status, List<int> _thisTransOrderID = null)
        {
            if (_thisTransOrderID != null)
            {
                foreach (var item in superPNR.BookingHotels.Where(x => _thisTransOrderID.Any(a => a == x.OrderID)))
                {
                    item.BookingStatusCode = status;
                }
            }
            else
            {
                foreach (var item in superPNR.BookingHotels)
                {
                    item.BookingStatusCode = status;
                }
            }
        }

        private void UpdateEventBookings(ref SuperPNR superPNR, string status, List<int> _thisTransOrderID = null)
        {
            try
            {
                if (_thisTransOrderID != null)
                {
                    foreach (var item in superPNR.EventBookings.Where(x => _thisTransOrderID.Any(a => a == x.OrderID)))
                    {
                        item.BookingStatusCode = status;
                    }
                }
                else
                {
                    foreach (var item in superPNR.EventBookings)
                    {
                        item.BookingStatusCode = status;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void UpdateInsuranceBookings(ref SuperPNR superPNR, string status, List<int> _thisTransOrderID = null)
        {
            try
            {
                if (_thisTransOrderID != null)
                {
                    foreach (var item in superPNR.BookingInsurances.Where(x => _thisTransOrderID.Any(a => a == x.OrderID)))
                    {
                        item.BookingStatusCode = status;
                    }
                }
                else
                {
                    foreach (var item in superPNR.BookingInsurances)
                    {
                        item.BookingStatusCode = status;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void UpdateCarRentalBookings(ref SuperPNR superPNR, string status, List<int> _thisTransOrderID = null)
        {
            try
            {
                if (_thisTransOrderID != null)
                {
                    foreach (var item in superPNR.CarRentalBookings.Where(x => _thisTransOrderID.Any(a => a == x.OrderID)))
                    {
                        item.BookingStatusCode = status;
                    }
                }
                else
                {
                    foreach (var item in superPNR.CarRentalBookings)
                    {
                        item.BookingStatusCode = status;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void UpdateUserCredit(ref SuperPNR superPNR, string bookStatus, string paymentStatus, List<int> paymentOrdersID, MayFlower dbCtx = null)
        {
            SqlCommand command = new SqlCommand();
            dbCtx = dbCtx == null ? new MayFlower() : dbCtx;

            try
            {
                string bookingType = superPNR.Bookings.Count > 0 ? "FLT" : "HTL";
                foreach (var item in superPNR.SuperPNROrders)
                {
                    var creditPaymentRecord = item.PaymentOrders.Where(x => paymentOrdersID.Any(a => a == x.PaymentID))?
                        .Where(x => x.PaymentMethodCode == "SC" || x.PaymentMethodCode == "AC" || x.PaymentMethodCode == "TW");

                    foreach (var creditPayment in creditPaymentRecord)
                    {
                        string payMethod = creditPayment.PaymentMethodCode;

                        if (bookStatus == "CON")
                        {
                            if (payMethod == "AC")
                            {
                                PaymentServiceController.updateAgentCreditRedeem(item.SuperPNRID, item.OrderID, item.CreatedByID, item.CurrencyCode, payMethod, creditPayment.PaymentAmount, bookingType, command);
                            }
                            else if (payMethod == "SC")
                            {
                                PaymentServiceController.ClaimUserCredit(item.CreatedByID, item.CurrencyCode, item.OrderID, creditPayment.PaymentAmount, command);
                            }
                            else if (payMethod == "TW")
                            {
                                PaymentServiceController.UpdateCashCreditRedeem(item.CreatedByID, item.CurrencyCode, item.OrderID, creditPayment.PaymentAmount, bookingType, command);
                            }
                        }

                        if (bookStatus != "RHI")
                        {
                            if (payMethod == "TW")
                                new PaymentServiceController().TempCashCreditRedeemDelete(item.OrderID, item.CreatedByID, item.CurrencyCode, creditPayment.PaymentAmount, command);
                            else
                                new PaymentServiceController().TempCreditRedeemDelete(item.OrderID, item.CreatedByID, item.CurrencyCode, creditPayment.PaymentAmount, command);
                        }
                    }
                }

                command?.Transaction?.Commit(); // no open transaction then no commit, avoid throw null reference error.
            }
            catch (Exception ex)
            {
                if (command.Transaction != null)
                {
                    command.Transaction.Rollback();
                }

                logger.Error(ex);
            }
        }

        private void UpdateStatusFromReserve(ref SuperPNR superPNR, Dictionary<string, ProductReserve.BookResultType> reserveStatus, List<int> _thisTransOrderID = null)
        {
            foreach (var book in reserveStatus)
            {
                // update successed booking status
                if (book.Value == ProductReserve.BookResultType.AllSuccess && book.Key != null && book.Key.ToLower().Contains("flight"))
                {
                    UpdateBookingFlights(ref superPNR, "CON", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllSuccess && book.Key != null && book.Key.ToLower().Contains("hotel"))
                {
                    UpdateBookingHotels(ref superPNR, "CON", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllSuccess && book.Key != null && book.Key.ToLower().Contains("event"))
                {
                    UpdateEventBookings(ref superPNR, "CON", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllSuccess && book.Key != null && book.Key.ToLower().Contains("insurance"))
                {
                    UpdateInsuranceBookings(ref superPNR, "CON", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllSuccess && book.Key != null && book.Key.ToLower().Contains("carrental"))
                {
                    UpdateCarRentalBookings(ref superPNR, "CON", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.PartialSuccess && book.Key != null && book.Key.ToLower().Contains("flight"))
                {
                    UpdateBookingFlights(ref superPNR, "RHI", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.PartialSuccess && book.Key != null && book.Key.ToLower().Contains("hotel"))
                {
                    UpdateBookingHotels(ref superPNR, "RHI", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.PartialSuccess && book.Key != null && book.Key.ToLower().Contains("event"))
                {
                    UpdateEventBookings(ref superPNR, "RHI", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.PartialSuccess && book.Key != null && book.Key.ToLower().Contains("insurance"))
                {
                    UpdateInsuranceBookings(ref superPNR, "RHI", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllFail && book.Key != null && book.Key.ToLower().Contains("flight"))
                {
                    UpdateBookingFlights(ref superPNR, "EXP", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllFail && book.Key != null && book.Key.ToLower().Contains("hotel"))
                {
                    UpdateBookingHotels(ref superPNR, "EXP", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllFail && book.Key != null && book.Key.ToLower().Contains("event"))
                {
                    UpdateEventBookings(ref superPNR, "EXP", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllFail && book.Key != null && book.Key.ToLower().Contains("insurance"))
                {
                    UpdateInsuranceBookings(ref superPNR, "EXP", _thisTransOrderID);
                }
                else if (book.Value == ProductReserve.BookResultType.AllFail && book.Key != null && book.Key.ToLower().Contains("carrental"))
                {
                    UpdateCarRentalBookings(ref superPNR, "EXP", _thisTransOrderID);
                }
            }
        }

        /// <summary>
        /// Use this when add new item into Dictionary ProductReserve.BookResultType.
        /// </summary>
        protected void RefreshStatus(ref bool finalReserveResult, ref string bookStatus, Dictionary<string, ProductReserve.BookResultType> reserveStatus)
        {
            finalReserveResult = reserveStatus.All(x => x.Value == ProductReserve.BookResultType.AllSuccess);

            if (finalReserveResult)
            {
                bookStatus = "CON";
            }
            else if (reserveStatus.All(x => x.Value == ProductReserve.BookResultType.AllFail))
            {
                bookStatus = "EXP";
            }
            else
            {
                bookStatus = "RHI";
            }
        }

        public bool IsEventInventoryEnough(ProductEventTicket productEvent, MayFlower dbContext = null)
        {
            if (productEvent == null || productEvent.TicketInfo == null)
            {
                return true;
            }

            // Wifi Need Date to return specific row data.
            var searchInfo = productEvent.SearchInfo ?? new ProductEventTicket.SearchCriteria();

            string idList = string.Join(",", productEvent.TicketInfo.Select(x => x.EventDetailsID));
            var eventProductList = eventDBFunc.GetEventProduct(idList, searchInfo.CheckInDate, searchInfo.CheckOutDate, dbContext)?.ToList();

            if (eventProductList != null && eventProductList.Count > 0)
            {
                var _result = productEvent.TicketInfo.GroupJoin(eventProductList,
                    inv => inv.EventProductID,
                    sel => sel.EventProductID,
                    (inv, result) => new
                    {
                        Ticket = inv.ItemDesc + " - " + inv.TicketCategory,
                        Enough = inv.ProductType == ProductTypes.TW ? true : inv.OrderedQty <= result.Sum(x => x.TicketBalance),
                    });

                return _result.All(x => x.Enough);
            }
            else
            {
                return false;
            }
        }

        public bool isEBReserveCodeUsed(string code)
        {
            MayFlower db = new MayFlower();

            if (code == null)
            {
                return false;
            }
            else
            {
                var reserveEB = db.ReserveEventSets.FirstOrDefault(x => x.ReserveCode == code);
                return reserveEB.IsUsed;
            }


        }

        private dynamic GetPaymentDetailsJson(CheckoutProduct checkout, dynamic obj = null, string returnMsg = null)
        {
            obj = obj ?? new System.Dynamic.ExpandoObject();

            bool useCredit = checkout.CheckOutSummary.DiscountDetails
                .Where(x => x.DiscType == DiscountType.TC && x.Disc_Amt > 0)
                .Sum(s => s.Disc_Amt) > 0;

            bool useCashCredit = checkout.CheckOutSummary.DiscountDetails
                .Where(x => x.DiscType == DiscountType.TW && x.Disc_Amt > 0)
                .Sum(s => s.Disc_Amt) > 0;

            bool isAllowWithTC = !(checkout.PromoID != 0 || checkout.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.CODE)) ||
                ((checkout.PromoID != 0 || checkout.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.CODE))
                && (checkout?.PromoCodeFunctions?.GetFrontendFunction?.AllowWithTC ?? false));
            //bool isFullCredit = checkout.TourPackage != null ? checkout.CheckOutSummary.DepositAmt - Math.Abs(checkout.CheckOutSummary.TTlDisc_Amt) == 0 : checkout.CheckOutSummary.GrandTtlAmt == 0;
            bool isFullCredit = checkout.TourPackage != null ? checkout.PaymentDetails.DepositAmt == 0 : checkout.CheckOutSummary.GrandTtlAmt == 0;

            obj.full = isFullCredit;
            obj.part = (useCredit || useCashCredit) && !isFullCredit;

            obj.cur = checkout.CheckOutSummary.CurrencyCode;
            obj.scAmt = checkout.PaymentDetails.CreditUsed.ToString("n2");
            obj.scUsed = useCredit;
            obj.cashCreditAmt = checkout.PaymentDetails.EWallet.UseAmt.ToString("n2");
            obj.cashCreditUsed = useCashCredit;

            var _castObj = (System.Dynamic.ExpandoObject)obj;

            if (!_castObj.Any(x => x.Key == "msg"))
            {
                obj.msg = returnMsg;
            }

            obj.ttlPF = checkout.CheckOutSummary.ProcessingFee.TtlAmt.ToString("n2");
            obj.ttlTX = checkout.CheckOutSummary.TtlSurchage.ToString("n2");
            obj.ttlGST = checkout.CheckOutSummary.TtlGST.ToString("n2");

            obj.nettTtl = (checkout.CheckOutSummary.GrandTtlAmt_BeforeDiscount + -Math.Abs(checkout.CheckOutSummary.DiscountDetails.GetTtlDiscAmtWithoutCredit())).ToString("n2");
            obj.grdTtl = checkout.CheckOutSummary.GrandTtlAmt.ToString("n2");
            obj.awc = !(isFullCredit && useCashCredit && !useCredit) && isAllowWithTC && !checkout.PaymentDetails.NotAllowUsingTC;
            obj.aww = (checkout?.PaymentDetails?.EWallet?.BalanceAmt > 0 || checkout?.PaymentDetails?.EWallet?.UseAmt > 0);

            if (checkout.TourPackage != null)
            {
                int paxNo = checkout.TourPackage.TourPackagesInfo.NoOfPax;
                obj.ttldeposit = checkout.CheckOutSummary.DepositAmt.ToString("n2");
                obj.depositAfterDisc = checkout.PaymentDetails.DepositAmt.ToString("n2");
                obj.nettTtlpax = ((checkout.CheckOutSummary.GrandTtlAmt_BeforeDiscount + -Math.Abs(checkout.CheckOutSummary.DiscountDetails.GetTtlDiscAmtWithoutCredit())) / paxNo).ToString("n2");
                obj.grdTtlpax = (checkout.CheckOutSummary.GrandTtlAmt / paxNo).ToString("n2");
            }
            return obj;
        }

        private bool IsValidCardForPromo(PromoCodeRule promoCodeRule, string parsedCardNo, ref string errMsg)
        {
            if (promoCodeRule == null)
            {
                errMsg = "Invalid Promo Code.";
                return false;
            }
            else if (string.IsNullOrWhiteSpace(parsedCardNo))
            {
                errMsg = "This Promo Code must use with Card Payment Method.";
                return false;
            }

            parsedCardNo = parsedCardNo ?? "";

            // uncover card no.
            parsedCardNo = parsedCardNo.Replace("Z", "0")
                    .Replace("h", "1")
                    .Replace("x", "2")
                    .Replace("Y", "3")
                    .Replace("o", "4")
                    .Replace("R", "5")
                    .Replace("w", "6")
                    .Replace("N", "7")
                    .Replace("c", "8")
                    .Replace("t", "9");

            var _trimCCN = parsedCardNo.Trim().Replace(" ", "");
            bool isValidBIN = false;
            List<string> bankCardAccepted = new List<string>();
            CreditCardValidator.CreditCardDetector creditCardDetector = new CreditCardValidator.CreditCardDetector(_trimCCN);

            foreach (var cardPromo in promoCodeRule.PromoBankBinRanges)
            {
                if ((cardPromo.CardTypeCode == "VI" && creditCardDetector.Brand == CreditCardValidator.CardIssuer.Visa)
                    || (cardPromo.CardTypeCode == "MC" && creditCardDetector.Brand == CreditCardValidator.CardIssuer.MasterCard)
                    || (cardPromo.CardTypeCode == "AX" && creditCardDetector.Brand == CreditCardValidator.CardIssuer.AmericanExpress))
                {
                    string _binFrom = cardPromo.BinRangeFrom.ToString();
                    string _binTo = cardPromo.BinRangeTo.ToString();
                    string _customerBIN = _trimCCN;

                    // Check Start BIN length same with End BIN or not, if not same then add 0
                    if (_binTo.Length < _binFrom.Length)
                    {
                        for (int i = 0; i < (_binTo.Length - _binFrom.Length) - 1; i++)
                        {
                            _binFrom += "0";
                        }
                    }

                    if (_customerBIN.Length < _binTo.Length)
                    {
                        _customerBIN += "0";
                    }
                    else if (_customerBIN.Length != _binTo.Length)
                    {
                        _customerBIN = _customerBIN.Substring(0, _binTo.Length);
                    }

                    int binFrom = _binFrom.ToInt();
                    int binTo = _binTo.ToInt();
                    int customerBin = _customerBIN.ToInt();

                    if (binFrom >= customerBin && binTo <= customerBin)
                    {
                        isValidBIN = true;
                        errMsg = null;
                        break;
                    }
                }
                else
                {
                    bankCardAccepted.Add($"{cardPromo.Bank.Bank1} - {cardPromo.CardType.CardType1}");
                    //errMsg = "Card type used not entitled for this promo code. ";
                }
            }

            if (!isValidBIN)
            {

                errMsg += $"This code only entitled for card: {string.Join(", ", bankCardAccepted)}.";
            }

            return isValidBIN;
        }

        private Dictionary<string, string> GetContactEmailAndName(SuperPNR superPnr, bool hasFlightBooking, bool hasHotelBooking)
        {            
            Dictionary<string, string> EmailAndNameList = new Dictionary<string, string>();

            string userEmail = "";
            string userName = "";
            if (hasFlightBooking)
            {                
                var flight = superPnr.Bookings.FirstOrDefault().Paxes.FirstOrDefault(x => x.IsContactPerson == true);
                if (flight != null)
                {
                    userEmail = flight.PassengerEmail;
                    userName = flight.GivenName + " " + flight.Surname;
                }
            }
            else if (hasHotelBooking)
            {                
                var hotel = superPnr.BookingHotels.FirstOrDefault().RoomPaxHotels.FirstOrDefault(x => x.IsContactPerson == true);
                if (hotel != null)
                {
                    userEmail = hotel.PassengerEmail;
                    userName = hotel.GivenName + " " + hotel.Surname;
                }
            }
            else
            {                
                var bookContact = superPnr.BookingContact;
                if (bookContact != null)
                {
                    userEmail = bookContact.Email;
                    userName = bookContact.GivenName + " " + bookContact.Surname;
                }
            }

            EmailAndNameList.Add("email", userEmail);
            EmailAndNameList.Add("name", userName);
            //add name

            return EmailAndNameList;
        }

        private void SendConfirmedQueuePlacesStatusEmail(string userEmail, string userName, MayFlower db, ProductTypes productTypes)
        {
            //var user = db.Users.FirstOrDefault(x => x.UserID == userID);
            //string fullName = user.UserDetails.FirstOrDefault().JoinFirstLastName;

            Hashtable ht = new Hashtable();
            //ht.Add("<#UserName>", fullName);
            ht.Add("<#UserName>", userName);

            string templateCode = "manualconfirmetc";
            if (productTypes == ProductTypes.Flight)
            {
                templateCode = "confirmedandqueueplacesstatus";
            }

            //CommonServiceController.SendEmail(user.Email, "Mayflower – Hang On There, Your Booking Confirmation Is On Its Way", Core.getMailTemplate(templateCode, ht));
            CommonServiceController.SendEmail(userEmail, "Mayflower – Hang On There, Your Booking Confirmation Is On Its Way", Core.getMailTemplate(templateCode, ht));
        }

        private void insertCashCredit(int userID, string currencyCode, decimal amount, string description)
        {
            UserCashCredit userCashCredit = new UserCashCredit
            {
                UserID = userID,
                AvailableCashCredit = amount,
                Description = description,
                CurrencyCode = currencyCode,
                ExpiredDate = DateTime.Now.AddYears(1)
            };
            HotelServiceController.InsertUserCashCredit(userCashCredit);
        }
        #endregion

        #region Step 5 - Confirmation Page
        /* 2018/05/22 - Use for display payment result */
        //[LocalhostFilter]
        //public ActionResult Confirmation()
        //{
        //    try
        //    {
        //        MayFlower db = new MayFlower();

        //        var model = db.SuperPNRs.OrderByDescending(x => x.SuperPNRID).FirstOrDefault();
        //        return View(model);
        //    }
        //    catch (Exception ex)
        //    {
        //        return null;
        //    }
        //}

        public ActionResult Confirmation(string confirmid, bool isRegister = false)
        {
            try
            {
                MayFlower db = new MayFlower();
                var model = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRNo == confirmid);
                //SendPDFQueueHandler.Functions.CallAPI callPDFWebAPI = new SendPDFQueueHandler.Functions.CallAPI();
                //var sendResp = callPDFWebAPI.SendPDFAfterSuccess(model);

                bool allowToAccess = false;

                if (model?.CreatedByID == 0 || isRegister)
                {
                    allowToAccess = true;
                }
                else
                {
                    allowToAccess = model != null && CustomPrincipal.UserId == model.CreatedByID;
                }

                if (model.CarRentalBookings.Count > 0)
                {
                    int bookingID = model.CarRentalBookings.FirstOrDefault().BookingNo.ToInt();
                    var bookingfromAPI = CarsRentalServiceCall.SelectCarRentalBooking(bookingID);
                    ViewBag.bookingfromAPI = bookingfromAPI;
                }

                if (allowToAccess)
                {
                    return View(model);
                }
                else
                {
                    return RedirectToAction("notfound", "error");
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        #endregion

        #region Booking Notification
        [ChildActionOnly]
        public ActionResult Interruption(string pushMsg)
        {
            ViewBag.PushMsg = pushMsg;
            return View("~/Views/Checkout/Interruption.cshtml");
        }
        #endregion

        #region Offline payment
        public ActionResult GetOfflinePayment(OfflinePaymentDetail paymentDtl, string fullName, string phone, string email)
        {
            if (!string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(paymentDtl.CustomerFullname))
            {
                paymentDtl.CustomerFullname = fullName.ToTitleCase();
            }

            if (!string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(paymentDtl.CustomerPhone))
            {
                paymentDtl.CustomerPhone = phone;
            }

            if (!string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(paymentDtl.CustomerEmail))
            {
                paymentDtl.CustomerEmail = email;
            }

            string tripid = Guid.NewGuid().ToString();

            CheckoutProduct checkout = new CheckoutProduct();
            var OfflinePaymentDtl = new OfflinePaymentDetail()
            {
                CustomerEmail = paymentDtl.CustomerEmail,
                PaymentDesc = paymentDtl.PaymentDesc,
                CustomerFullname = paymentDtl.CustomerFullname,
                CustomerPhone = paymentDtl.CustomerPhone,
                PaymentAmt = paymentDtl.PaymentAmt,
                PaymentRef = paymentDtl.PaymentRef,
                StaffName = paymentDtl.StaffName,
                StaffContact = paymentDtl.StaffContact,
                StaffEmail = paymentDtl.StaffEmail
            };
            if (UtilitiesService.CheckOfflinePaymentBlock(OfflinePaymentDtl.PaymentRef, OfflinePaymentDtl.CustomerEmail))
            {
                return RedirectToAction("notfound", "error");
            }
            var pFee = new ProcessingFee("adyenc", OfflinePaymentDtl.PaymentAmt, ProductTypes.Undefined, null);
            OfflinePaymentDtl.Processingfee = pFee.TtlAmt;

            checkout.PaymentDetails = new PaymentCheckout()
            {
                CreditCard = new Alphareds.Module.Model.CreditCard(),
                GrandTotalForPayment = OfflinePaymentDtl.PaymentAmt + OfflinePaymentDtl.Processingfee,
                PaymentCurrencyCode = "MYR",
                PaymentMethod = "adyenc",
                OfflinePaymentDetails = OfflinePaymentDtl,
            };
            Core.SetSession(Enumeration.SessionName.CheckoutProduct, tripid, checkout);
            return RedirectToAction("OfflinePayment", "Checkout", new { tripid });
        }

        public ActionResult OfflinePayment(string tripid)
        {
            CheckoutProduct checkoutModel = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            return View(checkoutModel);
        }

        [HttpPost]
        public ActionResult OfflinePayment(PaymentCheckout payment, Alphareds.Module.Model.CreditCard creditCardPost, string tripid, string ccn = null)
        {
            CheckoutProduct checkoutModel = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            // Remove unnecessary model error, if not specificed payment method.
            string _pMethod = payment.PaymentMethod?.ToLower();
            if (_pMethod == null || (_pMethod != null && !_pMethod.Contains("adyen")) || Request.Form["adyen-encrypted-data"] != null)
            {
                // Reflection get credit card property to remove model validation
                var ccModel = typeof(Alphareds.Module.Model.CreditCard);

                foreach (var item in ccModel.GetProperties())
                {
                    // Remove model validtion
                    ModelState[item.Name]?.Errors.Clear();
                }
            }
            if (checkoutModel.PaymentDetails != null)
            {
                checkoutModel.PaymentDetails.CreditCard = creditCardPost;
                checkoutModel.PaymentDetails.PaymentMethod = payment.PaymentMethod;
                SqlCommand command = new SqlCommand();

                try
                {
                    var paymentDtl = checkoutModel.PaymentDetails;
                    var OffPaymentDtl = checkoutModel.PaymentDetails.OfflinePaymentDetails;
                    List<int> paymentOrderInserted = new List<int>();

                    OfflinePayment paymentOrder = PaymentServiceController.PopulateOfflinePaymentOrder(paymentDtl.PaymentCurrencyCode, OffPaymentDtl, 0,
                    paymentDtl.GrandTotalForPayment, "PEND", paymentDtl.PaymentMethod.ToUpper(), OffPaymentDtl.UserID);
                    checkoutModel.SuperPNRID = PaymentServiceController.InsertOfflinePayment(paymentOrder, command);
                    checkoutModel.SuperPNRNo = checkoutModel.PaymentDetails.OfflinePaymentDetails.PaymentRef;
                    paymentOrderInserted.Add(checkoutModel.SuperPNRID);

                    PaymentSubmitModels iPayModel = PaymentController.PopulatePaymentSubmitModel(DateTime.Now, checkoutModel.SuperPNRID, checkoutModel.SuperPNRNo, paymentDtl.PaymentCurrencyCode, paymentDtl.GrandTotalForPayment, paymentDtl.PaymentMethod, OffPaymentDtl.CustomerPhone, OffPaymentDtl.CustomerEmail, OffPaymentDtl.CustomerFullname);
                    string token = checkoutModel.SuperPNRID.ToString() + "," + checkoutModel.SuperPNRNo;
                    string encToken = General.CustomizeBaseEncoding.CodeBase64(token);
                    string iPay88RefNo = checkoutModel.SuperPNRID.ToString() + " - " + checkoutModel.SuperPNRNo; // Important, cannot simply change, will cause cannot requery fail.

                    command.Transaction.Commit();

                    PaymentController pc = new PaymentController();
                    string encPaymentOrderIDList = Cryptography.AES.Encrypt(paymentOrderInserted.JoinToString(","));

                    switch (paymentDtl.PaymentMethod.ToLower())
                    {
                        case "ipacc":
                            return pc.iPay88CheckOut(Url.Action("OfflineCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList }, Request.Url.Scheme), iPayModel, true);
                        case "ipafpx":
                            return pc.iPay88CheckOut(Url.Action("OfflineCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList }, Request.Url.Scheme), iPayModel);
                        case "adyenc":
                            AdyenCardPaymentModels adyenModel = PaymentController.PopulateAdyenPaymentSubmitModel(checkoutModel.SuperPNRID, Request.Url.Scheme, checkoutModel.SuperPNRNo, checkoutModel.PaymentDetails.PaymentCurrencyCode, checkoutModel.PaymentDetails.GrandTotalForPayment, OffPaymentDtl.CustomerEmail, checkoutModel.PaymentDetails.CreditCard);
                            return pc.AdyenCheckOut(Url.Action("OfflineCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList }, Request.Url.Scheme), adyenModel, Request.Form);
                        default:
                            ModelState.AddModelError("Error", "Payment Method Not Found.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    command?.Transaction?.Rollback();

                    logger.Fatal(ex, $"Payment POST Error [{checkoutModel.SuperPNRID}: {checkoutModel.SuperPNRNo}] - {DateTime.Now.ToLoggerDateTime()}");
                    ModelState.AddModelError("Error", "Unexpected error occured, please try again later. If this keep happen please contact support.");
                }

            }
            return View(checkoutModel);
        }


        [HttpPost]
        public async Task<ActionResult> OfflineCheckOut(FormCollection form, iPayCaptureResponseModels responseModel, adyenCaptureResponseModels responseModel2, string token, string tripid,
string paymentOdToken = null, bool fromRepay = false, string returnURL = null)
        {
            string decToken = General.CustomizeBaseEncoding.DeCodeBase64(token);
            string decPaymentOdToken = null;
            List<int> _thisTransPaymentOrderID = new List<int>();
            Cryptography.AES.TryDecrypt(paymentOdToken, out decPaymentOdToken);

            if (decPaymentOdToken != null)
            {
                var _id = decPaymentOdToken.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var _itemId in _id)
                {
                    _thisTransPaymentOrderID.Add(_itemId.ToInt());
                }
            }

            string[] orderInfo = decToken.Split(',');

            bool isRequestError = form["TechErrDesc"] != null && form["TechErrDesc"].ToString() != null && form["Status"].ToString() == "0";
            bool isPaymentError = form["ErrDesc"] != null && form["ErrDesc"].ToString() != null && form["Status"].ToString() == "0";

            // For capture payment amount base on Bank Response.

            decimal iPayResponseAmount = form["Amount"] != null && form["Amount"].ToString() != null ? Convert.ToDecimal(form["Amount"]) : 0m;
            decimal adyenResponseAmount = form["AmountValue"] != null && form["AmountValue"].ToString() != null ? Convert.ToDecimal(form["AmountValue"]) : 0m;
            bool isUseAuthCapture = form["PaymentId"] != null && form["PaymentId"].ToString() != null && form["PaymentId"].ToString() == "55";

            if (responseModel2.ShopperReference != null && responseModel2.PspReference != null)
            {
                isUseAuthCapture = true;
            }

            /* CON - Confirmed */
            /* PCP - Pending Company Payment */
            /* PPA - Pending Payment */
            string paymentStatus = isRequestError || isPaymentError ? "FAIL" : "PAID";
            string urlPaymentStatus = isRequestError || isPaymentError ? "fail" : "success";

            List<Exception> error = new List<Exception>();
            bool errorDuringUpdate = false;

            try
            {
                Dictionary<string, string> captureResponse = new Dictionary<string, string>();
                foreach (string item in form)
                {
                    if (!item.ToLower().Contains("viewstate") || !item.ToLower().Contains("Status"))
                    {
                        captureResponse.Add(item, form[item]);

                        if (item.ToLower() == "errdesc" && form[item].Contains("Customer Cancel Transaction"))
                        {
                            urlPaymentStatus = "payment-cancel";
                        }
                        else if (item.ToLower() == "errdesc" && form[item].Contains("Transaction Timeout"))
                        {
                            urlPaymentStatus = "payment-timeout";
                        }
                    }
                }

                string iPayTransactionID = captureResponse.FirstOrDefault(x => x.Key == "TransId").Value;
                string iPayDescription = captureResponse.FirstOrDefault(x => x.Key == "ErrDesc").Value;
                string adyenShopperReference = captureResponse.FirstOrDefault(x => x.Key == "ShopperReference").Value;
                string adyenPspReference = captureResponse.FirstOrDefault(x => x.Key == "PspReference").Value;
                string adyenDescription = captureResponse.FirstOrDefault(x => x.Key == "ErrDesc").Value;
                iPayCaptureResponseModels iPayCaptureResponse = null;
                adyenCaptureResponseModels adyenCaptureResponse = null;

                MayFlower db = new MayFlower();
                var superPNR = db.OfflinePayments.FirstOrDefault(x => _thisTransPaymentOrderID.Any(s => s == x.PaymentID));

                if (superPNR == null)
                {
                    string mailToSend = Core.IsForStaging ? Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailStaging") :
                        Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailLive");
                    string iPayRespondSerialize = JsonConvert.SerializeObject(captureResponse);
                    string adyenRespondSerialize = JsonConvert.SerializeObject(captureResponse);

                    CommonServiceController.SendEmail(mailToSend, "[WARNING] Mayflower After Payment Success Error", DateTime.Now.ToLoggerDateTime() + " Payment ID Not Found after payment success (" + decToken + "). " + iPayRespondSerialize + adyenRespondSerialize);
                    goto PaymentEndpoint;
                }
                else
                {
                    string currency = superPNR.CurrencyCode;

                    if (!(isPaymentError || isRequestError))
                    {
                        string _PaymentMethodCode = superPNR.PaymentMethodCode.ToLower();
                        UpdateOfflinePaymentOrder(ref superPNR, paymentStatus, _thisTransPaymentOrderID, adyenPspReference);

                        // Auth & Capture Code Here
                        // Set Status Here
                        if (isUseAuthCapture)
                        {
                            // Capture Total Amount
                            try
                            {
                                decimal _captureAmt = 0;
                                _captureAmt = superPNR.PaymentAmount;
                                if (_PaymentMethodCode.StartsWith("ipacc"))
                                {
                                    iPayCaptureResponse = PaymentServiceController.iPay88.CapturePayment(iPayTransactionID, currency, _captureAmt.ToString("n2"));
                                }
                                else
                                {
                                    adyenCaptureResponse = PaymentServiceController.Adyen.CapturePayment(adyenShopperReference, adyenPspReference, currency, _captureAmt.ToString("n2"));
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Fatal(ex, DateTime.Now.ToLoggerDateTime() + " Cannot capture offline payment. Total Payment Amount : " + superPNR.PaymentAmount.ToString()
                                + " Payment ID : " + superPNR.PaymentID + "<br/><br/>" + JsonConvert.SerializeObject(iPayCaptureResponse));
                            }

                            if (((iPayCaptureResponse == null || iPayCaptureResponse.Status != "1") && _PaymentMethodCode.StartsWith("ipa"))
                                || ((adyenCaptureResponse == null || adyenCaptureResponse.Status != "1") && _PaymentMethodCode.StartsWith("adyen")))
                            {
                                paymentStatus = "AUTH";
                            }
                            else if (iPayCaptureResponse?.Status == "1" || adyenCaptureResponse?.Status == "1")
                            {
                                // If capture success then captured partial
                                paymentStatus = "CAPT";
                            }
                        }
                        else
                        {
                            paymentStatus = "PAID";
                        }

                        /* Update Status Here */
                        try
                        {
                            UpdateOfflinePaymentOrder(ref superPNR, paymentStatus, _thisTransPaymentOrderID, _PaymentMethodCode.StartsWith("ipa") ? iPayTransactionID : adyenPspReference);
                            await db.SaveChangesAsync();
                        }
                        catch (System.Data.Entity.Validation.DbEntityValidationException ee)
                        {
                            logger.Log(LogLevel.Fatal, ee.GetBaseException(), "[EntityException] Error on CheckoutController.cs when attemp to update offline payment status. "
                            + (isUseAuthCapture ? "iPay88 status = " + iPayCaptureResponse?.Status ?? "" : "") + Environment.NewLine + Environment.NewLine
                            + (isUseAuthCapture ? "Adyen status = " + adyenCaptureResponse?.Status ?? "" : "") + Environment.NewLine + Environment.NewLine
                            + Environment.NewLine + Environment.NewLine + JsonConvert.SerializeObject(ee.EntityValidationErrors));
                        }
                        catch (Exception ex)
                        {
                            logger.Fatal(ex, DateTime.Now.ToLoggerDateTime() + " Cannot update offline payment status at CodeBlock Update Status Here : " + superPNR.PaymentAmount.ToString()
                            + Environment.NewLine + Environment.NewLine + "<br/><br/>" + JsonConvert.SerializeObject(iPayCaptureResponse)
                            + Environment.NewLine + Environment.NewLine + (isUseAuthCapture ? "iPay88 status = " + iPayCaptureResponse?.Status ?? "" : "")
                            + Environment.NewLine + Environment.NewLine
                            + Environment.NewLine + Environment.NewLine + "<br/><br/>" + JsonConvert.SerializeObject(adyenCaptureResponse)
                            + Environment.NewLine + Environment.NewLine + (isUseAuthCapture ? "Adyen status = " + adyenCaptureResponse?.Status ?? "" : ""));

                            UpdateOfflinePaymentOrder(ref superPNR, paymentStatus, _thisTransPaymentOrderID);
                            await db.SaveChangesAsync();
                        }

                        /* Handle Route After All Action Done */
                        if ((paymentStatus == "AUTH" || paymentStatus == "CAPT" || paymentStatus == "PAID"))
                        {
                            var paymentid = Mayflower.General.CustomizeBaseEncoding.CodeBase64(_thisTransPaymentOrderID.FirstOrDefault().ToString());
                            return RedirectToAction("OfflinePaymentConfirmation", "Checkout", new { tripid, confirmid = paymentid, status = "payment-success" });
                        }
                    }
                    else
                    {
                        // Here use for iPay88 postback respond payment fail only.
                        // Update Payment/Booking Status Fail Here
                        string _PaymentMethodCode = superPNR.PaymentMethodCode;
                        UpdateOfflinePaymentOrder(ref superPNR, paymentStatus, _thisTransPaymentOrderID, iPayTransactionID);

                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ee)
            {
                logger.Log(LogLevel.Fatal, ee.GetBaseException(), "[EntityException] Error on CheckoutController.cs when attemp to update offline payment status. "
                    + ("Payment Info: " + decToken) + Environment.NewLine + Environment.NewLine
                    + Environment.NewLine + Environment.NewLine + JsonConvert.SerializeObject(ee.EntityValidationErrors));
                errorDuringUpdate = true;
            }
            catch (AggregateException ae)
            {
                string allExcptMsg = "";
                foreach (var item in ae.InnerExceptions)
                {
                    allExcptMsg += item.ToString() + Environment.NewLine + Environment.NewLine;
                }
                logger.Warn(ae.GetBaseException(), "Unable to make offline payment. - " + DateTime.Today.ToLoggerDateTime()
                    + Environment.NewLine + Environment.NewLine + allExcptMsg);
                errorDuringUpdate = true;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Unable to make offline payment. - " + DateTime.Today.ToLoggerDateTime());
                errorDuringUpdate = true;
            }

            if (errorDuringUpdate)
            {
                /* Check gateway payment respond, in case db connection suddenly down but payment success. 
                 * Notice user wait for 15 mins for scheduler rebook. */
                if (!(isPaymentError || isRequestError))
                {
                    decimal _amtReceived = iPayResponseAmount > 0 ? adyenResponseAmount : iPayResponseAmount;
                    string _msgToPush = "There have some unexpected error during make offline payment, please try again later." + "<br>" +
                        $"We had captured payment - <b>{_amtReceived.ToString("n2")}</b> from payment gateway.";

                    #region Exception Email Handle Section
                    try
                    {
                        string mailToSend = Core.IsForStaging ? Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailStaging") : Core.GetAppSettingValueEnhanced("RequireHumanInterventionEmailLive");

                        string emailSubject = "Mayflower TODO Action - " + DateTime.Now.ToLoggerDateTime();

                        StringBuilder plainTextContent = new StringBuilder();
                        plainTextContent.AppendLine("Dear Support,").AppendLine();
                        plainTextContent.Append("There are errors while customer attemp to make offline payment.");
                        plainTextContent.AppendLine().AppendLine("Additional Info:");
                        plainTextContent.AppendLine(string.Join(", " + Environment.NewLine, orderInfo));

                        CommonServiceController.SendEmail(mailToSend.Trim(), emailSubject, plainTextContent.ToString(), null, false);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                    }
                    #endregion

                    return Interruption(_msgToPush);
                }
            }

            if (isRequestError)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("Technical issues, please check with your merchant and developer.");
                sb.AppendLine(responseModel.TechErrDesc);
                sb.AppendLine();

                logger.Error(new Exception("Offline Payment technical error."), sb.ToString());
            }

            PaymentEndpoint:
            if (returnURL != null)
            {
                returnURL += "&errorCode=pcfail";
                return Redirect(returnURL);
            }
            return RedirectToAction("OfflinePayment", "Checkout", new { tripid, status = urlPaymentStatus });
        }

        public ActionResult SuccessPayment()
        {
            return View();
        }

        public ActionResult OfflinePaymentConfirmation(string confirmid)
        {
            try
            {
                MayFlower db = new MayFlower();
                var PaymentID = General.CustomizeBaseEncoding.DeCodeBase64(confirmid);
                var model = db.OfflinePayments.FirstOrDefault(x => x.PaymentID.ToString() == PaymentID);

                bool allowToAccess = false;

                if (model?.CreatedByID == 0)
                {
                    allowToAccess = true;
                }
                else
                {
                    allowToAccess = model != null && CustomPrincipal.UserId == model.CreatedByID;
                }

                if (allowToAccess)
                {
                    return View(model);
                }
                else
                {
                    return RedirectToAction("notfound", "error");
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public ActionResult UpdateOfflinePayment(string method, string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);
            dynamic obj = new System.Dynamic.ExpandoObject();
            var processingfee = new ProcessingFee(method, checkout.PaymentDetails.GrandTotalForPayment, ProductTypes.Undefined, null, checkout.PaymentDetails.PaymentCurrencyCode);
            checkout.PaymentDetails.OfflinePaymentDetails.Processingfee = processingfee.TtlAmt;
            checkout.PaymentDetails.GrandTotalForPayment = checkout.PaymentDetails.OfflinePaymentDetails.PaymentAmt + processingfee.TtlAmt;
            obj.cur = checkout.PaymentDetails.PaymentCurrencyCode;
            obj.grdTtl = checkout.PaymentDetails.GrandTotalForPayment.ToString("n2");
            obj.pfee = processingfee.TtlAmt.ToString("n2");
            return Content(JsonConvert.SerializeObject(obj), "application/json");
        }
        #endregion Offline payment

        [HttpPost]
        public async Task<FileResult> PrintBookingItinerary(string SuperPNRNo, bool agentb2cPrint = false)
        {
            try
            {

                UriBuilder uriBuilder = new UriBuilder();
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                MayFlower db = new MayFlower();
                var SuperPNRs = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRNo == SuperPNRNo);
                if (SuperPNRs != null)
                {
                    var bookContact = SuperPNRs.Bookings != null && SuperPNRs.Bookings.Count > 0 ? SuperPNRs.Bookings.FirstOrDefault().Paxes.FirstOrDefault(x => x.IsContactPerson == true) : null;
                    var hotelContact = SuperPNRs.BookingHotels != null && SuperPNRs.BookingHotels.Count > 0 ? SuperPNRs.BookingHotels.FirstOrDefault().RoomPaxHotels.FirstOrDefault(x => x.IsContactPerson == true) : null;

                    if (bookContact != null || hotelContact != null)
                    {
                        query.Add("givenName", bookContact != null ? bookContact.GivenName : hotelContact.GivenName);
                        query.Add("surName", bookContact != null ? bookContact.Surname : hotelContact.Surname);
                        query.Add("email", bookContact != null ? bookContact.PassengerEmail : hotelContact.PassengerEmail);
                        query.Add("bookingNo", SuperPNRNo);
                        query.Add("randomkey", Guid.NewGuid().ToString());
                        query.Add("agentb2cPrint", agentb2cPrint.ToString().ToLower());

                        uriBuilder.Query = query.ToString();
                    }
                    else
                    {
                        var contact = SuperPNRs.BookingContact ?? new BookingContact();
                        query.Add("givenName", contact.GivenName);
                        query.Add("surName", contact.Surname);
                        query.Add("email", contact.Email);
                        query.Add("bookingNo", SuperPNRNo);
                        query.Add("randomkey", Guid.NewGuid().ToString());
                        query.Add("agentb2cPrint", agentb2cPrint.ToString().ToLower());

                        uriBuilder.Query = query.ToString();
                    }
                }

                Alphareds.Module.Common.HTTPMethod httpUtli = new HTTPMethod();
                byte[] pdfFile = await httpUtli.GetPDFPOSTRespond<object>("/api/pdf/itinerary?" + query.ToString(), null);

                if (pdfFile != null)
                {
                    return File(pdfFile, "application/pdf");
                }
                else
                {
                    logger.Fatal(DateTime.Now.ToLoggerDateTime() + "No PDF return from web service. SuperPNRNo = " + SuperPNRNo);
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, DateTime.Now.ToLoggerDateTime() + "Exception when get PDF. <br/><br/>. SuperPNRNo = " + SuperPNRNo);
                return null;
            }
        }

        [HttpPost]
        public bool SendBookingItineraryEmail(string[] emailArray, bool isBookingPerson, int SuperPNRID, bool isAgentSendB2C = false)
        {
            Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress[] emailAddress = UtilitiesService.GenerateEmailList_PDFEngine(emailArray).ToArray();
            logger = LogManager.GetCurrentClassLogger();
            bool sendStatus = false;
            try
            {
                SendPDFQueueHandler.Functions.CallAPI callPDFWebAPI = new SendPDFQueueHandler.Functions.CallAPI();
                var sendResp = callPDFWebAPI.ForwardPDF(SuperPNRID, emailAddress, false, isAgentSendB2C);
                sendStatus = sendResp.SendStatus;

            }
            catch (Exception ex)
            {
                var err = ex;
                logger.Fatal(DateTime.Now.ToLoggerDateTime() + "Exception when get PDF. <br/><br/>. SuperPNRID = " + SuperPNRID);
            }
            return sendStatus;
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
                    user = Alphareds.Module.Common.Core.GetUserInfo(CurrentUserID.ToString());
                }

                return user == null ? false : user.UserTypeCode == "AGT";
            }
        }

        private int AgentCreditTerm(int userID, MayFlower db = null)
        {
            bool isInsideOpen = db == null;

            db = db ?? new MayFlower();
            Alphareds.Module.Model.Database.User user = Alphareds.Module.Common.Core.GetUserInfo(CurrentUserID.ToString(), db);

            int value = 0;
            if (User.Identity.IsAuthenticated)
            {
                value = db.Organizations.FirstOrDefault(x => x.OrganizationID == user.OrganizationID).CreditTermInDay.HasValue ? db.Organizations.FirstOrDefault(x => x.OrganizationID == user.OrganizationID).CreditTermInDay.Value : 0;
            }

            if (isInsideOpen)
            {
                db?.Dispose();
            }

            return value;

        }

        public bool isPromoFlightList(PromoCodeRule promocode, SearchFlightResultViewModel model, Alphareds.Module.CompareToolWebService.CTWS.serviceSource serviceSource)
        {
            var FltSupplierCode = UtilitiesService.ServiceSourceToDBServiceSourceName(serviceSource);
            var promoFlightList = promocode != null && promocode.PromoFlightDestinations.Count > 0 ? promocode.PromoFlightDestinations.FirstOrDefault(x => (x.ArrivalStation == model.ArrivalStationCode || x.ArrivalStation == "-") && (x.DepartureStation == model.DepartureStationCode || x.DepartureStation == "-") && x.Active)?.PromoFlightLists : null;
            bool isPromoFlightSupplier = promocode != null && promoFlightList != null && promoFlightList.Count > 0 && promoFlightList.Any(d => (d.SupplierCode.Contains(FltSupplierCode) || d.SupplierCode == "ALL") && d.Active);

            return isPromoFlightSupplier;
        }

        public bool isPromoHotelList(PromoCodeRule promocodeRule, ProductHotel model)
        {
            bool anyDestinationOK = promocodeRule.PromoHotelDestinations.Any(x => x.Destination == "-" && x.Active);
            bool anyHotelOK = promocodeRule.PromoHotelDestinations.Any(x => model.SearchHotelInfo.Destination.StartsWith(x.Destination) && x.PromoHotelLists.Any(h => h.HotelID == "-" && h.Active));

            return anyDestinationOK || anyHotelOK ? true :
                promocodeRule.PromoHotelDestinations.Any(x => model.SearchHotelInfo.Destination.StartsWith(x.Destination) && x.Active && x.PromoHotelLists.Any(p => p.HotelID == model.HotelSelected.FirstOrDefault().hotelId && p.Active));
        }

        public void TrivagoTrackingPixel(BookingHotel hotelBooking, string tripid)
        {
            string TrivagoTrackPixelUrl = "";
            JavaScriptSerializer jsSerializer = new JavaScriptSerializer();
            string trackingcookie = Request.Cookies["AffiliateSearchCookie"].Value;
            var hotelListFromAffiliate = jsSerializer.Deserialize<List<HotelSearchCookie>>(trackingcookie);
            var hotelFromAffiliate = hotelListFromAffiliate.FirstOrDefault(x => 
                                        x.HotelID == (hotelBooking.SupplierCode + hotelBooking.HotelID) && x.TripID == tripid);

            if (hotelFromAffiliate != null)
            {
                // HotelID need to match with Inventory CSV files.
                string inventoryID = hotelBooking.SupplierCode == "TP" && hotelBooking.HotelID?.Length >= 6 ? 
                                        $"{hotelBooking.SupplierCode}{(hotelBooking.HotelID.Remove(hotelBooking.HotelID.Length - 6, 6))}" :
                                        $"{hotelBooking.SupplierCode}{hotelBooking.HotelID}";

                TrivagoTrackPixelUrl = "https://secde.trivago.com/page_check.php?pagetype=track&ref=2494";
                TrivagoTrackPixelUrl += "&hotel=" + inventoryID;
                TrivagoTrackPixelUrl += "&arrival=" + (Int32)(hotelBooking.CheckInDateTime.Subtract((new DateTime(1970, 1, 1))).TotalSeconds);
                TrivagoTrackPixelUrl += "&departure=" + (Int32)(hotelBooking.CheckOutDateTime.Subtract((new DateTime(1970, 1, 1))).TotalSeconds);
                TrivagoTrackPixelUrl += "&volume=" + hotelBooking.TotalBookingAmt.ToString("n2");
                TrivagoTrackPixelUrl += "&booking_id=" + hotelBooking.SuperPNRNo;
                TrivagoTrackPixelUrl += "&currency=MYR&domain=MY";
                TempData["TrivagoTrackPixelUrl"] = TrivagoTrackPixelUrl;

                //remove cookie after store TrivagoTrackPixelUrl in ViewData
                hotelListFromAffiliate.Remove(hotelFromAffiliate);
                string myObjectJson = jsSerializer.Serialize(hotelListFromAffiliate);
                var cookie = new System.Web.HttpCookie("AffiliateSearchCookie", myObjectJson)
                {
                    Expires = DateTime.Now.AddDays(1)
                };

                Response.Cookies.Set(cookie);
                //Response.Cookies.Add(cookie);
            }
        }

        private void GetAndAssignHotelInformation(string tripid, CheckoutProduct checkout)
        {
            if (checkout.Hotel != null)
            {
                // Get notification and fee
                #region Get Notification and Fees
                try
                {
                    GetHotelInformationModel hotelInfo = new GetHotelInformationModel()
                    {
                        CurrencyCode = checkout.CheckOutSummary.CurrencyCode,
                        CustomerUserAgent = Request.UserAgent,
                        CustomerIpAddress = General.Utilities.GetClientIP,
                        CustomerSessionId = Guid.NewGuid().ToString(),
                        HotelID = checkout.Hotel.RoomDetails.FirstOrDefault()?.HotelId ?? "0",
                        JacTravelPropertyID = checkout.Hotel.TravelPropertyID,
                        MoreOptions = new MoreInformationOptions
                        {
                            Options = InformationOptions.HOTEL_DETAILS
                        }
                    };
                    var supplierSelected = checkout.Hotel.RoomSelected.HotelRoomInformationList.First().hotelSupplier;
                    if (supplierSelected == HotelSupplier.Expedia)
                    {
                        hotelInfo.Result = ExpediaHotelsServiceCall.GetHotelInformation(hotelInfo);
                    }
                    else if (supplierSelected == HotelSupplier.EANRapid)
                    {
                        var clientResult = EANRapidHotelServiceCall.GetPropertyContent(GetUserIP(), Request.UserAgent, tripid, hotelInfo.HotelID);
                        var _res = clientResult.result.FirstOrDefault().Value ?? new Alphareds.Module.EANRapidHotels.RapidServices.Result { };
                        hotelInfo.Result = _res.ToExpediaHotelContent(tripid);
                    }

                    if (hotelInfo.Result != null)
                    {
                        if (hotelInfo.Result.hotelInformation != null && hotelInfo.Result.hotelInformation.hotelDetails != null)
                        {
                            Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels.HotelDetails hotelDtl = hotelInfo.Result.hotelInformation.hotelDetails;
                            checkout.Hotel.HotelInstrusction = new ProductHotel.CheckoutHotelInformation
                            {
                                KnowBeforeYouGoDesc = string.IsNullOrEmpty(hotelDtl.knowBeforeYouGoDescription) ? null : hotelDtl.knowBeforeYouGoDescription,
                                MandatoryFee = string.IsNullOrEmpty(hotelDtl.mandatoryFeesDescription) ? null : hotelDtl.mandatoryFeesDescription,
                                NotificationFee = string.IsNullOrEmpty(hotelDtl.roomFeesDescription) ? null : hotelDtl.roomFeesDescription,
                                CheckInInstruction = string.IsNullOrWhiteSpace(hotelDtl.checkInInstructions) ? null : hotelDtl.checkInInstructions,
                                SpecialCheckInInstruction = string.IsNullOrEmpty(hotelDtl.specialCheckInInstructions) ? null : hotelDtl.specialCheckInInstructions
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                #endregion
            }
        }
    }
}