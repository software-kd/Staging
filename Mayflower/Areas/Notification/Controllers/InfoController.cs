using Alphareds.Module.Common;
using Alphareds.Module.Model;
using Mayflower.Areas.Notification.SignalR;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Mayflower.Areas.Notification.Controllers
{
    [SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
    public class InfoController : Controller
    {
        IHubContext context = GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();

        // GET: Notification/Info
        [Filters.ControllerAccess.AllowOn(Localhost = true, Staging = false, Production = false)]
        public ActionResult Index()
        {
            //context.Clients.All.pushSingle("msg");
            //context.Clients.User(User.Identity.Name).pushSingle("msg");

            return View();
        }

        [HttpPost]
        public ActionResult CheckSession(string tripid)
        {
            CheckoutProduct checkout = (CheckoutProduct)Core.GetSession(Enumeration.SessionName.CheckoutProduct, tripid);

            context.Clients.All.pushSingle("msg");
            //context.Clients.User(User.Identity.Name).pushSingle("msg");

            return View();
        }
    }
}