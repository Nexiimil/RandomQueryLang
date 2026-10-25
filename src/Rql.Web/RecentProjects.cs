using System.Text.Json;

namespace Rql.Web;

/// <summary>
/// The project folders opened lately, most recent first, kept in a file so they're there next time. The
/// first that still exists is what opens when the app starts.
/// </summary>
public sealed class RecentProjects(string file)
{
    private const int Kept = 12;
    private readonly Lock gate = new();

    public string File { get; } = Path.GetFullPath(file);

    public IReadOnlyList<string> All()
    {
        lock (gate)
        {
            return Read();
        }
    }

    /// <summary>Puts a folder at the top of the list.</summary>
    public void Touch(string directory)
    {
        directory = Path.GetFullPath(directory);
        lock (gate)
        {
            Write([directory, .. Read().Where(other => other != directory).Take(Kept - 1)]);
        }
    }

    public void Forget(string directory)
    {
        lock (gate)
        {
            Write([.. Read().Where(other => other != directory)]);
        }
    }

    private List<string> Read()
    {
        try
        {
            return System.IO.File.Exists(File)
                ? JsonSerializer.Deserialize<List<string>>(System.IO.File.ReadAllText(File)) ?? []
                : [];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Losing the list of recent projects only costs finding them again.
            return [];
        }
    }

    private void Write(List<string> directories)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, JsonSerializer.Serialize(directories, new JsonSerializerOptions { WriteIndented = true }));
    }
}
