using Alphareds.Module.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Alphareds.Module.Model.Database;
using Alphareds.Module.ServiceCall;
using System.Collections;
using Alphareds.Module.BookingController;
using System.Data;
using Alphareds.Library.DatabaseHandler;
using System.Net;
using System.Xml;
using log4net;
using log4net.Appender;
using Alphareds.Module.PaymentController;
using Alphareds.Module.CommonController;
using Alphareds.Module.HotelController;
using Alphareds.Module.Model;
using Newtonsoft.Json;
using System.Data.Entity.Validation;

//Here is the once-per-application setup information
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Alphareds.BatchPrograms.BookingStatusHandler
{
    class Program
    {
        #region Member variables Declarations

        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog emaillog = log4net.LogManager.GetLogger("EmailLogger");
        private static readonly log4net.ILog rhiLog = log4net.LogManager.GetLogger("RHILogger");
        private static ExecutionType executionType;
        private static MayFlower db = new MayFlower();
        private static DatabaseHandlerMain dbADO = new DatabaseHandlerMain();

        private static List<string> statusRequireAirlineCancellationChecking = new List<string>
        {
            Enumeration.SMCBookingStatus.PAP.ToString(),
            Enumeration.SMCBookingStatus.APP.ToString(),
            Enumeration.SMCBookingStatus.PCP.ToString(),
            Enumeration.SMCBookingStatus.PPA.ToString(),
            Enumeration.SMCBookingStatus.PPV.ToString(),
            Enumeration.SMCBookingStatus.FPV.ToString()
        };
        private static List<string> statusRequireRetryBooking = new List<string> {
            Enumeration.SMCBookingStatus.PAP.ToString(),
            Enumeration.SMCBookingStatus.APP.ToString(),
            Enumeration.SMCBookingStatus.PCP.ToString(),
            Enumeration.SMCBookingStatus.PPA.ToString(),
            Enumeration.SMCBookingStatus.PPV.ToString(),
            Enumeration.SMCBookingStatus.FPV.ToString(),
            Enumeration.SMCBookingStatus.RHI.ToString()
        };

        private static List<string> statusRequireCancellation = new List<string> { Enumeration.SMCBookingStatus.REJ.ToString() };
        private static List<string> statusRequireTicketChecking = new List<string> { Enumeration.SMCBookingStatus.QPL.ToString() };
        //staging test use CON //production use TKI
        private static List<string> statusRequireBookingNoChecking = new List<string> { Enumeration.SMCBookingStatus.TKI.ToString() };
        private static double takeBookingPaymentAfterMinute = Convert.ToDouble(ConfigurationManager.AppSettings["MinuteRetryBooking"].ToString());
        #endregion

        #region Constructors & Finalizers



        #endregion

        #region Properties

        public enum ExecutionType
        {
            TicketingStatus,
            ExpiredStatus,
            PaymentStatus,
            BookingNoStatus,
            Test
        }

        #endregion

        #region Methods

        private static void UpdateTicketNumber(int bookingid, Pax pax, string ticketNumber, SqlCommand command)
        {
            try
            {
                log.Debug("UpdateTicketNumber Started.");

                int userid = 1;
                DateTime issueDate = DateTime.Now;
                //sample "TE 2051247585094-MY FOO/C E1FD*AET 1659/09JAN*I"
                try
                {
                    issueDate = DateTime.ParseExact(ticketNumber.Split(' ')[4].Split('*')[0], "HHmm/ddMMM", null);
                    while (issueDate > DateTime.Now)
                    {
                        issueDate = issueDate.AddYears(-1);
                    }
                }
                catch (Exception ex)
                {
                    SendErrorMail("Ticket Issue Date error. Ticket Number:" + ticketNumber + ". Error:" + ex);
                    issueDate = DateTime.Now;
                }


                pax.ModifiedByID = userid;
                pax.TicketNumber = ticketNumber;
                pax.TicketIssueDate = issueDate;
                BookingServiceController.updatePax(pax, command);

                log.Debug("UpdateTicketNumber Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete UpdateTicketNumber.");
                throw ex;
            }
        }

        private static void UpdateBookingStatus(string bookingStatus, int bookingid, string remark, SqlCommand command)
        {
            try
            {
                log.Debug("UpdateBookingStatus Started.");

                int userid = 1;

                Booking booking = new Booking();
                //List<Booking> bookings = db.Bookings.Where(x => statusRequireTicketChecking.Any(y => x.BookingStatusCode.Equals(y))).ToList();
                //booking = db.Bookings.Find(1);
                booking = db.Bookings.FirstOrDefault(x => x.BookingID == bookingid);
                booking.BookingStatusCode = bookingStatus;
                booking.ModifiedByID = userid;

                Enumeration.SMCBookingActivityType activityType = new Enumeration.SMCBookingActivityType();
                if (bookingStatus.Equals(Enumeration.SMCBookingStatus.TKI.ToString()))
                {
                    activityType = Enumeration.SMCBookingActivityType.TKI;
                }
                else if (bookingStatus.Equals(Enumeration.SMCBookingStatus.EXP.ToString()))
                {
                    activityType = Enumeration.SMCBookingActivityType.EXP;
                }
                else
                {
                    activityType = Enumeration.SMCBookingActivityType.EXP;
                }

                BookingServiceController.updateBooking(booking, command);
                BookingServiceController.InsertBookingProgress(activityType, bookingid, remark, userid, command);

                //SendBookingStatusUpdateEmail(bookingid);

                log.Debug("UpdateBookingStatus Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete UpdateBookingStatus.");
                throw ex;
            }
        }

        private static void UpdateBookingNo(Booking booking, string supplierRefId, SqlCommand command)
        {
            try
            {
                log.Debug("Update Booking No. Started.");
                int userid = 1;
                
                booking.BookingNo = supplierRefId;
                booking.ModifiedByID = userid;
                BookingServiceController.updateBooking(booking, command);
                               

                log.Debug("Update Booking No. Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete Update Booking No.");
                throw ex;
            }
        }

        private static void UpdateBookingFlightSegment(List<FlightSegment> flightSegmentList, string supplierRefId, SqlCommand command)
        {
            try
            {
                log.Debug("Update Booking All Flight Segment List Started. For bookingID :" + flightSegmentList.FirstOrDefault().BookingID);

                foreach (var flightSegment in flightSegmentList)
                {
                    try
                    {
                        flightSegment.SupplierRefId = supplierRefId;
                        BookingServiceController.updateFlightSegment(flightSegment, command);
                    }
                    catch(Exception ex)
                    {
                        log.Debug("Fail update flight segment on bookingID :" + flightSegmentList.FirstOrDefault().BookingID + " - Flight Segment ID :" + flightSegment.FlightSegmentID);
                    }
                } 
                log.Debug("Update Booking All Flight Segment Completed. For bookingID :" + flightSegmentList.FirstOrDefault().BookingID);
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete Update Booking Flight Segnment. For bookingID :" + flightSegmentList.FirstOrDefault().BookingID);
                throw ex;
            }
        }

        //Need Refactor once email function has been refactor
        private static void SendBookingStatusUpdateEmail(string bookingid)
        {
            try
            {
                log.Debug("SendBookingStatusUpdateEmail Started.");

                Booking booking = new Booking();
                User user = new User();
                Pax pax = new Pax();
                string name = string.Empty;
                string emailAddress = string.Empty;

                booking = db.Bookings.FirstOrDefault(x => x.BookingID.ToString().Equals(bookingid));
                pax = db.Paxes.FirstOrDefault(x => x.BookingID == booking.BookingID && ((x.IsContactPerson == null) ? false : true));
                if (pax == null)//if contact pax not found get from user profile
                {
                    user = db.Users.FirstOrDefault(x => x.UserID == booking.UserID);
                    name = user.UserDetails.FirstOrDefault(x => x.UserID == user.UserID).FullName;
                    emailAddress = user.Email;
                }
                else
                {
                    name = pax.GivenName + " " + pax.Surname;
                    emailAddress = pax.PassengerEmail;
                }

                Hashtable ht = new Hashtable();
                ht.Add("<#UserName>", name);
                ht.Add("<#DepartureStation>", booking.Origin);
                ht.Add("<#ArrivalStation>", booking.Destination);
                ht.Add("<#BookingID>", booking.BookingNo);
                ht.Add("<#BookingStatus>", db.BookingStatus.FirstOrDefault(x => x.BookingStatusCode.Equals(booking.BookingStatusCode)).BookingStatus);

                if (booking.BookingStatusCode.Equals(Enumeration.SMCBookingStatus.EXP.ToString()))
                {
                    ht.Add("<#ExtraInfo>", "Your booking had passed the payment expiry date. Please make another booking if you still wish to fly to this destination.");
                }
                else if (booking.BookingStatusCode.Equals(Enumeration.SMCBookingStatus.TKI.ToString()) && !string.IsNullOrWhiteSpace(booking.TicketNumber))
                {
                    ht.Add("<#ExtraInfo>", "Ticket has been issue for the booking. Ticket Number: " + booking.TicketNumber);
                }

                CommonServiceController.SendEmail(emailAddress, "Flight Booking Status Updated", Core.getMailTemplate("sendbookingupdatestatusreminder", ht));

                log.Debug("SendBookingStatusUpdateEmail Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete SendBookingStatusUpdateEmail.");
                throw;
            }
        }

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
                throw;
            }
        }

        private static void SendRHIMail(string superPNRNo, int superPNRId, string additionalMsg = null)
        {
            try
            {
                log.Debug("SendErrorEmail Started.");

                string msg = string.Format(@"Dear Support, {0}
                                            There are errors while customer attemp to reserver the booking. Further action below required: {0}{0}
                                            Additional Info: {0}
                                            {1} - {2}{0}
                                            ""Flight ({1} - {2})"": 0",
                                            Environment.NewLine,
                                            superPNRId,
                                            superPNRNo);

                if (additionalMsg != null)
                {
                    msg += $"{Environment.NewLine}{Environment.NewLine}{additionalMsg}";
                }

                rhiLog.Debug(msg);

                log.Debug("SendErrorEmail Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to Complete SendErrorEmail.");
                throw;
            }
        }

        private static void CheckAirlineCancellation()
        {
            try
            {
                log.Debug("CheckAirlineCancellation Started.");

                List<string> updatedBookingIds = new List<string>();
                List<Booking> bookings = db.Bookings.Where(x => x.SupplierCode == "SBRE" && statusRequireAirlineCancellationChecking.Any(y => x.BookingStatusCode.Equals(y))).ToList();

                if (!ConfigurationManager.AppSettings["IsSabreLive"].ToString().Equals("true"))
                {
                    List<Booking> confirmbookings = db.Bookings.Where(x => x.SupplierCode == "SBRE" && x.BookingStatusCode.Equals(Enumeration.SMCBookingStatus.CON.ToString())).ToList();
                    log.Debug("[CON Booking STAG] Cancelling staging Confirm Booking PNR. Count = " + confirmbookings.Count().ToString());
                    foreach (Booking b in confirmbookings)
                    {
                        SqlCommand command = new SqlCommand();
                        if (DateTime.Now > b.BookingExpiryDate || b.BookingExpiryDate.Value.ToString("yyyyMMdd").Equals(DateTime.MaxValue.ToString("yyyyMMdd")))
                        {
                            Alphareds.Module.SabreWebService.SWS.ReadTravelItineraryResponse rs = SabreServiceCall.ReadTravelItineraryResponse(b.BookingNo);
                            if (rs.Output != null)
                            {
                                List<Alphareds.Module.SabreWebService.SWS.ReservationItem> reservationItems = rs.Output.PassengerNameRecord.ReservationItems.Where(x => x.Type.ToString().Equals("FlightSegment")).ToList();

                                if (reservationItems.Count() == 0)
                                {
                                    log.Debug("[CON Booking STAG] Reservation Items not in PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                    try
                                    {
                                        UpdateBookingStatus("TPC", b.BookingID, "Booking Cancelled by Airline.", command);
                                        updatedBookingIds.Add(b.BookingID.ToString());
                                        //PaymentServiceController.updatePaymentIPay(b, command, "FAIL", string.Empty);
                                        Payment latestPayment = b.Payments.OrderByDescending(x => x.CreatedDate).FirstOrDefault();
                                        if (latestPayment != null)
                                        {
                                            latestPayment.PaymentStatusCode = "FAIL";
                                            //latestPayment.Ipay88TransactionID = string.Empty;
                                            PaymentDatabaseHandler.UpdatePaymentIpay88(dbADO, latestPayment, command);
                                        }

                                        command.Transaction.Commit();
                                    }
                                    catch (Exception ex)
                                    {
                                        if (command.Transaction != null)
                                        {
                                            command.Transaction.Rollback();
                                        }
                                        //throw ex;//comment to prevent error causing PNR that is ok to stop being processed.
                                        SendErrorMail(ex.ToString());
                                    }
                                    finally
                                    {
                                        if (command.Connection != null)
                                        {
                                            command.Connection.Close();
                                        }
                                    }
                                }
                                else
                                {
                                    Alphareds.Module.SabreWebService.SWS.CancelTravelItineraryResponse crs = SabreServiceCall.CancelTravelItinerary(b.BookingNo);
                                    if (crs.Output)
                                    {
                                        log.Debug("[CON Booking STAG] Cancellation Succcessful for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                        try
                                        {
                                            UpdateBookingStatus("TPC", b.BookingID, "Booking Cancelled by Airline.", command);
                                            updatedBookingIds.Add(b.BookingID.ToString());
                                            //PaymentServiceController.updatePaymentIPay(b, command, "FAIL", string.Empty);
                                            Payment latestPayment = b.Payments.OrderByDescending(x => x.CreatedDate).FirstOrDefault();
                                            if (latestPayment != null)
                                            {
                                                latestPayment.PaymentStatusCode = "FAIL";
                                                //latestPayment.Ipay88TransactionID = string.Empty;
                                                PaymentDatabaseHandler.UpdatePaymentIpay88(dbADO, latestPayment, command);
                                            }

                                            command.Transaction.Commit();
                                        }
                                        catch (Exception ex)
                                        {
                                            if (command.Transaction != null)
                                            {
                                                command.Transaction.Rollback();
                                            }
                                            //throw ex;//comment to prevent error causing PNR that is ok to stop being processed.
                                            SendErrorMail(ex.ToString());
                                        }
                                        finally
                                        {
                                            if (command.Connection != null)
                                            {
                                                command.Connection.Close();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.AppendLine(crs.Header.Error.TraceId);
                                        sb.AppendLine(crs.Header.Error.ErrorMessage);

                                        log.Error("[CON Booking STAG] Cancellation Failed for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo, new Exception(sb.ToString()));
                                        SendErrorMail("[CON Booking STAG] Cancellation Failed for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo + sb.ToString());
                                    }
                                }
                            }
                            else
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine(rs.Header.Error.TraceId);
                                sb.AppendLine(rs.Header.Error.ErrorMessage);

                                log.Error("[CON Booking STAG] Unable to retrive data for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo, new Exception(sb.ToString()));
                            }
                        }
                    }
                }

                //&& db.FlightSegments.OrderBy(fs => fs.SegmentOrder).FirstOrDefault(fs => fs.BookingID == x.BookingID).DepartureDateTime > DateTime.Now).ToList();

                foreach (Booking b in bookings)
                {
                    SqlCommand command = new SqlCommand();

                    string ipay88transactionID = string.Empty;
                    bool isAuthCapt = false;

                    if (CheckPaymentStatus(b, ref ipay88transactionID, ref isAuthCapt))
                    {
                        //if Expiration date has not been set by admin, refer to sabre data to determine if carrier has cancel the flight
                        //otherwise just expire the booking based on the expiry date set by admin
                        if (b.BookingExpiryDate.Value.ToString("yyyyMMdd").Equals(DateTime.MaxValue.ToString("yyyyMMdd")))//used string to compare because after max datetime store in db it will have different precision
                        {
                            log.Debug("Expiry Date not set for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                            Alphareds.Module.SabreWebService.SWS.ReadTravelItineraryResponse rs = SabreServiceCall.ReadTravelItineraryResponse(b.BookingNo);
                            if (rs.Output != null)
                            {
                                List<Alphareds.Module.SabreWebService.SWS.ReservationItem> reservationItems = rs.Output.PassengerNameRecord.ReservationItems.Where(x => x.Type.ToString().Equals("FlightSegment")).ToList();

                                if (reservationItems.Count() == 0)
                                {
                                    try
                                    {
                                        log.Debug("Reservation Items not in PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                        UpdateBookingStatus(Enumeration.SMCBookingStatus.EXP.ToString(), b.BookingID, "Booking Cancelled by Airline.", command);
                                        //PaymentServiceController.updatePaymentIPay(b, command, "FAIL", string.Empty);
                                        Payment latestPayment = b.Payments.OrderByDescending(x => x.CreatedDate).FirstOrDefault();
                                        if (latestPayment != null)
                                        {
                                            latestPayment.PaymentStatusCode = "FAIL";
                                            latestPayment.Ipay88TransactionID = ipay88transactionID;
                                            PaymentDatabaseHandler.UpdatePaymentIpay88(dbADO, latestPayment, command);
                                        }
                                        command.Transaction.Commit();
                                    }
                                    catch (Exception ex)
                                    {
                                        if (command.Transaction != null)
                                        {
                                            command.Transaction.Rollback();
                                        }
                                        //throw ex;//comment to prevent error causing PNR that is ok to stop being processed.
                                        SendErrorMail(ex.ToString());
                                    }
                                    finally
                                    {
                                        if (command.Connection != null)
                                        {
                                            command.Connection.Close();
                                        }
                                    }
                                }
                                else
                                {
                                    log.Debug("Checking if any PNR contain HX segment for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                    bool containsCancelledFlight = false;
                                    for (int i = 0; i < reservationItems.Count(); i++)
                                    {
                                        for (int j = 0; j < reservationItems[i].ReservationFlightSegment.Count(); j++)
                                        {
                                            if (reservationItems[i].ReservationFlightSegment[j].Status.Equals("HX"))
                                            {
                                                containsCancelledFlight = true;
                                                log.Debug("1 of the flight segment has been cancelled by airline for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                                log.Debug("Cancelling PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                                Alphareds.Module.SabreWebService.SWS.CancelTravelItineraryResponse crs = SabreServiceCall.CancelTravelItinerary(b.BookingNo);
                                                if (crs.Output)
                                                {
                                                    log.Debug("Cancellation Succcessful for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                                    try
                                                    {
                                                        UpdateBookingStatus(Enumeration.SMCBookingStatus.EXP.ToString(), b.BookingID, "Booking Cancelled by Airline.", command);
                                                        updatedBookingIds.Add(b.BookingID.ToString());
                                                        //PaymentServiceController.updatePaymentIPay(b, command, "FAIL", string.Empty);
                                                        Payment latestPayment = b.Payments.OrderByDescending(x => x.CreatedDate).FirstOrDefault();
                                                        if (latestPayment != null)
                                                        {
                                                            latestPayment.PaymentStatusCode = "FAIL";
                                                            latestPayment.Ipay88TransactionID = ipay88transactionID;
                                                            PaymentDatabaseHandler.UpdatePaymentIpay88(dbADO, latestPayment, command);
                                                        }

                                                        command.Transaction.Commit();
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        if (command.Transaction != null)
                                                        {
                                                            command.Transaction.Rollback();
                                                        }
                                                        //throw ex;//comment to prevent error causing PNR that is ok to stop being processed.
                                                        SendErrorMail(ex.ToString());
                                                    }
                                                    finally
                                                    {
                                                        if (command.Connection != null)
                                                        {
                                                            command.Connection.Close();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    StringBuilder sb = new StringBuilder();
                                                    sb.AppendLine(crs.Header.Error.TraceId);
                                                    sb.AppendLine(crs.Header.Error.ErrorMessage);

                                                    log.Error("Cancellation Failed for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo, new Exception(sb.ToString()));
                                                }

                                            }

                                            if (containsCancelledFlight)
                                                break;
                                        }
                                        if (containsCancelledFlight)
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine(rs.Header.Error.TraceId);
                                sb.AppendLine(rs.Header.Error.ErrorMessage);

                                log.Error("Unable to retrive data for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo, new Exception(sb.ToString()));
                            }
                        }
                        else if (DateTime.Now > b.BookingExpiryDate)
                        {
                            log.Debug("Expiry Date passed for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                            log.Debug("Cancelling PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                            Alphareds.Module.SabreWebService.SWS.CancelTravelItineraryResponse crs = SabreServiceCall.CancelTravelItinerary(b.BookingNo);
                            if (crs.Output)
                            {
                                log.Debug("Cancellation Succcessful for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                                try
                                {
                                    UpdateBookingStatus(Enumeration.SMCBookingStatus.EXP.ToString(), b.BookingID, "Expiry Date Passed.", command);
                                    updatedBookingIds.Add(b.BookingID.ToString());
                                    //PaymentServiceController.updatePaymentIPay(b, command, "FAIL", string.Empty);
                                    Payment latestPayment = b.Payments.OrderByDescending(x => x.CreatedDate).FirstOrDefault();
                                    if (latestPayment != null)
                                    {
                                        latestPayment.PaymentStatusCode = "FAIL";
                                        latestPayment.Ipay88TransactionID = ipay88transactionID;
                                        PaymentDatabaseHandler.UpdatePaymentIpay88(dbADO, latestPayment, command);
                                    }

                                    command.Transaction.Commit();
                                }
                                catch (Exception ex)
                                {
                                    if (command.Transaction != null)
                                    {
                                        command.Transaction.Rollback();
                                    }
                                    //throw ex;//comment to prevent error causing PNR that is ok to stop being processed.
                                    SendErrorMail(ex.ToString());
                                }
                                finally
                                {
                                    if (command.Connection != null)
                                    {
                                        command.Connection.Close();
                                    }
                                }
                            }
                            else
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine(crs.Header.Error.TraceId);
                                sb.AppendLine(crs.Header.Error.ErrorMessage);

                                log.Error("Cancellation Failed for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo, new Exception(sb.ToString()));
                                SendErrorMail("Cancellation Failed for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo + sb.ToString());
                            }

                        }
                    }
                }

                log.Debug("Updated CheckAirlineCancellation Id (" + updatedBookingIds.Count.ToString() + "):" + updatedBookingIds.Aggregate("", (x, y) => x + ", " + y));
                log.Debug("CheckAirlineCancellation Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete CheckAirlineCancelation.");
                throw ex;
            }
        }

        private static void CheckCancellation()
        {
            try
            {
                log.Debug("CheckCancellation Started.");
                SqlCommand command = new SqlCommand();

                List<string> updatedBookingIds = new List<string>();
                List<Booking> bookings = db.Bookings.Where(x => x.SupplierCode == "SBRE" && statusRequireCancellation.Any(y => x.BookingStatusCode.Equals(y))
                    && db.FlightSegments.OrderBy(fs => fs.SegmentOrder).FirstOrDefault(fs => fs.BookingID == x.BookingID).DepartureDateTime > DateTime.Now).ToList();

                foreach (Booking b in bookings)
                {
                    log.Debug("Booking Status (" + b.BookingStatusCode + ") require cancellation for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                    log.Debug("Cancelling PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                    Alphareds.Module.SabreWebService.SWS.CancelTravelItineraryResponse crs = SabreServiceCall.CancelTravelItinerary(b.BookingNo);
                    if (crs.Output)
                    {
                        log.Debug("Cancellation Succcessful for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo);
                        updatedBookingIds.Add(b.BookingID.ToString());
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine(crs.Header.Error.TraceId);
                        sb.AppendLine(crs.Header.Error.ErrorMessage);

                        log.Error("Cancellation Failed for PNR:" + b.BookingID.ToString() + "-" + b.BookingNo, new Exception(sb.ToString()));
                    }
                }

                log.Debug("Updated CheckCancellation Id (" + updatedBookingIds.Count.ToString() + "):" + updatedBookingIds.Aggregate("", (x, y) => x + ", " + y));
                log.Debug("CheckCancellation Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete CheckCancellation.");
                throw ex;
            }
        }

        private static void TicketChecking()
        {
            try
            {
                log.Debug("TicketChecking Started.");

                List<string> updatedBookingIds = new List<string>();
                List<Booking> bookings = db.Bookings.Where(x => x.SupplierCode == "SBRE" && statusRequireTicketChecking.Any(y => x.BookingStatusCode.Equals(y))).ToList();

                foreach (Booking b in bookings)
                {
                    Alphareds.Module.SabreWebService.SWS.ReadTravelItineraryResponse rs = SabreServiceCall.ReadTravelItineraryResponse(b.SupplierBookingNo);

                    if (rs.Output != null)
                    {
                        //Alphareds.Module.SabreWebService.SWS.Ticketing ticket = rs.Output.PassengerNameRecord.Tickets.LastOrDefault();
                        if (!string.IsNullOrWhiteSpace(rs.Output.PassengerNameRecord.Tickets.LastOrDefault().ETicketNumber))
                        {
                            SqlCommand command = new SqlCommand();

                            try
                            {
                                List<Pax> paxes = db.Paxes.Where(x => !(x.IsContactPerson ?? false) && (x.BookingID == b.BookingID)).ToList();
                                List<Alphareds.Module.SabreWebService.SWS.Ticketing> tickets = rs.Output.PassengerNameRecord.Tickets.Where(x => x.RPH > 0 && !string.IsNullOrWhiteSpace(x.ETicketNumber)).ToList();

                                if (paxes.Count() >= tickets.Count())
                                {
                                    for (int i = 0; i < paxes.Count; i++)
                                    {
                                        try
                                        {
                                            UpdateTicketNumber(b.BookingID, paxes[i], tickets[i].ETicketNumber, command);
                                            UpdateBookingStatus(Enumeration.SMCBookingStatus.TKI.ToString(), b.BookingID, "Ticket Issued.", command);
                                            updatedBookingIds.Add(b.BookingID.ToString() + "-" + b.SupplierBookingNo);
                                            Alphareds.Module.SabreWebService.SWS.SendETicketResponse ticketRs = SabreServiceCall.SendETicket(b.BookingNo);
                                            log.Debug("Ticket has been issued for PNR:" + b.SupplierBookingNo.ToString() + "-" + b.SupplierBookingNo);
                                        }
                                        catch (Exception ex)
                                        {
                                            string errMsg = $"SuperPNR {b.SuperPNRID} - {b.SuperPNRNo}: Ticket issued less than book paxes. " + Environment.NewLine +
                                                $"SupplierBookingNo: {b.SupplierBookingNo} / {b.BookingNo}" + Environment.NewLine +
                                                $"Pax Count: {paxes.Count}" + Environment.NewLine + $"Ticket Count: {tickets.Count}" + Environment.NewLine +
                                                $"Sabre rs.Output Ticket Count: {rs.Output.PassengerNameRecord.Tickets.Length}";
                                            log.Error(errMsg, ex);
                                            SendErrorMail(errMsg + Environment.NewLine + Environment.NewLine + ex.ToString());
                                        }
                                    }
                                }
                                else
                                {
                                    string errMsg = $"SuperPNR {b.SuperPNRID} - {b.SuperPNRNo}: Ticket issued more than book paxes. " + Environment.NewLine +
                                        $"SupplierBookingNo: {b.SupplierBookingNo} / {b.BookingNo}" + Environment.NewLine +
                                        $"Pax Count: {paxes.Count}" + Environment.NewLine + $"Ticket Count: {tickets.Count}" + Environment.NewLine +
                                        $"Sabre rs.Output Ticket Count: {rs.Output.PassengerNameRecord.Tickets.Length}";
                                    log.Error(errMsg);
                                    SendErrorMail(errMsg);
                                }

                                command.Transaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                if (command.Transaction != null)
                                {
                                    command.Transaction.Rollback();
                                }
                                //throw ex;//comment to prevent error causing PNR that is ok to stop being processed.
                                SendErrorMail(ex.ToString());
                            }
                            finally
                            {
                                if (command.Connection != null)
                                {
                                    command.Connection.Close();
                                }
                            }
                        }
                        else
                        {
                            string errMsg = "This PNR didn't have any ticket information: " + b.SupplierBookingNo;
                            SendErrorMail(errMsg);
                            log.Error(errMsg);
                        }
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine(rs.Header.Error.TraceId);
                        sb.AppendLine(rs.Header.Error.ErrorMessage);

                        log.Error("Unable to retrive data for PNR:" + b.SupplierBookingNo.ToString() + "-" + b.SupplierBookingNo, new Exception(sb.ToString()));
                    }
                }

                log.Debug("Updated TicketChecking Id (" + updatedBookingIds.Count.ToString() + "):" + updatedBookingIds.Aggregate("", (x, y) => x + ", " + y));
                log.Debug("TicketChecking Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete TicketChecking.");
                throw ex;
            }
        }

        private static void BookingNoChecking()
        {
            try
            {
                log.Debug("BookingNoChecking Started.");

                List<string> updatedBookingIds = new List<string>();
                List<Booking> bookings = db.Bookings.Where(x => x.FlightSegments.FirstOrDefault().DepartureDateTime > System.DateTime.Now && x.BookingNo.Length <= 4 && x.SupplierCode == "SBRE" && statusRequireBookingNoChecking.Any(y => x.BookingStatusCode.Equals(y))).ToList();

                foreach (Booking b in bookings)
                {
                    Alphareds.Module.SabreWebService.SWS.ReadTravelItineraryResponse rs = SabreServiceCall.ReadTravelItineraryResponse(b.SupplierBookingNo);

                    if (rs.Output != null)
                    {

                        List<Module.SabreWebService.SWS.ReservationItem> reservedItems = rs.Output.PassengerNameRecord.ReservationItems.ToList();

                        if (reservedItems.Any(x => x.ReservationFlightSegment.Any()))
                        {
                            SqlCommand command = new SqlCommand();

                            try
                            {
                                //TO DO : compare here
                                var supplierRefId = reservedItems.FirstOrDefault().ReservationFlightSegment.FirstOrDefault().SupplierRefId;
                                if (supplierRefId.Contains('*'))
                                {
                                    var RefId = supplierRefId.Split('*');

                                    UpdateBookingNo(b, RefId[1], command);
                                    UpdateBookingFlightSegment(b.FlightSegments.ToList() , supplierRefId, command);

                                    command.Transaction.Commit();
                                    log.Debug("Booking No for: " + b.SuperPNRID.ToString() + "-" + b.SupplierBookingNo + " has been updated.");
                                }
                                else
                                {
                                    log.Error("No SupplierRefId returned for: " + b.SuperPNRID.ToString() + "-" + b.SupplierBookingNo);
                                }

                            }
                            catch (Exception ex)
                            {
                                if (command.Transaction != null)
                                {
                                    command.Transaction.Rollback();
                                }
                                SendErrorMail(ex.ToString());
                            }
                            finally
                            {
                                if (command.Connection != null)
                                {
                                    command.Connection.Close();
                                }
                            }
                        }
                        else
                        {
                            log.Error("ReservationItems or ReservationFlightSegment is null  for SuperPNR: " +
                            b.SuperPNRID.ToString() + " - " + b.SupplierBookingNo);
                        }
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine(rs.Header.Error.TraceId);
                        sb.AppendLine(rs.Header.Error.ErrorMessage);

                        log.Error("ReadTravelItineraryResponse is null for: " + b.SuperPNRID.ToString() + " - " + b.SupplierBookingNo , new Exception(sb.ToString()));

                        string errMsg = $"SuperPNR {b.SuperPNRID} - {b.SuperPNRNo}: " + rs.Header.Error.ErrorMessage;
                        SendErrorMail(errMsg);
                    }
                }
                log.Debug("BookingNoUpdate Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete BookingNoChecking.");
                throw ex;
            }
        }

        private static bool CheckPaymentStatus(Booking b, ref string ipay88transactionID, ref bool isAuthCap)
        {
            try
            {
                log.Debug("CheckPaymentStatus Started.");

                bool ShouldBook = false;
                string iPay88AppId = Core.GetAppSettingValueEnhanced("iPay88AppId");
                string url = Core.GetAppSettingValueEnhanced("iPay88REQUERYURL");
                string iPay88Testing = Core.GetAppSettingValueEnhanced("iPay88Testing");
                bool isTestingAcc = bool.TryParse(iPay88Testing, out isTestingAcc) ? isTestingAcc : isTestingAcc;

                PaymentOrder superPNRPayment = null;
                if (b.SuperPNR.SuperPNROrders != null && b.SuperPNR.SuperPNROrders.Count > 0)
                {
                    if (b.SuperPNR.SuperPNROrders.LastOrDefault().PaymentOrders != null && b.SuperPNR.SuperPNROrders.LastOrDefault().PaymentOrders.Count > 0)
                    {
                        superPNRPayment = b.SuperPNR.SuperPNROrders.LastOrDefault().PaymentOrders.FirstOrDefault(x => x.PaymentMethodCode != "SC");

                        if (superPNRPayment == null)
                        {
                            log.Debug(b.SuperPNRNo + " didn't have iPay payment transaction");
                            return false;
                        }
                    }
                    else
                    {
                        log.Debug(b.SuperPNRNo + " didn't exist super pnr payment order");
                        return false;
                    }
                }
                else
                {
                    log.Debug(b.SuperPNRNo + " didn't exist super pnr order");
                    return false;
                }

                String strPost = "AppId=" + iPay88AppId + "&RefNo=" + b.SuperPNRID + " - " + b.SuperPNRNo + "&Amount=" + (isTestingAcc ? "1.00" : (superPNRPayment.PaymentAmount).ToString("n2"));

                var myWebReq = new MyWebRequest(url, "POST", strPost);
                var response = myWebReq.GetResponse();

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(response);

                XmlNode node = doc.SelectSingleNode("Reply");

                string appId = node.SelectSingleNode("AppId").InnerText.ToString();
                string refNo = node.SelectSingleNode("RefNo").InnerText.ToString();
                string status = node.SelectSingleNode("Status").InnerText.ToString();
                string transId = node.SelectSingleNode("TransId").InnerText.ToString();
                string amount = node.SelectSingleNode("Amount").InnerText.ToString();
                string desc = node.SelectSingleNode("Desc").InnerText.ToString();
                ipay88transactionID = transId;
                isAuthCap = desc.ToLower() == ("authorised");

                if (status.Equals("1") || status.Equals("00"))
                {
                    //success
                    ShouldBook = true;
                    //b.BookingStatusCode = "CON";
                    //BookingServiceController.updateBookingAfterPayment(false, b, "PAID", transId, userid);
                }
                else if (status.Equals("Incorrect amount"))
                {
                    //send email
                    ShouldBook = false;
                    SendErrorMail(refNo + " payment status check shows incorrect amount, required human intervention to check." + status);
                }
                else if (status.Equals("0") || status.Equals("Payment fail") || status.Equals("Record not found"))
                {
                    //fail
                    ShouldBook = false;
                }
                else
                {
                    ShouldBook = false;
                    SendErrorMail(refNo + " payment status check shows status: " + status + ", required human intervention to check.");
                }

                log.Debug("CheckPaymentStatus Completed.");

                return ShouldBook;
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete CheckPaymentStatus.");
                throw ex;
            }
        }

        private static void RetryBooking()
        {
            try
            {
                MayFlower db = new MayFlower();
                log.Debug("RetryBooking started.");

                string strHostName = Dns.GetHostName();
                string ip = Dns.GetHostEntry(strHostName).AddressList.FirstOrDefault().ToString();

                DateTime dtWithExtra = DateTime.Now.AddMinutes(takeBookingPaymentAfterMinute * -1);
                //var superPNRs = db.SuperPNRs.Where(x => x.Bookings.Count > 0 && x.Bookings.Any(a => a.Temp_BookingInfo.Count > 0)
                //                && x.SuperPNROrders.Any(a => a.PaymentOrders.Any(b => b.PaymentStatusCode == "PEND"))
                //                && statusRequireRetryBooking.Any(y => x.Bookings.Any(a => a.BookingStatusCode == y))
                //                && x.Bookings.Any(a => a.CreateDateTime < dtWithExtra));

                var bookings = db.Bookings.Where(x => statusRequireRetryBooking.Any(y => x.BookingStatusCode == y)
                               && x.CreateDateTime < dtWithExtra);


                foreach (var bk in bookings)
                {
                    log.Debug("Retry single booking started - " + bk.SuperPNRID + " - " + bk.SuperPNRNo);

                    if (bk.Temp_BookingInfo != null && bk.Temp_BookingInfo.Count > 0)
                    {
                        string orderBookStatus = string.Empty;
                        string fltBookStatus = string.Empty;
                        string paymentStatus = string.Empty;
                        bool isSuccessBooking = false;
                        var orders = bk.SuperPNR.SuperPNROrders.Where(x => x.OrderID == bk.OrderID);
                        foreach (var order in orders)
                        {
                            if (bk.OrderID == order.OrderID)
                            {
                                List<PaymentQueueHandler.Model.RequeryStatus.Requery> paymentInfoList = new List<PaymentQueueHandler.Model.RequeryStatus.Requery>();

                                try
                                {
                                    foreach (var payment in order.PaymentOrders)
                                    {
                                        PaymentQueueHandler.Model.RequeryStatus.Requery paymentInfo = null;

                                        if (payment.PaymentMethodCode.ToUpper().StartsWith("IPA"))
                                        {
                                            paymentInfo = PaymentQueueHandler.Components.PaymentCheck.ServiceQuery.IPAY88.CheckPaymentPAID(payment, -1, bk.SuperPNRID, bk.SuperPNRNo);
                                        }
                                        else if (payment.PaymentMethodCode.ToUpper().StartsWith("ADY"))
                                        {
                                            paymentInfo = PaymentQueueHandler.Components.PaymentCheck.ServiceQuery.ADYEN.CheckPaymentPAID(payment, -1, bk.SuperPNRID, bk.SuperPNRNo);
                                        }
                                        else if (payment.PaymentMethodCode.ToUpper().StartsWith("SC"))
                                        {
                                            paymentInfo = new PaymentQueueHandler.Model.RequeryStatus.Requery { Status = true };
                                        }

                                        paymentInfoList.Add(paymentInfo);
                                    }


                                    if (paymentInfoList.All(x => x.Status) && paymentInfoList.Any(x => x.Desc != "Voided"))
                                    {
                                        string errMsg = null;
                                        ProductReserve.BookResultType bookResult = ProductReserve.BookResultType.AllFail;

                                        // TODO: Check current book is CON/RHI

                                        try
                                        {
                                            isSuccessBooking = HotelServiceController.PlaceBooking(bk, bk.UserID, ip, out errMsg, out bookResult);
                                        }
                                        catch (Exception ex)
                                        {
                                            log.Debug("SuperPNRNo - " + bk.SuperPNRNo + " - " + ex.Message);
                                            isSuccessBooking = false;
                                            SendErrorMail("SuperPNRNo - " + bk.SuperPNRNo + " IsSuccessBooking - " + isSuccessBooking + " - " + ex.Message);
                                        }

                                        if (bookResult == ProductReserve.BookResultType.AllSuccess)
                                        {
                                            fltBookStatus = "CON";

                                            //Place Queue Booking if is Sabre Booking
                                            if (bk.SupplierCode == "SBRE" && ConfigurationManager.AppSettings["IsSabreLive"].ToString().Equals("true"))
                                            {
                                                string pricingTicketing = bk.QueuePCC ?? string.Empty;
                                                string queueNo = bk.QueueNumber.ToString() ?? string.Empty;
                                                string ticketingQueueNo = bk.TicketingQueueNo.ToString() ?? string.Empty;

                                                bool isSuccessQueue = HotelServiceController.PlaceQueueToSabre(bk.SupplierBookingNo, pricingTicketing, queueNo);
                                                bool isSecondSuccessQueue = HotelServiceController.PlaceQueueToSabre(bk.SupplierBookingNo, pricingTicketing, ticketingQueueNo);

                                                if (isSuccessQueue && isSecondSuccessQueue)
                                                {
                                                    fltBookStatus = "QPL";
                                                }
                                            }
                                        }
                                        else if (bookResult == ProductReserve.BookResultType.PartialSuccess)
                                        {
                                            fltBookStatus = "RHI";
                                            SendRHIMail(bk.SuperPNRNo, bk.SuperPNRID, errMsg);
                                        }
                                        else if (bookResult == ProductReserve.BookResultType.AllFail)
                                        {
                                            // If send book, API result failed.
                                        }

                                        orderBookStatus = "RHI";
                                    }
                                    else
                                    {
                                        fltBookStatus = "EXP";
                                        orderBookStatus = "EXP";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    log.Debug("SuperPNRNo - " + bk.SuperPNRNo + " - " + ex.ToString() + " - " + ex.GetInnerExceptionMsg());
                                    SendErrorMail("SuperPNRNo - " + bk.SuperPNRNo + " - " + ex.ToString() + " - " + ex.GetInnerExceptionMsg());

                                    //fltBookStatus = bk.BookingStatusCode;
                                    //orderBookStatus = order.BookingStatusCode;

                                    //fltBookStatus = "RHI";
                                    orderBookStatus = "RHI";
                                    SendRHIMail(bk.SuperPNRNo, bk.SuperPNRID, ex.ToString());
                                }

                                bk.BookingStatusCode = string.IsNullOrWhiteSpace(fltBookStatus) ? bk.BookingStatusCode : fltBookStatus;
                                order.BookingStatusCode = orderBookStatus;
                            }
                        }
                    }
                    else
                    {
                        log.Debug(bk.SuperPNRID + " - " + bk.SuperPNRNo + " - Didn't have temp_BookingInfo.");
                        SendErrorMail(bk.SuperPNRID + " - " + bk.SuperPNRNo + " - Didn't have temp_BookingInfo.");
                    }

                    log.Debug("Retry single booking finished - " + bk.SuperPNRID + " - " + bk.SuperPNRNo);
                }

                db.SaveChanges();
                log.Debug("RetryBooking finished");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ee)
            {
                //var changedInfo = db.ChangeTracker.Entries()
                //    .Where(t => t.State == System.Data.Entity.EntityState.Modified)
                //    .Select(t => new
                //    {
                //        Original = t.OriginalValues.PropertyNames.ToDictionary(pn => pn, pn => t.OriginalValues[pn]),
                //        Current = t.CurrentValues.PropertyNames.ToDictionary(pn => pn, pn => t.CurrentValues[pn]),
                //    });

                List<string> entityMsg = new List<string>();

                foreach (DbEntityValidationResult validationResult in ee.EntityValidationErrors)
                {
                    string entityName = validationResult.Entry.Entity.GetType().Name;
                    foreach (DbValidationError error in validationResult.ValidationErrors)
                    {
                        entityMsg.Add(entityName + "." + error.PropertyName + ": " + error.ErrorMessage);
                    }
                }

                log.Debug("[EntityException] Error on db save final changes."
                + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, entityMsg)
                //+ Environment.NewLine + Environment.NewLine + "Error while attemp to save db change on finally action." +
                //    Environment.NewLine + Environment.NewLine +
                //    JsonConvert.SerializeObject(changedInfo, Newtonsoft.Json.Formatting.Indented,
                //    new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, NullValueHandling = NullValueHandling.Ignore })
                );
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete RetryBooking");
                throw ex;
            }
        }

        private static void UpdateSuperPNRAllStatus(SuperPNR superPNR, string bookStatus, string paymentStatus)
        {
            UpdateSuperPNROrders(superPNR, bookStatus, paymentStatus);
            UpdateBookingFlights(superPNR, bookStatus);
        }

        private static void UpdateSuperPNROrders(SuperPNR superPNR, string bookStatus, string paymentStatus)
        {
            foreach (var item in superPNR.SuperPNROrders)
            {
                item.BookingStatusCode = bookStatus;

                foreach (var record in item.PaymentOrders)
                {
                    record.PaymentStatusCode = paymentStatus;
                }
            }
        }

        private static void UpdateBookingFlights(SuperPNR superPNR, string status)
        {
            foreach (var item in superPNR.Bookings)
            {
                item.BookingStatusCode = status;
            }
        }
        #endregion

        #region Events

        static void Main(string[] args)
        {
            try
            {
                //bool ShouldCancel = false;
                //string iPay88AppId = "2";
                //string url = "https://pay.mayflower.com.my/iPay88_requerypaymentstatus.ashx";
                //string iPay88Testing = "false";
                //bool isTestingAcc = bool.TryParse(iPay88Testing, out isTestingAcc) ? isTestingAcc : isTestingAcc;

                //String strPost = "AppId=" + iPay88AppId + "&RefNo=242 - UIKYZT&Amount=" + (isTestingAcc ? "1.00" : (773.32).ToString("n2"));

                //var myWebReq = new MyWebRequest(url, "POST", strPost);
                //var response = myWebReq.GetResponse();

                //XmlDocument doc = new XmlDocument();
                //doc.LoadXml(response);


                log.Debug("Booking Status Handler Started.");

                if (args.Length != 1)
                {
                    throw new Exception("Event Handler must received an argument which is execution type.");
                }

                executionType = (ExecutionType)Enum.Parse(typeof(ExecutionType), args[0].ToString(), true);
                log.Debug("Execution Type: " + executionType.ToString());

                //dbADO.SetConnection(ConfigurationManager.ConnectionStrings["CorpBookingADO"].ConnectionString);

                //Code for Testing
                //Booking b = db.Bookings.Find(1);
                //BookingDatabaseHandler.UpdateBooking(b);
                //SendBookingStatusUpdateEmail("559");

                //Disabled because didn't suitable to use
                if (executionType == ExecutionType.ExpiredStatus)
                {
                    //check PNR that has flight segment that has been expired/cancel by airline and update status to EXP (expired)
                    CheckAirlineCancellation();
                    //cancel PNR that are unable to proceed further as dated 15/9/2016, PNR Rejected by Approver is unable to continue, so we should cancel the PNR.
                    //CheckCancellation();
                }

                if (executionType == ExecutionType.TicketingStatus)
                {
                    //check if PNR has been ticketed.
                    TicketChecking();
                }

                if (executionType == ExecutionType.PaymentStatus)
                {
                    RetryBooking();
                }

                if (executionType == ExecutionType.BookingNoStatus)
                {
                    BookingNoChecking();
                }


                //if (executionType == ExecutionType.Test)
                //{
                //    TestRetryBooking();
                //}

                log.Debug("Booking Status Handler Process Completed.");
            }
            catch (Exception ex)
            {
                SendErrorMail(ex.ToString() + ex.GetInnerExceptionMsg());
                log.Error("Unable to complete the Booking Status Handler process.", ex);
            }
        }
        #endregion
    }
}
