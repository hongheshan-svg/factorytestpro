using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core.Events;

    /// <summary>
    /// 事件总线实现 - 使用 ImmutableList 原子交换订阅列表，并行分派事件
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, ImmutableList<Delegate>> _handlers = new();
        private readonly ILogger _logger;
        private static readonly ImmutableList<Delegate> EmptyHandlers = ImmutableList<Delegate>.Empty;

        public EventBus(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 并行分派事件给所有订阅者；每个处理器独立 try/catch，异常不中断其他处理器
        /// </summary>
        public async Task PublishAsync<T>(T @event) where T : IEvent
        {
            var eventType = typeof(T);
            if (!_handlers.TryGetValue(eventType, out var handlers) || handlers.IsEmpty)
                return;

            // 拍照当前订阅列表，并行分派
            var snapshot = handlers;
            await Task.WhenAll(snapshot.Select(h => InvokeSafely<T>((Func<T, Task>)h, @event))).ConfigureAwait(false);
        }

        /// <summary>
        /// 安全调用单个处理器，异常通过注入的 <see cref="ILogger"/> 记录。
        /// 当 logger 不可用（理论上不会发生，构造函数已强制非空）时回退到 Debug.WriteLine。
        /// </summary>
        private async Task InvokeSafely<T>(Func<T, Task> handler, T @event)
        {
            try
            {
                await handler(@event).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var message = $"事件处理器异常: {typeof(T).Name} -> {ex.GetType().Name}: {ex.Message}";
                try
                {
                    _logger?.Error(message, ex);
                }
                catch
                {
                    // 记录器自身异常时回退到 Debug，避免吞掉原始异常信息
                    Debug.WriteLine(message);
                }
            }
        }

    /// <summary>订阅事件 - 通过 ImmutableList 不可变交换保证线程安全</summary>
    public IDisposable Subscribe<T>(Func<T, Task> handler) where T : IEvent
    {
        var eventType = typeof(T);

        _handlers.AddOrUpdate(eventType,
            _ => ImmutableList<Delegate>.Empty.Add(handler),
            (_, list) => list.Add(handler));

        return new Subscription(() =>
        {
            // 原子移除：AddOrUpdate 内部保证对列表的更新是原子的
            _handlers.AddOrUpdate(eventType,
                _ => EmptyHandlers,
                (_, list) => list.Remove(handler));
        });
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
