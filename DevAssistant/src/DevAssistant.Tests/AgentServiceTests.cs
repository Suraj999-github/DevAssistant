using DevAssistant.Models;
using DevAssistant.Services;
using DevAssistant.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace DevAssistant.Tests
{
    //public class AgentServiceTests
    //{
    //    private readonly Mock<WebEnvironmentHealthChecker> _healthMock = new(); 
    //    private readonly Mock<ILlmChatService> _llmMock = new();
    //    private readonly Mock<IMemoryService> _memoryMock = new();
    //    private readonly Mock<IFileBrowserService> _filesMock = new();
    //    private readonly Mock<ITestRunnerService> _testsMock = new();

    //    // Matches the exact constructor order in AgentService:
    //    // (health, llm, memory, files, tests, logger)
    //    private AgentService BuildSut() => new(
    //        _healthMock.Object,
    //        _llmMock.Object,
    //        _memoryMock.Object,
    //        _filesMock.Object,
    //        _testsMock.Object,
    //        NullLogger<AgentService>.Instance);

    //    // ── GetHealthAsync ────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetHealthAsync_DelegatesToHealthChecker()
    //    {
    //        var expected = new HealthReport(
    //                         OllamaOnline: true,
    //                         QdrantOnline: true,
    //                         OllamaModel: "llama3",
    //                         OllamaVersion: "0.6.0",
    //                         PulledModels: new[] { "llama3", "nomic-embed-text" },
    //                         WorkspaceReady: true,
    //                         WorkspacePath: "/workspace",
    //                         CheckedAt: new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
    //                    );

    //        _healthMock.Setup(h => h.RunAsync(default)).ReturnsAsync(expected);

    //        var result = await BuildSut().GetHealthAsync();

    //        Assert.Equal(expected, result);
    //        _healthMock.Verify(h => h.RunAsync(default), Times.Once);
    //    }

    //    // ── StreamChatAsync ───────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task StreamChatAsync_YieldsTokensFromLlmResponse()
    //    {
    //        _llmMock
    //            .Setup(l => l.StreamChatAsync(
    //                "hello", null, It.IsAny<ChatHistory>(), default))
    //            .ReturnsAsync("Hi there from the LLM!");

    //        var tokens = new List<string>();
    //        await foreach (var token in BuildSut().StreamChatAsync("hello", null))
    //            tokens.Add(token);

    //        Assert.NotEmpty(tokens);
    //        Assert.Equal("Hi there from the LLM!", string.Concat(tokens));
    //    }

    //    [Fact]
    //    public async Task StreamChatAsync_LongResponse_ChunkedInto50CharPieces()
    //    {
    //        var longText = new string('A', 175); // 175 chars → 4 chunks: 50+50+50+25
    //        _llmMock
    //            .Setup(l => l.StreamChatAsync(
    //                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ChatHistory>(), default))
    //            .ReturnsAsync(longText);

    //        var tokens = new List<string>();
    //        await foreach (var token in BuildSut().StreamChatAsync("msg", null))
    //            tokens.Add(token);

    //        Assert.Equal(4, tokens.Count);
    //        Assert.All(tokens.Take(3), t => Assert.Equal(50, t.Length));
    //        Assert.Equal(25, tokens.Last().Length);
    //    }

    //    [Fact]
    //    public async Task StreamChatAsync_EmptyLlmResponse_YieldsNothing()
    //    {
    //        _llmMock
    //            .Setup(l => l.StreamChatAsync(
    //                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ChatHistory>(), default))
    //            .ReturnsAsync(string.Empty);

    //        var tokens = new List<string>();
    //        await foreach (var token in BuildSut().StreamChatAsync("msg", null))
    //            tokens.Add(token);

    //        Assert.Empty(tokens);
    //    }

    //    [Fact]
    //    public async Task StreamChatAsync_PassesSystemPromptToLlm()
    //    {
    //        _llmMock
    //            .Setup(l => l.StreamChatAsync(
    //                "msg", "be concise", It.IsAny<ChatHistory>(), default))
    //            .ReturnsAsync("ok");

    //        await foreach (var _ in BuildSut().StreamChatAsync("msg", "be concise")) { }

    //        _llmMock.Verify(l => l.StreamChatAsync(
    //            "msg", "be concise", It.IsAny<ChatHistory>(), default), Times.Once);
    //    }

    //    // ── GetChatResponseAsync ──────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetChatResponseAsync_DelegatesToLlm()
    //    {
    //        var history = new ChatHistory();
    //        _llmMock
    //            .Setup(l => l.StreamChatAsync("hi", null, history, default))
    //            .ReturnsAsync("response text");

    //        var result = await BuildSut().GetChatResponseAsync("hi", history);

    //        Assert.Equal("response text", result);
    //    }

    //    [Fact]
    //    public async Task GetChatResponseAsync_AlwaysPassesNullSystemPrompt()
    //    {
    //        var history = new ChatHistory();
    //        _llmMock
    //            .Setup(l => l.StreamChatAsync(It.IsAny<string>(), null, It.IsAny<ChatHistory>(), default))
    //            .ReturnsAsync("ok");

    //        await BuildSut().GetChatResponseAsync("msg", history);

    //        _llmMock.Verify(l => l.StreamChatAsync(
    //            It.IsAny<string>(), null, It.IsAny<ChatHistory>(), default), Times.Once);
    //    }

    //    // ── GetFilesAsync ─────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetFilesAsync_DelegatesToFileBrowserService()
    //    {
    //        var expected = new FileBrowserViewModel(".", [], null, null);
    //        _filesMock.Setup(f => f.GetFilesAsync(".", default)).ReturnsAsync(expected);

    //        var result = await BuildSut().GetFilesAsync(".");

    //        Assert.Equal(expected, result);
    //        _filesMock.Verify(f => f.GetFilesAsync(".", default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task GetFilesAsync_CustomPath_ForwardedToService()
    //    {
    //        var expected = new FileBrowserViewModel("src", [], null, null);
    //        _filesMock.Setup(f => f.GetFilesAsync("src", default)).ReturnsAsync(expected);

    //        var result = await BuildSut().GetFilesAsync("src");

    //        Assert.Equal("src", result.CurrentPath);
    //    }

    //    [Fact]
    //    public async Task GetFilesAsync_ServiceThrows_ReturnsFallbackWithSamePath()
    //    {
    //        _filesMock
    //            .Setup(f => f.GetFilesAsync(It.IsAny<string>(), default))
    //            .ThrowsAsync(new IOException("disk error"));

    //        var result = await BuildSut().GetFilesAsync("src");

    //        Assert.Equal("src", result.CurrentPath);
    //        Assert.Empty(result.Entries);
    //        Assert.Null(result.FileContent);
    //        Assert.Null(result.SelectedFile);
    //    }

    //    // ── GetFileContentAsync ───────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetFileContentAsync_DelegatesToFileBrowserService()
    //    {
    //        var expected = new FileContentResult("Program.cs", "// code", true);
    //        _filesMock.Setup(f => f.GetFileContentAsync("Program.cs", default)).ReturnsAsync(expected);

    //        var result = await BuildSut().GetFileContentAsync("Program.cs");

    //        Assert.Equal(expected, result);
    //        _filesMock.Verify(f => f.GetFileContentAsync("Program.cs", default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task GetFileContentAsync_ServiceThrows_ReturnsFallback()
    //    {
    //        _filesMock
    //            .Setup(f => f.GetFileContentAsync(It.IsAny<string>(), default))
    //            .ThrowsAsync(new FileNotFoundException("not found"));

    //        var result = await BuildSut().GetFileContentAsync("missing.cs");

    //        Assert.False(result.Success);
    //        Assert.Equal("missing.cs", result.Path);
    //        Assert.Equal(string.Empty, result.Content);
    //        Assert.NotNull(result.Error);
    //    }

    //    [Fact]
    //    public async Task GetFileContentAsync_ServiceThrows_ErrorMessageFromException()
    //    {
    //        _filesMock
    //            .Setup(f => f.GetFileContentAsync(It.IsAny<string>(), default))
    //            .ThrowsAsync(new Exception("access denied"));

    //        var result = await BuildSut().GetFileContentAsync("x.cs");

    //        Assert.Contains("access denied", result.Error);
    //    }

    //    // ── WriteFileAsync ────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task WriteFileAsync_DelegatesToFileBrowserService()
    //    {
    //        _filesMock.Setup(f => f.WriteFileAsync("out.cs", "content", default)).ReturnsAsync(true);

    //        var result = await BuildSut().WriteFileAsync("out.cs", "content");

    //        Assert.True(result);
    //        _filesMock.Verify(f => f.WriteFileAsync("out.cs", "content", default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task WriteFileAsync_ServiceReturnsFalse_ReturnsFalse()
    //    {
    //        _filesMock.Setup(f => f.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>(), default))
    //                  .ReturnsAsync(false);

    //        var result = await BuildSut().WriteFileAsync("x.cs", "y");

    //        Assert.False(result);
    //    }

    //    [Fact]
    //    public async Task WriteFileAsync_ServiceThrows_ReturnsFalse()
    //    {
    //        _filesMock
    //            .Setup(f => f.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>(), default))
    //            .ThrowsAsync(new UnauthorizedAccessException("read only"));

    //        var result = await BuildSut().WriteFileAsync("x.cs", "y");

    //        Assert.False(result);
    //    }

    //    // ── RunTestsAsync ─────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task RunTestsAsync_NoFilter_DelegatesToTestRunnerService()
    //    {
    //        var expected = new TestRunSummary(10, 9, 1, 0, 250, [], "output");
    //        _testsMock.Setup(t => t.RunAsync(null, default)).ReturnsAsync(expected);

    //        var result = await BuildSut().RunTestsAsync();

    //        Assert.Equal(10, result.Total);
    //        Assert.Equal(9, result.Passed);
    //        _testsMock.Verify(t => t.RunAsync(null, default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task RunTestsAsync_WithFilter_PassesFilterToService()
    //    {
    //        var expected = new TestRunSummary(2, 2, 0, 0, 50, [], "filtered output");
    //        _testsMock.Setup(t => t.RunAsync("MemoryServiceTests", default)).ReturnsAsync(expected);

    //        var result = await BuildSut().RunTestsAsync("MemoryServiceTests");

    //        Assert.Equal(2, result.Total);
    //        _testsMock.Verify(t => t.RunAsync("MemoryServiceTests", default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task RunTestsAsync_ServiceThrows_ReturnsFallbackWithErrorMessage()
    //    {
    //        _testsMock
    //            .Setup(t => t.RunAsync(It.IsAny<string?>(), default))
    //            .ThrowsAsync(new Exception("process crashed"));

    //        var result = await BuildSut().RunTestsAsync();

    //        Assert.Equal(0, result.Total);
    //        Assert.Equal(0, result.Passed);
    //        Assert.Equal(0, result.Failed);
    //        Assert.Contains("process crashed", result.RawOutput);
    //        Assert.StartsWith("Error:", result.RawOutput);
    //    }

    //    [Fact]
    //    public async Task RunTestsAsync_ServiceThrows_ResultsListIsEmpty()
    //    {
    //        _testsMock
    //            .Setup(t => t.RunAsync(It.IsAny<string?>(), default))
    //            .ThrowsAsync(new Exception("boom"));

    //        var result = await BuildSut().RunTestsAsync();

    //        Assert.Empty(result.Results);
    //    }

    //    // ── GetMemoryAsync ────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task GetMemoryAsync_DelegatesToMemoryService()
    //    {
    //        var entries = new List<MemoryEntry>
    //    {
    //        new("id1", "JWT uses RS256", "default", DateTime.UtcNow)
    //    };
    //        var expected = new MemoryViewModel(entries, null, null, 1);
    //        _memoryMock.Setup(m => m.GetAllAsync(default)).ReturnsAsync(expected);

    //        var result = await BuildSut().GetMemoryAsync();

    //        Assert.Equal(1, result.TotalCount);
    //        Assert.Equal("JWT uses RS256", result.Entries[0].Content);
    //        _memoryMock.Verify(m => m.GetAllAsync(default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task GetMemoryAsync_ServiceThrows_ReturnsFallback()
    //    {
    //        _memoryMock.Setup(m => m.GetAllAsync(default))
    //                   .ThrowsAsync(new Exception("io error"));

    //        var result = await BuildSut().GetMemoryAsync();

    //        Assert.Empty(result.Entries);
    //        Assert.Equal(0, result.TotalCount);
    //        Assert.Null(result.SearchResults);
    //        Assert.Null(result.SearchQuery);
    //    }

    //    // ── SearchMemoryAsync ─────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task SearchMemoryAsync_DelegatesToMemoryService()
    //    {
    //        var expected = new List<MemoryEntry>
    //    {
    //        new("id1", "JWT auth fact", "default", DateTime.UtcNow, 0.9)
    //    };
    //        _memoryMock.Setup(m => m.SearchAsync("JWT", 5, default)).ReturnsAsync(expected);

    //        var result = await BuildSut().SearchMemoryAsync("JWT");

    //        Assert.Single(result);
    //        Assert.Equal("JWT auth fact", result[0].Content);
    //        _memoryMock.Verify(m => m.SearchAsync("JWT", 5, default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task SearchMemoryAsync_CustomTopK_ForwardedToService()
    //    {
    //        _memoryMock
    //            .Setup(m => m.SearchAsync("auth", 10, default))
    //            .ReturnsAsync(new List<MemoryEntry>());

    //        await BuildSut().SearchMemoryAsync("auth", topK: 10);

    //        _memoryMock.Verify(m => m.SearchAsync("auth", 10, default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task SearchMemoryAsync_ServiceThrows_ReturnsEmptyList()
    //    {
    //        _memoryMock
    //            .Setup(m => m.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), default))
    //            .ThrowsAsync(new Exception("search failed"));

    //        var result = await BuildSut().SearchMemoryAsync("anything");

    //        Assert.Empty(result);
    //    }

    //    // ── AddMemoryAsync ────────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task AddMemoryAsync_DelegatesToMemoryService()
    //    {
    //        _memoryMock.Setup(m => m.AddAsync("new fact", default)).ReturnsAsync(true);

    //        var result = await BuildSut().AddMemoryAsync("new fact");

    //        Assert.True(result);
    //        _memoryMock.Verify(m => m.AddAsync("new fact", default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task AddMemoryAsync_ServiceReturnsFalse_ReturnsFalse()
    //    {
    //        _memoryMock.Setup(m => m.AddAsync(It.IsAny<string>(), default)).ReturnsAsync(false);

    //        var result = await BuildSut().AddMemoryAsync("empty");

    //        Assert.False(result);
    //    }

    //    [Fact]
    //    public async Task AddMemoryAsync_ServiceThrows_ReturnsFalse()
    //    {
    //        _memoryMock
    //            .Setup(m => m.AddAsync(It.IsAny<string>(), default))
    //            .ThrowsAsync(new Exception("write error"));

    //        var result = await BuildSut().AddMemoryAsync("fact");

    //        Assert.False(result);
    //    }

    //    // ── DeleteMemoryAsync ─────────────────────────────────────────────────────

    //    [Fact]
    //    public async Task DeleteMemoryAsync_DelegatesToMemoryService()
    //    {
    //        _memoryMock.Setup(m => m.DeleteAsync("abc123", default)).ReturnsAsync(true);

    //        var result = await BuildSut().DeleteMemoryAsync("abc123");

    //        Assert.True(result);
    //        _memoryMock.Verify(m => m.DeleteAsync("abc123", default), Times.Once);
    //    }

    //    [Fact]
    //    public async Task DeleteMemoryAsync_UnknownId_ReturnsFalse()
    //    {
    //        _memoryMock.Setup(m => m.DeleteAsync("ghost", default)).ReturnsAsync(false);

    //        var result = await BuildSut().DeleteMemoryAsync("ghost");

    //        Assert.False(result);
    //    }

    //    [Fact]
    //    public async Task DeleteMemoryAsync_ServiceThrows_ReturnsFalse()
    //    {
    //        _memoryMock
    //            .Setup(m => m.DeleteAsync(It.IsAny<string>(), default))
    //            .ThrowsAsync(new Exception("delete failed"));

    //        var result = await BuildSut().DeleteMemoryAsync("id");

    //        Assert.False(result);
    //    }
    //}
}
