namespace DevAssistant.Agent
{
    public sealed record AgentExecutionResult(
        bool Success,
        string FinalResponse,
        int IterationsUsed,
        TimeSpan Duration,
        IReadOnlyList<ToolCallRecord> ToolCalls,
        string? StopReason,
        string? Error = null);

    public sealed record ToolCallRecord(
        int Iteration,
        string PluginName,
        string FunctionName,
        string Arguments,
        string Result,
        bool Succeeded,
        long DurationMs);
}
