using DevAssistant.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace DevAssistant.Core.Plugins
{
    public static class PluginServiceExtensions
    {
        public static Kernel RegisterAgentPlugins(
            this Kernel kernel,
            IServiceProvider services)
        {
            // ── File tools ────────────────────────────────────────────────────────
            var filePlugin = services.GetRequiredService<FilePlugin>();
            kernel.ImportPluginFromObject(filePlugin, pluginName: "FilePlugin");

            // ── Test tools ────────────────────────────────────────────────────────
            var testPlugin = services.GetRequiredService<TestPlugin>();
            kernel.ImportPluginFromObject(testPlugin, pluginName: "TestPlugin");

            return kernel;
        }
    }
}
