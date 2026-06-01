#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001

using DevAssistant.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
namespace DevAssistant.Api.Services
{

    /// <summary>
    /// Responsible for constructing and configuring the Semantic Kernel instance.
    /// Isolated here so every future service receives the same Kernel configuration
    /// without duplicating builder logic.
    /// </summary>
    public interface IKernelFactory
    {
        Kernel CreateKernel();
    }

    public sealed class KernelFactory : IKernelFactory
    {
        private readonly AgentOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<KernelFactory> _logger;

        public KernelFactory(
            IOptions<AgentOptions> options,
            ILoggerFactory loggerFactory,
            ILogger<KernelFactory> logger)
        {
            _options = options.Value;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        public Kernel CreateKernel()
        {
            _logger.LogInformation(
                "Building Semantic Kernel — Model: {ModelId}, Endpoint: {Endpoint}",
                _options.ModelId, _options.OllamaEndpoint);

            var builder = Kernel.CreateBuilder();

            // ── Chat Completion (Mistral via Ollama) ─────────────────────────────
            // Signature: modelId, apiKey, endpoint (Uri), orgId, serviceId, httpClient
            builder.AddOpenAIChatCompletion(
                modelId: _options.ModelId,
                apiKey: "ollama",
                endpoint: _options.OllamaUri);

            // ── Text Embedding (nomic-embed-text via Ollama) ─────────────────────
            // Signature differs from chat — use the HttpClient overload to set base URL

            var ollamaClient = new HttpClient
            {
                BaseAddress = _options.OllamaUri
            };

            builder.AddOpenAITextEmbeddingGeneration(
                modelId: _options.EmbeddingModelId,
                apiKey: "ollama",
                httpClient: ollamaClient);

            builder.Services.AddSingleton(_loggerFactory);

            var kernel = builder.Build();

            _logger.LogInformation("Kernel built successfully");
            return kernel;
        }
    }
}
