using System;
using System.Linq;
using System.Data.SqlClient;
using System.Data;
using System.Data.OleDb;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Collections.ObjectModel;
using Alphareds.Module.Common;

namespace Alphareds.Library.DatabaseHandler
{
    public class DatabaseHandlerMain
    {
        #region Member variables Declarations
        private SqlConnection _con = null;
        private SqlDataReader _rdr = null;
        private SqlCommand _cmd = null;

        private static OleDbConnection _objCon = null;
        private static string _excelPath = string.Empty;

        //private string _connectionString = Core.GetSettingValue("connectionstring");
        private string _connectionString = string.Empty;
        private string _clientName = string.Empty;
        private string _server = string.Empty;
        private string _database = string.Empty;
        private string _userId = string.Empty;
        private string _password = string.Empty;

        private int _timeOut = 180;

        private string _commandSql = string.Empty;
        #endregion

        #region Constructors & Finalizers
        public DatabaseHandlerMain()
        {

        }
        #endregion

        #region Properties
        public static OleDbConnection ObjCon
        {
            get { return _objCon; }
            set { _objCon = value; }
        }
        public static string ExcelPath
        {
            get { return _excelPath; }
            set { _excelPath = value; }
        }
        public string ConnectionString
        {
            get { return _connectionString; }
            set { _connectionString = value; }
        }
        public string ClientName
        {
            get { return _clientName; }
            set { _clientName = value; }
        }
        public string Server
        {
            get { return _server; }
            set { _server = value; }
        }
        public string Database
        {
            get { return _database; }
            set { _database = value; }
        }
        public string UserId
        {
            get { return _userId; }
            set { _userId = value; }
        }
        public string Password
        {
            get { return _password; }
            set { _password = value; }
        }
        public string CommandSql
        {
            get { return _commandSql; }

            set { _commandSql = value; }
        }
        #endregion

        #region Methods
        //public void SetConnection()
        //{
        //    _connectionString = "server=" + _server + "; uid=" + _userId + "; pwd=" + _password + "; database=" + _database + "; Connection Timeout=" + _timeOut;
        //    _con = new SqlConnection(_connectionString);
        //}


        /// <summary>
        /// Open Connection and Begin Transaction
        /// </summary>
        /// <param name="command">Sql command</param>
        /// <returns>Sql command with openned connection and transaction</returns>
        public SqlCommand OpenConnection(SqlCommand command)
        {
            SetConnection(Core.GetSettingValue("connectionstring"));
            _con.Open();
            command.Connection = _con;
            SqlTransaction transaction = _con.BeginTransaction("dbTransaction");
            command.Transaction = transaction;
            command.CommandTimeout = 1000;

            return command;
        }

        public void SetConnection()
        {
            _connectionString = "server=" + _server + "; uid=" + _userId + "; pwd=" + _password + "; database=" + _database + "; Connection Timeout=" + _timeOut;
            _con = new SqlConnection(_connectionString);
        }

        public void SetConnection(string connectionString)
        {
            _con = new SqlConnection(connectionString);
        }

        public void SetExcelConnection(string excelPath)
        {
            ExcelPath = excelPath;
            _connectionString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + excelPath + ";Mode=ReadWrite;Extended Properties=\"Excel 12.0;HDR=Yes;\";";
            _objCon = new OleDbConnection(_connectionString);
        }

        public DataSet GetData(string sql)
        {
            try
            {
                _rdr = null;
                SqlDataAdapter adp = null;
                DataSet results = new DataSet();
                _commandSql = sql;

                _con.Open();

                adp = new SqlDataAdapter(_commandSql, _con);
                adp.Fill(results);

                _con.Close();

                return results;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _con.Close();
            }
        }

        public DataSet GetDataByStoredProcedure(SqlCommand command)
        {
            try
            {
                _rdr = null;
                DataSet results = new DataSet();

                //_con.Open();
                //command.Connection = _con;

                _rdr = command.ExecuteReader();

                results.Tables.Add(new DataTable());
                results.Tables[0].Load(_rdr);

                //_con.Close();
                return results;
            }
            catch (Exception)
            {
                throw;
            }
            //finally
            //{
            //    _con.Close();
            //}
        }

        public bool UpdateData(string sql)
        {
            try
            {
                _con.Open();

                CommandSql = sql;
                _cmd = new SqlCommand(CommandSql);
                _cmd.Connection = _con;

                int result = _cmd.ExecuteNonQuery();

                _con.Close();

                if (result > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _con.Close();
            }
        }

        public DataSet UpdateDataByStoredProcedure(SqlCommand command)
        {
            DataSet results = new DataSet();

            try
            {
                //int numOfRow = command.ExecuteNonQuery();

                //if (numOfRow > 0)
                //{
                //    return true;
                //}
                //else
                //{
                //    return false;
                //}

                _rdr = command.ExecuteReader();

                results.Tables.Add(new DataTable());

                results.Tables[0].Load(_rdr, LoadOption.OverwriteChanges, FillErrorHandler);

                if (results.Tables[0].Rows.Count > 0)
                {
                    if (results.Tables[0].Columns.Count >= 5)
                        throw new Exception(results.Tables[0].Rows[0][5].ToString());
                }

                return results;
            }
            catch (SqlException ex)
            {
                #region Catch SQL Returned Error Message
                List<string> errList = new List<string>();
                errList.Add(Environment.NewLine + ex.GetBaseException().ToString());

                if (results.Tables != null && results.Tables.Count > 0 && results.Tables[0].Rows.Count > 0)
                {
                    foreach (DataTable item in results.Tables)
                    {
                        List<string> columnList = new List<string>();

                        foreach (DataColumn _column in item.Columns)
                        {
                            if (_column.ColumnName.ToLower().Contains("error"))
                            {
                                columnList.Add(_column.ColumnName);
                            }
                        }

                        foreach (DataRow row in item.Rows)
                        {
                            foreach (var columnName in columnList)
                            {
                                errList.Add(string.Format("{0} : {1}", columnName, row[columnName].ToString()));
                            }
                        }
                    }

                    if (results.Tables[0].Columns.Count >= 5 && errList.Count == 0)
                        throw new Exception(results.Tables[0].Rows[0][5].ToString());
                }

                throw new Exception(string.Join(Environment.NewLine, errList));
                #endregion
            }
            catch (AggregateException ae)
            {
                throw ae;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            //finally
            //{
            //    _con.Close();
            //}
        }

        public int UpdateDataByStoredProcedureReturningRowsAffected(SqlCommand command)
        {
            DataSet results = new DataSet();

            try
            {
                _con.Open();
                command.Connection = _con;


                int numOfRow = command.ExecuteNonQuery();

                _con.Close();
                return numOfRow;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _con.Close();
            }
        }

        public ObservableCollection<string> GetListOfExcelWorksheets()
        {
            try
            {
                ObservableCollection<string> worksheets = new ObservableCollection<string>();

                _objCon.Open();
                DataTable dt = _objCon.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                _objCon.Close();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["TABLE_NAME"].ToString().EndsWith("$"))
                        {
                            worksheets.Add(row["TABLE_NAME"].ToString().Substring(0, row["TABLE_NAME"].ToString().Length - 1));
                        }
                    }
                }

                return worksheets;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _objCon.Close();
            }
        }

        public List<KeyValuePair<string, Type>> GetListOfExcelWorksheetColumns(string worksheetName)
        {
            List<KeyValuePair<string, Type>> columns = new List<KeyValuePair<string, Type>>();

            try
            {
                _objCon.Open();
                OleDbDataAdapter adp = new OleDbDataAdapter("Select * from [" + worksheetName + "$]", _objCon);
                DataTable dt = new DataTable();
                adp.Fill(dt);
                _objCon.Close();

                foreach (DataColumn column in dt.Columns)
                {
                    string excelVersion = ExcelPath.Substring(ExcelPath.LastIndexOf('.'));
                    if (dt.Columns.Count == 1 && column.ToString().Equals("F1") && ((excelVersion.Equals(".xls") && dt.Rows.Count == 1) || (excelVersion.Equals(".xlsx") && dt.Rows.Count == 0)))
                    {
                        break;
                    }

                    columns.Add(new KeyValuePair<string, Type>(column.ColumnName, column.DataType));
                }

                return columns;

            }
            catch (Exception)
            {
                return columns;
            }
            finally
            {
                _objCon.Close();
            }
        }

        public DataSet ExecuteExcelQuery(string sql)
        {
            DataSet ds = new DataSet();

            try
            {
                _objCon.Open();
                OleDbDataAdapter adp = new OleDbDataAdapter(sql, _objCon);
                adp.Fill(ds);
                _objCon.Close();

                return ds;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _objCon.Close();
            }
        }

        //this is to use to capture Windows Structured Error Handling (SEH) errors as indicators of Corrupted State. These Corrupted State Exceptions (CSE) are not allowed to be caught by your standard managed code
        //this type of error will be raise when we perform write to excel file while the file is open by another program e.g Excel
        [HandleProcessCorruptedStateExceptions]
        public bool ExecuteExcelNonQuery(string sql)
        {
            try
            {
                _objCon.Open();
                OleDbCommand cmd = new OleDbCommand(sql, _objCon);
                cmd.ExecuteNonQuery();
                _objCon.Close();

                return true;
            }
            catch (System.AccessViolationException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _objCon.Close();
            }
        }

        public int SelectDataByStoredProcedureReturningRows(SqlCommand command)
        {
            DataSet results = new DataSet();

            try
            {
                _rdr = null;

                _con.Open();
                command.Connection = _con;

                _rdr = command.ExecuteReader();

                results.Tables.Add(new DataTable());
                results.Tables[0].Load(_rdr);

                _con.Close();

                int numOfRow = Int32.Parse(results.Tables[0].Rows[0][0].ToString());// command.ExecuteReader();

                return numOfRow;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _con.Close();
            }
        }
        #endregion

        #region Events
        static void FillErrorHandler(object sender, FillErrorEventArgs e)
        {
            // You can use the e.Errors value to determine exactly what
            // went wrong.
            if (e.Errors.GetType() == typeof(System.FormatException))
            {
                Console.WriteLine("Error when attempting to update the value: {0}",
                    e.Values[0]);
            }

            // Setting e.Continue to True tells the Load
            // method to continue trying. Setting it to False
            // indicates that an error has occurred, and the 
            // Load method raises the exception that got 
            // you here.
            e.Continue = true;
        }
        #endregion
    }

}
