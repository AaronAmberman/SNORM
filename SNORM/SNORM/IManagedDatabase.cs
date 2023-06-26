using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SNORM
{
    /// <summary>Describes a simplified database object.</summary>
    public interface IManagedDatabase : IConnectableDataSource
    {
        #region Properties

        /// <summary>Gets the state of the connection.</summary>
        ConnectionState ConnectionState { get; }

        /// <summary>Gets or sets the connection string.</summary>
        string ConnectionString { get; }

        #endregion

        #region Methods

        /// <summary>Executes a SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="query">The SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        int ExecuteNonQuery(string query, CommandType commandType, params SqlParameter[] parameters);

        /// <summary>Executes a SQL statement against the connection and returns the results or null if an error occurred.</summary>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The results or null if an error occurred.</returns>
        object[][] ExecuteQuery(string query, CommandType commandType, params SqlParameter[] parameters);

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        List<T> Select<T>(string query, CommandType commandType, params SqlParameter[] parameters)
             where T : class, new();

        #endregion
    }
}
