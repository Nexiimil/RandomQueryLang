using Rql.Evaluation;
using Rql.Nodes;
using Rql.Syntax;
using Rql.Tables;

namespace Rql.Tests.Tables;

public class TableCatalogTest
{
    private static Table Named(string name) => new(name, [new Thing(name, 1, 1)]);

    private readonly TableCatalog catalog = new(new[]
    {
        "Flaws", "Monsters.Undead", "Spells.Undead", "Monsters.Beasts.Wolves", "Loot.Wolves.Pelts",
    }.ToDictionary(name => name, Named));

    [Theory]
    [InlineData("Flaws")]
    [InlineData("Monsters.Undead")]
    [InlineData("Monsters.Beasts.Wolves")]
    public void AFullNameAlwaysFindsItsTable(string name)
    {
        Assert.Equal(name, catalog.Find(name, out _)?.Name);
    }

    [Theory]
    [InlineData("Wolves", "Monsters.Beasts.Wolves")]
    [InlineData("Beasts.Wolves", "Monsters.Beasts.Wolves")]
    [InlineData("Pelts", "Loot.Wolves.Pelts")]
    public void ATableInAFolderCanBeCalledByTheEndOfItsName(string name, string found)
    {
        Assert.Equal(found, catalog.Find(name, out _)?.Name);
    }

    [Fact]
    public void ANameSharedByTwoTablesSaysWhichTheyAre()
    {
        Assert.Null(catalog.Find("Undead", out string? problem));
        Assert.Contains("\"Monsters.Undead\", \"Spells.Undead\"", problem);
    }

    [Theory]
    [InlineData("Beasts")]
    [InlineData("easts.Wolves")]
    [InlineData("Nope")]
    public void OnlyWholeFolderAndTableNamesMatch(string name)
    {
        Assert.Null(catalog.Find(name, out string? problem));
        Assert.Contains("no table named", problem);
    }

    [Theory]
    [InlineData("Flaws", "Flaws")]
    [InlineData("Monsters.Beasts.Wolves", "Wolves")]
    [InlineData("Monsters.Undead", "Monsters.Undead")]
    public void TheShortestNameIsAsMuchAsItTakesToTellATableApart(string full, string shortest)
    {
        Assert.Equal(shortest, catalog.ShortestName(full));
    }

    [Fact]
    public void ATableAtTheTopIsntShadowedByOneInAFolder()
    {
        var tables = new TableCatalog(new[] { "Undead", "Monsters.Undead" }.ToDictionary(name => name, Named));

        Assert.Equal("Undead", tables.Find("Undead", out _)?.Name);
        Assert.Equal("Monsters.Undead", tables.ShortestName("Monsters.Undead"));
    }

    [Fact]
    public void QueriesFindNestedTables()
    {
        var interpreter = new Interpreter(new Context(catalog, new Random(0)));

        Assert.Equal("Monsters.Beasts.Wolves", interpreter.Run("Roll(Wolves)").As<Table>().Name);
        Assert.Equal("Spells.Undead", interpreter.Run("Roll(Spells.Undead)").As<Table>().Name);
        Assert.Equal(5, Assert.Throws<QueryException>(() => interpreter.Run("Roll(Undead)")).Position);
    }

    [Fact]
    public void ReferencesAreTheShortestNameAndNeedNoQuotes()
    {
        Assert.Equal("Wolves", QueryText.TableReference(catalog, "Monsters.Beasts.Wolves"));
        Assert.Equal("Monsters.Undead", QueryText.TableReference(catalog, "Monsters.Undead"));
    }

    [Fact]
    public void TextCanStillNameANestedTable()
    {
        var interpreter = new Interpreter(new Context(catalog, new Random(0)));

        Assert.Equal("Spells.Undead", interpreter.Run("Roll(\"Spells.Undead\")").As<Table>().Name);
    }

    [Theory]
    [InlineData("Weapon Types")]
    [InlineData("Monsters.Weapon Types")]
    [InlineData("d6")]
    [InlineData("4thLevel")]
    public void ATableCantBeGivenANameAQueryCouldntWrite(string name)
    {
        var error = Assert.Throws<ArgumentException>(() => new TableCatalog(new[] { name }.ToDictionary(n => n, Named)));

        Assert.Contains("no spaces or dots", error.Message);
    }

    [Fact]
    public void TextWithForbiddenCharactersNamesNoTable()
    {
        var interpreter = new Interpreter(new Context(catalog, new Random(0)));

        QueryException error = Assert.Throws<QueryException>(() => interpreter.Run("Roll(\"Weapon Types\")"));

        Assert.Equal(5, error.Position);
        Assert.Contains("isn't a table name", error.Message);
    }
}
