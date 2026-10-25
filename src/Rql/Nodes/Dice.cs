using System.Globalization;
using System.Text;
using Rql.Tables;

namespace Rql.Nodes;

/// <summary>How a comparison in dice notation reads a face. Roll20's <c>&gt;</c> and <c>&lt;</c> include the number itself.</summary>
public enum CompareKind
{
    Equal,
    AtLeast,
    AtMost,
}

/// <summary>A condition on a face, such as the <c>&gt;5</c> in <c>d6!&gt;5</c>: a face of at least 5.</summary>
public sealed record Comparison(CompareKind Kind, int Value)
{
    public bool Matches(int face) => Kind switch
    {
        CompareKind.AtLeast => face >= Value,
        CompareKind.AtMost => face <= Value,
        _ => face == Value,
    };

    /// <summary>How it's written. A plain number means equal, except where <c>=</c> is needed to tell it apart.</summary>
    public string Display(bool equalsSign = false) => Kind switch
    {
        CompareKind.AtLeast => ">" + Value.ToString(CultureInfo.InvariantCulture),
        CompareKind.AtMost => "<" + Value.ToString(CultureInfo.InvariantCulture),
        _ => (equalsSign ? "=" : "") + Value.ToString(CultureInfo.InvariantCulture),
    };
}

public enum ExplodeKind
{
    /// <summary><c>!</c>: each die that explodes adds another die.</summary>
    Standard,

    /// <summary><c>!!</c>: a die that explodes adds its next roll to itself, staying one die.</summary>
    Compounding,

    /// <summary><c>!p</c>: like <c>!</c>, but every die added takes 1 off what it rolled.</summary>
    Penetrating,
}

/// <summary>Rolling another die when a face matches, by default the highest face.</summary>
public sealed record Explode(ExplodeKind Kind, Comparison? On = null);

/// <summary>Rolling a face again while it matches, or just the once.</summary>
public sealed record Reroll(Comparison When, bool Once = false);

/// <summary>Keeping or dropping the highest or lowest <c>Amount</c> dice.</summary>
public sealed record KeepDrop(bool Keep, bool Highest, int Amount)
{
    public string Display() => (Keep ? "k" : "d") + (Highest ? "h" : "l") + Amount.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Matching, <c>m</c>: picking out sets of at least <c>MinimumSet</c> dice that came up the same, only counting
/// faces that match <c>Only</c> if it's given, and with <c>mt</c>, making the total how many such sets there are.
/// </summary>
public sealed record Match(bool Count, int MinimumSet = 2, Comparison? Only = null)
{
    public string Display() =>
        (Count ? "mt" : "m") + (MinimumSet == 2 ? "" : MinimumSet.ToString(CultureInfo.InvariantCulture))
        + (Only is null ? "" : Only.Kind == CompareKind.Equal ? "=" + Only.Display() : Only.Display());
}

public enum DiceSort
{
    None,
    Ascending,
    Descending,
}

/// <summary>Rolling dice went wrong in a way that depends on what came up.</summary>
public sealed class DiceRollException(string message) : Exception(message);

/// <summary>
/// Dice such as <c>4d6kh3</c>, described rather than rolled: how many, how many sides, and anything to do
/// to them after they're rolled, in Roll20's notation. Holding the description means a node can roll them
/// as many times as it needs to, and that nothing is rolled until something asks.
/// </summary>
public sealed record Dice
{
    /// <summary>The most dice one roll can come to, counting rerolls and explosions, so nothing rolls forever.</summary>
    public const int MostDice = 10_000;

    /// <summary>The most sides a die can have, so the highest any one die can roll.</summary>
    public const int MostSides = 1000;

    /// <param name="count">How many dice, which can be none, as Roll20 allows for <c>0d6</c> in a group.</param>
    public Dice(int count, int sides)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sides, MostSides);
        Count = count;
        Sides = sides;
    }

    /// <summary>Fate (Fudge) dice, <c>dF</c>, whose faces are -1, 0 and 1.</summary>
    public static Dice FateDice(int count) => new(count, 3) { Fate = true };

    public int Count { get; }

    public int Sides { get; }

    public bool Fate { get; init; }

    public int LowestFace => Fate ? -1 : 1;

    public int HighestFace => Fate ? 1 : Sides;

    public Explode? Explode { get; init; }

    public IReadOnlyList<Reroll> Rerolls { get; init; } = [];

    public KeepDrop? KeepDrop { get; init; }

    /// <summary>Counting dice that match as successes, making the total how many did.</summary>
    public Comparison? Success { get; init; }

    /// <summary>Taking a success away for each die that matches. Only meaningful with <see cref="Success"/>.</summary>
    public Comparison? Failure { get; init; }

    public DiceSort Sort { get; init; }

    /// <summary>
    /// Faces that count as a critical success, <c>cs</c>, which only changes how a die is shown. With none
    /// given, the highest face is one.
    /// </summary>
    public IReadOnlyList<Comparison> CriticalSuccess { get; init; } = [];

    /// <summary>Faces that count as a critical failure, <c>cf</c>. With none given, the lowest face is one.</summary>
    public IReadOnlyList<Comparison> CriticalFailure { get; init; } = [];

    public Match? Match { get; init; }

    /// <summary>A label written after the dice, like the <c>Fire</c> in <c>2d6[Fire]</c>, shown beside the roll.</summary>
    public string? Label { get; init; }

    /// <summary>What's wrong with these dice as a whole, such as a reroll every face would trigger, or null.</summary>
    public string? Problem()
    {
        bool AllFaces(Comparison when) =>
            Enumerable.Range(LowestFace, HighestFace - LowestFace + 1).All(when.Matches);

        if (Explode is { } explode)
        {
            if (Fate)
            {
                return "Fate dice can't explode";
            }
            if (AllFaces(explode.On ?? new Comparison(CompareKind.Equal, HighestFace)))
            {
                return "every face would explode, so it would never stop rolling";
            }
        }
        if (Rerolls.Where(reroll => !reroll.Once).Any(reroll => AllFaces(reroll.When)))
        {
            return "every face would be rerolled, so it would never stop rolling";
        }
        if (Failure is not null && Success is null)
        {
            return "failures (f) only count against successes, so give a success target too, like >5f1";
        }
        if (Match is { Count: true } && Success is not null)
        {
            return "dice can count matches (mt) or successes, but not both";
        }
        if (Match is { MinimumSet: < 2 })
        {
            return "a match needs at least 2 dice";
        }
        return null;
    }

    /// <summary>The dice as written, in a standard order.</summary>
    public string Display()
    {
        var text = new StringBuilder();
        if (Count != 1)
        {
            text.Append(Count.ToString(CultureInfo.InvariantCulture));
        }
        text.Append('d').Append(Fate ? "F" : Sides.ToString(CultureInfo.InvariantCulture));
        if (Explode is { } explode)
        {
            text.Append(explode.Kind switch { ExplodeKind.Compounding => "!!", ExplodeKind.Penetrating => "!p", _ => "!" });
            text.Append(explode.On?.Display() ?? "");
        }
        foreach (Reroll reroll in Rerolls)
        {
            text.Append(reroll.Once ? "ro" : "r").Append(reroll.When.Display());
        }
        text.Append(KeepDrop?.Display() ?? "");
        if (Success is not null)
        {
            text.Append(Success.Display(equalsSign: true));
        }
        if (Failure is not null)
        {
            text.Append('f').Append(Failure.Display());
        }
        foreach (Comparison critical in CriticalSuccess)
        {
            text.Append("cs").Append(critical.Display());
        }
        foreach (Comparison critical in CriticalFailure)
        {
            text.Append("cf").Append(critical.Display());
        }
        text.Append(Match?.Display() ?? "");
        text.Append(Sort switch { DiceSort.Ascending => "s", DiceSort.Descending => "sd", _ => "" });
        return text.ToString();
    }

    public override string ToString() => Display();

    // A record compares lists by reference, so dice written the same way need telling that they are.
    public bool Equals(Dice? other) => other is not null && Display() == other.Display();

    public override int GetHashCode() => Display().GetHashCode(StringComparison.Ordinal);

    /// <exception cref="DiceRollException">The roll came to too many dice, or too large a total.</exception>
    public DiceRoll Roll(Random random)
    {
        var faces = new List<Die>(Count);
        int rolled = 0;

        (int Face, List<int> Rerolled) RollFace()
        {
            var rerolled = new List<int>();
            bool usedOnce = false;
            int face = Next();
            while (true)
            {
                bool again = Rerolls.Any(reroll => !reroll.Once && reroll.When.Matches(face));
                if (!again && !usedOnce && Rerolls.Any(reroll => reroll.Once && reroll.When.Matches(face)))
                {
                    again = usedOnce = true;
                }
                if (!again)
                {
                    return (face, rerolled);
                }
                rerolled.Add(face);
                face = Next();
            }
        }

        int Next()
        {
            if (++rolled > MostDice)
            {
                throw new DiceRollException($"{Display()} came to more than {MostDice} dice");
            }
            return Fate ? random.Next(3) - 1 : random.Next(Sides) + 1;
        }

        Comparison explodeOn = Explode?.On ?? new Comparison(CompareKind.Equal, HighestFace);
        for (int i = 0; i < Count; i++)
        {
            var (face, rerolled) = RollFace();
            switch (Explode?.Kind)
            {
                case null:
                    faces.Add(new Die(face) { Rerolled = rerolled });
                    break;

                case ExplodeKind.Compounding:
                    long total = face;
                    int raw = face;
                    bool exploded = false;
                    while (explodeOn.Matches(raw))
                    {
                        raw = RollFace().Face;
                        total += raw;
                        exploded = true;
                    }
                    faces.Add(new Die(Checked(total)) { Rerolled = rerolled, Exploded = exploded });
                    break;

                default:
                    faces.Add(new Die(face) { Rerolled = rerolled });
                    bool penetrating = Explode.Kind == ExplodeKind.Penetrating;
                    while (explodeOn.Matches(face))
                    {
                        (face, rerolled) = RollFace();
                        faces.Add(new Die(penetrating ? face - 1 : face) { Rerolled = rerolled, Exploded = true });
                    }
                    break;
            }
        }

        if (KeepDrop is { } keepDrop)
        {
            // Ties go to whichever die came first, so what's kept doesn't depend on anything but the faces.
            List<int> order = [.. Enumerable.Range(0, faces.Count).OrderBy(index => faces[index].Value).ThenBy(index => index)];
            if (keepDrop.Highest)
            {
                order.Reverse();
                order = [.. order.GroupBy(index => faces[index].Value).SelectMany(tied => tied.Order())];
            }
            // Picked are the highest or lowest Amount; keeping keeps those, dropping drops them.
            var picked = order.Take(keepDrop.Amount).ToHashSet();
            for (int index = 0; index < faces.Count; index++)
            {
                if (picked.Contains(index) != keepDrop.Keep)
                {
                    faces[index] = faces[index] with { Dropped = true };
                }
            }
        }

        for (int index = 0; index < faces.Count; index++)
        {
            int value = faces[index].Value;
            faces[index] = faces[index] with
            {
                CriticalSuccess = !Fate && (CriticalSuccess.Count == 0 ? value >= HighestFace : CriticalSuccess.Any(c => c.Matches(value))),
                CriticalFailure = !Fate && (CriticalFailure.Count == 0 ? value <= LowestFace : CriticalFailure.Any(c => c.Matches(value))),
            };
        }

        int sets = 0;
        if (Match is { } match)
        {
            // Sets are numbered in the order their value first came up, so the same set is shown alike.
            var setOf = faces.Where(die => !die.Dropped && (match.Only?.Matches(die.Value) ?? true)).GroupBy(die => die.Value)
                .Where(same => same.Count() >= match.MinimumSet)
                .Select((same, number) => (same.Key, number))
                .ToDictionary(set => set.Key, set => set.number);
            sets = setOf.Count;
            for (int index = 0; index < faces.Count; index++)
            {
                if (!faces[index].Dropped && setOf.TryGetValue(faces[index].Value, out int set))
                {
                    faces[index] = faces[index] with { MatchSet = set };
                }
            }
        }

        int result;
        if (Match is { Count: true })
        {
            result = sets;
        }
        else if (Success is { } success)
        {
            for (int index = 0; index < faces.Count; index++)
            {
                if (!faces[index].Dropped)
                {
                    faces[index] = faces[index] with
                    {
                        Success = success.Matches(faces[index].Value),
                        Failure = Failure?.Matches(faces[index].Value) ?? false,
                    };
                }
            }
            result = faces.Count(die => die.Success) - faces.Count(die => die.Failure);
        }
        else
        {
            result = Checked(faces.Where(die => !die.Dropped).Sum(die => (long)die.Value));
        }

        IEnumerable<Die> sorted = Sort switch
        {
            DiceSort.Ascending => faces.OrderBy(die => die.Value),
            DiceSort.Descending => faces.OrderByDescending(die => die.Value),
            _ => faces,
        };
        return new DiceRoll(this, [.. sorted], result);
    }

    private int Checked(long total) =>
        total is < int.MinValue or > int.MaxValue
            ? throw new DiceRollException($"{Display()} came to {total}, which is too large")
            : (int)total;
}

/// <summary>One die as it landed, and what became of it.</summary>
public sealed record Die(int Value)
{
    /// <summary>Faces it rolled before this one, and rerolled.</summary>
    public IReadOnlyList<int> Rerolled { get; init; } = [];

    /// <summary>Rolled because another die exploded, or for a compounding die, having exploded into itself.</summary>
    public bool Exploded { get; init; }

    /// <summary>Left out by keeping or dropping.</summary>
    public bool Dropped { get; init; }

    public bool Success { get; init; }

    public bool Failure { get; init; }

    /// <summary>A critical success: the highest face, or one matching <c>cs</c>. Only changes how it's shown.</summary>
    public bool CriticalSuccess { get; init; }

    /// <summary>A critical failure: the lowest face, or one matching <c>cf</c>. Only changes how it's shown.</summary>
    public bool CriticalFailure { get; init; }

    /// <summary>Which set of matching dice this is in, counting from 0, if it's in one.</summary>
    public int? MatchSet { get; init; }
}

/// <summary>Dice that have been rolled: every die, kept or not, and what they came to.</summary>
/// <param name="Total">
/// The kept dice added up, or with a success target, how many succeeded less how many failed.
/// </param>
public sealed record DiceRoll(Dice Dice, IReadOnlyList<Die> Faces, int Total)
{
    public IEnumerable<Die> Kept => Faces.Where(die => !die.Dropped);

    /// <summary>The kept dice as a table, a row per die named and valued after its face.</summary>
    public Table AsTable()
    {
        List<Die> kept = [.. Kept];
        return new Table(Dice.Display(), [.. kept.Select(die =>
            new Thing(die.Value.ToString(CultureInfo.InvariantCulture), die.Value, 1.0 / Math.Max(1, kept.Count)))]);
    }

    /// <summary>
    /// The roll written out, like <c>4d6kh3: 6, 5, 3, (1) = 14</c>: dropped dice in brackets, an exploded die
    /// marked <c>!</c>, a rerolled one after the faces it replaced, and successes marked <c>*</c>.
    /// </summary>
    public string Describe()
    {
        string Face(Die die)
        {
            string text = string.Concat(die.Rerolled.Select(face => face + "→")) + die.Value
                + (die.Exploded ? "!" : "") + (die.Success ? "*" : "") + (die.Failure ? "×" : "");
            return die.Dropped ? $"({text})" : text;
        }
        string label = Dice.Label is null ? "" : $" [{Dice.Label}]";
        string faces = Faces.Count == 0 ? "no dice" : string.Join(", ", Faces.Select(Face));
        return $"{Dice.Display()}{label}: {faces} = {Total}";
    }
}

/// <summary>What's written after a group's closing brace: keeping or dropping, and counting successes.</summary>
public sealed record GroupModifiers(KeepDrop? KeepDrop, Comparison? Success, Comparison? Failure)
{
    public static GroupModifiers None { get; } = new(null, null, null);

    public bool IsNone => KeepDrop is null && Success is null;

    public string Display() =>
        (KeepDrop?.Display() ?? "") + (Success?.Display(equalsSign: true) ?? "") + (Failure is null ? "" : "f" + Failure.Display());
}
