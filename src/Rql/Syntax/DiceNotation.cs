using System.Globalization;
using Rql.Nodes;

namespace Rql.Syntax;

/// <summary>
/// Roll20's dice notation: a count, <c>d</c>, the sides or <c>F</c> for Fate dice, then any modifiers.
/// <code>
/// dice     := [COUNT] ('d'|'D') (SIDES | 'F') { modifier }
/// modifier := '!' ['!' | 'p'] [compare]        explode, compound, penetrate
///           | ('k' | 'kh' | 'kl' | 'd' | 'dh' | 'dl') [N]   keep or drop (k is kh, d is dl)
///           | 'r' ['o'] compare                reroll, or reroll once
///           | ('&gt;' | '&lt;' | '=') N               count successes
///           | 'f' compare                      count failures
///           | ('cs' | 'cf') compare            what counts as a critical success or failure
///           | 'm' ['t'] [N] [compare']         match dice that came up the same, or count the sets
///           | 's' ['a' | 'd']                  sort
/// compare  := ['&gt;' | '&lt;' | '='] N               &gt; is at least, &lt; is at most, a bare N is exactly
/// </code>
/// The same reader finds where dice end, for the lexer, and works out what they mean, for the parser, so the
/// two can't disagree.
/// </summary>
public static class DiceNotation
{
    /// <summary>Where dice written at <paramref name="start"/> end, or -1 if there aren't any there.</summary>
    /// <remarks>Dice run straight into a letter or digit aren't dice, so <c>d6x</c> is a word.</remarks>
    public static int End(string source, int start)
    {
        var reader = new Reader(source, start);
        if (!reader.Prefix(out _, out _))
        {
            return -1;
        }
        try
        {
            reader.Modifiers(build: false);
        }
        catch (FormatException)
        {
            // A modifier that's malformed still ends the dice; the parser says what's wrong with it.
        }
        int end = reader.Position;
        return end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_') ? -1 : end;
    }

    /// <summary>
    /// Where dice written with a count at <paramref name="start"/> end, running on through anything after
    /// them that's part of the same word, or -1 if there's no count and <c>d</c> there. A count can't start
    /// a name, so <c>4d6kq</c> is dice with a modifier that's wrong, rather than 4 and then a name.
    /// </summary>
    public static int EndWithCount(string source, int start)
    {
        if (start >= source.Length || !char.IsAsciiDigit(source[start]))
        {
            return -1;
        }
        var reader = new Reader(source, start);
        if (!reader.Prefix(out _, out _))
        {
            return -1;
        }
        try
        {
            reader.Modifiers(build: false);
        }
        catch (FormatException)
        {
        }
        int end = reader.Position;
        while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_'))
        {
            end++;
        }
        return end;
    }

    /// <summary>Where modifiers written at <paramref name="start"/> end, for those written after a group.</summary>
    public static int ModifiersEnd(string source, int start)
    {
        var reader = new Reader(source, start);
        try
        {
            reader.Modifiers(build: false);
        }
        catch (FormatException)
        {
        }
        return reader.Position;
    }

    /// <summary>Reads what's written after a group's closing brace, like the <c>kh1</c> in <c>{d20, d20}kh1</c>.</summary>
    /// <exception cref="DiceNotationException">Saying where in the text the problem is.</exception>
    public static GroupModifiers ParseGroupModifiers(string text)
    {
        var reader = new Reader(text, 0);
        Dice read;
        try
        {
            read = reader.Modifiers(build: true, new Dice(1, Dice.MostSides))!;
        }
        catch (FormatException e)
        {
            throw new DiceNotationException(e.Message, reader.Position);
        }
        if (reader.Position != text.Length)
        {
            throw new DiceNotationException($"\"{text[reader.Position..]}\" isn't a group modifier", reader.Position);
        }
        if (read.Explode is not null || read.Rerolls.Count > 0 || read.Sort != DiceSort.None || read.Match is not null
            || read.CriticalSuccess.Count > 0 || read.CriticalFailure.Count > 0)
        {
            throw new DiceNotationException("a group can only keep, drop, or count successes and failures", 0);
        }
        if (read.Failure is not null && read.Success is null)
        {
            throw new DiceNotationException("failures (f) only count against successes, so give a success target too, like >5f1", 0);
        }
        return new GroupModifiers(read.KeepDrop, read.Success, read.Failure);
    }

    /// <summary>Whether a whole word reads as dice, which is why no table can be called <c>d6</c> or <c>dF</c>.</summary>
    public static bool IsDice(string word) => End(word, 0) == word.Length;

    /// <summary>Reads dice written as a whole, such as <c>4d6kh3</c>.</summary>
    /// <exception cref="DiceNotationException">Saying where in the text the problem is.</exception>
    public static Dice Parse(string text)
    {
        var reader = new Reader(text, 0);
        if (!reader.Prefix(out string count, out string sides))
        {
            throw new DiceNotationException($"{text} isn't dice", 0);
        }
        int dice = count.Length == 0 ? 1 : Whole(count, 0);

        Dice built;
        if (sides is "F" or "f")
        {
            built = Dice.FateDice(dice);
        }
        else
        {
            int faces = Whole(sides, count.Length + 1);
            if (faces < 1)
            {
                throw new DiceNotationException("a die needs at least 1 side", 0);
            }
            if (faces > Dice.MostSides)
            {
                throw new DiceNotationException($"a die can have at most {Dice.MostSides} sides, not {faces}", count.Length + 1);
            }
            built = new Dice(dice, faces);
        }

        try
        {
            built = reader.Modifiers(build: true, built)!;
        }
        catch (FormatException e)
        {
            throw new DiceNotationException(e.Message, reader.Position);
        }
        if (reader.Position != text.Length)
        {
            throw new DiceNotationException($"\"{text[reader.Position..]}\" isn't a dice modifier", reader.Position);
        }
        return built.Problem() is string problem ? throw new DiceNotationException(problem, 0) : built;
    }

    private static int Whole(string digits, int at) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new DiceNotationException($"the number {digits} is too large", at);

    private sealed class Reader(string source, int position)
    {
        public int Position => position;

        private char Peek(int ahead = 0) => position + ahead < source.Length ? source[position + ahead] : '\0';

        private string Digits()
        {
            int start = position;
            while (char.IsAsciiDigit(Peek()))
            {
                position++;
            }
            return source[start..position];
        }

        public bool Prefix(out string count, out string sides)
        {
            int start = position;
            count = Digits();
            sides = "";
            if (Peek() is not ('d' or 'D'))
            {
                position = start;
                return false;
            }
            position++;
            if (Peek() is 'F' or 'f')
            {
                sides = Peek().ToString();
                position++;
                return true;
            }
            sides = Digits();
            if (sides.Length == 0)
            {
                position = start;
                return false;
            }
            return true;
        }

        /// <summary>Reads modifiers until there are none left, building them onto <paramref name="dice"/> if asked.</summary>
        public Dice? Modifiers(bool build, Dice? dice = null)
        {
            List<Reroll> rerolls = [];
            while (true)
            {
                char c = Peek();
                if (c == '!')
                {
                    position++;
                    var kind = ExplodeKind.Standard;
                    if (Peek() == '!')
                    {
                        kind = ExplodeKind.Compounding;
                        position++;
                    }
                    else if (Peek() is 'p' or 'P')
                    {
                        kind = ExplodeKind.Penetrating;
                        position++;
                    }
                    Comparison? on = Compare(required: false);
                    if (build)
                    {
                        dice = Once(dice!, dice!.Explode, "explode") with { Explode = new Explode(kind, on) };
                    }
                }
                else if (c is 'k' or 'K' or 'd' or 'D')
                {
                    bool keep = c is 'k' or 'K';
                    position++;
                    bool highest = keep;
                    if (Peek() is 'h' or 'H')
                    {
                        highest = true;
                        position++;
                    }
                    else if (Peek() is 'l' or 'L')
                    {
                        highest = false;
                        position++;
                    }
                    string amount = Digits();
                    if (build)
                    {
                        int count = amount.Length == 0 ? 1 : Whole(amount, position - amount.Length);
                        dice = Once(dice!, dice!.KeepDrop, "keep or drop") with { KeepDrop = new KeepDrop(keep, highest, count) };
                    }
                }
                else if (c is 'r' or 'R')
                {
                    position++;
                    bool once = false;
                    if (Peek() is 'o' or 'O')
                    {
                        once = true;
                        position++;
                    }
                    rerolls.Add(new Reroll(Compare(required: true)!, once));
                    if (build)
                    {
                        dice = dice! with { Rerolls = [.. rerolls] };
                    }
                }
                else if (c is '>' or '<' or '=')
                {
                    Comparison success = Compare(required: true)!;
                    if (build)
                    {
                        dice = Once(dice!, dice!.Success, "success target") with { Success = success };
                    }
                }
                else if (c is 'f' or 'F')
                {
                    position++;
                    Comparison failure = Compare(required: true)!;
                    if (build)
                    {
                        dice = Once(dice!, dice!.Failure, "failure target") with { Failure = failure };
                    }
                }
                else if (c is 'c' or 'C' && Peek(1) is 's' or 'S' or 'f' or 'F')
                {
                    bool success = Peek(1) is 's' or 'S';
                    position += 2;
                    Comparison critical = Compare(required: true)!;
                    if (build)
                    {
                        dice = success
                            ? dice! with { CriticalSuccess = [.. dice!.CriticalSuccess, critical] }
                            : dice! with { CriticalFailure = [.. dice!.CriticalFailure, critical] };
                    }
                }
                else if (c is 'm' or 'M')
                {
                    position++;
                    bool count = false;
                    if (Peek() is 't' or 'T')
                    {
                        count = true;
                        position++;
                    }
                    string least = Digits();
                    // A comparison straight after belongs to the match, as the faces a match has to be.
                    Comparison? only = Peek() is '>' or '<' or '=' ? Compare(required: true) : null;
                    if (build)
                    {
                        int minimum = least.Length == 0 ? 2 : Whole(least, position - least.Length);
                        dice = Once(dice!, dice!.Match, "match") with { Match = new Match(count, minimum, only) };
                    }
                }
                else if (c is 's' or 'S')
                {
                    position++;
                    var sort = DiceSort.Ascending;
                    if (Peek() is 'a' or 'A')
                    {
                        position++;
                    }
                    else if (Peek() is 'd' or 'D' && !char.IsAsciiDigit(Peek(1)) && Peek(1) is not ('h' or 'H' or 'l' or 'L'))
                    {
                        sort = DiceSort.Descending;
                        position++;
                    }
                    if (build)
                    {
                        dice = dice! with { Sort = sort };
                    }
                }
                else
                {
                    return dice;
                }
            }
        }

        private static Dice Once(Dice dice, object? already, string what) =>
            already is null ? dice : throw new FormatException($"dice can only have one {what}");

        private Comparison? Compare(bool required)
        {
            var kind = CompareKind.Equal;
            char c = Peek();
            if (c is '>' or '<' or '=')
            {
                kind = c switch { '>' => CompareKind.AtLeast, '<' => CompareKind.AtMost, _ => CompareKind.Equal };
                position++;
            }
            string digits = Digits();
            if (digits.Length == 0)
            {
                if (required || kind != CompareKind.Equal || c == '=')
                {
                    throw new FormatException("expected a number to compare the dice with");
                }
                return null;
            }
            return new Comparison(kind, Whole(digits, position - digits.Length));
        }
    }
}

/// <summary>Dice notation that can't be read, with where in it the problem is.</summary>
public sealed class DiceNotationException(string message, int offset) : FormatException(message)
{
    /// <summary>Where in the notation the problem is, counting from 0.</summary>
    public int Offset { get; } = offset;
}
