using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Mayflower
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.MapMvcAttributeRoutes();

            routes.LowercaseUrls = true;

            #region Customize Routing for Register
            routes.MapRoute(
                name: "Register",
                url: "Register",
                defaults: new { controller = "Member", action = "Register" }
                );
            #endregion

            #region Customize Routing for Booking Payment
            //routes.MapRoute(
            //    name: "FlightConfirm",
            //    url: "Flight/OrderHistory",
            //    defaults: new { controller = "Booking", action = "BookingDetail" }
            //    );
            #endregion

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );

            // 2017/11/13, Added by Heng
            #region Customize Routing for Login
            routes.MapRoute(
                name: "RedirectLogin",
                url: "account/login/account/login",
                defaults: new { controller = "Account", action = "Login" }
                );
            #endregion
        }
    }
}