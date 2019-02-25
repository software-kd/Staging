using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SendPDFQueueHandler.Functions
{
    public class Helper
    {
        #region Helper Method
        public static string GetAppSettingValueEnhanced(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                { throw new Exception("Key empty cannot be accepted."); }

                var appSetting = System.Configuration.ConfigurationManager.AppSettings[key];
                if (appSetting == null)
                { throw new Exception("App Setting not found."); }

                return appSetting.ToString();
            }
            catch (Exception)
            {
            }
            return string.Empty;
        }
        #endregion

    }
}
