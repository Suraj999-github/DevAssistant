using Microsoft.SemanticKernel.ChatCompletion;

namespace DevAssistant.Services
{
    public sealed class Step2Demo
    {
        private readonly ILlmChatService _chat;
        private readonly ILogger<Step2Demo> _logger;

        public Step2Demo(ILlmChatService chat, ILogger<Step2Demo> logger)
        {
            _chat = chat;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("═══════════════════════════════════════════════════");
            _logger.LogInformation("   Dev Assistant — Step 2: First LLM Call          ");
            _logger.LogInformation("═══════════════════════════════════════════════════");

            // ── Demo 1: Single-turn question ─────────────────────────────────────
            _logger.LogInformation("--- Demo 1: Single-turn question ---");
            Console.WriteLine("\n\u001b[33mUser:\u001b[0m What are the top 3 benefits of Semantic Kernel over raw HTTP calls to an LLM?");

            await _chat.StreamChatAsync(
                userMessage: "What are the top 3 benefits of Semantic Kernel over raw HTTP calls to an LLM?",
                cancellationToken: cancellationToken);

            // ── Demo 2: Multi-turn conversation (history persists) ───────────────
            _logger.LogInformation("--- Demo 2: Multi-turn conversation ---");

            // Create a shared history so context persists across turns
            var history = new ChatHistory();

            Console.WriteLine("\u001b[33mUser:\u001b[0m I'm building a Dev Assistant agent in .NET 8.");
            await _chat.StreamChatAsync(
                userMessage: "I'm building a Dev Assistant agent in .NET 8. " +
                             "It will read files, run tests, and suggest code fixes.",
                existingHistory: history,
                cancellationToken: cancellationToken);

            // Second turn — the LLM will remember the context from the first turn
            Console.WriteLine("\u001b[33mUser:\u001b[0m What should my first KernelFunction tool do?");
            await _chat.StreamChatAsync(
                userMessage: "Given what I told you, what should my first KernelFunction tool do?",
                existingHistory: history,  // same history object → multi-turn context
                cancellationToken: cancellationToken);

            // ── Demo 3: Show the full serialized ChatHistory ──────────────────────
            _logger.LogInformation("--- Demo 3: Inspect full ChatHistory ---");
            _chat.LogChatHistory(history, "Final Multi-Turn History");

            _logger.LogInformation("Step 2 complete. Ready for Step 3: First KernelFunction tool.");
        }
    }
}
