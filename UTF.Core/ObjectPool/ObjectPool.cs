using System;
using System.Collections.Concurrent;
using System.Threading;

namespace UTF.Core.ObjectPool;

/// <summary>
/// 对象池实现
/// </summary>
public sealed class ObjectPool<T> : IObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly ObjectPoolPolicy<T> _policy;
    private int _totalCreated = 0;
    private int _inUseCount = 0;
    private bool _disposed = false;

    public ObjectPool(ObjectPoolPolicy<T> policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        
        // 预创建初始对象
        for (int i = 0; i < _policy.InitialPoolSize; i++)
        {
            var item = _policy.Factory();
            _pool.Add(item);
            Interlocked.Increment(ref _totalCreated);
        }
    }

    public T Get()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ObjectPool<T>));

        T? item;
        
        // 尝试从池中获取
        while (_pool.TryTake(out item))
        {
            // 如果需要验证
            if (_policy.ValidateOnGet && _policy.Validate != null)
            {
                if (!_policy.Validate(item))
                {
                    // 验证失败，丢弃对象
                    if (item is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    continue;
                }
            }
            
            Interlocked.Increment(ref _inUseCount);
            return item;
        }

        // 池为空，创建新对象
        item = _policy.Factory();
        Interlocked.Increment(ref _totalCreated);
        Interlocked.Increment(ref _inUseCount);
        return item;
    }

    public void Return(T item)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ObjectPool<T>));

        if (item == null)
            throw new ArgumentNullException(nameof(item));

        // 重置对象
        if (_policy.ResetOnReturn && _policy.Reset != null)
        {
            try
            {
                _policy.Reset(item);
            }
            catch
            {
                // 重置失败，丢弃对象
                if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                Interlocked.Decrement(ref _inUseCount);
                return;
            }
        }

        // 如果池已满，丢弃对象
        if (_pool.Count >= _policy.MaxPoolSize)
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        else
        {
            _pool.Add(item);
        }

        Interlocked.Decrement(ref _inUseCount);
    }

    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            AvailableCount = _pool.Count,
            TotalCreated = _totalCreated,
            InUseCount = _inUseCount,
            MaxCapacity = _policy.MaxPoolSize
        };
    }

    public void Clear()
    {
        while (_pool.TryTake(out var item))
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Clear();
        _disposed = true;
    }
}

/// <summary>
/// 对象池工厂
/// </summary>
public static class ObjectPoolFactory
{
    /// <summary>创建默认对象池</summary>
    public static IObjectPool<T> Create<T>(Func<T> factory, int maxSize = 100) where T : class
    {
        var policy = new ObjectPoolPolicy<T>
        {
            Factory = factory,
            MaxPoolSize = maxSize,
            InitialPoolSize = Math.Min(10, maxSize)
        };
        return new ObjectPool<T>(policy);
    }

    /// <summary>创建带重置的对象池</summary>
    public static IObjectPool<T> Create<T>(Func<T> factory, Action<T> reset, int maxSize = 100) where T : class
    {
        var policy = new ObjectPoolPolicy<T>
        {
            Factory = factory,
            Reset = reset,
            MaxPoolSize = maxSize,
            InitialPoolSize = Math.Min(10, maxSize),
            ResetOnReturn = true
        };
        return new ObjectPool<T>(policy);
    }
}

