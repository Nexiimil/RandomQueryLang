using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rql.Projects;

/// <summary>
/// Saved queries, kept in one JSON file so they can be read, edited and shared by hand. Names are unique,
/// ignoring case, and the queries stay in the order they were first saved.
/// </summary>
public sealed class SavedQueryStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record Document(List<SavedQuery> Queries);

    /// <summary>The full path of the JSON file, which doesn't have to exist until a query is saved.</summary>
    public string Path { get; } = System.IO.Path.GetFullPath(path);

    /// <exception cref="InvalidDataException">The file isn't a list of saved queries.</exception>
    public IReadOnlyList<SavedQuery> All()
    {
        if (!File.Exists(Path))
        {
            return [];
        }
        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(Path), Json);
            return [.. (document?.Queries ?? []).Where(query =>
                !string.IsNullOrWhiteSpace(query?.Name) && query.Query is not null)];
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{System.IO.Path.GetFileName(Path)} can't be read: {e.Message}", e);
        }
    }

    /// <summary>The query saved under this name, ignoring case and spaces at either end, or null.</summary>
    /// <exception cref="InvalidDataException">The file isn't a list of saved queries.</exception>
    public SavedQuery? Find(string name) => All().FirstOrDefault(query => Same(query.Name, name));

    /// <summary>
    /// Saves a query, replacing the one with the same name. Giving <paramref name="previousName"/> renames
    /// that one instead, keeping its place, and refuses to overwrite a different query with the new name.
    /// </summary>
    public void Save(SavedQuery query, string? previousName = null)
    {
        query = query with { Name = query.Name.Trim(), Query = query.Query.Trim() };
        if (query.Name.Length == 0)
        {
            throw new ArgumentException("a saved query needs a name", nameof(query));
        }

        List<SavedQuery> queries = [.. All()];
        string replacing = previousName ?? query.Name;
        if (!Same(replacing, query.Name) && queries.Any(other => Same(other.Name, query.Name)))
        {
            throw new IOException($"there is already a saved query named \"{query.Name}\"");
        }

        int at = queries.FindIndex(other => Same(other.Name, replacing));
        if (at < 0)
        {
            queries.Add(query);
        }
        else
        {
            queries[at] = query;
        }
        Write(queries);
    }

    /// <summary>Deletes the query saved under this name, ignoring case, if there is one.</summary>
    /// <exception cref="InvalidDataException">The file isn't a list of saved queries.</exception>
    public void Delete(string name)
    {
        List<SavedQuery> queries = [.. All()];
        if (queries.RemoveAll(query => Same(query.Name, name)) > 0)
        {
            Write(queries);
        }
    }

    private void Write(List<SavedQuery> queries) =>
        AtomicFile.WriteAllText(Path, JsonSerializer.Serialize(new Document(queries), Json) + "\n");

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
