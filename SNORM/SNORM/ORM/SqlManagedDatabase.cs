using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;

namespace SNORM.ORM
{
    /// <summary>Describes an object that will communicate to a database (a simplfied implementation).</summary>
    public sealed class SqlManagedDatabase : IManagedDatabase
    {
        #region Constants

        public const string DEFAULT_SCHEMA = "dbo";

        #endregion

        #region Fields

        private bool disposedValue;
        private SqlDatabase sqlDatabase;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the connection.</summary>
        public ConnectionState ConnectionState => sqlDatabase.ConnectionState;

        /// <summary>Gets the connection string.</summary>
        public string ConnectionString => sqlDatabase.ConnectionString;

        /// <summary>Gets or sets the action to call to log an error.</summary>
        public Action<string> ErrorLogAction 
        {
            get => sqlDatabase.ErrorLogAction;
            set => sqlDatabase.ErrorLogAction = value;
        }

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance of <see cref="SqlManagedDatabase"/>.</summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <exception cref="ArgumentNullException">The connectionString parameter is null, empty or consists of only white-space characters.</exception>
        public SqlManagedDatabase(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrEmpty(builder.InitialCatalog))
                throw new ArgumentException("The connection string MUST contain a database name.");

            sqlDatabase = new SqlDatabase(builder.ConnectionString);
        }

        #endregion

        #region Methods

        /// <summary>Attempts to connect to the database.</summary>
        /// <returns>True if successfully connected otherwise false.</returns>
        public bool Connect()
        {
            return sqlDatabase.Connect();
        }

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Delete<T>(List<T> instances)
        {
            VerifyDisposed();

            Type type = typeof(T);

            if (!sqlDatabase.CreateTvpType(type, true)) return -1;

            int result = sqlDatabase.Delete(instances);

            sqlDatabase.DropTvpType(type);

            return result;
        }

        /// <summary>Disconnects from the database.</summary>
        public void Disconnect()
        {
            sqlDatabase.Disconnect();
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    sqlDatabase.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged and managed resources.</summary>
        public void Dispose()
        {
            VerifyDisposed();

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>Executes a SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="query">The SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        public int ExecuteNonQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
            return sqlDatabase.ExecuteNonQuery(query, commandType, parameters);
        }

        /// <summary>Executes a SQL statement against the connection and returns the results or null if an error occurred.</summary>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The results or null if an error occurred.</returns>
        public object[][] ExecuteQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
            return sqlDatabase.ExecuteQuery(query, commandType, parameters);
        }

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Insert<T>(List<T> instances)
        {
            VerifyDisposed();

            Type type = typeof(T);

            if (!sqlDatabase.CreateTvpType(type, false)) return -1;

            int result = sqlDatabase.Insert(instances);

            sqlDatabase.DropTvpType(type);

            return result;
        }

        /// <summary>Returns all the objects of type T from the database.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <returns>A list of all instances of type T or null if an error occurred.</returns>
        public List<T> Select<T>() where T : class, new()
        {
            return sqlDatabase.Select<T>();
        }

        /// <summary>Returns objects of type T that the query matches.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <param name="query">The query to execute to select records.</param>
        /// <returns>A list of instances that are returned from the query.</returns>
        public List<T> Select<T>(string query) where T : class, new()
        {
            return Select<T>(query, CommandType.Text);
        }

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        public List<T> Select<T>(string query, CommandType commandType, params SqlParameter[] parameters) where T : class, new()
        {
            return sqlDatabase.Select<T>(query, commandType, parameters);
        }

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Update<T>(List<T> instances)
        {
            VerifyDisposed();

            Type type = typeof(T);

            if (!sqlDatabase.CreateTvpType(type, true)) return -1;

            int result = sqlDatabase.Update(instances);

            sqlDatabase.DropTvpType(type);

            return result;
        }

        private void VerifyDisposed([CallerMemberName] string caller = "")
        {
            if (disposedValue)
                throw new ObjectDisposedException("Solis.Database.Sql.ORM.SqlManagedDatabase", $"{caller} cannot be accessed because the object instance has been disposed.");
        }

        #endregion
    }
}
