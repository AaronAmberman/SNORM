using System.Collections.Generic;

namespace SNORM
{
    /// <summary>Helps retrieve information about tables from the database.</summary>
    public static class SqlInformationService
    {
        /// <summary>Gets the column information for the table.</summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="tableName">The table name to get column information about.</param>
        /// <returns>A list of columns contain metadata about each column.</returns>
        public static List<SqlColumn> GetTableInformation(string connectionString, string tableName)
        {
            List<SqlColumn> columns = new List<SqlColumn>();



            return columns;
        }
    }
}
