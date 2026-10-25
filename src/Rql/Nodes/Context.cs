using Rql.Tables;

namespace Rql.Nodes;

/// <summary>What a node needs while it runs: the tables in scope, and where random numbers come from.</summary>
public sealed record Context
{
    /// <param name="tables">Tables by their full names, which can be a <see cref="TableCatalog"/> already.</param>
    public Context(IReadOnlyDictionary<string, Table> tables, Random random)
    {
        Tables = tables as TableCatalog ?? new TableCatalog(tables);
        Random = random;
    }

    public TableCatalog Tables { get; }

    public Random Random { get; }

    /// <summary>Every roll of dice made with this context, in order, for showing how a result came about.</summary>
    public RollLog Log { get; init; } = new();

    /// <summary>Rolls dice and writes the roll in the log, blaming <paramref name="blame"/> if it goes wrong.</summary>
    public DiceRoll Roll(Dice dice, INode blame)
    {
        try
        {
            DiceRoll roll = dice.Roll(Random);
            Log.Add(roll);
            return roll;
        }
        catch (DiceRollException e)
        {
            throw new EvalException(blame, e.Message);
        }
    }

    /// <summary>
    /// A context for evaluating a part of a tree that nothing random can reach, so its result is the same
    /// every time and can be worked out before the query is run. Asking for a random number here is a
    /// mistake in whatever decided the subtree was settled, so it fails loudly rather than quietly rolling
    /// early.
    /// </summary>
    public static Context Settled(IReadOnlyDictionary<string, Table> tables) => new(tables, new SettledRandom());

    private sealed class SettledRandom : Random
    {
        private static InvalidOperationException Rolled() => new("a subtree treated as settled tried to roll");

        public override int Next() => throw Rolled();
        public override int Next(int maxValue) => throw Rolled();
        public override int Next(int minValue, int maxValue) => throw Rolled();
        public override long NextInt64() => throw Rolled();
        public override long NextInt64(long maxValue) => throw Rolled();
        public override long NextInt64(long minValue, long maxValue) => throw Rolled();
        public override double NextDouble() => throw Rolled();
        public override float NextSingle() => throw Rolled();
        public override void NextBytes(byte[] buffer) => throw Rolled();
        public override void NextBytes(Span<byte> buffer) => throw Rolled();
        protected override double Sample() => throw Rolled();
    }
}

/// <summary>
/// What went into working out a query, for showing how it came about: the dice rolled, the groups kept or
/// counted, and the labelled terms, each in the order they happened.
/// </summary>
public sealed class RollLog
{
    private readonly List<DiceRoll> rolls = [];
    private readonly List<GroupRoll> groups = [];
    private readonly List<LabelledValue> labels = [];

    public IReadOnlyList<DiceRoll> Rolls => rolls;

    public IReadOnlyList<GroupRoll> Groups => groups;

    public IReadOnlyList<LabelledValue> Labels => labels;

    public void Add(DiceRoll roll) => rolls.Add(roll);

    public void Add(GroupRoll group) => groups.Add(group);

    public void Add(LabelledValue label) => labels.Add(label);
}

/// <summary>A labelled term and what it came to, like <c>STR: 5</c> for <c>5[STR]</c>.</summary>
public sealed record LabelledValue(string Label, string Value);
