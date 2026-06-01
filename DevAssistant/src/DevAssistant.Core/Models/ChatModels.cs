namespace DevAssistant.Models
{
    public class ChatModels
    {
    }
    public sealed record ChatMessage(string Role, string Content, DateTime Timestamp);

    public sealed record ChatRequest(string Message, string? SystemPrompt = null);

    public sealed record ChatResponse(string Content, bool Success, string? Error = null);
}
