using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Web.Mvc;
using System.Security.Cryptography;
using System.IO;

namespace Mayflower.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class EncryptedActionParameterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            Dictionary<string, object> decryptedParameters = new Dictionary<string, object>();
            if (HttpContext.Current.Request.QueryString.Get("q") != null)
            {
                string encryptedQueryString = HttpContext.Current.Request.QueryString.Get("q");
                string decrptedString = Decrypt(encryptedQueryString.ToString());
                string[] paramsArrs = decrptedString.Split('?');

                if (!string.IsNullOrEmpty(decrptedString))
                {
                    for (int i = 0; i < paramsArrs.Length; i++)
                    {
                        string[] paramArr = paramsArrs[i].Split('=');
                        decryptedParameters.Add(paramArr[0], Convert.ToInt32(paramArr[1]));
                    }
                }
            }
            for (int i = 0; i < decryptedParameters.Count; i++)
            {
                filterContext.ActionParameters[decryptedParameters.Keys.ElementAt(i)] = decryptedParameters.Values.ElementAt(i);
            }
            base.OnActionExecuting(filterContext);

        }

        public string Decrypt(string encryptedText)
        {
            var privateKey = "<RSAKeyValue><Modulus>yj/bd1/xrvqTMSZ7IuH8jKynqESl+C/9VqWjJ6yyMoY7UFicLXiFLVQ/bvNu7UB3KBUqHBBQ65qVFqLtNeRiVIhhdnCge8k7lJsvTm8/2OHGJ0Sd5hStWHLcZCyDNLDT07Dh3+XelXSQulKPIrhA4tDgXYyXpb5nlE1YbtTvplU=</Modulus><Exponent>AQAB</Exponent><P>7qq/F5UfoChI8O+YOuh7aXoIZvATZnqbzH3lx8QGfvBuE631F0znbht9VxyovgAdfu6lF3OEExG3was1uNt6MQ==</P><Q>2PAK5SzYqMs57W6oNlpo/eedknrk0RreoAVuq2FL8Mmu/HuhT5ocOkkMAH3EaZo/5xmA/5WobsERUX25yvJBZQ==</Q><DP>kRQeARnXFaYnYL5kTTrQ+jcCMICzalIRrubA7QQN5tAEOdY+7CEFdXskX/W95XFwfJ5YoL7JhEX667FFgM95oQ==</DP><DQ>w2zKg9U4gCZDUs6ingQoHMKvwisPQgfwkTsTjTOjE5C8IBrHIEx2LVNsimzBqVgZRPhGqveIue0WytB1tIszuQ==</DQ><InverseQ>mQejYGK/lPJX3gADPPlb92tGTCXSovIYQpscbhskCvv7PkTA2kY3BCnkD2yQAswZcinbRuntWORl3hx1bzsaPA==</InverseQ><D>CgJyDqzpbbMKEN8qLfZIRQAQhiProOZjH+QvuHl0EksRaW8RP7DcynsGbqvOnCBaJVoyzNPD5X0vjsC+g7HLafQPgR5070YsivAw2oPs/n6GNd3uZdZQ5ZSj2MLgUh/NsXuv0zdc8lLMBxqMfOXtT+ITVF9Vj0t/w7Ub/62Yw6E=</D></RSAKeyValue>";

            using (var rsa = new RSACryptoServiceProvider(1024))
            {
                try
                {
                    rsa.FromXmlString(privateKey);
                    var resultBytes = Convert.FromBase64String(encryptedText);
                    var decryptedBytes = rsa.Decrypt(resultBytes, true);
                    var decryptedData = System.Text.Encoding.UTF8.GetString(decryptedBytes);

                    return decryptedData;
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}