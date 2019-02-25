using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Mayflower.Models
{
    /// <summary>
    /// MerchantPaymentStart.aspx 
    /// </summary>
    public class OnePayPaymentSubmitModels
    {
        public decimal Amount { get; set; }
        public bool AutoCapture { get; set; }
        public int ClientID { get; set; }
        public string Currency { get; set; }
        public string IPAddress { get; set; }
        public string PurchaseDescription { get; set; }
        public string Reference { get; set; }
        public string UrlError { get; set; }
        public string UrlSuccess { get; set; }
        public string Cardholder { get; set; }
        public string Number { get; set; }
        public string CVV { get; set; }
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string CountryCode { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class OnePayCaptureResponseModels
    {
        public int PaymentId { get; set; }
        public bool autosubmit { get; set; }
        public bool HasError { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string RRN { get; set; }
        public string ApprovalCode { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Reference { get; set; }
    }

    public class CardPaymentModels : BillingAddressModels
    {
        [Required(AllowEmptyStrings=false, ErrorMessage = "Please enter the cardholder's name exactly as it appears on the card.")]
        public string CardHolderName { get; set; }

        [CreditCard(ErrorMessage = "Please enter a valid card number.")]
        [Required(ErrorMessage = "Please enter a valid card number.")]
        public string CardNumber { get; set; }

        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "Please enter a valid card security code.")]
        [Required(ErrorMessage = "Please enter a valid card security code.")]
        public string CVV { get; set; }

        [Required(ErrorMessage = "Please choose a valid month and year.")]
        public int ExpiryMonth { get; set; }

        [Required(ErrorMessage = "Please choose a valid month and year.")]
        public int ExpiryYear { get; set; }

        public ICollection<OnePayPaymentSubmitModels> POSTOnePayGateway { get; set; }
    }

    public class BankTransferPaymentModels
    {
        public decimal? BankInAmount { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy hh:MM tt}", ApplyFormatInEditMode = true)]
        public DateTime? BankInDate { get; set; }
        public string RefNo { get; set; }
        public HttpPostedFile paymentProof { get; set; }
    }

    public class BillingAddressModels
    {
        [Required(ErrorMessage = "Please enter the street address.")]
        [MaxLength(80)]
        public string StreetAddress { get; set; }

        [Required(ErrorMessage = "Please enter the City.")]
        [MaxLength(80)]
        public string City { get; set; }

        [Required(ErrorMessage = "Please select a Country.")]
        [MaxLength(3)]
        public string CountryCode { get; set; }

        [Required(ErrorMessage = "Please enter the Post Code.")]
        [DataType(DataType.PostalCode)]
        public string PostCode { get; set; }

        [MaxLength(80)]
        [Required(ErrorMessage = "Please enter the State.")]
        public string State { get; set; }
    }
}