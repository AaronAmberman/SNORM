using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SNORM
{
    /// <summary>Describes an object that will communicate to a database.</summary>
    public interface IDatabase : IManagedDatabase
    {
        #region Methods

        /// <summary>Begins a SQL transaction.</summary>
        /// <returns>The transaction.</returns>
        SqlTransaction BeginTransaction();

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type. Call this prior to BeginTransaction, Delete, Insert, Select or Update but after Connect.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <returns>True if created otherwise false.</returns>
        bool CreateTvpType(Type type);

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type. Call this prior to BeginTransaction, Delete, Insert, Select or Update but after Connect.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <param name="includeAutoIncrementColumns">Whether or not to include auto increment columns from the table.</param>
        /// <returns>True if created otherwise false.</returns>
        bool CreateTvpType(Type type, bool includeAutoIncrementColumns);

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type. Call this prior to BeginTransaction, Delete, Insert, Select or Update but after Connect.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <param name="schema">The schema for the TVP type.</param>
        /// <param name="includeAutoIncrementColumns">Whether or not to include auto increment columns from the table.</param>
        /// <returns>True if created otherwise false.</returns>
        bool CreateTvpType(Type type, string schema, bool includeAutoIncrementColumns);

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Delete<T>(List<T> instances, SqlTransaction transaction);

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <returns>True if created otherwise false.</returns>
        bool DropTvpType(Type type);

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <param name="schema">The schema for the TVP type.</param>
        /// <returns>True if created otherwise false.</returns>
        bool DropTvpType(Type type, string schema);

        /// <summary>Executes a SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="transaction">The transaction to use for the command.</param>
        /// <param name="query">The SQL statement to execute.</param>
        /// /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        int ExecuteNonQuery(SqlTransaction transaction, string query, CommandType commandType, params SqlParameter[] parameters);

        /// <summary>Executes a SQL statement against the connection and returns the results or null if an error occurred.</summary>
        /// <param name="transaction">The transaction to use for the command.</param>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The results or null if an error occurred.</returns>
        object[][] ExecuteQuery(SqlTransaction transaction, string query, CommandType commandType, params SqlParameter[] parameters);

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Insert<T>(List<T> instances, SqlTransaction transaction);

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        List<T> Select<T>(SqlTransaction transaction, string query, CommandType commandType, params SqlParameter[] parameters)
             where T : class, new();

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Update<T>(List<T> instances, SqlTransaction transaction);

        #endregion
    }
}
