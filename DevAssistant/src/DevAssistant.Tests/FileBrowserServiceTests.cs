namespace DevAssistant.Tests
{
    //public class FileBrowserServiceTests : IDisposable
    //{
    //    private readonly string _root;
    //    private readonly Mock<IWebHostEnvironment> _envMock;
    //    private readonly FileBrowserService _sut;

    //    public FileBrowserServiceTests()
    //    {
    //        _root = Path.Combine(Path.GetTempPath(), $"fbtest_{Guid.NewGuid():N}");
    //        Directory.CreateDirectory(_root);

    //        _envMock = new Mock<IWebHostEnvironment>();
    //        _envMock.Setup(e => e.ContentRootPath).Returns(_root);

    //        _sut = new FileBrowserService(_envMock.Object, NullLogger<FileBrowserService>.Instance);
    //    }

    //    public void Dispose() => Directory.Delete(_root, recursive: true);

    //    // ── GetFilesAsync ─────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetFilesAsync_RootPath_ReturnsAllEntries()
    //    {
    //        File.WriteAllText(Path.Combine(_root, "readme.md"), "hello");
    //        Directory.CreateDirectory(Path.Combine(_root, "src"));

    //        var result = await _sut.GetFilesAsync(".");

    //        Assert.Equal(".", result.CurrentPath);
    //        Assert.Equal(2, result.Entries.Count);
    //    }

    //    [Fact]
    //    public async Task GetFilesAsync_NonExistentPath_ReturnsEmptyEntries()
    //    {
    //        var result = await _sut.GetFilesAsync("does-not-exist");

    //        Assert.Empty(result.Entries);
    //        Assert.Equal("does-not-exist", result.CurrentPath);
    //    }

    //    [Fact]
    //    public async Task GetFilesAsync_DirectoryEntry_HasIsDirectoryTrue()
    //    {
    //        Directory.CreateDirectory(Path.Combine(_root, "subdir"));

    //        var result = await _sut.GetFilesAsync(".");
    //        var entry = Assert.Single(result.Entries);

    //        Assert.True(entry.IsDirectory);
    //        Assert.Equal("subdir", entry.Name);
    //        Assert.Equal(string.Empty, entry.Extension);
    //    }

    //    [Fact]
    //    public async Task GetFilesAsync_FileEntry_HasCorrectMetadata()
    //    {
    //        var content = "test content";
    //        File.WriteAllText(Path.Combine(_root, "app.cs"), content);

    //        var result = await _sut.GetFilesAsync(".");
    //        var entry = Assert.Single(result.Entries);

    //        Assert.False(entry.IsDirectory);
    //        Assert.Equal("app.cs", entry.Name);
    //        Assert.Equal(".cs", entry.Extension);
    //        Assert.True(entry.SizeBytes > 0);
    //    }

    //    [Fact]
    //    public async Task GetFilesAsync_NestedDirectory_ResolvesCorrectly()
    //    {
    //        var nested = Path.Combine(_root, "src");
    //        Directory.CreateDirectory(nested);
    //        File.WriteAllText(Path.Combine(nested, "Program.cs"), "// entry");

    //        var result = await _sut.GetFilesAsync("src");

    //        Assert.Single(result.Entries);
    //        Assert.Equal("Program.cs", result.Entries[0].Name);
    //    }

    //    // ── GetFileContentAsync ───────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetFileContentAsync_ExistingTextFile_ReturnsContent()
    //    {
    //        File.WriteAllText(Path.Combine(_root, "hello.txt"), "Hello world");

    //        var result = await _sut.GetFileContentAsync("hello.txt");

    //        Assert.True(result.Success);
    //        Assert.Equal("Hello world", result.Content);
    //        Assert.Null(result.Error);
    //    }

    //    [Fact]
    //    public async Task GetFileContentAsync_NonExistentFile_ReturnsFailure()
    //    {
    //        var result = await _sut.GetFileContentAsync("ghost.txt");

    //        Assert.False(result.Success);
    //        Assert.Empty(result.Content);
    //        Assert.NotNull(result.Error);
    //    }

    //    [Fact]
    //    public async Task GetFileContentAsync_KnownTextExtensions_Succeeds()
    //    {
    //        var extensions = new[] { ".cs", ".md", ".json", ".yaml", ".html", ".ts", ".razor" };

    //        foreach (var ext in extensions)
    //        {
    //            var filename = $"file{ext}";
    //            File.WriteAllText(Path.Combine(_root, filename), $"content of {filename}");

    //            var result = await _sut.GetFileContentAsync(filename);

    //            Assert.True(result.Success, $"Expected success for {ext}");
    //        }
    //    }

    //    [Fact]
    //    public async Task GetFileContentAsync_BinaryExtension_ReturnsFailure()
    //    {
    //        // .exe is not in the allowed text extensions list
    //        File.WriteAllBytes(Path.Combine(_root, "app.exe"), [0x4D, 0x5A, 0x00]);

    //        var result = await _sut.GetFileContentAsync("app.exe");

    //        Assert.False(result.Success);
    //        Assert.NotNull(result.Error);
    //    }

    //    // ── WriteFileAsync ────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task WriteFileAsync_ValidPath_WritesContentToDisk()
    //    {
    //        var result = await _sut.WriteFileAsync("output.txt", "written content");

    //        Assert.True(result);
    //        Assert.Equal("written content", File.ReadAllText(Path.Combine(_root, "output.txt")));
    //    }

    //    [Fact]
    //    public async Task WriteFileAsync_NestedPath_CreatesDirectories()
    //    {
    //        var result = await _sut.WriteFileAsync("a/b/c/file.txt", "deep content");

    //        Assert.True(result);
    //        Assert.True(File.Exists(Path.Combine(_root, "a", "b", "c", "file.txt")));
    //    }

    //    [Fact]
    //    public async Task WriteFileAsync_OverwriteExistingFile_ReturnsTrue()
    //    {
    //        File.WriteAllText(Path.Combine(_root, "overwrite.txt"), "original");

    //        var result = await _sut.WriteFileAsync("overwrite.txt", "updated");

    //        Assert.True(result);
    //        Assert.Equal("updated", File.ReadAllText(Path.Combine(_root, "overwrite.txt")));
    //    }

    //    [Fact]
    //    public async Task WriteFileAsync_PathTraversal_ThrowsOrReturnsFalse()
    //    {
    //        // ../../../etc/passwd style traversal must be blocked
    //        var result = await _sut.WriteFileAsync("../../etc/passwd", "hacked");

    //        Assert.False(result);
    //    }

    //    [Fact]
    //    public async Task GetFileContentAsync_PathTraversal_ThrowsOrReturnsFalse()
    //    {
    //        var result = await _sut.GetFileContentAsync("../../etc/passwd");

    //        Assert.False(result.Success);
    //    }
    //}
}
