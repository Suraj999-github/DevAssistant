namespace DevAssistant.Configuration
{
    public sealed class AgentOptions
    {
        public const string SectionName = "Agent";

        public string ModelId { get; init; } = "mistral";
        public string EmbeddingModelId { get; init; } = "nomic-embed-text";
        public string OllamaEndpoint { get; init; } = "http://localhost:11434";
        public string QdrantEndpoint { get; init; } = "http://localhost:6333";
        public string QdrantCollectionName { get; init; } = "dev-assistant-code";
        public int MaxIterations { get; init; } = 10;
        public string WorkingDirectory { get; init; } = "./workspace";
        public bool StreamingEnabled { get; init; } = true;

        // Derived — computed at startup
        public Uri OllamaUri => new(OllamaEndpoint);
        public Uri QdrantUri => new(QdrantEndpoint);
    }
}
