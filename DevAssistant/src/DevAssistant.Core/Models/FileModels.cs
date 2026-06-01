namespace DevAssistant.Models
{
    public class FileModels
    {
    }
    public sealed record FileEntry(
    string Name,
    string Path,
    string Extension,
    long SizeBytes,
    DateTime LastModified,
    bool IsDirectory);

    public sealed record FileContentResult(
        string Path,
        string Content,
        bool Success,
        string? Error = null);

    public sealed record FileBrowserViewModel(
        string CurrentPath,
        IReadOnlyList<FileEntry> Entries,
        string? FileContent,
        string? SelectedFile);
}
