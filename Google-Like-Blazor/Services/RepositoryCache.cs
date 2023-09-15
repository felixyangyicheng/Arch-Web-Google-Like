using System;


namespace Google_Like_Blazor.Services
{
	public class RepositoryCache
	{
        private readonly IFileRepo _fileRepo;
        private readonly MemoryCache _memoryCache;
        private readonly MyRedisCache _redisCache;
        public RepositoryCache(IFileRepo fileRepo, MemoryCacheConfig memoryCache, MyRedisCache redisCache)
        {
            _fileRepo = fileRepo;
            _memoryCache = memoryCache.Cache;
            _redisCache = redisCache;
        }


        public async Task<int> GetFilesCount()
        {
            const string cacheKey = "pdf";
            if (!_memoryCache.TryGetValue(cacheKey, out int cacheResult))
            {

                cacheResult =(await _fileRepo.SearchInFileName(cacheKey)).Count();


                var cacheEntryOptions = new MemoryCacheEntryOptions()
                {

                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                }
             
                    .SetSize(1);

                _memoryCache.Set(cacheKey, cacheResult, cacheEntryOptions);
            }
            return cacheResult;
        }

        public async Task<List<FileViewModel>> GetFiles(string cacheKey)
        {
           
            if (!_memoryCache.TryGetValue(cacheKey, out List<FileViewModel> cacheResult))
            {
                // cacheKey不存在於快取,重取資料
                cacheResult = (await _fileRepo.SearchInContent(cacheKey));

                // 設定快取的使用量和到期時間
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                {
                    // 撐 (60秒一到自動清除)
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(6)
                }
                    // 使用1個單位
                    .SetSize(1);
              
                _memoryCache.Set(cacheKey, cacheResult, cacheEntryOptions);
            }
            return cacheResult;
        }
    }
}

