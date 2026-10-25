using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Rql.Tables;

public enum ImportFormat
{
    Csv,
    Json,

    /// <summary>RandomQueryLang's own <c>name;value;chance</c> lines.</summary>
    Rql,
}

/// <summary>
/// Reads tables written in other formats, CSV and JSON, into tables as they'd be written here, so they can be
/// looked over and saved.
/// </summary>
/// <remarks>
/// Rows can give a <b>chance</b> (0 to 1, or a percentage like <c>25%</c>), as here, or a <b>weight</b>, as Roll20's
/// rollable tables do, where a row of weight 2 comes up twice as often as one of weight 1. Weights are turned
/// into chances; a row with no weight counts as 1. A table can't mix the two.
/// </remarks>
public static class TableImport
{
    private static readonly string[] NameColumns = ["name", "item", "result", "entry", "text", "thing"];
    private static readonly string[] ValueColumns = ["value", "points", "score"];
    private static readonly string[] ChanceColumns = ["chance", "probability", "percent", "percentage"];
    private static readonly string[] WeightColumns = ["weight", "odds", "frequency"];

    /// <summary>Works out a file's format from its name, or failing that, what it starts with.</summary>
    public static ImportFormat Detect(string text, string? fileName = null)
    {
        string extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (extension == ".json")
        {
            return ImportFormat.Json;
        }
        if (extension is ".csv" or ".tsv")
        {
            return ImportFormat.Csv;
        }
        if (string.Equals(extension, TableStore.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return ImportFormat.Rql;
        }
        string start = text.TrimStart('﻿', ' ', '\t', '\r', '\n');
        return start.StartsWith('[') || start.StartsWith('{') ? ImportFormat.Json : ImportFormat.Csv;
    }

    /// <summary>
    /// Every table in <paramref name="text"/>. A format that holds one table calls it <paramref name="name"/>,
    /// made into a name a table can have; JSON that names its tables keeps those names.
    /// </summary>
    /// <exception cref="TableFormatException">The text can't be read as tables, saying where.</exception>
    public static IReadOnlyList<TableDefinition> Import(string text, string name, ImportFormat? format = null)
    {
        text = text.TrimStart('﻿');
        string table = SuggestName(name);
        return (format ?? Detect(text)) switch
        {
            ImportFormat.Json => FromJson(text, table),
            ImportFormat.Rql => [TableDefinition.Parse(table, text)],
            _ => [FromCsv(text, table)],
        };
    }

    /// <summary>
    /// A name a table can have, made from any text: a file name, or a name from another program. Spaces and
    /// other characters a table's name can't have become underscores, and dots are kept as folders.
    /// </summary>
    public static string SuggestName(string raw)
    {
        string stem = raw.EndsWith(TableStore.Extension, StringComparison.OrdinalIgnoreCase)
            ? raw[..^TableStore.Extension.Length]
            : Path.GetExtension(raw).ToLowerInvariant() is ".csv" or ".tsv" or ".json" or ".txt" ? Path.GetFileNameWithoutExtension(raw) : raw;
        stem = Path.GetFileName(stem.Replace('\\', '/'));
        var parts = stem.Split(TableCatalog.Separator)
            .Select(part =>
            {
                var cleaned = new StringBuilder();
                foreach (char c in part.Trim())
                {
                    cleaned.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
                }
                string name = cleaned.ToString().Trim('_');
                while (name.Contains("__"))
                {
                    name = name.Replace("__", "_");
                }
                if (name.Length == 0)
                {
                    return "";
                }
                if (char.IsDigit(name[0]))
                {
                    name = "_" + name;
                }
                return TableCatalog.IsValidName(name) ? name : name + "_";
            })
            .Where(part => part.Length > 0)
            .ToList();
        return parts.Count == 0 ? "Imported" : string.Join(TableCatalog.Separator, parts);
    }

    // ---- CSV ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A table from CSV. With a header row, columns are found by name (name, value, chance or weight, and a few
    /// others meaning the same) and any others are ignored; without one, they're name, value and chance, as in
    /// a table file. Commas, semicolons or tabs can separate fields, and fields can be quoted.
    /// </summary>
    public static TableDefinition FromCsv(string text, string name)
    {
        List<(List<string> Fields, int Line)> records = [.. Records(text, Delimiter(text))
            .Where(record => record.Fields.Any(field => field.Trim().Length > 0))];
        if (records.Count == 0)
        {
            return new TableDefinition(name, []);
        }

        int nameColumn = 0, valueColumn = 1, chanceColumn = 2, weightColumn = -1;
        List<string> header = [.. records[0].Fields.Select(field => field.Trim().ToLowerInvariant())];
        if (header.Any(field => NameColumns.Contains(field) || ValueColumns.Contains(field) || ChanceColumns.Contains(field) || WeightColumns.Contains(field)))
        {
            int Column(string[] names) => header.FindIndex(names.Contains);
            nameColumn = Column(NameColumns);
            valueColumn = Column(ValueColumns);
            chanceColumn = Column(ChanceColumns);
            weightColumn = Column(WeightColumns);
            if (nameColumn < 0)
            {
                throw new TableFormatException(name, "the header has no name column (name, item or result)", 0, records[0].Line);
            }
            records.RemoveAt(0);
        }

        var rows = new List<RawRow>();
        foreach (var (fields, line) in records)
        {
            string Field(int column) => column >= 0 && column < fields.Count ? fields[column].Trim() : "";
            rows.Add(new RawRow(Field(nameColumn), Field(valueColumn), Field(chanceColumn),
                weightColumn >= 0 ? Field(weightColumn) : "", rows.Count + 1, line));
        }
        return Build(name, rows);
    }

    /// <summary>The separator the first line uses most, of commas, semicolons and tabs, outside quotes.</summary>
    private static char Delimiter(string text)
    {
        string first = text.Split('\n').FirstOrDefault(line => line.Trim().Length > 0) ?? "";
        var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0 };
        bool quoted = false;
        foreach (char c in first)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && counts.ContainsKey(c))
            {
                counts[c]++;
            }
        }
        return counts.MaxBy(count => count.Value) is { Value: > 0 } most ? most.Key : ',';
    }

    /// <summary>CSV records, as RFC 4180 has them: quotes around a field, "" for a quote, and newlines allowed inside quotes.</summary>
    private static IEnumerable<(List<string> Fields, int Line)> Records(string text, char delimiter)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        int line = 1, start = 1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }
                    field.Append(c);
                }
            }
            else if (c == '"' && field.ToString().Trim().Length == 0)
            {
                field.Clear();
                quoted = true;
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                fields.Add(field.ToString());
                field.Clear();
                yield return (fields, start);
                fields = [];
                line++;
                start = line;
            }
            else
            {
                field.Append(c);
            }
        }
        if (quoted)
        {
            throw new TableFormatException("CSV", "a quoted field is missing its closing quote", 0, start);
        }
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return (fields, start);
        }
    }

    // ---- JSON --------------------------------------------------------------------------------------------

    /// <summary>
    /// Tables from JSON: a list of names or of rows; an object with a <c>name</c> and its rows under
    /// <c>rows</c>, <c>items</c>, <c>entries</c> or <c>results</c> (as Roll20 gives a rollable table); or several
    /// of those, as a list or under <c>tables</c>. A row is a name, or an object with a name and any of value,
    /// chance and weight.
    /// </summary>
    public static IReadOnlyList<TableDefinition> FromJson(string text, string name)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException e)
        {
            int line = (int)(e.LineNumber ?? 0) + 1;
            throw new TableFormatException(name, $"this isn't JSON: {e.Message.Split(". Path")[0]}", 0, line);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && Property(root, "tables") is { ValueKind: JsonValueKind.Array } tables)
            {
                return Tables(tables, name);
            }
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && root.EnumerateArray().All(IsTable))
            {
                return Tables(root, name);
            }
            return [Table(root, name)];
        }
    }

    private static List<TableDefinition> Tables(JsonElement list, string name)
    {
        var tables = new List<TableDefinition>();
        int index = 0;
        foreach (JsonElement table in list.EnumerateArray())
        {
            index++;
            tables.Add(Table(table, $"{name}_{index}"));
        }
        return tables;
    }

    private static bool IsTable(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && RowsOf(element) is not null;

    private static JsonElement? RowsOf(JsonElement table) =>
        new[] { "rows", "items", "entries", "results" }
            .Select(key => Property(table, key))
            .FirstOrDefault(rows => rows is { ValueKind: JsonValueKind.Array });

    private static TableDefinition Table(JsonElement element, string fallback)
    {
        string name = fallback;
        JsonElement rows = element;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if ((Property(element, "name") ?? Property(element, "title")) is { ValueKind: JsonValueKind.String } named
                && named.GetString() is { Length: > 0 } written)
            {
                name = SuggestName(written);
            }
            rows = RowsOf(element) ?? throw new TableFormatException(name, "expected the table's rows under rows, items, entries or results");
        }
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw new TableFormatException(name, "expected a list of rows");
        }

        var raw = new List<RawRow>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            int number = raw.Count + 1;
            switch (row.ValueKind)
            {
                case JsonValueKind.String:
                    raw.Add(new RawRow(row.GetString()!, "", "", "", number, null));
                    break;
                case JsonValueKind.Number:
                    raw.Add(new RawRow(row.GetRawText(), "", "", "", number, null));
                    break;
                case JsonValueKind.Object:
                    string Text(params string[] keys) =>
                        keys.Select(key => Property(row, key)).FirstOrDefault(value => value is not null) is { } value
                            ? value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ValueKind == JsonValueKind.Null ? "" : value.GetRawText()
                            : "";
                    raw.Add(new RawRow(Text(NameColumns), Text(ValueColumns), Text(ChanceColumns), Text(WeightColumns), number, null));
                    break;
                default:
                    throw new TableFormatException(name, "a row has to be a name, or an object with a name", number);
            }
        }
        return Build(name, raw);
    }

    private static JsonElement? Property(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }
        return null;
    }

    // ---- Rows, whatever they were read from --------------------------------------------------------------

    private sealed record RawRow(string Name, string Value, string Chance, string Weight, int Row, int? Line);

    private static TableDefinition Build(string name, List<RawRow> raw)
    {
        if (raw.Any(row => row.Chance.Length > 0) && raw.Any(row => row.Weight.Length > 0))
        {
            throw new TableFormatException(name, "rows give both chances and weights; use one or the other");
        }

        bool weighted = raw.Any(row => row.Weight.Length > 0);
        var weights = new List<double>();
        var rows = new List<TableRow>();
        foreach (RawRow row in raw)
        {
            TableFormatException Problem(string message) => new(name, message, row.Row, row.Line);

            int? value = null;
            if (row.Value.Length > 0)
            {
                if (!decimal.TryParse(row.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)
                    || number != decimal.Truncate(number) || number is < int.MinValue or > int.MaxValue)
                {
                    throw Problem($"value \"{row.Value}\" is not a whole number");
                }
                value = (int)number;
            }

            double? chance = null;
            if (row.Chance.Length > 0)
            {
                bool percent = row.Chance.EndsWith('%');
                if (!double.TryParse(percent ? row.Chance[..^1] : row.Chance, NumberStyles.Float, CultureInfo.InvariantCulture, out double read))
                {
                    throw Problem($"chance \"{row.Chance}\" is not a number");
                }
                chance = percent ? read / 100 : read;
            }

            if (weighted)
            {
                double weight = 1;
                if (row.Weight.Length > 0 && (!double.TryParse(row.Weight, NumberStyles.Float, CultureInfo.InvariantCulture, out weight) || weight < 0))
                {
                    throw Problem($"weight \"{row.Weight}\" is not a number of 0 or more");
                }
                weights.Add(weight);
            }

            // A spreadsheet cell can hold line breaks, which a row's name can't, so they become spaces.
            string rowName = System.Text.RegularExpressions.Regex.Replace(row.Name.Trim(), @"\s*[\r\n]+\s*", " ");
            var built = new TableRow(rowName, value, chance);
            if (built.Problem() is string problem)
            {
                throw Problem(problem);
            }
            rows.Add(built);
        }

        if (weighted)
        {
            double total = weights.Sum();
            if (total <= 0)
            {
                throw new TableFormatException(name, "every row has a weight of 0, so none could be rolled");
            }
            rows = [.. rows.Select((row, index) => row with { Chance = weights[index] / total })];
        }
        return new TableDefinition(name, rows);
    }
}
