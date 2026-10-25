using System.Net;
using System.Text.RegularExpressions;

namespace Rql.Evaluation;

/// <summary>
/// Something a query asks for when it's run: a roll query, <c>?{Bonus|2}</c>, or a value from a character
/// sheet, <c>@{strength}</c>, which there's no sheet to read, so it's asked for instead.
/// </summary>
/// <param name="Key">What answers are given against. The same key is only asked once in a query.</param>
/// <param name="Question">What to ask.</param>
/// <param name="Default">The answer if none is given, or null when there's nothing to fall back on.</param>
/// <param name="Options">For a roll query of options, the options to pick between; their values are the answers.</param>
/// <param name="FromCharacterSheet">Whether this stands in for a value Roll20 would read from a character sheet.</param>
public sealed record RollPrompt(string Key, string Question, string? Default, IReadOnlyList<PromptOption> Options, bool FromCharacterSheet)
{
    /// <summary>Whether two prompts ask the same thing, comparing the options one by one rather than as lists.</summary>
    public bool Equals(RollPrompt? other) =>
        other is not null && Key == other.Key && Question == other.Question && Default == other.Default
        && FromCharacterSheet == other.FromCharacterSheet && Options.SequenceEqual(other.Options);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Key, Default);
}

/// <summary>One of the options a roll query gives to pick between, <c>?{Size|Small,1|Large,2}</c>.</summary>
/// <param name="Label">What the option is shown as.</param>
/// <param name="Value">What's put in the query when it's picked; the label too, when the option doesn't give one.</param>
public sealed record PromptOption(string Label, string Value);

/// <summary>A query that can't be run until it's told the answers to these.</summary>
public sealed class PromptsNeededException(IReadOnlyList<RollPrompt> prompts)
    : Exception($"this query needs answers for {string.Join(", ", prompts.Select(prompt => prompt.Question))}")
{
    /// <summary>Everything the query asks that has no answer and no default.</summary>
    public IReadOnlyList<RollPrompt> Prompts { get; } = prompts;
}

/// <summary>An inline roll, <c>[[2d6]]</c>, rolled before the rest of the query and put in its place as a number.</summary>
public sealed record InlineRoll(string Text, decimal Value, int Position)
{
    /// <summary>The dice rolled for it.</summary>
    public IReadOnlyList<Nodes.DiceRoll> Rolls { get; init; } = [];

    /// <summary>Groups kept or counted while working it out.</summary>
    public IReadOnlyList<Nodes.GroupRoll> Groups { get; init; } = [];

    /// <summary>Labelled terms in it and what they came to.</summary>
    public IReadOnlyList<Nodes.LabelledValue> Labels { get; init; } = [];
}

/// <summary>
/// Query text that has had things put in place of other things, remembering where in the query as written
/// each character came from, so a mistake can be pointed out where it was written.
/// </summary>
internal sealed class MappedText
{
    private readonly int[] origin;

    public MappedText(string text)
        : this(text, [.. Enumerable.Range(0, text.Length + 1)])
    {
    }

    private MappedText(string text, int[] origin)
    {
        Text = text;
        this.origin = origin;
    }

    public string Text { get; }

    /// <summary>Where in the query as written a position in this text came from.</summary>
    public int OriginOf(int position) =>
        position < 0 ? position : origin[Math.Clamp(position, 0, origin.Length - 1)];

    /// <summary>Puts <paramref name="replacement"/> in place of the text from start to end, all of it coming from start.</summary>
    public MappedText Replace(int start, int end, string replacement)
    {
        int from = origin[start];
        return new MappedText(
            Text[..start] + replacement + Text[end..],
            [.. origin[..start], .. Enumerable.Repeat(from, replacement.Length), .. origin[end..]]);
    }

    /// <summary>Part of the text on its own, still knowing where it came from.</summary>
    public MappedText Slice(int start, int end) => new(Text[start..end], [.. origin[start..end], origin[end]]);
}

/// <summary>
/// What Roll20 does to a query before rolling it, in Roll20's order: macros, then character sheet values,
/// then roll queries, then inline rolls, innermost first. Each is replaced in the text by what it stands for,
/// so, as in Roll20, a macro or an answer can be any part of a query, not only a whole number.
/// </summary>
internal sealed class Expansion(IReadOnlyDictionary<string, string> macros, Func<RollPrompt, string?> answer)
{
    private const int Deepest = 20;

    private static readonly Regex RollCommand = new(@"^\s*/(roll|r|gmroll|gr)\b\s*", RegexOptions.IgnoreCase);
    // Not after & or a letter, so an HTML entity such as &#44; isn't taken for a macro called 44.
    private static readonly Regex Macro = new(@"(?<![&\w])#([A-Za-z0-9_\-]+)");

    private readonly Dictionary<string, string> answered = new(StringComparer.Ordinal);
    private readonly List<RollPrompt> missing = [];

    /// <summary>Every prompt the query asked, in the order it asked them.</summary>
    public List<RollPrompt> Asked { get; } = [];

    public MappedText Expand(string query)
    {
        var text = new MappedText(query);
        if (RollCommand.Match(text.Text) is { Success: true } command)
        {
            text = text.Replace(0, command.Length, "");
        }

        for (int depth = 0; ; depth++)
        {
            // Counted rather than compared, since a macro can stand for itself and change nothing.
            int replaced = 0;
            text = ExpandMacros(text, ref replaced);
            text = ExpandReferences(text, "@{", ref replaced);
            text = ExpandReferences(text, "%{", ref replaced);
            if (replaced == 0)
            {
                break;
            }
            if (depth == Deepest)
            {
                throw new QueryException("macros or character sheet values refer to each other without end", text.OriginOf(0));
            }
        }

        for (int depth = 0; FindOutsideText(text.Text, "?{", 0) >= 0; depth++)
        {
            if (depth == Deepest)
            {
                throw new QueryException("roll queries are nested too deeply", text.OriginOf(FindOutsideText(text.Text, "?{", 0)));
            }
            text = ExpandRollQueries(text);
        }

        if (missing.Count > 0)
        {
            throw new PromptsNeededException(missing);
        }
        return text;
    }

    private MappedText ExpandMacros(MappedText text, ref int replaced)
    {
        foreach (Match match in Macro.Matches(text.Text).Reverse())
        {
            if (InsideText(text.Text, match.Index))
            {
                continue;
            }
            string name = match.Groups[1].Value;
            if (!macros.TryGetValue(name, out string? body))
            {
                throw new QueryException($"no saved query named \"{name}\" (#{name} rolls a saved query)", text.OriginOf(match.Index));
            }
            text = text.Replace(match.Index, match.Index + match.Length, body);
            replaced++;
        }
        return text;
    }

    /// <summary>
    /// Character sheet values, <c>@{...}</c>, and abilities, <c>%{...}</c>, which are asked for, since there
    /// are no character sheets to read them from.
    /// </summary>
    private MappedText ExpandReferences(MappedText text, string opening, ref int replaced)
    {
        for (int start = FindOutsideText(text.Text, opening, 0); start >= 0; start = FindOutsideText(text.Text, opening, start))
        {
            int close = text.Text.IndexOf('}', start);
            if (close < 0)
            {
                throw new QueryException($"{opening}...}} is missing its closing }}", text.OriginOf(start));
            }
            string reference = text.Text[start..(close + 1)];
            string what = text.Text[(start + 2)..close].Trim();
            string question = opening == "@{" ? $"{what} (from a character sheet)" : $"{what} (an ability from a character sheet)";
            string value = Answer(new RollPrompt(reference, question, null, [], FromCharacterSheet: true));
            text = text.Replace(start, close + 1, value);
            start += value.Length;
            replaced++;
        }
        return text;
    }

    /// <summary>
    /// Roll queries: <c>?{Question}</c>, <c>?{Question|default}</c>, or <c>?{Question|Label,value|...}</c> to
    /// pick between options. As in Roll20, the first <c>}</c> ends one, so one inside another is written with
    /// HTML entities such as <c>&amp;#124;</c> for <c>|</c>, which are read once the query is split up.
    /// </summary>
    private MappedText ExpandRollQueries(MappedText text)
    {
        for (int start = FindOutsideText(text.Text, "?{", 0); start >= 0; start = FindOutsideText(text.Text, "?{", start))
        {
            int close = text.Text.IndexOf('}', start);
            if (close < 0)
            {
                throw new QueryException("?{...} is missing its closing }", text.OriginOf(start));
            }
            string[] parts = text.Text[(start + 2)..close].Split('|');
            string question = Decode(parts[0]).Trim();
            if (question.Length == 0)
            {
                throw new QueryException("a roll query needs a question, like ?{Bonus|2}", text.OriginOf(start));
            }

            string? fallback = null;
            List<PromptOption> options = [];
            if (parts.Length == 2)
            {
                fallback = Decode(parts[1]);
            }
            else if (parts.Length > 2)
            {
                options = [.. parts.Skip(1).Select(part =>
                {
                    int comma = part.IndexOf(',');
                    return comma < 0
                        ? new PromptOption(Decode(part).Trim(), Decode(part))
                        : new PromptOption(Decode(part[..comma]).Trim(), Decode(part[(comma + 1)..]));
                })];
                fallback = options[0].Value;
            }

            string value = Answer(new RollPrompt(question, question, fallback, options, FromCharacterSheet: false));
            text = text.Replace(start, close + 1, value);
            // A roll query in the answer is asked next time round, which is how nesting is kept from running away.
            start += value.Length;
        }
        return text;
    }

    private string Answer(RollPrompt prompt)
    {
        if (answered.TryGetValue(prompt.Key, out string? already))
        {
            return already;
        }
        Asked.Add(prompt);
        string? value = answer(prompt);
        if (value is null)
        {
            missing.Add(prompt);
            // Carries on with something in its place, so every missing answer is found in one go.
            value = "0";
        }
        answered[prompt.Key] = value;
        return value;
    }

    private static string Decode(string text) => WebUtility.HtmlDecode(text);

    /// <summary>Where <paramref name="find"/> next appears from <paramref name="from"/>, not counting inside quoted text.</summary>
    internal static int FindOutsideText(string text, string find, int from)
    {
        for (int at = text.IndexOf(find, from, StringComparison.Ordinal); at >= 0; at = text.IndexOf(find, at + 1, StringComparison.Ordinal))
        {
            if (!InsideText(text, at))
            {
                return at;
            }
        }
        return -1;
    }

    /// <summary>Whether a position is inside quoted text, which nothing is put in place of.</summary>
    internal static bool InsideText(string text, int position)
    {
        bool inside = false;
        for (int i = 0; i < position && i < text.Length; i++)
        {
            if (text[i] == '\\' && inside)
            {
                i++;
            }
            else if (text[i] == '"')
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
