namespace DevAssistant.Models
{
    public class MemoryModels
    {
    }
    public sealed record MemoryEntry(
    string Id,
    string Content,
    string Collection,
    DateTime CreatedAt,
    double? RelevanceScore = null);

    public sealed record MemorySearchRequest(string Query, int TopK = 5);

    public sealed record MemoryViewModel(
        IReadOnlyList<MemoryEntry> Entries,
        IReadOnlyList<MemoryEntry>? SearchResults,
        string? SearchQuery,
        int TotalCount);

    public sealed record HealthStatus(
        bool OllamaOnline,
        bool QdrantOnline,
        string? OllamaModel,
        string? OllamaVersion,
        int MemoryEntryCount,
        DateTime CheckedAt);
}
