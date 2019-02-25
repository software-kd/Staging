using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Alphareds.Module.Model;
using Alphareds.Module.Common;
using Alphareds.Module.PaymentController;
using NLog;
using Newtonsoft.Json;
using Alphareds.Module.BookingController;
using Alphareds.Module.Cryptography;
using Alphareds.Module.Model.Database;
using Alphareds.Module.HotelController;
using Alphareds.Module.MemberController;

namespace Mayflower.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.Disabled)]
    public class PaymentController : AsyncController
    {
        private MayFlower db = new MayFlower();
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);
        private Logger logger { get; set; }
        private string tripid { get; set; }

        public PaymentController()
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
        }

        #region Step 4 - ~/Payment/Checkout
        /// <summary>
        /// GET : Generate Payment Summary
        /// </summary>
        /// <returns></returns>
        [Filters.LocalhostFilter]
        public ActionResult Checkout(string paymentID = null)
        {
            string _paymentInfo = null;
            Cryptography.AES.TryDecrypt(paymentID, out _paymentInfo);

            var _superPNR = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRID.ToString() == paymentID);

            return View(_superPNR);
        }

        /// <summary>
        /// POST : Insert Product & Payment into Database
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Checkout(PaymentCheckout payment, string paymentID = null)
        {
            string _pMethod = payment.PaymentMethod?.ToLower();
            if (_pMethod != null && _pMethod.Contains("adyen"))
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
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Account inactive.");
            }

            string _paymentInfo = null;
            Cryptography.AES.TryDecrypt(paymentID, out _paymentInfo);

            var _superPNR = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRID.ToString() == _paymentInfo);

            // Check model error before postback
            if (!ModelState.IsValid)
            {
                if (_superPNR.MainProductType == ProductTypes.Flight)
                {
                    return RedirectToAction("OrderHistory", "Flight", new { tripid, bookingID = _superPNR.SuperPNRNo, status = "payment-fail" });
                }
                else if (_superPNR.MainProductType == ProductTypes.Hotel)
                {
                    return RedirectToAction("OrderHistory", "Hotel", new { tripid, confirmid = _superPNR.SuperPNRNo, status = "payment-fail" });
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Product Undefined.");
                }
            }

            var _paymentList = _superPNR.GetAllPaymentRecords();
            string paymentCurrency = "MYR";
            _superPNR.LoadPaymentDetailList(true, paymentCurrency);
            var paymentDtlList = _superPNR.PaymentDetailsList.FirstOrDefault(); // take converted rate, only one result

            SqlCommand command = new SqlCommand();

            // Can pass in user preferred currency code into ChkCreditAmtRemin, with optional parameter
            // Use this method to handle multi currency code in different order issues.
            decimal creditUserAvail = MemberServiceController.ChkCreditAmtRemain.ByUserId(CustomPrincipal.UserId, CustomPrincipal.UserTypeCode, command, paymentCurrency);
            decimal cashWalletUserAvail = MemberServiceController.GetUserCashCredit(CustomPrincipal.UserId, command);
            CheckoutController checkoutCtl = new CheckoutController(ControllerContext);

            decimal thisBookUsableAmt = checkoutCtl.CalcTravelCreditUsable(paymentDtlList.TotalPendingAmount, creditUserAvail, cashWalletUserAvail, DiscountType.TC);
            if (creditUserAvail >= thisBookUsableAmt && payment.UseCredit && (paymentDtlList.TotalPendingAmount <= thisBookUsableAmt))
            {
                payment.PaymentMethod = CustomPrincipal.IsAgent ? "AC" : "SC";
            }

            payment = new PaymentCheckout
            {
                AvailableCredit = creditUserAvail,
                CreditUsed = payment.UseCredit ? thisBookUsableAmt : 0m,
                PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMethod) ? CustomPrincipal.IsAgent ? "AC" : "SC" : payment.PaymentMethod,
                Policy = payment.Policy,
                TnC = payment.TnC,
                UseCredit = payment.UseCredit,
                CreditCard = payment.CreditCard,
                CreditTerm = CustomPrincipal.CreditTerm,
                PaymentCurrencyCode = paymentDtlList.CurrencyCode,
                GrandTotalForPayment = paymentDtlList.TotalPendingAmount
            };

            PromoCodeFunctions promoCodeFunctions = new PromoCodeFunctions(_superPNR._MainPromoID, db);
            decimal ttlOrderUsedTCAmountUseable = payment.CreditUsed;
            decimal ttlTCAmtUsed = 0m;
            List<PaymentOrder> paymentOrderToInsert = new List<PaymentOrder>();
            foreach (var _orderId in paymentDtlList.OrdersPendingPayment)
            {
                var _orders = _superPNR.SuperPNROrders.FirstOrDefault(x => x.OrderID == _orderId);

                #region Insert/Update Processing Fee
                var feeChargeOrder = _orders.FeeChargeOrders.FirstOrDefault();
                ProcessingFee pf = new ProcessingFee(payment.PaymentMethod, _orders.PendingPaymentAmt, _superPNR.MainProductType, promoCodeFunctions, paymentCurrency);

                var latestFeeCharge = new FeeChargeOrder
                {
                    OrderID = _orders.OrderID,
                    CreatedByID = _superPNR.CreatedByID,
                    CurrencyCode = _orders.CurrencyCode,
                    FeeChargeAmount = pf.Amt,
                    FeeCode = string.IsNullOrWhiteSpace(pf.FeeCode) ? payment.PaymentMethod : pf.FeeCode,
                    TaxCode = string.IsNullOrWhiteSpace(pf.TaxCode) ? "ES" : pf.TaxCode,
                    TaxAmount = pf.GST,
                    ModifiedByID = CurrentUserID,
                    SuperPNROrder = _orders,
                };

                if (feeChargeOrder == null)
                {
                    _orders.FeeChargeOrders.Add(latestFeeCharge); // for model calculation
                    PaymentServiceController.InsertFeeChargeOrder(latestFeeCharge, command);
                }
                else
                {
                    // For model calculation only
                    feeChargeOrder.CurrencyCode = _orders.CurrencyCode;
                    feeChargeOrder.FeeChargeAmount = pf.Amt;
                    feeChargeOrder.FeeCode = string.IsNullOrWhiteSpace(pf.FeeCode) ? payment.PaymentMethod : pf.FeeCode;
                    feeChargeOrder.TaxCode = string.IsNullOrWhiteSpace(pf.TaxCode) ? "ES" : pf.TaxCode;
                    feeChargeOrder.TaxAmount = pf.GST;

                    PaymentServiceController.UpdateFeeChargeOrder(latestFeeCharge, command);
                }
                #endregion

                if (payment != null && payment.UseCredit && payment.CreditUsed > 0 && (payment.PaymentMethod != "SC" && payment.PaymentMethod != "AC"))
                {
                    decimal _thisTransAmt = _orders.PendingPaymentAmt;
                    decimal _thisTransUseTCAmt = ttlOrderUsedTCAmountUseable;
                    bool usedAllTC = ttlTCAmtUsed == payment.CreditUsed;

                    if (ttlOrderUsedTCAmountUseable > _thisTransAmt && !usedAllTC)
                    {
                        _thisTransUseTCAmt = _thisTransAmt;
                    }

                    if (ttlOrderUsedTCAmountUseable > 0 && !usedAllTC)
                    {
                        ttlOrderUsedTCAmountUseable -= _thisTransUseTCAmt;
                        ttlTCAmtUsed += _thisTransUseTCAmt;

                        PaymentOrder tcPaymentOrder = new PaymentOrder
                        {
                            OrderID = _orders.OrderID,
                            PaymentDate = DateTime.Now,
                            PaymentMethodCode = CustomPrincipal.IsAgent ? "AC" : "SC",
                            PaymentStatusCode = "PEND",
                            CurrencyCode = _orders.CurrencyCode,
                            PaymentAmount = _thisTransUseTCAmt,
                            ImagePath = string.Empty,
                            Ipay88RefNo = string.Empty,
                            Ipay88TransactionID = string.Empty,
                            CreatedByID = CurrentUserID,
                            ModifiedByID = CurrentUserID
                        };

                        paymentOrderToInsert.Add(tcPaymentOrder);

                        // Update SuperPNR Order indicated is Credit Used.
                        _orders.CreditAmount += _thisTransUseTCAmt;
                        _orders.IsCreditUsed = _orders.CreditAmount > 0;
                        HotelServiceController.UpdateSuperPNROrder(_orders, command);
                    }
                }
                else
                {
                    // Update SuperPNR Order indicated is Credit Used.
                    _orders.CreditAmount = _orders.PaymentOrders.Where(x => (x.PaymentMethodCode == "AC" || x.PaymentMethodCode == "SC") && x.PaymentStatusCode == "PAID").Sum(s => s.PaymentAmount);
                    _orders.IsCreditUsed = _orders.CreditAmount > 0;
                    HotelServiceController.UpdateSuperPNROrder(_orders, command);
                }

                decimal paymentPrepareToInsert = paymentOrderToInsert.Where(x => x.OrderID == _orders.OrderID).Sum(s => s.PaymentAmount);

                if (paymentPrepareToInsert < _orders.PendingPaymentAmt)
                {
                    PaymentOrder normalPayment = new PaymentOrder
                    {
                        OrderID = _orders.OrderID,
                        PaymentDate = DateTime.Now,
                        PaymentMethodCode = payment.PaymentMethod,
                        PaymentStatusCode = "PEND",
                        CurrencyCode = _orders.CurrencyCode,
                        PaymentAmount = _orders.PendingPaymentAmt - paymentPrepareToInsert,
                        ImagePath = string.Empty,
                        Ipay88RefNo = string.Empty,
                        Ipay88TransactionID = string.Empty,
                        CreatedByID = CurrentUserID,
                        ModifiedByID = CurrentUserID
                    };

                    paymentOrderToInsert.Add(normalPayment);
                }
            }

            decimal ttlAmtPOSTGateway = paymentOrderToInsert.Where(x => x.PaymentMethodCode != "SC" && x.PaymentMethodCode != "AC").Sum(s => s.PaymentAmount);
            List<int> paymentOrderInserted = new List<int>();

            #region Insert SupperPNROrders records into DB
            try
            {
                foreach (var paymentRecord in paymentOrderToInsert)
                {
                    paymentOrderInserted.Add(PaymentServiceController.InsertPaymentOrder(paymentRecord, command));
                }

                #region Payment Gateway Redirect
                _superPNR.GetContactPerson();
                var contactPerson = _superPNR.ContactPerson;
                PaymentSubmitModels iPayModel = PaymentController.PopulatePaymentSubmitModel(DateTime.Now, _superPNR.SuperPNRID, _superPNR.SuperPNRNo, paymentCurrency, ttlAmtPOSTGateway, payment.PaymentMethod, contactPerson.Phone1, contactPerson.Email, contactPerson.FullName);

                // Save transaction before redirect
                command.Transaction.Commit();

                string clientIP = HttpContext.Request.UserHostAddress;
                string paymentMethod = payment.PaymentMethod.ToUpper();
                string token = _superPNR.SuperPNRID.ToString() + "," + _superPNR.SuperPNRNo;
                string encToken = General.CustomizeBaseEncoding.CodeBase64(token);
                string encPaymentOrderIDList = Cryptography.AES.Encrypt(paymentOrderInserted.JoinToString(","));

                FormCollection form = new FormCollection();
                adyenCaptureResponseModels captureResponseModels2 = new adyenCaptureResponseModels();
                iPayCaptureResponseModels captureResponseModels = new iPayCaptureResponseModels
                { Status = "1", Amount = iPayModel.PaymentAmount, TransId = "" };

                switch (payment.PaymentMethod.ToLower())
                {
                    case "sc":
                        form.Add("Status", captureResponseModels.Status);
                        form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                        return await checkoutCtl.PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList, true);
                    case "ac":
                        form.Add("Status", captureResponseModels.Status);
                        form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                        return await checkoutCtl.PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, tripid, encPaymentOrderIDList, true);
                    case "ipacc":
                        return iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, fromRepay = true }, Request.Url.Scheme), iPayModel, true);
                    case "ipafpx":
                        return iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, fromRepay = true }, Request.Url.Scheme), iPayModel);
                    case "adyenc":
                        AdyenCardPaymentModels adyenModel = PaymentController.PopulateAdyenPaymentSubmitModel(_superPNR.SuperPNRID, Request.Url.Scheme, _superPNR.SuperPNRNo, paymentCurrency, ttlAmtPOSTGateway, contactPerson.Email, payment.CreditCard);
                        return AdyenCheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, tripid, paymentOdToken = encPaymentOrderIDList, fromRepay = true }, Request.Url.Scheme), adyenModel, Request.Form);
                    default:
                        ModelState.AddModelError("Error", "Payment Method Not Found.");
                        break;
                }

                #endregion

            }
            catch (Exception ex)
            {
                // Insert payment record failed.
                command?.Transaction?.Rollback();

                // Handle redirect back to OrderHistory Page
                ViewBag.PaymentDetails = payment;
                ModelState.AddModelError("Error", "Unexpected error occured, please try again later.");

                string errorMsg = $"Insert Payment Order record failed on OrderHistory repayment.";
                logger.Fatal(ex, errorMsg);

                if (_superPNR.MainProductType == ProductTypes.Flight)
                {
                    return RedirectToAction("OrderHistory", "Flight", new { tripid, bookingID = _superPNR.SuperPNRNo, status = "payment-fail" });
                }
                else if (_superPNR.MainProductType == ProductTypes.Hotel)
                {
                    return RedirectToAction("OrderHistory", "Hotel", new { tripid, confirmid = _superPNR.SuperPNRNo, status = "payment-fail" });
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Product Undefined.");
                }
            }
            #endregion

            return null;
        }
        #endregion

        public static PaymentSubmitModels PopulatePaymentSubmitModel(DateTime bookingDate, int bookingId, string bookingNo, string currencyCode, decimal totalAmount, string paymentMethod, string userContact, string userEmail, string userFullName)
        {
            PaymentSubmitModels paymentSubmitModel = new PaymentSubmitModels();

            paymentSubmitModel.BookingDate = bookingDate;
            paymentSubmitModel.BookingId = bookingId;
            paymentSubmitModel.BookingNo = bookingNo;
            paymentSubmitModel.Currency = currencyCode;
            paymentSubmitModel.PaymentAmount = totalAmount;
            paymentSubmitModel.PaymentMethod = paymentMethod;
            paymentSubmitModel.UserContact = userContact;
            paymentSubmitModel.UserEmail = userEmail;
            paymentSubmitModel.UserName = userFullName;

            return paymentSubmitModel;
        }

        public static AdyenCardPaymentModels PopulateAdyenPaymentSubmitModel(int bookingId, string destinationUrl, string bookingNo, string currencyCode, decimal totalAmount, string userEmail, CreditCard model)
        {
            AdyenCardPaymentModels adyenPaymentSubmitModel = new AdyenCardPaymentModels();
            adyenPaymentSubmitModel.SuperPNRID = bookingId;
            adyenPaymentSubmitModel.SuperPNRNo = bookingNo;
            adyenPaymentSubmitModel.CardNumber = model.CreditCardNo;
            adyenPaymentSubmitModel.CardHolderName = model.CardholderName;
            adyenPaymentSubmitModel.ExpiryMonth = model.ExpMonths.ToString("00");
            adyenPaymentSubmitModel.ExpiryYear = model.ExpYear;
            adyenPaymentSubmitModel.CVV = model.CVC;
            adyenPaymentSubmitModel.GenerationTime = model.GenerationTime;
            adyenPaymentSubmitModel.AppId = "2";
            adyenPaymentSubmitModel.Currency = currencyCode;
            adyenPaymentSubmitModel.PaymentAmount = string.Join("", (totalAmount * 100).ToString("0.####").Where(char.IsDigit));
            adyenPaymentSubmitModel.ProdDesc = "MayFlower Booking Payment - " + bookingNo;
            adyenPaymentSubmitModel.ShopperReference = bookingNo;
            adyenPaymentSubmitModel.ShopperEmail = userEmail;
            adyenPaymentSubmitModel.ShopperIP = Mayflower.General.Utilities.GetClientIP;
            adyenPaymentSubmitModel.ResponseURL = destinationUrl;

            return adyenPaymentSubmitModel;
        }

        public static BoostPaymentModels PopulateBoostPaymentSubmitModel(int bookingId, string bookingNo, string destinationUrl, string phoneNo,decimal totalAmount)
        {
            BoostPaymentModels boostPaymentSubmitModel = new BoostPaymentModels();
            boostPaymentSubmitModel.SuperPNRID = bookingId;
            boostPaymentSubmitModel.SuperPNRNo = bookingNo;
            boostPaymentSubmitModel.AppId = "2";
            boostPaymentSubmitModel.PaymentAmount = totalAmount.ToString();
            boostPaymentSubmitModel.ProdDesc = "MayFlower Booking Payment - " + bookingNo;
            boostPaymentSubmitModel.ShopperReference = bookingNo;
            boostPaymentSubmitModel.ResponseURL = destinationUrl;
            boostPaymentSubmitModel.UserContact = phoneNo;

            return boostPaymentSubmitModel;
        }

        /// <summary>
        /// For iPay payment usage.
        /// </summary>
        /// <param name="destinationUrl">POSTBACK URL after action done on iPay Payment Gateway tpage.</param>
        /// <param name="payment">Payment Model for submit to iPay.</param>
        /// <param name="requireAuthCapture">Pay using Authorize & Capture method, if Booking success then call xxxx Method to mark as Success or Fail. (For Credit Card only.)</param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult iPay88CheckOut(string destinationUrl, PaymentSubmitModels payment, bool requireAuthCapture = false)
        {
            bool isUseAuthCapt = bool.TryParse(Core.GetAppSettingValueEnhanced("iPay88CCAuthCapture"), out isUseAuthCapt) ? isUseAuthCapt : isUseAuthCapt;

            if (payment == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            string paymentId = null;
            switch (payment.PaymentMethod.ToLower())
            {
                case "ipacc":
                    paymentId = requireAuthCapture && isUseAuthCapt ? "55" : "2";
                    break;
                case "ipafpx":
                    paymentId = "16";
                    break;
                default:
                    break;
            }

            NameValueCollection formData = new NameValueCollection();
            string iPay88POSTURL = Core.GetAppSettingValueEnhanced("iPay88POSTURL");

            if (!string.IsNullOrEmpty(payment.BookingNo))
            {
                // The Merchant Code provided by iPay88 and use to uniquely identify the Merchant.
                string iPay88AppId = Core.GetAppSettingValueEnhanced("iPay88AppId");

                string iPay88Testing = Core.GetAppSettingValueEnhanced("iPay88Testing");
                bool isTestingAcc = bool.TryParse(iPay88Testing, out isTestingAcc) ? isTestingAcc : isTestingAcc;

                formData.Add("AppId", iPay88AppId);
                // Refer to Appendix I.pdf
                formData.Add("PaymentId", paymentId);

                // Ref No only allow 20 char.
                formData.Add("RefNo", payment.BookingId + " - " + payment.BookingNo);
                formData.Add("Amount", isTestingAcc ? "1.00" : payment.PaymentAmount.ToString("n2"));

                formData.Add("Currency", payment.Currency);
                formData.Add("ProdDesc", "MayFlower Booking Payment - " + payment.BookingNo);
                formData.Add("UserName", payment.UserName);
                formData.Add("UserEmail", payment.UserEmail);
                formData.Add("UserContact", string.IsNullOrEmpty(payment.UserContact) ? "-" : payment.UserContact);
                // Refer to Technical Spec
                //formData.Add("Lang", "UTF-8");
                formData.Add("ResponseURL", destinationUrl);
            }

            string postURL = iPay88POSTURL;
            string strForm = PreparePOSTForm(postURL, formData);

            return Content(strForm);
        }

        private string PreparePOSTForm(string url, NameValueCollection data)
        {
            //Set a name for the form
            string formID = "PostForm";
            //Build the form using the specified data to be posted.
            StringBuilder strForm = new StringBuilder();
            strForm.Append("<form id=\"" + formID + "\" name=\"" +
                           formID + "\" action=\"" + url +
                           "\" method=\"POST\">");

            foreach (string key in data)
            {
                strForm.Append("<input type=\"hidden\" name=\"" + key +
                               "\" value=\"" + data[key] + "\">");
            }

            strForm.Append("</form>");
            //Build the JavaScript which will do the Posting operation.
            StringBuilder strScript = new StringBuilder();
            strScript.Append("<script language=\"javascript\">");
            strScript.Append("var v" + formID + " = document." +
                             formID + ";");
            strScript.Append("v" + formID + ".submit();");
            strScript.Append("</script>");
            //Return the form and the script concatenated.
            //(The order is important, Form then JavaScript)
            return strForm.ToString() + strScript.ToString();
        }

        #region Private Utitlies Helper Method
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
        #endregion

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }

        [HttpPost]
        public ActionResult AdyenCheckOut(string destinationUrl, AdyenCardPaymentModels model, NameValueCollection fc)
        {
            string isAdyenTesting = Core.GetAppSettingValueEnhanced("AdyenTesting");
            bool isTestingAcc = bool.TryParse(isAdyenTesting, out isTestingAcc) ? isTestingAcc : isTestingAcc;
            if (model == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            NameValueCollection formData = new NameValueCollection();
            string AdyenPOSTURL = Core.GetAppSettingValueEnhanced("AdyenPOSTURL");

            if (!string.IsNullOrEmpty(model.SuperPNRNo))
            {
                //formData.Add("number", model.CardNumber.Replace("-", ""));
                //formData.Add("holderName", model.CardHolderName);
                //formData.Add("expiryMonth", model.ExpiryMonth.ToString());// expected two digit
                //formData.Add("expiryYear", model.ExpiryYear.ToString());
                //formData.Add("cvc", model.CVV.ToString());
                //formData.Add("generationtime", model.GenerationTime);
                formData.Add("adyen-encrypted-data", fc["adyen-encrypted-data"]);

                /*foreach (var item in fc.AllKeys)
                {
                    formData.Add(item, fc[item]);
                }*/

                formData.Add("ShopperReference", model.SuperPNRID + " - " + model.SuperPNRNo);
                formData.Add("ShopperEmail", model.ShopperEmail);
                formData.Add("AmountValue", isTestingAcc ? "500" : model.PaymentAmount.ToString().Replace(".", ""));
                formData.Add("AmountCurrency", model.Currency);
                formData.Add("ProdDesc", "MayFlower Booking Payment - " + model.SuperPNRNo);
                formData.Add("AppId", "2");
                formData.Add("ShopperIP", Mayflower.General.Utilities.GetClientIP);
                formData.Add("ResponseURL", destinationUrl);
                // Refer to Appendix I.pdf
                //formData.Add("PaymentId", paymentId);

                // Ref No only allow 20 char.
                //formData.Add("Amount", isTestingAcc ? "1.00" : payment.PaymentAmount.ToString("n2"));
            }

            string postURL = AdyenPOSTURL;
            string strForm = AdyenPreparePOSTForm(postURL, formData);

            return Content(strForm);
        }

        [HttpPost]
        public ActionResult AdyenDefaultCardCheckOut(string destinationUrl, AdyenCardPaymentModels model, string bookingNo, decimal bookingAmt)
        {
            if (model == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            NameValueCollection formData = new NameValueCollection();
            string AdyenMethod2POSTURL = Core.GetAppSettingValueEnhanced("AdyenMethod2POSTURL");

            if (!string.IsNullOrEmpty(bookingNo))
            {
                formData.Add("AppId", "2");
                formData.Add("AmountValue", model.PaymentAmount.ToString().Replace(".", ""));
                formData.Add("AmountCurrency", model.Currency);
                formData.Add("ProdDesc", "MayFlower Booking Payment - " + model.SuperPNRNo);
                formData.Add("ShopperReference", model.SuperPNRID + " - " + model.SuperPNRNo);
                formData.Add("ShopperEmail", model.UserEmail);
                formData.Add("ShopperIP", Mayflower.General.Utilities.GetClientIP);
                formData.Add("ResponseURL", destinationUrl);
            }

            string postURL = AdyenMethod2POSTURL;
            string strForm = PreparePOSTForm(postURL, formData);

            return Content(strForm);
        }

        private string AdyenPreparePOSTForm(string url, NameValueCollection data)
        {
            //Set a name for the form
            string formID = "PostForm";
            //Build the form using the specified data to be posted.
            StringBuilder strForm = new StringBuilder();
            strForm.Append("<form id=\"" + formID + "\" name=\"" +
                           formID + "\" action=\"" + url +
                           "\" method=\"POST\">");

            foreach (string key in data)
            {
                strForm.Append("<input type=\"hidden\" name=\"" + key +
                               "\" value=\"" + data[key] + "\">");
            }

            strForm.Append("</form>");
            //Build the JavaScript which will do the Posting operation.
            StringBuilder strScript = new StringBuilder();
            strScript.Append("<script language=\"javascript\">");
            strScript.Append("var v" + formID + " = document." +
                             formID + ";");
            strScript.Append("v" + formID + ".submit();");
            strScript.Append("</script>");
            //Return the form and the script concatenated.
            //(The order is important, Form then JavaScript)
            return strForm.ToString() + strScript.ToString();
        }

        [HttpPost]
        public ActionResult BoostCheckOut(string destinationUrl = null, BoostPaymentModels model =null) ////
        {
            if (model == null && !Core.IsForLocalHost) 
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            //string destinationUrl2 = Url.Action("TestCheckOut", "Checkout");

            NameValueCollection formData = new NameValueCollection();
            string BoostRequestPaymentURL = Core.GetAppSettingValueEnhanced("BoostRequestPaymentURL");
            string adyenAppId = Core.GetAppSettingValueEnhanced("BoostAppId");
            if (Core.IsForStaging) /////for test only
            {
                //formData.Add("onlineRefNum", "2148 - 0VMKG6T");
                //formData.Add("AppID", adyenAppId);
                //formData.Add("amount", "0.01");
                //formData.Add("remark", "ticket");
                //formData.Add("responseURL", destinationUrl);
                //formData.Add("initiatorMobileNo", "+6012346789"); //not sure
                //formData.Add("redirectLabel", "");
                //formData.Add("redirectDeepLink", "");
                formData.Add("onlineRefNum", model.SuperPNRID + " - " + model.SuperPNRNo);
                formData.Add("AppID", adyenAppId);
                formData.Add("amount", "0.01");
                formData.Add("remark", "ticket");
                formData.Add("responseURL", destinationUrl);
                formData.Add("initiatorMobileNo", model.UserContact);
                formData.Add("redirectLabel", "");
                formData.Add("redirectDeepLink", "");
            }
            else
            {
                //follow local after done test
                formData.Add("onlineRefNum", model.SuperPNRID + " - " + model.SuperPNRNo);
                formData.Add("AppID", adyenAppId);
                formData.Add("amount", model.PaymentAmount.ToString());
                formData.Add("remark", "ticket");
                formData.Add("responseURL", destinationUrl);
                formData.Add("initiatorMobileNo", model.UserContact); 
                formData.Add("redirectLabel", "");
                formData.Add("redirectDeepLink", "");
            }

            string strForm = PrepareBoostPOSTForm(BoostRequestPaymentURL, formData);
            return Content(strForm);
        }


        private string PrepareBoostPOSTForm(string url, NameValueCollection data)
        {
            //Set a name for the form
            string formID = "PostForm";
            //Build the form using the specified data to be posted.
            StringBuilder strForm = new StringBuilder();
            strForm.Append("<form id=\"" + formID + "\" name=\"" +
                           formID + "\" action=\"" + url +
                           "\" method=\"POST\">");

            foreach (string key in data)
            {
                strForm.Append("<input type=\"text\" name=\"" + key +
                               "\" value=\"" + data[key] + "\">");
            }

            strForm.Append("</form>");
            //Build the JavaScript which will do the Posting operation.
            StringBuilder strScript = new StringBuilder();
            strScript.Append("<script language=\"javascript\">");
            strScript.Append("var v" + formID + " = document." +
                             formID + ";");
            strScript.Append("v" + formID + ".submit();");
            strScript.Append("</script>");
            //Return the form and the script concatenated.
            //(The order is important, Form then JavaScript)
            return strForm.ToString() + strScript.ToString();
        }




    }
    }
