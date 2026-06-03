using DevAssistant.Core.Plugins;
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
            var filePlugin = services.GetRequiredService<FilePlugin>();
            kernel.ImportPluginFromObject(filePlugin, pluginName: "FilePlugin");
            return kernel;
        }
    }
}
