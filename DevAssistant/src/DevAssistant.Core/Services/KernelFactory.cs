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
                "Building Kernel — Model: {ModelId}, Endpoint: {Endpoint}",
                _options.ModelId, _options.OllamaEndpoint);

            var builder = Kernel.CreateBuilder();
          
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

            // ── Register all plugins so LLM can call them ─────────────────────────
            kernel.RegisterAgentPlugins(_services);

            // In KernelFactory.CreateKernel() after RegisterAgentPlugins:
            foreach (var plugin in kernel.Plugins)
            {
                foreach (var fn in plugin)
                {
                    _logger.LogInformation("=======================================================================");
                    _logger.LogInformation("=======================================================================");

                    _logger.LogInformation(
                        "  Tool registered: {Plugin}-{Function} — {Description}",
                        plugin.Name, fn.Name, fn.Description);

                    _logger.LogInformation("=======================================================================");
                    _logger.LogInformation("=======================================================================");

                }
            }

            _logger.LogInformation("Kernel built successfully");
            return kernel;
        }
    }
}
