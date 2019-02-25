using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using NLog;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Alphareds.Module.BookingController;
using Alphareds.Module.PaymentController;
using Alphareds.Module.HotelController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.Common;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.MemberController;
using Alphareds.Module.CommonController;
using AutoMapper;
using System.Net.Mail;
using WebGrease.Css.Extensions;

namespace Mayflower.Controllers
{
    [Filters.PreserveQueryStringFilter(QueryString = "tripid,affiliationId")]
    public class BookingController : Controller
    {
        private Mayflower.General.CustomPrincipal CustomPrincipal => (User as Mayflower.General.CustomPrincipal);
        private Logger logger = LogManager.GetCurrentClassLogger();

        private static string tripid
        {
            get
            {
                var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
                var routeValue = request.RequestContext.RouteData.Values["tripid"];
                string routeString = routeValue != null ? routeValue.ToString() : null;

                string obj = (String.IsNullOrEmpty(System.Web.HttpContext.Current.Request.QueryString["tripid"]) ? null : System.Web.HttpContext.Current.Request.QueryString["tripid"].Split(',')[0]) ??
                    (routeString ?? System.Web.HttpContext.Current.Request.Form["tripid"]);
                return obj;
            }
        }
        private static string affiliationId
        {
            get
            {
                var request = new UrlHelper(System.Web.HttpContext.Current.Request.RequestContext);
                var routeValue = request.RequestContext.RouteData.Values["affiliationId"];
                string routeString = routeValue != null ? routeValue.ToString() : null;

                string obj = System.Web.HttpContext.Current.Request.QueryString["affiliationId"] ?? (routeString ?? System.Web.HttpContext.Current.Request.Form["affiliationId"]);
                return obj;
            }
        }

        #region Utilities
        private int CurrentUserID
        {
            get
            {
                int userid = 0;
                if (User.Identity.IsAuthenticated)
                {
                    int.TryParse(User.Identity.Name, out userid);
                }
                else if (Session["RegisteredUserId"] != null)
                {
                    userid = (int)Session["RegisteredUserId"];
                }
                return userid;
            }
        }

        private bool IsAgentUser
        {
            get
            {
                Alphareds.Module.Model.Database.User user = null;

                if (User.Identity.IsAuthenticated)
                {
                    user = Alphareds.Module.Common.Core.GetUserInfo(User.Identity.Name);
                }

                return user == null ? false : user.UserTypeCode == "AGT";
            }
        }
        #endregion
    }
}