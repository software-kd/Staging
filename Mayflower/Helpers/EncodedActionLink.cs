using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Text;
using System.Web.Routing;
using System.Security.Cryptography;
using System.IO;

namespace Mayflower.Helpers
{
    public static class MyExtensions
    {
        public static string EncodedURL(this UrlHelper htmlHelper, string actionName, string controllerName, object routeValues)
        {
            string queryString = string.Empty;
            string htmlAttributesString = string.Empty;
            string AreaName = string.Empty;
            if (routeValues != null)
            {
                RouteValueDictionary d = new RouteValueDictionary(routeValues);
                for (int i = 0; i < d.Keys.Count; i++)
                {
                    string elementName = d.Keys.ElementAt(i).ToLower();
                    if (elementName == "area")
                    {
                        AreaName = Convert.ToString(d.Values.ElementAt(i));
                        continue;
                    }
                    if (i > 0)
                    {
                        queryString += "?";
                    }
                    queryString += d.Keys.ElementAt(i) + "=" + d.Values.ElementAt(i);
                }
            }

            //What is Entity Framework??
            StringBuilder ancor = new StringBuilder();

            #region SubDirectory host use
            // for subdirectory
            if (HttpContext.Current.Request.ApplicationPath != "/")
            {
                ancor.Append(HttpContext.Current.Request.ApplicationPath);
            }
            #endregion

            if (AreaName != string.Empty)
            {
                ancor.Append("/" + AreaName);
            }
            if (controllerName != string.Empty)
            {
                ancor.Append("/" + controllerName);
            }

            if (actionName != "Index")
            {
                ancor.Append("/" + actionName);
            }
            if (queryString != string.Empty)
            {
                ancor.Append("?q=" + Encrypt(queryString));
            }
            return ancor.ToString();
        }

        public static MvcHtmlString EncodedActionLink(this HtmlHelper htmlHelper, string linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            string queryString = string.Empty;
            string htmlAttributesString = string.Empty;
            string AreaName = string.Empty;
            if (routeValues != null)
            {
                RouteValueDictionary d = new RouteValueDictionary(routeValues);
                for (int i = 0; i < d.Keys.Count; i++)
                {
                    string elementName = d.Keys.ElementAt(i).ToLower();
                    if (elementName == "area")
                    {
                        AreaName = Convert.ToString(d.Values.ElementAt(i));
                        continue;
                    }
                    if (i > 0)
                    {
                        queryString += "?";
                    }
                    queryString += d.Keys.ElementAt(i) + "=" + d.Values.ElementAt(i);
                }
            }

            if (htmlAttributes != null)
            {
                RouteValueDictionary d = new RouteValueDictionary(htmlAttributes);
                for (int i = 0; i < d.Keys.Count; i++)
                {
                    htmlAttributesString += " " + d.Keys.ElementAt(i).Replace("_", "-") + "=" + d.Values.ElementAt(i);
                }
            }

            //What is Entity Framework??
            StringBuilder ancor = new StringBuilder();
            ancor.Append("<a ");
            if (htmlAttributesString != string.Empty)
            {
                ancor.Append(htmlAttributesString);
            }
            ancor.Append(" href='");

            #region SubDirectory host use
            // for subdirectory
            if (HttpContext.Current.Request.ApplicationPath != "/")
            {
                ancor.Append(HttpContext.Current.Request.ApplicationPath);
            }
            #endregion

            if (AreaName != string.Empty)
            {
                ancor.Append("/" + AreaName);
            }
            if (controllerName != string.Empty)
            {
                ancor.Append("/" + controllerName);
            }

            if (actionName != "Index")
            {
                ancor.Append("/" + actionName);
            }
            if (queryString != string.Empty)
            {
                ancor.Append("?q=" + Encrypt(queryString));
            }
            ancor.Append("'");
            ancor.Append(">");
            ancor.Append(linkText);
            ancor.Append("");
            return new MvcHtmlString(ancor.ToString());
        }

        private static string Encrypt(string plainText)
        {
            var publicKey = "<RSAKeyValue><Modulus>yj/bd1/xrvqTMSZ7IuH8jKynqESl+C/9VqWjJ6yyMoY7UFicLXiFLVQ/bvNu7UB3KBUqHBBQ65qVFqLtNeRiVIhhdnCge8k7lJsvTm8/2OHGJ0Sd5hStWHLcZCyDNLDT07Dh3+XelXSQulKPIrhA4tDgXYyXpb5nlE1YbtTvplU=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

            using (var rsa = new RSACryptoServiceProvider(1024))
            {
                try
                {
                    rsa.FromXmlString(publicKey);
                    var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                    var encryptedData = rsa.Encrypt(plainTextBytes, true);
                    var base64Encrypted = Convert.ToBase64String(encryptedData);

                    return HttpUtility.UrlEncode(base64Encrypted);
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}