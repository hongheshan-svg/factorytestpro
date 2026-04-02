using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Caching;

/// <summary>
/// 内存缓存实现
/// </summary>
public sealed class MemoryCache : ICache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly CacheConfiguration _configuration;
    private readonly Timer _cleanupTimer;
    private readonly ReaderWriterLockSlim _lock = new();
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

    public TValue? Get<TValue>(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        _lock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.IsExpired)
                {
                    if (_configuration.SlidingExpiration && entry.Expiration.HasValue)
                    {
                        entry.UpdateAccess();
                    }
                    
                    Interlocked.Increment(ref _hits);
                    return (TValue?)entry.Value;
                }
                else
                {
                    // 过期，删除
                    _cache.TryRemove(key, out _);
                    Interlocked.Increment(ref _expirations);
                }
            }

            Interlocked.Increment(ref _misses);
            return default;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public async Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Get<TValue>(key), cancellationToken);
    }

    public void Set<TValue>(string key, TValue value, TimeSpan? expiration = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        _lock.EnterWriteLock();
        try
        {
            // 检查缓存是否已满
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
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task SetAsync<TValue>(string key, TValue value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Set(key, value, expiration), cancellationToken);
    }

    public TValue GetOrCreate<TValue>(string key, Func<TValue> factory, TimeSpan? expiration = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        var value = Get<TValue>(key);
        if (value != null)
            return value;

        var newValue = factory();
        Set(key, newValue, expiration);
        return newValue;
    }

    public async Task<TValue> GetOrCreateAsync<TValue>(string key, Func<Task<TValue>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        var value = await GetAsync<TValue>(key, cancellationToken);
        if (value != null)
            return value;

        var newValue = await factory();
        await SetAsync(key, newValue, expiration, cancellationToken);
        return newValue;
    }

    public bool Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        return _cache.TryRemove(key, out _);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Remove(key), cancellationToken);
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
        _lock.EnterWriteLock();
        try
        {
            _cache.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(Clear, cancellationToken);
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
        _lock.EnterWriteLock();
        try
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
                Interlocked.Increment(ref _expirations);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
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
        _lock?.Dispose();
        _cache.Clear();

        _disposed = true;
    }

    private sealed class CacheEntry
    {
        public string Key { get; init; } = string.Empty;
        public object? Value { get; init; }
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

