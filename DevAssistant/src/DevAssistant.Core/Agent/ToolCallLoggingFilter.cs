using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics;


namespace DevAssistant.Agent
{
    // <summary>
    /// Intercepts every KernelFunction invocation.
    /// Logs timing, arguments, and results for observability.
    /// Registered as a singleton in the Kernel's filter pipeline.
    /// Per-loop instance — records every tool call into the shared audit list.

    /// </summary>
    public sealed class ToolCallLoggingFilter : IFunctionInvocationFilter
    {
        private readonly List<ToolCallRecord> _toolCalls;
        private readonly ILogger _logger;
        private int _currentIteration = 1;

        public ToolCallLoggingFilter(
            List<ToolCallRecord> toolCalls,
            ILoggerFactory loggerFactory)
        {
            _toolCalls = toolCalls;
            _logger = loggerFactory.CreateLogger<ToolCallLoggingFilter>();
        }

        public void SetIteration(int iteration) => _currentIteration = iteration;

        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            var sw = Stopwatch.StartNew();
            var pluginName = context.Function.PluginName ?? "Unknown";
            var fnName = context.Function.Name;

            var args = context.Arguments
                .Where(kv => kv.Value is not null)
                .Select(kv =>
                {
                    var val = kv.Value?.ToString() ?? "";
                    return $"{kv.Key}={(val.Length > 60 ? val[..60] + "…" : val)}";
                })
                .ToList();

            _logger.LogInformation(
                "[Filter] ► {Plugin}.{Fn} | Args: [{Args}]",
                pluginName, fnName, string.Join(", ", args));

            string result = string.Empty;
            bool succeeded = false;

            try
            {
                await next(context);
                sw.Stop();

                result = context.Result?.GetValue<string>() ?? string.Empty;
                succeeded = true;

                var preview = result.Length > 120 ? result[..120] + "…" : result;
                _logger.LogInformation(
                    "[Filter] ◄ {Plugin}.{Fn} | DurationMs: {Ms} | Result: {R}",
                    pluginName, fnName, sw.ElapsedMilliseconds, preview);
            }
            catch (Exception ex)
            {
                sw.Stop();
                succeeded = false;
                result = ex.Message;
                _logger.LogError(ex,
                    "[Filter] ✗ {Plugin}.{Fn} threw after {Ms}ms",
                    pluginName, fnName, sw.ElapsedMilliseconds);
                throw;
            }
            finally
            {
                // ── THIS is the missing link — populate toolCalls ─────────────────
                _toolCalls.Add(new ToolCallRecord(
                    Iteration: _currentIteration,
                    PluginName: pluginName,
                    FunctionName: fnName,
                    Arguments: string.Join(", ", args),
                    Result: result,
                    Succeeded: succeeded,
                    DurationMs: sw.ElapsedMilliseconds));
            }
        }
    }
}
