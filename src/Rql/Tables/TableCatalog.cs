using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Rql.Tables;

/// <summary>
/// The tables a query can use. A table in a folder is named by its path with dots between the folders,
/// the way .NET names a type in a namespace, such as <c>Monsters.Undead</c>. A query can also call it by
/// the end of that path, <c>Undead</c>, as long as no other table's path ends the same way, so moving
/// tables into folders doesn't break the queries that use them.
/// </summary>
/// <remarks>
/// Every table has a name a query can write as it is (see <see cref="IsValidName"/>), so no table is out
/// of reach of a query and no query reaches a table by some other spelling. Indexing it, like any
/// dictionary, takes only the full name; <see cref="Find"/> takes either.
/// </remarks>
public sealed class TableCatalog : IReadOnlyDictionary<string, Table>
{
    public const char Separator = '.';

    /// <summary>What <see cref="IsValidName"/> asks of each folder and table name, for saying so.</summary>
    public const string NameRule =
        "folder and table names can only have letters, digits and _ in them (no spaces or dots), "
        + "and can't start with a digit or be dice, such as d6 or dF";

    private readonly FrozenDictionary<string, Table> tables;

    /// <exception cref="ArgumentException">A table's name isn't one a query could write.</exception>
    public TableCatalog(IEnumerable<KeyValuePair<string, Table>> tables)
    {
        this.tables = tables.ToFrozenDictionary(StringComparer.Ordinal);
        if (this.tables.Keys.FirstOrDefault(name => !IsValidName(name)) is string bad)
        {
            throw new ArgumentException($"\"{bad}\" can't be a table's name: {NameRule}", nameof(tables));
        }
    }

    /// <summary>
    /// Whether a table can be called this, which is its own name with any folders it's in before it,
    /// separated by dots. Each part has to be something a query can write without quotes: letters, digits
    /// and underscores, not starting with a digit, and not something that reads as dice, such as <c>d6</c>,
    /// <c>d6s</c> or <c>dF</c>.
    /// </summary>
    public static bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) && name.Split(Separator).All(IsValidPart);

    private static bool IsValidPart(string part) =>
        part.Length > 0
        && (char.IsLetter(part[0]) || part[0] == '_')
        && part.All(c => char.IsLetterOrDigit(c) || c == '_')
        && !Syntax.DiceNotation.IsDice(part);

    public static TableCatalog Empty { get; } = new([]);

    /// <summary>The table a query means by <paramref name="name"/>, or null with why there isn't one.</summary>
    public Table? Find(string name, out string? problem)
    {
        problem = null;
        if (!IsValidName(name))
        {
            // Text can name a table, but only by a name the table could have been written as anyway.
            problem = $"\"{name}\" isn't a table name: {NameRule}";
            return null;
        }
        if (tables.TryGetValue(name, out Table? exact))
        {
            return exact;
        }

        List<string> matches = [.. Keys.Where(full => EndsWith(full, name)).Order(StringComparer.Ordinal)];
        switch (matches.Count)
        {
            case 1:
                return tables[matches[0]];
            case 0:
                problem = $"no table named \"{name}\"";
                return null;
            default:
                problem = $"more than one table is called \"{name}\" ("
                    + string.Join(", ", matches.Select(match => $"\"{match}\""))
                    + "), so write out which one";
                return null;
        }
    }

    /// <summary>
    /// The shortest name that finds this table: its own name where that's unique, or as much of its folder
    /// as it takes to tell it apart.
    /// </summary>
    public string ShortestName(string fullName)
    {
        string[] segments = fullName.Split(Separator);
        for (int take = 1; take < segments.Length; take++)
        {
            string candidate = string.Join(Separator, segments[^take..]);
            if (ReferenceEquals(Find(candidate, out _), tables.GetValueOrDefault(fullName)))
            {
                return candidate;
            }
        }
        return fullName;
    }

    /// <summary>The folder a table is in, such as <c>Monsters</c>, or empty for the top of the project.</summary>
    public static string FolderOf(string fullName)
    {
        int last = fullName.LastIndexOf(Separator);
        return last < 0 ? "" : fullName[..last];
    }

    /// <summary>A table's own name, without its folder.</summary>
    public static string LeafOf(string fullName) => fullName[(fullName.LastIndexOf(Separator) + 1)..];

    private static bool EndsWith(string full, string name) =>
        full.Length > name.Length && full.EndsWith(name, StringComparison.Ordinal)
        && full[full.Length - name.Length - 1] == Separator;

    public Table this[string key] => tables[key];
    public IEnumerable<string> Keys => tables.Keys;
    public IEnumerable<Table> Values => tables.Values;
    public int Count => tables.Count;
    public bool ContainsKey(string key) => tables.ContainsKey(key);
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Table value) => tables.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<string, Table>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, Table>>)tables).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
