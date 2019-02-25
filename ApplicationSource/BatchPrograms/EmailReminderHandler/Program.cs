using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphareds.Module.Model.Database;
using Alphareds.Module.MemberController;
using Alphareds.Module.CommonController;
using Alphareds.Module.Common;
using System.Collections;
using log4net;
using log4net.Appender;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace EmailReminderHandler
{
    class Program
    {
        #region Member variables Declarations

        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog emaillog = log4net.LogManager.GetLogger("EmailLogger");
        //private static ExecutionType executionType = ExecutionType.None;
        private static MayFlower db = new MayFlower();

        #endregion

        #region Properties
        public enum ExecutionType
        {
            undefined,
            CrossSales,
            MemberActivate,
            TravelCreditExpire,
            CrossSalesIns
        }

        #endregion

        private static void SendErrorMail(string error)
        {
            try
            {
                log.Debug("SendErrorEmail Started.");

                emaillog.Debug("Error:" + error);

                log.Debug("SendErrorEmail Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete SendErrorEmail.");
                throw ex;
            }
        }

        static void Main(string[] args)
        {
            try
            {
                log.Debug("Email Handler Started.");

                var emailLists = db.EmailMarketingQueues;
                foreach(var list in emailLists)
                {
                    ExecutionType executionType = ExecutionType.undefined;
                    Enum.TryParse(list.FunctionType, out executionType);

                    log.Debug("Execution Type: " + executionType.ToString() + "Email: "+ list.Recipient.ToString());

                    if (executionType == ExecutionType.MemberActivate)
                    {
                        EmailReminderHandler.Functions.ActivationReminderEmail.Send(list);
                    }
                    else if (executionType == ExecutionType.CrossSales)
                    {
                        EmailReminderHandler.Functions.CrossSaleEmail.Send(list); 
                    }
                    else if (executionType == ExecutionType.TravelCreditExpire)
                    {
                        EmailReminderHandler.Functions.TravelCreditExpiredEmail.Send(list);
                    }
                    else if (executionType == ExecutionType.CrossSalesIns)
                    {
                        EmailReminderHandler.Functions.CrossSalesInsuranceEmail.Send(list);
                    }
                }
                log.Debug("Email Handler Process Completed.");
            }
            catch (Exception ex)
            {
                SendErrorMail(ex.ToString());
                log.Error("Unable to complete the Reminder Handler process.", ex);
            }
        }
    }
}
