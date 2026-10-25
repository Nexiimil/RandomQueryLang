using Rql.Tables;

namespace Rql.Projects;

/// <summary>
/// A folder of tables, found in it and every folder inside it, along with the queries saved for them. Any
/// folder can be opened as a project; nothing marks it as one until a query is saved.
/// </summary>
public sealed class Project
{
    /// <summary>The file in the top of the project that saved queries are kept in.</summary>
    public const string QueriesFile = "saved-queries.json";

    public Project(string directory)
    {
        Directory = System.IO.Path.GetFullPath(directory);
        Tables = new TableStore(Directory);
        Queries = new SavedQueryStore(System.IO.Path.Combine(Directory, QueriesFile));
    }

    public string Directory { get; }

    /// <summary>The project's folder name.</summary>
    public string Name => System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Directory)) is { Length: > 0 } name
        ? name
        : Directory;

    public TableStore Tables { get; }

    public SavedQueryStore Queries { get; }

    public bool Exists => System.IO.Directory.Exists(Directory);
}
