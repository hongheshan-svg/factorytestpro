using System;

namespace UTF.Core.ObjectPool;

/// <summary>
/// 对象池接口
/// </summary>
public interface IObjectPool<T> : IDisposable where T : class
{
    /// <summary>从池中获取对象</summary>
    T Get();
    
    /// <summary>归还对象到池中</summary>
    void Return(T item);
    
    /// <summary>获取池统计信息</summary>
    PoolStatistics GetStatistics();
    
    /// <summary>清空池</summary>
    void Clear();
}

/// <summary>
/// 池统计信息
/// </summary>
public sealed record PoolStatistics
{
    /// <summary>池中对象数量</summary>
    public int AvailableCount { get; init; }
    
    /// <summary>总创建数量</summary>
    public int TotalCreated { get; init; }
    
    /// <summary>使用中数量</summary>
    public int InUseCount { get; init; }
    
    /// <summary>最大容量</summary>
    public int MaxCapacity { get; init; }
}

/// <summary>
/// 对象池策略</summary>
public sealed record ObjectPoolPolicy<T> where T : class
{
    /// <summary>对象创建工厂</summary>
    public Func<T> Factory { get; init; } = () => throw new NotImplementedException();
    
    /// <summary>对象重置动作</summary>
    public Action<T>? Reset { get; init; }
    
    /// <summary>对象验证函数</summary>
    public Func<T, bool>? Validate { get; init; }
    
    /// <summary>最大池大小</summary>
    public int MaxPoolSize { get; init; } = 100;
    
    /// <summary>初始池大小</summary>
    public int InitialPoolSize { get; init; } = 10;
    
    /// <summary>是否在归还时重置对象</summary>
    public bool ResetOnReturn { get; init; } = true;
    
    /// <summary>是否在获取时验证对象</summary>
    public bool ValidateOnGet { get; init; } = false;
}

