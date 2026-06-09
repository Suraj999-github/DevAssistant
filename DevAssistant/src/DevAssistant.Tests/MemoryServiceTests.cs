namespace DevAssistant.Tests
{
    //public class MemoryServiceTests : IDisposable
    //{
    //    private readonly string _storePath;
    //    private readonly MemoryService _sut;

    //    public MemoryServiceTests()
    //    {
    //        _storePath = Path.Combine(Path.GetTempPath(), $"memory_{Guid.NewGuid():N}.json");

    //        var config = new ConfigurationBuilder()
    //            .AddInMemoryCollection(new Dictionary<string, string?>
    //            {
    //                ["Memory:StorePath"] = _storePath,
    //                ["Memory:Collection"] = "test-collection"
    //            })
    //            .Build();

    //        _sut = new MemoryService(config, NullLogger<MemoryService>.Instance);
    //    }

    //    public void Dispose()
    //    {
    //        if (File.Exists(_storePath))
    //            File.Delete(_storePath);
    //    }

    //    // ── AddAsync ──────────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task AddAsync_ValidContent_ReturnsTrue()
    //    {
    //        var result = await _sut.AddAsync("JWT auth uses RS256 with 1h expiry");

    //        Assert.True(result);
    //    }

    //    [Fact]
    //    public async Task AddAsync_ValidContent_PersistsToDisk()
    //    {
    //        await _sut.AddAsync("Qdrant runs on port 6333");

    //        Assert.True(File.Exists(_storePath));
    //        var json = await File.ReadAllTextAsync(_storePath);
    //        Assert.Contains("Qdrant", json);
    //    }

    //    [Fact]
    //    public async Task AddAsync_EmptyContent_ReturnsFalse()
    //    {
    //        var result = await _sut.AddAsync("   ");

    //        Assert.False(result);
    //    }

    //    [Fact]
    //    public async Task AddAsync_NullContent_ReturnsFalse()
    //    {
    //        var result = await _sut.AddAsync(null!);

    //        Assert.False(result);
    //    }

    //    [Fact]
    //    public async Task AddAsync_SetsCollectionFromConfig()
    //    {
    //        await _sut.AddAsync("some fact");

    //        var vm = await _sut.GetAllAsync();
    //        Assert.Equal("test-collection", vm.Entries[0].Collection);
    //    }

    //    [Fact]
    //    public async Task AddAsync_AssignsUniqueIds()
    //    {
    //        await _sut.AddAsync("fact one");
    //        await _sut.AddAsync("fact two");

    //        var vm = await _sut.GetAllAsync();
    //        var ids = vm.Entries.Select(e => e.Id).ToList();
    //        Assert.Equal(ids.Distinct().Count(), ids.Count);
    //    }

    //    [Fact]
    //    public async Task AddAsync_SetsCreatedAtToUtcNow()
    //    {
    //        var before = DateTime.UtcNow.AddSeconds(-1);
    //        await _sut.AddAsync("timestamped fact");
    //        var after = DateTime.UtcNow.AddSeconds(1);

    //        var vm = await _sut.GetAllAsync();
    //        var entry = vm.Entries[0];

    //        Assert.InRange(entry.CreatedAt, before, after);
    //    }

    //    // ── GetAllAsync ───────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetAllAsync_NoEntries_ReturnsEmptyViewModel()
    //    {
    //        var vm = await _sut.GetAllAsync();

    //        Assert.Empty(vm.Entries);
    //        Assert.Equal(0, vm.TotalCount);
    //        Assert.Null(vm.SearchResults);
    //        Assert.Null(vm.SearchQuery);
    //    }

    //    [Fact]
    //    public async Task GetAllAsync_AfterAdding_ReturnsTotalCount()
    //    {
    //        await _sut.AddAsync("entry one");
    //        await _sut.AddAsync("entry two");
    //        await _sut.AddAsync("entry three");

    //        var vm = await _sut.GetAllAsync();

    //        Assert.Equal(3, vm.TotalCount);
    //        Assert.Equal(3, vm.Entries.Count);
    //    }

    //    [Fact]
    //    public async Task GetAllAsync_NoStorageFile_ReturnsEmpty()
    //    {
    //        // File never created — should not throw
    //        var vm = await _sut.GetAllAsync();

    //        Assert.Empty(vm.Entries);
    //    }

    //    // ── DeleteAsync ───────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task DeleteAsync_ExistingId_ReturnsTrue()
    //    {
    //        await _sut.AddAsync("deletable fact");
    //        var vm = await _sut.GetAllAsync();
    //        var id = vm.Entries[0].Id;

    //        var result = await _sut.DeleteAsync(id);

    //        Assert.True(result);
    //    }

    //    [Fact]
    //    public async Task DeleteAsync_ExistingId_RemovesFromStore()
    //    {
    //        await _sut.AddAsync("to be deleted");
    //        var vm = await _sut.GetAllAsync();
    //        var id = vm.Entries[0].Id;

    //        await _sut.DeleteAsync(id);

    //        var after = await _sut.GetAllAsync();
    //        Assert.Empty(after.Entries);
    //    }

    //    [Fact]
    //    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    //    {
    //        await _sut.AddAsync("existing fact");

    //        var result = await _sut.DeleteAsync("nonexistent-id-xyz");

    //        Assert.False(result);
    //    }

    //    [Fact]
    //    public async Task DeleteAsync_OnlyRemovesTargetEntry()
    //    {
    //        await _sut.AddAsync("keep this");
    //        await _sut.AddAsync("delete this");

    //        var vm = await _sut.GetAllAsync();
    //        var toDelete = vm.Entries.First(e => e.Content == "delete this").Id;

    //        await _sut.DeleteAsync(toDelete);

    //        var after = await _sut.GetAllAsync();
    //        Assert.Single(after.Entries);
    //        Assert.Equal("keep this", after.Entries[0].Content);
    //    }

    //    // ── SearchAsync ───────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task SearchAsync_MatchingKeyword_ReturnsResults()
    //    {
    //        await _sut.AddAsync("JWT authentication uses RS256 algorithm");
    //        await _sut.AddAsync("Qdrant vector database on port 6333");

    //        var results = await _sut.SearchAsync("JWT");

    //        Assert.Single(results);
    //        Assert.Contains("JWT", results[0].Content);
    //    }

    //    [Fact]
    //    public async Task SearchAsync_NoMatch_ReturnsEmpty()
    //    {
    //        await _sut.AddAsync("something unrelated");

    //        var results = await _sut.SearchAsync("XYZ_NOMATCH_TOKEN");

    //        Assert.Empty(results);
    //    }

    //    [Fact]
    //    public async Task SearchAsync_MultipleMatches_OrderedByRelevanceScore()
    //    {
    //        await _sut.AddAsync("JWT is used for auth");                // 1 term hit
    //        await _sut.AddAsync("JWT auth with RS256 and JWT refresh"); // 2 term hits

    //        var results = await _sut.SearchAsync("JWT auth", topK: 5);

    //        // Higher score should come first
    //        Assert.True(results[0].RelevanceScore >= results[1].RelevanceScore);
    //    }

    //    [Fact]
    //    public async Task SearchAsync_PopulatesRelevanceScore()
    //    {
    //        await _sut.AddAsync("Qdrant is the vector store");

    //        var results = await _sut.SearchAsync("Qdrant");

    //        Assert.NotNull(results[0].RelevanceScore);
    //        Assert.True(results[0].RelevanceScore > 0);
    //    }

    //    [Fact]
    //    public async Task SearchAsync_RespectsTopK()
    //    {
    //        for (var i = 0; i < 10; i++)
    //            await _sut.AddAsync($"auth fact number {i}");

    //        var results = await _sut.SearchAsync("auth", topK: 3);

    //        Assert.True(results.Count <= 3);
    //    }

    //    [Fact]
    //    public async Task SearchAsync_EmptyQuery_ReturnsTopKEntries()
    //    {
    //        await _sut.AddAsync("fact one");
    //        await _sut.AddAsync("fact two");
    //        await _sut.AddAsync("fact three");

    //        var results = await _sut.SearchAsync("", topK: 2);

    //        Assert.True(results.Count <= 2);
    //    }

    //    [Fact]
    //    public async Task SearchAsync_CaseInsensitive_ReturnsMatch()
    //    {
    //        await _sut.AddAsync("The AUTH service uses JWT");

    //        var results = await _sut.SearchAsync("auth");

    //        Assert.Single(results);
    //    }

    //    // ── Concurrency ───────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task AddAsync_ConcurrentWrites_AllPersisted()
    //    {
    //        var tasks = Enumerable.Range(0, 10)
    //            .Select(i => _sut.AddAsync($"concurrent fact {i}"));

    //        await Task.WhenAll(tasks);

    //        var vm = await _sut.GetAllAsync();
    //        Assert.Equal(10, vm.TotalCount);
    //    }
    //}
}
