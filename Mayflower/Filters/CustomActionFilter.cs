using Alphareds.Module.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Mayflower.Filters
{
    public class ControllerAccess
    {
        public class AllowOn : ActionFilterAttribute
        {
            public bool Localhost { get; set; }
            public bool Staging { get; set; }
            public bool Production { get; set; }

            public override void OnActionExecuting(ActionExecutingContext filterContext)
            {
                string ip = filterContext.HttpContext.Request.UserHostAddress;
                bool isLocalhost = (ip == "127.0.0.1" || ip == "::1");

                if ((!Localhost && isLocalhost) || (!Staging && Core.IsForStaging) || (!Production && !Core.IsForStaging))
                {
                    filterContext.Result = new RedirectToRouteResult(new
                    RouteValueDictionary(new { controller = "Error", action = "NotFound" }));
                    return;
                }

                base.OnActionExecuting(filterContext);
            }
        }

        public class CheckOnConfiguration : ActionFilterAttribute
        {
            /// <summary>
            /// Check by web.config AppKey see whether module enable or not.
            /// </summary>
            public string AppKey { get; set; }

            public override void OnActionExecuting(ActionExecutingContext filterContext)
            {
                bool setting = false;
                bool.TryParse(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced(AppKey), out setting);

                if (!setting)
                {
                    filterContext.Result = new RedirectToRouteResult(new
                    RouteValueDictionary(new { controller = "Error", action = "NotFound" }));
                    return;
                }

                base.OnActionExecuting(filterContext);
            }
        }
    }
}