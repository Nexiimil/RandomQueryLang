using Rql.Projects;
using Rql.Web.Components.Pages;

namespace Rql.Web.Tests;

public class SavedQueriesTest : WebTest
{
    public SavedQueriesTest()
    {
        WriteTable("loot", "Sword;10\nShield;5\nPotion;1\n");
    }

    private static void Type(IRenderedComponent<Roll> page, string query) => page.Find(".query-input").Input(query);

    private static void Save(IRenderedComponent<Roll> page, string name)
    {
        page.Find("button.save-query").Click();
        page.Find(".save-name").Input(name);
        page.Find(".save-form").Submit();
    }

    [Fact]
    public void AQueryCanBeSavedToTheProject()
    {
        var page = Render<Roll>();

        Type(page, "Pick(loot, 1)");
        Save(page, "First thing");

        Assert.Equal([new SavedQuery("First thing", "Pick(loot, 1)")], Project.Queries.All());
        Assert.Equal("First thing", page.Find(".saved-name").TextContent);
    }

    [Fact]
    public void ClickingASavedQueryRollsIt()
    {
        Project.Queries.Save(new SavedQuery("Best", "Pick(loot, 1)"));
        var page = Render<Roll>();

        page.Find(".saved-run").Click();

        Assert.Contains("Sword", page.Find(".history-entry").TextContent);
        Assert.Equal("Best", page.Find(".saved-label").TextContent);
    }

    [Fact]
    public void ThePaletteFiltersAndEnterRollsTheFirstMatch()
    {
        Project.Queries.Save(new SavedQuery("Best", "Pick(loot, 1)"));
        Project.Queries.Save(new SavedQuery("Worst", "Pick(loot, 3)"));
        var page = Render<Roll>();

        page.Find(".palette-filter").Input("wor");
        Assert.Equal(["Worst"], page.FindAll(".saved-name").Select(name => name.TextContent));

        page.Find(".palette-filter").KeyDown("Enter");
        Assert.Contains("Potion", page.Find(".history-entry").TextContent);
    }

    [Fact]
    public void EditingASavedQueryAndSavingUpdatesIt()
    {
        Project.Queries.Save(new SavedQuery("Best", "Pick(loot, 1)"));
        var page = Render<Roll>();

        page.Find("button[title='Put in the query box to change it']").Click();
        Assert.Equal("Pick(loot, 1)", page.Find(".query-input").GetAttribute("value"));

        Type(page, "Pick(loot, 2)");
        page.Find("button.save-query").Click();
        Assert.Equal("Best", page.Find(".save-name").GetAttribute("value"));
        Assert.Contains("Replaces the saved query", page.Find(".save-form").TextContent);
        page.Find(".save-form").Submit();

        Assert.Equal([new SavedQuery("Best", "Pick(loot, 2)")], Project.Queries.All());
    }

    [Fact]
    public void DeletingASavedQueryNeedsConfirming()
    {
        Project.Queries.Save(new SavedQuery("Best", "Pick(loot, 1)"));
        var page = Render<Roll>();

        page.Find("button[title='Delete']").Click();
        Assert.Single(Project.Queries.All());

        page.FindAll("button").Single(button => button.TextContent == "Delete").Click();
        Assert.Empty(Project.Queries.All());
    }

    [Fact]
    public void ASavedQueryThatNoLongerWorksIsMarked()
    {
        Project.Queries.Save(new SavedQuery("Gone", "Roll(Vanished)"));

        var page = Render<Roll>();

        Assert.Contains("broken", page.Find(".saved").ClassList);
        Assert.Contains("no table named", page.Find(".saved-run").GetAttribute("title"));
    }
}
