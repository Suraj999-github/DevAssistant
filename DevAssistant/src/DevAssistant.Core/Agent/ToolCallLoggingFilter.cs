using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics;


namespace DevAssistant.Agent
{
    // <summary>
    /// Intercepts every KernelFunction invocation.
    /// Logs timing, arguments, and results for observability.
    /// Registered as a singleton in the Kernel's filter pipeline.
    /// </summary>
    public sealed class ToolCallLoggingFilter : IFunctionInvocationFilter
    {
        private readonly ILogger _logger;

        // Use ILoggerFactory instead of ILogger<T> — avoids DI resolution issues
        // when SK activates filters outside the normal DI pipeline
        public ToolCallLoggingFilter(ILoggerFactory loggerFactory)
            => _logger = loggerFactory.CreateLogger<ToolCallLoggingFilter>();

        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            var sw = Stopwatch.StartNew();
            var pluginName = context.Function.PluginName ?? "Unknown";
            var fnName = context.Function.Name;

            // Build args preview safely
            var args = context.Arguments
                .Where(kv => kv.Value is not null)
                .Select(kv =>
                {
                    var val = kv.Value?.ToString() ?? "";
                    return $"{kv.Key}={(val.Length > 50 ? val[..50] + "…" : val)}";
                })
                .ToList();

            _logger.LogInformation(
                "[Filter] ► {Plugin}.{Fn} | Args: [{Args}]",
                pluginName, fnName, string.Join(", ", args));

            try
            {
                await next(context);
                sw.Stop();

                var resultPreview = context.Result?.GetValue<string>() is string r
                    ? (r.Length > 120 ? r[..120] + "…" : r)
                    : "(non-string)";

                _logger.LogInformation(
                    "[Filter] ◄ {Plugin}.{Fn} | DurationMs: {Ms} | Result: {Result}",
                    pluginName, fnName, sw.ElapsedMilliseconds, resultPreview);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[Filter] ✗ {Plugin}.{Fn} threw after {Ms}ms",
                    pluginName, fnName, sw.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
