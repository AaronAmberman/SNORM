using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SNORM.ORM
{
    /// <summary>Represents a SQL database. This class cannot be inherited.</summary>
    public sealed class SqlDatabase
    {
        #region Fields

        private SqlConnection sqlConnection;

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

        private void DefaultLogging(string entry)
        {
            Debug.WriteLine(entry);
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
                Log($"An error occurred attempting to run Connect...{Environment.NewLine}{ex}");

                return false;
            }
        }

        /// <summary>Disconnects from the database.</summary>
        public void Disconnect()
        {
            sqlConnection?.Close();
        }

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <param name="createSchema">This procedure creates types in the database and as such this allows control of which schema those objects are created in.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        public int Insert<T>(List<T> instances, string createSchema)
        {
            int returnValue = -1;

            if (instances == null)
            {
                Log("The collection of instances to insert cannot be null.");

                return returnValue;
            }

            try
            {
                // get type and property info
                Type type = typeof(T);
                PropertyInfo[] publicProperties = type.GetProperties();

                List<SqlColumn> columns = SqlInformationService.GetTableInformation(sqlConnection.ConnectionString, type.Name);

                // create TVP used to insert rows
                string query = $"CREATE TYPE {createSchema}.{type.Name}AsTvp AS TABLE (";

                foreach(SqlColumn col in columns)
                {
                    // if the column auto increments its own value then our TVP doesn't need to use it
                    if (col.AutoIncrement)
                        continue;

                    query += "";
                }

                query += ")";
            }
            catch (Exception ex)
            {
                Log($"An error occurred during Insert...{Environment.NewLine}{ex}");

                return -1;
            }

            return returnValue;
        }

        /// <summary>Returns all the objects of type T from the database.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <returns>A list of all instances of type T or null if an error occurred.</returns>
        public List<T> Select<T>()
            where T : class, new()
        {
            string query = "SELECT * FROM @table";
            SqlParameter tableName = new SqlParameter("@table", typeof(T).Name);

            return Select<T>(query, CommandType.Text, tableName);
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

            if (!query.StartsWith("select", StringComparison.OrdinalIgnoreCase))
            {
                Log("The query does not start with SELECT.");

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

        #endregion
    }
}
