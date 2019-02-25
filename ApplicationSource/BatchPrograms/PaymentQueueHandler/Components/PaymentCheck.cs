using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model.Database;

namespace PaymentQueueHandler.Components
{
    partial class PaymentCheck
    {
        public class DBQuery
        {
            private static double takeBookingPaymentAfterSecond = Convert.ToDouble(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("TakeBookingPaymentAfterSecond"));
            private static int? _parseSecond = (int?)takeBookingPaymentAfterSecond;

            public static IQueryable<PaymentOrder> GetOrdersRHI(MayFlower db, bool ignoreRequeryTime = false, bool ignoreRequeryStatus = false)
            {
                db = db ?? new MayFlower();

                var recordRHI = db.SuperPNROrders.Where(x => x.BookingStatusCode == "RHI"
                && (ignoreRequeryStatus || x.PaymentOrders.Count(p => p.RequeryStatusCode == "ERR" || p.RequeryStatusCode == "MAN") == 0)
                && x.CreatedDate >= new DateTime(2017, 12, 05) // requery new item
                && (ignoreRequeryTime || DateTime.Now >= System.Data.Entity.DbFunctions.AddSeconds(x.CreatedDate, _parseSecond))
                );
                
                return recordRHI.SelectMany(x => x.PaymentOrders).Distinct();
            }

            public static IQueryable<PaymentOrder> GetOrdersRHI(MayFlower db, string superPNRNo, bool ignoreRequeryTime = false, bool ignoreRequeryStatus = false)
            {
                db = db ?? new MayFlower();

                var recordRHI = db.SuperPNROrders.Where(x => x.BookingStatusCode == "RHI" 
                && x.SuperPNR.SuperPNRNo == superPNRNo
                && (ignoreRequeryStatus || x.PaymentOrders.Count(p => p.RequeryStatusCode == "ERR" || p.RequeryStatusCode == "MAN") == 0)
                && x.CreatedDate >= new DateTime(2017, 12, 05) // requery new item
                && (ignoreRequeryTime || DateTime.Now >= System.Data.Entity.DbFunctions.AddSeconds(x.CreatedDate, _parseSecond))
                );
                
                return recordRHI.SelectMany(x => x.PaymentOrders).Distinct();
            }

            public static IQueryable<PaymentOrder> GetPaymentsPEND(MayFlower db, bool ignoreRequeryTime = false)
            {
                db = db ?? new MayFlower();

                return db.PaymentOrders.Where(x => x.PaymentStatusCode == "PEND"
                && x.RequeryStatusCode != "ERR"
                && (ignoreRequeryTime || DateTime.Now >= System.Data.Entity.DbFunctions.AddSeconds(x.CreatedDate, _parseSecond))
                ).Distinct();
            }

            public static IQueryable<PaymentOrder> GetPaymentsPEND(MayFlower db, string superPNRNo, bool ignoreRequeryTime = false)
            {
                db = db ?? new MayFlower();

                return db.PaymentOrders.Where(x => x.PaymentStatusCode == "PEND"
                && x.SuperPNROrder.SuperPNR.SuperPNRNo == superPNRNo
                && x.RequeryStatusCode != "ERR"
                && (ignoreRequeryTime || DateTime.Now >= System.Data.Entity.DbFunctions.AddSeconds(x.CreatedDate, _parseSecond))
                );
            }

        }
    }
}
