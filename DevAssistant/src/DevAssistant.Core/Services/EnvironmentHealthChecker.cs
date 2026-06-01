using DevAssistant.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace DevAssistant.Api.Services
{
    public sealed class EnvironmentHealthChecker
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EnvironmentHealthChecker> _logger;

        public EnvironmentHealthChecker(
            IHttpClientFactory httpClientFactory,
            ILogger<EnvironmentHealthChecker> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting environment health checks...");

            var results = new Dictionary<string, bool>();

            results["Ollama"] = await CheckOllamaAsync(cancellationToken);
            results["Qdrant"] = await CheckQdrantAsync(cancellationToken);
            results["WorkspaceDir"] = CheckWorkspaceDirectory();

            // ─── Summary ───────────────────────────────────────────────────────
            _logger.LogInformation("────────────────────────────────────");
            _logger.LogInformation("  Health Check Summary               ");
            _logger.LogInformation("────────────────────────────────────");

            var allPassed = true;
            foreach (var (service, passed) in results)
            {
                var status = passed ? "✓ PASS" : "✗ FAIL";
                if (passed)
                    _logger.LogInformation("  {Status}  {Service}", status, service);
                else
                {
                    _logger.LogError("  {Status}  {Service}", status, service);
                    allPassed = false;
                }
            }

            if (allPassed)
            {
                _logger.LogInformation("────────────────────────────────────");
                _logger.LogInformation("  All systems ready. Build Step 2!");
            }
            else
            {
                _logger.LogError("────────────────────────────────────");
                _logger.LogError("  Fix the failing services above before proceeding.");
            }
        }

        private async Task<bool> CheckOllamaAsync(CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("[Ollama] Checking connectivity at http://localhost:11434...");
                var client = _httpClientFactory.CreateClient("ollama");

                // /api/tags lists all pulled models
                var response = await client.GetAsync("/api/tags", cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
                    cancellationToken: cancellationToken);

                sw.Stop();
                _logger.LogInformation("[Ollama] Reachable in {ElapsedMs}ms", sw.ElapsedMilliseconds);

                if (payload?.Models is null || payload.Models.Length == 0)
                {
                    _logger.LogWarning("[Ollama] No models found. Run: ollama pull mistral && ollama pull nomic-embed-text");
                    return false;
                }

                var modelNames = payload.Models.Select(m => m.Name).ToArray();
                _logger.LogInformation("[Ollama] Pulled models: {Models}", string.Join(", ", modelNames));

                // Check required models
                var hasMistral = modelNames.Any(n => n.StartsWith("mistral", StringComparison.OrdinalIgnoreCase));
                var hasEmbed = modelNames.Any(n => n.Contains("nomic-embed", StringComparison.OrdinalIgnoreCase));

                if (!hasMistral)
                    _logger.LogWarning("[Ollama] 'mistral' model not found. Run: ollama pull mistral");
                if (!hasEmbed)
                    _logger.LogWarning("[Ollama] 'nomic-embed-text' not found. Run: ollama pull nomic-embed-text");

                return hasMistral; // embed can be pulled later; mistral is needed for Step 2
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[Ollama] Cannot reach Ollama after {ElapsedMs}ms. Is 'ollama serve' running?",
                    sw.ElapsedMilliseconds);
                return false;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[Ollama] Unexpected error during health check");
                return false;
            }
        }

        private async Task<bool> CheckQdrantAsync(CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("[Qdrant] Checking connectivity at http://localhost:6333...");
                var client = _httpClientFactory.CreateClient("qdrant");

                var response = await client.GetAsync("/healthz", cancellationToken);
                response.EnsureSuccessStatusCode();

                sw.Stop();
                _logger.LogInformation("[Qdrant] Reachable in {ElapsedMs}ms", sw.ElapsedMilliseconds);
                return true;
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[Qdrant] Cannot reach Qdrant after {ElapsedMs}ms. Is Docker running? Run: docker start qdrant",
                    sw.ElapsedMilliseconds);
                return false;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[Qdrant] Unexpected error during health check");
                return false;
            }
        }

        private bool CheckWorkspaceDirectory()
        {
            try
            {
                var workspacePath = Path.GetFullPath("./workspace");
                if (!Directory.Exists(workspacePath))
                {
                    Directory.CreateDirectory(workspacePath);
                    _logger.LogInformation("[Workspace] Created workspace directory at {Path}", workspacePath);
                }
                else
                {
                    _logger.LogInformation("[Workspace] Found workspace directory at {Path}", workspacePath);
                }

                // Verify write access
                var testFile = Path.Combine(workspacePath, ".write-test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                _logger.LogInformation("[Workspace] Write access confirmed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Workspace] Failed to create or write to workspace directory");
                return false;
            }
        }
    }
}
