using Rql.Nodes;
using Rql.Tables;

namespace Rql.Tests.Nodes;

// Text in a query today is always written in quotes, so a query's own text is settled before anything
// rolls. These cover text converted while running, for when functions start producing text of their own.
public class CoerceTest
{
    private readonly Table loot = new("loot", [new Thing("Sword", 10, 1)]);
    private readonly Context context;

    public CoerceTest()
    {
        context = new Context(new Dictionary<string, Table> { ["loot"] = loot }, new Random(0));
    }

    [Fact]
    public void AValueAlreadyOfTheRightTypeIsLeftAlone()
    {
        Node<decimal> already = Literal.Of(4m);

        Assert.Same(already, Coerce.To<decimal>(already));
    }

    [Fact]
    public void TextIsReadAsANumberWhenItIsOne()
    {
        Assert.Equal(42, Coerce.To<decimal>(Literal.Of(" 42 ")).Evaluate(context));
    }

    [Fact]
    public void TextThatIsNotANumberBlamesTheTextItself()
    {
        var text = Literal.Of("Sword");

        var error = Assert.Throws<EvalException>(() => Coerce.To<decimal>(text).Evaluate(context));

        Assert.Same(text, error.Blame);
    }

    [Fact]
    public void ATableWithOneRowCanBeUsedAsText()
    {
        Assert.Equal("Sword", Coerce.To<string>(Literal.Of(loot)).Evaluate(context));
    }

    [Fact]
    public void ATableWithSeveralRowsSuggestsSum()
    {
        var several = new Table("several", [new Thing("A", 1, 0.5), new Thing("B", 2, 0.5)]);

        var error = Assert.Throws<EvalException>(() => Coerce.To<decimal>(Literal.Of(several)).Evaluate(context));

        Assert.Contains("Sum", error.Message);
    }

    [Fact]
    public void TextCanNameATable()
    {
        Assert.Same(loot, Coerce.To<Table>(Literal.Of("loot")).Evaluate(context));
    }

    [Fact]
    public void TextNamingNoTableIsAnError()
    {
        Assert.Throws<EvalException>(() => Coerce.To<Table>(Literal.Of("nope")).Evaluate(context));
    }

    [Fact]
    public void DiceUsedAsANumberGiveTheirTotal()
    {
        Assert.Equal(2, Coerce.To<decimal>(Literal.Of(new Dice(2, 1))).Evaluate(context));
    }

    [Fact]
    public void DiceUsedAsATableGiveARowPerDie()
    {
        Assert.Equal(3, Coerce.To<Table>(Literal.Of(new Dice(3, 6))).Evaluate(context).Count);
    }

    [Fact]
    public void ConvertingDiceCountsAsRollingSoItIsNeverSettledEarly()
    {
        Assert.True(Coerce.To<Table>(Literal.Of(new Dice(3, 6))).Rolls);
        Assert.True(Coerce.To<decimal>(Literal.Of(new Dice(3, 6))).Rolls);
        Assert.False(Coerce.To<decimal>(Literal.Of("3")).Rolls);
    }

    [Fact]
    public void ANumberIsNeverATable()
    {
        Assert.False(Coerce.Possible(typeof(int), typeof(Table)));
        Assert.True(Coerce.Possible(typeof(string), typeof(Table)));
    }

    [Fact]
    public void NothingBecomesDiceExceptDice()
    {
        Assert.False(Coerce.Possible(typeof(int), typeof(Dice)));
        Assert.True(Coerce.Possible(typeof(Dice), typeof(Dice)));
    }

    [Fact]
    public void ConversionsKeepTheirSourceAsAChild()
    {
        var text = Literal.Of("3");
        Node<decimal> converted = Coerce.To<decimal>(text);

        Assert.IsType<Coerce.TextToNumber>(converted);
        Assert.Same(text, Assert.Single(converted.Children));
    }
}
