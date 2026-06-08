namespace CqrsGenerator.Core.Generation;

public interface IPreparedApplyFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string content);

    void CreateDirectory(string path);

    void DeleteFile(string path);

    void DeleteDirectory(string path);

    bool IsDirectoryEmpty(string path);
}
