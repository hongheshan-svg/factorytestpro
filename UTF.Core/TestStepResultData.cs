using System;
using System.Collections.Generic;

namespace UTF.Core
{
    /// <summary>
    /// 详细的测试步骤结果数据类
    /// </summary>
    public sealed class TestStepResultData
    {
        /// <summary>步骤ID</summary>
        public string StepId { get; set; } = string.Empty;
        
        /// <summary>步骤名称</summary>
        public string StepName { get; set; } = string.Empty;
        
        /// <summary>是否通过</summary>
        public bool Passed { get; set; }
        
        /// <summary>结果状态</summary>
        public TestStepStatus Status { get; set; }
        
        /// <summary>开始时间</summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>结束时间</summary>
        public DateTime EndTime { get; set; }
        
        /// <summary>执行时长</summary>
        public TimeSpan Duration => EndTime - StartTime;
        
        /// <summary>错误信息</summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>结果消息</summary>
        public string? Message { get; set; }
        
        /// <summary>重试次数</summary>
        public int RetryCount { get; set; }
        
        /// <summary>测量值</summary>
        public object? MeasuredValue { get; set; }
        
        /// <summary>期望值</summary>
        public object? ExpectedValue { get; set; }
        
        /// <summary>扩展数据</summary>
        public Dictionary<string, object> ExtendedData { get; set; } = new();
        
        /// <summary>测量值列表</summary>
        public Dictionary<string, object> MeasuredValues { get; set; } = new();

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static TestStepResultData CreateSuccess(string stepId, string stepName, string? message = null)
        {
            return new TestStepResultData
            {
                StepId = stepId,
                StepName = stepName,
                Passed = true,
                Status = TestStepStatus.Completed,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                Message = message ?? "测试通过"
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static TestStepResultData CreateFailure(string stepId, string stepName, string errorMessage)
        {
            return new TestStepResultData
            {
                StepId = stepId,
                StepName = stepName,
                Passed = false,
                Status = TestStepStatus.Failed,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                ErrorMessage = errorMessage,
                Message = "测试失败"
            };
        }

        /// <summary>
        /// 从简单枚举转换
        /// </summary>
        public static TestStepResultData FromEnum(TestStepResult result, string stepId = "", string stepName = "")
        {
            return new TestStepResultData
            {
                StepId = stepId,
                StepName = stepName,
                Passed = result == TestStepResult.Pass,
                Status = result switch
                {
                    TestStepResult.Pass => TestStepStatus.Completed,
                    TestStepResult.Fail => TestStepStatus.Failed,
                    TestStepResult.Skip => TestStepStatus.Skipped,
                    _ => TestStepStatus.Pending
                },
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                Message = result switch
                {
                    TestStepResult.Pass => "测试通过",
                    TestStepResult.Fail => "测试失败",
                    TestStepResult.Skip => "测试跳过",
                    _ => "未运行"
                }
            };
        }

        /// <summary>
        /// 转换为简单枚举
        /// </summary>
        public TestStepResult ToEnum()
        {
            if (Status == TestStepStatus.Skipped) return TestStepResult.Skip;
            return Passed ? TestStepResult.Pass : TestStepResult.Fail;
        }
    }
}
