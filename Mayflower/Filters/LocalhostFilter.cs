using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Mayflower.Filters
{
    /// <summary>
    /// For development usage. Set to Only accessible on localhost.
    /// </summary>
    public class LocalhostFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            string ip = filterContext.HttpContext.Request.UserHostAddress;
            bool isLocalhost = (ip == "127.0.0.1" || ip == "::1");
        
            if(!isLocalhost)
            {
                filterContext.Result = new RedirectToRouteResult(new
                RouteValueDictionary(new { controller = "Error", action = "NotFound" }));
                return;
            }
            
            base.OnActionExecuting(filterContext);
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            base.OnActionExecuted(filterContext);
        }

        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            base.OnResultExecuting(filterContext);
        }

        public override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            base.OnResultExecuted(filterContext);
        }

    }
}