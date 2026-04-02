using System;

namespace UTF.Core;

public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;

    public ExponentialBackoffRetryPolicy(int maxRetries = 3, int baseDelayMs = 1000)
    {
        _maxRetries = maxRetries;
        _baseDelay = TimeSpan.FromMilliseconds(baseDelayMs);
    }

    public bool ShouldRetry(int attemptCount, Exception? exception)
    {
        return attemptCount < _maxRetries;
    }

    public TimeSpan GetNextDelay(int attemptCount)
    {
        return TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attemptCount));
    }
}
