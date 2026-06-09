using DevAssistant.Agent;
using DevAssistant.Core.Agent;
using DevAssistant.Models;
using DevAssistant.Services;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace DevAssistant.Web.Services
{
    /// <summary>
    /// Thin adapter between MVC controllers and Core services.
    /// No HTTP — calls Core directly via injected interfaces.
    /// </summary>
    public interface IAgentService
    {
        Task<HealthReport> GetHealthAsync(CancellationToken ct = default);

        Task<string> GetChatResponseAsync(string message, ChatHistory history, CancellationToken ct = default);

        // Files
        Task<FileBrowserViewModel> GetFilesAsync(string path = ".", CancellationToken ct = default);
        Task<FileContentResult> GetFileContentAsync(string path, CancellationToken ct = default);
        Task<bool> WriteFileAsync(string path, string content, CancellationToken ct = default);

        // Tests
        Task<TestRunSummary> RunTestsAsync(string? filter = null, CancellationToken ct = default);

        // Memory
        Task<MemoryViewModel> GetMemoryAsync(CancellationToken ct = default);
        Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(string query, int topK = 5, CancellationToken ct = default);
        Task<bool> AddMemoryAsync(string content, CancellationToken ct = default);
        Task<bool> DeleteMemoryAsync(string id, CancellationToken ct = default);

        bool IsTestRunning { get; }
        void CancelTestRun();

        Task<string> CollectResponseAsync(string message, string? systemPrompt, CancellationToken ct = default);
        IAsyncEnumerable<string> StreamChatAsync(string message, string? systemPrompt, CancellationToken ct = default);
    }

    public sealed class AgentService : IAgentService
    {
        private readonly IMessageRouter _router;
        private readonly WebEnvironmentHealthChecker _health;
        private readonly ILlmChatService _llm;
        private readonly ILogger<AgentService> _logger;
        private readonly IMemoryService _memory;
        private readonly IFileBrowserService _files;
        private readonly ITestRunnerService _tests;
        private readonly ChatHistory _chatHistory = new(); // for direct chat
        private readonly ChatHistory _agentHistory = new(); // for agent loop
        private readonly IAgentLoop _agentLoop;

        public AgentService(
             IMessageRouter router,
            WebEnvironmentHealthChecker health,
            ILlmChatService llm,
            IAgentLoop agentLoop,
            IMemoryService memory,
            IFileBrowserService files,
            ITestRunnerService tests,
            ILogger<AgentService> logger)
        {
            _router = router;
            _health = health;
            _llm = llm;
            _agentLoop = agentLoop;
            _memory = memory;
            _files = files;
            _tests = tests;
            _logger = logger;
        }
        // AgentService — add:
        public bool IsTestRunning => _tests.IsRunning;

        public void CancelTestRun()
        {
            _logger.LogWarning("[AgentService] CancelTestRun called");
            _tests.CancelRun();
        }
        public Task<HealthReport> GetHealthAsync(CancellationToken ct = default)
            => _health.RunAsync(ct);

        // For SSE streaming in the Chat controller
        public async IAsyncEnumerable<string> StreamChatAsync(
            string message,
            string? systemPrompt,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            _logger.LogInformation("[AgentService] Chat: {Msg}", message);

            var options = new AgentLoopOptions
            {
                MaxIterations = 10,
                MaxDuration = TimeSpan.FromMinutes(8),
                VerboseHistoryLogging = false,
                RequireWriteConfirmation = false
            };

            // Run the full think→call→observe loop
            var result = await _agentLoop.RunAsync(
                message, _chatHistory, options, progress: null, ct);

            // ── Log tool call audit trail ─────────────────────────────────────────
            _logger.LogInformation(
                "[AgentService] Loop complete — Iterations: {I} | ToolCalls: {T} | Stop: {S}",
                result.IterationsUsed, result.ToolCalls.Count, result.StopReason);

            foreach (var call in result.ToolCalls)
            {
                _logger.LogInformation(
                    "  [{I}] {Plugin}-{Fn} | Success:{Ok} | {Ms}ms",
                    call.Iteration, call.PluginName, call.FunctionName,
                    call.Succeeded, call.DurationMs);
            }

            // ── Warn if no WriteFile happened ─────────────────────────────────────
            var wroteFile = result.ToolCalls.Any(t => t.FunctionName == "WriteFile");
            if (!wroteFile && result.StopReason == "FinalResponse")
            {
                _logger.LogWarning(
                    "[AgentService] ⚠ Agent completed without calling WriteFile. " +
                    "No files were modified.");
            }

            // Stream the final response to browser in chunks
            var response = result.Success
                ? result.FinalResponse
                : $"⚠ {result.FinalResponse}";

            for (var i = 0; i < response.Length; i += 40)
            {
                ct.ThrowIfCancellationRequested();
                yield return response[i..Math.Min(i + 40, response.Length)];
                await Task.Delay(8, ct);
            }
        }

        public Task<string> GetChatResponseAsync(
            string message,
            ChatHistory history,
            CancellationToken ct = default)
            => _llm.StreamChatAsync(message, null, history, ct);

        // ── Files ────────────────────────────────────────────────────────────────

        public async Task<FileBrowserViewModel> GetFilesAsync(
            string path = ".", CancellationToken ct = default)
        {
            try
            {
                return await _files.GetFilesAsync(path, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] GetFiles failed for path {Path}", path);
                return new FileBrowserViewModel(path, [], null, null);
            }
        }

        public async Task<FileContentResult> GetFileContentAsync(
            string path, CancellationToken ct = default)
        {
            try
            {
                return await _files.GetFileContentAsync(path, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] GetFileContent failed for {Path}", path);
                return new FileContentResult(path, "", false, ex.Message);
            }
        }

        public async Task<bool> WriteFileAsync(
            string path, string content, CancellationToken ct = default)
        {
            try
            {
                return await _files.WriteFileAsync(path, content, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] WriteFile failed for {Path}", path);
                return false;
            }
        }

        // ── Tests ────────────────────────────────────────────────────────────────

        public async Task<TestRunSummary> RunTestsAsync(
            string? filter = null, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("[AgentService] RunTests filter={Filter}", filter);
                return await _tests.RunAsync(filter, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] RunTests failed");
                return new TestRunSummary(0, 0, 0, 0, 0, [], $"Error: {ex.Message}");
            }
        }

        // ── Memory ───────────────────────────────────────────────────────────────

        public async Task<MemoryViewModel> GetMemoryAsync(CancellationToken ct = default)
        {
            try
            {
                return await _memory.GetAllAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] GetMemory failed");
                return new MemoryViewModel([], null, null, 0);
            }
        }

        public async Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(
            string query, int topK = 5, CancellationToken ct = default)
        {
            try
            {
                return await _memory.SearchAsync(query, topK, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] SearchMemory failed");
                return [];
            }
        }

        public async Task<bool> AddMemoryAsync(string content, CancellationToken ct = default)
        {
            try
            {
                return await _memory.AddAsync(content, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] AddMemory failed");
                return false;
            }
        }

        public async Task<bool> DeleteMemoryAsync(string id, CancellationToken ct = default)
        {
            try
            {
                return await _memory.DeleteAsync(id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] DeleteMemory failed");
                return false;
            }
        }

        //public async Task<string> CollectResponseAsync(
        //string message,
        //string? systemPrompt,
        //CancellationToken ct = default)  // this ct comes from the background task
        //{
        //    _logger.LogInformation("[AgentService] CollectResponse: {Msg}", message);

        //    // ── Create an INDEPENDENT token — not linked to HTTP request lifetime ──
        //    // This means even if the caller cancels, the agent finishes its work
        //    using var agentCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        //    var options = new AgentLoopOptions
        //    {
        //        MaxIterations = 10,
        //        MaxDuration = TimeSpan.FromMinutes(8),
        //        VerboseHistoryLogging = false,
        //        RequireWriteConfirmation = false
        //    };

        //    _logger.LogInformation(
        //        "[AgentService] Starting agent loop with independent token " +
        //        "(timeout: 10min, not linked to HTTP request)");

        //    // Pass agentCts.Token NOT ct — fully decoupled from browser lifetime
        //    var result = await _agentLoop.RunAsync(
        //        message, _history, options, null, agentCts.Token);

        //    _logger.LogInformation(
        //        "[AgentService] Complete — Iterations: {I} | ToolCalls: {T} | Stop: {S}",
        //        result.IterationsUsed, result.ToolCalls.Count, result.StopReason);

        //    foreach (var call in result.ToolCalls)
        //        _logger.LogInformation(
        //            "  [{I}] {Fn} | Ok:{Ok} | {Ms}ms",
        //            call.Iteration, call.FunctionName, call.Succeeded, call.DurationMs);

        //    return result.Success
        //        ? result.FinalResponse
        //        : $"⚠ {result.FinalResponse}";
        //}
        public async Task<string> CollectResponseAsync(
        string message,
        string? systemPrompt,
        CancellationToken ct = default)
        {
            _logger.LogInformation("[AgentService] Message: {Msg}", message);

            var classification = _router.Classify(message);

            _logger.LogInformation(
                "[AgentService] Route → {Intent} ({Reason})",
                classification.Intent, classification.Reason);

            return classification.Intent switch
            {
                MessageIntent.DirectChat => await HandleDirectChatAsync(
                    message, systemPrompt, ct),

                MessageIntent.AgentTask => await HandleAgentTaskAsync(
                    message, ct),

                _ => await HandleDirectChatAsync(message, systemPrompt, ct)
            };
        }
        // ── Direct chat — fast, no tools ─────────────────────────────────────────────
        private async Task<string> HandleDirectChatAsync(
            string message,
            string? systemPrompt,
            CancellationToken ct)
        {
            _logger.LogInformation("[AgentService] DirectChat path");

            try
            {
                var response = await _llm.StreamChatAsync(
                    message, systemPrompt, _chatHistory, ct);

                _logger.LogInformation(
                    "[AgentService] DirectChat complete — {Len} chars", response.Length);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentService] DirectChat failed");
                return $"Sorry, I encountered an error: {ex.Message}";
            }
        }
        // ── Agent task — full loop with tools ────────────────────────────────────────
        private async Task<string> HandleAgentTaskAsync(
            string message,
            CancellationToken ct)
        {
            _logger.LogInformation("[AgentService] AgentTask path");

            using var agentCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

            var options = new AgentLoopOptions
            {
                MaxIterations = 10,
                MaxDuration = TimeSpan.FromMinutes(8),
                VerboseHistoryLogging = false,
                RequireWriteConfirmation = false
            };

            var result = await _agentLoop.RunAsync(
                message, _agentHistory, options, null, agentCts.Token);

            _logger.LogInformation(
                "[AgentService] AgentTask complete — " +
                "Iterations: {I} | ToolCalls: {T} | Stop: {S}",
                result.IterationsUsed, result.ToolCalls.Count, result.StopReason);

            return result.Success
                ? result.FinalResponse
                : result.FinalResponse; // BuildFallbackResponse already handles this
        }
    }
}
