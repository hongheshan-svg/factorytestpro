using System;
using System.Collections.Generic;
using System.Linq;
using UTF.Core;
using UTF.Reporting;
using Xunit;

namespace UTF.Core.Tests;

public class ConfigDrivenReportBridgeTests
{
    [Fact]
    public void ConvertToDataSet_SingleReport_CorrectRowCount()
    {
        var report = CreateSampleReport(stepCount: 5, passedCount: 3);

        var dataSet = ConfigDrivenReportBridge.ConvertToDataSet(report);

        Assert.Equal(5, dataSet.Rows.Count);
    }

    [Fact]
    public void ConvertToDataSet_SingleReport_CorrectSummaryItems()
    {
        var report = CreateSampleReport(stepCount: 4, passedCount: 3);

        var dataSet = ConfigDrivenReportBridge.ConvertToDataSet(report);

        var totalItem = dataSet.DataItems.First(i => i.Name == "TotalTests");
        var passedItem = dataSet.DataItems.First(i => i.Name == "PassedTests");
        var failedItem = dataSet.DataItems.First(i => i.Name == "FailedTests");
        var passRateItem = dataSet.DataItems.First(i => i.Name == "PassRate");

        Assert.Equal(4, totalItem.Value);
        Assert.Equal(3, passedItem.Value);
        Assert.Equal(1, failedItem.Value);
        Assert.Equal(75.0, passRateItem.Value);
    }

    [Fact]
    public void ConvertToDataSet_EmptySteps_ZeroPassRate()
    {
        var report = new ConfigDrivenTestReport
        {
            ProjectId = "P1",
            ProjectName = "Empty",
            DutId = "DUT_001",
            StepResults = new List<ConfigDrivenStepResult>()
        };

        var dataSet = ConfigDrivenReportBridge.ConvertToDataSet(report);

        var passRate = dataSet.DataItems.First(i => i.Name == "PassRate");
        Assert.Equal(0.0, passRate.Value);
    }

    [Fact]
    public void ConvertToDataSet_SkippedSteps_MarkedAsSkip()
    {
        var report = new ConfigDrivenTestReport
        {
            ProjectId = "P1",
            DutId = "DUT_001",
            StepResults = new List<ConfigDrivenStepResult>
            {
                new() { StepId = "S1", Passed = true, Skipped = true }
            }
        };

        var dataSet = ConfigDrivenReportBridge.ConvertToDataSet(report);

        Assert.Equal("SKIP", dataSet.Rows[0]["TestResult"]);
    }

    [Fact]
    public void ConvertToDataSet_ColumnsMatchExpected()
    {
        var report = CreateSampleReport(1, 1);
        var dataSet = ConfigDrivenReportBridge.ConvertToDataSet(report);

        Assert.Contains("StepId", dataSet.Columns);
        Assert.Contains("StepName", dataSet.Columns);
        Assert.Contains("TestResult", dataSet.Columns);
        Assert.Contains("MeasuredValue", dataSet.Columns);
        Assert.Contains("ExpectedValue", dataSet.Columns);
        Assert.Contains("ExecutionTime", dataSet.Columns);
    }

    [Fact]
    public void MergeReportsToDataSet_MultipleReports_AggregatesCorrectly()
    {
        var reports = new List<ConfigDrivenTestReport>
        {
            CreateSampleReport(stepCount: 3, passedCount: 2, dutId: "DUT_001"),
            CreateSampleReport(stepCount: 4, passedCount: 4, dutId: "DUT_002")
        };

        var merged = ConfigDrivenReportBridge.MergeReportsToDataSet(reports);

        Assert.Equal(7, merged.Rows.Count);
        var totalItem = merged.DataItems.First(i => i.Name == "TotalTests");
        Assert.Equal(7, totalItem.Value);

        var dutCount = merged.DataItems.First(i => i.Name == "DutCount");
        Assert.Equal(2, dutCount.Value);
    }

    [Fact]
    public void MergeReportsToDataSet_EmptyList_ReturnsZeros()
    {
        var merged = ConfigDrivenReportBridge.MergeReportsToDataSet(new List<ConfigDrivenTestReport>());

        Assert.Empty(merged.Rows);
        var totalItem = merged.DataItems.First(i => i.Name == "TotalTests");
        Assert.Equal(0, totalItem.Value);
    }

    [Fact]
    public void ConvertToDataSet_Metadata_ContainsProjectInfo()
    {
        var report = CreateSampleReport(2, 2);

        var dataSet = ConfigDrivenReportBridge.ConvertToDataSet(report);

        Assert.Equal("TEST_PROJECT", dataSet.Metadata["ProjectId"]);
        Assert.Equal("DUT_001", dataSet.Metadata["DutId"]);
        Assert.Equal("PASS", dataSet.Metadata["OverallResult"]);
    }

    private static ConfigDrivenTestReport CreateSampleReport(
        int stepCount, int passedCount, string dutId = "DUT_001")
    {
        var steps = new List<ConfigDrivenStepResult>();
        for (int i = 0; i < stepCount; i++)
        {
            steps.Add(new ConfigDrivenStepResult
            {
                StepId = $"S{i + 1}",
                StepName = $"步骤{i + 1}",
                Passed = i < passedCount,
                MeasuredValue = $"val_{i}",
                ExpectedValue = $"exp_{i}",
                StartTime = DateTime.UtcNow.AddSeconds(-stepCount + i),
                EndTime = DateTime.UtcNow.AddSeconds(-stepCount + i + 1)
            });
        }

        return new ConfigDrivenTestReport
        {
            ProjectId = "TEST_PROJECT",
            ProjectName = "测试项目",
            DutId = dutId,
            Passed = passedCount == stepCount,
            StartTime = DateTime.UtcNow.AddSeconds(-stepCount),
            EndTime = DateTime.UtcNow,
            StepResults = steps
        };
    }
}
