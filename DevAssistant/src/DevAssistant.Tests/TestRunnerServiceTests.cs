using DevAssistant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevAssistant.Tests
{
    //public class TestRunnerServiceTests
    //{
    //    private static TestRunnerService Build(string projectPath)
    //    {
    //        var config = new ConfigurationBuilder()
    //            .AddInMemoryCollection(new Dictionary<string, string?>
    //            {
    //                ["TestRunner:ProjectPath"] = projectPath
    //            })
    //            .Build();

    //        return new TestRunnerService(config, NullLogger<TestRunnerService>.Instance);
    //    }

    //    // ── Happy path — runs THIS test project ──────────────────────────────────

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_ReturnsNonNullSummary()
    //    {
    //        // Points at the current test assembly's project dir
    //        var projectDir = GetThisProjectDirectory();
    //        var sut = Build(projectDir);

    //        var summary = await sut.RunAsync();

    //        Assert.NotNull(summary);
    //        Assert.NotNull(summary.RawOutput);
    //    }

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_TotalIsPositive()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        var summary = await sut.RunAsync();

    //        Assert.True(summary.Total > 0,
    //            $"Expected at least 1 test; got {summary.Total}. Output:\n{summary.RawOutput}");
    //    }

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_PassedPlusFailed_EqualsTotalMinusSkipped()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        var summary = await sut.RunAsync();

    //        Assert.Equal(summary.Total, summary.Passed + summary.Failed + summary.Skipped);
    //    }

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_DurationMsIsPositive()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        var summary = await sut.RunAsync();

    //        Assert.True(summary.DurationMs >= 0);
    //    }

    //    // ── Filter ────────────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task RunAsync_WithMatchingFilter_ReturnsSubset()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        // Run only MemoryService tests — guaranteed to exist in this file
    //        var all = await sut.RunAsync();
    //        var filtered = await sut.RunAsync("MemoryServiceTests");

    //        Assert.True(filtered.Total < all.Total,
    //            "Filtered run should produce fewer tests than the full run");
    //    }

    //    [Fact]
    //    public async Task RunAsync_WithNonMatchingFilter_ReturnsZeroTests()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        var summary = await sut.RunAsync("XYZNONEXISTENT_CLASS_9999");

    //        // dotnet test exits cleanly with 0 results — not an error
    //        Assert.Equal(0, summary.Total);
    //    }

    //    // ── Error / bad config ────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task RunAsync_InvalidProjectPath_ReturnsErrorInOutput()
    //    {
    //        var sut = Build("/path/does/not/exist");

    //        var summary = await sut.RunAsync();

    //        // Should not throw — error surfaced in RawOutput
    //        Assert.NotNull(summary.RawOutput);
    //        Assert.True(summary.Total == 0 || summary.RawOutput.Contains("error", StringComparison.OrdinalIgnoreCase));
    //    }

    //    // ── Results collection ────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_ResultsCollectionNotNull()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        var summary = await sut.RunAsync();

    //        Assert.NotNull(summary.Results);
    //    }

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_EachResultHasTestName()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        var summary = await sut.RunAsync();

    //        Assert.All(summary.Results, r => Assert.False(string.IsNullOrWhiteSpace(r.TestName)));
    //    }

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_EachResultHasValidStatus()
    //    {
    //        var sut = Build(GetThisProjectDirectory());
    //        var validStatuses = new[]
    //        {
    //        DevAssistant.Models.TestStatus.Passed,
    //        DevAssistant.Models.TestStatus.Failed,
    //        DevAssistant.Models.TestStatus.Skipped,
    //        DevAssistant.Models.TestStatus.Error
    //    };

    //        var summary = await sut.RunAsync();

    //        Assert.All(summary.Results, r => Assert.Contains(r.Status, validStatuses));
    //    }

    //    [Fact]
    //    public async Task RunAsync_CurrentProject_FailedResultsHaveErrorMessage()
    //    {
    //        var sut = Build(GetThisProjectDirectory());

    //        var summary = await sut.RunAsync();

    //        var failedWithoutError = summary.Results
    //            .Where(r => r.Status == DevAssistant.Models.TestStatus.Failed
    //                     && string.IsNullOrWhiteSpace(r.ErrorMessage))
    //            .ToList();

    //        Assert.Empty(failedWithoutError);
    //    }

    //    // ── Skipped test (visible in runner) ─────────────────────────────────────

    //    [Fact(Skip = "Intentionally skipped — verifies Skipped count in TestRunSummary")]
    //    public Task RunAsync_SkippedTest_CountedAsSkipped()
    //        => Task.CompletedTask;

    //    // ── Helper ────────────────────────────────────────────────────────────────

    //    private static string GetThisProjectDirectory()
    //    {
    //        // Walk up from bin/Debug/net9.0/ to the .csproj folder
    //        var dir = new DirectoryInfo(AppContext.BaseDirectory);
    //        while (dir is not null && !dir.GetFiles("*.csproj").Any())
    //            dir = dir.Parent;
    //        return dir?.FullName ?? AppContext.BaseDirectory;
    //    }
    //}
}
