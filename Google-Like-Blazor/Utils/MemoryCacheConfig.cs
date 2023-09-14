using System;
using Microsoft.Extensions.Caching.Memory;

namespace Google_Like_Blazor.Utils
{
	public class MemoryCacheConfig
	{
        public MemoryCache Cache { get; set; }
        public MemoryCacheConfig()
        {
            Cache = new MemoryCache(new MemoryCacheOptions
            {
                //  utiliser 2 unités caches
                SizeLimit = 2
            });
        }
    }
}

