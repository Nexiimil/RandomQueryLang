using Rql.Tables;

namespace Rql.Tests.Tables;

public class TableImportTest
{
    private static TableDefinition Csv(string text) => Assert.Single(TableImport.Import(text, "loot.csv", ImportFormat.Csv));

    private static IReadOnlyList<TableDefinition> Json(string text) => TableImport.Import(text, "loot.json", ImportFormat.Json);

    private static List<double> Chances(TableDefinition table) => [.. table.Resolve().Things.Select(thing => thing.Chance)];

    // ---- CSV ----

    [Fact]
    public void CsvWithoutAHeaderIsNameValueChance()
    {
        TableDefinition table = Csv("Sword,10,0.1\nShield,5\nPotion,,0.5\nRope\n");

        Assert.Equal("loot", table.Name);
        Assert.Equal([new TableRow("Sword", 10, 0.1), new TableRow("Shield", 5), new TableRow("Potion", null, 0.5), new TableRow("Rope")], table.Rows);
    }

    [Fact]
    public void CsvWithAHeaderFindsColumnsByNameAndIgnoresTheRest()
    {
        TableDefinition table = Csv("Notes,Chance,Item,Value\nshiny,0.25,Sword,10\n,,Shield,\n");

        Assert.Equal([new TableRow("Sword", 10, 0.25), new TableRow("Shield")], table.Rows);
    }

    [Fact]
    public void CsvFieldsCanBeQuoted()
    {
        TableDefinition table = Csv("name,value\n\"Sword, rusty\",3\n\"The \"\"Big\"\" One\",4\n\"Two\nlines\",5\n");

        // A line break in a cell becomes a space, since a row's name is one line.
        Assert.Equal(["Sword, rusty", "The \"Big\" One", "Two lines"], table.Rows.Select(row => row.Name));
    }

    [Theory]
    [InlineData("name;value\nSword;3\nShield;4\n")]
    [InlineData("name\tvalue\nSword\t3\nShield\t4\n")]
    [InlineData("﻿name,value\r\nSword,3\r\n\r\nShield,4\r\n")]
    public void CsvCanUseSemicolonsOrTabsAndWindowsLineEndings(string text)
    {
        Assert.Equal([new TableRow("Sword", 3), new TableRow("Shield", 4)], Assert.Single(TableImport.Import(text, "loot")).Rows);
    }

    [Fact]
    public void ChancesCanBePercentages() => Assert.Equal([0.25, 0.75], Chances(Csv("name,chance\nSword,25%\nShield,75%\n")));

    [Fact]
    public void WeightsBecomeChancesAndAMissingWeightCountsAsOne()
    {
        Assert.Equal([0.5, 0.25, 0.25], Chances(Csv("name,weight\nSword,2\nShield,1\nPotion,\n")));
    }

    [Fact]
    public void ASingleColumnIsJustNames() => Assert.Equal(["Greedy", "Reckless"], Csv("Greedy\nReckless\n").Rows.Select(row => row.Name));

    [Theory]
    [InlineData("name,value\n\nShield,4\nSword,heavy\n", 4, "not a whole number")]
    [InlineData("name,value\nSword,1.5\n", 2, "not a whole number")]
    [InlineData("name,chance\nSword,half\n", 2, "not a number")]
    [InlineData("name,weight\nSword,-1\n", 2, "0 or more")]
    [InlineData("name,value\n,3\n", 2, "no name")]
    [InlineData("name,value\nSword;Bow,3\n", 2, "can't contain ';'")]
    public void CsvMistakesSayWhichLine(string text, int line, string problem)
    {
        var error = Assert.Throws<TableFormatException>(() => Csv(text));

        Assert.Equal(line, error.Line);
        Assert.Contains(problem, error.Message);
    }

    [Fact]
    public void ChancesAndWeightsCantBeMixed() =>
        Assert.Contains("both chances and weights", Assert.Throws<TableFormatException>(() => Csv("name,chance,weight\nA,0.5,\nB,,2\n")).Message);

    [Fact]
    public void AHeaderWithoutANameColumnIsRefused() =>
        Assert.Contains("no name column", Assert.Throws<TableFormatException>(() => Csv("value,chance\n3,0.5\n")).Message);

    [Fact]
    public void AnUnclosedQuoteIsRefused() =>
        Assert.Contains("closing quote", Assert.Throws<TableFormatException>(() => Csv("name\n\"Sword\n")).Message);

    // ---- JSON ----

    [Fact]
    public void JsonCanBeAListOfNames() =>
        Assert.Equal(["Sword", "Shield"], Assert.Single(Json("[\"Sword\", \"Shield\"]")).Rows.Select(row => row.Name));

    [Fact]
    public void JsonCanBeAListOfRows()
    {
        TableDefinition table = Assert.Single(Json("[{\"name\": \"Sword\", \"value\": 10, \"chance\": \"25%\"}, {\"Name\": \"Shield\"}]"));

        Assert.Equal([new TableRow("Sword", 10, 0.25), new TableRow("Shield")], table.Rows);
    }

    [Fact]
    public void JsonCanBeANamedTableAsRoll20GivesOne()
    {
        TableDefinition table = Assert.Single(Json("{\"name\": \"Weapon Types\", \"items\": [{\"name\": \"Sword\", \"weight\": 3}, {\"name\": \"Bow\", \"weight\": 1}]}"));

        Assert.Equal("Weapon_Types", table.Name);
        Assert.Equal([0.75, 0.25], Chances(table));
    }

    [Fact]
    public void JsonCanHoldSeveralTables()
    {
        const string text = """
            // Comments and trailing commas are fine.
            {"tables": [
                {"name": "Monsters.Undead", "rows": ["Zombie", "Ghoul"]},
                {"rows": [{"result": "Gold", "value": 100}]},
            ]}
            """;

        IReadOnlyList<TableDefinition> tables = Json(text);

        Assert.Equal(["Monsters.Undead", "loot_2"], tables.Select(table => table.Name));
        Assert.Equal(100, tables[1].Rows[0].Value);
    }

    [Fact]
    public void AListOfNamedTablesIsSeveralTables() =>
        Assert.Equal(["A", "B"], Json("[{\"name\": \"A\", \"rows\": [\"x\"]}, {\"name\": \"B\", \"rows\": [\"y\"]}]").Select(table => table.Name));

    [Theory]
    [InlineData("{\"rows\": [", "this isn't JSON")]
    [InlineData("{\"name\": \"x\"}", "rows, items, entries or results")]
    [InlineData("[true]", "has to be a name")]
    [InlineData("[{\"name\": \"A\", \"value\": \"lots\"}]", "not a whole number")]
    public void JsonMistakesSaySo(string text, string problem) =>
        Assert.Contains(problem, Assert.Throws<TableFormatException>(() => Json(text)).Message);

    // ---- Formats and names ----

    [Theory]
    [InlineData("loot.json", "Sword", ImportFormat.Json)]
    [InlineData("loot.csv", "[x]", ImportFormat.Csv)]
    [InlineData("loot.RQLtable", "Sword;1", ImportFormat.Rql)]
    [InlineData(null, "  [\"Sword\"]", ImportFormat.Json)]
    [InlineData(null, "Sword,1", ImportFormat.Csv)]
    public void TheFormatComesFromTheFileNameOrWhatItStartsWith(string? file, string text, ImportFormat format) =>
        Assert.Equal(format, TableImport.Detect(text, file));

    [Theory]
    [InlineData("Weapon Types.csv", "Weapon_Types")]
    [InlineData("4th-level spells.json", "_4th_level_spells")]
    [InlineData("d6.csv", "d6_")]
    [InlineData("Monsters.Undead.json", "Monsters.Undead")]
    [InlineData("Größe!.csv", "Größe")]
    [InlineData("!!!.csv", "Imported")]
    [InlineData("loot.RQLtable", "loot")]
    public void NamesAreMadeIntoOnesATableCanHave(string raw, string name)
    {
        Assert.Equal(name, TableImport.SuggestName(raw));
        Assert.True(TableCatalog.IsValidName(TableImport.SuggestName(raw)));
    }

    [Fact]
    public void ImportedTablesSaveAndReadBack()
    {
        string directory = Directory.CreateTempSubdirectory("rql-").FullName;
        try
        {
            var store = new TableStore(directory);
            TableDefinition table = Csv("name,weight\nSword,2\nShield,1\n");
            store.Save(table);

            Assert.Equal(table.Resolve().Things.Select(thing => thing.Chance), store.Read("loot").Resolve().Things.Select(thing => thing.Chance));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
