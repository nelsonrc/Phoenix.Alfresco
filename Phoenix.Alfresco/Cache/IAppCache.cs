namespace Phoenix.Alfresco;
public interface ICacheProvider
{
    TimeSpan DefaultTtl { get; }

    Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> factory,
        CancellationToken ct = default);

    bool TryGet<T>(string key, out T? value);

    T? Get<T>(string key);

    void Set<T>(string key, T value, TimeSpan? ttl = null);

    void Remove(string key);

    void Clear();
}
