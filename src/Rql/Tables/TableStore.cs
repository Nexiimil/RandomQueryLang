namespace Rql.Tables;

/// <summary>
/// A directory of table files, searched through every folder inside it. Each file ending in
/// <see cref="Extension"/> is one table, named by its path from the top of the directory without the
/// extension, with dots between the folders: <c>Monsters/Undead.RQLtable</c> is <c>Monsters.Undead</c>.
/// Every folder and table name has to be one a query can write as it is (see <see cref="IsValidName"/>);
/// a file that breaks that is reported by <see cref="LoadAll"/> rather than read as something else.
/// Hidden folders and files, such as <c>.git</c>, are skipped.
/// </summary>
public sealed class TableStore
{
    public const string Extension = ".RQLtable";

    private static readonly EnumerationOptions Everywhere = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MaxRecursionDepth = 32,
        MatchCasing = MatchCasing.CaseSensitive,
    };

    public TableStore(string directory)
    {
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    /// <summary>The names of every table, in order.</summary>
    public IReadOnlyList<string> Names()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }
        return [.. TableFiles().Select(NameOf).Where(name => name is not null).Cast<string>().Order(StringComparer.Ordinal)];
    }

    /// <summary>Table files whose folders or own name break <see cref="NameRule"/>, by their path from the top.</summary>
    public IReadOnlyList<string> Unusable() =>
        [.. TableFiles().Where(file => NameOf(file) is null)
            .Select(file => Path.GetRelativePath(Directory, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)];

    private IEnumerable<string> TableFiles() =>
        System.IO.Directory.Exists(Directory)
            ? System.IO.Directory.EnumerateFiles(Directory, "*" + Extension, Everywhere)
            : [];

    /// <summary>Every folder tables could be put in, by its path from the top, in order.</summary>
    public IReadOnlyList<string> Folders()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }
        return [.. System.IO.Directory.EnumerateDirectories(Directory, "*", Everywhere)
            .Select(folder => Path.GetRelativePath(Directory, folder).Split(Path.DirectorySeparatorChar))
            .Where(segments => segments.All(IsValidSegment))
            .Select(segments => string.Join(TableCatalog.Separator, segments))
            .Order(StringComparer.Ordinal)];
    }

    public bool Exists(string name) => IsValidName(name) && File.Exists(PathOf(name));

    /// <summary>Reads a table as it was written, without filling anything in.</summary>
    /// <exception cref="TableFormatException">The file can't be read as a table.</exception>
    public TableDefinition Read(string name) =>
        // ReadAllLines drops the byte order mark some Windows editors start UTF-8 files with, which would
        // otherwise end up in the first row's name.
        TableDefinition.Parse(name, File.ReadAllLines(PathOf(name)));

    /// <summary>A table file exactly as it is, for fixing one that can't be read as a table.</summary>
    public string ReadText(string name) => File.ReadAllText(PathOf(name));

    /// <summary>
    /// Every table that can be rolled on, and what's wrong with any that can't, so one bad file doesn't
    /// keep the rest from loading.
    /// </summary>
    public LoadedTables LoadAll()
    {
        var tables = new Dictionary<string, Table>(StringComparer.Ordinal);
        var problems = new List<TableProblem>();
        foreach (string name in Names())
        {
            try
            {
                tables[name] = Read(name).Resolve();
            }
            catch (TableFormatException e)
            {
                problems.Add(new TableProblem(name, e.Line is int line ? $"line {line}: {e.Problem}" : e.Problem));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add(new TableProblem(name, e.Message));
            }
        }
        foreach (string file in Unusable())
        {
            problems.Add(new TableProblem(file,
                $"can't be used as a table, because {NameRule}"));
        }
        return new LoadedTables(new TableCatalog(tables), problems);
    }

    /// <summary>
    /// Saves a table, replacing whatever it was saved as before and making any folders its name needs.
    /// Giving <paramref name="previousName"/> renames or moves it, refusing to overwrite a different table
    /// that already has the new name, and tidies away any folder the move leaves empty.
    /// </summary>
    public void Save(TableDefinition table, string? previousName = null)
    {
        if (!IsValidName(table.Name))
        {
            throw new ArgumentException($"\"{table.Name}\" can't be used as a table name", nameof(table));
        }
        bool renaming = previousName is not null && previousName != table.Name;
        if (renaming && Exists(table.Name))
        {
            throw new IOException($"there is already a table named \"{table.Name}\"");
        }

        AtomicFile.WriteAllText(PathOf(table.Name), string.Concat(table.ToLines().Select(line => line + "\n")));

        if (renaming)
        {
            Delete(previousName!);
        }
    }

    public void Delete(string name)
    {
        if (!Exists(name))
        {
            return;
        }
        string path = PathOf(name);
        File.Delete(path);
        RemoveEmptyFolders(Path.GetDirectoryName(path)!);
    }

    private void RemoveEmptyFolders(string folder)
    {
        while (folder.Length > Directory.Length && folder.StartsWith(Directory, StringComparison.Ordinal)
            && !System.IO.Directory.EnumerateFileSystemEntries(folder).Any())
        {
            System.IO.Directory.Delete(folder);
            folder = Path.GetDirectoryName(folder)!;
        }
    }

    /// <inheritdoc cref="TableCatalog.NameRule"/>
    public const string NameRule = TableCatalog.NameRule;

    /// <summary>
    /// Whether a table can be saved under this name: the same names a query can use, which also keeps
    /// every part a file name any system can have.
    /// </summary>
    public static bool IsValidName(string name) => TableCatalog.IsValidName(name);

    /// <summary>One folder or file name, which can't have a dot in it, since dots are what separate them.</summary>
    private static bool IsValidSegment(string segment) =>
        !segment.Contains(TableCatalog.Separator) && TableCatalog.IsValidName(segment);

    /// <summary>A file's table name, or null where a folder or the table's own name can't be one.</summary>
    private string? NameOf(string file)
    {
        string relative = Path.GetRelativePath(Directory, file);
        if (!relative.EndsWith(Extension, StringComparison.Ordinal))
        {
            return null;
        }
        string[] segments = relative[..^Extension.Length].Split(Path.DirectorySeparatorChar);
        return segments.All(IsValidSegment) ? string.Join(TableCatalog.Separator, segments) : null;
    }

    private string PathOf(string name) =>
        Path.Combine(Directory, name.Replace(TableCatalog.Separator, Path.DirectorySeparatorChar) + Extension);
}

public sealed record LoadedTables(TableCatalog Tables, IReadOnlyList<TableProblem> Problems)
{
    public static LoadedTables None { get; } = new(TableCatalog.Empty, []);
}

/// <summary>A table that couldn't be loaded, and why, not counting its name.</summary>
public sealed record TableProblem(string Name, string Message);
