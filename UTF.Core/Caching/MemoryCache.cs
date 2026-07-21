using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Caching;

/// <summary>
/// 内存缓存实现 - 基于 ConcurrentDictionary，无锁读取，best-effort 容量驱逐
/// </summary>
public sealed class MemoryCache : ICache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inflightFactories = new();
    private readonly CacheConfiguration _configuration;
    private readonly Timer _cleanupTimer;
    private long _hits = 0;
    private long _misses = 0;
    private long _evictions = 0;
    private long _expirations = 0;
    private bool _disposed = false;

    public MemoryCache(CacheConfiguration? configuration = null)
    {
        _configuration = configuration ?? new CacheConfiguration();
        _cleanupTimer = new Timer(CleanupCallback, null, _configuration.CleanupInterval, _configuration.CleanupInterval);
    }

    /// <summary>获取缓存项（无锁读取，不在读取路径中执行 TryRemove）</summary>
    public TValue? Get<TValue>(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                if (_configuration.SlidingExpiration && entry.Expiration.HasValue)
                {
                    entry.UpdateAccess();
                }

                if (entry.Value is TValue value)
                {
                    Interlocked.Increment(ref _hits);
                    return value;
                }

                _cache.TryRemove(key, out _);
            }

            // 过期：best-effort 移除（不计入 miss，计入过期）
            if (_cache.TryRemove(key, out _))
            {
                Interlocked.Increment(ref _expirations);
            }
        }

        Interlocked.Increment(ref _misses);
        return default;
    }

    public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Get<TValue>(key));
    }

    /// <summary>设置缓存项（best-effort 容量管理，容忍瞬时超容量）</summary>
    public void Set<TValue>(string key, TValue value, TimeSpan? expiration = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        // best-effort 容量驱逐：容忍竞态导致的瞬时超容量
        if (_cache.Count >= _configuration.MaxItems && !_cache.ContainsKey(key))
        {
            EvictOne();
        }

        var entry = new CacheEntry
        {
            Key = key,
            Value = value,
            CreatedTime = DateTime.UtcNow,
            LastAccessTime = DateTime.UtcNow,
            Expiration = expiration ?? _configuration.DefaultExpiration
        };

        _cache.AddOrUpdate(key, entry, (_, __) => entry);
    }

    public Task SetAsync<TValue>(string key, TValue value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Set(key, value, expiration);
        return Task.CompletedTask;
    }

    public TValue GetOrCreate<TValue>(string key, Func<TValue> factory, TimeSpan? expiration = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        return GetOrCreateAsync(key, () => Task.FromResult(factory()), expiration)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步获取或创建缓存项 - 使用 Lazy&lt;Task&lt;T&gt;&gt; 保证每个 key 的工厂只执行一次
    /// </summary>
    public async Task<TValue> GetOrCreateAsync<TValue>(string key, Func<Task<TValue>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        // 先尝试命中（无工厂开销）
        var existing = Get<TValue>(key);
        if (existing != null)
            return existing;

        var lazy = _inflightFactories.GetOrAdd(key, _ =>
            new Lazy<Task<object?>>(async () => await factory().ConfigureAwait(false),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var result = await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (result is not TValue typedResult)
            {
                throw new InvalidOperationException($"Cache factory for '{key}' returned an incompatible type.");
            }

            Set(key, typedResult, expiration);
            return typedResult;
        }
        finally
        {
            if (lazy.IsValueCreated && lazy.Value.IsCompleted)
            {
                _inflightFactories.TryRemove(key, out _);
            }
        }
    }

    public bool Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        return _cache.TryRemove(key, out _);
    }

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Remove(key));
    }

    public bool Exists(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (_cache.TryGetValue(key, out var entry))
        {
            return !entry.IsExpired;
        }

        return false;
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Clear();
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetKeys()
    {
        return _cache.Keys.ToList();
    }

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            Count = _cache.Count,
            Hits = _hits,
            Misses = _misses,
            Evictions = _evictions,
            Expirations = _expirations,
            TotalSize = EstimateCacheSize()
        };
    }

    private void EvictOne()
    {
        var entryToEvict = _configuration.EvictionPolicy switch
        {
            CacheEvictionPolicy.LRU => _cache.Values.OrderBy(e => e.LastAccessTime).FirstOrDefault(),
            CacheEvictionPolicy.LFU => _cache.Values.OrderBy(e => e.AccessCount).FirstOrDefault(),
            CacheEvictionPolicy.FIFO => _cache.Values.OrderBy(e => e.CreatedTime).FirstOrDefault(),
            CacheEvictionPolicy.Random => _cache.Values.OrderBy(_ => Guid.NewGuid()).FirstOrDefault(),
            _ => _cache.Values.FirstOrDefault()
        };

        if (entryToEvict != null)
        {
            _cache.TryRemove(entryToEvict.Key, out _);
            Interlocked.Increment(ref _evictions);
        }
    }

    private void CleanupCallback(object? state)
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (_cache.TryRemove(key, out _))
            {
                Interlocked.Increment(ref _expirations);
            }
        }
    }

    private long EstimateCacheSize()
    {
        // 简单估算，实际应根据对象大小计算
        return _cache.Count * 1024; // 假设每项平均 1KB
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Dispose();
        _cache.Clear();
        _inflightFactories.Clear();

        _disposed = true;
    }

    private sealed class CacheEntry
    {
        public string Key { get; init; } = string.Empty;
        public object? Value { get; set; }
        public DateTime CreatedTime { get; init; }
        public DateTime LastAccessTime { get; set; }
        public TimeSpan? Expiration { get; init; }
        public int AccessCount { get; private set; }

        public bool IsExpired => Expiration.HasValue && DateTime.UtcNow - LastAccessTime > Expiration.Value;

        public void UpdateAccess()
        {
            LastAccessTime = DateTime.UtcNow;
            AccessCount++;
        }
    }
}
