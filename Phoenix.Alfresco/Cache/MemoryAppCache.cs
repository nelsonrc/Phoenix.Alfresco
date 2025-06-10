using Microsoft.Extensions.Caching.Memory;

namespace Phoenix.Alfresco;
public class MemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache _cache;
    public TimeSpan DefaultTtl { get; }

    public MemoryCacheProvider(IMemoryCache memoryCache, TimeSpan? defaultTtl = null)
    {
        _cache = memoryCache;
        DefaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? cached))
            return cached!;

        var value = await factory();
        _cache.Set(key, value, DefaultTtl);
        return value;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public T? Get<T>(string key)
    {
        return _cache.TryGetValue(key, out var raw) && raw is T typed ? typed : default;
    }

    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        _cache.Set(key, value, ttl ?? DefaultTtl);
    }

    public void Remove(string key) => _cache.Remove(key);

    public void Clear() =>
        throw new NotSupportedException("MemoryCache does not support clearing all entries directly.");
}
