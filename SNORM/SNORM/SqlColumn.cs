using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;

namespace SNORM
{
    /// <summary>Represnts a column in a SQL table.</summary>
    public class SqlColumn
    {
        #region Properties

        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the position of the column in the table (index).</summary>
        public int OrdinalPosition { get; set; }

        /// <summary>Gets or sets the default value.</summary>
        public object DefaultValue { get; set; }

        /// <summary>Gets or sets whether or not the column is nullable.</summary>
        public bool IsNullable { get; set; }

        /// <summary>Gets or sets the SQL type.</summary>
        public SqlDbType Type { get; set; }

        /// <summary>Gets or sets the length. Default is -1 as not all data types utilize a length.</summary>
        public int Length { get; set; } = -1;

        /// <summary>Gets or sets whether or not the column is auto incremented.</summary>
        public bool AutoIncrement { get; set; }

        /// <summary>Gets or sets the auto increment step.</summary>
        public int AutoIncrementStep { get; set; }

        /// <summary>Gets or sets the auto increment seep.</summary>
        public int AutoIncrementSeed { get; set; }

        /// <summary>Gets or sets whether or not the column is a primary key (or part of a multi-column primary key).</summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>Gets or sets the collection of foreign key information, if any or mulitple. Default is null.</summary>
        public List<ForeignKey> ForeignKeys { get; set; }

        /// <summary>Gets the .NET type based off the <see cref="Type"/>.</summary>
        public Type DotNetType
        {
            get
            {
                if (Type == SqlDbType.BigInt) return typeof(long);
                else if (Type == SqlDbType.Binary) return typeof(byte[]);
                else if (Type == SqlDbType.Bit) return typeof(bool);
                else if (Type == SqlDbType.Char) return typeof(string);
                else if (Type == SqlDbType.Date) return typeof(DateTime);
                else if (Type == SqlDbType.DateTime) return typeof(DateTime);
                else if (Type == SqlDbType.DateTime2) return typeof(DateTime);
                else if (Type == SqlDbType.DateTimeOffset) return typeof(DateTimeOffset);
                else if (Type == SqlDbType.Decimal) return typeof(decimal);
                else if (Type == SqlDbType.Float) return typeof(double);
                else if (Type == SqlDbType.Image) return typeof(byte[]);
                else if (Type == SqlDbType.Int) return typeof(int);
                else if (Type == SqlDbType.Money) return typeof(decimal);
                else if (Type == SqlDbType.NChar) return typeof(string);
                else if (Type == SqlDbType.NText) return typeof(string);
                else if (Type == SqlDbType.NVarChar) return typeof(string);
                else if (Type == SqlDbType.Real) return typeof(float);
                else if (Type == SqlDbType.SmallDateTime) return typeof(DateTime);
                else if (Type == SqlDbType.SmallInt) return typeof(short);
                else if (Type == SqlDbType.SmallMoney) return typeof(decimal);
                else if (Type == SqlDbType.Text) return typeof(string);
                else if (Type == SqlDbType.Time) return typeof(TimeSpan);
                else if (Type == SqlDbType.Timestamp) return typeof(byte[]);
                else if (Type == SqlDbType.TinyInt) return typeof(byte);
                else if (Type == SqlDbType.UniqueIdentifier) return typeof(Guid);
                else if (Type == SqlDbType.VarBinary) return typeof(byte[]);
                else if (Type == SqlDbType.VarChar) return typeof(string);
                else if (Type == SqlDbType.Xml) return typeof(SqlXml);
                else throw new NotSupportedException("The Type (SqlDbType) property could parsed into a .NET type.");
            }
        }

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance of <see cref="SqlColumn"/>.</summary>
        public SqlColumn()
        {
            ForeignKeys = new List<ForeignKey>();
        }

        #endregion
    }
}
