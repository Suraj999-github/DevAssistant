#pragma warning disable SKEXP0070

using DevAssistant.Agent;
using DevAssistant.Configuration;
using DevAssistant.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text;

namespace DevAssistant.Core.Agent;
public sealed record AgentLoopProgress(
    int Iteration,
    string Phase,        // "thinking" | "calling" | "observing" | "complete"
    string? ToolName = null,
    string? ToolArgs = null,
    string? Message = null);
public interface IAgentLoop
{
    Task<AgentExecutionResult> RunAsync(
        string userMessage,
        ChatHistory history,
        AgentLoopOptions? options = null,
        IProgress<AgentLoopProgress>? progress = null,
        CancellationToken ct = default);
}

public sealed class AgentLoop : IAgentLoop
{
    private readonly IKernelFactory _kernelFactory;
    private readonly AgentOptions _agentOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentLoop> _logger;

    public AgentLoop(
        IKernelFactory kernelFactory,
        IOptions<AgentOptions> agentOptions,
        ILoggerFactory loggerFactory,
        ILogger<AgentLoop> logger)
    {
        _kernelFactory = kernelFactory;
        _agentOptions = agentOptions.Value;
        _loggerFactory = loggerFactory;
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

        // ── State ─────────────────────────────────────────────────────────────
        var toolCalls = new List<ToolCallRecord>();
        var loopSw = Stopwatch.StartNew();
        var iteration = 0;
        var stopReason = "Unknown";
        var finalResponse = string.Empty;

        // Collects the last meaningful content seen — used as fallback response
        var lastSeenContent = string.Empty;
        var lastException = string.Empty;

        // ── Kernel + per-loop filter ──────────────────────────────────────────
        var filter = new ToolCallLoggingFilter(toolCalls, _loggerFactory);
        var kernel = _kernelFactory.CreateKernel();
        kernel.FunctionInvocationFilters.Clear();
        kernel.FunctionInvocationFilters.Add(filter);

        _logger.LogInformation(
            "[AgentLoop] Setup | Filters: {F} | Plugins: {P} | Tools: {T} | Model: {M}",
            kernel.FunctionInvocationFilters.Count,
            kernel.Plugins.Count(),
            kernel.Plugins.SelectMany(p => p).Count(),
            _agentOptions.ModelId);

        // ── Seed history ──────────────────────────────────────────────────────
        if (history.Count == 0)
        {
            history.AddSystemMessage(BuildSystemPrompt());
        }
        history.AddUserMessage(userMessage);

        _logger.LogInformation(
            "[AgentLoop] ═══ Starting ═══ MaxIterations: {Max} | MaxDuration: {Dur}",
            options.MaxIterations, options.MaxDuration);

        // ── Duration guard — independent of caller token ──────────────────────
        using var loopCts = new CancellationTokenSource(options.MaxDuration);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // ══════════════════════════════════════════════════════════════════════
        // MAIN LOOP
        // ══════════════════════════════════════════════════════════════════════
        try
        {
            while (iteration < options.MaxIterations)
            {
                iteration++;
                filter.SetIteration(iteration);

                _logger.LogInformation(
                    "[AgentLoop] ── Iteration {I}/{Max} | ToolCalls so far: {T} ──",
                    iteration, options.MaxIterations, toolCalls.Count);

                progress?.Report(new AgentLoopProgress(
                    iteration, "thinking",
                    Message: $"Thinking... ({iteration}/{options.MaxIterations})"));

                var settings = new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.1,
                    MaxTokens = 800,
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                // Per-iteration timeout — prevents one call hanging forever
                using var iterCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    loopCts.Token, iterCts.Token);

                var iterSw = Stopwatch.StartNew();
                ChatMessageContent response;

                try
                {
                    var results = await chatService.GetChatMessageContentsAsync(
                        history,
                        executionSettings: settings,
                        kernel: kernel,
                        cancellationToken: combinedCts.Token);

                    iterSw.Stop();
                    response = results.Last();

                    // Log raw response for diagnostics
                    _logger.LogInformation(
                        "[AgentLoop] RAW | Role: {Role} | ContentLen: {Len} | " +
                        "Items: {Items} | ToolCallsRecorded: {T} | IterMs: {Ms}",
                        response.Role,
                        response.Content?.Length ?? 0,
                        response.Items?.Count ?? 0,
                        toolCalls.Count,
                        iterSw.ElapsedMilliseconds);

                    if (!string.IsNullOrWhiteSpace(response.Content))
                        lastSeenContent = response.Content;
                }
                catch (Exception ex) when (IsTimeout(ex, loopCts, iterCts))
                {
                    iterSw.Stop();
                    stopReason = loopCts.IsCancellationRequested
                        ? "DurationTimeout" : "IterationTimeout";
                    lastException = $"Timeout after {iterSw.ElapsedMilliseconds}ms on iteration {iteration}";

                    _logger.LogWarning(
                        "[AgentLoop] ⏱ {Reason} on iteration {I} after {Ms}ms",
                        stopReason, iteration, iterSw.ElapsedMilliseconds);
                    break;
                }
                catch (Exception ex)
                {
                    iterSw.Stop();
                    stopReason = "Error";
                    lastException = $"{ex.GetType().Name}: {ex.Message}";

                    _logger.LogError(ex,
                        "[AgentLoop] ✗ Error on iteration {I} after {Ms}ms",
                        iteration, iterSw.ElapsedMilliseconds);
                    break;  // ← break not return — we still build a response below
                }

                // ── Add to history ────────────────────────────────────────────
                history.Add(response);

                progress?.Report(new AgentLoopProgress(
                    iteration, "observing",
                    Message: $"Processing response ({response.Content?.Length ?? 0} chars)"));

                // ── Check for final text response ─────────────────────────────
                if (!string.IsNullOrWhiteSpace(response.Content))
                {
                    var guardResult = CheckHallucinationGuards(
                        response.Content, toolCalls, history, iteration);

                    if (guardResult.ShouldContinue)
                    {
                        _logger.LogWarning(
                            "[AgentLoop] ⚠ Guard {G} triggered — forcing next iteration",
                            guardResult.GuardName);
                        continue;
                    }

                    // All guards passed — accept as final
                    finalResponse = response.Content;
                    stopReason = "FinalResponse";
                    _logger.LogInformation(
                        "[AgentLoop] ✓ Final response accepted on iteration {I}",
                        iteration);
                    break;
                }

                // Empty content = tool calls executed, loop naturally continues
                _logger.LogDebug(
                    "[AgentLoop] Empty content on iteration {I} — " +
                    "{T} tool calls executed, continuing",
                    iteration, toolCalls.Count);
            }

            // Max iterations hit
            if (iteration >= options.MaxIterations && string.IsNullOrEmpty(finalResponse))
            {
                stopReason = "MaxIterationsReached";
                _logger.LogWarning(
                    "[AgentLoop] Max iterations {Max} reached", options.MaxIterations);
            }
        }
        catch (Exception ex)
        {
            // Outermost catch — should never reach here but just in case
            stopReason = "UnhandledError";
            lastException = ex.Message;
            _logger.LogError(ex, "[AgentLoop] Unhandled error in loop");
        }
        finally
        {
            loopSw.Stop();
        }

        // ══════════════════════════════════════════════════════════════════════
        // BUILD FINAL RESPONSE — every exit path lands here
        // ══════════════════════════════════════════════════════════════════════
        if (string.IsNullOrEmpty(finalResponse))
        {
            finalResponse = BuildFallbackResponse(
                stopReason, iteration, options.MaxIterations,
                toolCalls, lastSeenContent, lastException, loopSw.Elapsed);
        }

        // ── Audit log ─────────────────────────────────────────────────────────
        _logger.LogInformation(
            "[AgentLoop] ═══ Complete ═══ " +
            "Iterations: {I} | ToolCalls: {T} | Duration: {D}ms | Stop: {S}",
            iteration, toolCalls.Count, loopSw.ElapsedMilliseconds, stopReason);

        for (var i = 0; i < toolCalls.Count; i++)
        {
            var tc = toolCalls[i];
            _logger.LogInformation(
                "  [{I:00}] Iter{Iter} {Plugin}.{Fn} | Ok:{Ok} | {Ms}ms",
                i, tc.Iteration, tc.PluginName, tc.FunctionName,
                tc.Succeeded, tc.DurationMs);
        }

        var result = new AgentExecutionResult(
            Success: stopReason == "FinalResponse",
            FinalResponse: finalResponse,
            IterationsUsed: iteration,
            Duration: loopSw.Elapsed,
            ToolCalls: toolCalls,
            StopReason: stopReason);

        _logger.LogInformation(
            "[AgentLoop] Result: Success={S} | Stop={R} | ResponseLen={L}",
            result.Success, result.StopReason, result.FinalResponse.Length);

        progress?.Report(new AgentLoopProgress(
            iteration, "complete", Message: finalResponse));

        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FALLBACK RESPONSE BUILDER
    // Every non-success exit produces a human-readable UI message
    // ══════════════════════════════════════════════════════════════════════════
    private static string BuildFallbackResponse(
        string stopReason,
        int iteration,
        int maxIterations,
        List<ToolCallRecord> toolCalls,
        string lastSeenContent,
        string lastException,
        TimeSpan elapsed)
    {
        var sb = new StringBuilder();

        // ── Header ────────────────────────────────────────────────────────────
        sb.AppendLine(stopReason switch
        {
            "DurationTimeout" => "⏱ **Agent timed out** — the operation took too long.",
            "IterationTimeout" => "⏱ **Iteration timed out** — one LLM call exceeded the time limit.",
            "MaxIterationsReached" => $"🔄 **Max iterations reached** ({maxIterations}) without completing.",
            "Error" => "❌ **Agent encountered an error.**",
            "UnhandledError" => "❌ **Unexpected agent error.**",
            "UserCancelled" => "🛑 **Request was cancelled.**",
            _ => "⚠ **Agent stopped unexpectedly.**"
        });

        sb.AppendLine();

        // ── What was accomplished ─────────────────────────────────────────────
        if (toolCalls.Count > 0)
        {
            sb.AppendLine("**What was completed before stopping:**");
            foreach (var tc in toolCalls)
            {
                var status = tc.Succeeded ? "✓" : "✗";
                sb.AppendLine($"  {status} {tc.FunctionName} (iteration {tc.Iteration}, {tc.DurationMs}ms)");
            }
            sb.AppendLine();
        }

        // ── Last content seen ─────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(lastSeenContent))
        {
            sb.AppendLine("**Last response from agent:**");
            sb.AppendLine(lastSeenContent.Length > 500
                ? lastSeenContent[..500] + "…"
                : lastSeenContent);
            sb.AppendLine();
        }

        // ── Error detail ──────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(lastException))
        {
            sb.AppendLine($"**Error detail:** `{lastException}`");
            sb.AppendLine();
        }

        // ── Stats ─────────────────────────────────────────────────────────────
        sb.AppendLine($"**Stats:** {iteration} iterations | " +
                      $"{toolCalls.Count} tool calls | " +
                      $"{elapsed.TotalSeconds:F1}s elapsed");

        // ── Suggestions ───────────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine(stopReason switch
        {
            "DurationTimeout" or "IterationTimeout" =>
                "💡 **Try:** Use a faster model (`llama3.1`, `qwen2.5-coder:7b`) " +
                "or ask a more specific question.",
            "MaxIterationsReached" =>
                "💡 **Try:** Break the task into smaller steps.",
            "Error" =>
                "💡 **Try:** Check the console logs for details, then retry.",
            _ => "💡 **Try:** Rephrasing your request or checking service health."
        });

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HALLUCINATION GUARDS
    // ══════════════════════════════════════════════════════════════════════════
    private (bool ShouldContinue, string GuardName) CheckHallucinationGuards(
        string content,
        List<ToolCallRecord> toolCalls,
        ChatHistory history,
        int iteration)
    {
        var claimsSuccess = ContainsAny(content,
            "pass", "fixed", "resolved", "working", "corrected", "all tests");

        var writeRecords = toolCalls.Where(t => t.FunctionName == "WriteFile").ToList();
        var testRecords = toolCalls.Where(t => t.FunctionName == "RunTests").ToList();

        var lastWriteIdx = writeRecords.Count > 0
            ? toolCalls.IndexOf(writeRecords.Last()) : -1;
        var lastTestIdx = testRecords.Count > 0
            ? toolCalls.IndexOf(testRecords.Last()) : -1;

        var testsAfterWrite = lastTestIdx > lastWriteIdx && lastWriteIdx >= 0;

        // Guard A: claimed fix but never wrote
        if (claimsSuccess && writeRecords.Count == 0)
        {
            history.AddUserMessage(
                "IMPORTANT: You described a fix but never called WriteFile. " +
                "The source file on disk is UNCHANGED. " +
                "Call WriteFile with the complete corrected file content now.");
            return (true, "A:NoWrite");
        }

        // Guard B: wrote but didn't verify
        if (claimsSuccess && writeRecords.Count > 0 && !testsAfterWrite)
        {
            history.AddUserMessage(
                "You wrote the file but did not run tests to verify. " +
                "Call RunTests NOW.");
            return (true, "B:NoVerify");
        }

        // Guard C: last test run still failing
        var lastTest = testRecords.LastOrDefault();
        if (lastTest is not null && claimsSuccess)
        {
            var stillFailing = lastTest.Result.Contains("\"failed\":") &&
                               !lastTest.Result.Contains("\"failed\":0");
            if (stillFailing)
            {
                history.AddUserMessage(
                    "The last test run still shows failures. " +
                    "Read the source file again, fix remaining issues, " +
                    "call WriteFile, then RunTests again.");
                return (true, "C:StillFailing");
            }
        }

        return (false, string.Empty);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════════
    private static bool IsTimeout(
        Exception ex,
        CancellationTokenSource loopCts,
        CancellationTokenSource iterCts)
        => ex is OperationCanceledException
           && (loopCts.IsCancellationRequested || iterCts.IsCancellationRequested);

    private static bool ContainsAny(string text, params string[] words)
        => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static string BuildSystemPrompt() =>
        """
        You are a senior .NET developer assistant with access to file and test tools.

        AVAILABLE TOOLS:
        - ListFiles(relativePath)            → list files in a directory
        - ReadFile(relativePath)             → read a file's content
        - WriteFile(relativePath, content)   → write complete file to disk
        - RunTests(filter?)                  → run dotnet tests, see results
        - GetTestFailureDetails(testName)    → deep dive on one failing test

        MANDATORY BUG-FIX WORKFLOW — follow exactly in this order:
        1. RunTests()                → identify which tests fail and why
        2. ReadFile(sourceFile)      → read the actual source code
        3. WriteFile(sourceFile, X)  → write the COMPLETE corrected file
        4. RunTests()                → verify all tests now pass
        5. Report what you changed   → only after tests confirm success

        ABSOLUTE RULES:
        - You MUST call WriteFile to change any file. Describing a fix changes nothing.
        - You MUST call RunTests after WriteFile to verify the fix.
        - Never claim tests pass without calling RunTests to confirm.
        - Always write the COMPLETE file content to WriteFile, not just the changed lines.
        """;
}
