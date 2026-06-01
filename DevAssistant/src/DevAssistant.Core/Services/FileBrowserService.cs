using DevAssistant.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace DevAssistant.Services
{
    public interface IFileBrowserService
    {
        Task<FileBrowserViewModel> GetFilesAsync(string path = ".", CancellationToken ct = default);
        Task<Models.FileContentResult> GetFileContentAsync(string path, CancellationToken ct = default);
        Task<bool> WriteFileAsync(string path, string content, CancellationToken ct = default);
    }

    public sealed class FileBrowserService : IFileBrowserService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileBrowserService> _logger;

        private string Root => _env.ContentRootPath;

        public FileBrowserService(IWebHostEnvironment env, ILogger<FileBrowserService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public Task<FileBrowserViewModel> GetFilesAsync(
            string path = ".", CancellationToken ct = default)
        {
            try
            {
                var resolved = ResolveSafe(path);

                if (!Directory.Exists(resolved))
                    return Task.FromResult(new FileBrowserViewModel(path, [], null, null));

                var entries = new List<FileEntry>();

                foreach (var dir in Directory.EnumerateDirectories(resolved))
                {
                    var info = new DirectoryInfo(dir);
                    entries.Add(new FileEntry(
                        Name: info.Name,
                        Path: RelativeTo(dir),
                        Extension: string.Empty,
                        SizeBytes: 0,
                        LastModified: info.LastWriteTimeUtc,
                        IsDirectory: true));
                }

                foreach (var file in Directory.EnumerateFiles(resolved))
                {
                    var info = new FileInfo(file);
                    entries.Add(new FileEntry(
                        Name: info.Name,
                        Path: RelativeTo(file),
                        Extension: info.Extension,
                        SizeBytes: info.Length,
                        LastModified: info.LastWriteTimeUtc,
                        IsDirectory: false));
                }

                return Task.FromResult(new FileBrowserViewModel(path, entries, null, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FileBrowser] GetFiles failed for {Path}", path);
                return Task.FromResult(new FileBrowserViewModel(path, [], null, null));
            }
        }

        public async Task<Models.FileContentResult> GetFileContentAsync(
            string path, CancellationToken ct = default)
        {
            try
            {
                var resolved = ResolveSafe(path);

                if (!File.Exists(resolved))
                    return new Models.FileContentResult(path, string.Empty, false, "File not found");

                if (!IsTextFile(resolved))
                    return new Models.FileContentResult(path, string.Empty, false, "Binary files cannot be displayed");

                var content = await File.ReadAllTextAsync(resolved, ct);
                return new Models.FileContentResult(path, content, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FileBrowser] GetFileContent failed for {Path}", path);
                return new Models.FileContentResult(path, string.Empty, false, ex.Message);
            }
        }

        public async Task<bool> WriteFileAsync(
            string path, string content, CancellationToken ct = default)
        {
            try
            {
                var resolved = ResolveSafe(path);
                Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
                await File.WriteAllTextAsync(resolved, content, ct);
                _logger.LogInformation("[FileBrowser] Written {Path}", path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FileBrowser] WriteFile failed for {Path}", path);
                return false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private string ResolveSafe(string path)
        {
            var combined = Path.GetFullPath(Path.Combine(Root, path));
            if (!combined.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Path traversal denied: {path}");
            return combined;
        }

        private string RelativeTo(string absolute)
            => Path.GetRelativePath(Root, absolute);

        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".cs", ".json", ".xml", ".yaml", ".yml", ".html", ".htm",
        ".css", ".js", ".ts", ".razor", ".cshtml", ".sh", ".ps1", ".env",
        ".config", ".csproj", ".sln", ".toml", ".ini", ".log"
    };

        private static bool IsTextFile(string path)
            => TextExtensions.Contains(Path.GetExtension(path));
    }
}
