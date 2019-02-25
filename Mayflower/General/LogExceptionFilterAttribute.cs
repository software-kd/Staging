using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NLog;

namespace Mayflower.General
{
    public class LogExceptionFilterAttribute : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            string environment = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("Apps.Environment");

            if (environment.ToLower() == "production")
            {
                // Log the exception here with your logging framework of choice.
                Logger logger = LogManager.GetCurrentClassLogger();

                if (filterContext.Exception.Message?.StartsWith(@"The provided anti-forgery token was meant for user """) ?? false)
                {
                    // Skip message that random occur post action with forgery token error.
                }
                else
                {
                    string _reqUrl = filterContext.HttpContext?.Request?.Url?.ToString();

                    logger.Fatal(filterContext.Exception,
                        "Unexpected error doesn't handle by code." +
                        (_reqUrl != null ? Environment.NewLine + Environment.NewLine + $"Request Url: {_reqUrl}" : null)
                        );
                }
            }
        }
    }
}