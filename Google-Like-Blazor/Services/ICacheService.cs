using System;
namespace Google_Like_Blazor.Services
{
    /// <summary>
    /// Redis cache interface.
    /// </summary>
    /// <remarks>
    /// 🏷️ PLANNED — Redis caching is not yet integrated.
    /// The implementation <see cref="CacheService"/> is stubbed out.
    /// Currently the app uses <see cref="MemoryCache"/> via <see cref="RepositoryCache"/> for caching.
    /// </remarks>
    public interface ICacheService
    {
        /// <summary>
        /// Get Data using key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T GetData<T>(string key);

        /// <summary>
        /// Set Data with Value and Expiration Time of Key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expirationTime"></param>
        /// <returns></returns>
        bool SetData<T>(string key, T value, DateTimeOffset expirationTime);

        /// <summary>
        /// Remove Data
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        object RemoveData(string key);
    }
}

