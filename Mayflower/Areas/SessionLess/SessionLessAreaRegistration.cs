using System.Web.Mvc;

namespace Mayflower.Areas.SessionLess
{
    public class SessionLessAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "SessionLess";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                "SessionLess_default",
                "SessionLess/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}