using Rql.Web.Components.Pages;
using TablesPage = Rql.Web.Components.Pages.Tables;

namespace Rql.Web.Tests;

public class ProjectsTest : WebTest
{
    [Fact]
    public void TheLastProjectOpenedIsOpenToStartWith()
    {
        var page = Render<ProjectPicker>();

        Assert.Equal("project", page.Find(".current-project h2").TextContent);
    }

    [Fact]
    public void AFolderCanBeOpenedAsAProject()
    {
        string other = Path.Combine(Root, "other");
        System.IO.Directory.CreateDirectory(Path.Combine(other, "Monsters"));
        File.WriteAllText(Path.Combine(other, "Monsters", "Undead.RQLtable"), "Zombie\n");
        var page = Render<ProjectPicker>();

        page.Find(".browser-path input").Change(other);
        page.Find(".browser-path").Submit();
        Assert.Contains("1 table in this folder", page.Find(".browser-footer").TextContent);
        page.FindAll("button").Single(button => button.TextContent == "Open as project").Click();

        Assert.Equal(other, Recent.All()[0]);
        Assert.EndsWith("/tables", Navigation.Uri);
        var tables = Render<TablesPage>();
        Assert.Contains("Undead", tables.Find(".listing").TextContent);
        Assert.Contains("Monsters", tables.Find(".group-header h2").TextContent);
    }

    [Fact]
    public void ANewFolderCanBeMadeAndOpened()
    {
        var page = Render<ProjectPicker>();
        page.Find(".browser-path input").Change(Root);
        page.Find(".browser-path").Submit();

        page.Find(".browser-new input").Input("Campaign");
        page.Find(".browser-new").Submit();
        page.FindAll("button").Single(button => button.TextContent == "Open as project").Click();

        Assert.Equal(Path.Combine(Root, "Campaign"), Recent.All()[0]);
    }

    [Fact]
    public void RecentProjectsCanBeReopenedAndForgotten()
    {
        string other = Path.Combine(Root, "other");
        System.IO.Directory.CreateDirectory(other);
        Recent.Touch(other);
        var page = Render<ProjectPicker>();

        page.FindAll(".recent-open").Single(button => button.TextContent.Contains("project")).Click();
        Assert.Equal(Directory, Recent.All()[0]);

        Render<ProjectPicker>().FindAll("button[title^='Take off']")[1].Click();
        Assert.Equal([Directory], Recent.All());
    }

    [Fact]
    public void WithNoProjectThePagesAskForOne()
    {
        Recent.Forget(Directory);

        Assert.Contains("No project open", Render<Roll>().Markup);
        Assert.Contains("No project open", Render<TablesPage>().Markup);
    }

    [Fact]
    public void TablesInFoldersAreGroupedAndRolledByTheirShortestName()
    {
        WriteTable("Monsters/Beasts/Wolves", "Grey\n");

        var page = Render<TablesPage>();

        Assert.Contains("Monsters.Beasts", page.Find(".group-header h2").TextContent);
        Assert.Equal("?q=Roll(Wolves)", Uri.UnescapeDataString(page.FindAll("a.button").First(link => link.TextContent == "Roll").GetAttribute("href")!));
    }
}
