using System;
using System.Threading.Tasks;

namespace UTF.Core;

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// 获取最大尝试次数（含首次执行）。当策略被注入时，此值优先于步骤配置的 RetryCount。
    /// </summary>
    int MaxAttempts { get; }

    bool ShouldRetry(int attemptCount, Exception? exception);
    TimeSpan GetNextDelay(int attemptCount);
}
