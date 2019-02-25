using System.Web.Mvc;

namespace Mayflower.Areas.Notification
{
    public class NotificationAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Notification";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                "Notification_default",
                "Notification/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}