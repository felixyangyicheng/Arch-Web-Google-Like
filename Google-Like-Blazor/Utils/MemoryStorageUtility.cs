namespace Google_Like_Blazor.Utils;

/// <summary>
/// Per-circuit in-memory cache with a bounded size.
/// When the size limit is hit, the oldest half of entries are evicted (simple FIFO eviction).
/// Prevents unbounded memory growth during long-lived SignalR circuits.
/// </summary>
public class MemoryStorageUtility
{
    private const int MaxEntries = 50;

    public Dictionary<string, object> Storage { get; set; } = new();

    /// <summary>Set a value, evicting old entries if over the limit.</summary>
    public void Set(string key, object value)
    {
        if (Storage.Count >= MaxEntries)
        {
            // Evict oldest half
            var keysToRemove = Storage.Keys.Take(MaxEntries / 2).ToList();
            foreach (var k in keysToRemove)
                Storage.Remove(k);
        }
        Storage[key] = value;
    }
}
