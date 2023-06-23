using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SNORM
{
    /// <summary>A simple SQL class that provides ExecuteNonQuery (for non-SELECT statements) and ExecuteQuery (for SELECT statements).</summary>
    public static class SimpleSqlService
    {
        #region Methods

        /// <summary>Executes a Transact-SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="connection">The connection to the database.</param>
        /// <param name="autoConnect">Whether or not this method manages the state of the connection (e.g. opens the connection then closes it).</param>
        /// <param name="log">The method to call to write log messages to (only errors are logged).</param>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="sqlTransaction">The transaction to assign to the command. Default is null.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        public static int ExecuteNonQuery(SqlConnection connection, bool autoConnect, Action<string> log, string query, CommandType commandType, SqlTransaction sqlTransaction = null, params SqlParameter[] parameters)
        {
            int returnValue = -1;

            SqlTransaction transaction = null;

            try
            {
                if (connection == null)
                {
                    log("The connection is null and this cannot be, please set the connection property.");

                    return returnValue;
                }

                if (connection.State == ConnectionState.Connecting || connection.State == ConnectionState.Executing || connection.State == ConnectionState.Fetching)
                {
                    log("The connection is currently busy either connecting, executing or fetching. Failure.");

                    return returnValue;
                }

                if (autoConnect)
                {
                    if (connection.State == ConnectionState.Closed)
                        connection.Open();
                }

                if (sqlTransaction == null) transaction = connection.BeginTransaction();
                else transaction = sqlTransaction;

                SqlCommand command = new SqlCommand(query, connection, transaction)
                {
                    CommandType = commandType
                };

                if (parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                returnValue = command.ExecuteNonQuery();

                transaction.Commit();

                command.Dispose();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                log($"An error occurred during ExecuteNonQueryL {ex.Message}");
            }

            transaction?.Dispose();

            if (autoConnect)
                connection.Close();

            return returnValue;
        }

        /// <summary>Executes a Transact-SQL statement against the connection and returns the results or null if an error occurred.</summary>
        /// <param name="connection">The connection to the database.</param>
        /// <param name="autoConnect">Whether or not this method manages the state of the connection (e.g. opens the connection then closes it).</param>
        /// <param name="log">The method to call to write log messages to (only errors are logged).</param>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="sqlTransaction">The transaction to assign to the command. Default is null.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>The results or null if an error occurred.</returns>
        public static object[][] ExecuteQuery(SqlConnection connection, bool autoConnect, Action<string> log, string query, CommandType commandType, SqlTransaction sqlTransaction = null, params SqlParameter[] parameters)
        {
            try
            {
                if (connection == null)
                {
                    log("The connection is null and this cannot be, please set the connection property.");

                    return null;
                }

                if (connection.State == ConnectionState.Connecting || connection.State == ConnectionState.Executing || connection.State == ConnectionState.Fetching)
                {
                    log("The connection is currently busy either connecting, executing or fetching. Failure.");

                    return null;
                }

                if (autoConnect)
                {
                    if (connection.State == ConnectionState.Closed)
                        connection.Open();
                }

                SqlCommand command = new SqlCommand(query, connection)
                {
                    CommandType = commandType
                };

                if (sqlTransaction != null) command.Transaction = sqlTransaction;

                if (parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                SqlDataReader reader = command.ExecuteReader();

                List<object[]> rows = new List<object[]>();

                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        object[] row = new object[reader.FieldCount];

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            object value = reader.GetValue(i);

                            value = value == DBNull.Value ? null : value;

                            row[i] = value;
                        }

                        rows.Add(row);
                    }
                }

                reader.Close();

                command.Dispose();

                if (autoConnect)
                    connection.Close();

                return rows.ToArray();
            }
            catch (Exception ex)
            {
                log($"An error occurred during ExecuteQuery: {ex.Message}");

                return null;
            }
        }

        #endregion
    }
}
