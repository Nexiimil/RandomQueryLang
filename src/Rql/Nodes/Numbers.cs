using System.Globalization;

namespace Rql.Nodes;

/// <summary>
/// Numbers in a query are decimals, as in Roll20, so <c>7 / 2</c> is 3.5. Some things can only be counted
/// in whole numbers, such as how many times to roll or which row to pick, and those say so rather than
/// quietly rounding.
/// </summary>
public static class Numbers
{
    /// <summary>A number that has to be whole, blaming <paramref name="blame"/> if it isn't.</summary>
    /// <param name="what">What the number is for, as in "expected a whole number of {what}".</param>
    public static int Whole(decimal value, INode blame, string what)
    {
        if (value != decimal.Truncate(value))
        {
            throw new EvalException(blame, $"expected a whole number of {what} but got {Display(value)}");
        }
        return value is < int.MinValue or > int.MaxValue
            ? throw new EvalException(blame, $"{Display(value)} is too many {what}")
            : (int)value;
    }

    /// <summary>A number as it's shown: no trailing zeros, and no point if it's whole.</summary>
    public static string Display(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    /// <summary>Reads a number the way a query writes one, like <c>3</c>, <c>-2</c> or <c>1.5</c>.</summary>
    public static bool TryParse(string text, out decimal value) =>
        decimal.TryParse(text.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
}
