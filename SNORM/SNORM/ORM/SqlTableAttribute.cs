using System;

namespace SNORM.ORM
{
    /// <summary>Describes a SQL table name alias so that the type name does not have to be used in the ORM.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class SqlTableAttribute : Attribute
    {
        /// <summary>Gets the table name alias.</summary>
        public string TableName { get;}

        /// <summary>Initializes a new instance of <see cref="SqlTableAttribute"/>.</summary>
        /// <param name="tableName">The name of the table to reference in the database.</param>
        public SqlTableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }
}
