using Rql.Projects;

namespace Rql.Tests.Projects;

public sealed class SavedQueryStoreTest : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("rql-").FullName;
    private readonly Project project;

    public SavedQueryStoreTest()
    {
        project = new Project(directory);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void AProjectWithNothingSavedHasNoQueries()
    {
        Assert.Empty(project.Queries.All());
    }

    [Fact]
    public void SavedQueriesReadBackInTheOrderTheyWereSaved()
    {
        project.Queries.Save(new SavedQuery("Stats", "Sum(DropLowest(4d6))"));
        project.Queries.Save(new SavedQuery("Attack", "Roll(d20)"));

        Assert.Equal([new SavedQuery("Stats", "Sum(DropLowest(4d6))"), new SavedQuery("Attack", "Roll(d20)")],
            new Project(directory).Queries.All());
        Assert.True(File.Exists(Path.Combine(directory, Project.QueriesFile)));
    }

    [Fact]
    public void SavingUnderANameThatsTakenReplacesItIgnoringCase()
    {
        project.Queries.Save(new SavedQuery("Stats", "Roll(d6)"));
        project.Queries.Save(new SavedQuery("Attack", "Roll(d20)"));

        project.Queries.Save(new SavedQuery("stats", "Sum(4d6)"));

        Assert.Equal([new SavedQuery("stats", "Sum(4d6)"), new SavedQuery("Attack", "Roll(d20)")], project.Queries.All());
    }

    [Fact]
    public void RenamingKeepsItsPlaceButWontOverwriteAnother()
    {
        project.Queries.Save(new SavedQuery("Stats", "Roll(d6)"));
        project.Queries.Save(new SavedQuery("Attack", "Roll(d20)"));

        project.Queries.Save(new SavedQuery("Abilities", "Roll(d6)"), previousName: "Stats");
        Assert.Equal(["Abilities", "Attack"], project.Queries.All().Select(query => query.Name));

        Assert.Throws<IOException>(() => project.Queries.Save(new SavedQuery("Attack", "Roll(d6)"), "Abilities"));
    }

    [Fact]
    public void DeletedQueriesAreGone()
    {
        project.Queries.Save(new SavedQuery("Stats", "Roll(d6)"));

        project.Queries.Delete("STATS");

        Assert.Empty(project.Queries.All());
    }

    [Fact]
    public void AFileThatIsntSavedQueriesSaysSo()
    {
        File.WriteAllText(Path.Combine(directory, Project.QueriesFile), "not json");

        Assert.Throws<InvalidDataException>(() => project.Queries.All());
    }

    [Fact]
    public void AQueryNeedsAName()
    {
        Assert.Throws<ArgumentException>(() => project.Queries.Save(new SavedQuery("  ", "Roll(d6)")));
    }

    [Fact]
    public void AProjectIsNamedAfterItsFolder()
    {
        Assert.Equal(Path.GetFileName(directory), new Project(directory + Path.DirectorySeparatorChar).Name);
    }
}
