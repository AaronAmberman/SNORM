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

        /// <summary>Gets or sets the error log action (logs errors).</summary>
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
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrEmpty(builder.InitialCatalog))
            {
                throw new ArgumentException("The connection string MUST contain a database name.");
            }

            sqlConnection = new SqlConnection(builder.ConnectionString);

            ErrorLogAction = (string errorMessage) => 
            {
                Debug.WriteLine(errorMessage);
            };
        }

        #endregion

        #region Methods

        /// <summary>Begins a SQL transaction.</summary>
        /// <returns>The transaction.</returns>
        public SqlTransaction BeginTransaction()
        {
            return sqlConnection.BeginTransaction();
        }

        private void CreateTvpType(Type type, string createSchema, Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columnAndPropertyMetadata, bool includeAutoIncrementColumns)
        {
            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? createSchema : string.IsNullOrWhiteSpace(sta.Schema) ? createSchema : sta.Schema;

            string query = $"IF EXISTS(SELECT 1 FROM sys.types WHERE name = '{typeName}AsTvp' AND is_table_type = 1 AND schema_id = SCHEMA_ID('{schema}')) " +
                $"DROP TYPE {schema}.{typeName}AsTvp;" +
                $"CREATE TYPE {schema}.{typeName}AsTvp AS TABLE (";

            foreach (KeyValuePair<string, Tuple<SqlColumnInfo, PropertyInfo>> kvp in columnAndPropertyMetadata)
            {
                if (!includeAutoIncrementColumns && kvp.Value.Item1.AutoIncrement)
                    continue;

                SqlColumnAttribute sca = kvp.Value.Item2.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? kvp.Value.Item1.Name : sca.ColumnName;

                query += $"{columnName} {kvp.Value.Item1.Type}";

                // if our column is of a data type that requires a length specification then we need to specifiy the length
                if (kvp.Value.Item1.Type == SqlDbType.Char || kvp.Value.Item1.Type == SqlDbType.VarChar || kvp.Value.Item1.Type == SqlDbType.Text ||
                    kvp.Value.Item1.Type == SqlDbType.NText || kvp.Value.Item1.Type == SqlDbType.NChar || kvp.Value.Item1.Type == SqlDbType.NVarChar ||
                    kvp.Value.Item1.Type == SqlDbType.Binary || kvp.Value.Item1.Type == SqlDbType.VarBinary || kvp.Value.Item1.Type == SqlDbType.Image)
                {
                    query += $" ({kvp.Value.Item1.Length})";
                }

                query += ",";
            }

            query = query.Substring(0, query.Length - 1); // remove the last comma
            query += ");";

            SqlCommand command = new SqlCommand(query, sqlConnection);

            command.ExecuteNonQuery();
            command.Dispose();
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

        private bool DropTvpType(Type type, string createSchema)
        {
            try
            {
                SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

                string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
                string schema = sta == null ? createSchema : string.IsNullOrWhiteSpace(sta.Schema) ? createSchema : sta.Schema;

                string query = $"DROP TYPE {schema}.{typeName}AsTvp";

                SqlCommand command = new SqlCommand(query, sqlConnection);

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

        private DataTable GenerateTvpDataTable<T>(List<T> instances, Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columnAndPropertyMetadata, bool includeAutoIncrementColumns)
        {
            DataTable dt = new DataTable();

            foreach (KeyValuePair<string, Tuple<SqlColumnInfo, PropertyInfo>> kvp in columnAndPropertyMetadata)
            {
                if (!includeAutoIncrementColumns && kvp.Value.Item1.AutoIncrement) continue;

                dt.Columns.Add(kvp.Key, kvp.Value.Item1.DotNetType);
            }

            foreach (T instance in instances)
            {
                // get values to add to this row
                List<object> values = new List<object>();

                foreach (KeyValuePair<string, Tuple<SqlColumnInfo, PropertyInfo>> kvp in columnAndPropertyMetadata)
                {
                    // we don't need to insert data into auto incremented columns
                    if (!includeAutoIncrementColumns && kvp.Value.Item1.AutoIncrement) continue;

                    values.Add(kvp.Value.Item2.GetValue(instance));
                }

                dt.Rows.Add(values.ToArray());
            }

            return dt;
        }

        private string GenerateInsertTvpQuery(Type type, string typeSchema, Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columns)
        {
            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? typeSchema : string.IsNullOrWhiteSpace(sta.Schema) ? typeSchema : sta.Schema;

            string query = $"INSERT INTO {schema}.{typeName} (";

            foreach (KeyValuePair<string, Tuple<SqlColumnInfo, PropertyInfo>> kvp in columns)
            {
                // we don't need to insert data into auto incremented columns
                if (kvp.Value.Item1.AutoIncrement) continue;

                SqlColumnAttribute sca = kvp.Value.Item2.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? kvp.Value.Item1.Name : sca.ColumnName;

                query += $"{columnName},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);

            query += ") SELECT ";

            foreach (KeyValuePair<string, Tuple<SqlColumnInfo, PropertyInfo>> kvp in columns)
            {
                // we don't need to insert data into auto incremented columns
                if (kvp.Value.Item1.AutoIncrement) continue;

                SqlColumnAttribute sca = kvp.Value.Item2.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? kvp.Value.Item1.Name : sca.ColumnName;

                query += $"tvp.{columnName},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);

            query += " FROM @TVP AS tvp";

            return query;
        }

        private string GenerateUpdateTvpQuery(Type type, string typeSchema, Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columns)
        {
            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? typeSchema : string.IsNullOrWhiteSpace(sta.Schema) ? typeSchema : sta.Schema;

            string query = $"UPDATE {schema}.{typeName} SET ";

            foreach (KeyValuePair<string, Tuple<SqlColumnInfo, PropertyInfo>> kvp in columns)
            {
                // we do not update auto incrementing columns
                if (kvp.Value.Item1.AutoIncrement) continue;

                SqlColumnAttribute sca = kvp.Value.Item2.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? kvp.Value.Item1.Name : sca.ColumnName;

                query += $"{typeName}.{columnName} = tvp.{columnName},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);
            query += $" FROM {typeName} INNER JOIN @tvp AS tvp ON ";

            Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> primaryKeyColumns = columns.Where(c => c.Value.Item1.IsPrimaryKey).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (KeyValuePair<string, Tuple<SqlColumnInfo, PropertyInfo>> kvp in primaryKeyColumns)
            {
                SqlColumnAttribute sca = kvp.Value.Item2.GetCustomAttribute<SqlColumnAttribute>();

                string columnName = sca == null ? kvp.Value.Item1.Name : sca.ColumnName;

                query += $"{typeName}.{columnName} = tvp.{columnName} AND ";
            }

            // remove the last ' AND '
            query = query.Substring(0, query.Length - 5);

            return query;
        }

        private int GetColumnsAndCreateTvp<T>(List<T> instances, string createSchema, string typeSchema, bool includeAutoIncrementColumns, out Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columns)
        {
            columns = new Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>>();

            List<SqlColumnInfo> tempColumns = new List<SqlColumnInfo>();

            if (instances == null)
            {
                ErrorLogAction("The collection of instances to delete cannot be null.");

                return -1;
            }

            if (sqlConnection.State != ConnectionState.Open)
            {
                ErrorLogAction("There must be an open and not busy connection waiting. Please connect first by calling Connect.");

                return -1;
            }

            Type type = typeof(T);
            PropertyInfo[] publicProperties = type.GetProperties();
            List<PropertyInfo> publicPropertiesWithAttributes = publicProperties.Where(pi => pi.GetCustomAttribute<SqlColumnAttribute>() != null).ToList();

            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? typeSchema : string.IsNullOrWhiteSpace(sta.Schema) ? typeSchema : sta.Schema;

            // get column information
            try
            {
                tempColumns = SqlInformationService.GetTableInformation(sqlConnection, typeSchema, typeName, ErrorLogAction);
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred attempting to get basic column information: {ex.Message}");

                return -1;
            }

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

                columns.Add(col.Name, new Tuple<SqlColumnInfo, PropertyInfo>(col, matchingPropertyInfo));
            }

            schema = sta == null ? createSchema : string.IsNullOrWhiteSpace(sta.Schema) ? createSchema : sta.Schema;

            try
            {
                CreateTvpType(type, schema, columns, includeAutoIncrementColumns);
            }
            catch (Exception ex)
            {
                ErrorLogAction($"An error occurred attempting to create TVP type: {ex.Message}");

                return -1;
            }

            return 0;
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

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Delete<T>(List<T> instances)
        {
            return Delete(instances, "dbo", "dbo");
        }

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data deleted from it.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Delete<T>(List<T> instances, string typeSchema, string createSchema)
        {
            VerifyDisposed();

            int returnValue;
            SqlTransaction transaction = null;

            try
            {
                transaction = sqlConnection.BeginTransaction();

                returnValue = Delete<T>(instances, transaction, typeSchema, createSchema);

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
        /// <param name="typeSchema">The schema which owns the table that is having data deleted from it.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Delete<T>(List<T> instances, SqlTransaction transaction, string typeSchema, string createSchema)
        {
            VerifyDisposed();

            Type type = typeof(T);

            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? typeSchema : string.IsNullOrWhiteSpace(sta.Schema) ? typeSchema : sta.Schema;
            string schema2 = sta == null ? createSchema : string.IsNullOrWhiteSpace(sta.Schema) ? createSchema : sta.Schema;

            Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columns;

            int returnValue = GetColumnsAndCreateTvp(instances, schema2, schema, true, out columns);

            if (returnValue == -1) return returnValue;

            try
            {
                string query = $"DELETE FROM {schema}.{typeName} WHERE EXISTS (SELECT tvp.* FROM @tvp AS tvp)";
                DataTable dt = GenerateTvpDataTable(instances, columns, true);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{schema2}.{typeName}AsTvp",
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

            if (!DropTvpType(type, schema2))
                returnValue = -1;

            return returnValue;
        }

        /// <summary>Disconnects from the database.</summary>
        public void Disconnect()
        {
            VerifyDisposed();

            sqlConnection.Close();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged and managed resources.</summary>
        public void Dispose()
        {
            VerifyDisposed();

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public int ExecuteNonQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
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

        public int ExecuteNonQuery(SqlTransaction transaction, string query, CommandType commandType, params SqlParameter[] parameters)
        {
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

        public object[][] ExecuteQuery(string query, CommandType commandType, params SqlParameter[] parameters)
        {
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

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Insert<T>(List<T> instances)
        {
            return Insert(instances, "dbo", "dbo");
        }

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data inserted into.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Insert<T>(List<T> instances, string typeSchema, string createSchema)
        {
            VerifyDisposed();

            int returnValue;
            SqlTransaction transaction = null;

            try
            {
                transaction = sqlConnection.BeginTransaction();

                returnValue = Insert<T>(instances, transaction, typeSchema, createSchema);

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
        /// <param name="typeSchema">The schema which owns the table that is having data inserted into.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Insert<T>(List<T> instances, SqlTransaction transaction, string typeSchema, string createSchema)
        {
            VerifyDisposed();

            Type type = typeof(T);

            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? typeSchema : string.IsNullOrWhiteSpace(sta.Schema) ? typeSchema : sta.Schema;
            string schema2 = sta == null ? createSchema : string.IsNullOrWhiteSpace(sta.Schema) ? createSchema : sta.Schema;

            Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columns;

            int returnValue = GetColumnsAndCreateTvp(instances, schema2, schema, false, out columns);

            if (returnValue == -1) return returnValue;

            try
            {
                string query = GenerateInsertTvpQuery(type, schema, columns);
                DataTable dt = GenerateTvpDataTable(instances, columns, false);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{schema2}.{typeName}AsTvp",
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

            if (!DropTvpType(type, schema2))
                returnValue = -1;

            return returnValue;
        }

        /// <summary>Returns all the objects of type T from the database.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <returns>A list of all instances of type T or null if an error occurred.</returns>
        public List<T> Select<T>()
            where T : class, new()
        {
            Type type = typeof(T);

            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;

            string query = $"SELECT * FROM {typeName}";

            return Select<T>(query, CommandType.Text);
        }

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
            return Update(instances, "dbo", "dbo");
        }

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <param name="typeSchema">The schema which owns the table that is having data updated.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Update<T>(List<T> instances, string typeSchema, string createSchema)
        {
            VerifyDisposed();

            int returnValue = -1;
            SqlTransaction transaction = null;

            try
            {
                transaction = sqlConnection.BeginTransaction();

                returnValue = Update<T>(instances, transaction, typeSchema, createSchema);

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
        /// <param name="typeSchema">The schema which owns the table that is having data updated.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Update<T>(List<T> instances, SqlTransaction transaction, string typeSchema, string createSchema)
        {
            VerifyDisposed();

            Type type = typeof(T);

            SqlTableAttribute sta = type.GetCustomAttribute<SqlTableAttribute>();

            string typeName = sta == null ? type.Name : string.IsNullOrWhiteSpace(sta.TableName) ? type.Name : sta.TableName;
            string schema = sta == null ? typeSchema : string.IsNullOrWhiteSpace(sta.Schema) ? typeSchema : sta.Schema;
            string schema2 = sta == null ? createSchema : string.IsNullOrWhiteSpace(sta.Schema) ? createSchema : sta.Schema;

            Dictionary<string, Tuple<SqlColumnInfo, PropertyInfo>> columns;

            int returnValue = GetColumnsAndCreateTvp(instances, schema2, schema, true, out columns);

            if (returnValue == -1) return returnValue;

            try
            {
                string query = GenerateUpdateTvpQuery(type, schema, columns);
                DataTable dt = GenerateTvpDataTable(instances, columns, true);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{schema2}.{typeName}AsTvp",
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

            if (!DropTvpType(type, schema2))
                returnValue = -1;

            return returnValue;
        }

        private void VerifyDisposed([CallerMemberName] string caller = "")
        {
            if (disposedValue)
                throw new ObjectDisposedException("Solis.Database.Sql.ORM.SqlDatabase", $"{caller} cannot be accessed because the object instance has been disposed.");
        }
        #endregion
    }
}
