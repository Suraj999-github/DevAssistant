using DevAssistant.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text;

namespace DevAssistant.Services
{
    /// <summary>
    /// Wraps the Semantic Kernel chat completion service.
    /// Handles streaming, logging of every token, and chat history management.
    /// </summary>
    public interface ILlmChatService
    {
        /// <summary>
        /// Sends a single prompt and streams the response to console.
        /// Returns the complete assembled response text.
        /// </summary>
        Task<string> StreamChatAsync(
            string userMessage,
            string? systemPrompt = null,
            ChatHistory? existingHistory = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Dumps the full serialized ChatHistory to the log so you can
        /// see exactly what the LLM sees on each turn.
        /// </summary>
        void LogChatHistory(ChatHistory history, string label = "ChatHistory");
    }

    public sealed class LlmChatService : ILlmChatService
    {
        private readonly Kernel _kernel;
        private readonly AgentOptions _options;
        private readonly ILogger<LlmChatService> _logger;

        // System prompt used for all Step-2 interactions
        private const string DefaultSystemPrompt =
            "You are a senior .NET developer assistant. " +
            "Be concise, precise, and always prefer working C# code over explanation alone.";

        public LlmChatService(
            IKernelFactory kernelFactory,
            IOptions<AgentOptions> options,
            ILogger<LlmChatService> logger)
        {
            _kernel = kernelFactory.CreateKernel();
            _options = options.Value;
            _logger = logger;
        }

        public async Task<string> StreamChatAsync(
            string userMessage,
            string? systemPrompt = null,
            ChatHistory? existingHistory = null,
            CancellationToken cancellationToken = default)
        {
            // ── 1. Build or extend ChatHistory ──────────────────────────────────
            var history = existingHistory ?? new ChatHistory();

            if (history.Count == 0)
            {
                var sysPrompt = systemPrompt ?? DefaultSystemPrompt;
                history.AddSystemMessage(sysPrompt);
                _logger.LogDebug(
                    "[LLM] System prompt set ({Length} chars): {Prompt}",
                    sysPrompt.Length, sysPrompt);
            }

            history.AddUserMessage(userMessage);

            // ── 2. Log the exact request we're about to send ────────────────────
            LogLlmRequest(history);

            // ── 3. Configure execution settings ─────────────────────────────────
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.7,
                MaxTokens = 2048,

                /*
                 * ToolCallBehavior.AutoInvokeKernelFunctions will be set in Step 4
                 * when we add tools. For now, no tools → pure completion.
                 */
            };

            // ── 4. Get the chat completion service from the kernel ───────────────
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            // ── 5. Stream the response ───────────────────────────────────────────
            var sw = Stopwatch.StartNew();
            var tokenCount = 0;
            var responseBuilder = new StringBuilder();

            _logger.LogInformation(
                "[LLM] Streaming request → Model: {Model}, Endpoint: {Endpoint}",
                _options.ModelId, _options.OllamaEndpoint);

            Console.WriteLine();
            Console.Write("\u001b[36mAssistant:\u001b[0m "); // Cyan prefix

            try
            {
                await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                                   history,
                                   executionSettings: executionSettings,
                                   kernel: _kernel,
                                   cancellationToken: cancellationToken))
                {
                    if (chunk.Content is null) continue;

                    // Write token to console immediately (streaming UX)
                    Console.Write(chunk.Content);
                    responseBuilder.Append(chunk.Content);
                    tokenCount++;

                    // Debug-level per-token log (only visible in Debug mode)
                    _logger.LogTrace(
                        "[LLM] Token #{Index}: '{Token}'",
                        tokenCount, chunk.Content);
                }

                Console.WriteLine("\n");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[LLM] HTTP error calling Ollama after {ElapsedMs}ms — " +
                    "Is 'ollama serve' running? Model '{Model}' pulled?",
                    sw.ElapsedMilliseconds, _options.ModelId);
                throw;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning(
                    "[LLM] Request cancelled after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[LLM] Unexpected error during streaming");
                throw;
            }

            sw.Stop();

            // ── 6. Log the response metadata ────────────────────────────────────
            var fullResponse = responseBuilder.ToString();
            _logger.LogInformation(
                "[LLM] Stream complete — DurationMs: {DurationMs}, " +
                "Chunks: {TokenCount}, ResponseLength: {Length}",
                sw.ElapsedMilliseconds, tokenCount, fullResponse.Length);

            // ── 7. Add assistant response to history for multi-turn continuity ──
            history.AddAssistantMessage(fullResponse);

            // ── 8. Log the updated history so you can see full context ──────────
            LogChatHistory(history, "After Response");

            return fullResponse;
        }

        public void LogChatHistory(ChatHistory history, string label = "ChatHistory")
        {
            _logger.LogDebug("────────────────────────────────────────────────");
            _logger.LogDebug("[ChatHistory] {Label} — {Count} messages", label, history.Count);

            for (var i = 0; i < history.Count; i++)
            {
                var msg = history[i];
                var preview = msg.Content?.Length > 120
                    ? msg.Content[..120] + "..."
                    : msg.Content ?? "(null)";

                _logger.LogDebug(
                    "  [{Index}] Role={Role} | Length={Length} | Content={Preview}",
                    i, msg.Role, msg.Content?.Length ?? 0, preview);
            }

            _logger.LogDebug("────────────────────────────────────────────────");
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private void LogLlmRequest(ChatHistory history)
        {
            // Build a JSON-like representation of what SK will send
            // This is what hits the wire at /v1/chat/completions
            var sb = new StringBuilder();
            sb.AppendLine("[LLM] Outbound request payload:");
            sb.AppendLine("{");
            sb.AppendLine($"  \"model\": \"{_options.ModelId}\",");
            sb.AppendLine("  \"stream\": true,");
            sb.AppendLine("  \"messages\": [");

            for (var i = 0; i < history.Count; i++)
            {
                var msg = history[i];
                var comma = i < history.Count - 1 ? "," : "";
                var contentPreview = msg.Content?.Length > 80
                    ? msg.Content[..80] + "..."
                    : msg.Content ?? "";
                sb.AppendLine($"    {{ \"role\": \"{msg.Role}\", \"content\": \"{contentPreview}\" }}{comma}");
            }

            sb.AppendLine("  ]");
            sb.Append("}");

            _logger.LogDebug(sb.ToString());
        }
    }
}
