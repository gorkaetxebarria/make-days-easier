using Mde.Tool.Integrations;

namespace Mde.Tool.Tests;

internal sealed class FakeFileSystem(string currentDirectory, params string[] directories) : IFileSystem {
    private readonly HashSet<string> directorySet = new(directories, StringComparer.Ordinal);

    public string GetCurrentDirectory() => currentDirectory;
    public bool DirectoryExists(string path) => directorySet.Contains(path);
    public string Combine(string left, string right) => Path.Combine(left, right);
}
