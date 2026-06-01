using DevAssistant.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Json;

namespace DevAssistant.Services
{

    public sealed class WebEnvironmentHealthChecker
    {
        private readonly AgentOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebEnvironmentHealthChecker> _logger;

        public WebEnvironmentHealthChecker(
            IOptions<AgentOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<WebEnvironmentHealthChecker> logger)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<HealthReport> RunAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Running environment health checks...");

            var ollamaResult = await CheckOllamaAsync(ct);
            var qdrantResult = await CheckQdrantAsync(ct);
            var workspaceResult = CheckWorkspace();

            var report = new HealthReport(
                OllamaOnline: ollamaResult.Online,
                QdrantOnline: qdrantResult.Online,
                OllamaModel: _options.ModelId,
                OllamaVersion: ollamaResult.Version,
                PulledModels: ollamaResult.Models,
                WorkspaceReady: workspaceResult,
                WorkspacePath: Path.GetFullPath(_options.WorkingDirectory),
                CheckedAt: DateTime.UtcNow);

            LogSummary(report);
            return report;
        }

        // ── Ollama ────────────────────────────────────────────────────────────────
        private async Task<(bool Online, string? Version, string[] Models)> CheckOllamaAsync(
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient("ollama");
                var response = await client.GetAsync("/api/tags", ct);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content
                    .ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: ct);

                var models = payload?.Models?.Select(m => m.Name).ToArray() ?? [];
                sw.Stop();

                _logger.LogInformation(
                    "[Ollama] Online in {Ms}ms — models: {Models}",
                    sw.ElapsedMilliseconds, string.Join(", ", models));

                return (true, null, models);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[Ollama] Unreachable after {Ms}ms", sw.ElapsedMilliseconds);
                return (false, null, []);
            }
        }

        // ── Qdrant ────────────────────────────────────────────────────────────────
        private async Task<(bool Online, string? Version)> CheckQdrantAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient("qdrant");
                var response = await client.GetAsync("/healthz", ct);
                response.EnsureSuccessStatusCode();
                sw.Stop();

                _logger.LogInformation("[Qdrant] Online in {Ms}ms", sw.ElapsedMilliseconds);
                return (true, null);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[Qdrant] Unreachable after {Ms}ms", sw.ElapsedMilliseconds);
                return (false, null);
            }
        }

        // ── Workspace directory ───────────────────────────────────────────────────
        private bool CheckWorkspace()
        {
            try
            {
                var path = Path.GetFullPath(_options.WorkingDirectory);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                var test = Path.Combine(path, ".write-test");
                File.WriteAllText(test, "ok");
                File.Delete(test);

                _logger.LogInformation("[Workspace] Ready at {Path}", path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Workspace] Not writable");
                return false;
            }
        }

        private void LogSummary(HealthReport r)
        {
            _logger.LogInformation("── Health Summary ──────────────────────");
            _logger.LogInformation("  Ollama    : {S}", r.OllamaOnline ? "✓ ONLINE" : "✗ OFFLINE");
            _logger.LogInformation("  Qdrant    : {S}", r.QdrantOnline ? "✓ ONLINE" : "✗ OFFLINE");
            _logger.LogInformation("  Workspace : {S}", r.WorkspaceReady ? "✓ READY" : "✗ ERROR");
            _logger.LogInformation("────────────────────────────────────────");
        }
    }

    // ── Result types ──────────────────────────────────────────────────────────────
    public sealed record HealthReport(
        bool OllamaOnline,
        bool QdrantOnline,
        string? OllamaModel,
        string? OllamaVersion,
        string[] PulledModels,
        bool WorkspaceReady,
        string WorkspacePath,
        DateTime CheckedAt);

    // ── Ollama API DTOs ───────────────────────────────────────────────────────────
    internal sealed record OllamaTagsResponse(OllamaModel[]? Models);
    internal sealed record OllamaModel(string Name, string Model, long Size);
}
