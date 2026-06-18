using Microsoft.Extensions.Caching.Memory;

namespace Google_Like_Blazor.Services;

/// <summary>
/// MemoryCache-based caching facade wrapping <see cref="IFileRepo"/> calls.
/// Uses consistent 5-minute TTL across all cache entries.
/// </summary>
public class RepositoryCache
{
    private readonly IFileRepo _fileRepo;
    private readonly MemoryCache _memoryCache;

    // Consistent TTL across all cache entries
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public RepositoryCache(IFileRepo fileRepo, MemoryCacheConfig memoryCache)
    {
        _fileRepo = fileRepo;
        _memoryCache = memoryCache.Cache;
    }

    public async Task<int> GetFilesCount()
    {
        const string cacheKey = "files:count";
        if (!_memoryCache.TryGetValue(cacheKey, out int cacheResult))
        {
            cacheResult = (await _fileRepo.SearchInFileName("pdf")).Count();
            _memoryCache.Set(cacheKey, cacheResult,
                new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(CacheTtl)
                    .SetSize(1));
        }
        return cacheResult;
    }

    public async Task<List<FileViewModel>> GetFiles(string cacheKey)
    {
        if (!_memoryCache.TryGetValue(cacheKey, out List<FileViewModel>? cacheResult))
        {
            cacheResult = await _fileRepo.SearchInContent(cacheKey);
            _memoryCache.Set(cacheKey, cacheResult,
                new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(CacheTtl)
                    .SetSize(1));
        }
        return cacheResult!;
    }
}
