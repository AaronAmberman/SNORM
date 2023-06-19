using System;

namespace SNORM.ORM
{
    /// <summary>Describes a SQL table name alias so that the type name does not have to be used in the ORM.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class SqlTableAttribute : Attribute
    {
        /// <summary>Gets the schema for the table. If not provided dbo will be used as the default.</summary>
        public string Schema { get; }

        /// <summary>Gets the table name alias.</summary>
        public string TableName { get; }

        /// <summary>Initializes a new instance of <see cref="SqlTableAttribute"/>.</summary>
        /// <param name="schema">The schema id for the table.</param>
        /// <param name="tableName">The name of the table to reference in the database.</param>
        public SqlTableAttribute(string schema, string tableName)
        {
            Schema = schema;
            TableName = tableName;
        }
    }
}
