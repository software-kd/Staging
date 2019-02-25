using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Mayflower.Filters
{
    /// <summary>
    /// Check session by ActionFilter. (Split by ',')
    /// </summary>
    public class PreserveQueryStringFilter : ActionFilterAttribute
    {
        public string QueryString { get; set; }
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            foreach (var item in QueryString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string key = item;
                string sessionId = filterContext.HttpContext.Request.QueryString[key];

                // If query string null get from FormCollection (from ajax data)
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    var formCollection = new FormCollection(filterContext.Controller.ControllerContext.HttpContext.Request.Form);
                    sessionId = formCollection[key];
                }

                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    if (key == "tripid")
                        filterContext.Controller.ViewBag.tripid = sessionId;
                    filterContext.HttpContext.Items[key] = sessionId;

                    if (!filterContext.ActionParameters.Any(x => x.Key == key))
                    {
                        filterContext.ActionParameters.Add(key, sessionId);
                    }
                }
            }

            base.OnActionExecuting(filterContext);
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var redirectResult = filterContext.Result as RedirectToRouteResult;
            if (redirectResult == null)
            {
                return;
            }

            foreach (var item in QueryString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string key = item;
                string sessionId = filterContext.HttpContext.Request.QueryString[key];

                // If query string null get from FormCollection (from ajax data)
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    var formCollection = new FormCollection(filterContext.Controller.ControllerContext.HttpContext.Request.Form);
                    sessionId = formCollection[key];
                }

                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    if (!redirectResult.RouteValues.ContainsKey(key))
                    {
                        redirectResult.RouteValues.Add(key, sessionId);
                    }
                }
            }


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