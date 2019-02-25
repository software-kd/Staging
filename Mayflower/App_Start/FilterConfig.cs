using System.Web;
using System.Web.Mvc;

namespace Mayflower
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new General.LogExceptionFilterAttribute());
            filters.Add(new HandleErrorAttribute());
            //filters.Add(new Mayflower.Filters.RolesFilter());
        }
    }
}