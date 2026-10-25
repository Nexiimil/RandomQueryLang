using Rql.Nodes;
using Rql.Syntax;

namespace Rql.Tests.Syntax;

public class ParserTest
{
    [Fact]
    public void ParsesNestedCalls()
    {
        Expr expected = new Call("Sum", [
            new Call("DropLowest", [
                new Call("Roll", [new Identifier("loot", 20), new NumberLiteral(4, 26)], 15)], 4)], 0);

        Assert.Equal(expected, Parser.Parse("Sum(DropLowest(Roll(loot, 4)))"));
    }

    [Fact]
    public void ParsesDice()
    {
        Assert.Equal(new DiceLiteral(new Dice(1, 20), 0), Parser.Parse("d20"));
        Assert.Equal(new DiceLiteral(new Dice(4, 6), 0), Parser.Parse("4D6"));
        Assert.Equal(new Call("Roll", [new DiceLiteral(new Dice(3, 8), 5)], 0), Parser.Parse("Roll(3d8)"));
    }

    [Fact]
    public void WordsThatOnlyStartLikeDiceAreTableNames()
    {
        Assert.Equal(new Identifier("dragons", 0), Parser.Parse("dragons"));
        Assert.Equal(new Identifier("d6x", 0), Parser.Parse("d6x"));
    }

    [Fact]
    public void ATableInAFolderIsOneNameWithDots()
    {
        Assert.Equal(new Identifier("Monsters.Beasts.Wolves", 0), Parser.Parse("Monsters.Beasts.Wolves"));
        Assert.Equal(new Call("Roll", [new Identifier("Monsters.Undead", 5), new NumberLiteral(2, 22)], 0),
            Parser.Parse("Roll(Monsters.Undead, 2)"));
    }

    [Fact]
    public void ParsesTextWithEscapes()
    {
        Assert.Equal(new TextLiteral("My \"Big\" Table\\", 0), Parser.Parse("\"My \\\"Big\\\" Table\\\\\""));
    }

    [Fact]
    public void ParsesNegativeNumbersAndCallsWithoutArguments()
    {
        Assert.Equal(new NumberLiteral(-3, 0), Parser.Parse("-3"));
        Assert.Equal(new Call("Roll", [], 0), Parser.Parse("Roll()"));
    }

    [Theory]
    [InlineData("Roll(d6, 4", 10)]
    [InlineData("Roll(d6,, 4)", 8)]
    [InlineData("Roll(d6) extra", 9)]
    [InlineData("4dragons", 1)]
    [InlineData("Roll(\"abc", 5)]
    [InlineData("Roll(d6 # 2)", 8)]
    [InlineData("Roll(\"\\n\")", 6)]
    [InlineData("99999999999999999999999999999999", 0)]
    [InlineData("Roll(d0)", 5)]
    [InlineData("d99999999999", 1)]
    [InlineData("Monsters.", 8)]
    [InlineData("Monsters..Undead", 8)]
    [InlineData("Monsters.4", 8)]
    public void ReportsWhereTheQueryIsBroken(string query, int position)
    {
        var error = Assert.Throws<QueryException>(() => Parser.Parse(query));

        Assert.True(position == error.Position, error.Message);
    }

    [Fact]
    public void ArithmeticFollowsTheUsualOrder()
    {
        // 1 + (2 * 3) - 4
        Expr expected = new Binary("-",
            new Binary("+", new NumberLiteral(1, 0), new Binary("*", new NumberLiteral(2, 4), new NumberLiteral(3, 8), 6), 2),
            new NumberLiteral(4, 12), 10);

        Assert.Equal(expected, Parser.Parse("1 + 2 * 3 - 4"));
    }

    [Fact]
    public void BracketsGroup()
    {
        Assert.Equal(new Binary("*", new Binary("+", new NumberLiteral(1, 1), new NumberLiteral(2, 3), 2), new NumberLiteral(3, 6), 5),
            Parser.Parse("(1+2)*3"));
    }

    [Fact]
    public void MinusIsANumberWhenWrittenOntoOneAndNegationOtherwise()
    {
        Assert.Equal(new Binary("-", new NumberLiteral(5, 0), new NumberLiteral(3, 2), 1), Parser.Parse("5-3"));
        Assert.Equal(new NumberLiteral(int.MinValue, 0), Parser.Parse("-2147483648"));
        Assert.Equal(new Negate(new DiceLiteral(new Dice(2, 6), 1), 0), Parser.Parse("-2d6"));
    }

    [Fact]
    public void DiceRunUpToAnOperator()
    {
        Assert.Equal(new Binary("+", new DiceLiteral(new Dice(4, 6) { KeepDrop = new KeepDrop(true, true, 3) }, 0), new NumberLiteral(2, 7), 6),
            Parser.Parse("4d6kh3+2"));
    }

    [Fact]
    public void AQueryCanHaveSeveralLabelledRolls()
    {
        IReadOnlyList<Statement> statements = Parser.ParseQuery("Attack: d20 + 5; \"Sneak Attack\": 2d6;d4;");

        Assert.Equal(["Attack", "Sneak Attack", null], statements.Select(statement => statement.Label));
        Assert.Equal(new DiceLiteral(new Dice(1, 4), 37), statements[2].Expr);
    }

    [Theory]
    [InlineData("d20;;d6", 4)]
    [InlineData("1 + ", 4)]
    [InlineData("(1 + 2", 6)]
    [InlineData("d20 Attack: 2", 4)]
    [InlineData("4d6kq", 4)]
    [InlineData("d6r", 3)]
    public void ReportsWhereAQueryOfRollsIsBroken(string query, int position)
    {
        var error = Assert.Throws<QueryException>(() => Parser.ParseQuery(query));

        Assert.True(position == error.Position, error.Message);
    }

    [Fact]
    public void AnEmptyQueryIsAnError()
    {
        Assert.Throws<QueryException>(() => Parser.Parse(""));
    }
}
