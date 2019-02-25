using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.PaymentController;
using PaymentQueueHandler.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentQueueHandler.Components
{
    public sealed class BookingList
    {
        MayFlower MayFlowerDB { get; set; }
        SqlCommand MaySqlCommand { get; set; }
        public List<string> InformationCaution { get; set; }
        public bool CommandInitInside { get; set; }

        public BookingList(MayFlower _dbContext, SqlCommand _sqlCommand)
        {
            CommandInitInside = _sqlCommand == null;

            MayFlowerDB = _dbContext ?? new MayFlower();
            MaySqlCommand = _sqlCommand ?? new SqlCommand();
        }

        public IEnumerable<SuperPNR> GetBookingPaid(string superPNRNo, bool chkFlight, bool chkHotel, bool chkAddOnBook)
        {
            return GetAllBookingPaidRepository(chkFlight, chkHotel, chkAddOnBook, superPNRNo);
        }

        public IEnumerable<SuperPNR> GetAllBookingPaid(bool chkFlight, bool chkHotel, bool chkAddOnBook)
        {
            return GetAllBookingPaidRepository(chkFlight, chkHotel, chkAddOnBook);
        }

        private IEnumerable<SuperPNR> GetAllBookingPaidRepository(bool chkFlight, bool chkHotel, bool chkAddOnBook, string superPNRNo = null)
        {
            IQueryable<PaymentOrder> paymentOrders = null;

            if (superPNRNo == null)
            {
                paymentOrders = PaymentCheck.DBQuery.GetOrdersRHI(MayFlowerDB);
            }
            else
            {
                paymentOrders = PaymentCheck.DBQuery.GetOrdersRHI(MayFlowerDB, superPNRNo);
            }

            List<Task<RequeryStatus.Requery>> paymentResultList = new List<Task<RequeryStatus.Requery>>();

            foreach (var item in paymentOrders)
            {
                if (item.PaymentMethodCode.ToLower().StartsWith("ipa"))
                {
                    if (string.IsNullOrWhiteSpace(item.Ipay88RefNo))
                    {
                        InformationCaution = InformationCaution ?? new List<string>();
                        InformationCaution.Add(string.Format("PaymentOrders PaymentID {0} :- {1}", item.PaymentID, "Ref No is NULL cannot requery."));
                        item.RequeryStatusCode = "ERR";
                    }
                    else
                    {
                        paymentResultList.Add(PaymentCheck.ServiceQuery.IPAY88
                            .CheckPaymentPAIDAsync(item, item.SuperPNROrder.SuperPNRID, item.SuperPNROrder.SuperPNR.SuperPNRNo));
                    }
                }
                else if (item.PaymentMethodCode.ToLower().StartsWith("ady"))
                {
                    if (string.IsNullOrWhiteSpace(item.Ipay88RefNo))
                    {
                        InformationCaution = InformationCaution ?? new List<string>();
                        InformationCaution.Add(string.Format("PaymentOrders PaymentID {0} :- {1}", item.PaymentID, "Ref No is NULL cannot requery."));
                        item.RequeryStatusCode = "ERR";
                    }
                    else
                    {
                        paymentResultList.Add(PaymentCheck.ServiceQuery.ADYEN
                            .CheckPaymentPAIDAsync(item, item.SuperPNROrder.SuperPNRID, item.SuperPNROrder.SuperPNR.SuperPNRNo));
                    }
                }
            }

            var paymentResult = Task.WhenAll(paymentResultList).ConfigureAwait(false).GetAwaiter().GetResult();

            if (paymentResult != null && paymentResult.Length > 0)
            {
                List<ItemCheck> itemBookStatus = new List<ItemCheck>();

                foreach (var item in paymentResult)
                {
                    itemBookStatus = itemBookStatus.Union(PaymentStatusRequeryHandler(paymentOrders, item, chkFlight, chkHotel, chkAddOnBook, superPNRNo)).ToList();
                }

                foreach (var item in itemBookStatus)
                {
                    bool validFlight = (!item.Flight.HasValue || (item.Flight.HasValue && item.Flight.Value == ItemCheck.ItemStatus.CON));
                    bool validHotel = (!item.Hotel.HasValue || (item.Hotel.HasValue && item.Hotel.Value == ItemCheck.ItemStatus.CON));
                    bool validInsurance = (!item.Insurance.HasValue || (item.Insurance.HasValue && item.Insurance.Value == ItemCheck.ItemStatus.CON));
                    bool validEventProducts = (!item.EventProducts.HasValue || (item.EventProducts.HasValue && item.EventProducts.Value == ItemCheck.ItemStatus.CON));

                    if (validFlight && validHotel && validInsurance && validEventProducts)
                    {
                        if (item.InformationCaution != null && item.InformationCaution.Any(x => x.Contains("Passed 7 days for capture")))
                        {

                        }
                        else
                        {
                            yield return item.SuperPNR;
                        }
                    }
                }

                var msg = itemBookStatus.Where(x => x.InformationCaution != null).SelectMany(x => x.InformationCaution);

                if (msg != null && msg.Count() > 0)
                {
                    InformationCaution = InformationCaution ?? new List<string>();
                    InformationCaution.AddRange(msg);
                    InformationCaution = InformationCaution.Distinct().OrderBy(x => x).ToList();
                }
            }
        }

        public async Task CheckUpdatePendingPayment(bool chkFlight, bool chkHotel, bool chkAddOnBook, string superPNRNo = null)
        {
            IQueryable<PaymentOrder> paymentOrders = null;

            if (superPNRNo == null)
            {
                paymentOrders = PaymentCheck.DBQuery.GetPaymentsPEND(MayFlowerDB);
            }
            else
            {
                paymentOrders = PaymentCheck.DBQuery.GetPaymentsPEND(MayFlowerDB, superPNRNo);
            }

            List<Task<RequeryStatus.Requery>> paymentResultList = new List<Task<RequeryStatus.Requery>>();

            foreach (var item in paymentOrders.Where(x => x.PaymentMethodCode.ToLower() != "sc").Take(40))
            {
                if (item.PaymentMethodCode.ToLower().StartsWith("ipa"))
                {
                    if (string.IsNullOrWhiteSpace(item.Ipay88RefNo))
                    {
                        InformationCaution = InformationCaution ?? new List<string>();
                        InformationCaution.Add(string.Format("PaymentOrders PaymentID {0} :- {1}", item.PaymentID, "Ref No is NULL cannot requery."));
                        item.RequeryStatusCode = "ERR";
                    }
                    else
                    {
                        paymentResultList.Add(PaymentCheck.ServiceQuery.IPAY88
                            .CheckPaymentPAIDAsync(item, item.SuperPNROrder.SuperPNRID, item.SuperPNROrder.SuperPNR.SuperPNRNo));
                    }
                }
                else if (item.PaymentMethodCode.ToLower().StartsWith("ady"))
                {
                    if (string.IsNullOrWhiteSpace(item.Ipay88RefNo))
                    {
                        InformationCaution = InformationCaution ?? new List<string>();
                        InformationCaution.Add(string.Format("PaymentOrders PaymentID {0} :- {1}", item.PaymentID, "Ref No is NULL cannot requery."));
                        item.RequeryStatusCode = "ERR";
                    }
                    else
                    {
                        paymentResultList.Add(PaymentCheck.ServiceQuery.ADYEN
                            .CheckPaymentPAIDAsync(item, item.SuperPNROrder.SuperPNRID, item.SuperPNROrder.SuperPNR.SuperPNRNo));
                    }
                }
                else if (item.PaymentMethodCode.ToLower() == "tw")
                {
                    paymentResultList.Add(PaymentCheck.StoreQuery.TravelWallet
                        .CheckPaymentPAIDAsync(item, item.SuperPNROrder.SuperPNRID, item.SuperPNROrder.SuperPNR.SuperPNRNo, MayFlowerDB));
                }
            }

            //var paymentResult = await Task.WhenAll(paymentResultList);
            List<ItemCheck> itemBookStatus = new List<ItemCheck>();

            while (paymentResultList.Count > 0)
            {
                var itemTsk = await Task.WhenAny(paymentResultList);
                var item = await itemTsk;

                if (item != null)
                {
                    itemBookStatus = itemBookStatus.Union(PaymentStatusRequeryHandler(paymentOrders, item, chkFlight, chkHotel, chkAddOnBook, superPNRNo)).ToList();

                    var msg = itemBookStatus.Where(x => x.InformationCaution != null).SelectMany(x => x.InformationCaution);

                    if (msg != null && msg.Count() > 0)
                    {
                        InformationCaution = InformationCaution ?? new List<string>();
                        InformationCaution.AddRange(msg);
                        InformationCaution = InformationCaution.Distinct().OrderBy(x => x).ToList();
                    }
                }

                paymentResultList.Remove(itemTsk);
            }

        }

        private List<ItemCheck> PaymentStatusRequeryHandler(IQueryable<PaymentOrder> paymentOrders, RequeryStatus.Requery item, bool chkFlight, bool chkHotel, bool chkAddOnBook, string superPNRNo = null)
        {
            var itemBookStatus = new List<ItemCheck>();

            if (item.Status && item.Desc != "Voided")
            {
                var _newItemCheck = new ItemCheck(item.SuperPNRID, MayFlowerDB, true, chkFlight, chkHotel, chkAddOnBook);
                itemBookStatus.Add(_newItemCheck);
            }
            else if (!item.Status && item.Desc == "Limited by per day maximum number of requery")
            {
                foreach (var payment in paymentOrders.Where(x => x.SuperPNROrder.OrderID == item.OrderID))
                {
                    InformationCaution = InformationCaution ?? new List<string>();
                    InformationCaution.Add(string.Format("PaymentOrders PaymentID {0} :- {1}", payment.PaymentID, item.Desc));
                    payment.RequeryStatusCode = "ERR";
                }
            }
            else if (!item.Status)
            {
                /* Functions:
                 * For handle payment failed record, update all related components to failed.
                 * -- For iPay close browser at payment gateway will return 'Record not found'.
                 */

                var ordersList = paymentOrders.Where(x => x.SuperPNROrder.OrderID == item.OrderID);
                foreach (var payment in ordersList)
                {
                    if (payment.PaymentStatusCode == "PAID" || payment.PaymentStatusCode == "CAPTURED")
                    {
                        InformationCaution = InformationCaution ?? new List<string>();
                        InformationCaution.Add(string.Format("PaymentOrders PaymentID {0} :- {1}", payment.PaymentID, item.Desc));
                        payment.RequeryStatusCode = "ERR";
                    }
                    else if (item.Desc == "Record not found")
                    {
                        InformationCaution = InformationCaution ?? new List<string>();
                        InformationCaution.Add(string.Format("SuperPNR {0} - {1}: Requery result as '{2}'.",
                            item.SuperPNRID, item.SuperPNRNo, item.Desc));
                        payment.RequeryStatusCode = "FAIL";
                        payment.PaymentStatusCode = "FAIL";
                    }
                    else if (item.Desc == "Voided")
                    {
                        payment.RequeryStatusCode = "FAIL";
                        payment.PaymentStatusCode = "VOID";
                    }
                    else
                    {
                        payment.RequeryStatusCode = "FAIL";
                        payment.PaymentStatusCode = "FAIL";
                    }

                    if (payment.RequeryStatusCode != "ERR")
                    {
                        string _payMethod = payment.PaymentMethodCode?.ToLower();

                        if (_payMethod == "tw")
                        {
                            // Remove holded travel wallet
                            new PaymentServiceController().TempCashCreditRedeemDelete(payment.OrderID, payment.CreatedByID,
                                payment.CurrencyCode, payment.PaymentAmount, MaySqlCommand);
                        }
                        else if (_payMethod == "sc")
                        {
                            // Remove holded travel wallet
                            new PaymentServiceController().TempCreditRedeemDelete(payment.OrderID, payment.CreatedByID,
                                payment.CurrencyCode, payment.PaymentAmount, MaySqlCommand);
                        }
                    }
                }

                foreach (var order in ordersList.Select(x => x.SuperPNROrder))
                {
                    bool anyError = false;

                    #region Flight
                    foreach (var flight in order.SuperPNR.Bookings)
                    {
                        if (flight.BookingStatusCode == "CON" || flight.BookingStatusCode == "TKI" || flight.BookingStatusCode == "QPL")
                        {
                            InformationCaution = InformationCaution ?? new List<string>();
                            InformationCaution.Add(string.Format("SuperPNR {0} - {1}: Payment Failed but Flight Booking Placed.",
                                item.SuperPNRID, item.SuperPNRNo));
                            anyError = true;
                            break;
                        }
                        else
                        {
                            flight.BookingStatusCode = "EXP";
                        }
                    }
                    #endregion

                    #region Insurance
                    if (!anyError)
                    {
                        foreach (var insurance in order.SuperPNR.BookingInsurances)
                        {
                            if (insurance.BookingStatusCode == "CON")
                            {
                                InformationCaution = InformationCaution ?? new List<string>();
                                InformationCaution.Add(string.Format("SuperPNR {0} - {1}: Payment Failed but Insurance Booking Placed.",
                                    item.SuperPNRID, item.SuperPNRNo));
                                anyError = true;
                                break;
                            }
                            else
                            {
                                insurance.BookingStatusCode = "EXP";
                            }
                        }
                    }
                    #endregion

                    #region Hotel
                    if (!anyError)
                    {
                        foreach (var hotel in order.SuperPNR.BookingHotels)
                        {
                            if (hotel.BookingStatusCode == "CON")
                            {
                                InformationCaution = InformationCaution ?? new List<string>();
                                InformationCaution.Add(string.Format("SuperPNR {0} - {1}: Payment Failed but Hotel Booking Placed.",
                                    item.SuperPNRID, item.SuperPNRNo));
                                anyError = true;
                                break;
                            }
                            else
                            {
                                hotel.BookingStatusCode = "EXP";
                            }
                        }
                    }
                    #endregion

                    #region Add On
                    if (!anyError)
                    {
                        foreach (var addOn in order.SuperPNR.EventBookings)
                        {
                            if (addOn.BookingStatusCode == "CON")
                            {
                                InformationCaution = InformationCaution ?? new List<string>();
                                InformationCaution.Add(string.Format("SuperPNR {0} - {1}: Payment Failed but AddOn Item Confirmed.",
                                    item.SuperPNRID, item.SuperPNRNo));
                                anyError = true;
                                break;
                            }
                            else
                            {
                                if (addOn.CreatedDate >= new DateTime(2017, 12, 06))
                                {
                                    Alphareds.Module.HotelController.HotelServiceController.UpdateEventBooking(addOn.SuperPNR, "EXP");
                                }

                                addOn.BookingStatusCode = "EXP";
                            }
                        }
                    }
                    #endregion

                    // Check all component first only proceed to modify SuperPNROrder status.
                    if (!anyError)
                    {
                        if (order.BookingStatusCode == "CON" || order.BookingStatusCode == "TKI" || order.BookingStatusCode == "QPL")
                        {
                            InformationCaution = InformationCaution ?? new List<string>();
                            InformationCaution.Add(string.Format("SuperPNR {0} - {1}: Payment Failed but SuperPNR Order status is [CON].",
                                item.SuperPNRID, item.SuperPNRNo));
                        }
                        else
                        {
                            order.BookingStatusCode = "EXP";
                        }
                    }
                }
            }

            try
            {
                if (CommandInitInside && MaySqlCommand?.Transaction != null)
                {
                    MaySqlCommand.Transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                InformationCaution.Add("Error when commit transaction for travel credit & travel wallet."
                            + Environment.NewLine + Environment.NewLine
                            + ex.GetBaseException().Message + Environment.NewLine + Environment.NewLine +
                            ex.StackTrace);
            }

            return itemBookStatus;
        }
    }
}
