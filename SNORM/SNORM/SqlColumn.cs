using System.Data;

namespace SNORM
{
    /// <summary>Represnts a column in a SQL table.</summary>
    public class SqlColumn
    {
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the type.</summary>
        public SqlDbType Type { get; set; }

        /// <summary>Gets or sets the length. Default is -1 as not all data types utilize a length.</summary>
        public int Length { get; set; } = -1;

        /// <summary>Gets or sets the default value.</summary>
        public object DefaultValue { get; set; }

        /// <summary>Gets or sets whether or not the column is auto incremented.</summary>
        public bool AutoIncrement { get; set; }

        /// <summary>Gets or sets the auto increment step.</summary>
        public int AutoIncrementStep { get; set; }

        /// <summary>Gets or sets whether or not the column is a primary key (or part of a multi-column primary key).</summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>Gets or sets the foreign key information, if any.</summary>
        public ForeignKey ForeignKey { get; set; }
    }
}
