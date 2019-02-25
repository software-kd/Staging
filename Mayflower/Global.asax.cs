using Forloop.HtmlHelpers;
using FluentValidation.Mvc;
using Mayflower.General;
using Alphareds.Module.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Globalization;
using NLog;
using System.Web.Helpers;

namespace Mayflower
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            var serializerSettings = GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings;
            var contractResolver = (Newtonsoft.Json.Serialization.DefaultContractResolver)serializerSettings.ContractResolver;
            contractResolver.IgnoreSerializableAttribute = true;

            AreaRegistration.RegisterAllAreas();

            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            AuthConfig.RegisterAuth();

            FluentValidationModelValidatorProvider.Configure();

            AntiForgeryConfig.SuppressXFrameOptionsHeader = true;

            // Partial View Script optimization
            // For more information please visit https://bitbucket.org/forloop/forloop-htmlhelpers/wiki/Home
            ScriptContext.ScriptPathResolver = System.Web.Optimization.Scripts.Render;

            #region 10/02/2017 - David added portion, get a full list of destination name for auto-complete at once. (Disabled)
            /*
            System.Web.HttpContext.Current.Cache.Remove(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString());
            List<Alphareds.Module.Model.AutoCompleteModel> model = null;
            List<string> destinations = Alphareds.Module.HotelController.HotelServiceController.GetHotelDestinationList(ref model);
            if (destinations.Count > 0)
            {
                //System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString(), destinations);   

                //bind data in model
                System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString(), model);
            }
            */
            #endregion

            #region 2017/02/03 - Heng Modify and enhance autocomplete model type
            try
            {
                System.Web.HttpContext.Current.Cache.Remove(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString());
                System.Web.HttpContext.Current.Cache.Remove(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotelStaying.ToString());
                var enhancedDestinations = Alphareds.Module.HotelController.HotelServiceController.GetGoingToList();
                //var stayingAt = Alphareds.Module.HotelController.HotelServiceController.GetStayingAtList();

                if (enhancedDestinations.Count > 0)
                {
                    //bind data in model
                    System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotel.ToString(), enhancedDestinations);
                    //System.Web.HttpContext.Current.Cache.Insert(Alphareds.Module.Common.Enumeration.SessionName.AutoCompleteHotelStaying.ToString(), stayingAt);
                }
            }
            catch (Exception ex)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Fatal(ex, "Error while caching hotel autocomplete list.");
            }
            #endregion
        }

        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            HttpCookie authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];

            if (authCookie != null)
            {
                FormsAuthenticationTicket authTicket = FormsAuthentication.Decrypt(authCookie.Value);

                UserData serializeModel = Newtonsoft.Json.JsonConvert.DeserializeObject<UserData>(authTicket.UserData);

                if (serializeModel.UserId <= 0)
                {
                    return; // exit function if userid invalid.
                }

                // When the ticket was created, the UserData property was assigned a
                // pipe delimited string of role names.
                string[] roles = new string[] { };

                try
                {
                    roles = Roles.GetRolesForUser(serializeModel.UserId.ToString());
                }
                catch (Exception ex)
                {
                    throw ex;
                }
             
                // Create an Identity object
                FormsIdentity id = new FormsIdentity(authTicket);

                // This principal will flow throughout the request.
                CustomPrincipal principal = new CustomPrincipal(id, roles)
                {
                    LoginID = serializeModel.LoginID,
                    
                    Id = serializeModel.UserId,
                    UserId = serializeModel.UserId,
                    Email = serializeModel.Email,
                    FirstName = serializeModel.FirstName,
                    IsActive = serializeModel.IsActive,
                    IsAgent = serializeModel.IsAgent,
                    IsProfileActive = serializeModel.IsProfileActive,
                    IsComapnyAdmin = serializeModel.IsComapnyAdmin,
                    IsLoginPasswordNotSetup = serializeModel.IsLoginPasswordNotSetup,

                    IsDisplayAllSupplier = serializeModel.IsDisplayAllSupplier,
                    IsHtlSameDayAllow = serializeModel.IsHtlSameDayAllow,

                    OrganizationID = serializeModel.OrganizationID,
                    OrganizationLogo = serializeModel.OrganizationLogo,

                    CreditTerm = serializeModel.CreditTerm,
                    UserTypeCode = serializeModel.UserTypeCode,
                };

                // Attach the new principal object to the current HttpContext object
                HttpContext.Current.User = principal;
            }
            else
            {
                try
                {
                    HttpContext.Current.User = new CustomPrincipal(User.Identity);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }

        #region trimming string
        public class TrimModelBinder : DefaultModelBinder
        {
            protected override void SetProperty(ControllerContext controllerContext, ModelBindingContext bindingContext, System.ComponentModel.PropertyDescriptor propertyDescriptor, object value)
            {
                if (propertyDescriptor.PropertyType == typeof(string))
                {
                    var stringValue = (string)value;
                    if (!string.IsNullOrEmpty(stringValue))
                        stringValue = stringValue.Trim();

                    value = stringValue;
                }

                base.SetProperty(controllerContext, bindingContext, propertyDescriptor, value);
            }
        }
        #endregion

        protected void Application_BeginRequest()
        {
            CultureInfo info = new CultureInfo(System.Threading.Thread.CurrentThread.CurrentCulture.ToString());
            //info.DateTimeFormat.ShortDatePattern = "M/dd/yyyy";
            info.DateTimeFormat.ShortDatePattern = "dd-MMM-yyyy";
            info.DateTimeFormat.LongDatePattern = "dd-MMM-yyyy HH:mm tt zzz";
            System.Threading.Thread.CurrentThread.CurrentCulture = info;
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            // Log the exception.
            
            Response.Clear();

            HttpException httpException = exception as HttpException;

            RouteData routeData = new RouteData();
            routeData.Values.Add("controller", "Error");
            string logTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            string _requestUrl = " (" + (Request?.Url?.ToString() ?? "n/a") + ") ";

            if (httpException == null)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Debug(exception, $"Root Exception - {logTime}{_requestUrl}");
                if (exception.InnerException != null)
                {
                    logger.Debug(exception.GetBaseException(), $"Base Exception - {logTime}{_requestUrl}");
                }
                routeData.Values.Add("action", "Type");
            }
            else //It's an Http Exception, Let's handle it.
            {
                Logger logger = LogManager.GetCurrentClassLogger();

                switch (httpException.GetHttpCode())
                {
                    case 404:
                        // Page not found.
                        routeData.Values.Add("action", "NotFound");
                        break;
                    case 500:
                        // Server error.
                        logger.Fatal(exception, $"Root Exception - {logTime}{_requestUrl}");
                        if (exception.InnerException != null)
                        {
                            logger.Fatal(exception.GetBaseException(), $"Base Exception - {logTime}{_requestUrl}");
                        }
                        routeData.Values.Add("action", "ServerError");
                        break;

                    // Here you can handle Views to other error codes.
                    // I choose a General error template  
                    default:
                        // Server error.
                        logger.Fatal(exception, $"Not specific http code - {logTime}{_requestUrl}");
                        if (exception.InnerException != null)
                        {
                            logger.Fatal(exception.GetBaseException(), $"Base Exception - {logTime}{_requestUrl}");
                        }
                        routeData.Values.Add("action", "ServerError");
                        break;
                }
            }

            // Pass exception details to the target error View.
            //routeData.Values.Add("error", exception);

            // Clear the error on server.
            Server.ClearError();

            // Avoid IIS7 getting in the middle
            //Response.TrySkipIisCustomErrors = true;

            Response.RedirectToRoute("Default", routeData.Values);
            // at this point how to properly pass route data to error controller?
            //Response.Redirect(String.Format("~/Error/{0}/?message={1}", "Index", exception.Message));

            // Call target Controller and pass the routeData.
            //IController errorController = new Controllers.ErrorController();
            //errorController.Execute(new RequestContext(
            //     new HttpContextWrapper(Context), routeData));
        }
    }


}