#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0070

using DevAssistant.Configuration;
using DevAssistant.Core.Plugins;
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

    public sealed class KernelFactory : IKernelFactory
    {
        private readonly AgentOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<KernelFactory> _logger;
        private readonly IServiceProvider _services;

        public KernelFactory(
            IOptions<AgentOptions> options,
            ILoggerFactory loggerFactory,
            ILogger<KernelFactory> logger,
            IServiceProvider services)
        {
            _options = options.Value;
            _loggerFactory = loggerFactory;
            _logger = logger;
            _services = services;
        }

        public Kernel CreateKernel()
        {
            _logger.LogInformation(
                "[KernelFactory] Building kernel | " +
                "OllamaUri: {Base} | OllamaOpenAiUri: {V1} | Model: {Model}",
                _options.OllamaUri,
                _options.OllamaOpenAiUri,
                _options.ModelId);

            var builder = Kernel.CreateBuilder();

            var ollamaHttpClient = new HttpClient
            {
                BaseAddress = _options.OllamaOpenAiUri,
                Timeout = Timeout.InfiniteTimeSpan   // let AgentLoop control timeout
            };

            builder.AddOpenAIChatCompletion(
               modelId: _options.ModelId,
               apiKey: "ollama",
               endpoint: _options.OllamaOpenAiUri,
               httpClient: ollamaHttpClient);
            // Embeddings client
            var embedHttpClient = new HttpClient
            {
                BaseAddress = _options.OllamaOpenAiUri,
                Timeout = TimeSpan.FromMinutes(2)
            };
            builder.AddOpenAITextEmbeddingGeneration(
                modelId: _options.EmbeddingModelId,
                apiKey: "ollama",
                httpClient: embedHttpClient);   // ← same

            builder.Services.AddSingleton(_loggerFactory);

            var kernel = builder.Build();

            kernel.RegisterAgentPlugins(_services);

            foreach (var plugin in kernel.Plugins)
                foreach (var fn in plugin)
                    _logger.LogInformation(
                        "  Tool ready: {Plugin}.{Fn}", plugin.Name, fn.Name);

            return kernel;
        }
    }
}
