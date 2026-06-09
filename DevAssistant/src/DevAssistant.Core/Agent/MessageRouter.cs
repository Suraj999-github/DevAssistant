using Microsoft.Extensions.Logging;


namespace DevAssistant.Agent
{
    public enum MessageIntent
    {
        DirectChat,   // simple conversation — bypass agent loop
        AgentTask     // requires tools — use agent loop
    }

    public sealed record ClassificationResult(
        MessageIntent Intent,
        string Reason);

    public interface IMessageRouter
    {
        ClassificationResult Classify(string message);
    }
    public sealed class MessageRouter : IMessageRouter
    {
        private readonly ILogger<MessageRouter> _logger;

        public MessageRouter(ILogger<MessageRouter> logger)
            => _logger = logger;

        public ClassificationResult Classify(string message)
        {
            var lower = message.Trim().ToLowerInvariant();
            var result = DoClassify(lower, message);

            _logger.LogInformation(
                "[MessageRouter] Intent: {Intent} | Reason: {Reason} | Message: {Msg}",
                result.Intent, result.Reason,
                message.Length > 60 ? message[..60] + "…" : message);

            return result;
        }

        private static ClassificationResult DoClassify(string lower, string original)
        {
            // ── Short messages are almost always conversational ───────────────────
            if (original.Trim().Split(' ').Length <= 4
                && !ContainsAny(lower, AgentKeywords))
                return new(MessageIntent.DirectChat, "short message");

            // ── Greetings ─────────────────────────────────────────────────────────
            if (ContainsAny(lower,
                "good morning", "good evening", "good afternoon", "good night",
                "hello", "hi there", "hey", "howdy", "greetings",
                "how are you", "how do you do", "what's up", "sup"))
                return new(MessageIntent.DirectChat, "greeting");

            // ── General knowledge questions ───────────────────────────────────────
            if (ContainsAny(lower,
                "what is the capital", "what is the population",
                "who is the president", "who invented", "when was",
                "where is", "how far", "what year", "tell me about",
                "explain what", "define ", "meaning of",
                "history of", "what does", "who was"))
                return new(MessageIntent.DirectChat, "general knowledge");

            // ── Small talk ────────────────────────────────────────────────────────
            if (ContainsAny(lower,
                "thank you", "thanks", "appreciate", "great job",
                "that's helpful", "nice", "awesome", "well done",
                "goodbye", "bye", "see you", "take care",
                "what can you do", "who are you", "what are you",
                "are you an ai", "help me understand",
                "joke", "tell me a joke", "make me laugh"))
                return new(MessageIntent.DirectChat, "small talk");

            // ── Coding questions WITHOUT file/test references ─────────────────────
            if (ContainsAny(lower,
                "how do i", "how to", "what is the difference",
                "can you explain", "show me an example", "what is a",
                "best practice", "when should i use", "pros and cons")
                && !ContainsAny(lower, AgentKeywords))
                return new(MessageIntent.DirectChat, "general coding question");

            // ── Explicit agent tasks ──────────────────────────────────────────────
            if (ContainsAny(lower, AgentKeywords))
                return new(MessageIntent.AgentTask, "contains agent keywords");

            // ── Default: short enough → chat, longer → agent ──────────────────────
            return original.Length < 80
                ? new(MessageIntent.DirectChat, "short, no agent keywords")
                : new(MessageIntent.AgentTask, "long message, assumed agent task");
        }

        // Keywords that indicate the user wants the agent to DO something
        private static readonly string[] AgentKeywords =
        [
            // File operations
            "read file", "read the file", "open file", "show file",
        "list files", "list the files", "browse files",
        "write file", "save file", "create file", "update file",
        "edit file", "modify file", "change file", "delete file",
        ".cs", ".csproj", ".json", ".sln", ".md",

        // Test operations
        "run test", "run the test", "execute test",
        "failing test", "fix test", "test fail", "test pass",
        "unit test", "dotnet test",

        // Code operations
        "fix the bug", "fix bug", "debug", "find the bug",
        "fix the error", "fix the issue", "fix the problem",
        "refactor", "implement", "add a method", "add method",
        "workspace", "codebase", "source code", "the code",
        "orderservice", "program.cs", "startup",

        // Agent-specific
        "search the code", "find in code", "semantic search",
        "remember", "recall", "memory"
        ];

        private static bool ContainsAny(string text, params string[] terms)
            => terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
    }
}
