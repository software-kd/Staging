using Alphareds.Library.DatabaseHandler;
using Alphareds.Module.Common;
using Alphareds.Module.CommonController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Alphareds.Module.ServiceCall;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//Here is the once-per-application setup information
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace SabreFlightSearchHandler
{
    class Program
    {
        #region Member variables Declarations

        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog emaillog = log4net.LogManager.GetLogger("EmailLogger");
        private static ExecutionType executionType;
        private static MayFlower db = new MayFlower();
        private static DatabaseHandlerMain dbADO = new DatabaseHandlerMain();

        #endregion

        #region Constructors & Finalizers



        #endregion

        #region Properties

        public enum ExecutionType
        {
            FlightSearch,
            GenerateReport
        }

        #endregion

        #region Methods
        private static DateTime? NextMonth(DateTime? dateN)
        {
            DateTime date = dateN ?? DateTime.Today;

            if (date.Day != DateTime.DaysInMonth(date.Year, date.Month))
                return date.AddMonths(1);
            else
                return date.AddDays(1).AddMonths(1).AddDays(-1);
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

        private static void ProcessFlightConditions(FlightSearchCondition f)
        {
            try
            {
                log.Debug("ProcessFlightConditions Started.");

                DateTime fromPeriod = f.SeachPeriodFrom ?? DateTime.Today;
                DateTime toPeriod = f.SeachPeriodTo ?? DateTime.Today;
                int stayduration = f.SeachStayDuration ?? 0;
                int totalAdults = f.TotalAdult ?? 1;
                int totalChild = f.TotalChild ?? 0;
                int totalInfants = f.TotalInfant ?? 0;

                #region Validation
                if (f.SeachPeriodFrom == null || f.SeachPeriodTo == null || f.FROM == null || f.TO == null)
                {
                    log.Debug("Fligh Search Condition not complete. ConditionID = " + f.ConditionID);
                    SendErrorMail("Fligh Search Condition not complete. ConditionID = " + f.ConditionID);
                }

                if ((toPeriod - fromPeriod).TotalDays < 0 || (fromPeriod - DateTime.Now).TotalDays > 365 || DateTime.Now > fromPeriod)
                {
                    log.Debug("Fligh Search Condition Search Period not correct. ConditionID = " + f.ConditionID);
                    SendErrorMail("Fligh Search Condition Search Period not correct. ConditionID = " + f.ConditionID);
                }
                #endregion

                if ((fromPeriod - DateTime.Now).TotalDays < 365 && DateTime.Now < fromPeriod)
                {
                    SqlCommand command = new SqlCommand();

                    try
                    {
                        InsertFlightSearchBatchService(command, f);
                        UpdateFlightSearchConditionService(command, f);

                        command.Transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        command.Transaction.Rollback();
                        throw ex;
                    }
                }

                log.Debug("ProcessFlightConditions Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete ProcessFlightConditions.");
                throw ex;
            }
        }

        private static void InsertFlightSearchBatchService(SqlCommand command, FlightSearchCondition f)
        {
            try
            {
                if (command.Connection == null)
                {
                    command = dbADO.OpenConnection(command);
                }

                FlightSearchBatch batch = new FlightSearchBatch()
                {
                    ConditionID = f.ConditionID,
                    FROM = f.FROM,
                    TO = f.TO,
                    SeachStayDuration = f.SeachStayDuration,
                    SeachPeriodFrom = f.SeachPeriodFrom,
                    SeachPeriodTo = f.SeachPeriodTo,
                    PreferredAirlineCode = f.PreferredAirlineCode,
                    TotalAdult = f.TotalAdult,
                    TotalChild = f.TotalChild,
                    TotalInfant = f.TotalInfant,
                    CabinClass = f.CabinClass,
                    IsGetCorporateFare = f.IsGetCorporateFare,
                    IsActive = true
                };
                InsertFlightSearchBatch(dbADO, batch, command);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void InsertFlightSearchBatch(DatabaseHandlerMain db, FlightSearchBatch f, SqlCommand command)
        {
            try
            {
                var searchBatchInsertQuery = "Exec [Report].[usp_FlightSearchBatchInsert]  @ConditionID,@FROM, @TO,@SeachStayDuration,@SeachPeriodFrom,@SeachPeriodTo,@PreferredAirlineCode,";
                searchBatchInsertQuery += "@TotalAdult,@TotalChild,@TotalInfant,@CabinClass,@IsGetCorporateFare,@IsActive";

                command.CommandText = searchBatchInsertQuery;

                command.Parameters.Clear();

                command.Parameters.Add(new SqlParameter("ConditionID", f.ConditionID ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("FROM", f.FROM ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TO", f.TO ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachStayDuration", f.SeachStayDuration ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachPeriodFrom", f.SeachPeriodFrom ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachPeriodTo", f.SeachPeriodTo ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("PreferredAirlineCode", f.PreferredAirlineCode ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalAdult", f.TotalAdult ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalChild", f.TotalChild ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalInfant", f.TotalInfant ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("CabinClass", f.CabinClass ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("IsGetCorporateFare", f.IsGetCorporateFare ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("IsActive", f.IsActive));

                db.UpdateDataByStoredProcedure(command);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void UpdateFlightSearchConditionService(SqlCommand command, FlightSearchCondition f)
        {
            try
            {
                if (command.Connection == null)
                {
                    command = dbADO.OpenConnection(command);
                }

                f.SeachPeriodFrom = NextMonth(f.SeachPeriodFrom) ?? DateTime.Today;
                f.SeachPeriodTo = NextMonth(f.SeachPeriodTo) ?? DateTime.Today;
                f.NextRunDate = NextMonth(f.NextRunDate) ?? DateTime.Today;
                UpdateFlightSearchCondition(dbADO, f, command);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        private static void UpdateFlightSearchCondition(DatabaseHandlerMain db, FlightSearchCondition f, SqlCommand command)
        {
            try
            {
                var searchConditionUpdateQuery = "Exec [Report].[usp_FlightSearchConditionUpdate] @ConditionID ,@FROM, @TO";
                searchConditionUpdateQuery += ",@SeachStayDuration, @SeachPeriodFrom ,@SeachPeriodTo ,@PreferredAirlineCode ,@TotalAdult";
                searchConditionUpdateQuery += ",@TotalChild ,@TotalInfant ,@CabinClass ,@IsGetCorporateFare,@NextRunDate ,@IsActive";

                command.CommandText = searchConditionUpdateQuery;

                command.Parameters.Clear();

                command.Parameters.Add(new SqlParameter("ConditionID", f.ConditionID));
                command.Parameters.Add(new SqlParameter("FROM", f.FROM ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TO", f.TO ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachStayDuration", f.SeachStayDuration ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachPeriodFrom", f.SeachPeriodFrom ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachPeriodTo", f.SeachPeriodTo ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("PreferredAirlineCode", f.PreferredAirlineCode ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalAdult", f.TotalAdult ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalChild", f.TotalChild ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalInfant", f.TotalInfant ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("CabinClass", f.CabinClass ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("IsGetCorporateFare", f.IsGetCorporateFare ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("NextRunDate", f.NextRunDate));
                command.Parameters.Add(new SqlParameter("IsActive", f.IsActive));

                db.UpdateDataByStoredProcedure(command);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        private static void SabreFlightSearch(FlightSearchBatch f)
        {
            try
            {
                log.Debug("SabreFlightSearch Started.");

                DateTime fromPeriod = f.SeachPeriodFrom ?? DateTime.Today;
                DateTime toPeriod = f.SeachPeriodTo ?? DateTime.Today;
                int stayduration = f.SeachStayDuration ?? 0;
                int totalAdults = f.TotalAdult ?? 1;
                int totalChild = f.TotalChild ?? 0;
                int totalInfants = f.TotalInfant ?? 0;

                #region Validation
                if (f.SeachPeriodFrom == null || f.SeachPeriodTo == null || f.FROM == null || f.TO == null)
                {
                    log.Debug("Fligh Search Batch not complete. BatchID = " + f.BatchID);
                    SendErrorMail("Fligh Search Batch not complete. BatchID = " + f.BatchID);
                }

                if ((toPeriod - fromPeriod).TotalDays < 0 || (fromPeriod - DateTime.Now).TotalDays > 365 || DateTime.Now > fromPeriod)
                {
                    log.Debug("Fligh Search Batch Seach Period not correct. BatchID = " + f.BatchID);
                    SendErrorMail("Fligh Search Batch Seach Period not correct. BatchID = " + f.BatchID);
                }
                #endregion

                if (//(DateTime.Now - lastSeachDate).TotalDays > rerunday && 
                    (fromPeriod - DateTime.Now).TotalDays < 365 && DateTime.Now < fromPeriod)
                {
                    while (fromPeriod <= toPeriod)
                    {
                        FlightBookingModel model = new FlightBookingModel()
                        {
                            SearchFlightResultViewModel = new SearchFlightResultViewModel()
                            {

                                DepartureStation = f.FROM,
                                ArrivalStation = f.TO,
                                BeginDate = fromPeriod,
                                EndDate = fromPeriod.AddDays(stayduration),
                                CabinClass = string.IsNullOrWhiteSpace(f.CabinClass) ? "Y" : f.CabinClass,
                                PrefferedAirlineCode = string.IsNullOrWhiteSpace(f.PreferredAirlineCode) ? "-" : f.PreferredAirlineCode,
                                TripType = "Return",
                                DirectFlight = false,
                                Adults = totalAdults,
                                Childrens = totalChild,
                                Infants = totalInfants
                            }
                        };
                        Alphareds.Module.SabreWebService.SWS.SearchFlightBargainFinderMaxResponse rs = SabreServiceCall.SearchFlightBargainFinderMaxResponse(model);
                        if (rs.Output == null)
                        {

                        }
                        else
                        {
                            SqlCommand command = new SqlCommand();

                            try
                            {
                                //model.FlightSearchResultViewModel.FullFlightSearchResult = rs.Output.ToList();
                                List<Alphareds.Module.SabreWebService.SWS.PricedItineryModel> result = rs.Output.ToList();
                                foreach (Alphareds.Module.SabreWebService.SWS.PricedItineryModel resultItem in result)
                                {
                                    InsertFlightSearchResultService(command, resultItem, f.BatchID, fromPeriod, fromPeriod.AddDays(stayduration));
                                }

                                command.Transaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                command.Transaction.Rollback();
                                throw ex;
                            }
                        }

                        fromPeriod = fromPeriod.AddDays(1);

                        int pauseDuration = 5000;
                        int.TryParse(Core.GetAppSettingValueEnhanced("PauseDuration").ToString(), out pauseDuration);
                        Thread.Sleep(pauseDuration);
                    }
                }

                UpdateFlightSearchBatchService(f);
                GenerateReport(f);
                log.Debug("SabreFlightSearch Completed.");
            }
            catch (Exception ex)
            {
                log.Debug("Unable to complete SabreFlightSearch.");
                throw ex;
            }
        }

        private static void InsertFlightSearchResultService(SqlCommand command, Alphareds.Module.SabreWebService.SWS.PricedItineryModel pricedItineraryModel, int batchID, DateTime start, DateTime end)
        {

            try
            {
                if (command.Connection == null)
                {
                    command = dbADO.OpenConnection(command);
                }

                List<string> RBD = new List<string>();
                foreach (var odo in pricedItineraryModel.OriginDestinationOptions)
                {
                    foreach (var fs in odo.FlightSegments)
                    {
                        RBD.Add(fs.ResBookDesigCode);
                    }
                }

                FlightSearchResult result = new FlightSearchResult()
                {
                    BatchID = batchID,
                    AirlineCode = pricedItineraryModel.PricingInfo.ValidatingCarrier,
                    TotalPrice = pricedItineraryModel.PricingInfo.TotalAfterTax,
                    OutboundDepartureDate = start,
                    InboundDepartureDate = end,
                    ResBookDesignCode = RBD.Aggregate((x, y) => x + "/" + y)//.Distinct().OrderBy(x => x)
                };
                InsertFlightSearchResult(dbADO, result, command);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void InsertFlightSearchResult(DatabaseHandlerMain db, FlightSearchResult searchresult, SqlCommand command)
        {
            try
            {
                var searchResultInsertQuery = "Exec [Report].[usp_FlightSearchResultInsert]  @BatchID,@OutboundDepartureDate,@InboundDepartureDate,@AirlineCode,@TotalPrice,@ResBookDesignCode";

                command.CommandText = searchResultInsertQuery;

                command.Parameters.Clear();

                command.Parameters.Add(new SqlParameter("BatchID", searchresult.BatchID ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("OutboundDepartureDate", searchresult.OutboundDepartureDate ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("InboundDepartureDate", searchresult.InboundDepartureDate ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("AirlineCode", searchresult.AirlineCode ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalPrice", searchresult.TotalPrice ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("ResBookDesignCode", searchresult.ResBookDesignCode ?? (object)DBNull.Value));

                db.UpdateDataByStoredProcedure(command);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void UpdateFlightSearchBatchService(FlightSearchBatch f)
        {
            SqlCommand command = new SqlCommand();

            try
            {
                if (command.Connection == null)
                {
                    command = dbADO.OpenConnection(command);
                }

                f.IsActive = false;
                UpdateFlightSearchBatch(dbADO, f, command);

                command.Transaction.Commit();
            }
            catch (Exception ex)
            {
                command.Transaction.Rollback();
                throw ex;
            }
        }

        private static void UpdateFlightSearchBatch(DatabaseHandlerMain db, FlightSearchBatch f, SqlCommand command)
        {
            try
            {
                var searchConditionUpdateQuery = "Exec [Report].[usp_FlightSearchBatchUpdate] @BatchID ,@FROM, @TO";
                searchConditionUpdateQuery += ",@SeachStayDuration, @SeachPeriodFrom ,@SeachPeriodTo ,@PreferredAirlineCode ,@TotalAdult";
                searchConditionUpdateQuery += ",@TotalChild ,@TotalInfant ,@CabinClass ,@IsGetCorporateFare ,@IsActive";

                command.CommandText = searchConditionUpdateQuery;

                command.Parameters.Clear();

                command.Parameters.Add(new SqlParameter("BatchID", f.BatchID));
                command.Parameters.Add(new SqlParameter("FROM", f.FROM ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TO", f.TO ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachStayDuration", f.SeachStayDuration ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachPeriodFrom", f.SeachPeriodFrom ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("SeachPeriodTo", f.SeachPeriodTo ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("PreferredAirlineCode", f.PreferredAirlineCode ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalAdult", f.TotalAdult ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalChild", f.TotalChild ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("TotalInfant", f.TotalInfant ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("CabinClass", f.CabinClass ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("IsGetCorporateFare", f.IsGetCorporateFare ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("IsActive", f.IsActive));

                db.UpdateDataByStoredProcedure(command);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static void GenerateReport(FlightSearchBatch batch)
        {
            try
            {
                log.Debug("Generate Report Started.");

                List<FlightSearchResult> results = db.FlightSearchResults.Where(x => x.BatchID == batch.BatchID).ToList();

                List<FlightSearchResult> searchPeriod = results.OrderBy(x => x.OutboundDepartureDate).GroupBy(x => x.OutboundDepartureDate).Select(x => x.First()).ToList();
                List<CheapeastModel> cheapestGroup = results
                            .GroupBy(x => new
                            {
                                x.AirlineCode,
                                x.OutboundDepartureDate,
                                x.ResBookDesignCode
                            })
                            .Select(y => new CheapeastModel
                            {
                                OutboundDepartureDate = y.Key.OutboundDepartureDate,
                                AirlineCode = y.Key.AirlineCode + " (" + y.Key.ResBookDesignCode + ")",
                                TotalPrice = y.Min(z => z.TotalPrice)
                            })
                            .ToList();

                //export into excell file
                //string FileName = @"F:\AlpharedsTFS\Mayflower\FrontEnd\ApplicationSource\BatchPrograms\SabreFlightSearchHandler\bin\Debug\" + System.DateTime.Now.ToString("yyyyMMddHHmm") + ".xlsx";
                //FileInfo workbook = new FileInfo(FileName);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Result ");

                    int counter = 1;
                    for (int col = 1; col < searchPeriod.Count() * 2 + 1; col += 2)
                    {
                        worksheet.SetValue(1, col + 1, searchPeriod[col - counter].OutboundDepartureDate.Value.ToString("dd MMM yyyy"));
                        worksheet.Cells[1, col + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[1, col + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[1, col + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkGray);
                        worksheet.Cells[1, col + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                        worksheet.Cells[1, col + 1].Style.Font.Bold = true;

                        worksheet.Cells[1, col + 1].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[1, col + 1].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[1, col + 1].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[1, col + 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells.AutoFitColumns();

                        worksheet.Cells[1, col + 1, 1, col + 2].Merge = true;


                        worksheet.SetValue(2, col + 1, "Airline");
                        worksheet.Cells[2, col + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[2, col + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[2, col + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkGray);
                        worksheet.Cells[2, col + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                        worksheet.Cells[2, col + 1].Style.Font.Bold = true;

                        worksheet.Cells[2, col + 1].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[2, col + 1].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[2, col + 1].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[2, col + 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells.AutoFitColumns();

                        worksheet.SetValue(2, col + 2, "Total Price");
                        worksheet.Cells[2, col + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[2, col + 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[2, col + 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkGray);
                        worksheet.Cells[2, col + 2].Style.Font.Color.SetColor(System.Drawing.Color.White);
                        worksheet.Cells[2, col + 2].Style.Font.Bold = true;

                        worksheet.Cells[2, col + 2].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[2, col + 2].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[2, col + 2].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[2, col + 2].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells.AutoFitColumns();

                        List<CheapeastModel> cheapestGroupForTheDay = cheapestGroup.Where(x => x.OutboundDepartureDate == searchPeriod[col - counter].OutboundDepartureDate).OrderBy(x => x.TotalPrice).ToList();
                        for (int row = 3; row < cheapestGroupForTheDay.Count() + 3; row++)
                        {
                            if (worksheet.Cells[row, 1].Value == null)
                            {
                                worksheet.SetValue(row, 1, (row - 2).ToString());
                                worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkGray);
                                worksheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                                worksheet.Cells[row, 1].Style.Font.Bold = true;
                            }

                            worksheet.SetValue(row, col + 1, cheapestGroupForTheDay[row - 3].AirlineCode);
                            worksheet.Cells[row, col + 1].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                            worksheet.Cells[row, col + 1].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                            worksheet.Cells[row, col + 1].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                            worksheet.Cells[row, col + 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

                            worksheet.SetValue(row, col + 2, cheapestGroupForTheDay[row - 3].TotalPrice);
                            worksheet.Cells[row, col + 2].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                            worksheet.Cells[row, col + 2].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                            worksheet.Cells[row, col + 2].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                            worksheet.Cells.AutoFitColumns();
                        }

                        counter++;
                    }


                    List<FlightSearchBatch> list = new List<FlightSearchBatch>();
                    list.Add(batch);
                    var worksheet2 = package.Workbook.Worksheets.Add("Condition");
                    worksheet2.SetValue(1, 1, "BatchID");
                    worksheet2.SetValue(1, 2, "From");
                    worksheet2.SetValue(1, 3, "To");
                    worksheet2.SetValue(1, 4, "SeachStayDuration");
                    worksheet2.SetValue(1, 5, "SearchPeriodFrom");
                    worksheet2.SetValue(1, 6, "SearchPeriodTo");
                    worksheet2.SetValue(1, 7, "PreferredAirlineCode");
                    worksheet2.SetValue(1, 8, "TotalAdult");
                    worksheet2.SetValue(1, 9, "TotalChild");
                    worksheet2.SetValue(1, 10, "TotalInfant");
                    worksheet2.SetValue(1, 11, "CabinClass");

                    worksheet2.SetValue(2, 1, batch.BatchID);
                    worksheet2.SetValue(2, 2, batch.FROM);
                    worksheet2.SetValue(2, 3, batch.TO);
                    worksheet2.SetValue(2, 4, batch.SeachStayDuration);
                    worksheet2.SetValue(2, 5, batch.SeachPeriodFrom.Value.ToString("dd MMM yyyy"));
                    worksheet2.SetValue(2, 6, batch.SeachPeriodTo.Value.ToString("dd MMM yyyy"));
                    worksheet2.SetValue(2, 7, batch.PreferredAirlineCode);
                    worksheet2.SetValue(2, 8, batch.TotalAdult);
                    worksheet2.SetValue(2, 9, batch.TotalChild);
                    worksheet2.SetValue(2, 10, batch.TotalInfant);
                    worksheet2.SetValue(2, 11, batch.CabinClass);
                    worksheet2.Cells.AutoFitColumns();

                    //package.Save();
                    var stream = new MemoryStream(package.GetAsByteArray());

                    List<Attachment> attachments = new List<Attachment>();
                    attachments.Add(new Attachment(stream, batch.BatchID.ToString() + "_" + System.DateTime.Now.ToString("yyyyMMddHHmm") + ".xlsx"));
                    CommonServiceController.SendEmail(Core.GetAppSettingValueEnhanced("ReportingEmail").ToString(), Core.GetAppSettingValueEnhanced("ReportingEmailCC").ToString(), Core.GetAppSettingValueEnhanced("ReportingEmailBCC").ToString(), batch.BatchID.ToString() + " - Sabre Flight Search Result (" + batch.FROM + batch.TO + "_" + batch.SeachPeriodFrom.Value.ToString("ddMMMyyyy") + "_" + batch.SeachPeriodTo.Value.ToString("ddMMMyyyy"),
                        "Please find the attached. <br/>" +
                    "BatchID:" + batch.BatchID + "<br/>" +
                    "From:" + batch.FROM + "<br/>" +
                    "To:" + batch.TO + "<br/>" +
                    "SeachStayDuration:" + batch.SeachStayDuration + "<br/>" +
                    "SearchPeriodFrom:" + batch.SeachPeriodFrom.Value.ToString("dd MMM yyyy") + "<br/>" +
                    "SearchPeriodTo:" + batch.SeachPeriodTo.Value.ToString("dd MMM yyyy") + "<br/>" +
                    "PreferredAirlineCode:" + batch.PreferredAirlineCode + "<br/>" +
                    "TotalAdult:" + batch.TotalAdult + "<br/>" +
                    "TotalChild:" + batch.TotalChild + "<br/>" +
                    "TotalInfant:" + batch.TotalInfant + "<br/>" +
                    "CabinClass:" + batch.CabinClass + "<br/>"
                    , attachments);
                }

                log.Debug("Generate Report Completed.");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion

        #region Events

        static void Main(string[] args)
        {
            try
            {
                log.Debug("Sabre Flight Search Handler Started.");

                if (args.Length < 1)
                {
                    throw new Exception("Event Handler must received an argument which is execution type.");
                }

                executionType = (ExecutionType)Enum.Parse(typeof(ExecutionType), args[0].ToString(), true);
                log.Debug("Execution Type: " + executionType.ToString());

                if (executionType == ExecutionType.FlightSearch)
                {
                    List<FlightSearchCondition> flightSearchConditions = db.FlightSearchConditions.Where(x => x.IsActive && DateTime.Now > x.NextRunDate).ToList();
                    foreach (FlightSearchCondition f in flightSearchConditions)
                    {
                        ProcessFlightConditions(f);
                    }

                    int batchPerRun = 10;
                    int.TryParse(Core.GetAppSettingValueEnhanced("BatchPerRun").ToString(), out batchPerRun);

                    List<FlightSearchBatch> flightSearchBatchs = db.FlightSearchBatches.Where(x => x.IsActive).Take(batchPerRun).ToList();
                    foreach (FlightSearchBatch f in flightSearchBatchs)
                    {
                        SabreFlightSearch(f);
                    }
                }
                else if (executionType == ExecutionType.GenerateReport)
                {
                    if (args.Length != 2)
                    {
                        log.Debug("GenerateReport execution type must have 2nd parameter which is Batch ID");
                    }
                    else
                    {
                        int batchid = 0;
                        int.TryParse(args[1], out batchid);

                        FlightSearchBatch batch = db.FlightSearchBatches.Where(x => x.BatchID == batchid).FirstOrDefault();
                        GenerateReport(batch);
                    }
                }

                log.Debug("Sabre Flight Search Handler Process Completed.");
            }
            catch (Exception ex)
            {
                SendErrorMail(ex.ToString());
                log.Error("Unable to complete the Sabre Flight Search Handler process.", ex);
            }
        }


        #endregion
    }

    public class CheapeastModel
    {
        public DateTime? OutboundDepartureDate { get; set; }
        public string AirlineCode { get; set; }
        public decimal? TotalPrice { get; set; }

    }

}
