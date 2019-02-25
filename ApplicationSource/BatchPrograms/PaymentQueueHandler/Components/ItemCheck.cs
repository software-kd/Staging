using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using System.Data.Entity;

namespace PaymentQueueHandler.Components
{
    class ItemCheck
    {
        #region Private Property
        MayFlower DBContext { get; set; }
        #endregion

        #region Public Property
        public SuperPNR SuperPNR { get; set; }
        public ItemStatus? Flight { get; set; }
        public ItemStatus? Hotel { get; set; }
        public ItemStatus? Insurance { get; set; }
        public ItemStatus? EventProducts { get; set; }

        public List<string> InformationCaution { get; set; }
        public enum ItemStatus
        {
            UNDEFINED,
            CAN,
            CON,
            CRF,
            EXP,
            PPA,
            QPL,
            RHI,
            TKI,
            TPC,
        }
        #endregion

        #region Constructor
        public ItemCheck(int superPNRID, MayFlower _dbContext, bool chkFlight, bool chkHotel, bool chkAddOnBook)
        {
            DBContext = _dbContext ?? new MayFlower();
            SuperPNR = DBContext.SuperPNRs.FirstOrDefault(x => x.SuperPNRID == superPNRID);

            if (chkFlight)
            {
                CheckFlight();
            }

            if (chkHotel)
            {
                CheckHotel();
            }

            if (chkAddOnBook)
            {
                CheckInsurance();
                CheckEventProducts();
            }
        }

        public ItemCheck(int superPNRID, MayFlower _dbContext, bool chkStatusPending, bool chkFlight, bool chkHotel, bool chkAddOnBook)
        {
            DBContext = _dbContext ?? new MayFlower();
            SuperPNR = DBContext.SuperPNRs.FirstOrDefault(x => x.SuperPNRID == superPNRID);

            if (chkFlight)
            {
                CheckFlight();
            }

            if (chkHotel)
            {
                CheckHotel();
            }

            if (chkAddOnBook)
            {
                CheckInsurance();
                CheckEventProducts();
            }

            if (chkStatusPending)
            {
                UpdatePendingPaymentStatus();
            }
        }
        #endregion

        protected void CheckFlight()
        {
            List<ItemStatus> _listStatus = null;

            foreach (var item in SuperPNR.Bookings)
            {
                _listStatus = _listStatus ?? new List<ItemStatus>();
                ItemStatus _itemStatus = ItemStatus.UNDEFINED;

                Enum.TryParse(item.BookingStatusCode, out _itemStatus);
                _listStatus.Add(_itemStatus);
            }

            if (_listStatus != null)
            {
                Flight = GetItemStatus(_listStatus);
            }
        }

        protected void CheckHotel()
        {
            List<ItemStatus> _listStatus = null;

            foreach (var item in SuperPNR.BookingHotels)
            {
                _listStatus = _listStatus ?? new List<ItemStatus>();
                ItemStatus _itemStatus = ItemStatus.UNDEFINED;

                Enum.TryParse(item.BookingStatusCode, out _itemStatus);
                _listStatus.Add(_itemStatus);
            }

            if (_listStatus != null)
            {
                Hotel = GetItemStatus(_listStatus);
            }
        }

        protected void CheckInsurance()
        {
            List<ItemStatus> _listStatus = null;

            foreach (var item in SuperPNR.BookingInsurances)
            {
                _listStatus = _listStatus ?? new List<ItemStatus>();
                ItemStatus _itemStatus = ItemStatus.UNDEFINED;

                Enum.TryParse(item.BookingStatusCode, out _itemStatus);
                _listStatus.Add(_itemStatus);
            }

            if (_listStatus != null)
            {
                Insurance = GetItemStatus(_listStatus);
            }
        }

        protected void CheckEventProducts()
        {
            List<ItemStatus> _listStatus = null;

            foreach (var item in SuperPNR.EventBookings)
            {
                _listStatus = _listStatus ?? new List<ItemStatus>();
                ItemStatus _itemStatus = ItemStatus.UNDEFINED;

                Enum.TryParse(item.BookingStatusCode, out _itemStatus);
                _listStatus.Add(_itemStatus);
            }

            if (_listStatus != null)
            {
                EventProducts = GetItemStatus(_listStatus);
            }
        }

        protected void UpdatePendingPaymentStatus()
        {
            foreach (var item in SuperPNR.SuperPNROrders)
            {
                if (item.BookingStatusCode == "PPA")
                {
                    item.BookingStatusCode = "RHI";
                }

                foreach (var payment in item.PaymentOrders)
                {
                    // Check is over 7 days for process or not.
                    _CheckMissedCapturePayment(payment);

                    if (payment.PaymentStatusCode != "PAID" && payment.PaymentStatusCode != "CAPT")
                    {
                        var _paymentMethod = payment.PaymentMethodCode.ToLower();
                        if (_paymentMethod == "ipafpx")
                        {
                            payment.RequeryStatusCode = "SUC";
                            payment.PaymentStatusCode = "PAID";
                        }
                        else if (_paymentMethod == "ipacc" || _paymentMethod.StartsWith("ady"))
                        {
                            payment.PaymentStatusCode = "AUTH";
                        }
                    }
                }
            }
        }

        internal void _CheckMissedCapturePayment(PaymentOrder payment)
        {
            InformationCaution = CheckMissedCapturePayment(payment);
        }

        public static List<string> CheckMissedCapturePayment(PaymentOrder payment)
        {
            List<string> pushInfo = null;
            var _paymentMethod = payment.PaymentMethodCode.ToLower();

            // Check is over 7 days for process or not.
            if (payment.PaymentDate.HasValue && (_paymentMethod == "ipacc" || _paymentMethod.StartsWith("ady"))
                && payment.PaymentStatusCode != "CAPT")
            {
                DateTime dtPayment = payment.PaymentDate.Value;
                if ((DateTime.Now - dtPayment).TotalDays > 7)
                {
                    pushInfo = pushInfo ?? new List<string>();
                    pushInfo.Add(string.Format("PaymentOrders PaymentID {0} : Passed 7 days for capture.", payment.OrderID));
                }
            }

            // Check payment order if is IPACC is it transactionID null, will cause capture request erorr.
            if (_paymentMethod == "ipacc" && string.IsNullOrWhiteSpace(payment.Ipay88TransactionID))
            {
                pushInfo = pushInfo ?? new List<string>();
                pushInfo.Add($"PaymentOrders PaymentID {payment.OrderID} : TransactionID empty.");
            }

            return pushInfo;
        }

        protected ItemStatus GetItemStatus(List<ItemStatus> list)
        {
            return list.All(x => x == ItemStatus.CON || x == ItemStatus.QPL || x == ItemStatus.TKI) ? ItemStatus.CON
                    : list.All(x => x == ItemStatus.EXP) ? ItemStatus.EXP
                    : list.FirstOrDefault(x => x != ItemStatus.CON && x != ItemStatus.QPL && x != ItemStatus.TKI && x != ItemStatus.EXP);
        }
    }
}
