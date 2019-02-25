using PagedList;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Mayflower.Models
{
    public class AgentLoginModels
    {
        [Required]
        [Display(Name = "User Login ID")]
        //[EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string UserLoginID { get; set; }

        [Required]
        [Display(Name = "Password")]
        public string Pwd { get; set; }
    }

    public class AgentModels
    {
        public int AgentID { get; set; }

        [Display(Name = "Company Name")]
        //[Required]
        public int? OrganizationID { get; set; }

        [Display(Name = "Role")]
        [UIHint("RoleSelectList")]
        [Required]
        public List<int?> RoleID { get; set; }

        [Required(ErrorMessage = "Email is required.", AllowEmptyStrings = false)]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [Remote("IsEmailAvailable", "User", ErrorMessage ="Email address already exists.")]
        public string Email { get; set; }

        [Display(Name = "Agent Code")]
        public string AgentCode { get; set; }

        //[Required] /* If First Name empty, will cause AccountController(IsValidUser) Tuple.Item1 return false, unable to login. */
        [Display(Name = "First Name")]
        public string FirstName { get; set; }
        //public string FirstName { get { return PassportName; } set { this.PassportName = value; } }

        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        //[Required(ErrorMessage = "Name is required.", AllowEmptyStrings = false)]
        [Display(Name = "Passport Name")]
        public string PassportName { get; set; }

        [Display(Name = "Mobile Phone")]
        [DataType(DataType.PhoneNumber)]
        //[RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Entered Mobile Phone is not valid.")]
        [RegularExpression(@"^([+]?[0-9]{1,3})?[-. ]?([0-9 ]{4,14})$", ErrorMessage = "Entered Mobile Phone is not valid.")]
        public string MobilePhone { get; set; }

        [Display(Name = "Office Phone")]
        [DataType(DataType.PhoneNumber)]
        //[RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Entered Office Phone is not valid.")]
        [RegularExpression(@"^([+]?[0-9]{1,3})?[-. ]?([0-9 ]{4,14})$", ErrorMessage = "Entered Office Phone is not valid.")]
        public string OfficePhone { get; set; }

        [Display(Name = "Home Phone")]
        [DataType(DataType.PhoneNumber)]
        //[RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Entered Home Phone is not valid.")]
        [RegularExpression(@"^([+]?[0-9]{1,3})?[-. ]?([0-9 ]{4,14})$", ErrorMessage = "Entered Home Phone is not valid.")]
        public string HomePhone { get; set; }

        public int CreatedByID { get; set; }

        public DateTime CreatedDate { get; set; }

        public int ModifiedByID { get; set; }

        public DateTime ModifiedDate { get; set; }

        [Display(Name = "Account Activated?")]
        public bool IsActive { get; set; }

        [Display(Name = "Account Activated?")]
        public bool IsProfileActivated { get; set; }

        public DateTime? CreatedDateUTC { get; set; }

        public DateTime? ModifiedDateUTC { get; set; }
    
    }

    //Search View 
    public class AgentViewModels
    {
        public int? Page { get; set; }
        public int? SizePage { get; set; }
        public string SearchButton { get; set; }

        public string Keyword { get; set; }

        //public UserData UserData { get; set; }

        public IPagedList<AgentListingViewModels> SearchResults { get; set; }
    }

    //Listing View table Details 
    public class AgentListingViewModels
    {
        public int AgentID { get; set; }

        [Display(Name = "Agent Code")]
        public string AgentCode { get; set; }

        public List<int> RoleID { get; set; }

        public List<string> Role { get; set; }

        [Display(Name = "Agent Name")]
        public string FullName { get; set; }

        public string Organization { get; set; }

        public string Email { get; set; }

        [Display(Name = "Mobile No")]
        public string MobilePhone { get; set; }

        [Display(Name = "Office No")]
        public string OfficePhone { get; set; }

        [Display(Name = "Is Actived?")]
        public bool IsActive { get; set; }
    }

}