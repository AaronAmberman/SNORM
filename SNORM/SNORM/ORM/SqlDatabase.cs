using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SNORM.ORM
{
    /// <summary>Represents a SQL database. This class cannot be inherited.</summary>
    public sealed class SqlDatabase : IDatabase
    {
        #region Constants

        public const string DEFAULT_SCHEMA = "dbo";

        #endregion

        #region Fields

        private bool disposedValue;
        private SqlConnection sqlConnection;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the connection.</summary>
        public ConnectionState ConnectionState => sqlConnection.State;

        /// <summary>Gets or sets the action to call to log an error.</summary>
        public Action<string> ErrorLogAction { get; set; }

        /// <summary>Gets the connection string.</summary>
        public string ConnectionString => sqlConnection.ConnectionString;

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance of <see cref="SqlDatabase"/>.</summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <exception cref="ArgumentNullException">The connectionString parameter is null, empty or consists of only white-space characters.</exception>
        public SqlDatabase(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrEmpty(builder.InitialCatalog))
                throw new ArgumentException("The connection string MUST contain a database name.");

            sqlConnection = new SqlConnection(builder.ConnectionString);

            ErrorLogAction = (string message) => 
            {
                Debug.WriteLine(message);
            };
        }

        #endregion

        #region Methods

        /// <summary>Begins a SQL transaction.</summary>
        /// <returns>The transaction.</returns>
        public SqlTransaction BeginTransaction()
        {
            VerifyDisposed();

            return sqlConnection.BeginTransaction();
        }

        /// <summary>Attempts to connect to the database.</summary>
        /// <returns>True if successfully connected otherwise false.</returns>
        public bool Connect()
        {
            VerifyDisposed();

            try
            {
                sqlConnection.Open();

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during Connect: {ex.Message}");

                return false;
            }
        }

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type. Call this prior to BeginTransaction, Delete, Insert, Select or Update but after Connect.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <returns>True if created otherwise false.</returns>
        /// <remarks>Use for Deletes and Updates only.</remarks>
        public bool CreateTvpType(Type type)
        {
            return CreateTvpType(type, true);
        }

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type. Call this prior to BeginTransaction, Delete, Insert, Select or Update but after Connect.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <param name="includeAutoIncrementColumns">Whether or not to include auto increment columns from the table.</param>
        /// <returns>True if created otherwise false.</returns>
        /// <remarks>For Deletes and Updates use auto increment columns, do not for Inserts.</remarks>
        public bool CreateTvpType(Type type, bool includeAutoIncrementColumns)
        {
            VerifyDisposed();

            return CreateTvpType(type, GetSchemaFromType(type), includeAutoIncrementColumns);
        }

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type. Call this prior to BeginTransaction, Delete, Insert, Select or Update but after Connect.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <param name="schema">The schema for the TVP type.</param>
        /// <param name="includeAutoIncrementColumns">Whether or not to include auto increment columns from the table.</param>        
        /// <returns>True if created otherwise false.</returns>
        /// <remarks>For Deletes and Updates use auto increment columns, do not for Inserts.</remarks>
        public bool CreateTvpType(Type type, string schema, bool includeAutoIncrementColumns)
        {
            VerifyDisposed();

            if (ConnectionState != ConnectionState.Open)
            {
                ErrorLogAction("There must be an open and not busy connection waiting. Please connect first by calling Connect.");

                return false;
            }

            if (!DropTvpType(type, schema)) return false;

            List<SqlColumn> columns = GetColumnInformation(type);
            string typeName = GetTypeName(type);

            string query = $"CREATE TYPE {schema}.{typeName}AsTvp AS TABLE (";

            foreach (SqlColumn column in columns)
            {
                if (!includeAutoIncrementColumns && column.ColumnInfo.AutoIncrement) continue;

                SqlColumnAttribute sca = column.PropertyInfo.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? column.ColumnName : sca.ColumnName;

                query += $"{columnName} {column.ColumnInfo.Type}";

                // if our column is of a data type that requires a length specification then we need to specifiy the length
                if (column.ColumnInfo.Type == SqlDbType.Char || column.ColumnInfo.Type == SqlDbType.VarChar || column.ColumnInfo.Type == SqlDbType.Text ||
                    column.ColumnInfo.Type == SqlDbType.NText || column.ColumnInfo.Type == SqlDbType.NChar || column.ColumnInfo.Type == SqlDbType.NVarChar ||
                    column.ColumnInfo.Type == SqlDbType.Binary || column.ColumnInfo.Type == SqlDbType.VarBinary || column.ColumnInfo.Type == SqlDbType.Image)
                {
                    query += $" ({column.ColumnInfo.Length})";
                }

                query += ",";
            }

            query = query.Substring(0, query.Length - 1); // remove the last comma
            query += ");";

            try
            {
                SqlCommand command = new SqlCommand(query, sqlConnection);
                command.ExecuteNonQuery();
                command.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred attempting to create TVP type: {ex.Message}");

                return false;
            }
        }

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data deleted from it.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Delete<T>(List<T> instances)
        {
            VerifyDisposed();

            if (instances == null || instances.Count == 0)
                throw new ArgumentNullException(nameof(instances));

            int returnValue;
            SqlTransaction transaction = null;

            try
            {
                transaction = sqlConnection.BeginTransaction();

                returnValue = Delete<T>(instances, transaction);

                if (returnValue == -1) transaction.Rollback();
                else transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                ErrorLogAction($"An error occurred during Delete: {ex.Message}");

                returnValue = -1;
            }

            transaction?.Dispose();

            return returnValue;
        }

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Delete<T>(List<T> instances, SqlTransaction transaction)
        {
            VerifyDisposed();

            if (instances == null || instances.Count == 0)
                throw new ArgumentNullException(nameof(instances));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            Type type = typeof(T);
            var schemaAndTypeName = GetSchemaAndTypeNameFromType(type);

            List<SqlColumn> columns = GetColumnInformation(type);

            if (columns == null || columns.Count == 0) return -1;

            int returnValue;

            try
            {
                string query = $"DELETE FROM {schemaAndTypeName.Schema}.{schemaAndTypeName.TypeName} WHERE EXISTS (SELECT tvp.* FROM @tvp AS tvp)";
                DataTable dt = GenerateTvpDataTable(instances, columns, true);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{schemaAndTypeName.Schema}.{schemaAndTypeName.TypeName}AsTvp",
                    Value = dt
                });

                returnValue = command.ExecuteNonQuery();

                command.Dispose();
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during Delete: {ex.Message}");

                returnValue = -1;
            }

            return returnValue;
        }

        /// <summary>Disconnects from the database.</summary>
        public void Disconnect()
        {
            VerifyDisposed();

            try
            {
                sqlConnection.Close();
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during Disconnect: {ex.Message}");
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    sqlConnection.Dispose();
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

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <returns>True if created otherwise false.</returns>
        public bool DropTvpType(Type type)
        {
            VerifyDisposed();

            return DropTvpType(type, GetSchemaFromType(type));
        }

        /// <summary>Creates a tabled-value parameter (TVP) SQL type for the specified type.</summary>
        /// <param name="type">The type to create a TVP for.</param>
        /// <param name="schema">The schema for the TVP type.</param>
        /// <returns>True if created otherwise false.</returns>
        public bool DropTvpType(Type type, string schema)
        {
            VerifyDisposed();

            string typeName = GetTypeName(type);

            try
            {
                string query = $"IF EXISTS(SELECT 1 FROM sys.types WHERE name = @name AND is_table_type = 1 AND schema_id = SCHEMA_ID(@schemaName)) DROP TYPE {schema}.{typeName}AsTvp;";

                SqlCommand command = new SqlCommand(query, sqlConnection);
                command.Parameters.Add(new SqlParameter("@name", $"{typeName}AsTvp"));
                command.Parameters.Add(new SqlParameter("@schemaName", schema));

                command.ExecuteNonQuery();
                command.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred attempting to drop TVP type: {ex.Message}");

                return false;
            }
        }

        /// <summary>Executes a SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="query">The SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        public int ExecuteNonQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
            VerifyDisposed();

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            SqlTransaction transaction = null;
            int returnValue;

            try
            {
                // begin our transaction so we can roll back if needed
                transaction = sqlConnection.BeginTransaction();

                returnValue = ExecuteNonQuery(transaction, query, commandType, parameters);

                if (returnValue == -1) transaction.Rollback();
                else transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                ErrorLogAction($"An error occurred during ExecuteNonQuery: {ex.Message}");

                returnValue = -1;
            }

            transaction?.Dispose();

            return returnValue;
        }

        /// <summary>Executes a SQL statement against the connection and returns the number of rows affected or -1 if an error occurred.</summary>
        /// <param name="transaction">The transaction the command is being executed in.</param>
        /// <param name="query">The SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The number of rows affected or -1 if an error occurred.</returns>
        public int ExecuteNonQuery(SqlTransaction transaction, string query, CommandType commandType, params SqlParameter[] parameters)
        {
            VerifyDisposed();

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            int returnValue;

            try
            {
                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.AddRange(parameters);

                returnValue = command.ExecuteNonQuery();

                command.Dispose();
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during ExecuteNonQuery: {ex.Message}");

                returnValue = -1;
            }

            return returnValue;
        }

        /// <summary>Executes a SQL statement against the connection and returns the results or null if an error occurred.</summary>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>The results or null if an error occurred.</returns>
        public object[][] ExecuteQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
            VerifyDisposed();

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            try
            {
                SqlCommand command = new SqlCommand(query, sqlConnection)
                {
                    CommandType = commandType
                };

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

                return rows.ToArray();
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during ExecuteQuery: {ex.Message}");

                return null;
            }
        }

        private DataTable GenerateTvpDataTable<T>(List<T> instances, List<SqlColumn> columns, bool includeAutoIncrementColumns)
        {
            DataTable dt = new DataTable();

            foreach (SqlColumn column in columns)
            {
                if (!includeAutoIncrementColumns && column.ColumnInfo.AutoIncrement) continue;

                dt.Columns.Add(column.ColumnName, column.ColumnInfo.DotNetType);
            }

            foreach (T instance in instances)
            {
                // get values to add to this row
                List<object> values = new List<object>();

                foreach (SqlColumn column in columns)
                {
                    if (!includeAutoIncrementColumns && column.ColumnInfo.AutoIncrement) continue;

                    values.Add(column.PropertyInfo.GetValue(instance));
                }

                dt.Rows.Add(values.ToArray());
            }

            return dt;
        }

        private string GenerateInsertTvpQuery(Type type, List<SqlColumn> columns)
        {
            var schemaAndTypeName = GetSchemaAndTypeNameFromType(type);

            string query = $"INSERT INTO {schemaAndTypeName.Schema}.{schemaAndTypeName.TypeName} (";

            foreach (SqlColumn column in columns)
            {
                // we don't need to insert data into auto incremented columns
                if (column.ColumnInfo.AutoIncrement) continue;

                SqlColumnAttribute sca = column.PropertyInfo.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? column.ColumnName : sca.ColumnName;

                query += $"{columnName},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);

            query += ") SELECT ";

            foreach (SqlColumn column in columns)
            {
                // we don't need to insert data into auto incremented columns
                if (column.ColumnInfo.AutoIncrement) continue;

                SqlColumnAttribute sca = column.PropertyInfo.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? column.ColumnName : sca.ColumnName;

                query += $"tvp.{columnName},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);

            query += " FROM @TVP AS tvp";

            return query;
        }

        private string GenerateUpdateTvpQuery(Type type, List<SqlColumn> columns)
        {
            var schemaAndTypeName = GetSchemaAndTypeNameFromType(type);

            string query = $"UPDATE {schemaAndTypeName.Schema} . {schemaAndTypeName.TypeName} SET ";

            foreach (SqlColumn column in columns)
            {
                // we do not update auto incrementing columns
                if (column.ColumnInfo.AutoIncrement) continue;

                SqlColumnAttribute sca = column.PropertyInfo.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? column.ColumnName : sca.ColumnName;

                query += $"{schemaAndTypeName.TypeName}.{columnName} = tvp.{columnName},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);
            query += $" FROM {schemaAndTypeName.TypeName} INNER JOIN @tvp AS tvp ON ";

            List<SqlColumn> primaryKeyColumns = columns.Where(c => c.ColumnInfo.IsPrimaryKey).ToList();

            foreach (SqlColumn column in primaryKeyColumns)
            {
                SqlColumnAttribute sca = column.PropertyInfo.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? column.ColumnName : sca.ColumnName;

                query += $"{schemaAndTypeName.TypeName}.{columnName} = tvp.{columnName} AND ";
            }

            // remove the last ' AND '
            query = query.Substring(0, query.Length - 5);

            return query;
        }

        private List<SqlColumn> GetColumnInformation(Type type, SqlTransaction transaction = null)
        {
            List<SqlColumn> columns = new List<SqlColumn>();

            PropertyInfo[] publicProperties = type.GetProperties();
            List<PropertyInfo> publicPropertiesWithAttributes = publicProperties.Where(pi => pi.GetCustomAttribute<SqlColumnAttribute>() != null).ToList();

            List<SqlColumnInfo> tempColumns = GetTableInformation(type, transaction);

            if (tempColumns == null || tempColumns.Count == 0) return null;

            // we need to check to see if we have an identity (auto-incremented) column, we require it
            SqlColumnInfo primaryKeyAutoIncrementedColumn = tempColumns.FirstOrDefault(col => col.AutoIncrement && col.IsPrimaryKey);

            if (primaryKeyAutoIncrementedColumn == null)
            {
                throw new InvalidOperationException("The referenced table in the database does not contain a primary key column that is an identity (auto-incremented). This API requires this.");
            }

            foreach (SqlColumnInfo col in tempColumns)
            {
                // first match off a custom attribute...if there one defined
                PropertyInfo matchingPropertyInfo = publicPropertiesWithAttributes.FirstOrDefault(pi => pi.GetCustomAttribute<SqlColumnAttribute>().ColumnName.Equals(col.Name, StringComparison.OrdinalIgnoreCase));

                // if not, match of the name of the property itself
                if (matchingPropertyInfo == null)
                    matchingPropertyInfo = publicProperties.FirstOrDefault(pi => pi.Name.Equals(col.Name, StringComparison.OrdinalIgnoreCase));

                columns.Add(new SqlColumn
                {
                    ColumnName = col.Name,
                    ColumnInfo = col,
                    PropertyInfo =  matchingPropertyInfo
                });
            }

            return columns;
        }

        private string GetSchemaFromType(Type type)
        {
            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string schema = sta == null ? DEFAULT_SCHEMA : string.IsNullOrWhiteSpace(sta.Schema) ? DEFAULT_SCHEMA : sta.Schema;

            return schema;
        }

        private (string Schema, string TypeName) GetSchemaAndTypeNameFromType(Type type)
        {
            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? DEFAULT_SCHEMA : string.IsNullOrWhiteSpace(sta.Schema) ? DEFAULT_SCHEMA : sta.Schema;

            return (schema, typeName);
        }

        private List<SqlColumnInfo> GetTableInformation(Type type, SqlTransaction sqlTransaction = null)
        {
            try
            {
                SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

                string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
                string schema = sta == null ? DEFAULT_SCHEMA : string.IsNullOrWhiteSpace(sta.Schema) ? DEFAULT_SCHEMA : sta.Schema;

                List<SqlColumnInfo> tempColumns = 
                    SqlInformationService.GetTableInformation(sqlConnection, schema, typeName, ErrorLogAction, sqlTransaction);

                return tempColumns;
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred attempting to get table information: {ex.Message}");

                return null;
            }
        }

        private string GetTypeName(Type type)
        {
            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;

            return typeName;
        }

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Insert<T>(List<T> instances)
        {
            VerifyDisposed();

            if (instances == null || instances.Count == 0)
                throw new ArgumentNullException(nameof(instances));

            int returnValue;
            SqlTransaction transaction = null;

            try
            {
                transaction = sqlConnection.BeginTransaction();

                returnValue = Insert<T>(instances, transaction);

                if (returnValue == -1) transaction.Rollback();
                else transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                ErrorLogAction($"An error occurred during Insert: {ex.Message}");

                returnValue = -1;
            }

            transaction?.Dispose();

            return returnValue;
        }

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Insert<T>(List<T> instances, SqlTransaction transaction)
        {
            VerifyDisposed();

            if (instances == null || instances.Count == 0)
                throw new ArgumentNullException(nameof(instances));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            Type type = typeof(T);

            var schemaAndTypeName = GetSchemaAndTypeNameFromType(type);

            List<SqlColumn> columns = GetColumnInformation(type);

            if (columns == null || columns.Count == 0) return -1;

            int returnValue;

            try
            {
                string query = GenerateInsertTvpQuery(type, columns);
                DataTable dt = GenerateTvpDataTable(instances, columns, false);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{schemaAndTypeName.Schema}.{schemaAndTypeName.TypeName}AsTvp",
                    Value = dt
                });

                returnValue = command.ExecuteNonQuery();

                command.Dispose();
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during Insert: {ex.Message}");

                returnValue = -1;
            }

            return returnValue;
        }

        /// <summary>Returns all the objects of type T from the database.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <returns>A list of all instances of type T or null if an error occurred.</returns>
        public List<T> Select<T>()
            where T : class, new()
        {
            VerifyDisposed();

            Type type = typeof(T);

            var schemaAndTypeName = GetSchemaAndTypeNameFromType(type);

            string query = $"SELECT * FROM {schemaAndTypeName.Schema}.{schemaAndTypeName.TypeName}";

            return Select<T>(query, CommandType.Text);
        }

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        public List<T> Select<T>(string query) where T : class, new()
        {
            return Select<T>(query, CommandType.Text);
        }

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        public List<T> Select<T>(string query, params SqlParameter[] parameters)
            where T : class, new()
        {
            return Select<T>(query, CommandType.Text, parameters);
        }

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        public List<T> Select<T>(string query, CommandType commandType, params SqlParameter[] parameters)
             where T : class, new()
        {
            return Select<T>(null, query, commandType, parameters);
        }

        /// <summary>Selects objects from the database and maps the results to the instances of type T.</summary>
        /// <typeparam name="T">The type of object to map the results to.</typeparam>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <param name="query">The Transact-SQL statement to execute.</param>
        /// <param name="commandType">The type of command.</param>
        /// <param name="parameters">Parameters, if any, for the Transact-SQL statement.</param>
        /// <returns>A list of instances of type T returned by the query or null if an error occurred.</returns>
        public List<T> Select<T>(SqlTransaction transaction, string query, CommandType commandType, params SqlParameter[] parameters) where T : class, new()
        {
            VerifyDisposed();

            if (string.IsNullOrEmpty(query))
            {
                ErrorLogAction("The query null, empty or consists only of white-space characters. Please set the query.");

                return null;
            }

            if (sqlConnection.State != ConnectionState.Open)
            {
                ErrorLogAction("There must be an open and not busy connection waiting. Please connect first by calling Connect.");

                return null;
            }

            try
            {
                List<T> returnValues = new List<T>();

                Type type = typeof(T);
                PropertyInfo[] publicProperties = type.GetProperties();
                List<PropertyInfo> publicPropertiesWithAttributes = publicProperties.Where(pi => pi.GetCustomAttribute<SqlColumnAttribute>() != null).ToList();

                SqlCommand command = new SqlCommand(query, sqlConnection)
                {
                    CommandType = commandType
                };

                if (transaction != null) command.Transaction = transaction;

                if (parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                SqlDataReader reader = command.ExecuteReader();

                if (reader.HasRows)
                {
                    List<string> columns = new List<string>();

                    while (reader.Read())
                    {
                        T instance = new T();

                        // get columns (only needs to be done once)
                        if (columns.Count == 0)
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string column = reader.GetName(i);

                                columns.Add(column);
                            }
                        }

                        // get value
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            // first match off a custom attribute...if there one defined
                            PropertyInfo matchingPropertyInfo = publicPropertiesWithAttributes.FirstOrDefault(pi => pi.GetCustomAttribute<SqlColumnAttribute>().ColumnName.Equals(columns[i], StringComparison.OrdinalIgnoreCase));

                            // if not, match of the name of the property itself
                            if (matchingPropertyInfo == null)
                                matchingPropertyInfo = publicProperties.FirstOrDefault(pi => pi.Name.Equals(columns[i], StringComparison.OrdinalIgnoreCase));

                            // if we couldn't find a match then just iterate
                            if (matchingPropertyInfo == null) continue;

                            object value = reader.GetValue(i);
                            value = value == DBNull.Value ? null : value;

                            matchingPropertyInfo.SetValue(instance, value);
                        }

                        returnValues.Add(instance);
                    }

                    reader.Close();
                }

                return returnValues;
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during Select:{ex.Message}");

                return null;
            }
        }

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Update<T>(List<T> instances)
        {
            VerifyDisposed();

            if (instances == null || instances.Count == 0)
                throw new ArgumentNullException(nameof(instances));

            int returnValue;
            SqlTransaction transaction = null;

            try
            {
                transaction = sqlConnection.BeginTransaction();

                returnValue = Update<T>(instances, transaction);

                if (returnValue == -1) transaction.Rollback();
                else transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                ErrorLogAction($"An error occurred during Update: {ex.Message}");

                returnValue = -1;
            }

            transaction?.Dispose();

            return returnValue;
        }

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <param name="transaction">The SQL transaction to use for the query.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Update<T>(List<T> instances, SqlTransaction transaction)
        {
            VerifyDisposed();

            if (instances == null || instances.Count == 0)
                throw new ArgumentNullException(nameof(instances));

            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            Type type = typeof(T);

            var schemaAndTypeName = GetSchemaAndTypeNameFromType(type);

            List<SqlColumn> columns = GetColumnInformation(type);

            if (columns == null || columns.Count == 0) return -1;

            int returnValue;

            try
            {
                string query = GenerateUpdateTvpQuery(type, columns);
                DataTable dt = GenerateTvpDataTable(instances, columns, true);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{schemaAndTypeName.Schema} . {schemaAndTypeName.TypeName}AsTvp",
                    Value = dt
                });

                returnValue = command.ExecuteNonQuery();

                command.Dispose();
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred during Update: {ex.Message}");

                returnValue = -1;
            }

            return returnValue;
        }

        private void VerifyDisposed([CallerMemberName] string caller = "")
        {
            if (disposedValue)
                throw new ObjectDisposedException("Solis.Database.Sql.ORM.SqlDatabase", $"{caller} cannot be accessed because the object instance has been disposed.");
        }

        #endregion

        #region Private Classes

        private class SqlColumn
        {
            #region Properties

            public string ColumnName { get; set; }
            public SqlColumnInfo ColumnInfo { get; set; }
            public PropertyInfo PropertyInfo { get; set; }

            #endregion
        }

        #endregion
    }
}
