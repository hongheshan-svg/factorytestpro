using System;
using UTF.Core.Caching;

namespace UTF.Core;

/// <summary>
/// 优化工具包 - 提供统一的优化功能入口。
/// 已精简：仅保留外部依赖的 CreateStandardCache；对象池工厂方法（StringBuilder/Buffer）
/// 与 WithCacheAsync 包装器经审计确认零调用方，已移除。如需对象池请直接使用
/// <see cref="UTF.Core.ObjectPool.ObjectPoolFactory"/>。
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
}
