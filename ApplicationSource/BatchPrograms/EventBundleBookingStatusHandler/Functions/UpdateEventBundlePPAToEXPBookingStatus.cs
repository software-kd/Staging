using Alphareds.Library.DatabaseHandler;
using Alphareds.Module.Common;
using Alphareds.Module.Model.Database;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBundleBookingStatusHandler.Functions
{
    class UpdateEventBundlePPAToEXPBookingStatus
    {
        private static DatabaseHandlerMain dbADO = new DatabaseHandlerMain();
        #region Member variables Declarations

        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog emaillog = log4net.LogManager.GetLogger("EmailLogger");

        private static MayFlower db = new MayFlower();

        #endregion

        public static void Update(EventBooking item)
        {
            try
            {
                log.Debug("Update EventBundlePPABokingStatus Started. (" +item.SuperPNRNo+ ")");

                SqlCommand command = new SqlCommand();
                if (command.Connection == null)
                {
                    command = dbADO.OpenConnection(command);
                }

                var UpdateEventBookingQuery = "EXEC EventMgt.usp_EventBookingUpdate @BookingID, @BookingStatusCode, @ModifiedByID";
                command.CommandText = UpdateEventBookingQuery;
                command.Parameters.Clear();

                #region SQL Parameter
                SqlParameter[] sqlParam = new SqlParameter[] {
                    new SqlParameter("BookingID", item.BookingID),
                    new SqlParameter("BookingStatusCode", "EXP"),
                    new SqlParameter("ModifiedByID", "0"),
                };
                #endregion

                command.Parameters.AddRange(sqlParam);

                dbADO.UpdateDataByStoredProcedure(command);

                command.Transaction.Commit();
                log.Debug("Update EventBundlePPABokingStatus Ended. (" + item.SuperPNRNo + ")");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete update EventBundlePPABokingStatus. (" + item.SuperPNRNo + ") ---" + ex.ToString());
                throw ex;
            }
        }

    }
}
