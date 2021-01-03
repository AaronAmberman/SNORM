using System;

namespace SNORM.ORM
{
    /// <summary>Describes a SQL column name alias so that the property name does not have to be used in the ORM.</summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class SqlColumnAttribute : Attribute
    {
        /// <summary>Gets the column name alias.</summary>
        public string ColumnName { get; }

        /// <summary>Initializes a new instance of <see cref="SqlColumnAttribute"/>.</summary>
        /// <param name="columnName">The name of the column to reference in the table.</param>
        public SqlColumnAttribute(string columnName)
        {
            ColumnName = columnName;
        }
    }
}
