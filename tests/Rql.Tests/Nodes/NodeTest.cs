using Rql.Functions;
using Rql.Nodes;
using Rql.Tables;

namespace Rql.Tests.Nodes;

public class NodeTest
{
    private static readonly Dictionary<string, Table> NoTables = [];
    private readonly Context context = new(NoTables, new Random(1234));

    private static Table Loot() => TableDefinition.Parse("loot", ["Sword;10", "Shield;5", "Potion;1"]).Resolve();

    [Fact]
    public void TreesAreStitchedTogetherByConstructor()
    {
        // Sum(DropLowest(4d6)), built in C# rather than parsed.
        Node<decimal> query = new Sum(
            new DropLowest(
                new RollDice(Literal.Of(new Dice(4, 6)), Literal.Of(1m)),
                Literal.Of(1m)));

        decimal total = query.Evaluate(context);

        Assert.InRange(total, 3m, 18m);
    }

    [Fact]
    public void ChildrenGiveAWalkOverTheWholeTree()
    {
        static int Count(INode node) => 1 + node.Children.Sum(Count);

        Assert.Equal(3, Count(new Pick(Literal.Of(Loot()), Literal.Of(2m))));
    }

    [Fact]
    public void ChildrenAreTheArgumentsInTheOrderTheyAreWritten()
    {
        var table = Literal.Of(Loot());
        var row = Literal.Of(2m);

        Assert.Equal<INode>([table, row], new Pick(table, row).Children);
    }

    [Fact]
    public void ErrorsBlameTheArgumentRatherThanAPosition()
    {
        var row = Literal.Of(99m);
        var query = new Pick(Literal.Of(Loot()), row);

        var error = Assert.Throws<EvalException>(() => query.Evaluate(context));

        Assert.Same(row, error.Blame);
        Assert.Contains("has rows 1 to 3", error.Message);
    }

    [Fact]
    public void SignatureFillsInDefaultsForArgumentsLeftOut()
    {
        var rolled = (Node<Table>)Signature.Of<RollTable>().Build([Literal.Of(Loot())]);

        Assert.Equal(1, rolled.Evaluate(context).Count);
    }

    [Fact]
    public void SignatureRejectsAnArgumentOfTheWrongType()
    {
        Assert.Throws<ArgumentException>(() => Signature.Of<Sum>().Build([Literal.Of(3m)]));
    }

    [Fact]
    public void SignatureIsReadFromTheConstructor()
    {
        Signature roll = Signature.Of<RollTable>();

        Assert.Equal("Roll(table, [count])", roll.Usage);
        Assert.Equal(typeof(Table), roll.Returns);
        Assert.Equal([typeof(Table), typeof(decimal)], roll.Parameters.Select(parameter => parameter.Type));
        Assert.Equal(1, roll.Required);
    }

    [Fact]
    public void RollingDiceSeveralTimesGivesARowPerDie()
    {
        Assert.Equal(8, new RollDice(Literal.Of(new Dice(4, 6)), Literal.Of(2m)).Evaluate(context).Count);
    }

    [Fact]
    public void ASettledContextRefusesToRoll()
    {
        var rolling = new RollTable(Literal.Of(Loot()), Literal.Of(1m));

        Assert.Throws<InvalidOperationException>(() => rolling.Evaluate(Context.Settled(NoTables)));
    }

    [Fact]
    public void ASettledSubtreeCanBeWorkedOutWithoutRolling()
    {
        Assert.Equal(16, new Sum(Literal.Of(Loot())).Evaluate(Context.Settled(NoTables)));
    }

    [Fact]
    public void DroppingKeepsTheRestOfTheTable()
    {
        var ties = new Table("ties", [new Thing("A", 1, 0.25), new Thing("B", 1, 0.25), new Thing("C", 9, 0.5)]);

        Table kept = new DropLowest(Literal.Of(ties), Literal.Of(1m)).Evaluate(context);

        Assert.Equal(2, kept.Count);
        Assert.Contains(kept.Things, thing => thing.Name == "C");
    }
}
