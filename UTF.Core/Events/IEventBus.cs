using System;
using System.Threading.Tasks;

namespace UTF.Core.Events;

public interface IEvent
{
    DateTime Timestamp { get; }
}

public interface IEventBus
{
    Task PublishAsync<T>(T @event) where T : IEvent;
    IDisposable Subscribe<T>(Func<T, Task> handler) where T : IEvent;
}
