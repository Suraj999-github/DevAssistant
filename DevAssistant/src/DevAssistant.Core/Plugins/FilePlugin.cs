using DevAssistant.Configuration;
using DevAssistant.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DevAssistant.Core.Plugins
{
    /// <summary>
    /// Provides file system tools for the AI agent.
    /// Every method decorated with [KernelFunction] becomes a callable tool.
    /// SK automatically serializes/deserializes parameters from LLM JSON.
    /// </summary>
    public sealed class FilePlugin
    {
        private readonly AgentOptions _options;
        private readonly ILogger<FilePlugin> _logger;

        // Max file size we'll read — prevents the LLM context window from exploding
        private const int MaxFileSizeBytes = 100_000; // 100KB

        public FilePlugin(IOptions<AgentOptions> options, ILogger<FilePlugin> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        // ── ReadFile ──────────────────────────────────────────────────────────────

        [KernelFunction("ReadFile")]
        [Description(
            "Reads the text content of a file from the workspace. " +
            "Use this when you need to examine source code, configuration, " +
            "or any text file. Returns the full file content as a string. " +
            "Path must be relative to the workspace root (e.g. 'src/Program.cs').")]
        public async Task<string> ReadFileAsync(
            [Description("Relative path to the file within the workspace root. " +
                     "Example: 'src/MyClass.cs' or 'appsettings.json'")]
        string relativePath,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();

            // ── 1. Log the tool call ─────────────────────────────────────────────
            _logger.LogInformation(
                "[FilePlugin.ReadFile] ToolCall Start — Path: {Path}", relativePath);

            try
            {
                // ── 2. Validate path (security) ──────────────────────────────────
                var resolvedPath = PathValidator.ResolveSafe(
                    _options.WorkingDirectory, relativePath);

                PathValidator.EnsureAllowedExtension(resolvedPath);

                // ── 3. Check file exists ─────────────────────────────────────────
                if (!File.Exists(resolvedPath))
                {
                    var msg = $"File not found: '{relativePath}'. " +
                              $"Resolved to: '{resolvedPath}'. " +
                              $"Use ListFiles to see available files.";

                    _logger.LogWarning("[FilePlugin.ReadFile] {Message}", msg);
                    return ToolResult.Failure(msg);
                }

                // ── 4. Check file size ───────────────────────────────────────────
                var fileInfo = new FileInfo(resolvedPath);
                if (fileInfo.Length > MaxFileSizeBytes)
                {
                    var msg = $"File '{relativePath}' is too large " +
                              $"({fileInfo.Length:N0} bytes, max {MaxFileSizeBytes:N0}). " +
                              $"Consider reading a specific section instead.";

                    _logger.LogWarning("[FilePlugin.ReadFile] {Message}", msg);
                    return ToolResult.Failure(msg);
                }

                // ── 5. Read the file ─────────────────────────────────────────────
                var content = await File.ReadAllTextAsync(
                    resolvedPath, Encoding.UTF8, cancellationToken);

                sw.Stop();

                // ── 6. Log the result ────────────────────────────────────────────
                _logger.LogInformation(
                    "[FilePlugin.ReadFile] ToolCall Complete — " +
                    "Path: {Path} | DurationMs: {Ms} | Bytes: {Bytes} | Lines: {Lines}",
                    relativePath,
                    sw.ElapsedMilliseconds,
                    content.Length,
                    content.Split('\n').Length);

                // ── 7. Return structured result to the LLM ───────────────────────
                return ToolResult.Success(new
                {
                    path = relativePath,
                    lines = content.Split('\n').Length,
                    bytes = content.Length,
                    content = content
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[FilePlugin.ReadFile] Security violation — Path: {Path}", relativePath);

                return ToolResult.Failure(
                    $"Access denied: {ex.Message}. " +
                    $"Only files within the workspace can be read.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "[FilePlugin.ReadFile] Cancelled — Path: {Path}", relativePath);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[FilePlugin.ReadFile] Error reading '{Path}' after {Ms}ms",
                    relativePath, sw.ElapsedMilliseconds);

                return ToolResult.Failure($"Error reading file: {ex.Message}");
            }
        }
    }
}
