using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace Mayflower.General
{
    public class CustomizeBaseEncoding
    {
        public static string CodeBase64(string Token)
        {
            byte[] TokenBytes = Encoding.Unicode.GetBytes(Token);
            string Base64Token = Convert.ToBase64String(TokenBytes);
            return Base64Token;
        }

        public static string DeCodeBase64(string Token)
        {
            try
            {
                byte[] Base64Token = Convert.FromBase64String(Token);
                string TokenString = Encoding.Unicode.GetString(Base64Token);
                return TokenString;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}