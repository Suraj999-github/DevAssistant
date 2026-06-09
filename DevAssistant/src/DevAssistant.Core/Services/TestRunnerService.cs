using DevAssistant.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DevAssistant.Services
{
    public interface ITestRunnerService
    {
        bool IsRunning { get; }
        void CancelRun();
        Task<TestRunSummary> RunAsync(string? filter = null, CancellationToken ct = default);
    }

    public sealed class TestRunnerService : ITestRunnerService
    {
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private readonly IConfiguration _config;
        private readonly ILogger<TestRunnerService> _logger;

        private string TestProjectPath =>
            _config["TestRunner:ProjectPath"] ?? Directory.GetCurrentDirectory();

        // Tracked so we can kill it from outside
        private Process? _activeProcess;
        private CancellationTokenSource? _activeCts;

        private int TimeoutSeconds =>
            int.TryParse(_config["TestRunner:TimeoutSeconds"], out var t) ? t : 120;


        public TestRunnerService(IConfiguration config, ILogger<TestRunnerService> logger)
        {
            _config = config;
            _logger = logger;
        }
        public bool IsRunning => _activeProcess is { HasExited: false };

        // Call this from the controller / AgentService
        public void CancelRun()
        {
            _activeCts?.Cancel();

            var p = _activeProcess;
            if (p is null || p.HasExited) return;

            try
            {
                _logger.LogWarning("[TestRunner] Killing active test run PID={Pid}", p.Id);
                p.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TestRunner] Kill failed");
            }
            finally
            {
                KillOrphanedTestHosts();
            }
        }
        public async Task<TestRunSummary> RunAsync(
            string? filter = null, CancellationToken ct = default)
        {
            // Write a TRX file so we get structured results alongside raw output
            var trxPath = Path.Combine(Path.GetTempPath(), $"testrun_{Guid.NewGuid():N}.trx");
            var args = BuildArgs(filter, trxPath);

            _logger.LogInformation("[TestRunner] dotnet test {Args}", args);

            var (exitCode, stdout, stderr) = await RunProcessAsync("dotnet", args, ct);
            var raw = string.IsNullOrWhiteSpace(stderr)
                ? stdout
                : $"{stdout}\n\nSTDERR:\n{stderr}";

            return ParseTrx(trxPath, raw)
                   ?? ParseConsoleOutput(exitCode, stdout, raw);
        }

        // ── Process ───────────────────────────────────────────────────────────────

        private string BuildArgs(string? filter, string trxPath)
        {
            var sb = new StringBuilder(
                $"test \"{TestProjectPath}\" --logger \"trx;LogFileName={trxPath}\" --logger \"console;verbosity=normal\" --no-build");
            if (!string.IsNullOrWhiteSpace(filter))
                sb.Append($" --filter \"{filter}\"");
            return sb.ToString();
        }

        private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
            string exe, string args, CancellationToken ct)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);
            return (process.ExitCode, stdout.ToString(), stderr.ToString());
        }

        // ── TRX parser (structured) ───────────────────────────────────────────────

        private TestRunSummary? ParseTrx(string trxPath, string raw)
        {
            try
            {
                if (!File.Exists(trxPath)) return null;

                var xml = XDocument.Load(trxPath);
                XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

                var results = xml
                    .Descendants(ns + "UnitTestResult")
                    .Select(r =>
                    {
                        var outcome = r.Attribute("outcome")?.Value ?? "Unknown";
                        var durationRaw = r.Attribute("duration")?.Value ?? "0";
                        var duration = TimeSpan.TryParse(durationRaw, out var ts)
                            ? ts.TotalMilliseconds
                            : 0;

                        var errorMessage = r
                            .Descendants(ns + "Message")
                            .FirstOrDefault()?.Value;

                        return new TestResult(
                            TestName: r.Attribute("testName")?.Value ?? "Unknown",
                            ClassName: r.Attribute("testName")?.Value?.Split('.').SkipLast(1).LastOrDefault() ?? "",
                            Status: outcome switch
                            {
                                "Passed" => TestStatus.Passed,
                                "Failed" => TestStatus.Failed,
                                "NotExecuted" => TestStatus.Skipped,
                                _ => TestStatus.Error
                            },
                            ErrorMessage: errorMessage,
                            DurationMs: duration);
                    })
                    .ToList();

                var counters = xml.Descendants(ns + "Counters").FirstOrDefault();
                int total = int.TryParse(counters?.Attribute("total")?.Value, out var t) ? t : results.Count;
                int passed = int.TryParse(counters?.Attribute("passed")?.Value, out var p) ? p : results.Count(r => r.Status == TestStatus.Passed);
                int failed = int.TryParse(counters?.Attribute("failed")?.Value, out var f) ? f : results.Count(r => r.Status == TestStatus.Failed);
                int skipped = total - passed - failed;
                double duration = results.Sum(r => r.DurationMs);

                File.Delete(trxPath);

                return new TestRunSummary(total, passed, failed, skipped, duration, results, raw);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TestRunner] TRX parse failed, falling back to console output");
                return null;
            }
        }

        // ── Console output fallback ───────────────────────────────────────────────

        private static TestRunSummary ParseConsoleOutput(int exitCode, string stdout, string raw)
        {
            var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var failures = new List<TestResult>();

            int passed = 0, failed = 0, skipped = 0, total = 0;
            double durationMs = 0;

            foreach (var line in lines)
            {
                // Capture individual failure names from console lines like:
                // "  Failed TestMethodName [120ms]"
                var failMatch = Regex.Match(line.TrimStart(), @"^Failed\s+(.+?)\s+\[", RegexOptions.IgnoreCase);
                if (failMatch.Success)
                {
                    failures.Add(new TestResult(
                        TestName: failMatch.Groups[1].Value,
                        ClassName: string.Empty,
                        Status: TestStatus.Failed,
                        ErrorMessage: null,
                        DurationMs: 0));
                }

                // Summary line: "Passed: 10, Failed: 2, Skipped: 1, Total: 13, Duration: 500ms"
                var summary = Regex.Match(line,
                    @"Passed:\s*(\d+).*Failed:\s*(\d+).*Skipped:\s*(\d+).*Total:\s*(\d+).*Duration:\s*([\d.]+)\s*(\w+)",
                    RegexOptions.IgnoreCase);

                if (!summary.Success) continue;

                passed = int.Parse(summary.Groups[1].Value);
                failed = int.Parse(summary.Groups[2].Value);
                skipped = int.Parse(summary.Groups[3].Value);
                total = int.Parse(summary.Groups[4].Value);

                var rawDur = double.Parse(summary.Groups[5].Value, CultureInfo.InvariantCulture);
                durationMs = summary.Groups[6].Value.ToLowerInvariant() switch
                {
                    "ms" => rawDur,
                    "s" => rawDur * 1_000,
                    "min" => rawDur * 60_000,
                    _ => rawDur
                };
            }

            return new TestRunSummary(total, passed, failed, skipped, durationMs, failures, raw);
        }
        private static void KillOrphanedTestHosts()
        {
            foreach (var name in new[] { "testhost", "testhost.x86" })
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(); p.Dispose(); }
                    catch { /* already gone */ }
                }
        }
    }
}
