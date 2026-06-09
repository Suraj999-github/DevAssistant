namespace DevAssistant.Configuration
{
    public sealed class AgentOptions
    {
        public const string SectionName = "Agent";

        public string ModelId { get; init; } = "mistral";
        // public string ModelId { get; init; } = "llama3";
        public string EmbeddingModelId { get; init; } = "nomic-embed-text";
        public string OllamaEndpoint { get; init; } = "http://localhost:11434";
        public string QdrantEndpoint { get; init; } = "http://localhost:6333";
        public string QdrantCollectionName { get; init; } = "dev-assistant-code";
        public int MaxIterations { get; init; } = 2;
        public string WorkingDirectory { get; init; } = "./workspace";
        public bool StreamingEnabled { get; init; } = true;

        // ── Derived URIs ──────────────────────────────────────────────────────────

        // Used by health checker → /api/tags, /api/version
        public Uri OllamaUri => new(OllamaEndpoint.TrimEnd('/'));

        // Used by OpenAI connector in KernelFactory → appends /v1/chat/completions
        public Uri OllamaOpenAiUri => new(OllamaEndpoint.TrimEnd('/') + "/v1/");

        // Used by Qdrant health checker → /healthz
        public Uri QdrantUri => new(QdrantEndpoint.TrimEnd('/'));
    }
}
