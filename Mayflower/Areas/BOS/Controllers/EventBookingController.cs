using Alphareds.Module.Common;
using Alphareds.Module.Cryptography;
using Alphareds.Module.Model.Database;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace Mayflower.Areas.BOS.Controllers
{
    [Filters.ControllerAccess.AllowOn(Localhost = true, Staging = true, Production = false)]
    public class EventBookingController : ApiController
    {
        Logger logger = LogManager.GetCurrentClassLogger();

        [HttpGet]
        [Route("api/bos/tools/event/getfiles")]
        // GET: BOS/Tools
        public HttpResponseMessage GetFiles(string eid)
        {
            string _parseId = null;
            bool parseStatus = Cryptography.AES.TryDecrypt(eid, out _parseId);
            int? _id = _parseId.ToIntNullable();

            if (_id.HasValue)
            {
                MayFlower db = new MayFlower();
                var _reg = db.EventMasters.FirstOrDefault(x => x.EventID == _id);

                var eventProperties = _reg.GetType().GetProperties();

                var pushObj = new ExpandoObject() as IDictionary<string, Object>;

                foreach (var item in eventProperties)
                {
                    if (item.PropertyType.Name == "String")
                    {
                        string _columnValue = (string)item.GetValue(_reg);
                        if (!string.IsNullOrWhiteSpace(_columnValue) && _columnValue.StartsWith("~/"))
                        {
                            string _uriString = Url.Link("DefaultApi", new { controller = "Home" });

                            var uriBuilder = new UriBuilder(_uriString);
                            uriBuilder.Path = _columnValue.Replace("~/", "");
                            string _outputUrl = uriBuilder.ToString();

                            // push image url into expando object
                            pushObj.Add(item.Name, _outputUrl);

                            string phsyPath = HttpContext.Current.Server.MapPath(_columnValue);
                        }
                    }
                }

                if (pushObj.Count > 0)
                {
                    var chkFile = pushObj.Select(x =>
                    {
                        UriBuilder uriBuilder = new UriBuilder(x.Value.ToString());
                        string serverPath = HttpContext.Current.Server.MapPath(uriBuilder.Path);

                        var _innerObj = new ExpandoObject() as IDictionary<string, Object>;
                        _innerObj.Add("ColumnName", x.Key);
                        _innerObj.Add("Url", x.Value);
                        _innerObj.Add("Valid", File.Exists(serverPath));

                        return _innerObj;
                    });

                    return Request.CreateResponse(HttpStatusCode.OK, chkFile);
                }
            }

            var message = string.Format("Bad request!");
            HttpError err = new HttpError(message);
            return Request.CreateResponse(HttpStatusCode.BadRequest, err);
        }

        [HttpPost]
        [Route("api/bos/tools/event/uploadfiles")]
        public async Task<HttpResponseMessage> UploadFiles()
        {
            // Check if the request contains multipart/form-data.
            if (!Request.Content.IsMimeMultipartContent())
            {
                return Request.CreateResponse(HttpStatusCode.UnsupportedMediaType, new HttpError("Unsupported Media Type."));
            }

            var provider = new MultipartMemoryStreamProvider();

            try
            {
                // Read the form data.
                await Request.Content.ReadAsMultipartAsync(provider);
                string eid = null;

                // Get event id
                foreach (var content in provider.Contents)
                {
                    if (string.IsNullOrWhiteSpace(content.Headers.ContentDisposition.FileName))
                    {
                        var _resValue = await content.ReadAsStringAsync();

                        if (content.Headers.ContentDisposition.Name != null &&
                            content.Headers.ContentDisposition.Name.Replace("\"", "").ToLower() == "eid")
                        {
                            eid = _resValue;
                            break;
                        }
                    }
                }

                if (eid != null)
                {
                    string _parseId = null;
                    bool parseStatus = Cryptography.AES.TryDecrypt(eid, out _parseId);
                    int? _id = _parseId.ToIntNullable();

                    if (_id.HasValue)
                    {
                        MayFlower db = new MayFlower();
                        var _event = db.EventMasters.FirstOrDefault(x => x.EventID == _id);

                        if (_event != null)
                        {
                            List<bool> updated = new List<bool>();
                            var _outputObj = new ExpandoObject() as IDictionary<string, Object>;

                            // Use eid to decide upload where
                            foreach (var content in provider.Contents)
                            {
                                string formFieldName = content.Headers?.ContentDisposition?.Name?.Replace("\"", "");

                                if (string.IsNullOrWhiteSpace(formFieldName))
                                {
                                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                                        new HttpError("Form field name cannot empty."));
                                }
                                else if (!string.IsNullOrWhiteSpace(content.Headers.ContentDisposition.FileName))
                                {
                                    var _uploadedFile = await content.ReadAsByteArrayAsync();
                                    System.Diagnostics.Trace.WriteLine(content.Headers.ContentDisposition.FileName);
                                    string relativePath = "~/content/addon/" + _event.EventTypeCode.Trim();
                                    string root = HttpContext.Current.Server.MapPath(relativePath);

                                    // Create folder path
                                    if (!Directory.Exists(root))
                                    {
                                        Directory.CreateDirectory(root.ToLower());
                                    }

                                    string filePath = @"\" + $"{_event.EventID}_" + content.Headers.ContentDisposition.FileName.Replace("\"", "").Replace(' ', '_');
                                    root += filePath;
                                    
                                    if (File.Exists(root))
                                    {
                                        root = root.Replace(filePath, @"\" + $"{_event.EventID}_" + $"{DateTime.Now.ToLoggerDateTime()}_"
                                            + content.Headers.ContentDisposition.FileName.Replace("\"", "").Replace(' ', '_'));

                                        // Rename new file only, old file keep.
                                        //FileInfo fileInfo = new FileInfo(root);
                                        //File.Move(root, root.Replace(fileInfo.Extension, $"_{DateTime.Now.ToLoggerDateTime()}" + fileInfo.Extension));
                                    }

                                    try
                                    {
                                        File.WriteAllBytes(root.ToLower(), _uploadedFile);

                                        var eventProperties = _event.GetType().GetProperties();

                                        foreach (var item in eventProperties)
                                        {
                                            if (item.Name.ToLower() == formFieldName.ToLower())
                                            {
                                                string _relativeUriSavePath = relativePath + filePath.Replace(@"\", @"/");
                                                item.SetValue(_event, _relativeUriSavePath.ToLower());
                                                updated.Add(true);
                                                break;
                                            }
                                        }

                                        if (updated.Any(x => !x))
                                        {
                                            var _errMessage = string.Format("Error while attempt to save the files after success save uploaded file.");
                                            HttpError _err = new HttpError(_errMessage);
                                            return Request.CreateResponse(HttpStatusCode.InternalServerError, _err);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        var _errMessage = string.Format("Error while attempt to save the files. " + Environment.NewLine + Environment.NewLine
                                            + "Tech Error:" + Environment.NewLine + ex.GetBaseException().Message);
                                        HttpError _err = new HttpError(_errMessage);
                                        return Request.CreateResponse(HttpStatusCode.InternalServerError, _err);
                                    }
                                }
                            }

                            bool outputStauts = updated.All(x => x);
                            _outputObj.Add("UploadCount", updated.Count);
                            _outputObj.Add("Status", outputStauts);

                            if (outputStauts)
                            {
                                db.SaveChanges();
                            }

                            return Request.CreateResponse(HttpStatusCode.OK, _outputObj);
                        }
                    }
                }

                var message = string.Format("Invalid 'eid'!");
                HttpError err = new HttpError(message);
                return Request.CreateResponse(HttpStatusCode.BadRequest, err);
            }
            catch (Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }

            #region MultipartStreamProvider Method
            /*
            try
            {
                // Read the form data.
                await Request.Content.ReadAsMultipartAsync(provider);

                // Show all the key-value pairs.
                foreach (var key in provider.FormData.AllKeys)
                {
                    foreach (var val in provider.FormData.GetValues(key))
                    {
                        System.Diagnostics.Trace.WriteLine(string.Format("{0}: {1}", key, val));
                    }
                }

                // This illustrates how to get the file names.
                foreach (MultipartFileData file in provider.Contents)
                {
                    System.Diagnostics.Trace.WriteLine(file.Headers.ContentDisposition.FileName);
                    System.Diagnostics.Trace.WriteLine("Server file path: " + file.LocalFileName);
                }
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (System.Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
            */
            #endregion
        }

        #region Draft Not In Use
        /*
        // GET api/<controller>
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<controller>/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<controller>
        public void Post([FromBody]string value)
        {
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }
        */
        #endregion
    }
}