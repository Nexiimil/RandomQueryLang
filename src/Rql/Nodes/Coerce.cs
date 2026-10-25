using System.Globalization;
using Rql.Tables;

namespace Rql.Nodes;

/// <summary>
/// Converting a value to the type a function needs, as nodes in the tree rather than something the
/// evaluator does on the way past. Which conversion is needed is known when the tree is built, so each one
/// is its own node and none of them has to ask what it was given.
/// </summary>
/// <remarks>
/// Text can stand in for a number when it is one, or for a table by naming it. A table with one row can
/// stand in for that row's value or name. Dice are rolled, giving a row per die where a table is needed,
/// or their total where a number is. A number is never a table, and nothing but dice is dice.
/// </remarks>
public static class Coerce
{
    private static readonly Dictionary<(Type From, Type To), Func<INode, INode>> Conversions = new()
    {
        [(typeof(string), typeof(decimal))] = source => new TextToNumber((Node<string>)source),
        [(typeof(Table), typeof(decimal))] = source => new TableToNumber((Node<Table>)source),
        [(typeof(Dice), typeof(decimal))] = source => new DiceToNumber((Node<Dice>)source),
        [(typeof(decimal), typeof(string))] = source => new NumberToText((Node<decimal>)source),
        [(typeof(Table), typeof(string))] = source => new TableToText((Node<Table>)source),
        [(typeof(Dice), typeof(string))] = source => new DiceToText((Node<Dice>)source),
        [(typeof(string), typeof(Table))] = source => new TextToTable((Node<string>)source),
        [(typeof(Dice), typeof(Table))] = source => new DiceToTable((Node<Dice>)source),
    };

    /// <summary>Whether a value of one type can ever stand in for the other.</summary>
    public static bool Possible(Type from, Type to) => from == to || Conversions.ContainsKey((from, to));

    /// <summary>
    /// Wraps <paramref name="source"/> in whichever conversion reaches <paramref name="wanted"/>. The binder
    /// calls <see cref="Possible"/> first, so a pair that can never work is a mistake in the binder rather
    /// than in the query.
    /// </summary>
    public static INode To(Type wanted, INode source)
    {
        if (source.ValueType == wanted)
        {
            return source;
        }
        return Conversions.TryGetValue((source.ValueType, wanted), out var convert)
            ? convert(source)
            : throw new InvalidOperationException(
                $"nothing converts {ValueTypes.Describe(source.ValueType)} to {ValueTypes.Describe(wanted)}");
    }

    public static Node<T> To<T>(INode source) where T : notnull => (Node<T>)To(typeof(T), source);

    /// <summary>Text that spells a number, like <c>Roll(d6, "3")</c>.</summary>
    public sealed record TextToNumber(Node<string> Source) : Node<decimal>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override decimal Evaluate(Context context)
        {
            string text = Source.Evaluate(context);
            return Numbers.TryParse(text, out decimal number)
                ? number
                : throw new EvalException(Source, $"expected a number but got the text \"{text}\"");
        }
    }

    /// <summary>A table of one row, standing in for that row's value.</summary>
    public sealed record TableToNumber(Node<Table> Source) : Node<decimal>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override decimal Evaluate(Context context)
        {
            Table table = Source.Evaluate(context);
            return table.Count == 1
                ? table.Things[0].Value
                : throw new EvalException(Source,
                    $"expected a number but got a table with {table.Count} rows (use Sum to add them up)");
        }
    }

    /// <summary>Dice standing in for a number, rolled and added up, or with a success target, the successes counted.</summary>
    public sealed record DiceToNumber(Node<Dice> Source) : Node<decimal>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override bool Rolls => true;

        public override decimal Evaluate(Context context) => context.Roll(Source.Evaluate(context), Source).Total;
    }

    /// <summary>A number written where text was wanted.</summary>
    public sealed record NumberToText(Node<decimal> Source) : Node<string>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override string Evaluate(Context context) => Numbers.Display(Source.Evaluate(context));
    }

    /// <summary>A table of one row, standing in for that row's name.</summary>
    public sealed record TableToText(Node<Table> Source) : Node<string>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override string Evaluate(Context context)
        {
            Table table = Source.Evaluate(context);
            return table.Count == 1
                ? table.Things[0].Name
                : throw new EvalException(Source, $"expected text but got a table with {table.Count} rows");
        }
    }

    /// <summary>Dice standing in for text, rolled and added up.</summary>
    public sealed record DiceToText(Node<Dice> Source) : Node<string>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override bool Rolls => true;

        public override string Evaluate(Context context) =>
            context.Roll(Source.Evaluate(context), Source).Total.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Text naming a table, which is how a name with spaces is written.</summary>
    public sealed record TextToTable(Node<string> Source) : Node<Table>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override Table Evaluate(Context context)
        {
            string name = Source.Evaluate(context);
            return context.Tables.Find(name, out string? problem) ?? throw new EvalException(Source, problem!);
        }
    }

    /// <summary>Dice used as a table, giving a row per kept die worth the number it landed on.</summary>
    public sealed record DiceToTable(Node<Dice> Source) : Node<Table>
    {
        public override IReadOnlyList<INode> Children => [Source];

        public override bool Rolls => true;

        public override Table Evaluate(Context context) => context.Roll(Source.Evaluate(context), Source).AsTable();
    }
}
