using Alphareds.Module.Model.Database;
using PagedList;
using Mayflower.CustomValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Mayflower.Models
{
    public class OrganizationModels
    {
        public int OrganizationID { get; set; }

        [Display(Name = "Company Code")]
        public string OrganizationCode { get; set; }

        [Required]
        [Display(Name = "Company Name")]
        public string OrganizationName { get; set; }

        //[Required] // 2016/08/11, RedMine Task#941, move to update from setting
        [Display(Name = "Company Registration No")]
        public string RegistrationNo { get; set; }

        [Display(Name = "Company Tax Registration No")]
        public string TaxRegistrationNo { get; set; }

        [Display(Name = "Country")]
        public short? CountryID { get; set; }

        [Display(Name = "Country")]
        public string CountryCode { get; set; }

        [Display(Name = "Province State")]
        public string ProvinceState { get; set; }

        [Required]
        [Display(Name = "Address 1")]
        public string Address1 { get; set; }

        [Display(Name = "Address 2")]
        public string Address2 { get; set; }

        [Display(Name = "Address 3")]
        public string Address3 { get; set; }

        [Required]
        [MaxLength(50)]
        public string City { get; set; }

        [Required]
        [Display(Name = "Post Code")]
        [MaxLength(10)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "Invalid Post Code")]
        public string PostCode { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [Remote("IsEmailAvailable", "Organization", ErrorMessage = "Email address already exists.")]
        public string Email { get; set; }

        [Display(Name = "Company Contact 1")]
        [DataType(DataType.PhoneNumber)]
        //[RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Entered Company Contact 1 is not valid.")]
        //[RegularExpression(@"^([+]?[0-9]{1,3})?[-. ]?([0-9 ]{4,14})$", ErrorMessage = "Entered Company Contact 1 is not valid.")]
        [PhoneFormat(ErrorMessage = "Entered Company Contact 1 is not valid.")]
        public string ContactNo1 { get; set; }

        [Display(Name = "Company Contact 2")]
        [DataType(DataType.PhoneNumber)]
        //[RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Entered Company Contact 2 is not valid.")]
        //[RegularExpression(@"^([+]?[0-9]{1,3})?[-. ]?([0-9 ]{4,14})$", ErrorMessage = "Entered Company Contact 2 is not valid.")]
        [PhoneFormat(ErrorMessage = "Entered Company Contact 2 is not valid.")]
        public string ContactNo2 { get; set; }

        [Display(Name = "Company Contact 3")]
        [DataType(DataType.PhoneNumber)]
        //[RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Entered Company Contact 3 is not valid.")]
        //[RegularExpression(@"^([+]?[0-9]{1,3})?[-. ]?([0-9 ]{4,14})$", ErrorMessage = "Entered Company Contact 3 is not valid.")]
        [PhoneFormat(ErrorMessage = "Entered Company Contact 3 is not valid.")]
        public string ContactNo3 { get; set; }

        public bool IsActive { get; set; }
        
        [ReadOnly(true)]
        [ScaffoldColumn(false)]
        public int CreatedByID { get; set; }

        [ReadOnly(true)]
        [ScaffoldColumn(false)]
        public DateTime CreatedDate { get; set; }

        [ReadOnly(true)]
        [ScaffoldColumn(false)]
        public int ModifiedByID { get; set; }

        [ReadOnly(true)]
        [ScaffoldColumn(false)]
        public DateTime ModifiedDate { get; set; }

        [ReadOnly(true)]
        [ScaffoldColumn(false)]
        public DateTime? CreatedDateUTC { get; set; }

        [ReadOnly(true)]
        [ScaffoldColumn(false)]
        public DateTime? ModifiedDateUTC { get; set; }
    }

    public class MemberRegisterModels : AgentModels
    {
        //[Required(ErrorMessage="Company Name is required.",AllowEmptyStrings = false)]
        [Remote("IsOrgNameAvailable", "Organization", ErrorMessage = "This Company Name registered.")]
        public string OrganizationName { get; set; }

        //[Required(ErrorMessage = "Please select your Country.", AllowEmptyStrings = false)]
        public short? CountryID { get; set; }

        [Required(ErrorMessage = "Please select your Country.", AllowEmptyStrings = false)]
        [MaxLength(3)]
        public string CountryCode { get; set; }

        [Required(ErrorMessage = "Password is required.", AllowEmptyStrings = false)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public string TitleCode { get; set; }
        public string IdentityNo { get; set; }
        public string PassportNo { get; set; }
        public string PassportIssuePlace { get; set; }
        public string PrimaryPhone { get; set; }
        public string SecondaryPhone { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string Postcode { get; set; }
        public string AddressProvinceState { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress1 { get; set; }
        public string CompanyAddress2 { get; set; }
        public string CompanyCity { get; set; }
        public string CompanyPostcode { get; set; }
        public string CompanyAddressProvinceState { get; set; }
        public string CompanyAddressCountryCode { get; set; }
        public DateTime DOB { get; set; }
        public DateTime PassportExpiryDate { get; set; }
        public List<UserFrequentFlyer> FrequentFlyer { get; set; }
        public string FrequentFlyerNumber { get; set; }
        public string FrequentFlyerAirlineCode{ get; set; }

    }

    public class OrganizationAgentViewModels
    {
        public OrganizationModels MainOrganization { get; set; }
        public AgentModels MainAgent { get; set; }
    }

    //Search View 
    public class OrganizationViewModels
    {
        public int? Page { get; set; }
        public int? SizePage { get; set; }
        public string SearchButton { get; set; }
         
        public string Keyword { get; set; }

        public IPagedList<OrganizationListingViewModels> SearchResults { get; set; }
    }

    //Listing View table Details 
    public class OrganizationListingViewModels
    {
        public int OrganizationID { get; set; }

        [Display(Name = "Organization Name")]
        public string OrganizationName { get; set; }

        [Display(Name = "Registration No.")]
        public string RegistrationNo { get; set; }

        public string Email { get; set; }

    }

}