using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model.Database;
using Alphareds.Module.PaymentController;
using PaymentQueueHandler.Model;

namespace PaymentQueueHandler.Components
{
    public partial class PaymentCheck
    {
        public class ServiceQuery
        {
            public class IPAY88
            {
                public static RequeryStatus.Requery CheckPaymentPAID(PaymentOrder record, int bookingId, int superPNRId, string superPNRNo)
                {
                    decimal amt = Alphareds.Module.Common.Core.IsForStaging ? 1.00m : record.PaymentAmount;
                    string _payStatus = record.PaymentStatusCode.ToUpper();
                    var _requeryResult = new RequeryStatus.Requery
                    {
                        SuperPNRID = superPNRId,
                        SuperPNRNo = superPNRNo,
                        OrderID = record?.OrderID ?? -1,
                        Status = false,
                    };

                    var result = PaymentServiceController.iPay88.RequeryPaymentStatus(record.Ipay88RefNo, record.CurrencyCode, amt.ToString("n2"));
                    _requeryResult.Desc = result?.Desc;

                    if (string.IsNullOrWhiteSpace(record.Ipay88TransactionID)
                        && !string.IsNullOrWhiteSpace(result.TransId))
                    {
                        record.Ipay88TransactionID = result.TransId;
                    }

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
                        return _requeryResult;
                    }
                    else if (result.Desc == "Voided")
                    {
                        record.PaymentStatusCode = "VOID";
                        return _requeryResult;
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
                        else if (result.Desc == string.Empty)
                        {
                            record.PaymentStatusCode = "PAID";
                        }

                    }

                    if (paymentAccept)
                    {
                        _requeryResult.Status = true;
                    }

                    return _requeryResult;
                }

                public static Task<RequeryStatus.Requery> CheckPaymentPAIDAsync(PaymentOrder record, int bookingId, int superPNRId, string superPNRNo)
                {
                    return Task.Factory.StartNew(() =>
                    {
                        return CheckPaymentPAID(record, bookingId, superPNRId, superPNRNo);
                    });
                }

                public static Task<RequeryStatus.Requery> CheckPaymentPAIDAsync(PaymentOrder record, int superPNRId, string superPNRNo)
                {
                    return Task.Factory.StartNew(() =>
                    {
                        return CheckPaymentPAID(record, -1, superPNRId, superPNRNo);
                    });
                }

            }

            public class ADYEN
            {
                public static RequeryStatus.Requery CheckPaymentPAID(PaymentOrder record, int bookingId, int superPNRId, string superPNRNo)
                {
                    decimal amt = Alphareds.Module.Common.Core.IsForStaging ? 500 : record.PaymentAmount;
                    string _payStatus = record.PaymentStatusCode.ToUpper();
                    var _requeryResult = new RequeryStatus.Requery
                    {
                        SuperPNRID = superPNRId,
                        SuperPNRNo = superPNRNo,
                        OrderID = record?.OrderID ?? -1,
                        Status = false,
                    };

                    var result = PaymentServiceController.Adyen.RequeryPaymentStatus(record.Ipay88RefNo, record.Ipay88TransactionID, record.CurrencyCode, amt.ToString("n2"));
                    _requeryResult.Desc = result?.Desc;

                    if (string.IsNullOrWhiteSpace(record.Ipay88TransactionID)
                        && !string.IsNullOrWhiteSpace(result.PspReference))
                    {
                        record.Ipay88TransactionID = result.PspReference;
                    }


                    /*
                       Adyen PayStatus Reference (Based on Adyen Web Service Document 1.1.0)
                     * "P"  – Pending
                     * "AS" – Authorize Success
                     * "AF" – Authorize Fail
                     * "CS" – Capture Success
                     * "CF" – Capture Fail
                     * "XS" – Cancel Success
                     * "XF" – Cancel Fail
                     */

                    if (result.Desc == "Record not found" || result.PayStatus == "Record not found")
                    {
                        string attempRefNo = bookingId.ToString() + " - " + superPNRNo;
                        attempRefNo = attempRefNo != record.Ipay88RefNo ? attempRefNo : superPNRId.ToString() + " - " + superPNRNo;
                        result = PaymentServiceController.Adyen.RequeryPaymentStatus(attempRefNo, record.Ipay88TransactionID, record.CurrencyCode, amt.ToString("n2"));
                    }
                    else if (result.Desc == "Invalid parameters" || result.Desc == "Incorrect amount")
                    {
                        throw new Exception("Adyen requery result stated - " + result.Desc);
                    }
                    else if (result.Desc == "Payment Fail" || result.Desc == "AF")
                    {
                        result.Desc = "Payment Fail"; // Assign For BookingList Usage
                        record.PaymentStatusCode = "FAIL";
                        return _requeryResult;
                    }
                    else if (result.Desc == "Voided" || result.Desc == "XS")
                    {
                        result.Desc = "Voided"; // Assign For BookingList Usage
                        record.PaymentStatusCode = "VOID";
                        return _requeryResult;
                    }
                    else if (result.PayStatus == "AS") // Assign For BookingList Usage
                    {
                        result.Desc = "Authorised";
                    }
                    else if (result.PayStatus == "CS") // Assign For BookingList Usage
                    {
                        result.Desc = "Captured";
                    }
                    else if (result.PayStatus == "CF" || result.PayStatus == "XF")
                    {
                        record.RequeryStatusCode = "MAN";
                        return _requeryResult;
                    }

                    bool paymentAccept = (result.PayStatus == "AS" || result.PayStatus == "CS") && 
                        (result.Desc == "Authorised" || result.Desc == "Captured" || result.Desc == string.Empty);

                    if (paymentAccept && (_payStatus != "PAID" && _payStatus != "CAPT" && _payStatus != "FAIL" && _payStatus != "VOID"))
                    {
                        if (record.SuperPNROrder.BookingStatusCode == "PPA")
                            record.SuperPNROrder.BookingStatusCode = "RHI";

                        if (result.PayStatus == "AS")
                        {
                            record.PaymentStatusCode = "AUTH";
                        }
                        else if (result.PayStatus == "CS")
                        {
                            record.PaymentStatusCode = "CAPT";
                        }
                        else if (result.Desc == string.Empty)
                        {
                            record.PaymentStatusCode = "PAID";
                        }

                    }

                    if (paymentAccept)
                    {
                        _requeryResult.Status = true;
                    }

                    return _requeryResult;
                }

                public static Task<RequeryStatus.Requery> CheckPaymentPAIDAsync(PaymentOrder record, int bookingId, int superPNRId, string superPNRNo)
                {
                    return Task.Factory.StartNew(() =>
                    {
                        return CheckPaymentPAID(record, bookingId, superPNRId, superPNRNo);
                    });
                }

                public static Task<RequeryStatus.Requery> CheckPaymentPAIDAsync(PaymentOrder record, int superPNRId, string superPNRNo)
                {
                    return Task.Factory.StartNew(() =>
                    {
                        return CheckPaymentPAID(record, -1, superPNRId, superPNRNo);
                    });
                }
            }
        }
    }

}
