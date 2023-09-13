
namespace Google_Like_Blazor.Services
{
    public interface IMongoReposBase<T> where T : class
    {
        Task<List<T>> GetAsync();
        Task<List<T>> SearchByNameAsync(string name);
        Task<T?> GetAsync(string id);
        Task<bool> CreateAsync(T obj);
        Task<bool> UpdateAsync(string id, T obj);
        Task RemoveAsync(string id);

    }
}
