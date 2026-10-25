using System.Globalization;

namespace Rql.Tables;

/// <summary>
/// A row as it is written, before the gaps are filled in. A row that leaves out its value or chance is
/// different from one that happens to give the same numbers, because the gaps are worked out from the
/// rest of the table, so this is what an editor works on and what a file is saved from.
/// </summary>
public sealed record TableRow(string Name, int? Value = null, double? Chance = null)
{
    /// <summary>What's wrong with this row on its own, or null when nothing is.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return "row has no name";
        }
        if (Name.Contains(';'))
        {
            return "a name can't contain ';', which separates the name from the value and chance";
        }
        if (Name.Contains('\n') || Name.Contains('\r'))
        {
            return "a name can't span more than one line";
        }
        if (Chance is double chance && !(chance >= 0 && chance <= 1))
        {
            return $"chance must be between 0 and 1 but was {Format(chance)}";
        }
        return null;
    }

    /// <summary>The line of a table file that reads back as this row, leaving out anything not given.</summary>
    public string ToLine()
    {
        string name = Name.Trim();
        if (Chance is double chance)
        {
            return $"{name};{Value?.ToString(CultureInfo.InvariantCulture)};{Format(chance)}";
        }
        return Value is int value ? $"{name};{value.ToString(CultureInfo.InvariantCulture)}" : name;
    }

    internal static string Format(double chance) => chance.ToString(CultureInfo.InvariantCulture);
}
