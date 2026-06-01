#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001

using DevAssistant.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
namespace DevAssistant.Services
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

    //public sealed class KernelFactory : IKernelFactory
    //{
    //    private readonly AgentOptions _options;
    //    private readonly ILoggerFactory _loggerFactory;
    //    private readonly ILogger<KernelFactory> _logger;

    //    public KernelFactory(
    //        IOptions<AgentOptions> options,
    //        ILoggerFactory loggerFactory,
    //        ILogger<KernelFactory> logger)
    //    {
    //        _options = options.Value;
    //        _loggerFactory = loggerFactory;
    //        _logger = logger;
    //    }

    //    public Kernel CreateKernel()
    //    {
    //        _logger.LogInformation(
    //            "Building Semantic Kernel — Model: {ModelId}, Endpoint: {Endpoint}",
    //            _options.ModelId, _options.OllamaEndpoint);

    //        var builder = Kernel.CreateBuilder();

    //        // ── Chat Completion (Mistral via Ollama) ─────────────────────────────
    //        // Signature: modelId, apiKey, endpoint (Uri), orgId, serviceId, httpClient
    //        builder.AddOpenAIChatCompletion(
    //            modelId: _options.ModelId,
    //            apiKey: "ollama",
    //            endpoint: _options.OllamaUri);

    //        // ── Text Embedding (nomic-embed-text via Ollama) ─────────────────────
    //        // Signature differs from chat — use the HttpClient overload to set base URL

    //        var ollamaClient = new HttpClient
    //        {
    //            BaseAddress = _options.OllamaUri
    //        };

    //        builder.AddOpenAITextEmbeddingGeneration(
    //            modelId: _options.EmbeddingModelId,
    //            apiKey: "ollama",
    //            httpClient: ollamaClient);

    //        builder.Services.AddSingleton(_loggerFactory);

    //        var kernel = builder.Build();

    //        _logger.LogInformation("Kernel built successfully");
    //        return kernel;
    //    }
    //}
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
                "Building Kernel — Model: {ModelId}, Endpoint: {Endpoint}",
                _options.ModelId, _options.OllamaEndpoint);

            var builder = Kernel.CreateBuilder();

            // ── Approach A: Use OllamaApiClient (recommended for SK 1.21+) ───────
            // This is the cleanest path — no endpoint URL confusion at all.
            // Requires: dotnet add package Microsoft.SemanticKernel.Connectors.Ollama
            //
            // builder.AddOllamaChatCompletion(
            //     modelId: _options.ModelId,
            //     endpoint: _options.OllamaUri);
            //
            // builder.AddOllamaTextEmbeddingGeneration(
            //     modelId: _options.EmbeddingModelId,
            //     endpoint: _options.OllamaUri);

            // ── Approach B: OpenAI connector with explicit HttpClient ────────────
            // Forces the base URL so SK never guesses the path.
            // The trailing slash on BaseAddress is required by HttpClient routing rules.
            var ollamaHttpClient = new HttpClient
            {
                BaseAddress = new Uri(_options.OllamaEndpoint.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromMinutes(5) // local models can be slow
            };

            builder.AddOpenAIChatCompletion(
                modelId: _options.ModelId,
                apiKey: "ollama",           // Ollama ignores this but SK requires it
                endpoint: new Uri(_options.OllamaEndpoint.TrimEnd('/') + "/"),
                httpClient: ollamaHttpClient);

            // Embeddings — same pattern
            var embedHttpClient = new HttpClient
            {
                BaseAddress = new Uri(_options.OllamaEndpoint.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromMinutes(2)
            };

            builder.AddOpenAITextEmbeddingGeneration(
                modelId: _options.EmbeddingModelId,
                apiKey: "ollama",
                //endpoint: new Uri(_options.OllamaEndpoint.TrimEnd('/') + "/"),
                httpClient: embedHttpClient);

            builder.Services.AddSingleton(_loggerFactory);

            var kernel = builder.Build();
            _logger.LogInformation("Kernel built successfully");
            return kernel;
        }
    }
}
