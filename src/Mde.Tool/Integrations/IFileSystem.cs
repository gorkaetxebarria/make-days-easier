namespace Mde.Tool.Integrations;

public interface IFileSystem
{
    string GetCurrentDirectory();

    bool DirectoryExists(string path);

    string Combine(string left, string right);
}
