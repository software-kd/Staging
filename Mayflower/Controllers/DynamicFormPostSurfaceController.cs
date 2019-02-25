using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Net;
using Newtonsoft.Json;
using System.Reflection;
using System.Collections.Specialized;
using System.Text;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using NLog;

namespace Mayflower.Controllers
{
    public class DynamicFormPostSurfaceController : Controller
    {
        Logger logger = LogManager.GetCurrentClassLogger();

        public DynamicFormPostSurfaceController()
        {
        }

        [Mayflower.Filters.LocalhostFilter]
        public ActionResult SecureLogin(UserData model, string returnUrl, NameValueCollection fc, int userId)
        {
            fc = fc ?? (Request.Form ?? new NameValueCollection());
            var property = typeof(UserData).GetProperties();
            var newFormCollection = new NameValueCollection();

            foreach (var item in fc.AllKeys)
            {
                newFormCollection.Add(item, fc[item]);
            }

            foreach (var item in property)
            {
                var _attempVal = item.GetValue(model);

                // Not pass in null value while reflection get null.
                if (_attempVal != null)
                {
                    newFormCollection.Add(item.Name, _attempVal.ToString());
                }
            }

            newFormCollection["SessionTransferMemberId"] = userId.ToString();

            string cmsUrl = System.Web.Configuration.WebConfigurationManager.AppSettings["AlphaReds.CMSUrl"];
            UriBuilder uriBuilder = new UriBuilder(cmsUrl);

            string redirectUrl = uriBuilder.ToString() + "umbraco/Surface/DynamicFormPostSurface/AuthAndRedirect?destinationUrl=" + returnUrl;
            redirectUrl += "&type=SessionTransfer";

            string strForm = PreparePOSTForm(redirectUrl, newFormCollection);
            return PartialView("~/Views/DynamicFormPostSurface/DynamicFormPost.cshtml", (object)strForm);
        }

        public ActionResult RedirectAndPOST(string destinationUrl, string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                logger.Error("RedirectAndPOST error. " + Environment.NewLine + Environment.NewLine +
                    " Ref Url : " + JsonConvert.SerializeObject(Request.Url) + Environment.NewLine +
                    " Raw Url : " + JsonConvert.SerializeObject(Request.RawUrl) + Environment.NewLine +
                    " UrlReferrer : " + JsonConvert.SerializeObject(Request.UrlReferrer) + Environment.NewLine +
                    " QueryString : " + JsonConvert.SerializeObject(Request.QueryString) + Environment.NewLine +
                    " Form : " + JsonConvert.SerializeObject(Request.Form) + Environment.NewLine +
                    " UserAgent : " + JsonConvert.SerializeObject(Request.UserAgent));

                if (Request.UrlReferrer == null)
                {
                    return RedirectToAction("Index", "Home", new { error = "invalid-param" });
                }
                else
                {
                    return Redirect(Request.UrlReferrer?.AbsoluteUri ?? "~/");
                }
            }
            else if (Request.UrlReferrer != null && !string.IsNullOrWhiteSpace(destinationUrl))
            {
                // Keep all query string from booking site.

                UriBuilder builder = new UriBuilder(Request.UrlReferrer);
                UriBuilder descUrlBuilder = new UriBuilder(destinationUrl);
                var query = HttpUtility.ParseQueryString(builder.Query);
                var descQuery = HttpUtility.ParseQueryString(descUrlBuilder.Query);

                if (query != null && query.Count > 0)
                {
                    foreach (var item in query)
                    {
                        descQuery[item.ToString()] = query[item.ToString()];
                    }
                }

                descUrlBuilder.Query = descQuery.ToString();
                destinationUrl = descUrlBuilder.ToString();
            }

            NameValueCollection formData = new NameValueCollection();

            if (User.Identity.IsAuthenticated && CurrentUser != null && type.Equals("SessionTransfer"))
            {
                formData.Add("SessionTransferMemberId", CurrentUser.Id.ToString());
                formData.Add("UserId", CurrentUser.Id.ToString());
                formData.Add("FirstName", CurrentUser.FirstName);
                formData.Add("Email", CurrentUser.Email);
                formData.Add("IsActive", CurrentUser.IsActive.ToString());
                formData.Add("isAgent", CurrentUser.IsAgent.ToString());
            }
            else if (type.Equals("MenuTransfer"))
            {
                if (User.Identity.IsAuthenticated)
                {
                    string name = User.Identity.Name;
                    formData.Add("SessionTransferMemberId", name);
                }
                else
                {
                    formData.Add("SessionTransferMemberId", "");
                }
            }

            string strForm = PreparePOSTForm(destinationUrl, formData);

            return PartialView("DynamicFormPost", (object)strForm);
        }

        private String PreparePOSTForm(string url, NameValueCollection data)
        {
            //Set a name for the form
            string formID = "PostForm";
            //Build the form using the specified data to be posted.
            StringBuilder strForm = new StringBuilder();
            strForm.Append("<form id=\"" + formID + "\" name=\"" +
                           formID + "\" action=\"" + url +
                           "\" method=\"POST\">");

            foreach (string key in data)
            {
                strForm.Append("<input type=\"hidden\" name=\"" + key +
                               "\" value=\"" + data[key] + "\">");
            }

            strForm.Append("</form>");
            //Build the JavaScript which will do the Posting operation.
            StringBuilder strScript = new StringBuilder();
            strScript.Append("<script language=\"javascript\">");
            strScript.Append("var v" + formID + " = document." +
                             formID + ";");
            strScript.Append("v" + formID + ".submit();");
            strScript.Append("</script>");
            //Return the form and the script concatenated.
            //(The order is important, Form then JavaScript)
            return strForm.ToString() + strScript.ToString();
        }

        protected virtual General.CustomPrincipal CurrentUser
        {
            get { return (User as Mayflower.General.CustomPrincipal); }
        }
    }
}
