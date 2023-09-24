namespace SNORM
{
    /// <summary>Describes a foreign key.</summary>
    public class ForeignKey
    {
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the parent table schema.</summary>
        public string ParentTableSchema { get; set; }

        /// <summary>Gets or sets the parent table name.</summary>
        public string ParentTableName { get; set; }

        /// <summary>Gets or sets the parent column name.</summary>
        public string ParentColumnName { get; set; }

        /// <summary>Gets or sets the child table schema.</summary>
        public string ChildTableSchema { get; set; }

        /// <summary>Gets or sets the child table name.</summary>
        public string ChildTableName { get; set; }

        /// <summary>Gets or sets the column name.</summary>
        public string ChildColumnName { get; set; }
    }
}
