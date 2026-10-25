using Rql.Evaluation;
using Rql.Nodes;
using Rql.Tables;

namespace Rql.Tests.Evaluation;

public class ExpansionTest
{
    private static readonly TableCatalog Tables = new(new Dictionary<string, Table>
    {
        ["loot"] = TableDefinition.Parse("loot", ["Sword;10", "Shield;5"]).Resolve(),
    });

    private static Interpreter Interpreter(params int[] faces) =>
        new(new Context(Tables, new Faces(faces))) { Macros = new Dictionary<string, string> { ["Broken"] = "2 + nope", ["Loop"] = "#Loop" } };

    private static QueryException Error(string query, IReadOnlyDictionary<string, string>? answers = null) =>
        Assert.Throws<QueryException>(() => Interpreter(1, 1, 1).Run(query, answers));

    [Fact]
    public void AMistakeAfterAnInlineRollIsPointedOutWhereItWasWritten()
    {
        // [[1d4]] becomes a single digit, so without keeping track the caret would land 6 places early.
        Assert.Equal(14, Error("[[1d4]] + 2 + nope").Position);
    }

    [Fact]
    public void AMistakeInsideAnInlineRollIsPointedOutInsideIt() => Assert.Equal(7, Error("1 + [[ nope ]]").Position);

    [Fact]
    public void AMistakeInAnAnswerIsBlamedOnTheQuestion()
    {
        var answers = new Dictionary<string, string> { ["Bonus"] = "nope" };
        Assert.Equal(4, Error("1 + ?{Bonus|2}", answers).Position);
    }

    [Fact]
    public void AMistakeInAMacroIsBlamedOnTheMacro() => Assert.Equal(4, Error("1 + #Broken").Position);

    [Fact]
    public void MacrosThatNeverEndAreStopped() => Assert.Contains("without end", Error("#Loop").Message);

    [Fact]
    public void AnUnknownMacroSaysSo()
    {
        QueryException error = Error("1 + #Nope");
        Assert.Equal(4, error.Position);
        Assert.Contains("no saved query named \"Nope\"", error.Message);
    }

    [Fact]
    public void QuotedTextIsLeftAlone() =>
        Assert.Contains("\"#Broken ?{x} [[1]]\" isn't a table name", Error("Roll(\"#Broken ?{x} [[1]]\")").Message);

    [Theory]
    [InlineData("[[1d4", 0)]
    [InlineData("1d4]]", 3)]
    [InlineData("?{Bonus", 0)]
    [InlineData("@{strength", 0)]
    public void UnclosedThingsSayWhereTheyStart(string query, int position) => Assert.Equal(position, Error(query).Position);

    [Fact]
    public void EveryMissingAnswerIsAskedForAtOnce()
    {
        var error = Assert.Throws<PromptsNeededException>(() => Interpreter().Run("@{a} + @{b} + ?{c|1}"));
        Assert.Equal(["@{a}", "@{b}"], error.Prompts.Select(prompt => prompt.Key));
    }

    [Fact]
    public void CheckingAsYouTypeTakesDefaultsAndRollsNothing()
    {
        Interpreter().Validate("[[1d20]]d6 + @{strength} + ?{Bonus|2}");
        Assert.Throws<QueryException>(() => Interpreter().Validate("[[1d20]] + nope"));
    }

    [Fact]
    public void AnAnswerCanChangeWhatsAsked()
    {
        const string query = "?{Roll|Plain,1d20|Bonus,1d20+?{Bonus&#124;2&#125;}";
        Assert.Equal(["Roll"], Interpreter().Prompts(query).Select(prompt => prompt.Key));
        Assert.Equal(["Roll", "Bonus"], Interpreter().Prompts(query, new Dictionary<string, string> { ["Roll"] = "1d20+?{Bonus|2}" }).Select(prompt => prompt.Key));
    }

    [Fact]
    public void InlineRollsGoWithTheRollTheyreIn()
    {
        QueryResult result = Interpreter(2, 3).Run("A: [[1d4]]; B: [[1d6]] + 1");

        Assert.Equal(["1d4"], result.Results[0].Inline.Select(roll => roll.Text));
        Assert.Equal(["1d6"], result.Results[1].Inline.Select(roll => roll.Text));
        Assert.Equal(4m, result.Results[1].As<decimal>());
    }

    [Fact]
    public void AnInlineRollHasToComeToANumber()
    {
        Assert.Contains("use Sum", Error("[[Roll(loot, 2)]]").Message);
        Assert.Contains("expected a number but got the text \"abc\"", Error("[[\"abc\"]]").Message);
    }
}
