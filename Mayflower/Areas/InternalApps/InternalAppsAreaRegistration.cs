using System.Web.Mvc;

namespace Mayflower.Areas.InternalApps
{
    public class InternalAppsAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "InternalApps";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                "InternalApps_default",
                "InternalApps/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}