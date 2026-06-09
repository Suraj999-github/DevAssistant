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
        // Add these two methods to FilePlugin.cs

        [KernelFunction("ListFiles")]
        [Description(
            "Lists all files and directories in a given workspace path. " +
            "Use this BEFORE ReadFile when you don't know the exact filename. " +
            "Returns file names, sizes, and last modified dates.")]
        public Task<string> ListFilesAsync(
            [Description("Relative path to list. Use '.' for the workspace root.")]
    string relativePath = ".",
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[FilePlugin.ListFiles] ToolCall Start — Path: {Path}", relativePath);
            try
            {
                var resolvedPath = PathValidator.ResolveSafe(
                    _options.WorkingDirectory, relativePath);

                if (!Directory.Exists(resolvedPath))
                    return Task.FromResult(ToolResult.Failure(
                        $"Directory not found: '{relativePath}'"));

                var entries = new List<object>();

                // Directories first
                foreach (var dir in Directory.GetDirectories(resolvedPath))
                {
                    var name = Path.GetFileName(dir);
                    if (name.StartsWith('.')) continue; // skip hidden
                    entries.Add(new { name, type = "directory", path = relativePath + "/" + name });
                }

                // Then files
                foreach (var file in Directory.GetFiles(resolvedPath))
                {
                    var info = new FileInfo(file);
                    if (!PathValidator.AllowedReadExtensions.Contains(info.Extension)) continue;
                    entries.Add(new
                    {
                        name = info.Name,
                        type = "file",
                        path = relativePath + "/" + info.Name,
                        sizeBytes = info.Length,
                        lastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                    });
                }

                _logger.LogInformation(
                    "[FilePlugin.ListFiles] Found {Count} entries in '{Path}'",
                    entries.Count, relativePath);

                return Task.FromResult(ToolResult.Success(new { path = relativePath, entries }));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "[FilePlugin.ListFiles] Access denied: {Path}", relativePath);
                return Task.FromResult(ToolResult.Failure($"Access denied: {ex.Message}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FilePlugin.ListFiles] Error listing '{Path}'", relativePath);
                return Task.FromResult(ToolResult.Failure($"Error: {ex.Message}"));
            }
        }

        [KernelFunction("WriteFile")]
        [Description(
            "Writes or overwrites a file in the workspace with the provided content. " +
            "Use this to save code fixes, new files, or updated configuration. " +
            "IMPORTANT: Always read the file first before overwriting it.")]
        public async Task<string> WriteFileAsync(
            [Description("Relative path where the file should be written. " +
                 "Example: 'src/OrderService.cs'")]
    string relativePath,
            [Description("The complete file content to write. Must be the full file, not a diff.")]
    string content,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation(
                "[FilePlugin.WriteFile] ToolCall Start — Path: {Path} | ContentLen: {Len}",
                relativePath, content.Length);
            try
            {
                var resolvedPath = PathValidator.ResolveSafe(
                    _options.WorkingDirectory, relativePath);

                PathValidator.EnsureAllowedExtension(resolvedPath);

                // Create directory if it doesn't exist
                var dir = Path.GetDirectoryName(resolvedPath);
                if (dir is not null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(
                    resolvedPath, content, System.Text.Encoding.UTF8, cancellationToken);

                sw.Stop();
                _logger.LogInformation(
                    "[FilePlugin.WriteFile] ToolCall Complete — " +
                    "Path: {Path} | DurationMs: {Ms} | BytesWritten: {Bytes}",
                    relativePath, sw.ElapsedMilliseconds, content.Length);

                return ToolResult.Success(new
                {
                    path = relativePath,
                    bytesWritten = content.Length,
                    lines = content.Split('\n').Length
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[FilePlugin.WriteFile] Security violation — Path: {Path}", relativePath);
                return ToolResult.Failure($"Access denied: {ex.Message}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[FilePlugin.WriteFile] Error writing '{Path}'", relativePath);
                return ToolResult.Failure($"Error writing file: {ex.Message}");
            }
        }
    }
}
