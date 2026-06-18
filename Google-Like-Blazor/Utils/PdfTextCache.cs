using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Google_Like_Blazor.Utils;

/// <summary>
/// Caches extracted PDF text per file ID to avoid re-parsing PDFs on every search.
/// Uses <see cref="MemoryCache"/> with sliding expiration so recently-accessed
/// documents stay hot while inactive ones are evicted.
/// </summary>
/// <remarks>
/// ⚡ This is the single biggest performance win for search: PdfPig parsing
/// is CPU-intensive and I/O-heavy. A cache hit skips it entirely.
/// </remarks>
public class PdfTextCache
{
    private readonly MemoryCache _cache;
    private static readonly MemoryCacheEntryOptions DefaultOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        Size = 1 // each entry counts as 1 against SizeLimit
    };

    public PdfTextCache(IOptions<PdfTextCacheOptions>? options = null)
    {
        var sizeLimit = options?.Value.SizeLimit ?? 200;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
    }

    /// <summary>Try to get cached text pages for a file.</summary>
    public bool TryGet(string fileId, out string[] pages)
    {
        if (_cache.TryGetValue(fileId, out object? cached) && cached is string[] arr)
        {
            pages = arr;
            return true;
        }
        pages = Array.Empty<string>();
        return false;
    }

    /// <summary>Store extracted pages for a file.</summary>
    public void Set(string fileId, string[] pages)
    {
        _cache.Set(fileId, pages, DefaultOptions);
    }

    /// <summary>Invalidate cached text for a file (e.g. after update).</summary>
    public void Remove(string fileId) => _cache.Remove(fileId);

    /// <summary>Total entries currently cached.</summary>
    public int Count => ((ConcurrentDictionary<object, object?>)
        typeof(MemoryCache).GetField("_entries",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
        .GetValue(_cache) ?? new ConcurrentDictionary<object, object?>()).Count;
}

/// <summary>Configuration for <see cref="PdfTextCache"/>.</summary>
public class PdfTextCacheOptions
{
    /// <summary>Maximum number of documents to cache (default 200).</summary>
    public int SizeLimit { get; set; } = 200;
}
