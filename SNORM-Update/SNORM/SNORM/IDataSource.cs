using System;
using System.Collections.Generic;

namespace SNORM
{
    /// <summary>Describes an object that generically can manage data.</summary>
    public interface IDataSource : IDisposable
    {
        #region Methods

        /// <summary>Deletes the objects from the database.</summary>
        /// <typeparam name="T">The type of object to delete.</typeparam>
        /// <param name="instances">The collection of objects to delete.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Delete<T>(List<T> instances);

        /// <summary>Inserts the objects into the database.</summary>
        /// <typeparam name="T">The type of object to insert.</typeparam>
        /// <param name="instances">The collection of objects to insert.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Insert<T>(List<T> instances);

        /// <summary>Returns all the objects of type T from the database.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <returns>A list of all instances of type T or null if an error occurred.</returns>
        List<T> Select<T>() where T : class, new();

        /// <summary>Returns objects of type T that the query matches.</summary>
        /// <typeparam name="T">The type data to retrieve from the database.</typeparam>
        /// <param name="query">The query to execute to select records.</param>
        /// <returns>A list of instances that are returned from the query.</returns>
        List<T> Select<T>(string query) where T : class, new();

        /// <summary>Updates objects in the database.</summary>
        /// <typeparam name="T">The type of object to update.</typeparam>
        /// <param name="instances">The collection of objects to update.</param>
        /// <returns>The number of affected rows or -1 if an error occurred.</returns>
        int Update<T>(List<T> instances);

        #endregion
    }
}
