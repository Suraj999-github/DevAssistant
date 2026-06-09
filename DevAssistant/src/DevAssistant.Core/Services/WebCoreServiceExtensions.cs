using DevAssistant.Agent;
using DevAssistant.Configuration;
using DevAssistant.Core.Agent;
using DevAssistant.Core.Plugins;
using DevAssistant.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevAssistant.Services
{
    public static class WebCoreServiceExtensions
    {
        /// <summary>
        /// Registers all Core agent services.
        /// Call this from both DevAssistant.Web and DevAssistant.Api Program.cs.
        /// </summary>
        public static IServiceCollection AddAgentCore(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ── Options ───────────────────────────────────────────────────────────
            services.Configure<AgentOptions>(
                configuration.GetSection(AgentOptions.SectionName));

            var options = configuration
                .GetSection(AgentOptions.SectionName)
                .Get<AgentOptions>() ?? new AgentOptions();

            // ── Named HttpClients ─────────────────────────────────────────────────
            services.AddHttpClient("ollama", client =>
            {
                client.BaseAddress = options.OllamaUri;
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            services.AddHttpClient("qdrant", client =>
            {
                client.BaseAddress = options.QdrantUri;
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // ── Agent services ────────────────────────────────────────────────────
            services.AddSingleton<IKernelFactory, KernelFactory>();
            services.AddSingleton<ILlmChatService, LlmChatService>();
            // services.AddTransient<EnvironmentHealthChecker>();
            // services.AddSingleton<IKernelFactory, KernelFactory>();             
            services.AddTransient<WebEnvironmentHealthChecker>();

            // Program.cs
            services.AddScoped<IFileBrowserService, FileBrowserService>();
            services.AddScoped<ITestRunnerService, TestRunnerService>();
            services.AddSingleton<IMemoryService, MemoryService>(); // singleton owns the file lock
            services.AddTransient<FilePlugin>();
            services.AddSingleton<IAgentLoop, AgentLoop>();
            //services.AddSingleton<IFunctionInvocationFilter, ToolCallLoggingFilter>();         
            // services.AddSingleton<ToolCallLoggingFilter>();
            services.AddTransient<TestPlugin>();
            services.AddSingleton<IMessageRouter, MessageRouter>();

            return services;
        }
    }
}
