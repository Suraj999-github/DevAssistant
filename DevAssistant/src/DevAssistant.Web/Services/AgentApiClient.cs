
using DevAssistant.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DevAssistant.Web.Services
{
    public interface IAgentApiClient
    {
        Task<HealthStatus> GetHealthAsync(CancellationToken ct = default);
        IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken ct = default);
        Task<FileBrowserViewModel> GetFilesAsync(string path = ".", CancellationToken ct = default);
        Task<FileContentResult> GetFileContentAsync(string path, CancellationToken ct = default);
        Task<bool> WriteFileAsync(string path, string content, CancellationToken ct = default);
        Task<TestRunSummary> RunTestsAsync(string? filter = null, CancellationToken ct = default);
        Task<MemoryViewModel> GetMemoryAsync(CancellationToken ct = default);
        Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(string query, int topK = 5, CancellationToken ct = default);
        Task<bool> AddMemoryAsync(string content, CancellationToken ct = default);
        Task<bool> DeleteMemoryAsync(string id, CancellationToken ct = default);
    }

    public sealed class AgentApiClient : IAgentApiClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<AgentApiClient> _logger;

        public AgentApiClient(HttpClient http, ILogger<AgentApiClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<HealthStatus> GetHealthAsync(CancellationToken ct = default)
        {
            try
            {
                var result = await _http.GetFromJsonAsync<HealthStatus>("/api/health", ct);
                return result ?? new HealthStatus(false, false, null, null, 0, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return new HealthStatus(false, false, null, null, 0, DateTime.UtcNow);
            }
        }

        // SSE streaming — yields tokens as they arrive from the API
        public async IAsyncEnumerable<string> StreamChatAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
            {
                Content = JsonContent.Create(request)
            };
            req.Headers.Accept.ParseAdd("text/event-stream");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat stream request failed");
                // yield return $"[ERROR] {ex.Message}";
                yield break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                if (!line.StartsWith("data:")) continue;

                var data = line["data:".Length..].Trim();
                if (data == "[DONE]") break;
                if (string.IsNullOrEmpty(data)) continue;

                string token;
                try
                {
                    var doc = JsonDocument.Parse(data);
                    token = doc.RootElement
                        .GetProperty("token")
                        .GetString() ?? string.Empty;
                }
                catch
                {
                    token = data;
                }

                if (!string.IsNullOrEmpty(token))
                    yield return token;
            }
        }

        public async Task<FileBrowserViewModel> GetFilesAsync(
            string path = ".", CancellationToken ct = default)
        {
            try
            {
                var encoded = Uri.EscapeDataString(path);
                var result = await _http.GetFromJsonAsync<FileBrowserViewModel>(
                    $"/api/files?path={encoded}", ct);
                return result ?? new FileBrowserViewModel(path, [], null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFiles failed for path {Path}", path);
                return new FileBrowserViewModel(path, [], null, null);
            }
        }

        public async Task<FileContentResult> GetFileContentAsync(
            string path, CancellationToken ct = default)
        {
            try
            {
                var encoded = Uri.EscapeDataString(path);
                var result = await _http.GetFromJsonAsync<FileContentResult>(
                    $"/api/files/content?path={encoded}", ct);
                return result ?? new FileContentResult(path, "", false, "No response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFileContent failed for {Path}", path);
                return new FileContentResult(path, "", false, ex.Message);
            }
        }

        public async Task<bool> WriteFileAsync(
            string path, string content, CancellationToken ct = default)
        {
            try
            {
                var res = await _http.PostAsJsonAsync(
                    "/api/files/write", new { path, content }, ct);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WriteFile failed for {Path}", path);
                return false;
            }
        }

        public async Task<TestRunSummary> RunTestsAsync(
            string? filter = null, CancellationToken ct = default)
        {
            try
            {
                var url = filter is not null
                    ? $"/api/tests/run?filter={Uri.EscapeDataString(filter)}"
                    : "/api/tests/run";
                var result = await _http.PostAsJsonAsync(url, new { }, ct);
                result.EnsureSuccessStatusCode();
                return await result.Content.ReadFromJsonAsync<TestRunSummary>(cancellationToken: ct)
                       ?? new TestRunSummary(0, 0, 0, 0, 0, [], "No response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RunTests failed");
                return new TestRunSummary(0, 0, 0, 0, 0, [],
                    $"Error: {ex.Message}");
            }
        }

        public async Task<MemoryViewModel> GetMemoryAsync(CancellationToken ct = default)
        {
            try
            {
                var result = await _http.GetFromJsonAsync<MemoryViewModel>("/api/memory", ct);
                return result ?? new MemoryViewModel([], null, null, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMemory failed");
                return new MemoryViewModel([], null, null, 0);
            }
        }

        public async Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(
            string query, int topK = 5, CancellationToken ct = default)
        {
            try
            {
                var result = await _http.PostAsJsonAsync(
                    "/api/memory/search", new MemorySearchRequest(query, topK), ct);
                return await result.Content
                           .ReadFromJsonAsync<List<MemoryEntry>>(cancellationToken: ct)
                       ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchMemory failed");
                return [];
            }
        }

        public async Task<bool> AddMemoryAsync(string content, CancellationToken ct = default)
        {
            try
            {
                var res = await _http.PostAsJsonAsync("/api/memory", new { content }, ct);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddMemory failed");
                return false;
            }
        }

        public async Task<bool> DeleteMemoryAsync(string id, CancellationToken ct = default)
        {
            try
            {
                var res = await _http.DeleteAsync($"/api/memory/{id}", ct);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteMemory failed");
                return false;
            }
        }
    }
}
