using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphareds.Module.Model.Database;
using SendPDFQueueHandler.Model;

namespace SendPDFQueueHandler.Functions
{
    public class CallAPI
    {
        public SendPDFRespond SendPDF(SuperPNR item, bool withExceptionMsg = false)
        {
            List<string> logMsg = new List<string>();
            var _resp = new SendPDFRespond();

            #region Email PDF Section
            SemaphoreSlim semaphoreSlimInternal = new SemaphoreSlim(1, 1);
            bool sendStatus = false;
            try
            {
                //api/internal_func/pdf/send
                semaphoreSlimInternal.Wait();
                var pushObj = new ExpandoObject() as IDictionary<string, Object>;

                pushObj.Add("eid", Alphareds.Module.Cryptography.Cryptography.AES.Encrypt(item.SuperPNRID.ToString()));
                pushObj.Add("privateKey", Helper.GetAppSettingValueEnhanced("InternalAppKey"));

                var resp = GetPOSTRespond<SendPDFRespond, object>("api/internal_func/pdf/send", pushObj);
                resp.Wait();

                if (resp.IsCompleted)
                {
                    semaphoreSlimInternal.Release();
                    sendStatus = resp.Result?.SendStatus ?? false;
                }

                if (resp.Result?.ErrMsg != null)
                {
                    throw new Exception(resp.Result.ErrMsg);
                }

                logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNRNo} pdf send status : {sendStatus} ");

                return resp.Result;
            }
            catch (AggregateException ae)
            {
                logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNRNo} pdf send status : {sendStatus} "
                    + (withExceptionMsg ? Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                    + ae.ToString() + Environment.NewLine : null));
            }
            catch (Exception ex)
            {
                logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNRNo} pdf send status : {sendStatus} "
                    + (withExceptionMsg ? Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                    + ex.ToString() + Environment.NewLine : null));
            }
            #endregion

            return default(SendPDFRespond);
        }

        public SendPDFRespond SendPDFAfterSuccess(SuperPNR item, bool withExceptionMsg = false)
        {
            List<string> logMsg = new List<string>();
            var _resp = new SendPDFRespond();

            #region Email PDF Section
            SemaphoreSlim semaphoreSlimInternal = new SemaphoreSlim(1, 1);
            bool sendStatus = false;
            try
            {
                //api/internal_func/pdf/send
                semaphoreSlimInternal.Wait();
                var resp = GetPOSTRespond<SendPDFRespond, int>("api/internal_func/pdf/sendpdf", item.SuperPNRID);
                resp.Wait();

                if (resp.IsCompleted)
                {
                    semaphoreSlimInternal.Release();
                    sendStatus = resp.Result.SendStatus;
                }

                if (resp.Result.ErrMsg != null)
                {
                    throw new Exception(resp.Result.ErrMsg);
                }

                logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNRNo} SendPDFAfterSuccess pdf send status : {sendStatus} ");

                return resp.Result;
            }
            catch (AggregateException ae)
            {
                logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNRNo} SendPDFAfterSuccess pdf send status : {sendStatus} "
                    + (withExceptionMsg ? Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                    + ae.ToString() + Environment.NewLine : null));
            }
            catch (Exception ex)
            {
                logMsg.Add($"SuperPNR {item.SuperPNRID} - {item.SuperPNRNo} SendPDFAfterSuccess pdf send status : {sendStatus} "
                    + (withExceptionMsg ? Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                    + ex.ToString() + Environment.NewLine : null));
            }
            #endregion

            return default(SendPDFRespond);
        }

        public SendPDFRespond ForwardPDF(int SuperPNRID, Alphareds.Module.PDFEngineWebService.PDFEngine.emailAddress[] emailArray, bool withExceptionMsg = false, bool isAgentSendB2C = false)
        {
            List<string> logMsg = new List<string>();
            var _resp = new SendPDFRespond();

            #region Email PDF Section
            SemaphoreSlim semaphoreSlimInternal = new SemaphoreSlim(1, 1);
            bool sendStatus = false;
            try
            {
                //api/internal_func/pdf/send
                semaphoreSlimInternal.Wait();
                var pushObj = new ExpandoObject() as IDictionary<string, Object>;

                pushObj.Add("SuperPNRID", SuperPNRID);
                pushObj.Add("email", emailArray);
                pushObj.Add("isAgentSendB2C", isAgentSendB2C.ToString());

                var resp = GetPOSTRespond<SendPDFRespond, object>("api/internal_func/pdf/forward", pushObj);
                resp.Wait();

                if (resp.IsCompleted)
                {
                    semaphoreSlimInternal.Release();
                    sendStatus = resp.Result.SendStatus;
                }

                if (resp.Result.ErrMsg != null)
                {
                    throw new Exception(resp.Result.ErrMsg);
                }

                logMsg.Add($"SuperPNRID {SuperPNRID} ForwardPDF pdf send status : {sendStatus} ");

                return resp.Result;
            }
            catch (AggregateException ae)
            {
                logMsg.Add($"SuperPNRID {SuperPNRID} ForwardPDF pdf send status : {sendStatus} "
                    + (withExceptionMsg ? Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                    + ae.ToString() + Environment.NewLine : null));
            }
            catch (Exception ex)
            {
                logMsg.Add($"SuperPNRID {SuperPNRID} ForwardPDF pdf send status : {sendStatus} "
                    + (withExceptionMsg ? Environment.NewLine + Environment.NewLine + " with Exception:" + Environment.NewLine
                    + ex.ToString() + Environment.NewLine : null));
            }
            #endregion

            return default(SendPDFRespond);
        }

        #region HTTP Client Method
        protected async Task<T1> GetPOSTRespond<T1, T2>(string url, T2 postModel)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(Helper.GetAppSettingValueEnhanced("InternalAppWebAPIUrl"));
                    //HTTP POST
                    var postTask = await client.PostAsJsonAsync<T2>(url, postModel).ConfigureAwait(continueOnCapturedContext: false);

                    if (postTask.IsSuccessStatusCode)
                    {
                        var resStr = await postTask.Content.ReadAsAsync<T1>();

                        return resStr;
                    }
                }
                return default(T1);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        #endregion
    }
}
