using System.Web.Mvc;

namespace Mayflower.Areas.BOS
{
    public class BOSAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "BOS";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                "BOS_default",
                "BOS/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}