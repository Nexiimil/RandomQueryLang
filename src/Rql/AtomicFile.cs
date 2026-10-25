namespace Rql;

internal static class AtomicFile
{
    /// <summary>
    /// Writes beside the file and moves it into place, so a failed write never leaves half a file.
    /// </summary>
    public static void WriteAllText(string path, string text)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }
}
