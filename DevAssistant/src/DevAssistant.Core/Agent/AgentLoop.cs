#pragma warning disable SKEXP0070
using DevAssistant.Configuration;
using DevAssistant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text.Json;

namespace DevAssistant.Agent
{
    /// <summary>Progress updates streamed back to the UI during execution.</summary>
    public sealed record AgentLoopProgress(
        int Iteration,
        string Phase,        // "thinking" | "calling" | "observing" | "complete"
        string? ToolName = null,
        string? ToolArgs = null,
        string? Message = null);
    public interface IAgentLoop
    {
        /// <summary>
        /// Runs the full think→tool→observe loop until the LLM
        /// produces a final text response or the guard conditions trigger.
        /// </summary>
        Task<AgentExecutionResult> RunAsync(
            string userMessage,
            ChatHistory history,
            AgentLoopOptions? options = null,
            IProgress<AgentLoopProgress>? progress = null,
            CancellationToken ct = default);
    }
    public sealed class AgentLoop : IAgentLoop
    {
        private readonly Kernel _kernel;
        private readonly ILogger<AgentLoop> _logger;
        private readonly AgentOptions _agentOptions;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true
        };

        public AgentLoop(
            IKernelFactory kernelFactory,
            IOptions<AgentOptions> agentOptions,
            ILogger<AgentLoop> logger)
        {
            _kernel = kernelFactory.CreateKernel();
            _agentOptions = agentOptions.Value;
            _logger = logger;
        }

        public async Task<AgentExecutionResult> RunAsync(
            string userMessage,
            ChatHistory history,
            AgentLoopOptions? options = null,
            IProgress<AgentLoopProgress>? progress = null,
            CancellationToken ct = default)
        {
            options ??= new AgentLoopOptions
            {
                MaxIterations = _agentOptions.MaxIterations
            };

            var toolCalls = new List<ToolCallRecord>();
            var loopSw = Stopwatch.StartNew();
            var iteration = 0;
            var stopReason = "MaxResponse";

            // ── Seed history if empty ─────────────────────────────────────────────
            if (history.Count == 0)
            {
                history.AddSystemMessage(BuildSystemPrompt());
                _logger.LogDebug("[AgentLoop] System prompt added to history");
            }

            history.AddUserMessage(userMessage);

            _logger.LogInformation(
                "[AgentLoop] ═══ Starting loop ═══ " +
                "MaxIterations: {Max} | MaxDuration: {Dur} | User: {Msg}",
                options.MaxIterations, options.MaxDuration, userMessage);

            // ── Guard: total duration timeout ─────────────────────────────────────
            using var durationCts = new CancellationTokenSource(options.MaxDuration);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct, durationCts.Token);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            string finalResponse = string.Empty;

            // ══════════════════════════════════════════════════════════════════════
            // THE LOOP
            // ══════════════════════════════════════════════════════════════════════
            while (iteration < options.MaxIterations)
            {
                iteration++;

                _logger.LogInformation(
                    "[AgentLoop] ── Iteration {I}/{Max} ──────────────────────",
                    iteration, options.MaxIterations);

                // ── PHASE 1: THINK ────────────────────────────────────────────────
                progress?.Report(new AgentLoopProgress(
                    iteration, "thinking",
                    Message: $"Thinking... (iteration {iteration}/{options.MaxIterations})"));

                _logger.LogInformation("[AgentLoop] Phase: THINK");
                LogChatHistory(history, $"Before iteration {iteration}");

                //var settings = new OllamaPromptExecutionSettings
                //{
                //    Temperature = 0.3f,
                //    MaxTokens = 2048,
                //    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                //};
                // ✓ REPLACE WITH:
                var settings = new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.3,
                    MaxTokens = 2048,
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                // ── PHASE 2: CALL (SK handles this automatically) ─────────────────
                // SK sends history + tool definitions to Ollama.
                // If Ollama returns tool_calls, SK invokes them and appends results.
                // We get back the FINAL text response after all tool calls resolve.
                var iterSw = Stopwatch.StartNew();

                ChatMessageContent response;
                try
                {
                    // Register a filter to intercept tool calls before/after execution
                    // This lets us record every tool call for the audit trail
                    var filterKernel = _kernel;
                    var callRecorder = new ToolCallRecorder(iteration, toolCalls, _logger, progress);

                    // Use InvokePromptAsync for single-shot with auto tool invocation
                    // This handles the full think→call→observe internally per iteration
                    var results = await chatService.GetChatMessageContentsAsync(
                        history,
                        executionSettings: settings,
                        kernel: filterKernel,
                        cancellationToken: linkedCts.Token);

                    iterSw.Stop();
                    response = results.Last();
                }
                catch (OperationCanceledException) when (durationCts.IsCancellationRequested)
                {
                    stopReason = "DurationTimeout";
                    _logger.LogWarning(
                        "[AgentLoop] Duration timeout after {Dur}", loopSw.Elapsed);
                    break;
                }
                catch (OperationCanceledException)
                {
                    stopReason = "UserCancelled";
                    _logger.LogInformation("[AgentLoop] Cancelled by user");
                    break;
                }
                catch (Exception ex)
                {
                    stopReason = "Error";
                    _logger.LogError(ex, "[AgentLoop] Error on iteration {I}", iteration);

                    return new AgentExecutionResult(
                        Success: false,
                        FinalResponse: $"Agent error on iteration {iteration}: {ex.Message}",
                        IterationsUsed: iteration,
                        Duration: loopSw.Elapsed,
                        ToolCalls: toolCalls,
                        StopReason: "Error",
                        Error: ex.Message);
                }

                iterSw.Stop();

                // ── PHASE 3: OBSERVE ──────────────────────────────────────────────
                progress?.Report(new AgentLoopProgress(
                    iteration, "observing",
                    Message: response.Content?.Length > 0
                        ? $"Got response ({response.Content.Length} chars)"
                        : "Tool call executed"));

                _logger.LogInformation(
                    "[AgentLoop] Phase: OBSERVE | Role: {Role} | " +
                    "ContentLength: {Len} | IterMs: {Ms}",
                    response.Role,
                    response.Content?.Length ?? 0,
                    iterSw.ElapsedMilliseconds);

                // Add response to history
                history.Add(response);

                // ── CHECK: Is this a final text response? ─────────────────────────
                // If the response has content and no pending tool calls,
                // the LLM is done — exit the loop.
                if (!string.IsNullOrWhiteSpace(response.Content))
                {
                    finalResponse = response.Content;
                    stopReason = "FinalResponse";

                    _logger.LogInformation(
                        "[AgentLoop] ✓ Final response received on iteration {I}. " +
                        "Length: {Len}", iteration, finalResponse.Length);
                    break;
                }

                // ── GUARD: If no content and no tool calls, something is wrong ────
                if (response.Content is null || response.Content.Length == 0)
                {
                    _logger.LogWarning(
                        "[AgentLoop] Empty response on iteration {I} — " +
                        "LLM returned neither text nor tool call. Breaking.",
                        iteration);
                    stopReason = "EmptyResponse";
                    break;
                }
            }

            // ── Max iterations hit ────────────────────────────────────────────────
            if (iteration >= options.MaxIterations && string.IsNullOrEmpty(finalResponse))
            {
                stopReason = "MaxIterationsReached";
                finalResponse = $"Agent reached maximum iterations ({options.MaxIterations}) " +
                                $"without completing. Last tool calls: " +
                                $"{string.Join(", ", toolCalls.TakeLast(3).Select(t => t.FunctionName))}. " +
                                $"Try a more specific request.";

                _logger.LogWarning(
                    "[AgentLoop] Max iterations {Max} reached without final response",
                    options.MaxIterations);
            }

            loopSw.Stop();

            // ── Final summary log ─────────────────────────────────────────────────
            _logger.LogInformation(
                "[AgentLoop] ═══ Complete ═══ " +
                "Iterations: {I} | ToolCalls: {T} | Duration: {D}ms | Stop: {S}",
                iteration,
                toolCalls.Count,
                loopSw.ElapsedMilliseconds,
                stopReason);

            LogChatHistory(history, "Final History");

            progress?.Report(new AgentLoopProgress(
                iteration, "complete", Message: finalResponse));

            return new AgentExecutionResult(
                Success: stopReason == "FinalResponse",
                FinalResponse: finalResponse,
                IterationsUsed: iteration,
                Duration: loopSw.Elapsed,
                ToolCalls: toolCalls,
                StopReason: stopReason);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static string BuildSystemPrompt() =>
             """
             You are a senior .NET developer assistant with access to file and test tools.

             AVAILABLE TOOLS:
             - ListFiles(path)       → see what files exist in a directory
             - ReadFile(path)        → read the content of a specific file
             - WriteFile(path, content) → save fixed code back to disk
             - RunTests(filter?)     → run dotnet tests and see which fail
             - GetTestFailureDetails(testName) → deep dive on one failing test

             CHAIN OF THOUGHT RULES — always follow this order:
             1. If you don't know what files exist → call ListFiles FIRST
             2. If asked about code → call ReadFile before answering
             3. If asked to fix bugs → call RunTests FIRST to see what fails
             4. After reading failing tests → call ReadFile on the failing source file
             5. After writing a fix → call RunTests again to confirm fix works
             6. NEVER guess file content — always read it first
             7. NEVER fabricate test output — always run tests to get real results
             8. When all tests pass → give a clear summary of what you fixed

             RESPONSE FORMAT after completing work:
             - State what was broken (with test names)
             - State what you changed (file + line)
             - Confirm tests now pass
             """;

        private void LogChatHistory(ChatHistory history, string label)
        {
            if (!_logger.IsEnabled(LogLevel.Debug)) return;

            _logger.LogDebug("[AgentLoop] ── ChatHistory: {Label} ({N} messages) ──",
                label, history.Count);

            for (var i = 0; i < history.Count; i++)
            {
                var msg = history[i];
                var preview = msg.Content?.Length > 100
                    ? msg.Content[..100] + "…"
                    : msg.Content ?? "(null — tool call)";

                _logger.LogDebug(
                    "  [{I:00}] {Role,-14} | {Len,5} chars | {Preview}",
                    i, msg.Role.ToString(), msg.Content?.Length ?? 0, preview);
            }
        }
    }

    // ── Tool Call Recorder ────────────────────────────────────────────────────────
    // Intercepts tool calls to build the audit trail shown in the UI

    internal sealed class ToolCallRecorder
    {
        private readonly int _iteration;
        private readonly List<ToolCallRecord> _records;
        private readonly ILogger _logger;
        private readonly IProgress<AgentLoopProgress>? _progress;

        public ToolCallRecorder(
            int iteration,
            List<ToolCallRecord> records,
            ILogger logger,
            IProgress<AgentLoopProgress>? progress)
        {
            _iteration = iteration;
            _records = records;
            _logger = logger;
            _progress = progress;
        }

        public void RecordCall(
            string pluginName,
            string functionName,
            string arguments,
            string result,
            bool succeeded,
            long durationMs)
        {
            var record = new ToolCallRecord(
                _iteration, pluginName, functionName,
                arguments, result, succeeded, durationMs);

            _records.Add(record);

            _progress?.Report(new AgentLoopProgress(
                _iteration, "calling",
                ToolName: $"{pluginName}-{functionName}",
                ToolArgs: arguments,
                Message: $"Called {functionName} ({durationMs}ms)"));

            _logger.LogInformation(
                "[ToolCall] {Plugin}-{Fn} | Args: {Args} | " +
                "Success: {Ok} | DurationMs: {Ms} | ResultLen: {Len}",
                pluginName, functionName,
                arguments.Length > 80 ? arguments[..80] + "…" : arguments,
                succeeded, durationMs, result.Length);
        }
    }
}
