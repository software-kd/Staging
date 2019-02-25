using System.Web.Mvc;

namespace Mayflower.Areas.SPAgent
{
    public class SPAgentAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Agent";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                "SPAgent_default",
                "SPAgent/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}