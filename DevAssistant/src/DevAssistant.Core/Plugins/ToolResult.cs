using System.Text.Json;

namespace DevAssistant.Core.Plugins
{
    /// <summary>
    /// Standardizes tool return values so the LLM always sees
    /// a consistent { success, data } or { success, error } envelope.
    /// This reduces hallucination caused by unpredictable tool output shapes.
    /// </summary>
    public static class ToolResult
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>Returns a JSON success envelope the LLM can parse reliably.</summary>
        public static string Success(object data)
        {
            return JsonSerializer.Serialize(new
            {
                success = true,
                data
            }, _opts);
        }

        /// <summary>Returns a JSON failure envelope with a clear error message.</summary>
        public static string Failure(string error, string? hint = null)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error,
                hint = hint ?? "Check the path and try again."
            }, _opts);
        }
    }
}
