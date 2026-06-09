
namespace DevAssistant.Security
{

    /// <summary>
    /// Validates and resolves file paths to prevent directory traversal attacks.
    /// All file operations must go through this before touching the filesystem.
    /// </summary>
    public static class PathValidator
    {
        /// <summary>
        /// Resolves a relative path against the workspace root and verifies
        /// it cannot escape the workspace via ../ or absolute path tricks.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the resolved path escapes the workspace root.
        /// </exception>
        public static string ResolveSafe(string workspaceRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Path cannot be empty", nameof(relativePath));

            // Reject absolute paths — must be relative to workspace
            if (Path.IsPathRooted(relativePath))
                throw new UnauthorizedAccessException(
                    $"Absolute paths are not allowed: '{relativePath}'");

            // Reject obviously malicious patterns
            if (relativePath.Contains('\0'))
                throw new UnauthorizedAccessException("Path contains null bytes");

            var root = Path.GetFullPath(workspaceRoot);
            var resolved = Path.GetFullPath(Path.Combine(root, relativePath));

            // The resolved path must start with the workspace root
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolved, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    $"Path '{relativePath}' escapes the workspace root. " +
                    $"Resolved to '{resolved}', workspace is '{root}'");
            }

            return resolved;
        }

        /// <summary>
        /// Returns true if the path is safe, false otherwise. Non-throwing version.
        /// </summary>
        public static bool IsSafe(string workspaceRoot, string relativePath, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            try
            {
                resolvedPath = ResolveSafe(workspaceRoot, relativePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Allowed file extensions for reading. Prevents reading binaries/secrets.
        /// </summary>
        public static readonly HashSet<string> AllowedReadExtensions = new(
            StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".json", ".xml", ".md",
        ".txt", ".yaml", ".yml", ".http", ".env.example",
        ".razor", ".html", ".css", ".js", ".ts", ".sql"
    };

        public static void EnsureAllowedExtension(string path)
        {
            var ext = Path.GetExtension(path);
            if (!AllowedReadExtensions.Contains(ext))
                throw new UnauthorizedAccessException(
                    $"Reading files with extension '{ext}' is not allowed. " +
                    $"Allowed: {string.Join(", ", AllowedReadExtensions)}");
        }
    }
}
