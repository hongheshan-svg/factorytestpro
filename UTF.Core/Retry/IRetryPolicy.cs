using System;
using System.Threading.Tasks;

namespace UTF.Core;

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryPolicy
{
    bool ShouldRetry(int attemptCount, Exception? exception);
    TimeSpan GetNextDelay(int attemptCount);
}
