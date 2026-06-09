namespace DevAssistant.Agent
{
    public sealed class AgentLoopOptions
    {
        /// <summary>
        /// Maximum number of think→tool→observe iterations before forcing a stop.
        /// Prevents infinite loops where the LLM keeps calling tools endlessly.
        /// Default: 10 — enough for complex multi-file tasks.
        /// </summary>
        public int MaxIterations { get; init; } = 15;

        /// <summary>
        /// Maximum total wall-clock time for the entire agent loop.
        /// Prevents a single runaway session from blocking the server.
        /// </summary>
        public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(8);

        /// <summary>
        /// If true, logs the full ChatHistory after every iteration.
        /// Expensive — only enable in Development.
        /// </summary>
        public bool VerboseHistoryLogging { get; init; } = false;

        /// <summary>
        /// If true, requires explicit user confirmation before WriteFile executes.
        /// Set false for automated pipelines, true for interactive use.
        /// </summary>
        public bool RequireWriteConfirmation { get; init; } = false;


    }
}
