using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace SNORM
{
    /// <summary>Helps retrieve information about tables from the database.</summary>
    public static class SqlInformationService
    {
        private static Dictionary<string, List<SqlColumnInfo>> tableColumns = new Dictionary<string, List<SqlColumnInfo>>();

        private static bool GetAutoIncrementStatus(SqlConnection connection, string tableName, Action<string> log, List<SqlColumnInfo> columns)
        {
            // next get the auto increment step (increment in database) and seed
            string query = "SELECT [column].name, [column].is_identity, ident_incr([table].name) as identity_increment, ident_seed([table].name) as identity_seed " +
                           "FROM sys.tables[table] " +
                           "INNER JOIN sys.columns[column] ON[table].object_id = [column].object_id " +
                           "WHERE[table].name = @tableName AND [column].is_identity = 1";

            SqlParameter tableNameParameter = new SqlParameter("@tableName", tableName);

            object[][] results = SimpleSqlService.ExecuteQuery(connection, false, log, query, CommandType.Text, tableNameParameter);

            if (results == null)
            {
                log("Unable get auto increment (identity) information from the table. Failure.");

                return false;
            }

            foreach (object[] row in results)
            {
                SqlColumnInfo matchingColumn = columns.FirstOrDefault(col => col.Name.Equals(row[0].ToString(), StringComparison.OrdinalIgnoreCase));

                if (matchingColumn != null)
                {
                    matchingColumn.AutoIncrement = true;
                    matchingColumn.AutoIncrementStep = int.Parse(row[2].ToString(), CultureInfo.CurrentCulture);
                    matchingColumn.AutoIncrementSeed = int.Parse(row[3].ToString(), CultureInfo.CurrentCulture);
                }
            }

            return true;
        }

        private static List<SqlColumnInfo> GetBasicColumnInformation(SqlConnection connection, string tableName, Action<string> log)
        {
            List<SqlColumnInfo> columns = new List<SqlColumnInfo>();

            string query = "SELECT COLUMN_NAME, ORDINAL_POSITION, COLUMN_DEFAULT, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName";

            SqlParameter tableNameParameter = new SqlParameter("@tableName", tableName);

            object[][] results = SimpleSqlService.ExecuteQuery(connection, false, log, query, CommandType.Text, tableNameParameter);

            if (results == null)
            {
                log("Unable get basic column information from the table. Failure.");

                return null;
            }

            foreach (object[] row in results)
            {
                SqlColumnInfo column = new SqlColumnInfo
                {
                    Name = row[0].ToString(),
                    OrdinalPosition = int.Parse(row[1].ToString(), CultureInfo.CurrentCulture),
                    DefaultValue = row[2],
                    IsNullable = row[3].ToString().Equals("yes", StringComparison.OrdinalIgnoreCase) ? true : false,
                    Type = GetSqlDbTypeFromString(row[4].ToString())
                };

                // only if we have a length do we want to set one otherwise leave it -1
                if (row[5] != null)
                {
                    column.Length = int.Parse(row[5].ToString(), CultureInfo.CurrentCulture);
                }

                columns.Add(column);
            }

            return columns;
        }

        private static List<ForeignKey> GetForeignKeysForTable(SqlConnection connection, string tableSchema, string tableName, Action<string> log)
        {
            string query = "SELECT KCU1.CONSTRAINT_SCHEMA AS FK_CONSTRAINT_SCHEMA, " +
                                  "KCU1.CONSTRAINT_NAME AS FK_CONSTRAINT_NAME, " +
                                  "KCU1.TABLE_SCHEMA AS FK_TABLE_SCHEMA, " +
                                  "KCU1.TABLE_NAME AS FK_TABLE_NAME, " +
                                  "KCU1.COLUMN_NAME AS FK_COLUMN_NAME, " +
                                  "KCU1.ORDINAL_POSITION AS FK_ORDINAL_POSITION, " +
                                  "KCU2.CONSTRAINT_SCHEMA AS REFERENCED_CONSTRAINT_SCHEMA, " +
                                  "KCU2.CONSTRAINT_NAME AS REFERENCED_CONSTRAINT_NAME, " +
                                  "KCU2.TABLE_SCHEMA AS REFERENCED_TABLE_SCHEMA, " +
                                  "KCU2.TABLE_NAME AS REFERENCED_TABLE_NAME, " +
                                  "KCU2.COLUMN_NAME AS REFERENCED_COLUMN_NAME, " +
                                  "KCU2.ORDINAL_POSITION AS REFERENCED_ORDINAL_POSITION " +
                           "FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS RC " +
                           "INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS KCU1 " +
                                  "ON KCU1.CONSTRAINT_CATALOG = RC.CONSTRAINT_CATALOG " +
                                  "AND KCU1.CONSTRAINT_SCHEMA = RC.CONSTRAINT_SCHEMA " +
                                  "AND KCU1.CONSTRAINT_NAME = RC.CONSTRAINT_NAME " +
                           "INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS KCU2 " +
                                  "ON KCU2.CONSTRAINT_CATALOG = RC.UNIQUE_CONSTRAINT_CATALOG " +
                                  "AND KCU2.CONSTRAINT_SCHEMA = RC.UNIQUE_CONSTRAINT_SCHEMA " +
                                  "AND KCU2.CONSTRAINT_NAME = RC.UNIQUE_CONSTRAINT_NAME " +
                                  "AND KCU2.ORDINAL_POSITION = KCU1.ORDINAL_POSITION " +
                           "WHERE (KCU1.TABLE_SCHEMA = @tableSchema AND KCU1.TABLE_NAME = @tableName) " +
                                 "OR (KCU2.TABLE_SCHEMA = @tableSchema AND KCU2.TABLE_NAME = @tableName)" +
                                 "AND RC.CONSTRAINT_CATALOG = @databaseName";

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connection.ConnectionString);

            SqlParameter databaseNameParameter = new SqlParameter("@databaseName", builder.InitialCatalog);
            SqlParameter tableSchemaParameter = new SqlParameter("@tableSchema", tableSchema);
            SqlParameter tableNameParameter = new SqlParameter("@tableName", tableName);

            object[][] results = SimpleSqlService.ExecuteQuery(connection, false, log, query, CommandType.Text, databaseNameParameter, tableSchemaParameter, tableNameParameter);

            if (results == null)
            {
                log($"Unable to retrieve the foreign key information for the table {tableSchema}.{tableName}");

                return null;
            }

            List<ForeignKey> foreignKeys = new List<ForeignKey>();

            foreach (object[] row in results)
            {
                ForeignKey fk = new ForeignKey
                {
                    Name = row[1].ToString(),
                    ChildTableSchema = row[2].ToString(),
                    ChildTableName = row[3].ToString(),
                    ChildColumnName = row[4].ToString(),
                    ParentTableSchema = row[8].ToString(),
                    ParentTableName = row[9].ToString(),
                    ParentColumnName = row[10].ToString()
                };

                foreignKeys.Add(fk);
            }

            return foreignKeys;
        }

        private static bool GetPrimaryKeyStatus(SqlConnection connection, string tableSchema, string tableName, Action<string> log, List<SqlColumnInfo> columns)
        {
            // next get if column is primary key (or part of multi-column primary key)
            string query = "SELECT kcu.COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu " +
                    "INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kcu.CONSTRAINT_CATALOG = tc.CONSTRAINT_CATALOG AND kcu.TABLE_SCHEMA = tc.TABLE_SCHEMA AND kcu.TABLE_NAME = tc.TABLE_NAME " +
                    "WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_SCHEMA = @tableSchema AND tc.TABLE_NAME = @tableName";

            SqlParameter tableNameParameter = new SqlParameter("@tableName", tableName);
            SqlParameter tableSchemaParameter = new SqlParameter("@tableSchema", tableSchema);

            object[][] results = SimpleSqlService.ExecuteQuery(connection, false, log, query, CommandType.Text, tableSchemaParameter, tableNameParameter);

            if (results == null)
            {
                log("Unable get primary key information from the table. Failure.");

                return false;
            }

            foreach (object[] row in results)
            {
                SqlColumnInfo matchingColumn = columns.FirstOrDefault(col => col.Name.Equals(row[0].ToString(), StringComparison.OrdinalIgnoreCase));

                if (matchingColumn != null)
                {
                    matchingColumn.IsPrimaryKey = true;
                }
            }

            return true;
        }

        // list of type names from: https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings
        private static SqlDbType GetSqlDbTypeFromString(string value)
        {
            if (value.Equals("bigint", StringComparison.OrdinalIgnoreCase)) return SqlDbType.BigInt;
            else if (value.Equals("binary", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Binary;
            else if (value.Equals("bit", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Bit;
            else if (value.Equals("char", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Char;
            else if (value.Equals("date", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Date;
            else if (value.Equals("datetime", StringComparison.OrdinalIgnoreCase)) return SqlDbType.DateTime;
            else if (value.Equals("datetime2", StringComparison.OrdinalIgnoreCase)) return SqlDbType.DateTime2;
            else if (value.Equals("datetimeoffset", StringComparison.OrdinalIgnoreCase)) return SqlDbType.DateTimeOffset;
            else if (value.Equals("decimal", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Decimal;
            else if (value.Equals("float", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Float;
            else if (value.Equals("image", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Image;
            else if (value.Equals("int", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Int;
            else if (value.Equals("money", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Money;
            else if (value.Equals("nchar", StringComparison.OrdinalIgnoreCase)) return SqlDbType.NChar;
            else if (value.Equals("ntext", StringComparison.OrdinalIgnoreCase)) return SqlDbType.NText;
            else if (value.Equals("nvarchar", StringComparison.OrdinalIgnoreCase)) return SqlDbType.NVarChar;
            else if (value.Equals("real", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Real;
            else if (value.Equals("smalldatetime", StringComparison.OrdinalIgnoreCase)) return SqlDbType.SmallDateTime;
            else if (value.Equals("smallint", StringComparison.OrdinalIgnoreCase)) return SqlDbType.SmallInt;
            else if (value.Equals("smallmoney", StringComparison.OrdinalIgnoreCase)) return SqlDbType.SmallMoney;
            else if (value.Equals("text", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Text;
            else if (value.Equals("time", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Time;
            else if (value.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Timestamp;
            else if (value.Equals("tinyint", StringComparison.OrdinalIgnoreCase)) return SqlDbType.TinyInt;
            else if (value.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase)) return SqlDbType.UniqueIdentifier;
            else if (value.Equals("varbinary", StringComparison.OrdinalIgnoreCase)) return SqlDbType.VarBinary;
            else if (value.Equals("varchar", StringComparison.OrdinalIgnoreCase)) return SqlDbType.VarChar;
            else if (value.Equals("xml", StringComparison.OrdinalIgnoreCase)) return SqlDbType.Xml;
            else throw new NotSupportedException("The value could parsed into SqlDbType.");
        }

        /// <summary>Gets the column information for the table.</summary>
        /// <param name="connection">The connection to the database.</param>
        /// <param name="tableSchema">The schema that owns the table.</param>
        /// <param name="tableName">The table name to get column information about.</param>
        /// <param name="log">The method to call to write log messages to (only errors are logged).</param>
        /// <returns>A list of columns containing metadata about each column or null if an error occurred.</returns>
        public static List<SqlColumnInfo> GetTableInformation(SqlConnection connection, string tableSchema, string tableName, Action<string> log)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connection.ConnectionString);

            // we'll check to see if our dictionary contains our collection of columns already
            if (tableColumns.ContainsKey($"{builder.InitialCatalog}.{tableSchema}.{tableName}"))
            {
                return tableColumns[$"{builder.InitialCatalog}.{tableSchema}.{tableName}"];
            }

            try
            {
                List<SqlColumnInfo> columns = GetBasicColumnInformation(connection, tableName, log);
                        
                if (!GetAutoIncrementStatus(connection, tableName, log, columns))
                {
                    return null;
                }

                if (!GetPrimaryKeyStatus(connection, tableSchema, tableName, log, columns))
                {
                    return null;
                }

                // finally get foreign key information about the column
                List<ForeignKey> foreignKeys = GetForeignKeysForTable(connection, tableSchema, tableName, log);

                if (foreignKeys != null)
                {
                    // first check child name for match
                    List<ForeignKey> childForeignKeys = foreignKeys.Where(fk => fk.ChildTableSchema == tableSchema && fk.ChildTableName == tableName).ToList();

                    foreach (ForeignKey childFk in childForeignKeys)
                    {
                        SqlColumnInfo matchingColumn = columns.FirstOrDefault(col => col.Name.Equals(childFk.ChildColumnName, StringComparison.OrdinalIgnoreCase));
                        matchingColumn?.ForeignKeys.Add(childFk);
                    }

                    // second check parent name for match
                    List<ForeignKey> parentForeignKeys = foreignKeys.Where(fk => fk.ParentTableSchema == tableSchema && fk.ParentTableName == tableName).ToList();

                    foreach (ForeignKey parentFk in parentForeignKeys)
                    {
                        SqlColumnInfo matchingColumn = columns.FirstOrDefault(col => col.Name.Equals(parentFk.ParentColumnName, StringComparison.OrdinalIgnoreCase));
                        matchingColumn?.ForeignKeys.Add(parentFk);
                    }
                }

                tableColumns.Add($"{builder.InitialCatalog}.{tableSchema}.{tableName}", columns);

                return columns;
            }
            catch(Exception ex)
            {
                log($"An error occurred during GetTableInformation...{Environment.NewLine}{ex}");

                return null;
            }
        }
    }
}
