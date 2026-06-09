using DevAssistant.Configuration;
using DevAssistant.Core.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;


namespace DevAssistant.Plugins
{
    public sealed class TestPlugin
    {
        private readonly AgentOptions _options;
        private readonly ILogger<TestPlugin> _logger;

        // Timeout for dotnet test — prevents agent hanging on infinite loops in tests
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(120);

        public TestPlugin(IOptions<AgentOptions> options, ILogger<TestPlugin> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        // ── RunTests ──────────────────────────────────────────────────────────────

        [KernelFunction("RunTests")]
        [Description(
            "Runs dotnet test on the workspace test project and returns structured results. " +
            "Shows which tests passed, failed, and the exact failure messages with stack traces. " +
            "Use this to verify code is correct before and after making fixes. " +
            "Optionally filter to run only specific tests by name.")]
        public async Task<string> RunTestsAsync(
            [Description(
            "Optional test filter expression. " +
            "Examples: 'OrderServiceTests' runs all tests in that class. " +
            "'CreateOrder_NullCustomerId' runs one specific test. " +
            "Leave empty to run all tests.")]
        string? filter = null,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "[TestPlugin.RunTests] ToolCall Start — Filter: {Filter}",
                filter ?? "(all)");

            try
            {
                // ── 1. Resolve workspace path ────────────────────────────────────
                var workspacePath = Path.GetFullPath(_options.WorkingDirectory);

                // ── 2. Find the test project ─────────────────────────────────────
                var testProject = FindTestProject(workspacePath);
                if (testProject is null)
                {
                    var msg = $"No test project (.csproj with IsTestProject=true or " +
                              $"xunit reference) found under '{workspacePath}'. " +
                              $"Expected at: workspace/tests/*.csproj";
                    _logger.LogWarning("[TestPlugin.RunTests] {Msg}", msg);
                    return ToolResult.Failure(msg, "Create a test project under workspace/tests/");
                }

                _logger.LogInformation(
                    "[TestPlugin.RunTests] Found test project: {Project}", testProject);

                // ── 3. Build the dotnet test command ─────────────────────────────
                var args = BuildTestArgs(testProject, filter);

                _logger.LogInformation(
                    "[TestPlugin.RunTests] Executing: dotnet {Args}", args);

                // ── 4. Run the process ───────────────────────────────────────────
                var (stdout, stderr, exitCode) = await RunProcessAsync(
                    "dotnet", args, workspacePath,
                    TestTimeout, cancellationToken);

                sw.Stop();

                _logger.LogInformation(
                    "[TestPlugin.RunTests] Process complete — " +
                    "ExitCode: {Code} | DurationMs: {Ms}",
                    exitCode, sw.ElapsedMilliseconds);

                // ── 5. Parse the output ──────────────────────────────────────────
                var parsed = ParseTestOutput(stdout + stderr, exitCode);

                _logger.LogInformation(
                    "[TestPlugin.RunTests] ToolCall Complete — " +
                    "Total: {T} | Passed: {P} | Failed: {F} | DurationMs: {Ms}",
                    parsed.Total, parsed.Passed, parsed.Failed, sw.ElapsedMilliseconds);

                return ToolResult.Success(new
                {
                    summary = new
                    {
                        total = parsed.Total,
                        passed = parsed.Passed,
                        failed = parsed.Failed,
                        skipped = parsed.Skipped,
                        durationMs = sw.ElapsedMilliseconds,
                        allPassed = parsed.Failed == 0
                    },
                    failures = parsed.Failures,
                    rawOutput = parsed.RawOutput.Length > 3000
                        ? parsed.RawOutput[..3000] + "\n...(truncated)"
                        : parsed.RawOutput
                });
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning(
                    "[TestPlugin.RunTests] Cancelled after {Ms}ms", sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[TestPlugin.RunTests] Error after {Ms}ms", sw.ElapsedMilliseconds);
                return ToolResult.Failure($"Error running tests: {ex.Message}");
            }
        }

        // ── GetTestFailureDetails ─────────────────────────────────────────────────

        [KernelFunction("GetTestFailureDetails")]
        [Description(
            "Returns detailed information about a specific failing test including " +
            "the full stack trace, expected vs actual values, and the line number " +
            "where it failed. Use this after RunTests shows failures to understand " +
            "exactly what needs to be fixed.")]
        public async Task<string> GetTestFailureDetailsAsync(
            [Description(
            "The exact test method name to get details for. " +
            "Example: 'CreateOrder_NullCustomerId_ThrowsArgumentNullException'")]
        string testName,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[TestPlugin.GetTestFailureDetails] Test: {Test}", testName);

            // Run with verbose output for just this test
            return await RunTestsAsync(testName, cancellationToken);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static string? FindTestProject(string workspacePath)
        {
            // Look for .csproj files that reference xunit or have IsTestProject
            var csprojFiles = Directory.GetFiles(
                workspacePath, "*.csproj", SearchOption.AllDirectories);

            foreach (var file in csprojFiles)
            {
                var content = File.ReadAllText(file);
                if (content.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("IsTestProject", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("MSTest", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("NUnit", StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            return null;
        }

        private static string BuildTestArgs(string projectPath, string? filter)
        {
            var sb = new StringBuilder();
            sb.Append($"test \"{projectPath}\"");
            sb.Append(" --logger \"console;verbosity=detailed\"");
            sb.Append(" --no-interaction");

            if (!string.IsNullOrWhiteSpace(filter))
                sb.Append($" --filter \"{filter}\"");

            return sb.ToString();
        }

        private static async Task<(string Stdout, string Stderr, int ExitCode)> RunProcessAsync(
            string command,
            string args,
            string workingDir,
            TimeSpan timeout,
            CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };

            var stdoutSb = new StringBuilder();
            var stderrSb = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) stdoutSb.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) stderrSb.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            return (stdoutSb.ToString(), stderrSb.ToString(), process.ExitCode);
        }

        private static TestParseResult ParseTestOutput(string raw, int exitCode)
        {
            var failures = new List<TestFailure>();

            // ── Parse individual test failures ────────────────────────────────────
            // dotnet test output format:
            // Failed  TestName [Xms]
            //   Error Message:
            //    Expected type: System.ArgumentNullException ...
            //   Stack Trace:
            //    at OrderService.CreateOrder(...) in OrderService.cs:line 12
            var failureBlocks = Regex.Split(raw, @"\bFailed\b\s+")
                .Skip(1); // first element is before any "Failed"

            foreach (var block in failureBlocks)
            {
                var lines = block.Split('\n');
                var testName = lines[0].Trim().Split(' ')[0]; // name before timing
                var errorMsg = ExtractSection(block, "Error Message:", "Stack Trace:");
                var stackTrace = ExtractSection(block, "Stack Trace:", "  Failed");

                failures.Add(new TestFailure(
                    TestName: testName,
                    Error: errorMsg.Trim(),
                    StackTrace: stackTrace.Trim()));
            }

            // ── Parse summary line ────────────────────────────────────────────────
            // "Failed!  - Failed:     3, Passed:     1, Skipped:     0, Total:     4"
            var summaryMatch = Regex.Match(raw,
                @"Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)");

            int failed = 0, passed = 0, skipped = 0, total = 0;

            if (summaryMatch.Success)
            {
                failed = int.Parse(summaryMatch.Groups[1].Value);
                passed = int.Parse(summaryMatch.Groups[2].Value);
                skipped = int.Parse(summaryMatch.Groups[3].Value);
                total = int.Parse(summaryMatch.Groups[4].Value);
            }
            else
            {
                // Fallback: if exit code 0, all passed
                var passMatch = Regex.Match(raw, @"Passed:\s*(\d+)");
                if (passMatch.Success) passed = int.Parse(passMatch.Groups[1].Value);
                total = passed;
            }

            return new TestParseResult(
                Total: total,
                Passed: passed,
                Failed: failed,
                Skipped: skipped,
                Failures: failures,
                RawOutput: raw);
        }

        private static string ExtractSection(string text, string startMarker, string endMarker)
        {
            var start = text.IndexOf(startMarker, StringComparison.Ordinal);
            if (start < 0) return string.Empty;

            start += startMarker.Length;
            var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);

            return end < 0
                ? text[start..]
                : text[start..end];
        }

        // ── Result types ──────────────────────────────────────────────────────────

        private sealed record TestParseResult(
            int Total,
            int Passed,
            int Failed,
            int Skipped,
            List<TestFailure> Failures,
            string RawOutput);

        private sealed record TestFailure(
            string TestName,
            string Error,
            string StackTrace);
    }
}
