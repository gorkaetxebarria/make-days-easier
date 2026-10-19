namespace Mde.Tool.Integrations;

public sealed class DefaultFileSystem : IFileSystem
{
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string Combine(string left, string right) => Path.Combine(left, right);
}
