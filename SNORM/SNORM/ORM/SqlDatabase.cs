using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNORM.ORM
{
    /// <summary>Represents a SQL database. This class cannot be inherited.</summary>
    public sealed class SqlDatabase
    {
        #region Properties

        /// <summary>Gets or sets the connection string.</summary>
        public string ConnectionString { get; set; }

        #endregion

        #region Constructors

        /// <summary>Initializes a new instance of <see cref="SqlDatabase"/>.</summary>
        public SqlDatabase() { }

        /// <summary>Initializes a new instance of <see cref="SqlDatabase"/>.</summary>
        /// <param name="connectionString"></param>
        public SqlDatabase(string connectionString)
        {
            ConnectionString = connectionString;
        }

        #endregion

        #region Methods

        #endregion
    }
}