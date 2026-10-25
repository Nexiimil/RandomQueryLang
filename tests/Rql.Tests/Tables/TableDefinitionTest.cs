using Rql.Tables;

namespace Rql.Tests.Tables;

public class TableDefinitionTest
{
    private static Table Parse(string name, params string[] lines) => TableDefinition.Parse(name, lines).Resolve();

    [Fact]
    public void ReadsNameValueAndChance()
    {
        Table table = Parse("loot", "Sword;10;0.25", "Shield;5;0.75");

        Assert.Equal("loot", table.Name);
        Assert.Equal([new Thing("Sword", 10, 0.25), new Thing("Shield", 5, 0.75)], table.Things);
    }

    [Fact]
    public void ValueDefaultsToPositionFromTheTopStartingAtOne()
    {
        Assert.Equal([1, 2, 3], Values(Parse("monsters", "Goblin", "Orc", "Troll")));
    }

    [Fact]
    public void ValueDefaultsToTheNameWhenTheNameIsAWholeNumber()
    {
        Assert.Equal([4, 7, 3], Values(Parse("cards", "4", "7", "Joker")));
    }

    [Fact]
    public void RowsWithoutAChanceShareWhatIsLeftOver()
    {
        Assert.Equal([0.5, 0.25, 0.25], Chances(Parse("loot", "Sword;;0.5", "Shield", "Potion")));
    }

    [Fact]
    public void RowsShareChanceEquallyWhenNoneIsGiven()
    {
        Assert.Equal([0.25, 0.25, 0.25, 0.25], Chances(Parse("letters", "A", "B", "C", "D")));
    }

    [Fact]
    public void SkipsBlankLinesAndTrimsFields()
    {
        Table table = Parse("loot", " Sword ; 3 ; 0.5 ", "", "   ", "Shield");

        Assert.Equal([new Thing("Sword", 3, 0.5), new Thing("Shield", 2, 0.5)], table.Things);
    }

    [Fact]
    public void ReportsTheLineOfABadRow()
    {
        var error = Assert.Throws<TableFormatException>(() => Parse("loot", "Sword;1", "", "Shield;heavy"));

        Assert.Contains("line 3", error.Message);
        Assert.Equal(2, error.Row);
    }

    [Theory]
    [InlineData("Sword;1;0.5;extra")]
    [InlineData(";1")]
    [InlineData("Sword;1.5")]
    [InlineData("Sword;1;half")]
    [InlineData("Sword;1;1.5")]
    [InlineData("Sword;1;-0.1")]
    public void RejectsMalformedRows(string line)
    {
        Assert.Throws<TableFormatException>(() => Parse("loot", line));
    }

    [Fact]
    public void RejectsChancesThatAddUpToMoreThanOne()
    {
        Assert.Throws<TableFormatException>(() => Parse("loot", "Sword;;0.6", "Shield;;0.6"));
    }

    [Fact]
    public void RejectsRowsThatCouldNeverBeRolled()
    {
        Assert.Throws<TableFormatException>(() => Parse("loot", "Sword;;0.5", "Shield;;0.5", "Potion"));
    }

    [Fact]
    public void RowsBuiltInAnEditorAreCheckedByRow()
    {
        var table = new TableDefinition("loot", [new TableRow("Sword"), new TableRow("Bow;Arrow")]);

        var error = Assert.Throws<TableFormatException>(table.Resolve);

        Assert.Equal(2, error.Row);
        Assert.Null(error.Line);
        Assert.Contains("row 2", error.Message);
    }

    [Fact]
    public void WritingLeavesOutWhatTheRowLeftOut()
    {
        var table = new TableDefinition("loot", [
            new TableRow("Sword", 10, 0.1),
            new TableRow("Shield", 5),
            new TableRow("Potion", Chance: 0.5),
            new TableRow("Rope"),
        ]);

        Assert.Equal(["Sword;10;0.1", "Shield;5", "Potion;;0.5", "Rope"], table.ToLines());
    }

    [Fact]
    public void WrittenTablesReadBackTheSame()
    {
        var table = new TableDefinition("loot", [
            new TableRow("Sword", -3, 0.125),
            new TableRow("Potion", Chance: 1.0 / 3),
            new TableRow("12"),
        ]);

        TableDefinition read = TableDefinition.Parse("loot", string.Join("\n", table.ToLines()));

        Assert.Equal(table.Rows, read.Rows);
    }

    private static List<int> Values(Table table) => [.. table.Things.Select(thing => thing.Value)];

    private static List<double> Chances(Table table) => [.. table.Things.Select(thing => thing.Chance)];
}
