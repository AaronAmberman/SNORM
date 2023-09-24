namespace SNORM
{
    /// <summary>Describes a data source object that requires a connection.</summary>
    public interface IConnectableDataSource : IDataSource
    {
        #region Methods

        /// <summary>Attempts to connect to the data source.</summary>
        /// <returns>True if successfully connected otherwise false.</returns>
        bool Connect();

        /// <summary>Disconnects from the database.</summary>
        void Disconnect();

        #endregion
    }
}
