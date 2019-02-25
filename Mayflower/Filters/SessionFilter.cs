using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Mayflower.Filters
{
    /// <summary>
    /// Check session by ActionFilter.
    /// </summary>
    public class SessionFilter : ActionFilterAttribute
    {
        /// <summary>
        /// Set Session to check with comma separate. (Ex: "FlightBooking" ,"FlightBooking, HotelBooking")
        /// </summary>
        public string SessionName { get; set; }
        private IEnumerable<string> SessionNameList
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SessionName))
                {
                    foreach (var item in SessionName.Split(','))
                    {
                        yield return item;
                    }
                }
            }
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            foreach (var item in SessionNameList)
            {
                string sessionId = filterContext.HttpContext.Request.QueryString["tripid"] ?? filterContext.HttpContext.Request.Form["tripid"];

                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    sessionId = sessionId.Split(',')[0];

                    if (sessionId.ToLower().Contains("tripid"))
                    {
                        sessionId = sessionId.ToLower().Split(new[] { "tripid", "=", "?" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    }
                }
                else
                {
                    string _queryString = HttpUtility.UrlDecode(filterContext.HttpContext.Request.QueryString.ToString())
                    ?.Replace("{", "").Replace("}", "")
                    .Replace("\"", "").Replace(":", "=");
                    var _parsedQS = HttpUtility.ParseQueryString(_queryString ?? "");

                    sessionId = _parsedQS["tripid"];
                }

                var normalize = sessionId != null ? sessionId.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries) : new string[] { };
                sessionId = normalize.Length > 1 ? normalize.First() : sessionId;

                if (filterContext.HttpContext.Session[item + sessionId] == null)
                {
                    filterContext.Result = new RedirectToRouteResult(new
                    RouteValueDictionary(new { controller = "Error", action = "Type", id = "session-error" }));
                    return;
                }
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