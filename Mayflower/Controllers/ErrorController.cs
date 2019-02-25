using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Mayflower.Models;
using System.Web.UI;
using Alphareds.Module.Common;

namespace Mayflower.Controllers
{
    public class ErrorController : Controller
    {
        private static string tripid
        {
            get
            {
                string obj = System.Web.HttpContext.Current.Request.QueryString["tripid"];
                return obj;
            }
        }
        private string sessionNameBooking = Enumeration.SessionName.FlightBooking + tripid;

        //
        // GET: /Error/
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult NotFound()
        {
            var statusCode = (int)System.Net.HttpStatusCode.NotFound;
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            HttpContext.Response.StatusCode = statusCode;
            HttpContext.Response.TrySkipIisCustomErrors = true;
            return View();
        }

        public ActionResult ServerError()
        {
            Response.StatusCode = (int)System.Net.HttpStatusCode.InternalServerError;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }

        public ActionResult AccessDenied()
        {
            return View();
        }

        public ActionResult Type(string id)
        {
            ViewBag.ErrorCode = id;
            return View();
        }

        [HttpPost]
        [OutputCache(Location = OutputCacheLocation.Server, Duration = 0, VaryByParam = "none")]
        public ActionResult CheckSession(string pType)
        {
            if (Session[sessionNameBooking] == null && string.IsNullOrWhiteSpace(pType) ||
                Core.GetSession(Enumeration.SessionName.HotelList, tripid) == null && pType == "h")
            {
                return RedirectToAction("Type", "Error", new { id = "session-error" });
            }
            else
            {
                return Json(true);
            }
        }

        [OutputCache(Location = OutputCacheLocation.Server, Duration = 0, VaryByParam = "none")]
        public ActionResult SessionTimeOut()
        {
            if (Session["FlightBooking"] != null && User.Identity.IsAuthenticated)
            {
                return HttpNotFound();
            }
            else
            {
                return View();
            }
        }
    }
}
