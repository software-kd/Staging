using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Mayflower.Models;
using System.Web.Mvc;
using Alphareds.Module.Model.Database;
using System.Web.Routing;

namespace Mayflower.Filters
{

    public class EditSelfInfoAttribute : ActionFilterAttribute
    {
        public bool IsFilterOrganization { get; set; }
        public string ActionParameterName { get; set; }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            MayFlower db = new MayFlower();
            //var requestID = filterContext.RequestContext.RouteData.Values["id"];
            int userID = Convert.ToInt32(filterContext.HttpContext.User.Identity.Name);
            var user = db.Users.AsEnumerable().FirstOrDefault(x => x.UserID == userID);

            if (!string.IsNullOrEmpty(ActionParameterName))
            {
            }

            if (IsFilterOrganization)
            {
                // Based on QueryString
                //if (user.OrganizationID != Convert.ToInt32(requestID))
                var model = (Organization)filterContext.ActionParameters[ActionParameterName];
                if (user.OrganizationID != model.OrganizationID)
                {
                    filterContext.Result = new RedirectToRouteResult(new
                        RouteValueDictionary(new { controller = "Error", action = "AccessDenied" }));
                }
            }
            else
            {
                // Based on QueryString
                //if (user.UserID != Convert.ToInt32(requestID))
                var model = (User)filterContext.ActionParameters[ActionParameterName];
                if (user.UserID != model.UserID)
                {
                    filterContext.Result = new RedirectToRouteResult(new
                        RouteValueDictionary(new { controller = "Error", action = "AccessDenied" }));
                }
            }
        }

        //protected override bool AuthorizeCore(HttpContextBase httpContext)
        //{
        //    var authorized = base.AuthorizeCore(httpContext);
        //    if (!authorized)
        //    {
        //        return false;
        //    }

        //    var rd = httpContext.Request.RequestContext.RouteData;

        //    var id = rd.Values["id"] == null ? "0" : rd.Values["id"];
        //    var userName = httpContext.User.Identity.Name;
        //    MayFlower db = new MayFlower();
        //    var orgId = db.Users.AsEnumerable().FirstOrDefault(x=>x.UserID==Convert.ToInt32(userName)).OrganizationID;

        //    return orgId.ToString() == id.ToString();
        //}
    }
}