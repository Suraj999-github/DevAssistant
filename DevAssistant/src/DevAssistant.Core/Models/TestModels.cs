namespace DevAssistant.Models
{
    public class TestModels
    {
    }
    public enum TestStatus
    {
        NotRun, Running, Passed, Failed, Error,
        Skipped
    }
    public sealed record TestResult(
    string TestName,
    string ClassName,
    TestStatus Status,
    string? ErrorMessage,
    double DurationMs);

    public sealed record TestRunSummary(
        int Total,
        int Passed,
        int Failed,
        int Skipped,
        double DurationMs,
        IReadOnlyList<TestResult> Results,
        string RawOutput);

    public sealed record TestRunnerViewModel(
        TestRunSummary? LastRun,
        bool IsRunning,
        string? WorkingDirectory);
}
