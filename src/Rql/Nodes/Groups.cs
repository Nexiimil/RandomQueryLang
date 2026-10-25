namespace Rql.Nodes;

/// <summary>One thing in a group as it came out: a roll's total, or a single die, and what became of it.</summary>
public sealed record GroupEntry(string Text, decimal Value)
{
    /// <summary>Left out by keeping or dropping.</summary>
    public bool Dropped { get; init; }

    /// <summary>Kept and matching the group's success target.</summary>
    public bool Success { get; init; }

    /// <summary>Kept and matching the group's failure target, so it takes away a success.</summary>
    public bool Failure { get; init; }
}

/// <summary>A group as it came out, for showing: what was in it, what was kept or counted, and the total.</summary>
public sealed record GroupRoll(string Text, IReadOnlyList<GroupEntry> Entries, decimal Total)
{
    /// <summary>
    /// The group written out, like <c>{4d6+2d8, 3d20+3}kh1: 17, (12) = 17</c>: dropped entries in brackets and
    /// successes marked <c>*</c>.
    /// </summary>
    public string Describe()
    {
        string Entry(GroupEntry entry)
        {
            string text = Numbers.Display(entry.Value) + (entry.Success ? "*" : "") + (entry.Failure ? "×" : "");
            return entry.Dropped ? $"({text})" : text;
        }
        return $"{Text}: {string.Join(", ", Entries.Select(Entry))} = {Numbers.Display(Total)}";
    }
}

internal static class GroupRules
{
    /// <summary>
    /// Keeps or drops entries by value, then counts successes among those kept, as Roll20 does for a group;
    /// <paramref name="successOffset"/> is added to each before comparing it.
    /// </summary>
    public static (List<GroupEntry> Entries, decimal Total) Apply(List<GroupEntry> entries, GroupModifiers modifiers,
        decimal successOffset = 0, decimal totalOffset = 0)
    {
        if (modifiers.KeepDrop is { } keepDrop)
        {
            List<int> order = [.. Enumerable.Range(0, entries.Count).OrderBy(index => entries[index].Value).ThenBy(index => index)];
            if (keepDrop.Highest)
            {
                order = [.. Enumerable.Range(0, entries.Count).OrderByDescending(index => entries[index].Value).ThenBy(index => index)];
            }
            var picked = order.Take(keepDrop.Amount).ToHashSet();
            for (int index = 0; index < entries.Count; index++)
            {
                if (picked.Contains(index) != keepDrop.Keep)
                {
                    entries[index] = entries[index] with { Dropped = true };
                }
            }
        }

        if (modifiers.Success is { } success)
        {
            bool Matches(Comparison comparison, decimal value) => comparison.Kind switch
            {
                CompareKind.AtLeast => value >= comparison.Value,
                CompareKind.AtMost => value <= comparison.Value,
                _ => value == comparison.Value,
            };
            for (int index = 0; index < entries.Count; index++)
            {
                if (!entries[index].Dropped)
                {
                    decimal compared = entries[index].Value + successOffset;
                    entries[index] = entries[index] with
                    {
                        Success = Matches(success, compared),
                        Failure = modifiers.Failure is { } failure && Matches(failure, compared),
                    };
                }
            }
            return (entries, entries.Count(entry => entry.Success) - entries.Count(entry => entry.Failure));
        }
        return (entries, entries.Where(entry => !entry.Dropped).Sum(entry => entry.Value) + totalOffset);
    }
}

/// <summary>
/// A group of several rolls, like <c>{4d6+2d8, 3d20+3}kh1</c>: keeping, dropping and success targets apply
/// to each roll's total.
/// </summary>
public sealed record GroupOfRolls(IReadOnlyList<Node<decimal>> Items, IReadOnlyList<string> Texts, GroupModifiers Modifiers, string Text)
    : Node<decimal>
{
    /// <inheritdoc/>
    public override IReadOnlyList<INode> Children => [.. Items];

    /// <inheritdoc/>
    public override decimal Evaluate(Context context)
    {
        List<GroupEntry> entries = [.. Items.Select((item, index) => new GroupEntry(Texts[index], item.Evaluate(context)))];
        var (applied, total) = GroupRules.Apply(entries, Modifiers);
        context.Log.Add(new GroupRoll(Text, applied, total));
        return total;
    }
}

/// <summary>
/// A group of one roll made only of dice and numbers added together, like <c>{4d6+3d8}kh1</c> or
/// <c>{3d20+5}&gt;21</c>. As in Roll20, keeping, dropping and success targets then apply to each die on its
/// own, and the numbers are added to each die before it's compared with a success target.
/// </summary>
public sealed record GroupOfDice(IReadOnlyList<Node<Dice>> Dice, IReadOnlyList<int> Signs, decimal Offset, GroupModifiers Modifiers, string Text)
    : Node<decimal>
{
    /// <inheritdoc/>
    public override IReadOnlyList<INode> Children => [.. Dice];

    /// <inheritdoc/>
    public override bool Rolls => true;

    /// <inheritdoc/>
    public override decimal Evaluate(Context context)
    {
        var entries = new List<GroupEntry>();
        for (int i = 0; i < Dice.Count; i++)
        {
            DiceRoll roll = context.Roll(Dice[i].Evaluate(context), Dice[i]);
            entries.AddRange(roll.Kept.Select(die => new GroupEntry(roll.Dice.Display(), Signs[i] * die.Value)));
        }
        var (applied, total) = GroupRules.Apply(entries, Modifiers, successOffset: Offset, totalOffset: Offset);
        context.Log.Add(new GroupRoll(Text, applied, total));
        return total;
    }
}
