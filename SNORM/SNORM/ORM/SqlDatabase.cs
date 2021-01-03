using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace SNORM.ORM
{
    /// <summary>Represents a SQL database. This class cannot be inherited.</summary>
    public sealed class SqlDatabase : IDisposable
    {
        #region Fields

        private SqlConnection sqlConnection;
        private bool disposedValue;

        #endregion

        #region Properties

        /// <summary>Gets or sets the connection string.</summary>
        public string ConnectionString => sqlConnection.ConnectionString;

        /// <summary>Gets or sets the method to call for logging (only errors are logged).</summary>
        public Action<string> Log { get; set; }

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

            Log = DefaultLogging;
        }

        #endregion

        #region Methods

        private void CreateTvpType(Type type, string createSchema, List<SqlColumn> columns, bool includeAutoIncrementColumns)
        {
            string query = $"CREATE TYPE {createSchema}.{type.Name}AsTvp AS TABLE (";

            foreach (SqlColumn col in columns)
            {
                if (!includeAutoIncrementColumns && col.AutoIncrement)
                    continue;

                query += $"{col.Name} {col.Type}";

                // if our column is of a data type that requires a length specification then we need to specifiy the length
                if (col.Type == SqlDbType.Char || col.Type == SqlDbType.VarChar || col.Type == SqlDbType.Text || col.Type == SqlDbType.NText ||
                    col.Type == SqlDbType.NChar || col.Type == SqlDbType.NVarChar || col.Type == SqlDbType.Binary || col.Type == SqlDbType.VarBinary ||
                    col.Type == SqlDbType.Image)
                {
                    query += $" ({col.Length})";
                }

                query += ",";
            }

            query = query.Substring(0, query.Length - 1); // remove the last comma
            query += ")";

            SqlCommand command = new SqlCommand(query, sqlConnection);

            command.ExecuteNonQuery();
            command.Dispose();
        }

        private void DefaultLogging(string entry)
        {
            Debug.WriteLine(entry);
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
                string query = $"DROP TYPE {createSchema}.{type.Name}AsTvp";

                SqlCommand command = new SqlCommand(query, sqlConnection);

                command.ExecuteNonQuery();
                command.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                Log($"An error occurred attempting to drop TVP type...{Environment.NewLine}{ex}");

                return false;
            }
        }

        private DataTable GenerateTvpDataTable<T>(List<T> instances, Dictionary<string, Tuple<SqlColumn, PropertyInfo>> columnAndPropertyMetadata, bool includeAutoIncrementColumns)
        {
            DataTable dt = new DataTable();

            foreach (KeyValuePair<string, Tuple<SqlColumn, PropertyInfo>> kvp in columnAndPropertyMetadata)
            {
                if (!includeAutoIncrementColumns && kvp.Value.Item1.AutoIncrement) continue;

                dt.Columns.Add(kvp.Key, kvp.Value.Item1.DotNetType);
            }

            foreach (T instance in instances)
            {
                // get values to add to this row
                List<object> values = new List<object>();

                foreach (KeyValuePair<string, Tuple<SqlColumn, PropertyInfo>> kvp in columnAndPropertyMetadata)
                {
                    // we don't need to insert data into auto incremented columns
                    if (!includeAutoIncrementColumns && kvp.Value.Item1.AutoIncrement) continue;

                    values.Add(kvp.Value.Item2.GetValue(instance));
                }

                dt.Rows.Add(values.ToArray());
            }

            return dt;
        }

        private string GenerateInsertTvpQuery(Type type, string typeSchema, Dictionary<string, Tuple<SqlColumn, PropertyInfo>> columns)
        {
            string query = $"INSERT INTO {typeSchema}.{type.Name} (";

            foreach (KeyValuePair<string, Tuple<SqlColumn, PropertyInfo>> kvp in columns)
            {
                // we don't need to insert data into auto incremented columns
                if (kvp.Value.Item1.AutoIncrement) continue;

                query += $"{kvp.Value.Item1.Name},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);

            query += ") SELECT ";

            foreach (KeyValuePair<string, Tuple<SqlColumn, PropertyInfo>> kvp in columns)
            {
                // we don't need to insert data into auto incremented columns
                if (kvp.Value.Item1.AutoIncrement) continue;

                query += $"tvp.{kvp.Value.Item1.Name},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);

            query += " FROM @TVP AS tvp";

            return query;
        }

        private string GenerateUpdateTvpQuery(Type type, string typeSchema, Dictionary<string, Tuple<SqlColumn, PropertyInfo>> columns)
        {
            string query = $"UPDATE {type.Name} SET ";

            foreach (KeyValuePair<string, Tuple<SqlColumn, PropertyInfo>> kvp in columns)
            {
                // we do not update auto incrementing columns
                if (kvp.Value.Item1.AutoIncrement) continue;

                query += $"{type.Name}.{kvp.Value.Item1.Name} = tvp.{kvp.Value.Item1.Name},";
            }

            // remove the last comma
            query = query.Substring(0, query.Length - 1);
            query += $" FROM {type.Name} INNER JOIN @tvp AS tvp ON ";

            Dictionary<string, Tuple<SqlColumn, PropertyInfo>> primaryKeyColumns = columns.Where(c => c.Value.Item1.IsPrimaryKey).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (KeyValuePair<string, Tuple<SqlColumn, PropertyInfo>> kvp in primaryKeyColumns)
            {
                query += $"{type.Name}.{kvp.Value.Item1.Name} = tvp.{kvp.Value.Item1.Name} AND ";
            }

            // remove the last ' AND '
            query = query.Substring(0, query.Length - 5);

            return query;
        }

        private int GetColumnsAndCreateTvp<T>(List<T> instances, string createSchema, string typeSchema, bool includeAutoIncrementColumns, out Dictionary<string, Tuple<SqlColumn, PropertyInfo>> columns)
        {
            columns = new Dictionary<string, Tuple<SqlColumn, PropertyInfo>>();

            List<SqlColumn> tempColumns = new List<SqlColumn>();

            if (instances == null)
            {
                Log("The collection of instances to delete cannot be null.");

                return -1;
            }

            if (sqlConnection.State != ConnectionState.Open)
            {
                Log("There must be an open and not busy connection waiting. Please connect first by calling Connect.");

                return -1;
            }

            Type type = typeof(T);
            PropertyInfo[] publicProperties = type.GetProperties();

            // get column information
            try
            {
                tempColumns = SqlInformationService.GetTableInformation(sqlConnection, typeSchema, type.Name, Log);
            }
            catch (Exception ex)
            {
                Log($"An error occurred attempting to get basic column information...{Environment.NewLine}{ex}");

                return -1;
            }

            foreach (SqlColumn col in tempColumns)
            {
                PropertyInfo temp = publicProperties.FirstOrDefault(pi => pi.Name.Equals(col.Name, StringComparison.OrdinalIgnoreCase));

                columns.Add(col.Name, new Tuple<SqlColumn, PropertyInfo>(col, temp));
            }

            // create the TVP out side of the transaction
            try
            {
                CreateTvpType(type, createSchema, tempColumns, includeAutoIncrementColumns);
            }
            catch (Exception ex)
            {
                Log($"An error occurred attempting to create TVP type...{Environment.NewLine}{ex}");

                return -1;
            }

            return 0;
        }

        /// <summary>Attempts to connect to the database.</summary>
        /// <returns>True if successfully connected or else false.</returns>
        public bool Connect()
        {
            try
            {
                sqlConnection.Open();

                return true;
            }
            catch (Exception ex)
            {
                Log($"An error occurred during Connect...{Environment.NewLine}{ex}");

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
            Type type = typeof(T);

            Dictionary<string, Tuple<SqlColumn, PropertyInfo>> columns;

            int returnValue = GetColumnsAndCreateTvp(instances, createSchema, typeSchema, true, out columns);

            if (returnValue == -1) return returnValue;

            SqlTransaction transaction = null;

            try
            {
                // begin our transaction so we can roll back if needed
                transaction = sqlConnection.BeginTransaction();

                string query = $"DELETE FROM {typeSchema}.{type.Name} WHERE EXISTS (SELECT tvp.* FROM @tvp AS tvp)";
                DataTable dt = GenerateTvpDataTable(instances, columns, true);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{createSchema}.{type.Name}AsTvp",
                    Value = dt
                });

                returnValue = command.ExecuteNonQuery();

                transaction?.Commit();

                command.Dispose();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                Log($"An error occurred during Delete...{Environment.NewLine}{ex}");

                returnValue = -1;
            }

            transaction?.Dispose();

            if (!DropTvpType(type, createSchema))
                returnValue = -1;

            return returnValue;
        }

        /// <summary>Disconnects from the database.</summary>
        public void Disconnect()
        {
            sqlConnection.Close();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged and managed resources.</summary>
        public void Dispose()
        {
            Dispose(disposing: true);

            GC.SuppressFinalize(this);
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
            Type type = typeof(T);

            Dictionary<string, Tuple<SqlColumn, PropertyInfo>> columns;

            int returnValue = GetColumnsAndCreateTvp(instances, createSchema, typeSchema, false, out columns);

            if (returnValue == -1) return returnValue;

            SqlTransaction transaction = null;

            try
            {
                // begin our transaction so we can roll back if needed
                transaction = sqlConnection.BeginTransaction();

                string query = GenerateInsertTvpQuery(type, typeSchema, columns);
                DataTable dt = GenerateTvpDataTable(instances, columns, false);                

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{createSchema}.{type.Name}AsTvp",
                    Value = dt
                });

                returnValue = command.ExecuteNonQuery();

                transaction?.Commit();

                command.Dispose();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                Log($"An error occurred during Insert...{Environment.NewLine}{ex}");
            }

            transaction?.Dispose();

            if (!DropTvpType(type, createSchema))
                returnValue = -1;

            return returnValue;
        }

        /// <summary>Returns all the objects of type T from the database.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <returns>A list of all instances of type T or null if an error occurred.</returns>
        public List<T> Select<T>()
            where T : class, new()
        {
            string query = $"SELECT * FROM {typeof(T).Name}";

            return Select<T>(query, CommandType.Text);
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
            if (string.IsNullOrEmpty(query))
            {
                Log("The query null, empty or consists only of white-space characters. Please set the query.");

                return null;
            }

            if (sqlConnection.State != ConnectionState.Open)
            {
                Log("There must be an open and not busy connection waiting. Please connect first by calling Connect.");

                return null;
            }

            try
            {
                List<T> returnValues = new List<T>();

                Type type = typeof(T);
                PropertyInfo[] publicProperties = type.GetProperties();

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
                            PropertyInfo matchingPropertyInfo = publicProperties.FirstOrDefault(pi => pi.Name.Equals(columns[i], StringComparison.OrdinalIgnoreCase));

                            object value = reader.GetValue(i);
                            value = value == DBNull.Value ? null : value;

                            matchingPropertyInfo?.SetValue(instance, value);
                        }

                        returnValues.Add(instance);
                    }

                    reader.Close();
                }

                return returnValues;
            }
            catch(Exception ex)
            {
                Log($"An error occurred during Select...{Environment.NewLine}{ex}");

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
            Type type = typeof(T);

            Dictionary<string, Tuple<SqlColumn, PropertyInfo>> columns;

            int returnValue = GetColumnsAndCreateTvp(instances, createSchema, typeSchema, true, out columns);

            if (returnValue == -1) return returnValue;

            SqlTransaction transaction = null;

            try
            {
                // begin our transaction so we can roll back if needed
                transaction = sqlConnection.BeginTransaction();

                string query = GenerateUpdateTvpQuery(type, typeSchema, columns);
                DataTable dt = GenerateTvpDataTable(instances, columns, true);

                // setup command and execute query
                SqlCommand command = new SqlCommand(query, sqlConnection, transaction);
                command.Parameters.Add(new SqlParameter("@TVP", SqlDbType.Structured)
                {
                    TypeName = $"{createSchema}.{type.Name}AsTvp",
                    Value = dt
                });

                returnValue = command.ExecuteNonQuery();

                transaction?.Commit();

                command.Dispose();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();

                Log($"An error occurred during Update...{Environment.NewLine}{ex}");
            }

            transaction?.Dispose();

            if (!DropTvpType(type, createSchema))
                returnValue = -1;

            return returnValue;
        }

        #endregion
    }
}
