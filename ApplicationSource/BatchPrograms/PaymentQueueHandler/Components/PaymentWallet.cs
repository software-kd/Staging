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
        public class StoreQuery
        {
            public class TravelWallet
            {
                public static RequeryStatus.Requery CheckPaymentPAID(PaymentOrder record, int superPNRId, string superPNRNo
                    , MayFlower dbContext)
                {
                    if (record.SuperPNROrder.PaymentOrders.Count == 1 && 
                        !record.SuperPNROrder.SuperPNR.Bookings.Any(x => x.BookingStatusCode == "TKI" || x.BookingStatusCode == "CON" || x.BookingStatusCode == "QPL")
                        && !record.SuperPNROrder.SuperPNR.BookingHotels.Any(x => x.BookingStatusCode == "TKI" || x.BookingStatusCode == "CON" || x.BookingStatusCode == "QPL")
                        && !record.SuperPNROrder.SuperPNR.EventBookings.Any(x => x.BookingStatusCode == "TKI" || x.BookingStatusCode == "CON" || x.BookingStatusCode == "QPL")
                        && !record.SuperPNROrder.SuperPNR.BookingInsurances.Any(x => x.BookingStatusCode == "TKI" || x.BookingStatusCode == "CON" || x.BookingStatusCode == "QPL")
                        && !record.SuperPNROrder.SuperPNR.CarRentalBookings.Any(x => x.BookingStatusCode == "TKI" || x.BookingStatusCode == "CON" || x.BookingStatusCode == "QPL")
                        && !record.SuperPNROrder.SuperPNR.TourPackageBookings.Any(x => x.BookingStatusCode == "TKI" || x.BookingStatusCode == "CON" || x.BookingStatusCode == "QPL"))
                    {
                        record.PaymentStatusCode = "FAIL";

                        // Use edmx to remove hold credit used.
                        var _tempCreditHold = dbContext.Temp_UserCashCreditRedeem.FirstOrDefault(x => x.OrderID == record.OrderID);

                        if (_tempCreditHold != null)
                        {
                            System.Data.SqlClient.SqlCommand command = new System.Data.SqlClient.SqlCommand();
                            
                            new PaymentServiceController()
                                .TempCashCreditRedeemDelete(record.OrderID, record.CreatedByID, record.CurrencyCode, record.PaymentAmount, command);

                            command?.Transaction?.Commit();
                        }

                        record.SuperPNROrder.BookingStatusCode = "EXP";
                        
                        return new RequeryStatus.Requery
                        {
                            SuperPNRID = superPNRId,
                            SuperPNRNo = superPNRNo,
                            OrderID = record.OrderID,
                            Amount = record.PaymentAmount,
                            Status = false,
                        };
                    }
                    
                    return null;
                }
                
                public static Task<RequeryStatus.Requery> CheckPaymentPAIDAsync(PaymentOrder record, int superPNRId, string superPNRNo,
                    MayFlower dbContext)
                {
                    return Task.Factory.StartNew(() =>
                    {
                        return CheckPaymentPAID(record, superPNRId, superPNRNo, dbContext);
                    });
                }
            }
        }
    }

}
