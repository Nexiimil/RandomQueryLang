using System.Globalization;

namespace Rql.Tables;

/// <summary>
/// A table as it is written: its name and rows, with any value or chance a row left out still left out.
/// <see cref="Resolve"/> fills those in to give a <see cref="Table"/> that can be rolled on.
/// </summary>
/// <remarks>
/// A row without a value is worth its position from the top of the table, starting at 1 like Pick does,
/// unless its name is a whole number, which it is then worth. Rows without a chance share whatever the
/// rows with one leave over, equally.
/// </remarks>
/// <param name="Name">The table's full name, folders and all.</param>
/// <param name="Rows">The rows, in order.</param>
public sealed record TableDefinition(string Name, IReadOnlyList<TableRow> Rows)
{
    private const double Epsilon = 1e-9;

    /// <summary>The rows as written, copied so the table doesn't change if the list it was made from does.</summary>
    public IReadOnlyList<TableRow> Rows { get; } = [.. Rows];

    /// <summary>
    /// Reads rows of the form <c>name;value;chance</c>, one per line. The value and chance can be left
    /// out, or left empty as in <c>Sword;;0.5</c>, and blank lines are skipped.
    /// </summary>
    public static TableDefinition Parse(string name, IEnumerable<string> lines)
    {
        var rows = new List<TableRow>();
        int lineNumber = 0;
        foreach (string raw in lines)
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            TableRow row = ParseRow(name, rows.Count + 1, lineNumber, line);
            if (row.Problem() is string problem)
            {
                throw new TableFormatException(name, problem, rows.Count + 1, lineNumber);
            }
            rows.Add(row);
        }
        return new TableDefinition(name, rows);
    }

    /// <summary>Reads a whole file's worth of text, as <see cref="Parse(string, IEnumerable{string})"/> does.</summary>
    public static TableDefinition Parse(string name, string text) =>
        Parse(name, text.Split('\n').Select(line => line.TrimEnd('\r')));

    private static TableRow ParseRow(string table, int row, int line, string text)
    {
        string[] fields = text.Split(';');
        if (fields.Length > 3)
        {
            throw new TableFormatException(table,
                $"expected at most 3 fields (name;value;chance) but found {fields.Length}", row, line);
        }

        string name = fields[0].Trim();

        int? value = null;
        if (fields.Length > 1 && !string.IsNullOrWhiteSpace(fields[1]))
        {
            string given = fields[1].Trim();
            if (!int.TryParse(given, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new TableFormatException(table, $"value \"{given}\" is not a whole number", row, line);
            }
            value = parsed;
        }

        double? chance = null;
        if (fields.Length > 2 && !string.IsNullOrWhiteSpace(fields[2]))
        {
            string given = fields[2].Trim();
            if (!double.TryParse(given, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                throw new TableFormatException(table, $"chance \"{given}\" is not a decimal number", row, line);
            }
            chance = parsed;
        }

        return new TableRow(name, value, chance);
    }

    /// <summary>The lines of a table file that reads back as this definition.</summary>
    public IEnumerable<string> ToLines() => Rows.Select(row => row.ToLine());

    /// <summary>
    /// Fills in every value and chance a row left out, giving a table that can be rolled on.
    /// </summary>
    /// <exception cref="TableFormatException">
    /// A row is malformed, the chances add up to more than 1, or they add up to exactly 1 while some rows
    /// have no chance, so those rows could never be rolled.
    /// </exception>
    public Table Resolve()
    {
        for (int i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Problem() is string problem)
            {
                throw new TableFormatException(Name, problem, i + 1);
            }
        }

        double[] chances = ResolveChances();
        var things = new List<Thing>(Rows.Count);
        for (int i = 0; i < Rows.Count; i++)
        {
            things.Add(new Thing(Rows[i].Name.Trim(), ValueOf(i), chances[i]));
        }
        return new Table(Name, things);
    }

    /// <summary>
    /// What the row at <paramref name="index"/> is worth, which is what it says it's worth if it says.
    /// </summary>
    public int ValueOf(int index)
    {
        TableRow row = Rows[index];
        if (row.Value is int value)
        {
            return value;
        }
        // A row named with a whole number is worth that number, so a table of 1 to 6 acts like a die.
        return int.TryParse(row.Name.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int named)
            ? named
            : index + 1;
    }

    private double[] ResolveChances()
    {
        double given = Rows.Sum(row => row.Chance ?? 0);
        int unspecified = Rows.Count(row => row.Chance is null);

        if (given > 1 + Epsilon)
        {
            throw new TableFormatException(Name,
                $"chances add up to {given.ToString("0.###", CultureInfo.InvariantCulture)}, which is more than 1");
        }
        double remainder = Math.Max(0, 1 - given);
        if (unspecified > 0 && remainder <= Epsilon)
        {
            throw new TableFormatException(Name,
                "chances already add up to 1, so rows without a chance could never be rolled");
        }

        return [.. Rows.Select(row => row.Chance ?? remainder / unspecified)];
    }
}
