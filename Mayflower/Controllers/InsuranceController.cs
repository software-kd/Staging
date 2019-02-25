using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Alphareds.Module.Model.Database;
using Alphareds.Module.Model;
using PagedList;
using System.Data.SqlClient;
//using Mayflower.General;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Mayflower.Filters;
using System.Net;
using System.IO;
using System.Dynamic;
using NLog;
using Alphareds.Module.BookingController;
using Alphareds.Module.MemberController;
using Alphareds.Module.CommonController;
using Alphareds.Module.Cryptography;
using Newtonsoft.Json;
using Alphareds.Module.Common;
using Microsoft.Ajax.Utilities;
using Alphareds.Module.InsuranceController;
using Alphareds.Module.PaymentController;

namespace Mayflower.Controllers
{
    [Authorize]
    public class InsuranceController : AsyncController
    {
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);
        private Logger logger = LogManager.GetCurrentClassLogger();
        

        #region step 1 quote insurance
        [HttpGet]
        public ActionResult GetProtected(string superPNRNo)
        {
            MayFlower db = new MayFlower();
            CheckoutProduct checkoutProduct = new CheckoutProduct();
            //need check is it login user and superPNR user same or not //date is it already pass
            List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote> res = null;
            try
            {

                var superPNR = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRNo == superPNRNo);
                #region check can add insurance or not
                bool notAllow = false;

                if(superPNR == null)
                {
                    ModelState.AddModelError("Error", "We are sorry! Your PNR number is incorrect.");
                    return View(new CheckoutProduct());
                }

                if (superPNR.UserID != CurrentUserID)
                {
                    string errorMessage = "You are logging in with a different account. Please login with the correct account. (" + superPNR.Bookings?.FirstOrDefault().User.Email + ")" ;

                    ModelState.AddModelError("Error", errorMessage);
                    notAllow = true;
                }
                if (superPNR.Bookings.Count <= 0)
                {
                    ModelState.AddModelError("Error", "This is not a flight booking.");
                    notAllow = true;
                }
                else
                {
                    var testOnly = DateTime.Now.AddHours(5);
                    bool dateAfterDepart = DateTime.Now.AddHours(5) > superPNR.Bookings.FirstOrDefault().FlightSegments.FirstOrDefault(x => x.SegmentOrder == "O1").DepartureDateTime;
                    if (dateAfterDepart)
                    {
                        ModelState.AddModelError("Error", "Not allow to purchase 5 hour before flight depart.");
                        notAllow = true;
                    }
                    else if (superPNR.BookingInsurances.Count > 0 && superPNR.BookingInsurances.Any(x => x.BookingStatusCode == "CON")) //change to count
                    {
                        ModelState.AddModelError("Error", "Already having Insurance on the Booking.");
                        notAllow = true;
                    }
                }

                if (notAllow)
                {
                    return View(new CheckoutProduct());
                }
                #endregion

                var flightOrigin = superPNR.Bookings.FirstOrDefault().Origin;
                bool departFromMYS = db.Stations.FirstOrDefault(x => x.StationCode == flightOrigin)?.CountryCode == "MYS";
                var flightBooking = superPNR.Bookings.FirstOrDefault();
                var beginDate = flightBooking.FlightSegments.FirstOrDefault(x => x.SegmentOrder == "O1").DepartureDateTime;
                var endDate = flightBooking.IsReturn ? flightBooking.FlightSegments.LastOrDefault(x => x.SegmentOrder.Contains("I")).ArrivalDateTime : beginDate.AddDays(1);

                var totalTravelDay = endDate - beginDate; //"searchDateFrom" will be different when flight reach on next day
                bool totalTravelDaysLessThan45 = flightBooking.IsReturn ? totalTravelDay.TotalDays < 45 : true; //more than 45 days no offer insurance

                if (Core.IsEnableFlightInsurance && departFromMYS && totalTravelDaysLessThan45) //add one for beforee departure
                {
                    res = QuoteInsurance(superPNRNo);
                }
                else
                {
                    ModelState.AddModelError("Error", "Sorry booking not meet insurance requirement");
                    return View(new CheckoutProduct());
                }

                #region bind checkoutProduct data here
                Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote quoteRes = new Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote();

                if (res != null && res.Count > 0)
                {
                    quoteRes = res.FirstOrDefault();
                }
                else
                {
                    ModelState.AddModelError("Error", "Error when quote Insurance, pls try again later");
                    return View(new CheckoutProduct()); //ori need checkoutProductnew Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote()
                }

                if (quoteRes.Body != null)
                {
                    #region Insurance
                    checkoutProduct.SellItemsAvailable.Insurance = new CrossSellItemsAvailable.InsuranceInformation
                    {
                        ServiceRespond = res,
                        QuotedInformations = new List<CrossSellItemsAvailable.InsuranceInformation.QuotedInformation>(),
                    };

                    var resQuotedInfo = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo;

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
                        EffectiveDate = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.EffectiveDt.ToDateTime(),
                        ExpirationDate = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.ExpirationDt.ToDateTime(),
                        TermsConditions = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo.CoverageConditionsInd,
                        InsurancedAddress = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.InsuredOrPrincipal.GeneralPartyInfo.Addr.Addr1,
                        PlanType = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_Plan.PlanDesc,
                        Package = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_InsuredPackage.InsuredPackageDesc,

                        insuranceCost = totalInsuranceCost
                    };
                    checkoutProduct.SellItemsAvailable.Insurance.QuotedInformations.Add(quotedInformation);
                    #endregion

                    var bookedDetial = GetBookedDetail(superPNRNo);
                    if(bookedDetial != null)
                    {
                        ViewBag.BookedDetail = bookedDetial;
                    }
                    


                    checkoutProduct.SuperPNRNo = superPNRNo;
                }
                else
                {
                    ModelState.AddModelError("Error", "Error when quote Insurance, pls try again later");
                }
                #endregion

                ViewBag.superPNRNo = superPNRNo;
            }
            catch (AggregateException ae)
            {
                logger.Error(ae.GetBaseException(), "Error when add quote insurance.");
            }
            catch (Exception ex)
            {
                logger.Error(ex.GetBaseException(), "Error when add quote insurance.");
            }
            return View(checkoutProduct);
        }

        [HttpPost]
        public ActionResult GetProtected(bool requestInsurance, string superPNRNo)
        {
            if (requestInsurance)
            {
                return RedirectToAction("InsurancePayment", "Insurance", new { superPNRNo });                 
            } 
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
        #endregion

        [HttpGet] 
        public ActionResult InsurancePayment(string superPNRNo, string errorCode = null)
        {
            MayFlower db = new MayFlower();
            if (errorCode != null && errorCode != "pcfail")
            {
                string errorMsg = "";

                if(errorCode == "pcfail")
                {
                    //errorMsg = "Payment Checkout Fail";
                }
                else if (errorCode == "pf")
                {
                    errorMsg = "Payment Fail";
                }
                else if (errorCode == "pv")
                {
                    errorMsg = "Payment Void";
                }

                ModelState.AddModelError("Error", errorMsg);
            }
            
            CheckoutProduct checkoutProduct = new CheckoutProduct();

            //quote again 
            List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote> res = QuoteInsurance(superPNRNo);
            Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote quoteRes = new Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote();

            if(res != null && res.Count > 0)
            {
                quoteRes = res.FirstOrDefault();
            }
            else
            {
                ModelState.AddModelError("Error", "Error when quote Insurance, pls try again later");
                return View(checkoutProduct);
            }
            
            bool isAgent = IsAgentUser;
            decimal availableCredit = CurrentUserID != 0 ? (isAgent ? MemberServiceController.ChkCreditAmtRemain.Agent(CurrentUserID.ToString()) :
            MemberServiceController.ChkCreditAmtRemain.Member(CurrentUserID.ToString())) : 0m;
            decimal CashCreditBalace = CurrentUserID != 0 ? MemberServiceController.GetUserCashCredit(CurrentUserID) : 0;
            int creditTerm = 1;
            if (isAgent)
            {
                creditTerm = AgentCreditTerm(CurrentUserID);
            }

            if (quoteRes.Body != null) 
            {
                checkoutProduct.SellItemsAvailable.Insurance = new CrossSellItemsAvailable.InsuranceInformation
                {
                    ServiceRespond = res,
                    QuotedInformations = new List<CrossSellItemsAvailable.InsuranceInformation.QuotedInformation>(),
                };

                var resQuotedInfo = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo;

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
                    EffectiveDate = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.EffectiveDt.ToDateTime(),
                    ExpirationDate = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.ContractTerm.ExpirationDt.ToDateTime(),
                    TermsConditions = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo.CoverageConditionsInd,
                    InsurancedAddress = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.InsuredOrPrincipal.GeneralPartyInfo.Addr.Addr1,
                    PlanType = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_Plan.PlanDesc,
                    Package = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.acegroup_InsuredPackage.InsuredPackageDesc,

                    insuranceCost = totalInsuranceCost
                };
                checkoutProduct.SellItemsAvailable.Insurance.QuotedInformations.Add(quotedInformation);

                checkoutProduct.RemoveProduct(ProductTypes.Insurance);
                AddInsuranceToPayment(ref checkoutProduct);

                if (checkoutProduct.PaymentDetails == null)
                {
                    checkoutProduct.PaymentDetails = new PaymentCheckout()
                    {
                        AvailableCredit = availableCredit,
                        PaymentMethod = "IPAFPX",
                        CreditTerm = creditTerm,
                        PaymentCurrencyCode = checkoutProduct.CheckOutSummary.CurrencyCode,
                        EWallet = new EWallet
                        {
                            BalanceAmt = CashCreditBalace,
                        }
                    };
                }
                else
                {
                    checkoutProduct.PaymentDetails.AvailableCredit = availableCredit;
                    checkoutProduct.PaymentDetails.CreditTerm = creditTerm;
                    checkoutProduct.PaymentDetails.EWallet.BalanceAmt = CashCreditBalace;

                    if (checkoutProduct.CheckOutSummary.DiscountDetails.Count > 0)
                    {
                        checkoutProduct = UpdatePayment(checkoutProduct, checkoutProduct.PaymentDetails.PaymentMethod, checkoutProduct.PaymentDetails.UseCredit, checkoutProduct.PaymentDetails.EWallet?.UseWallet ?? false);
                    }
                }

                checkoutProduct.PaymentDetails.CreditCard = new Alphareds.Module.Model.CreditCard();

                checkoutProduct.SuperPNRNo = superPNRNo;
            }
            else
            {
                ModelState.AddModelError("Error", "Error when quote Insurance, pls try again later");
            }
            TempData["CheckoutProduct"] = checkoutProduct;
            ViewBag.OnlyInsurance = true;
            var bookedDetial = GetBookedDetail(superPNRNo);

            if (bookedDetial != null)
            {
                ViewBag.BookedDetail = bookedDetial;
            }

            return View(checkoutProduct); 
            //return View("~/Views/Checkout/Payment_v2.cshtml", checkoutProduct); ///payment button need call different controller function //by call different action and controller name(string)
        }

        [HttpPost] 
        public async Task<ActionResult> InsurancePayment(string superPNRNo, PaymentCheckout payment, Alphareds.Module.Model.CreditCard creditCardPost, string tripid = null, string paymentLater = null,
        string ccn = null)
        {
            MayFlower db = new MayFlower();
            CheckoutProduct checkoutProduct = (CheckoutProduct)TempData["CheckoutProduct"];
            //must quote again// no quote will get error
            List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote> res = QuoteInsurance(superPNRNo);
            Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote quoteRes = new Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote();

            if(res.Count > 0)
            {
                quoteRes = res.FirstOrDefault();
            }
            else
            {
                ModelState.AddModelError("Error", "Error when quote Insurance, pls try again later");
                return View(checkoutProduct);
            }

            bool isAgent = IsAgentUser;
            decimal totalInsurancePrice = quoteRes.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.PersPolicy.QuoteInfo.InsuredFullToBePaidAmt.Amt.ToDecimal();
            #region Insert SuperPNROrder 
            SuperPNROrder insertSuperPNROrder = InsuranceServiceController.ToInsertSuperPNROrderDB(superPNRNo, CurrentUserID, totalInsurancePrice, totalInsurancePrice,payment.UseCredit, payment.CreditUsed);
            #endregion

            int SuperPNROrderID = insertSuperPNROrder.OrderID;
            string currencyCode = insertSuperPNROrder.CurrencyCode;
            int superPNRID = insertSuperPNROrder.SuperPNRID;

            #region Insert BookingInsurance
            BookingInsurance bookingInsuranceDBContext = InsuranceServiceController.ToBookingInsuranceDBContext(superPNRNo, quoteRes, CurrentUserID, SuperPNROrderID);
            int bookingInsuranceID = InsuranceServiceController.ToInsertBookingInsurance(bookingInsuranceDBContext);
            #endregion

            #region Inser InsurancePax
            InsuranceServiceController.ToInsertInsurancePaxDBContext(superPNRNo, CurrentUserID, bookingInsuranceID);
            #endregion

            #region insert FeeChargeOrder / payment order 
            var feeChargeOrder = db.FeeChargeOrders.FirstOrDefault(x => x.OrderID == SuperPNROrderID);
            SqlCommand command = new SqlCommand();

            decimal creditUserAvail = 0m;//TC or AC 
            decimal cashCreditBalance = 0m;//TW 
            decimal TWUsed = 0m;//for also insert PaymentOrder
            decimal TCUsed = 0m;//for also insert PaymentOrder
            string payMethod = "";

            if (User.Identity.IsAuthenticated)
            {
                SqlCommand _sqlCommand = new SqlCommand();
                creditUserAvail = MemberServiceController.ChkCreditAmtRemain.ByUserId(CurrentUserID);// CustomPrincipal.UserTypeCode,, _sqlCommand
                cashCreditBalance = MemberServiceController.GetUserCashCredit(CurrentUserID, _sqlCommand);
                _sqlCommand.Connection.Close();
            }

            decimal totalNeedToPayLeft = totalInsurancePrice;

            List<CreditTypes> creditTypeList = new List<CreditTypes>();
            //need uncomment
            creditTypeList.Add(new CreditTypes { CreditType = DiscountType.TW, CreditUsed = payment.EWallet.UseWallet });
            creditTypeList.Add(new CreditTypes { CreditType = DiscountType.TC, CreditUsed = payment.UseCredit });

            PaymentServiceController paymentServiceController = null;
            List<int> paymentOrderInserted = new List<int>();

            //TW TC
            foreach (var creditType in creditTypeList.Where(x => x.CreditUsed))
            {
                bool isVisualPaymentRC = false;
                paymentServiceController = paymentServiceController ?? new PaymentServiceController();
                decimal thisCreditTypeAmount = 0;
                if (CurrentUserID != 0 && creditType.CreditUsed)
                {
                    //bool TCIncAddon = IsAgentUser || Core.IsForStaging;

                    decimal totalAmtAllowToCalcUseableTC = totalNeedToPayLeft;//TCIncAddon ? totalNeedToPayLeft : 0m;
                    decimal thisBookUsableAmt = CalcTravelCreditUsable(totalAmtAllowToCalcUseableTC, creditUserAvail, cashCreditBalance, creditType.CreditType, ProductTypes.Insurance.ToString());

                    if (thisBookUsableAmt > 0)
                    {
                        if (creditType.CreditUsed && (totalNeedToPayLeft <= thisBookUsableAmt)) 
                        {
                            //if paid all then PaymentMethod is below
                            payMethod = creditType.CreditType == DiscountType.TC || creditType.CreditType == DiscountType.AC ?
                                                                        (isAgent ? "AC" : "SC") : "TW";  
                        }

                        if (creditType.CreditType == DiscountType.TW && creditType.CreditUsed)
                        {
                            if(totalNeedToPayLeft <= thisBookUsableAmt) //paid full
                            {
                                TWUsed = thisBookUsableAmt;
                                totalNeedToPayLeft -= thisBookUsableAmt; //remain cost//should be 0
                            }
                            else if(totalNeedToPayLeft > thisBookUsableAmt)
                            {
                                TWUsed = thisBookUsableAmt;
                                totalNeedToPayLeft -= thisBookUsableAmt;//remain cost
                            }
                            thisCreditTypeAmount = thisBookUsableAmt;
                            #region Insert Travel Wallet () Insert temp credit redeem records
                            //Insert temp credit records
                            Temp_UserCashCreditRedeem tempUserCashCredit = paymentServiceController.TempUserCashCreditRedeemPopulate(CurrentUserID, SuperPNROrderID, superPNRID, superPNRNo, TWUsed, currencyCode);
                            paymentServiceController.TempCashCreditRedeemInsert(tempUserCashCredit, command);
                            #endregion

                            isVisualPaymentRC = true;
                        }
                        else if (creditType.CreditType == DiscountType.TC && creditType.CreditUsed)
                        {
                            if (totalNeedToPayLeft <= thisBookUsableAmt) //paid full
                            {
                                TCUsed = thisBookUsableAmt;
                                totalNeedToPayLeft -= thisBookUsableAmt; //remain cost//should be 0
                            }
                            else if (totalNeedToPayLeft > thisBookUsableAmt)
                            {
                                TCUsed = thisBookUsableAmt;
                                totalNeedToPayLeft -= thisBookUsableAmt;//remain cost
                            }
                            thisCreditTypeAmount = thisBookUsableAmt;
                            #region Insert Travel Credit () Insert temp credit redeem records
                            //Insert temp credit records
                            if (!isAgent) // how is is agent?
                            {
                                string mainProductType = "OTH";

                                Temp_UserCreditRedeem tempUserCredit = paymentServiceController.TempUserCreditRedeemPopulate(SuperPNROrderID, CurrentUserID, TCUsed, mainProductType, currencyCode);
                                paymentServiceController.TempCreditRedeemInsert(tempUserCredit, command);
                            }
                            #endregion

                            isVisualPaymentRC = true;
                        }
                    }
                }

                if (isVisualPaymentRC)
                {
                    PaymentOrder _paymentOrderInsert = new PaymentOrder
                    {
                        OrderID = SuperPNROrderID,
                        PaymentDate = DateTime.Now,
                        PaymentMethodCode = creditType.CreditType == DiscountType.TC || creditType.CreditType == DiscountType.AC ? (isAgent ? "AC" : "SC") : "TW",
                        PaymentStatusCode = "PEND",
                        CurrencyCode = currencyCode,
                        PaymentAmount = thisCreditTypeAmount,
                        ImagePath = string.Empty,
                        Ipay88RefNo = string.Empty,
                        Ipay88TransactionID = string.Empty,
                        CreatedByID = CurrentUserID,
                        ModifiedByID = CurrentUserID
                    };

                    paymentOrderInserted.Add(PaymentServiceController.InsertSuperPNRPaymentOrder(_paymentOrderInsert, command));
                }
            }

            var superPNRFromDB = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRNo == superPNRNo);
            var contactPerson = superPNRFromDB.Bookings.FirstOrDefault().Paxes.FirstOrDefault(x => x.IsContactPerson == true);

            string iPay88RefNo = superPNRID + " - " + superPNRNo; // Important, cannot simply change, will cause cannot requery fail.
            var test = payment.PaymentMethod;
            bool isFpxUsed = payment.PaymentMethod == "IPAFPX";
            bool isAdendUsed = payment.PaymentMethod == "ADYENC";
            bool isIpaCCUsed = payment.PaymentMethod == "IPACC";
            //ipacc

            // Normal Payment 
            // Insert for Not Full Store Credit/Cash Wallet 
            if (totalNeedToPayLeft > 0)
            {
                //need uncomment
                if (isFpxUsed == true || isAdendUsed == true || isIpaCCUsed == true)
                {
                    ProcessingFee pfOnCount = null;
                    if (isFpxUsed)
                    {
                        payMethod = payment.PaymentMethod;
                        pfOnCount = new ProcessingFee(payMethod, totalInsurancePrice - (TCUsed + TWUsed), ProductTypes.Insurance, new PromoCodeFunctions(), currencyCode);
                        totalNeedToPayLeft = totalNeedToPayLeft + pfOnCount.Amt + pfOnCount.GST; //should be no charge on PF and GST
                    }
                    else if (isAdendUsed)
                    {
                        payMethod = payment.PaymentMethod;
                        //need count&add proccessing fee
                        pfOnCount = new ProcessingFee(payMethod, totalInsurancePrice - (TCUsed + TWUsed), ProductTypes.Insurance, new PromoCodeFunctions(), currencyCode);
                        //add the ProcessFee and GST to last total need to paid
                        totalNeedToPayLeft = totalNeedToPayLeft + pfOnCount.Amt + pfOnCount.GST;
                    }
                    else if (isIpaCCUsed)
                    {
                        payMethod = payment.PaymentMethod;
                        pfOnCount = new ProcessingFee(payMethod, totalInsurancePrice - (TCUsed + TWUsed), ProductTypes.Insurance, new PromoCodeFunctions(), currencyCode);
                        totalNeedToPayLeft = totalNeedToPayLeft + pfOnCount.Amt + pfOnCount.GST;
                    }


                    PaymentOrder paymentOrder = PaymentServiceController.PopulatePaymentPaymentOrder(SuperPNROrderID, currencyCode, iPay88RefNo, 0,
                        totalNeedToPayLeft, "PEND", payMethod, CurrentUserID);
                    paymentOrderInserted.Add(PaymentServiceController.InsertSuperPNRPaymentOrder(paymentOrder, command));

                    #region insert fee Charge part for IPAFPX/ADYENC/IPACC
                    var latestFeeCharge = new FeeChargeOrder
                    {
                        OrderID = SuperPNROrderID,
                        CreatedByID = CurrentUserID,
                        CurrencyCode = currencyCode,
                        FeeChargeAmount = pfOnCount.Amt,
                        FeeCode = string.IsNullOrWhiteSpace(pfOnCount.FeeCode) ? "SC" : pfOnCount.FeeCode,
                        TaxCode = string.IsNullOrWhiteSpace(pfOnCount.TaxCode) ? "ES" : pfOnCount.TaxCode,
                        TaxAmount = pfOnCount.GST,
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
                }
                else
                {
                    //TC TW not paid full but no others payment
                    ModelState.AddModelError("Error", "Please Select Payment Method");
                    return View(checkoutProduct);
                }
            }
            else
            {
                #region insert fee charge part for TW TC AC
                decimal TTlDisc_Amt = -(TCUsed + TWUsed); // how many used with TC/TW will be negative
                var pf = new ProcessingFee(payMethod, totalInsurancePrice + TTlDisc_Amt, ProductTypes.Insurance, new PromoCodeFunctions(), currencyCode);

                var latestFeeCharge = new FeeChargeOrder
                {
                    OrderID = SuperPNROrderID,
                    CreatedByID = CurrentUserID,
                    CurrencyCode = currencyCode,
                    FeeChargeAmount = pf.Amt,
                    FeeCode = string.IsNullOrWhiteSpace(pf.FeeCode) ? "SC" : pf.FeeCode,//not sure on this
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
            }
            command.Transaction.Commit();
            #endregion

            PaymentSubmitModels iPayModel = PaymentController.PopulatePaymentSubmitModel(DateTime.Now, superPNRID, superPNRNo, currencyCode, totalNeedToPayLeft, payMethod, contactPerson.Phone1, contactPerson.PassengerEmail, contactPerson.FullName);
            
            #region Payment Gateway Redirect
            CheckoutController checkoutCon = new CheckoutController();
            PaymentController pc = new PaymentController();
            string clientIP = HttpContext.Request.UserHostAddress;
            string paymentMethodGR = payMethod.ToUpper();
            string token = superPNRID.ToString() + "," + superPNRNo;
            string encToken = General.CustomizeBaseEncoding.CodeBase64(token);
            string encPaymentOrderIDList = Cryptography.AES.Encrypt(paymentOrderInserted.JoinToString(","));

            FormCollection form = new FormCollection();
            adyenCaptureResponseModels captureResponseModels2 = new adyenCaptureResponseModels();
            iPayCaptureResponseModels captureResponseModels = new iPayCaptureResponseModels
            { Status = "1", Amount = iPayModel.PaymentAmount, TransId = "" };

            //make a url for return when payment fail 
            string returnURL = Url.Action("InsurancePayment", "Insurance", new { superPNRNo});

            //creditCard detail get from PaymentSummaryForm
            var creditCard = new CreditCard()
            {
                CardholderName = creditCardPost.CardholderName,
                CardType = creditCardPost.CardType ,
                CVC = creditCardPost.CVC,
                CreditCardNo = creditCardPost.CreditCardNo,
                GenerationTime = creditCardPost.GenerationTime,
                ExpMonths = creditCardPost.ExpMonths,
                ExpYear = creditCardPost.ExpYear
            };

            TempData["CheckoutProduct"] = checkoutProduct;

            switch (payMethod.ToLower())
            {
                case "sc":
                    form.Add("Status", captureResponseModels.Status);
                    form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                    return await checkoutCon.PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, "", encPaymentOrderIDList);
                case "ac":
                    form.Add("Status", captureResponseModels.Status);
                    form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                    return await checkoutCon.PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, "", encPaymentOrderIDList);
                case "tw":
                    form.Add("Status", captureResponseModels.Status);
                    form.Add("Amount", captureResponseModels.Amount.ToString("n2"));

                    return await checkoutCon.PaymentCheckOut(form, captureResponseModels, captureResponseModels2, encToken, "", encPaymentOrderIDList);
                case "ipacc":
                    return pc.iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, string.Empty, paymentOdToken = encPaymentOrderIDList, returnURL }, Request.Url.Scheme), iPayModel, true);
                case "ipafpx":
                    return pc.iPay88CheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, string.Empty, paymentOdToken = encPaymentOrderIDList, returnURL }, Request.Url.Scheme), iPayModel);
                case "adyenc":
                    AdyenCardPaymentModels adyenModel = PaymentController.PopulateAdyenPaymentSubmitModel(superPNRID, Request.Url.Scheme, superPNRNo, currencyCode, totalNeedToPayLeft, contactPerson.PassengerEmail, creditCard);
                    return pc.AdyenCheckOut(Url.Action("PaymentCheckOut", "Checkout", new { token = encToken, string.Empty, paymentOdToken = encPaymentOrderIDList, returnURL }, Request.Url.Scheme), adyenModel, Request.Form);
                default:
                    ModelState.AddModelError("Error", "Payment Method Not Found.");
                    break;
            }
            #endregion
            return View(checkoutProduct);
        }

        public BookedProductView GetBookedDetail(string bookingID)
        {
            if (string.IsNullOrWhiteSpace(bookingID))
            {
                return null;// RedirectToAction("NotFound", "Error");
            }

            string superPNRNo = bookingID;

            ViewBag.ReturnUrl = Url.Action("OrderHistory", "Flight");

            BookingConfirmationDetail model = new BookingConfirmationDetail();
            int userid = CurrentUserID;

            try
            {
                List<string> statusToDisplayBk = new List<string> { "CON", "QPL", "TKI", "RHI", "HTP" };
                MayFlower db = new MayFlower();
                Booking fltBk = db.Bookings.FirstOrDefault(x => x.SuperPNRNo == superPNRNo);
                SuperPNROrder sPO = fltBk.SuperPNR.SuperPNROrders.FirstOrDefault(x => x.OrderID == fltBk.OrderID);
                model.ConfirmationOutputDb = fltBk;

                if (!IsSelfBookingOrGuest(userid, fltBk.UserID))
                {
                    string statusQuery = Request.QueryString["status"] != null ? Request.QueryString["status"].ToString() : string.Empty;

                    if (string.IsNullOrEmpty(statusQuery) || statusQuery != "success")
                    {
                        return null;//RedirectToAction("Index", "Home");
                    }
                }

                if (statusToDisplayBk.Any(x => x == sPO.BookingStatusCode))
                {
                    model = Alphareds.Module.BookingController.BookingServiceController.getBookingDetailPage(model, userid, db);

                    if (sPO.BookingStatusCode == "HTP" && fltBk.SupplierCode == "SBRE")
                    {
                        sPO.SuperPNR.LoadPaymentDetailList(true);
                        var pDtl = sPO.SuperPNR.PaymentDetailsList.FirstOrDefault();
                        ViewBag.PaymentDetails = new PaymentCheckout()
                        {
                            AvailableCredit = MemberServiceController.ChkCreditAmtRemain.ByUserId(CustomPrincipal.UserId, CustomPrincipal.UserTypeCode ?? "GT"),
                            PaymentMethod = "IPAFPX",
                            CreditTerm = CustomPrincipal.CreditTerm,
                            CreditCard = new CreditCard(),
                            PaymentCurrencyCode = pDtl.CurrencyCode,
                            GrandTotalForPayment = pDtl.TotalPendingAmount,
                        };

                        var _tempBook = fltBk.Temp_BookingInfo.LastOrDefault(x => x.Method == "response");
                        Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse _previousOutput = null;

                        try
                        {
                            _previousOutput = JsonConvert.DeserializeObject<Alphareds.Module.SabreWebService.SWS.BookFlightEnhancedAirBookResponse>(_tempBook.BookingInfo);
                            bool expiredBooking = false;

                            // Reduce 30 minutes from last ticket time.
                            if (_previousOutput.Output.IsAirlineReturnedLastTicketingDate)
                            {
                                expiredBooking = DateTime.Now.AddMinutes(-30) > _previousOutput.Output.LastTicketingDate;
                                // Service provided date.
                                ViewBag.ServiceRemarkMsg = $"Last ticketing date: { _previousOutput.Output.LastTicketingDate.ToString("dd MMM yyyy HH:mm")}";
                            }
                            else
                            {
                                // Mayflower system generated expiry date.
                                var _dumpDate = fltBk.BookingExpiryDate ?? fltBk.CreateDateTime.AddMinutes(30);
                                expiredBooking = DateTime.Now.AddMinutes(-30) > _dumpDate;
                                ViewBag.ServiceRemarkMsg = $"Last ticketing date: { _dumpDate.ToString("dd MMM yyyy HH:mm")}";
                            }

                            if (expiredBooking)
                            {
                                fltBk.BookingStatusCode = "EXP";
                                fltBk.ModifiedDate = DateTime.Now;
                                fltBk.ModifiedDateUTC = DateTime.UtcNow;

                                fltBk.SuperPNR.LoadPaymentDetailList(true, "MYR");
                                var _allPayment = fltBk.SuperPNR.PaymentDetailsList.FirstOrDefault(); // converted currency only one record
                                if (_allPayment.TotalPaidAmount == 0)
                                {
                                    fltBk.SuperPNR.SuperPNROrders.ForEach(x =>
                                    {
                                        x.BookingStatusCode = "EXP";
                                        x.ModifiedDate = DateTime.Now;
                                        x.ModifiedDateUTC = DateTime.UtcNow;
                                    });
                                }

                                db.SaveChanges();
                                return null;// RedirectToAction("upcomingtrips", "account", new { repaystatus = "flight-expired" });
                            }
                        }
                        catch
                        {
                        }
                    }

                    // 2014/04/09 - Cross-sell model initialize.
                    var hotelModel = model.ConfirmationOutputDb.SuperPNR.BookingHotels;
                    BookedProductView viewModel = new BookedProductView
                    {
                        Flight = model,
                        Hotel = hotelModel != null ? hotelModel.FirstOrDefault() : null,
                    };
                    return viewModel;
                }
                else
                {
                    return null;//RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                Tuple<string, string> errorCodes = UtilitiesService.NlogExceptionForBookingFlow(logger, null, ex, userid, "homesearchflighterror", "", "",
                    $"Requested Flight SuperPNRNo - {bookingID}");

                return null;//RedirectToRoute("BookingPayment");
            }
        }


        public List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote> QuoteInsurance(string superPNRNo) //Quote in here
        {
            MayFlower db = new MayFlower();
            List<Alphareds.Module.ACEInsuranceWebService.ACEIns.Quote> res = null;

            try
            {
                var superPNR = db.SuperPNRs.FirstOrDefault(x => x.SuperPNRNo == superPNRNo);
                var flightData = superPNR.Bookings.FirstOrDefault();
                res = Alphareds.Module.ServiceCall.ACEInsuranceServiceCall.AddGetTravelQuote(flightData);
                Alphareds.Module.Model.CrossSellItemsAvailable.InsuranceInformation insuranceInfor = new CrossSellItemsAvailable.InsuranceInformation
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
                                + "Destination - From :" + flightData.Origin + " , To :" + flightData.Destination + Environment.NewLine
                                + "Date - From :" + flightData.FlightSegments.FirstOrDefault(x => x.SegmentOrder == "O1").DepartureDateTime.ToString() + " , To :" +  flightData.FlightSegments.FirstOrDefault(x => x.SegmentOrder == "I1").DepartureDateTime.ToString() ?? flightData.FlightSegments.FirstOrDefault(x => x.SegmentOrder == "O1").DepartureDateTime.ToString()
                                + Environment.NewLine + Environment.NewLine
                                + JsonConvert.SerializeObject(item, Formatting.Indented));
                        }
                        // Break out avoid continue to next line code while looping
                        break;
                    }

                    var quoteStatus = item.Body.GetTravelQuoteResponse.ACORD.InsuranceSvcRs.PersPkgPolicyQuoteInqRs.MsgStatus;
                    bool isQuoteSuccess = quoteStatus.MsgStatusCd == "Success";

                    if (isQuoteSuccess)
                    {
                        return res;
                    }
                    else
                    {
                        logger.Error("Quote Insurance not success. "
                                + "Destination - From :" + flightData.Origin + " , To :" + flightData.Destination + Environment.NewLine
                                + "Date - From :" + flightData.FlightSegments.FirstOrDefault(x => x.SegmentOrder == "O1").DepartureDateTime.ToString() + " , To :" + flightData.FlightSegments.FirstOrDefault(x => x.SegmentOrder == "I1").DepartureDateTime.ToString() ?? flightData.FlightSegments.FirstOrDefault(x => x.SegmentOrder == "O1").DepartureDateTime.ToString()
                                + "Status Message :" + quoteStatus.MsgStatusDesc);

                        return null;
                    }
                }

            }
            catch (AggregateException ae)
            {
                logger.Error(ae.GetBaseException(), "Error when add quote insurance.");
            }
            catch (Exception ex)
            {
                logger.Error(ex.GetBaseException(), "Error when add quote insurance.");
            }
            return null;
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

        private int CurrentUserID
        {
            get
            {
                int userid = 0;
                int.TryParse(User.Identity.Name, out userid);
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

        private bool IsSelfBookingOrGuest(int userLogged, int userIDBooking)
        {
            return userIDBooking == 0 || userLogged == userIDBooking ? true : false;
        }

        [HttpPost]
        [SessionFilter(SessionName = "CheckoutProduct")]
        public CheckoutProduct UpdatePayment(CheckoutProduct checkout, string method, bool useCredit = false, bool useCashCredit = false)
        {
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

            if (Core.IsEnablePackageDiscount && checkout.IsDynamic && checkout.TotalPrdDisc == null)
            {
                checkout.TotalPrdDisc = new List<ProductPricingDetail>();
                checkout.TotalPrdDisc.Add(new ProductPricingDetail()
                {
                    Discounts = new List<DiscountDetail>()
                });
            }
            foreach (var product in checkout.Products)
            {
                product.PricingDetail.Discounts.RemoveAll(x => x.DiscType == DiscountType.TC || x.DiscType == DiscountType.TW);
            }

            bool isSingleDiscount = false;
            if ((!checkout.PromoCodeFunctions.GetFrontendFunction.WaiveCreditCardFee) // Special promo code, must TRUE
                && (!checkout.PromoCodeFunctions.GetFrontendFunction.AllowWithTC && checkout.PaymentDetails.UseCredit)
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
                        decimal totalAmtAllowToCalcUseableTC = (checkout.CheckOutSummary.SubTtl + checkout.CheckOutSummary.TTlDisc_Amt)
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
                            if (creditUserAvail >= thisBookUsableAmt && creditType.CreditUsed && ((checkout.CheckOutSummary.SubTtl + checkout.CheckOutSummary.TTlDisc_Amt) <= thisBookUsableAmt))
                            {
                                checkout.PaymentDetails.PaymentMethod = creditType.CreditType == DiscountType.TC || creditType.CreditType == DiscountType.AC ?
                                                                            (isAgent ? "ac" : "sc") : "tw";
                            }

                            var tcDiscountDetail = new DiscountDetail
                            {
                                Seq = 1,
                                DiscType = creditType.CreditType,
                                Disc_Desc = creditType.CreditType == DiscountType.TC ? "Travel Credit" : "Travel Wallet",
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

            string _returnMsg = isSingleDiscount ? "Unable use travel credit with promo code"
                      : (useCredit && CurrentUserID == 0 ? "Please relogin your account." : null);
            var obj = GetPaymentDetailsJson(checkout, _returnMsg);

            //return Content(JsonConvert.SerializeObject(obj), "application/json");
            return checkout;
        }

        #region update payment from script
        [HttpPost]
        public ActionResult UpdatePaymentStatus(string superPNRNo, string method, bool useCredit = false, bool useCashCredit = false)
        {
            CheckoutProduct checkoutProduct = (CheckoutProduct)TempData["CheckoutProduct"];

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
            if (checkoutProduct.PaymentDetails == null)
            {
                checkoutProduct.PaymentDetails = new PaymentCheckout()
                {
                    PaymentMethod = "IPAFPX",
                };
            }
            checkoutProduct.PaymentDetails.AvailableCredit = creditUserAvail;
            checkoutProduct.PaymentDetails.UseCredit = useCredit;
            checkoutProduct.PaymentDetails.CreditUsed = 0;
            checkoutProduct.PaymentDetails.PaymentMethod = method;
            checkoutProduct.PaymentDetails.EWallet = new EWallet
            {
                BalanceAmt = cashCreditBalance,
                UseAmt = 0,
                UseWallet = useCashCredit,
            };

            if (Core.IsEnablePackageDiscount && checkoutProduct.IsDynamic && checkoutProduct.TotalPrdDisc == null)
            {
                checkoutProduct.TotalPrdDisc = new List<ProductPricingDetail>();
                checkoutProduct.TotalPrdDisc.Add(new ProductPricingDetail()
                {
                    Discounts = new List<DiscountDetail>()
                });
            }
            foreach (var product in checkoutProduct.Products)
            {
                product.PricingDetail.Discounts.RemoveAll(x => x.DiscType == DiscountType.TC || x.DiscType == DiscountType.TW);
            }

            bool isSingleDiscount = false;
            if ((!checkoutProduct.PromoCodeFunctions.GetFrontendFunction.WaiveCreditCardFee) // Special promo code, must TRUE
                && (!checkoutProduct.PromoCodeFunctions.GetFrontendFunction.AllowWithTC && checkoutProduct.PaymentDetails.UseCredit)
                && (checkoutProduct.PromoID != 0 || checkoutProduct.CheckOutSummary.DiscountDetails.Any(x => x.DiscType == DiscountType.CODE)))
            {
                //Block travel credit when having promo code
                isSingleDiscount = true;
                checkoutProduct.PaymentDetails.UseCredit = false;
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
                        decimal totalAmtAllowToCalcUseableTC = (checkoutProduct.CheckOutSummary.SubTtl + checkoutProduct.CheckOutSummary.TTlDisc_Amt)
                            - (TCIncAddon ? 0m : checkoutProduct.CheckOutSummary.TtlAddOnAmount);

                        if (creditType.CreditType == DiscountType.TW)
                        {
                            // add back deducted add on amount
                            if (checkoutProduct.CheckOutSummary.TtlAddOnAmount > cashCreditBalance)
                            {
                                totalAmtAllowToCalcUseableTC += cashCreditBalance;
                            }
                            else
                            {
                                totalAmtAllowToCalcUseableTC += (TCIncAddon ? 0m : checkoutProduct.CheckOutSummary.TtlAddOnAmount);
                            }
                        }

                        decimal thisBookUsableAmt = CalcTravelCreditUsable(totalAmtAllowToCalcUseableTC, creditUserAvail, cashCreditBalance, creditType.CreditType, checkoutProduct.MainProductType.ToString());

                        if (thisBookUsableAmt > 0)
                        {
                            if (creditUserAvail >= thisBookUsableAmt && creditType.CreditUsed && ((checkoutProduct.CheckOutSummary.SubTtl + checkoutProduct.CheckOutSummary.TTlDisc_Amt) <= thisBookUsableAmt))
                            {
                                checkoutProduct.PaymentDetails.PaymentMethod = creditType.CreditType == DiscountType.TC || creditType.CreditType == DiscountType.AC ?
                                                                            (isAgent ? "ac" : "sc") : "tw";
                            }

                            var tcDiscountDetail = new DiscountDetail
                            {
                                Seq = 1,
                                DiscType = creditType.CreditType,
                                Disc_Desc = creditType.CreditType == DiscountType.TC ? "Travel Credit" : "Travel Wallet",
                                Disc_Amt = thisBookUsableAmt
                            };

                            if (Core.IsEnablePackageDiscount && checkoutProduct.IsDynamic)
                            {
                                checkoutProduct.TotalPrdDisc.FirstOrDefault().DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkoutProduct.Flight != null) // flight only
                            {
                                tcDiscountDetail.PrdType = ProductTypes.Flight;
                                checkoutProduct.Flight.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkoutProduct.Flight == null && checkoutProduct.Hotel != null) // hotel only
                            {
                                tcDiscountDetail.PrdType = ProductTypes.Hotel;
                                checkoutProduct.Hotel.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }
                            else if (checkoutProduct.Flight == null && checkoutProduct.Insurance != null) //insurance only
                            {
                                tcDiscountDetail.PrdType = ProductTypes.Insurance;
                                checkoutProduct.Insurance.PricingDetail.DiscountInsert(tcDiscountDetail);
                            }

                            if (creditType.CreditType == DiscountType.TC)
                            {
                                // Update Travel Credit Used
                                checkoutProduct.PaymentDetails.AvailableCredit -= Math.Abs(tcDiscountDetail.Disc_Amt);
                                checkoutProduct.PaymentDetails.CreditUsed = Math.Abs(tcDiscountDetail.Disc_Amt);
                            }
                            else
                            {
                                // Update Travel Wallet Used
                                checkoutProduct.PaymentDetails.EWallet.BalanceAmt -= Math.Abs(tcDiscountDetail.Disc_Amt);
                                checkoutProduct.PaymentDetails.EWallet.UseAmt = Math.Abs(tcDiscountDetail.Disc_Amt);
                            }
                        }
                    }
                }
            }

            string _returnMsg = isSingleDiscount ? "Unable use travel credit with promo code"
                      : (useCredit && CurrentUserID == 0 ? "Please relogin your account." : null);
            var obj = GetPaymentDetailsJson(checkoutProduct, _returnMsg);
            TempData["CheckoutProduct"] = checkoutProduct;
            return Content(JsonConvert.SerializeObject(obj), "application/json");
        }
        #endregion

        private dynamic GetPaymentDetailsJson(CheckoutProduct checkout, dynamic obj = null, string returnMsg = null)
        {
            obj = obj ?? new System.Dynamic.ExpandoObject();

            bool useCredit = checkout.CheckOutSummary.DiscountDetails
                .Where(x => x.DiscType == DiscountType.TC && x.Disc_Amt > 0)
                .Sum(s => s.Disc_Amt) > 0;

            bool useCashCredit = checkout.CheckOutSummary.DiscountDetails
                .Where(x => x.DiscType == DiscountType.TW && x.Disc_Amt > 0)
                .Sum(s => s.Disc_Amt) > 0;

            bool isFullCredit = checkout.CheckOutSummary.GrandTtlAmt == 0;

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
            obj.awc = !(isFullCredit && useCashCredit && !useCredit);
            return obj;
        }

        private int AgentCreditTerm(int userID)
        {
            MayFlower db = new MayFlower();
            Alphareds.Module.Model.Database.User user = Alphareds.Module.Common.Core.GetUserInfo(CurrentUserID.ToString(), db);

            int value = 0;
            if (User.Identity.IsAuthenticated)
            {
                value = db.Organizations.FirstOrDefault(x => x.OrganizationID == user.OrganizationID).CreditTermInDay.HasValue ? db.Organizations.FirstOrDefault(x => x.OrganizationID == user.OrganizationID).CreditTermInDay.Value : 0;
            }
            return value;
        }
    }
}