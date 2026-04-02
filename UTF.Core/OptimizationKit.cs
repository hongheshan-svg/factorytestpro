using System;
using System.Threading.Tasks;
using UTF.Core.Caching;
using UTF.Core.ObjectPool;
using UTF.Core.Validation;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 优化工具包 - 提供统一的优化功能入口
/// </summary>
public static class OptimizationKit
{
    /// <summary>
    /// 创建标准缓存配置
    /// </summary>
    public static ICache CreateStandardCache(int maxItems = 1000, TimeSpan? expiration = null)
    {
        return new MemoryCache(new CacheConfiguration
        {
            MaxItems = maxItems,
            DefaultExpiration = expiration ?? TimeSpan.FromMinutes(30),
            EvictionPolicy = CacheEvictionPolicy.LRU,
            EnableStatistics = true,
            SlidingExpiration = true
        });
    }
    
    /// <summary>
    /// 创建StringBuilder对象池
    /// </summary>
    public static IObjectPool<System.Text.StringBuilder> CreateStringBuilderPool(int maxSize = 100)
    {
        return ObjectPoolFactory.Create(
            factory: () => new System.Text.StringBuilder(256),
            reset: sb => sb.Clear(),
            maxSize: maxSize
        );
    }
    
    /// <summary>
    /// 创建字节数组对象池（用于网络通信）
    /// </summary>
    public static IObjectPool<byte[]> CreateBufferPool(int bufferSize = 4096, int maxSize = 100)
    {
        return ObjectPoolFactory.Create(
            factory: () => new byte[bufferSize],
            reset: buffer => Array.Clear(buffer, 0, buffer.Length),
            maxSize: maxSize
        );
    }
    
    /// <summary>
    /// 创建优化的测试引擎
    /// </summary>
    public static ITestEngine CreateOptimizedTestEngine(
        ILogger? logger = null, 
        ICache? cache = null, 
        int? maxConcurrentTasks = null)
    {
        cache ??= CreateStandardCache();
        var concurrency = maxConcurrentTasks ?? Environment.ProcessorCount * 2;
        
        return new OptimizedTestEngine(logger, cache, concurrency);
    }
    
    /// <summary>
    /// 验证配置对象
    /// </summary>
    public static ValidationResult ValidateConfiguration<T>(T config, string configName) where T : class
    {
        return ValidationHelper.Combine(
            ValidationHelper.ValidateNotNull(config, configName),
            ValidationHelper.ValidateNotNull(config, $"{configName} 实例")
        );
    }
    
    /// <summary>
    /// 带缓存的异步操作包装器
    /// </summary>
    public static async Task<T> WithCacheAsync<T>(
        ICache cache,
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null)
    {
        return await cache.GetOrCreateAsync(key, factory, expiration);
    }
    
    /// <summary>
    /// 带对象池的操作包装器
    /// </summary>
    public static TResult WithPooledObject<TPooled, TResult>(
        IObjectPool<TPooled> pool,
        Func<TPooled, TResult> operation) where TPooled : class
    {
        var obj = pool.Get();
        try
        {
            return operation(obj);
        }
        finally
        {
            pool.Return(obj);
        }
    }
    
    /// <summary>
    /// 带对象池的异步操作包装器
    /// </summary>
    public static async Task<TResult> WithPooledObjectAsync<TPooled, TResult>(
        IObjectPool<TPooled> pool,
        Func<TPooled, Task<TResult>> operation) where TPooled : class
    {
        var obj = pool.Get();
        try
        {
            return await operation(obj);
        }
        finally
        {
            pool.Return(obj);
        }
    }
}

/// <summary>
/// 优化扩展方法
/// </summary>
public static class OptimizationExtensions
{
    /// <summary>
    /// 为设备管理器添加缓存支持
    /// </summary>
    public static async Task<T?> GetWithCacheAsync<T>(
        this ICache cache,
        string key,
        Func<Task<T?>> loadFunc,
        TimeSpan? expiration = null) where T : class
    {
        return await cache.GetOrCreateAsync(key, loadFunc, expiration);
    }
    
    /// <summary>
    /// 批量验证
    /// </summary>
    public static ValidationResult ValidateAll(params ValidationResult[] results)
    {
        return ValidationHelper.Combine(results);
    }
}

