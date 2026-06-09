using DevAssistant.Agent;
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
        IAsyncEnumerable<string> StreamChatAsync(string message, string? systemPrompt, CancellationToken ct = default);
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

    }

    public sealed class AgentService : IAgentService
    {
        private readonly WebEnvironmentHealthChecker _health;
        private readonly ILlmChatService _llm;
        private readonly ILogger<AgentService> _logger;
        private readonly IMemoryService _memory;
        private readonly IFileBrowserService _files;
        private readonly ITestRunnerService _tests;
        private readonly ChatHistory _history = new();
        private readonly IAgentLoop _agentLoop;

        public AgentService(
            WebEnvironmentHealthChecker health,
            ILlmChatService llm,
            IAgentLoop agentLoop,
            IMemoryService memory,
            IFileBrowserService files,
            ITestRunnerService tests,
            ILogger<AgentService> logger)
        {
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
                MaxDuration = TimeSpan.FromMinutes(3),
                VerboseHistoryLogging = true,
                RequireWriteConfirmation = false
            };

            // Run the full think→call→observe loop
            var result = await _agentLoop.RunAsync(
                message, _history, options, progress: null, ct);

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
    }
}
