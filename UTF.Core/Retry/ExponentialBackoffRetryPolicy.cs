using System;

namespace UTF.Core;

public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public ExponentialBackoffRetryPolicy(int maxRetries = 3, int baseDelayMs = 1000)
        : this(maxRetries, TimeSpan.FromMilliseconds(baseDelayMs), TimeSpan.FromMinutes(5))
    {
    }

    public ExponentialBackoffRetryPolicy(int maxRetries, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        _maxRetries = maxRetries;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    /// <inheritdoc />
    public int MaxAttempts => _maxRetries + 1;

    public bool ShouldRetry(int attemptCount, Exception? exception)
    {
        return attemptCount < _maxRetries;
    }

    public TimeSpan GetNextDelay(int attemptCount)
    {
        var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attemptCount));
        return delay > _maxDelay ? _maxDelay : delay;
    }
}
