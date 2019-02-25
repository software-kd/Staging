using Alphareds.Module.Model.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Alphareds.Module.Model;
using Alphareds.Module.ServiceCall;
using Alphareds.Module.Common;
using Alphareds.Module.ExpediaHotelsWebService.ExpediaHotels;
using Alphareds.Module.ESBHotelComparisonWebService.ESBHotel;

namespace Mayflower.Controllers
{
    public class PublicController : Controller
    {
        //public ActionResult TempEmail()
        //{
        //    return View();
        //}
        #region Use CMS Version
        //public ActionResult TermsConditions()
        //{
        //    return View();
        //}

        //public ActionResult PDPA()
        //{
        //    return View();
        //}

        //public ActionResult Cancel()
        //{
        //    return View();
        //}
        #endregion

        //[HttpPost]
        public ActionResult GetCurrentDate()
        {
            string dt = DateTime.Now.ToString("ddd MMM dd yyyy HH:mm:ss") + " GMT+0800 (Malay Peninsula Standard Time)";
            return Content(dt);
        }

        /// <summary>
        /// Get server info usage.
        /// </summary>
        /// <returns></returns>
        public JavaScriptResult SInfo()
        {
            string dateTime = DateTime.Now.ToString("ddd MMM dd yyyy HH:mm:ss") + " GMT+0800 (Malay Peninsula Standard Time)";

            string scripts = @"var ServerDateTime = ";
            scripts += "";
            scripts += "new Date('" + dateTime + "')";
            scripts += "";
            return JavaScript(scripts);
        }

        public ActionResult GetAirlineImage(string airlineCode)
        {
            var db = new MayFlower();

            var result = from a in db.Airlines.AsEnumerable()
                         orderby a.Airline1
                         where a.IsActive == true && a.Airline1 != ""
                         select new
                         {
                             AirlineCode = a.AirlineCode,
                             AirlineName = a.Airline1,
                             ImagePath = Url.Content(a.ImagePath ?? "/Images/AirlineLogo/multi_airline.png")
                         };

            if (airlineCode != null)
            {
                var resultQ = result.FirstOrDefault(x => x.AirlineCode.ToLower() == airlineCode.ToLower());
                return Json(resultQ, JsonRequestBehavior.AllowGet);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [Route("public/content/{fileType}/{file}", Name ="RenderWebPDF")]
        public ActionResult ReadPDF(string file, string fileType)
        {
            if (fileType?.ToLower() == "addon")
            {
                file = "~/Content/AddOn/" + file + ".pdf";
            }

            try
            {
                return File(file, "application/pdf");
            }
            catch
            {
                return new HttpNotFoundResult("File not found.");
            }
        }

        [Filters.ControllerAccess.AllowOn(Localhost = true, Staging = true, Production = false)]
        public ActionResult SwitchLayout(string version = "v2")
        {
            if (Response?.Cookies["version"] != null)
            {
                Response.Cookies.Add(new HttpCookie("version", version));
            }
            else
            {
                Response.Cookies["version"].Value = version;
            }
            
            return Redirect(Request?.UrlReferrer?.ToString() ?? "/");
        }

        private byte[] GetBytesFromFile(string fullFilePath)
        {
            // this method is limited to 2^32 byte files (4.2 GB)

            FileStream fs = null;
            try
            {
                fs = System.IO.File.OpenRead(fullFilePath);
                byte[] bytes = new byte[fs.Length];
                fs.Read(bytes, 0, Convert.ToInt32(fs.Length));
                return bytes;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }
            }
        }

        #region Private
        private string GetUserIP()
        {
            string IpAddress = "xxxxxx";
            try
            {
                string Temp = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (!string.IsNullOrEmpty(Temp))
                {
                    string[] arrTemp = Temp.Split(',');
                    if (arrTemp.Length != 0)
                    {
                        IpAddress = arrTemp[0];
                    }
                }
            }
            catch { }
            
            return IpAddress;
        }
        #endregion
    }
}
