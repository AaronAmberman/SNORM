using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SNORM
{
    /// <summary>Describes an object that will communicate to a database.</summary>
    public interface IDatabase : IConnectableDataSource
    {
        #region Properties

        /// <summary>Gets the state of the connection.</summary>
        ConnectionState ConnectionState { get; }

        /// <summary>Gets or sets the connection string.</summary>
        string ConnectionString { get; }

        #endregion

        #region Methods

        /// <summary>Begins a SQL transaction.</summary>
        /// <returns>The transaction.</returns>
        SqlTransaction BeginTransaction();

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data deleted from it.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Delete<T>(List<T> instances, string typeSchema, string createSchema);

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data updated.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Delete<T>(List<T> instances, SqlTransaction transaction, string typeSchema, string createSchema);

        /// <summary>Executes a SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="query">The SQL statement to execute.</param>
        /// <param name="commandType"></param>
        /// <param name="parameters"></param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        int ExecuteNonQuery(string query, CommandType commandType, params SqlParameter[] parameters);

        /// <summary>Executes a SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="transaction">The transaction to use for the command.</param>
        /// <param name="query">The SQL statement to execute.</param>
        /// <param name="commandType"></param>
        /// <param name="parameters"></param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        int ExecuteNonQuery(SqlTransaction transaction, string query, CommandType commandType, params SqlParameter[] parameters);

        /// <summary>Executes a SQL statement against the connection and returns the results or null if an error occurred.</summary>
        /// <param name="query"></param>
        /// <param name="commandType"></param>
        /// <param name="parameters"></param>
        /// <returns>The results or null if an error occurred.</returns>
        object[][] ExecuteQuery(string query, CommandType commandType, params SqlParameter[] parameters);

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data inserted into.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Insert<T>(List<T> instances, string typeSchema, string createSchema);

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data updated.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Insert<T>(List<T> instances, SqlTransaction transaction, string typeSchema, string createSchema);

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        List<T> Select<T>(string query, CommandType commandType, params SqlParameter[] parameters)
             where T : class, new();

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data updated.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Update<T>(List<T> instances, string typeSchema, string createSchema);

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data updated.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Update<T>(List<T> instances, SqlTransaction transaction, string typeSchema, string createSchema);

        #endregion
    }
}
