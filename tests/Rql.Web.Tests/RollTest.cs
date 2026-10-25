using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Rql.Web.Components.Pages;

namespace Rql.Web.Tests;

public class RollTest : WebTest
{
    public RollTest()
    {
        WriteTable("loot", "Sword;10\nShield;5\nPotion;1\n");
        WriteTable("Gear/WeaponTypes", "Bow\n");
    }

    private static void Type(IRenderedComponent<Roll> page, string query) => page.Find(".query-input").Input(query);

    [Fact]
    public void AQueryIsRolledAndKeptInTheHistory()
    {
        var page = Render<Roll>();

        Type(page, "Pick(loot, 1)");
        page.Find("form").Submit();

        Assert.Contains("Sword", page.Find(".history-entry").TextContent);
        Assert.Equal("Pick(loot, 1)", page.Find(".history-query code").TextContent);
    }

    [Fact]
    public void MistakesShowAsTheQueryIsTyped()
    {
        var page = Render<Roll>();

        Type(page, "Roll(nope)");

        Assert.Contains("no table named", page.Find(".query-error").TextContent);
        Assert.Equal("     ^", page.Find(".caret").TextContent);
        Assert.True(page.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Empty(page.FindAll(".history-entry"));
    }

    [Fact]
    public void NumbersAreShownLarge()
    {
        var page = Render<Roll>();

        Type(page, "Sum(loot)");
        page.Find("form").Submit();

        Assert.Equal("16", page.Find(".result-number").TextContent);
    }

    [Fact]
    public void ClickingATableAddsItToTheQuery()
    {
        var page = Render<Roll>();

        Type(page, "Roll(");
        page.FindAll(".chip").Single(chip => chip.TextContent == "WeaponTypes").Click();

        Assert.Equal("Roll(WeaponTypes", page.Find(".query-input").GetAttribute("value"));
    }

    [Fact]
    public void AQueryInTheAddressIsRolledOnArrival()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/?q=Pick(loot%2C%203)");

        var page = Render<Roll>();

        Assert.Contains("Potion", page.Find(".history-entry").TextContent);
    }

    [Fact]
    public void HistoryOutlivesThePage()
    {
        var first = Render<Roll>();
        Type(first, "Pick(loot, 2)");
        first.Find("form").Submit();
        first.Dispose();

        var second = Render<Roll>();

        Assert.Contains("Shield", second.Find(".history-entry").TextContent);
    }

    [Fact]
    public void SeveralRollsAreShownEachUnderItsLabel()
    {
        var page = Render<Roll>();

        Type(page, "Attack: 1d1 + 5; Damage: 2d1 + 3");
        page.Find("form").Submit();

        Assert.Equal(["Attack", "Damage"], page.FindAll(".outcome-label").Select(label => label.TextContent));
        Assert.Equal(["6", "5"], page.FindAll(".result-number").Select(number => number.TextContent));
        Assert.Equal(["d1", "2d1"], page.FindAll(".dice-notation").Select(dice => dice.TextContent));
    }

    [Fact]
    public void DiceAreShownWithTheDroppedOnesMarked()
    {
        var page = Render<Roll>();

        Type(page, "4d1kh3");
        page.Find("form").Submit();

        Assert.Equal(4, page.FindAll(".die").Count);
        Assert.Single(page.FindAll(".die.dropped"));
        Assert.Equal("= 3", page.Find(".dice-total").TextContent);
    }

    [Fact]
    public void DiceNotationIsAddedAfterAnOperator()
    {
        var page = Render<Roll>();

        Type(page, "d20 +");
        page.FindAll(".dice-help button").Single(button => button.TextContent == "4d6kh3").Click();

        Assert.Equal("d20 + 4d6kh3", page.Find(".query-input").GetAttribute("value"));
    }
}
