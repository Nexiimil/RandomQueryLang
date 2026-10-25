using Microsoft.AspNetCore.Components.Forms;
using Rql.Web.Components.Pages;

namespace Rql.Web.Tests;

public class TableImporterTest : WebTest
{
    private static void Upload(IRenderedComponent<TableImporter> page, params (string Name, string Text)[] files) =>
        page.FindComponent<InputFile>().UploadFiles([.. files.Select(file => InputFileContent.CreateFromText(file.Text, file.Name))]);

    private static AngleSharp.Dom.IElement ImportButton(IRenderedComponent<TableImporter> page) =>
        page.FindAll("button").Single(button => button.TextContent.Trim().StartsWith("Import"));

    [Fact]
    public void ACsvFileIsPreviewedThenImported()
    {
        var page = Render<TableImporter>();

        Upload(page, ("Weapon Types.csv", "name,value,weight\nSword,10,3\nBow,6,1\n"));

        Assert.Equal("Weapon_Types", page.Find(".candidate-name").GetAttribute("value"));
        Assert.Equal(["75%", "25%"], page.FindAll(".preview-table tbody tr td:last-child").Select(cell => cell.TextContent));
        ImportButton(page).Click();

        Assert.Equal(["Weapon_Types"], Store.Names());
        Assert.Contains("Imported Weapon_Types", page.Find(".banner.ok").TextContent);
    }

    [Fact]
    public void AJsonFileCanHoldSeveralTablesAndAFolderCanBeChosen()
    {
        var page = Render<TableImporter>();

        Upload(page, ("bestiary.json", "{\"tables\": [{\"name\": \"Undead\", \"rows\": [\"Zombie\"]}, {\"name\": \"Beasts\", \"rows\": [\"Wolf\"]}]}"));
        page.Find(".folder-input").Input("Monsters");
        ImportButton(page).Click();

        Assert.Equal(["Monsters.Beasts", "Monsters.Undead"], Store.Names());
    }

    [Fact]
    public void PastedTextCanBeImported()
    {
        var page = Render<TableImporter>();

        page.Find(".paste").Input("[\"Greedy\", \"Reckless\"]");
        page.Find(".paste-actions input").Input("Flaws");
        page.FindAll("button").Single(button => button.TextContent == "Read").Click();
        ImportButton(page).Click();

        Assert.Equal("Greedy\nReckless\n", ReadTable("Flaws"));
    }

    [Fact]
    public void ATableThatExistsIsOnlyReplacedWhenAskedTo()
    {
        WriteTable("loot", "Old\n");
        var page = Render<TableImporter>();

        Upload(page, ("loot.csv", "New\n"));
        Assert.True(ImportButton(page).HasAttribute("disabled"));

        page.Find(".replace input").Change(true);
        ImportButton(page).Click();

        Assert.Equal("New\n", ReadTable("loot"));
    }

    [Fact]
    public void AFileThatCantBeReadSaysWhy()
    {
        var page = Render<TableImporter>();

        Upload(page, ("loot.csv", "name,value\nSword,heavy\n"));

        Assert.Contains("line 2", page.Find(".candidate .problem").TextContent);
        Assert.True(ImportButton(page).HasAttribute("disabled"));
    }

    [Fact]
    public void RenamingFixesANameThatsTaken()
    {
        WriteTable("loot", "Old\n");
        var page = Render<TableImporter>();

        Upload(page, ("loot.csv", "New\n"));
        page.Find(".candidate-name").Input("loot2");
        ImportButton(page).Click();

        Assert.Equal(["loot", "loot2"], Store.Names());
        Assert.Equal("Old\n", ReadTable("loot"));
    }
}
