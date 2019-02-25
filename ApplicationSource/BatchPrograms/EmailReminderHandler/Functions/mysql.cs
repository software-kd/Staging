using System;
using MySql.Data.MySqlClient;
using System.Data;

namespace Service.Database
{
    public class mysql : IDisposable
    {
        protected MySqlConnection myConnection;
        //protected MySqlConnection myCommitConnection;
        protected MySqlCommand myCommand;
        protected string myConnectionString;
        protected int myCommandTimeOut;
        private bool disposed = false;

        #region New / Dispose / Connection String
        public mysql()
        {
            myCommandTimeOut = 30;
        }
        public mysql(string ConnectionString)
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception("Empty connection string. Please make sure the connection is valid.");
            }
            else
            {
                myConnectionString = ConnectionString;
                myCommandTimeOut = 30;
            }
        }
        public mysql(string ConnectionString, int CommandTimeOut)
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception("Empty connection string. Please make sure the connection is valid.");
            }
            else
            {
                myConnectionString = ConnectionString;
                myCommandTimeOut = CommandTimeOut;
            }
        }
        public string ConnectionString
        {
            get { return myConnectionString; }
            set { myConnectionString = value; }
        }
        public int CommandTimeOut
        {
            get { return myCommandTimeOut; }
            set { myCommandTimeOut = value; }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    myConnection = null;
                    //myCommitConnection = null;
                    myCommand = null;
                    myConnectionString = null;
                }
            }
            this.disposed = true;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region SQL Connection
        public void OpenConnection()
        {
            try
            {
                myConnection = new MySqlConnection(myConnectionString);
                myConnection.Open();
            }
            catch (Exception ex)
            {
                myConnection = null;
                throw ex;
            }
        }
        public void OpenCommitConnection()
        {
            try
            {
                myConnection = new MySqlConnection(myConnectionString);
                myConnection.Open();
            }
            catch (Exception ex)
            {
                myConnection = null;
                throw ex;
            }
        }
        public void CreateCommand()
        {
            try
            {
                myCommand = new MySqlCommand();
                myCommand.CommandTimeout = myCommandTimeOut;
            }
            catch (Exception ex)
            {
                myCommand = null;
                throw ex;
            }
        }
        public void CloseConnection()
        {
            try
            {
                if (myConnection.State != ConnectionState.Closed)
                {
                    myConnection.Close();
                    myConnection.Dispose();
                }
                myConnection = null;
            }
            catch (Exception ex)
            {
                myConnection = null;
                throw ex;
            }
        }
        public void CloseCommitConnection()
        {
            try
            {
                if (myConnection.State != ConnectionState.Closed)
                {
                    myConnection.Close();
                    myConnection.Dispose();
                }
                myConnection = null;
            }
            catch (Exception ex)
            {
                myConnection = null;
                throw ex;
            }
        }
        public MySqlConnection GetConnection
        {
            get { return myConnection; }
        }
        public MySqlConnection GetCommitConnection
        {
            get { return myConnection; }
        }
        #endregion

        #region Execute Query

        #region  DataSet
        public DataSet ExecuteQueryDataSet(string sqlString)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            DataSet myDataSet = new DataSet();
            OpenConnection();
            CreateCommand();
            myCommand.Connection = myConnection;
            myCommand.CommandText = sqlString;
            myDataAdapter.SelectCommand = myCommand;
            myDataAdapter.Fill(myDataSet);
            myCommand.Dispose();
            CloseConnection();
            myDataAdapter.Dispose();
            myDataAdapter = null;
            return myDataSet;
        }
        public DataSet ExecuteQueryDataSet(string sqlString, ref MySqlTransaction Context)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            DataSet myDataSet = new DataSet();
            CreateCommand();
            myCommand.Connection = Context.Connection;
            myCommand.CommandText = sqlString;
            myDataAdapter.SelectCommand = myCommand;
            myDataAdapter.Fill(myDataSet);
            myDataAdapter.Dispose();
            myDataAdapter = null;
            return myDataSet;
        }
        #endregion

        #region  Table
        public DataTable ExecuteQueryTable(string sqlString)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            DataTable myDataTable = new DataTable();
            try
            {
                OpenConnection();
                CreateCommand();
                myCommand.Connection = myConnection;
                myCommand.CommandText = sqlString;
                myDataAdapter.SelectCommand = myCommand;
                myDataAdapter.Fill(myDataTable);
                myCommand.Dispose();
                CloseConnection();
                myDataAdapter.Dispose();
                myDataAdapter = null;
                myDataTable.TableName = "TABLE";
                return myDataTable;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public DataTable ExecuteQueryTable(string sqlString, ref MySqlTransaction Context)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            DataTable myDataTable = new DataTable();
            CreateCommand();
            myCommand.Connection = Context.Connection;
            myCommand.CommandText = sqlString;
            myDataAdapter.SelectCommand = myCommand;
            myDataAdapter.Fill(myDataTable);
            myCommand.Dispose();
            myDataAdapter.Dispose();
            myDataAdapter = null;
            return myDataTable;
        }
        #endregion

        #region  Scalar
        public object ExecuteQueryScalar(string sqlString)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            object Result;
            OpenConnection();
            CreateCommand();
            myCommand.Connection = myConnection;
            myCommand.CommandText = sqlString;
            myDataAdapter.SelectCommand = myCommand;
            Result = myDataAdapter.SelectCommand.ExecuteScalar();
            myCommand.Dispose();
            CloseConnection();
            myDataAdapter.Dispose();
            myDataAdapter = null;
            return Result;
        }
        public object ExecuteQueryScalar(string sqlString, ref MySqlTransaction Context)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            object Result;
            CreateCommand();
            myCommand.Connection = Context.Connection;
            myCommand.CommandText = sqlString;
            myDataAdapter.SelectCommand = myCommand;
            Result = myDataAdapter.SelectCommand.ExecuteScalar();
            myCommand.Dispose();
            myDataAdapter.Dispose();
            myDataAdapter = null;
            return Result;
        }
        #endregion

        #region  Execute Non Query
        public void ExecuteNonQuery(string sqlString)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            OpenConnection();
            CreateCommand();
            myCommand.Connection = myConnection;
            myCommand.CommandText = sqlString;
            myDataAdapter.SelectCommand = myCommand;
            myDataAdapter.SelectCommand.ExecuteNonQuery();
            myCommand.Dispose();
            CloseConnection();
            myDataAdapter.Dispose();
            myDataAdapter = null;
        }
        public void ExecuteNonQuery(string sqlString, ref MySqlTransaction Context)
        {
            MySqlDataAdapter myDataAdapter = new MySqlDataAdapter();
            CreateCommand();
            myCommand.Connection = Context.Connection;
            myCommand.CommandText = sqlString;
            myDataAdapter.SelectCommand = myCommand;
            myDataAdapter.SelectCommand.ExecuteNonQuery();
            myCommand.Dispose();
            myDataAdapter.Dispose();
            myDataAdapter = null;
        }
        #endregion

        #endregion

        #region  Transactions
        public MySqlTransaction GetCommitTransaction()
        {

            //myConnection = new MySqlConnection();
            //myConnection.Open();
            return myConnection.BeginTransaction(IsolationLevel.ReadCommitted);
        }
        public void CommitTransaction(ref MySqlTransaction Context)
        {
            Context.Commit();
        }
        public void RollbackTransaction(ref MySqlTransaction Context)
        {
            Context.Rollback();
        }
        #endregion

        #region  Others
        public static string FixSQLString(string input)
        { try { return input.Replace("'", "''").Trim(); } catch { return input; } }
        #endregion
    }
}