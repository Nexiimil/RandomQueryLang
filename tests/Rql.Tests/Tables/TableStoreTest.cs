using Rql.Tables;

namespace Rql.Tests.Tables;

public sealed class TableStoreTest : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("rql-").FullName;
    private readonly TableStore store;

    public TableStoreTest()
    {
        store = new TableStore(directory);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void LoadsTableFilesNamedAfterTheFile()
    {
        File.WriteAllText(Path.Combine(directory, "loot.RQLtable"), "\uFEFFSword;10\r\nShield;5\r\n");
        File.WriteAllText(Path.Combine(directory, "notes.txt"), "not a table");

        Assert.Equal(["loot"], store.Names());

        Table table = store.Read("loot").Resolve();
        Assert.Equal("loot", table.Name);
        Assert.Equal([new Thing("Sword", 10, 0.5), new Thing("Shield", 5, 0.5)], table.Things);
    }

    [Fact]
    public void OneBadTableDoesNotStopTheRestLoading()
    {
        File.WriteAllText(Path.Combine(directory, "good.RQLtable"), "A\nB\n");
        File.WriteAllText(Path.Combine(directory, "bad.RQLtable"), "A;;0.9\nB;;0.9\n");

        LoadedTables loaded = store.LoadAll();

        Assert.Equal(["good"], loaded.Tables.Keys);
        Assert.Equal("bad", Assert.Single(loaded.Problems).Name);
    }

    [Fact]
    public void SavedTablesReadBack()
    {
        var table = new TableDefinition("WeaponTypes", [new TableRow("Sword", 3), new TableRow("Bow", Chance: 0.25)]);

        store.Save(table);

        Assert.Equal(table.Rows, store.Read("WeaponTypes").Rows);
        Assert.Equal("Sword;3\nBow;;0.25\n", File.ReadAllText(Path.Combine(directory, "WeaponTypes.RQLtable")));
    }

    [Fact]
    public void SavingUnderANewNameRenames()
    {
        store.Save(new TableDefinition("old", [new TableRow("A")]));

        store.Save(new TableDefinition("new", [new TableRow("A")]), previousName: "old");

        Assert.Equal(["new"], store.Names());
    }

    [Fact]
    public void RenamingRefusesToOverwriteAnotherTable()
    {
        store.Save(new TableDefinition("first", [new TableRow("A")]));
        store.Save(new TableDefinition("second", [new TableRow("B")]));

        Assert.Throws<IOException>(() => store.Save(new TableDefinition("second", [new TableRow("A")]), "first"));
        Assert.Equal(["first", "second"], store.Names());
    }

    [Fact]
    public void DeletedTablesAreGone()
    {
        store.Save(new TableDefinition("loot", [new TableRow("A")]));

        store.Delete("loot");

        Assert.Empty(store.Names());
    }

    [Fact]
    public void TablesInFoldersAreFoundAndNamedByTheirPath()
    {
        Directory.CreateDirectory(Path.Combine(directory, "Monsters", "Beasts"));
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        File.WriteAllText(Path.Combine(directory, "Flaws.RQLtable"), "A\n");
        File.WriteAllText(Path.Combine(directory, "Monsters", "Beasts", "Wolves.RQLtable"), "Grey\n");
        File.WriteAllText(Path.Combine(directory, ".git", "Hidden.RQLtable"), "A\n");

        Assert.Equal(["Flaws", "Monsters.Beasts.Wolves"], store.Names());
        Assert.Equal(["Monsters", "Monsters.Beasts"], store.Folders());
        Assert.Equal("Monsters.Beasts.Wolves", store.LoadAll().Tables["Monsters.Beasts.Wolves"].Name);
    }

    [Fact]
    public void ATableOrFolderWhoseNameAQueryCantWriteIsReportedRatherThanMisread()
    {
        Directory.CreateDirectory(Path.Combine(directory, "v1.2"));
        Directory.CreateDirectory(Path.Combine(directory, "My Stuff"));
        File.WriteAllText(Path.Combine(directory, "Weapon.Types.RQLtable"), "Sword\n");
        File.WriteAllText(Path.Combine(directory, "Weapon Types.RQLtable"), "Sword\n");
        File.WriteAllText(Path.Combine(directory, "v1.2", "Loot.RQLtable"), "Gold\n");
        File.WriteAllText(Path.Combine(directory, "My Stuff", "Loot.RQLtable"), "Gold\n");
        File.WriteAllText(Path.Combine(directory, "Flaws.RQLtable"), "Greedy\n");

        LoadedTables loaded = store.LoadAll();

        Assert.Equal(["Flaws"], loaded.Tables.Keys);
        Assert.Equal(["My Stuff/Loot.RQLtable", "Weapon Types.RQLtable", "Weapon.Types.RQLtable", "v1.2/Loot.RQLtable"],
            loaded.Problems.Select(problem => problem.Name).Order(StringComparer.Ordinal));
        Assert.All(loaded.Problems, problem => Assert.Contains("no spaces or dots", problem.Message));
        Assert.Empty(store.Folders());
    }

    [Theory]
    [InlineData("Undead")]
    [InlineData("_private")]
    [InlineData("Level4")]
    [InlineData("Monsters.Beasts.Wolves")]
    [InlineData("Dragons")]
    [InlineData("d6x")]
    [InlineData("Größe")]
    public void NamesAQueryCanWriteAreAllowed(string name)
    {
        Assert.True(TableStore.IsValidName(name));
    }

    [Fact]
    public void SavingIntoAFolderMakesIt()
    {
        store.Save(new TableDefinition("Monsters.Undead", [new TableRow("Zombie")]));

        Assert.True(File.Exists(Path.Combine(directory, "Monsters", "Undead.RQLtable")));
        Assert.Equal(["Monsters.Undead"], store.Names());
    }

    [Fact]
    public void MovingATableOutOfAFolderTidiesTheEmptyFolderAway()
    {
        store.Save(new TableDefinition("Monsters.Deep.Undead", [new TableRow("Zombie")]));

        store.Save(new TableDefinition("Undead", [new TableRow("Zombie")]), previousName: "Monsters.Deep.Undead");

        Assert.Equal(["Undead"], store.Names());
        Assert.False(Directory.Exists(Path.Combine(directory, "Monsters")));
        Assert.True(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" padded ")]
    [InlineData("../escape")]
    [InlineData("Weapon Types")]
    [InlineData("Weapon-Types")]
    [InlineData("Dragon's")]
    [InlineData("4thLevel")]
    [InlineData("d6")]
    [InlineData("d6s")]
    [InlineData("dF")]
    [InlineData("Dice.D20")]
    [InlineData("a..b")]
    [InlineData("a.")]
    [InlineData(".a")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("folder. padded")]
    public void NamesThatArentFileNamesAreRefused(string name)
    {
        Assert.False(TableStore.IsValidName(name));
        Assert.Throws<ArgumentException>(() => store.Save(new TableDefinition(name, [new TableRow("A")])));
    }

    [Fact]
    public void AMissingDirectoryHasNoTables()
    {
        Assert.Empty(new TableStore(Path.Combine(directory, "missing")).Names());
    }
}
