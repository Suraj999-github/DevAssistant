using DevAssistant.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevAssistant.Services
{
    public interface IMemoryService
    {
        Task<MemoryViewModel> GetAllAsync(CancellationToken ct = default);
        Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int topK = 5, CancellationToken ct = default);
        Task<bool> AddAsync(string content, CancellationToken ct = default);
        Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    }

    /// <summary>
    /// JSON-file-backed memory store with keyword search.
    /// Swap SearchAsync for Qdrant/SemanticKernel when vector search is ready.
    /// Collection name is configurable via "Memory:Collection" in appsettings.
    /// </summary>
    public sealed class MemoryService : IMemoryService
    {
        private readonly string _storePath;
        private readonly string _collection;
        private readonly ILogger<MemoryService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public MemoryService(IConfiguration config, ILogger<MemoryService> logger)
        {
            _storePath = config["Memory:StorePath"] ?? "memory.json";
            _collection = config["Memory:Collection"] ?? "default";
            _logger = logger;
        }

        public async Task<MemoryViewModel> GetAllAsync(CancellationToken ct = default)
        {
            var entries = await LoadAsync(ct);
            return new MemoryViewModel(entries, null, null, entries.Count);
        }

        public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(
            string query, int topK = 5, CancellationToken ct = default)
        {
            var entries = await LoadAsync(ct);

            if (string.IsNullOrWhiteSpace(query))
                return entries.Take(topK).ToList();

            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return entries
                .Select(e =>
                {
                    var score = KeywordScore(e.Content, terms);
                    // Return a new entry with the RelevanceScore field populated
                    return e with { RelevanceScore = score };
                })
                .Where(e => e.RelevanceScore > 0)
                .OrderByDescending(e => e.RelevanceScore)
                .Take(topK)
                .ToList();
        }

        public async Task<bool> AddAsync(string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            await _lock.WaitAsync(ct);
            try
            {
                var entries = await LoadAsync(ct);
                entries.Add(new MemoryEntry(
                    Id: Guid.NewGuid().ToString("N"),
                    Content: content.Trim(),
                    Collection: _collection,
                    CreatedAt: DateTime.UtcNow));
                await SaveAsync(entries, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Memory] AddAsync failed");
                return false;
            }
            finally { _lock.Release(); }
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var entries = await LoadAsync(ct);
                var removed = entries.RemoveAll(e => e.Id == id);
                if (removed == 0)
                {
                    _logger.LogWarning("[Memory] DeleteAsync — id {Id} not found", id);
                    return false;
                }
                await SaveAsync(entries, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Memory] DeleteAsync failed for id {Id}", id);
                return false;
            }
            finally { _lock.Release(); }
        }

        // ── Persistence ───────────────────────────────────────────────────────────

        private async Task<List<MemoryEntry>> LoadAsync(CancellationToken ct)
        {
            if (!File.Exists(_storePath)) return [];

            var json = await File.ReadAllTextAsync(_storePath, ct);
            return JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? [];
        }

        private async Task SaveAsync(List<MemoryEntry> entries, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(entries,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_storePath, json, ct);
        }

        // Normalised score: hits / total terms, so it's always in [0, 1]
        private static double KeywordScore(string content, string[] terms)
        {
            if (terms.Length == 0) return 0;
            var hits = terms.Count(t => content.Contains(t, StringComparison.OrdinalIgnoreCase));
            return (double)hits / terms.Length;
        }
    }
}
