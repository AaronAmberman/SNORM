using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace SNORM
{
    /// <summary>A simple SQL class that provides ExecuteNonQuery (for non-SELECT statements) and ExecuteQuery (for SELECT statements).</summary>
    public static class SimpleSqlService
    {
        #region Properties 

        /// <summary>Gets or sets whether or not to automatically connect and disconnect on each method call. Default is true.</summary>
        public static bool AutoConnect { get; set; } = true;

        /// <summary>Gets or sets the method to call for logging (only errors are logged).</summary>
        public static Action<string> Log { get; set; } = DefaultLogging;

        /// <summary>Gets or sets the SQL connection.</summary>
        public static SqlConnection Connection { get; set; }

        #endregion

        #region Methods

        private static void DefaultLogging(string entry)
        {
            Debug.WriteLine(entry);
        }

        /// <summary>Executes a Transact-SQL statement against the connection and returns the number of rows affected or -1 if an error occured.</summary>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>The number of rows affected or -1 if an error occured.</returns>
        public static int ExecuteNonQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
            int returnValue = -1;

            SqlTransaction transaction = null;

            try
            {
                if (Connection == null)
                {
                    Log("The connection is null and this cannot be, please set the Connection property.");

                    return returnValue;
                }

                if (Connection.State == ConnectionState.Connecting || Connection.State == ConnectionState.Executing || Connection.State == ConnectionState.Fetching)
                {
                    Log("The connection is currently busy either connecting, executing or fetching. Failure.");

                    return returnValue;
                }

                if (AutoConnect)
                {
                    if (Connection.State == ConnectionState.Closed)
                        Connection.Open();
                }

                transaction = Connection.BeginTransaction();

                SqlCommand command = new SqlCommand(query, Connection, transaction)
                {
                    CommandType = commandType
                };

                if (parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                returnValue = command.ExecuteNonQuery();

                transaction.Commit();
            }
            catch(Exception ex)
            {
                transaction?.Rollback();

                Log($"An error occurred attempting to run ExecuteNonQuery...{Environment.NewLine}{ex}");
            }

            transaction?.Dispose();

            if (AutoConnect)
                Connection.Close();

            return returnValue;
        }

        /// <summary>Executes a Transact-SQL statement against the connection and returns the results or null if an error occured.</summary>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>The results or null if an error occured.</returns>
        public static object[][] ExecuteQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
            try
            {
                if (Connection == null)
                {
                    Log("The connection is null and this cannot be, please set the Connection property.");

                    return null;
                }

                if (Connection.State == ConnectionState.Connecting || Connection.State == ConnectionState.Executing || Connection.State == ConnectionState.Fetching)
                {
                    Log("The connection is currently busy either connecting, executing or fetching. Failure.");

                    return null;
                }

                if (AutoConnect)
                {
                    if (Connection.State == ConnectionState.Closed)
                        Connection.Open();
                }

                SqlCommand command = new SqlCommand(query, Connection)
                {
                    CommandType = commandType
                };

                if (parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                SqlDataReader reader = command.ExecuteReader();

                List<object[]> rows = new List<object[]>();

                if (reader.HasRows)
                {
                    while(reader.Read())
                    {
                        object[] row = new object[reader.FieldCount];

                        for(int i = 0; i < reader.FieldCount; i++)
                        {
                            object value = reader.GetValue(i);

                            value = value == DBNull.Value ? null : value;
                        }

                        rows.Add(row);
                    }

                    reader.Close();
                }

                if (AutoConnect)
                    Connection.Close();

                return rows.ToArray();
            }
            catch(Exception ex)
            {
                Log($"An error occurred attempting to run ExecuteQuery...{Environment.NewLine}{ex}");

                return null;
            }
        }

        #endregion
    }
}
