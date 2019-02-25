using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web;
using Mayflower.General;
using System.Web.Security;
using Alphareds.Module.Model.Database;

namespace Mayflower.Filters
{
    public class RolesFilter : ActionFilterAttribute
    {
        //private int[] rolesID;
        private string[] roleName;

        protected virtual CustomPrincipal CurrentUser
        {
            get { return HttpContext.Current.User as CustomPrincipal; }
        }

        private void GetRolesForUser()
        {
            roleName = Roles.GetRolesForUser();
            //rolesID = db.Roles.Where(x => roleName.Contains(x.RoleName)).Select(x=>x.RoleID).ToArray();
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Create new instance everytime call
            MayFlower db = new MayFlower();
            #region
            /*
            Assembly asm = Assembly.GetAssembly(typeof(Mayflower.MvcApplication));

            List<string> excludeItem = new List<string> { "Item1", "Item2" };
            CurrentRole.GetRolesForUser(CurrentUser.Id.ToString());

            var controlleractionlist = asm.GetTypes()
                    .Where(type => typeof(System.Web.Mvc.Controller).IsAssignableFrom(type))
                    .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public))
                    .Where(m => !m.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), true).Any())
                    .Select(x => new { Controller = x.DeclaringType.Name, Action = x.Name, ReturnType = x.ReturnType.Name })
                    .Where(x => !x.Controller.ToLower().Contains("person") && !x.Controller.ToLower().Contains("agent") && !x.Controller.ToLower().Contains("account")
                        //&& !x.Action.ToLower().Contains("Register") && !x.Action.ToLower().Contains("log") 
                        && !x.Action.ToLower().Contains("mail"))
                    .OrderBy(x => x.Controller).ThenBy(x => x.Action)
                    .GroupBy(x => new { x.Controller, x.Action })
                    .Select(x => new { GroupController = x.Key.Controller, GroupAction = x.Key.Action })
                    .Where(x => x.GroupController == controllerName)
                    .ToList();
            */
            #endregion

            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                GetRolesForUser();
            }
            else
            {
                roleName = new string[] {};
            }

            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerType.Name;
            var actionName = filterContext.ActionDescriptor.ActionName;

            var rolePolicy = db.RolePolicies.Where(x => roleName.Contains(x.Role.RoleName)).Select(x => x.ControllerID);

            var ControllerActionList = db.SMC_ControllerList.Where(r => rolePolicy.Contains(r.ControllerID) || r.IsAllowAnonymous == true)
                                       .Where(x => x.ControllerName == controllerName && x.ActionName == actionName)
                                       .ToList();

            if (ControllerActionList.Count <= 0)
            {
                filterContext.Result = new RedirectToRouteResult(new
                    RouteValueDictionary(new { controller = "Error", action = "AccessDenied" }));
            }

            //Log("OnActionExecuting", filterContext.RouteData);
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            //Log("OnActionExecuted", filterContext.RouteData);
        }

        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            //Log("OnResultExecuting", filterContext.RouteData);
        }

        public override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            //Log("OnResultExecuted", filterContext.RouteData);
        }


        private void Log(string methodName, RouteData routeData)
        {
            var controllerName = routeData.Values["controller"];
            var actionName = routeData.Values["action"];
            var message = String.Format("{0} controller:{1} action:{2}", methodName, controllerName, actionName);
            Debug.WriteLine(message, "Action Filter Log");
        }

    }
}
