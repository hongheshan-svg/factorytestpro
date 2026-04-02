using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core.Events;

public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly ILogger _logger;

    public EventBus(ILogger logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event) where T : IEvent
    {
        var eventType = typeof(T);
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        foreach (var handler in handlers.ToList())
        {
            try
            {
                await ((Func<T, Task>)handler)(@event);
            }
            catch (Exception ex)
            {
                _logger.Error($"事件处理失败: {eventType.Name}", ex);
            }
        }
    }

    public IDisposable Subscribe<T>(Func<T, Task> handler) where T : IEvent
    {
        var eventType = typeof(T);
        _handlers.AddOrUpdate(eventType,
            _ => new List<Delegate> { handler },
            (_, list) => { list.Add(handler); return list; });

        return new Subscription(() =>
        {
            if (_handlers.TryGetValue(eventType, out var list))
                list.Remove(handler);
        });
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
