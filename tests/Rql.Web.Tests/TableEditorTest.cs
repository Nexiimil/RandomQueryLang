using AngleSharp.Dom;
using Rql.Web.Components.Pages;

namespace Rql.Web.Tests;

public class TableEditorTest : WebTest
{
    private IRenderedComponent<TableEditor> Edit(string? name = null) =>
        Render<TableEditor>(parameters => parameters.Add(editor => editor.Name, name));

    private static IElement SaveButton(IRenderedComponent<TableEditor> editor) =>
        editor.FindAll("button").Single(button => button.TextContent == "Save");

    private static IReadOnlyList<IElement> RowInputs(IRenderedComponent<TableEditor> editor, string label) =>
        editor.FindAll($"input[aria-label^='{label} of row']");

    [Fact]
    public void ANewTableIsSavedToAFile()
    {
        var editor = Edit();

        editor.Find(".name-input").Input("WeaponTypes");
        RowInputs(editor, "Name")[0].Input("Sword");
        RowInputs(editor, "Value")[0].Change("10");
        editor.FindAll("button").Single(button => button.TextContent == "+ Add row").Click();
        RowInputs(editor, "Name")[1].Input("Potion");
        RowInputs(editor, "Chance")[1].Change("0.25");
        SaveButton(editor).Click();

        Assert.Equal("Sword;10\nPotion;;0.25\n", ReadTable("WeaponTypes"));
        Assert.EndsWith("tables/edit/WeaponTypes", Navigation.Uri);
        Assert.Contains("Saved", editor.Find(".badge").TextContent);
    }

    [Fact]
    public void SharesAreWorkedOutAsRowsAreEdited()
    {
        WriteTable("loot", "Sword\nShield\n");
        var editor = Edit("loot");

        Assert.Equal(["50%", "50%"], editor.FindAll(".share-text").Select(share => share.TextContent));

        RowInputs(editor, "Chance")[0].Change("0.8");

        Assert.Equal(["80%", "20%"], editor.FindAll(".share-text").Select(share => share.TextContent));
        Assert.False(SaveButton(editor).HasAttribute("disabled"));
    }

    [Fact]
    public void ATableThatCantBeRolledCantBeSaved()
    {
        WriteTable("loot", "Sword\nShield\n");
        var editor = Edit("loot");

        RowInputs(editor, "Chance")[0].Change("0.6");
        RowInputs(editor, "Chance")[1].Change("0.6");

        Assert.Contains("more than 1", editor.Find(".banner.error").TextContent);
        Assert.True(SaveButton(editor).HasAttribute("disabled"));
    }

    [Fact]
    public void ABadRowIsPointedOut()
    {
        WriteTable("loot", "Sword\nShield\n");
        var editor = Edit("loot");

        RowInputs(editor, "Name")[1].Input("Bow;Arrow");

        Assert.Contains("Row 2", editor.Find(".banner.error").TextContent);
        Assert.Single(editor.FindAll("tr.bad"));
    }

    [Fact]
    public void BlankRowsAreLeftOut()
    {
        WriteTable("loot", "Sword\n");
        var editor = Edit("loot");

        editor.FindAll("button").Single(button => button.TextContent == "+ Add row").Click();

        Assert.DoesNotContain(editor.FindAll(".badge"), badge => badge.TextContent.Contains("Unsaved"));
    }

    [Fact]
    public void TextModeEditsTheSameTable()
    {
        WriteTable("loot", "Sword;10\n");
        var editor = Edit("loot");

        editor.FindAll("button[role=tab]").Single(tab => tab.TextContent == "Text").Click();
        Assert.Equal("Sword;10", editor.Find("textarea").GetAttribute("value"));

        editor.Find("textarea").Input("Sword;10\nShield;;0.25\n\nRope");
        editor.FindAll("button[role=tab]").Single(tab => tab.TextContent == "Rows").Click();

        Assert.Equal(["Sword", "Shield", "Rope"], RowInputs(editor, "Name").Select(input => input.GetAttribute("value")));
        SaveButton(editor).Click();
        Assert.Equal("Sword;10\nShield;;0.25\nRope\n", ReadTable("loot"));
    }

    [Fact]
    public void AFileThatCantBeReadOpensAsTextToBeFixed()
    {
        WriteTable("loot", "Sword;heavy\n");
        var editor = Edit("loot");

        Assert.Equal("Sword;heavy\n", editor.Find("textarea").GetAttribute("value"));
        Assert.Contains("Line 1", editor.Find(".banner.error").TextContent);
        Assert.True(editor.FindAll("button[role=tab]").Single(tab => tab.TextContent == "Rows").HasAttribute("disabled"));

        editor.Find("textarea").Input("Sword;12\n");
        SaveButton(editor).Click();

        Assert.Equal("Sword;12\n", ReadTable("loot"));
    }

    [Fact]
    public void RenamingMovesTheFile()
    {
        WriteTable("old", "A\n");
        var editor = Edit("old");

        editor.Find(".name-input").Input("new");
        SaveButton(editor).Click();

        Assert.Equal(["new"], Store.Names());
        Assert.EndsWith("tables/edit/new", Navigation.Uri);
    }

    [Fact]
    public void ANameAlreadyTakenIsRefused()
    {
        WriteTable("first", "A\n");
        WriteTable("second", "B\n");
        var editor = Edit("first");

        editor.Find(".name-input").Input("second");

        Assert.Contains("already a table named second", editor.Find(".field-error").TextContent);
        Assert.True(SaveButton(editor).HasAttribute("disabled"));
    }

    [Fact]
    public void RowsCanBeReordered()
    {
        WriteTable("loot", "A\nB\n");
        var editor = Edit("loot");

        editor.FindAll("button[title='Move down']")[0].Click();
        SaveButton(editor).Click();

        Assert.Equal("B\nA\n", ReadTable("loot"));
    }

    [Fact]
    public void DeletingNeedsConfirming()
    {
        WriteTable("loot", "A\n");
        var editor = Edit("loot");

        editor.FindAll("button").Single(button => button.TextContent == "Delete table…").Click();
        Assert.Equal(["loot"], Store.Names());

        editor.FindAll("button").Single(button => button.TextContent == "Delete").Click();
        Assert.Empty(Store.Names());
        Assert.EndsWith("/tables", Navigation.Uri);
    }

    [Fact]
    public void LeavingWithUnsavedChangesAsksFirst()
    {
        WriteTable("loot", "A\n");
        var editor = Edit("loot");
        string here = Navigation.Uri;

        RowInputs(editor, "Name")[0].Input("B");
        Navigation.NavigateTo("tables");

        Assert.Equal(here, Navigation.Uri);
        editor.FindAll("button").Single(button => button.TextContent == "Leave without saving").Click();
        Assert.EndsWith("/tables", Navigation.Uri);
        Assert.Equal("A\n", ReadTable("loot"));
    }

    [Fact]
    public void TryingItRollsTheUnsavedTable()
    {
        var editor = Edit();

        RowInputs(editor, "Name")[0].Input("Only");
        editor.FindAll("button").Single(button => button.TextContent == "Roll 10 times").Click();

        Assert.Equal(10, editor.FindAll(".try .chip").Count);
        Assert.Empty(Store.Names());
    }

    [Fact]
    public void AMissingTableSaysSo()
    {
        Assert.Contains("No such table", Edit("nope").Markup);
    }
}

public class TableEditorFolderTest : WebTest
{
    private IRenderedComponent<TableEditor> Edit(string? name = null) =>
        Render<TableEditor>(parameters => parameters.Add(editor => editor.Name, name));

    private static AngleSharp.Dom.IElement SaveButton(IRenderedComponent<TableEditor> editor) =>
        editor.FindAll("button").Single(button => button.TextContent == "Save");

    [Fact]
    public void ATableCanBeSavedIntoAFolder()
    {
        var editor = Edit();

        // Slashes typed out of habit become dots.
        editor.Find(".folder-input").Input("Monsters/Undead");
        Assert.Equal("Monsters.Undead", editor.Find(".folder-input").GetAttribute("value"));
        editor.Find(".name-input").Input("Zombies");
        editor.Find("input[aria-label='Name of row 1']").Input("Shambler");
        SaveButton(editor).Click();

        Assert.Equal("Shambler\n", ReadTable("Monsters/Undead/Zombies"));
        Assert.EndsWith("tables/edit/Monsters.Undead.Zombies", Navigation.Uri);
    }

    [Fact]
    public void ANestedTableOpensWithItsFolder()
    {
        WriteTable("Monsters/Undead", "Zombie\n");

        var editor = Edit("Monsters.Undead");

        Assert.Equal("Monsters", editor.Find(".folder-input").GetAttribute("value"));
        Assert.Equal("Undead", editor.Find(".name-input").GetAttribute("value"));
        Assert.Equal("Undead", editor.Find("p.hint code").TextContent);
    }

    [Fact]
    public void ChangingTheFolderMovesTheTable()
    {
        WriteTable("Monsters/Undead", "Zombie\n");
        var editor = Edit("Monsters.Undead");

        editor.Find(".folder-input").Input("");
        SaveButton(editor).Click();

        Assert.Equal(["Undead"], Store.Names());
        Assert.False(System.IO.Directory.Exists(Path.Combine(Directory, "Monsters")));
    }

    [Fact]
    public void ANameSharedWithAnotherFolderIsWrittenWithItsFolder()
    {
        WriteTable("Spells/Undead", "Raise Dead\n");
        var editor = Edit();

        editor.Find(".folder-input").Input("Monsters");
        editor.Find(".name-input").Input("Undead");

        Assert.Equal("Monsters.Undead", editor.Find("p.hint code").TextContent);
    }

    [Fact]
    public void ANewTableCanStartInAFolder()
    {
        Navigation.NavigateTo("tables/new?folder=Monsters");

        var editor = Edit();

        Assert.Equal("Monsters", editor.Find(".folder-input").GetAttribute("value"));
    }

    [Fact]
    public void ASpaceInTheNameIsRefusedWithSuggestions()
    {
        var editor = Edit();

        editor.Find(".name-input").Input("weapon types");
        editor.Find("input[aria-label='Name of row 1']").Input("Sword");

        Assert.Contains("try weapon_types or WeaponTypes", editor.Find("p.field-error").TextContent);
        Assert.True(SaveButton(editor).HasAttribute("disabled"));
    }

    [Fact]
    public void ADotInTheNameIsSentToTheFolderBox()
    {
        var editor = Edit();

        editor.Find(".name-input").Input("Monsters.Undead");

        Assert.Contains("Folder box", editor.Find("p.field-error").TextContent);
    }
}
