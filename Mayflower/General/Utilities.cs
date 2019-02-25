using NLog;
using Mayflower.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Alphareds.Module.Model.Database;

namespace Mayflower.General
{
    public class Utilities
    {
        #region Get Database Information
        /// <summary>
        /// Fetch airport name from database by AirportCode.
        /// </summary>
        /// <param name="AirportCode">Airport code stored in database.</param>
        /// <returns>Airport Name</returns>
        public static string GetAirportName(string AirportCode)
        {
            using (MayFlower db = new MayFlower())
            {
                var airport = db.Stations.FirstOrDefault(x => x.StationCode == AirportCode);
                return airport != null ? airport.DisplayName : "Airport Not Exist";
            }
        }

        /// <summary>
        /// Fetch Airline name from database by AirlineCode.
        /// </summary>
        /// <param name="AirlineCode">Airline code stored in database.</param>
        /// <returns>Airline Name</returns>
        public static string GetAirlineName(string AirlineCode)
        {
            using (MayFlower db = new MayFlower())
            {
                var airline = db.Airlines.FirstOrDefault(x => x.AirlineCode == AirlineCode);
                return airline != null ? airline.Airline1 : "Airline Not Exist";
            }
        }

        /// <summary>
        /// Fetch Country name from database by CountryCode.
        /// </summary>
        /// <param name="CountryCode">Country code stored in database.</param>
        /// <returns>Country Name</returns>
        public static string GetCountryName(string CountryCode)
        {
            using (MayFlower db = new MayFlower())
            {
                var model = db.Countries.FirstOrDefault(x => x.CountryCode == CountryCode);
                return model != null ? model.Country1 : "Country Not Exist";
            }
        }

        public static string GetPassengerTypeName(string PassengerType)
        {
            switch (PassengerType)
            {
                case "ADT":
                    return "Adult";
                case "CNN":
                    return "Child";
                case "INF":
                    return "Infant";
                default:
                    return "Unknow Type";
            }
        }
        #endregion

        public static string GetClientIP
        {
            get
            {
                return HttpContext.Current.Request.UserHostAddress;
            }
        }
        public static string HostURL
        {
            get
            {
                Uri requestUrl = new Uri(HttpContext.Current.Request.Url.ToString());

                string hostIpAddress = HttpContext.Current.Request.UserHostAddress;
                string hostAppPath = string.Empty;
                if (HttpContext.Current.Request.ApplicationPath != "/")
                {
                    hostAppPath = HttpContext.Current.Request.ApplicationPath;
                }
                string hosturl = requestUrl.GetLeftPart(UriPartial.Authority);
                return hosturl;
            }
        }
        public static string GetCreditCardType(string CreditCardNumber)
        {
            Regex regVisa = new Regex("^4[0-9]{12}(?:[0-9]{3})?$");
            Regex regMaster = new Regex("^5[1-5][0-9]{14}$");
            Regex regExpress = new Regex("^3[47][0-9]{13}$");
            Regex regDiners = new Regex("^3(?:0[0-5]|[68][0-9])[0-9]{11}$");
            Regex regDiscover = new Regex("^6(?:011|5[0-9]{2})[0-9]{12}$");
            Regex regJCB = new Regex("^(?:2131|1800|35\\d{3})\\d{11}$");


            if (regVisa.IsMatch(CreditCardNumber))
                return "VISA";
            else if (regMaster.IsMatch(CreditCardNumber))
                return "MASTER";
            else if (regExpress.IsMatch(CreditCardNumber))
                return "AMEXPRESS";
            else if (regDiners.IsMatch(CreditCardNumber))
                return "DINERS";
            else if (regDiscover.IsMatch(CreditCardNumber))
                return "DISCOVERS";
            else if (regJCB.IsMatch(CreditCardNumber))
                return "JCB";
            else
                return "invalid";
        }

        public static IEnumerable<SelectListItem> ExpiredYearsDropList(int years)
        {
            int current = DateTime.Now.Year;

            for (int i = 0; i <= years; i++)
            {
                int loopYear = current + i;
                yield return new SelectListItem { Text = loopYear.ToString(), Value = loopYear.ToString() };
            }
        }       

        public static IEnumerable<SelectListItem> DOBYearsDropList
        {
            get
            {
                int endYear = DateTime.Now.Year;
                List<SelectListItem> year = new List<SelectListItem>();

                for (int i = 0; i <= 100; i++)
                {
                    int loopYear = endYear - i;
                    year.Add(new SelectListItem { Text = loopYear.ToString(), Value = loopYear.ToString() });
                }

                return year;
            }
        }

        public static IEnumerable<SelectListItem> CountryDropList(string selectedCountryCode = "")
        {
            var context = new Alphareds.Module.Model.Database.MayFlower();

            return from a in context.Countries
                   where a.IsActive == true
                   orderby a.Country1
                   select new SelectListItem
                   {
                       Text = a.Country1,
                       Value = a.CountryCode,
                       Selected = a.CountryCode == selectedCountryCode
                   };

        }
        public static IEnumerable<SelectListItem> PhoneCodeList(string selectedPhoneCountryCode = "")
        {
            var context = new Alphareds.Module.Model.Database.MayFlower();

            return (from a in context.Countries
                    where a.IsActive == true && a.PhoneCode != "0" && a.PhoneCode != null
                    //orderby a.Country1 
                    //orderby a.PhoneCodeDisplay.Length, a.PhoneCodeDisplay ascending
                    select new SelectListItem
                    {
                        Text = a.Country1 + " (" + a.PhoneCodeDisplay + ")",
                        Value = a.CountryCode,
                        Selected = a.CountryCode == selectedPhoneCountryCode,
                    }).Distinct().OrderBy(a => a.Text);

        }

        public static IEnumerable<SelectListItem> DaysDropList
        {
            get
            {
                for (int i = 1; i <= 31; i++)
                {
                    string days = i.ToString();
                    yield return new SelectListItem() { Text = days, Value = days };
                }
            }
        }

        public static IEnumerable<SelectListItem> MonthsDropList
        {
            get
            {
                return DateTimeFormatInfo
                       .InvariantInfo
                       .MonthNames
                       .Where(x => !string.IsNullOrEmpty(x))
                       .Select((monthName, index) => new SelectListItem
                       {
                           Value = (index + 1).ToString(),
                           Text = (index + 1).ToString("00") + " - " + monthName
                       });
            }
        }

        public static IEnumerable<SelectListItem> CreditCardYearDropList
        {
            get
            {
                int current = DateTime.Now.Year;

                for (int i = 0; i <= 30; i++)
                {
                    int loopYear = current + i;
                    yield return new SelectListItem
                    {
                        Text = loopYear.ToString(),
                        Value = loopYear.ToString()
                    };
                }
            
            }
        }

        /// <summary>
        /// List of month for SelectListItem dropdown usage.
        /// </summary>
        /// <param name="StyleType">1 - Represent JAN, FEB, MAR.
        /// Default - Represent 01 - JAN, 02 - FEB, 03 - MAR.
        /// </param>
        /// <returns></returns>
        public static IEnumerable<SelectListItem> MonthsDropList2(int StyleType)
        {
            switch (StyleType)
            {
                case 1:
                    return DateTimeFormatInfo
                               .InvariantInfo
                               .MonthNames
                               .Where(x => !string.IsNullOrEmpty(x))
                               .Select((monthName, index) => new SelectListItem
                               {
                                   Value = (index + 1).ToString(),
                                   Text = monthName.Substring(0, 3).ToUpper()
                               });
                default:
                    return DateTimeFormatInfo
                               .InvariantInfo
                               .MonthNames
                               .Where(x => !string.IsNullOrEmpty(x))
                               .Select((monthName, index) => new SelectListItem
                               {
                                   Value = (index + 1).ToString(),
                                   Text = (index + 1).ToString("00") + " - " + monthName
                               });
            }
        }

        /// <summary>
        /// Check is the company profile completed/updated. For active this system feature.
        /// </summary>
        public static bool IsCompanyProfileActivated
        {
            get
            {
                using (MayFlower db = new MayFlower { })
                {
                    var org = db.Organizations.FirstOrDefault(x => x.OrganizationID == GetCurrentOrganizationId);
                    return org != null ? org.IsProfileActivated : false;
                }
            }
        }

        /// <summary>
        /// Check is the user profile completed/updated.
        /// </summary>
        public static bool IsUsersProfileActivated
        {
            get
            {
                using (MayFlower db = new MayFlower { })
                {
                    var result = db.Users.FirstOrDefault(x => x.UserID == GetCurrentUserId);
                    return result != null ? result.IsProfileActivated : false;
                }
            }
        }

        public static int GetCurrentUserId
        {
            get
            {
                var username = System.Web.HttpContext.Current.User.Identity.Name;
                int userid = !string.IsNullOrEmpty(username) ? Convert.ToInt32(username) : 0;
                return userid;
            }
        }

        public static int GetCurrentUserGroupId
        {
            get
            {
                using (MayFlower db = new MayFlower())
                {
                    var id = db.UsersInGroups.FirstOrDefault(x => x.UserID == GetCurrentUserId);
                    return id != null ? id.GroupID : -1;
                }
            }
        }

        public static int GetCurrentOrganizationId
        {
            get
            {
                using (MayFlower db = new MayFlower())
                {
                    var id = db.Users.FirstOrDefault(x => x.UserID == GetCurrentUserId);
                    return id != null ? id.OrganizationID : -1;
                }
            }
        }

        public static List<int> GetSystemAdminUserIDList
        {
            get
            {
                using (var db = new MayFlower())
                {
                    var list = from a in db.Users
                               from role in a.UsersInRoles
                               where role.Role.RoleName == "System Admin" && a.IsActive == true
                               select a.UserID;
                    return list.ToList();
                }
            }
        }

        public static IQueryable<User> GetCurrentOrganizationUsersByRoleRoleCode(string RoleCode)
        {
            MayFlower db = new MayFlower();
            var item = from a in db.Users
                       from role in a.UsersInRoles
                       where a.OrganizationID == GetCurrentOrganizationId && role.Role.RoleCode == RoleCode && a.IsActive == true
                       select a;

            return item;
        }

        public static IQueryable<User> GetCurrentOrganizationUsersByRoleName(string RoleName)
        {
            MayFlower db = new MayFlower();
            var item = from a in db.Users
                       from role in a.UsersInRoles
                       where a.OrganizationID == GetCurrentOrganizationId && role.Role.RoleName == RoleName && a.IsActive == true
                       select a;

            return item;
        }

        /// <summary>
        /// Name used as Default usage, to restrict user edit/delete it.
        /// </summary>
        public static string PreDefinedDefaultName
        {
            get
            {
                return "Company";
            }
        }

        /// <summary>
        /// Get the Default Traveller Group which system default named as "Company".
        /// </summary>
        public static int GetCurrentOrganizationDefaultGroupId
        {
            get
            {
                using (MayFlower db = new MayFlower())
                {
                    var id = db.UserGroups.FirstOrDefault(x => x.OrganizationID == GetCurrentOrganizationId && x.GroupName == PreDefinedDefaultName);
                    return id != null ? id.GroupID : -1;
                }
            }
        }

        /// <summary>
        /// Use for bootstrap popout modal update. Update ("#.update-area") HTML selector.
        /// </summary>
        /// <param name="loadpartial">URL Action to load within update-area.</param>
        /// <returns></returns>
        public static string OnPopOutSuccuessUpdate(string loadpartial)
        {
            var script = String.Empty;
            script += @"$('#PopModal').modal('hide');
                        $('#PopModal').on('hidden.bs.modal', function (e) {
                            $('#update-area').load('" + loadpartial + @"');
                            $('#successful').show();
                            $(document).scrollTop($('#successful').offset().top);
	                        $('#PopModal').unbind('hidden.bs.modal');
                        });";

            return script;
        }

        /// <summary>
        /// Return exception message to ("#popOut-status") bootstrap popout modal.
        /// </summary>
        /// <param name="HeaderMessage">As tittle message for modal popOut-stauts.</param>
        /// <param name="Message">Content to display on popOut-status.</param>
        /// <returns></returns>
        public static string OnPopOutFailedUpdate(string HeaderMessage, string Message)
        {
            string errorExecute = @"$('#popOut-status').html('<p>" + HeaderMessage.Replace("'", "").Replace("\r\n", @"\r\n") + "</p><h4>" + Message.Replace("'", "").Replace("\r\n", @"\r\n") + "</h4>')";
            return errorExecute;
        }

        /// <summary>
        /// Return exception message to ("#popOut-status") bootstrap popout modal.
        /// </summary>
        /// <param name="ex">Exception from Try...Catch</param>
        /// <returns></returns>
        public static string OnPopOutFailedUpdate(Exception ex)
        {
            string InnerException = ex.InnerException != null ? ex.InnerException.InnerException.Message.Replace("'", "").Replace("\r\n", @"\r\n") : "";
            string exMessage = ex.Message.Replace("'", "").Replace("\r\n", @"\r\n");
            string errorExecuteCritical = @"$('#popOut-status').html('<p>Critical Error. Please contact support for help. </p><h4>" + exMessage + @" \r\n" + InnerException + "</h4>')";

            return errorExecuteCritical;
        }

        /// <summary>
        /// Return model error to ("#popOut-status") bootstrap popout modal.
        /// </summary>
        /// <param name="ModelState">ModelState from Controller.</param>
        /// <returns></returns>
        public static string OnPopOutFailedUpdate(ModelStateDictionary ModelState)
        {
            string ModelError = String.Join("\r\n<br/>", ModelState.GetModelErrors()).Replace("'", "").Replace("\r\n", @"\r\n");
            string errorExecute = @"$('#popOut-status').html('<p>Error: </p><h4>" + ModelError + "</h4>')";
            return errorExecute;
        }

        /// <summary>
        /// Helper to auto assign model value to Nlog with long exception log.
        /// </summary>
        /// <param name="logger">Call LogManager.GetCurrentClassLogger() to get class.</param>
        /// <param name="ex">Exception from Try...Catch</param>
        /// <param name="ModelState">ModelState from Controller.</param>
        public static void NlogCatchExceptionWithModelValue(Logger logger, Exception ex, ModelStateDictionary ModelState)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in ModelState)
            {
                sb.AppendLine();
                sb.AppendFormat("{0,-25}: {1}", item.Key, item.Value.Value.AttemptedValue);
            }

            logger.Error(ex, sb.ToString());
        }

        /// <summary>
        /// In Testing. Helper to auto assign model value to Nlog with long exception log, and additional log message.
        /// </summary>
        /// <param name="logger">Call LogManager.GetCurrentClassLogger() to get class.</param>
        /// <param name="ex">Exception from Try...Catch</param>
        /// <param name="ModelState">ModelState from Controller.</param>
        /// <param name="AdditionalValue">Add Dictonary and this helper will loop from it.</param>
        public static void NlogCatchExceptionWithModelValue(Logger logger, Exception ex, ModelStateDictionary ModelState, Dictionary<string, string> AdditionalValue = null)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in ModelState)
            {
                sb.AppendLine();
                sb.AppendFormat("{0,-25}: {1}", item.Key, item.Value.Value.AttemptedValue);
            }
            foreach (var item in AdditionalValue)
            {
                sb.AppendLine();
                sb.AppendFormat("{0,-25}: {1}", item.Key, item.Value);
            }

            logger.Error(ex, sb.ToString());
        }

        /// <summary>
        /// Get airline image
        /// </summary>
        /// <param name="airlineCode">Airline Code. If is multi airline, pass "multiple"</param>
        /// <returns></returns>
        public static string GetAirlineImagePath(string airlineCode)
        {
            string multiAirline = "~/Images/AirlineLogo/multi_airline.jpg";

            using (MayFlower db = new MayFlower())
            {
                var airline = db.Airlines.FirstOrDefault(x => x.AirlineCode == airlineCode);
                if (airline != null)
                {
                    return (airline.ImagePath ?? multiAirline);
                }
                else
                {
                    return multiAirline;
                }
            }
        }
    }
}