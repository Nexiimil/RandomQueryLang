using Rql.Projects;
using Rql.Web.Components.Pages;

namespace Rql.Web.Tests;

public class Roll20Test : WebTest
{
    public Roll20Test()
    {
        WriteTable("loot", "Sword;10\nShield;5\nPotion;1\n");
    }

    private static void Roll(IRenderedComponent<Roll> page, string query)
    {
        page.Find(".query-input").Input(query);
        page.Find("form.query-form").Submit();
    }

    [Fact]
    public void ARollQueryAsksBeforeRolling()
    {
        var page = Render<Roll>();

        Roll(page, "1d1 + ?{Bonus|2}");
        Assert.Empty(page.FindAll(".history-entry"));
        Assert.Equal("2", page.Find(".prompt input").GetAttribute("value"));

        page.Find(".prompt input").Input("5");
        page.Find(".prompt-form").Submit();

        Assert.Equal("6", page.Find(".result-number").TextContent);
        Assert.Empty(page.FindAll(".prompt-form"));
    }

    [Fact]
    public void OptionsArePickedFromAList()
    {
        var page = Render<Roll>();

        Roll(page, "?{Weapon|Sword,10|Axe,20}");
        page.Find(".prompt select").Change("20");
        page.Find(".prompt-form").Submit();

        Assert.Equal("20", page.Find(".result-number").TextContent);
    }

    [Fact]
    public void CharacterSheetValuesAreAskedForAndRemembered()
    {
        var page = Render<Roll>();

        Roll(page, "@{strength} * 2");
        Assert.Contains("sheet", page.Find(".prompt").TextContent);
        page.Find(".prompt input").Input("4");
        page.Find(".prompt-form").Submit();
        Assert.Equal("8", page.Find(".result-number").TextContent);

        Roll(page, "@{strength} + 1");
        Assert.Equal("4", page.Find(".prompt input").GetAttribute("value"));
    }

    [Fact]
    public void AnAnswerThatAsksAnotherQuestionAsksItBeforeRolling()
    {
        var page = Render<Roll>();

        Roll(page, "?{Roll|Plain,1|Bonus,1+?{Bonus&#124;2&#125;}");
        page.Find(".prompt select").Change("1+?{Bonus|2}");
        page.Find(".prompt-form").Submit();

        Assert.Equal(2, page.FindAll(".prompt").Count);
        page.Find(".prompt-form").Submit();
        Assert.Equal("3", page.Find(".result-number").TextContent);
    }

    [Fact]
    public void SavedQueriesAreMacros()
    {
        Project.Queries.Save(new SavedQuery("Best", "Pick(loot, 1)"));
        var page = Render<Roll>();

        Roll(page, "Sum(#Best) + 1");

        Assert.Equal("11", page.Find(".result-number").TextContent);
    }

    [Fact]
    public void GroupsLabelsAndInlineRollsAreShown()
    {
        var page = Render<Roll>();

        Roll(page, "{1d1, 2d1}kh1 + [[1d1]] + 5[STR]");

        Assert.Equal("8", page.Find(".result-number").TextContent);
        Assert.Equal("{1d1, 2d1}kh1", page.Find(".group-entry").ParentElement!.ParentElement!.QuerySelector(".dice-notation")!.TextContent);
        Assert.Single(page.FindAll(".group-entry.dropped"));
        Assert.Equal("[[1d1]]", page.Find(".inline-roll .dice-notation").TextContent);
        Assert.Contains("STR", page.Find(".term-labels").TextContent);
    }

    [Fact]
    public void DecimalsAreShownAsRoll20ShowsThem()
    {
        var page = Render<Roll>();

        Roll(page, "7/2");

        Assert.Equal("3.5", page.Find(".result-number").TextContent);
    }
}
