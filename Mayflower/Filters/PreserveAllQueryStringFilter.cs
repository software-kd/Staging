using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Mayflower.Filters
{
    public class PreserveAllQueryStringFilter : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var redirectResult = filterContext.Result as RedirectToRouteResult;
            if (redirectResult == null)
            {
                return;
            }

            var query = filterContext.HttpContext.Request.QueryString;

            foreach (string key in query.Keys)
            {
                if (!redirectResult.RouteValues.ContainsKey(key))
                {
                    redirectResult.RouteValues.Add(key, query[key]);
                }
            }
        }
    }
}