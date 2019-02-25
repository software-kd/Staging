using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.PaymentController;
using HotelBookingHandler.Model;

namespace HotelBookingHandler
{
    class PaymentQuery
    {
        public class IPAY88
        {
            public static bool CheckPaymentPAID(PaymentOrder record, int bookingId, int superPNRId, string superPNRNo)
            {
                decimal amt = Alphareds.Module.Common.Core.IsForStaging ? 1.00m : record.PaymentAmount;
                string _payStatus = record.PaymentStatusCode.ToUpper();

                var result = PaymentServiceController.iPay88.RequeryPaymentStatus(record.Ipay88RefNo, record.CurrencyCode, amt.ToString("n2"));
                if (result.Desc == "Record not found" || result.Status == "Record not found")
                {
                    string attempRefNo = bookingId.ToString() + " - " + superPNRNo;
                    attempRefNo = attempRefNo != record.Ipay88RefNo ? attempRefNo : superPNRId.ToString() + " - " + superPNRNo;
                    result = PaymentServiceController.iPay88.RequeryPaymentStatus(attempRefNo, record.CurrencyCode, amt.ToString("n2"));
                }
                else if (result.Desc == "Invalid parameters" || result.Desc == "Incorrect amount")
                {
                    throw new Exception("iPay88 requery result stated - " + result.Desc);
                }
                else if (result.Desc == "Payment Fail") // iPay FPX only
                {
                    record.PaymentStatusCode = "FAIL";
                    return false;
                }
                else if (result.Desc == "Voided")
                {
                    record.PaymentStatusCode = "VOID";
                    return false;
                }

                bool paymentAccept = result.Status == "1" && (result.Desc == "Authorised" || result.Desc == "Captured" || result.Desc == string.Empty);

                if (paymentAccept && (_payStatus != "PAID" && _payStatus != "CAPT" && _payStatus != "FAIL" && _payStatus != "VOID"))
                {
                    if (record.SuperPNROrder.BookingStatusCode == "PPA")
                        record.SuperPNROrder.BookingStatusCode = "RHI";

                    if (result.Desc == "Authorised")
                    {
                        record.PaymentStatusCode = "AUTH";
                    }
                    else if (result.Desc == "Captured")
                    {
                        record.PaymentStatusCode = "CAPT";
                    }
                    else if(result.Desc == string.Empty)
                    {
                        record.PaymentStatusCode = "PAID";
                    }

                }

                return paymentAccept;
            }

            public static Task<bool> CheckPaymentPAIDAsync(PaymentOrder record, int bookingId, int superPNRId, string superPNRNo)
            {
                return Task.Factory.StartNew(() =>
                {
                    return CheckPaymentPAID(record, bookingId, superPNRId, superPNRNo);
                });
            }
        }

        public class ADYEN
        {
            public class StatusHandler
            {
                //public static List<IPAY88.StatusHandler> PaymentCapture()
                //{
                //    bool sendPDF = false;
                //    bool.TryParse(Core.GetAppSettingValueEnhanced("RequerySendPDF"), out sendPDF);
                //    if (booking.BookingStatusCode == "CON" && sendPDF)
                //    {
                //        Alphareds.Module.HotelController.HotelServiceController.sendHotelItineraryEmailAfterSuccess(booking);
                //    }
                //}
            }
        }
    }
}
