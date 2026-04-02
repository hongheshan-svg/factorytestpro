using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Caching;

/// <summary>
/// 缓存接口
/// </summary>
public interface ICache : IDisposable
{
    /// <summary>获取缓存项</summary>
    TValue? Get<TValue>(string key);
    
    /// <summary>异步获取缓存项</summary>
    Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>设置缓存项</summary>
    void Set<TValue>(string key, TValue value, TimeSpan? expiration = null);
    
    /// <summary>异步设置缓存项</summary>
    Task SetAsync<TValue>(string key, TValue value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    
    /// <summary>获取或创建缓存项</summary>
    TValue GetOrCreate<TValue>(string key, Func<TValue> factory, TimeSpan? expiration = null);
    
    /// <summary>异步获取或创建缓存项</summary>
    Task<TValue> GetOrCreateAsync<TValue>(string key, Func<Task<TValue>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    
    /// <summary>删除缓存项</summary>
    bool Remove(string key);
    
    /// <summary>异步删除缓存项</summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>检查缓存项是否存在</summary>
    bool Exists(string key);
    
    /// <summary>清空缓存</summary>
    void Clear();
    
    /// <summary>异步清空缓存</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
    
    /// <summary>获取所有键</summary>
    IEnumerable<string> GetKeys();
    
    /// <summary>获取缓存统计信息</summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// 缓存统计信息
/// </summary>
public sealed record CacheStatistics
{
    /// <summary>缓存项数量</summary>
    public int Count { get; init; }
    
    /// <summary>命中次数</summary>
    public long Hits { get; init; }
    
    /// <summary>未命中次数</summary>
    public long Misses { get; init; }
    
    /// <summary>命中率</summary>
    public double HitRate => (Hits + Misses) > 0 ? (double)Hits / (Hits + Misses) : 0.0;
    
    /// <summary>驱逐次数</summary>
    public long Evictions { get; init; }
    
    /// <summary>过期次数</summary>
    public long Expirations { get; init; }
    
    /// <summary>总大小（字节）</summary>
    public long TotalSize { get; init; }
}

/// <summary>
/// 缓存配置
/// </summary>
public sealed record CacheConfiguration
{
    /// <summary>最大缓存项数量</summary>
    public int MaxItems { get; init; } = 10000;
    
    /// <summary>默认过期时间</summary>
    public TimeSpan DefaultExpiration { get; init; } = TimeSpan.FromMinutes(30);
    
    /// <summary>清理间隔</summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromMinutes(5);
    
    /// <summary>是否启用滑动过期</summary>
    public bool SlidingExpiration { get; init; } = true;
    
    /// <summary>是否启用统计</summary>
    public bool EnableStatistics { get; init; } = true;
    
    /// <summary>缓存驱逐策略</summary>
    public CacheEvictionPolicy EvictionPolicy { get; init; } = CacheEvictionPolicy.LRU;
}

/// <summary>
/// 缓存驱逐策略
/// </summary>
public enum CacheEvictionPolicy
{
    /// <summary>最近最少使用</summary>
    LRU,
    /// <summary>最不经常使用</summary>
    LFU,
    /// <summary>先进先出</summary>
    FIFO,
    /// <summary>随机</summary>
    Random
}

