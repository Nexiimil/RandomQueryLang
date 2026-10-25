using Rql.Evaluation;
using Rql.Nodes;
using Rql.Tables;

namespace Rql.Tests.Roll20;

/// <summary>
/// Roll20's dice syntax, case by case, with the dice scripted so every result is exact. Each test has a
/// "Parity" trait saying how sure we are Roll20 agrees:
/// <list type="bullet">
/// <item><b>Documented</b>: Roll20's Dice Reference, wiki or help pages say so.</item>
/// <item><b>Community</b>: Roll20 forum posts show it working this way.</item>
/// <item><b>Recalled</b>: from Roll20's documentation as remembered, not checked against a copy of it.</item>
/// <item><b>Inferred</b>: the documentation leaves it open, and this is the reading that fits it best.</item>
/// <item><b>Deviation</b>: deliberately different from Roll20; docs/roll20-parity.md says why.</item>
/// </list>
/// Run one kind with, for example, <c>dotnet test --filter Parity=Inferred</c>.
/// Sources: Dice Reference https://help.roll20.net/hc/en-us/articles/360037773133,
/// Order of Operations https://wiki.roll20.net/Order_of_Operations, Roll Query https://wiki.roll20.net/Roll_Query.
/// </summary>
public class Roll20ParityTest
{
    private static readonly TableCatalog Tables = new(new Dictionary<string, Table>
    {
        ["Flaws"] = TableDefinition.Parse("Flaws", ["Greedy", "Reckless", "Cowardly"]).Resolve(),
    });

    private static QueryResult Run(string query, int[] faces, IReadOnlyDictionary<string, string>? answers = null,
        IReadOnlyDictionary<string, string>? macros = null, bool fate = false)
    {
        var random = new Faces(faces) { Fate = fate };
        var interpreter = new Interpreter(new Context(Tables, random)) { Macros = macros ?? new Dictionary<string, string>() };
        QueryResult result = interpreter.Run(query, answers);
        Assert.True(random.Used == faces.Length, $"{query} rolled {random.Used} dice, not the {faces.Length} given");
        return result;
    }

    private static decimal Total(string query, params int[] faces) => Run(query, faces).As<decimal>();

    private static IReadOnlyList<Die> Dice(string query, params int[] faces) => Assert.Single(Run(query, faces).Single.Rolls).Faces;

    // ---- Math ----------------------------------------------------------------------------------------

    [Theory]
    [Trait("Parity", "Documented")] // Order of Operations: brackets, functions, **, then * / %, then + -
    [InlineData("2+3*4", 14)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("2*3**2", 18)]
    [InlineData("10%3", 1)]
    [InlineData("2**3", 8)]
    [InlineData("floor(2.5)**2", 4)]
    public void ArithmeticFollowsRoll20sOrderOfOperations(string query, double expected) =>
        Assert.Equal((decimal)expected, Total(query));

    [Theory]
    [Trait("Parity", "Documented")] // Dice Reference, rounding functions
    [InlineData("floor(7/2)", 3)]
    [InlineData("floor(-1.5)", -2)]
    [InlineData("ceil(7/2)", 4)]
    [InlineData("round(4.4)", 4)]
    [InlineData("round(4.5)", 5)]
    [InlineData("round(-2.5)", -2)]
    [InlineData("abs(-3)", 3)]
    public void RoundingFunctionsRoundAsRoll20Does(string query, double expected) =>
        Assert.Equal((decimal)expected, Total(query));

    [Fact]
    [Trait("Parity", "Community")] // forum: ** with 0.5 for a square root
    public void AFractionalPowerIsARoot() => Assert.Equal(2m, Total("4**0.5"));

    [Fact]
    [Trait("Parity", "Recalled")] // Roll20 does its math in decimals
    public void DivisionGivesDecimals()
    {
        Assert.Equal(3.5m, Total("7/2"));
        Assert.Equal(7.5m, Total("1d20/2", 15));
    }

    [Theory]
    [Trait("Parity", "Inferred")] // undocumented; these follow mathematics
    [InlineData("-2**2", -4)]
    [InlineData("2**3**2", 512)]
    [InlineData("2**-1", 0.5)]
    public void PowersBindTighterThanMinusAndGroupFromTheRight(string query, double expected) =>
        Assert.Equal((decimal)expected, Total(query));

    // ---- Basic dice ----------------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")]
    public void DiceAreAddedUp()
    {
        Assert.Equal(14m, Total("1d20", 14));
        Assert.Equal(10m, Total("2d6+3", 2, 5));
        Assert.Equal(14m, Total("d20", 14));
    }

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: Fate dice, 4dF
    public void FateDiceAreMinusOneZeroOrOne() => Assert.Equal(1m, Run("4dF", [-1, 0, 1, 1], fate: true).As<decimal>());

    [Fact]
    [Trait("Parity", "Community")] // forum: 0d1+10 and 0d6 used in groups
    public void ZeroDiceComeToZero()
    {
        Assert.Equal(10m, Total("0d1+10"));
        Assert.Empty(Dice("0d6"));
    }

    // ---- Keep and drop -------------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 8d100d3 drops the 3 lowest, 8d100k3 keeps the 3 highest
    public void KeepingAndDroppingAsTheDiceReferenceDoes()
    {
        int[] rolled = [50, 10, 90, 20, 70, 30, 80, 60];
        Assert.Equal(350m, Total("8d100d3", rolled));
        Assert.Equal(240m, Total("8d100k3", rolled));
        Assert.Equal(3, Dice("8d100d3", rolled).Count(die => die.Dropped));
    }

    [Theory]
    [Trait("Parity", "Documented")] // Dice Reference: kh, kl, dh, dl
    [InlineData("4d6kh3", 14)]
    [InlineData("4d6kl1", 1)]
    [InlineData("4d6dh1", 9)]
    [InlineData("4d6dl1", 14)]
    public void KeepAndDropHighestOrLowest(string query, int expected) => Assert.Equal(expected, Total(query, 3, 6, 1, 5));

    [Fact]
    [Trait("Parity", "Inferred")] // k with no number keeps one
    public void KeepingWithoutANumberKeepsOne() => Assert.Equal(6m, Total("4d6k", 3, 6, 1, 5));

    // ---- Exploding, compounding, penetrating ---------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 3d6! explodes on the highest face, and keeps exploding
    public void ExplodingDiceRollAgainOnTheirHighestFace()
    {
        Assert.Equal(24m, Total("3d6!", 6, 2, 6, 6, 1, 3));
        Assert.Equal(6, Dice("3d6!", 6, 2, 6, 6, 1, 3).Count);
    }

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 3d6!>5 explodes on 5 or more
    public void ExplodingOnAComparison() => Assert.Equal(11m, Total("3d6!>5", 5, 1, 2, 3));

    [Fact]
    [Trait("Parity", "Inferred")] // a bare number after ! explodes on exactly that face
    public void ExplodingOnAnExactFace() => Assert.Equal(10m, Total("3d6!3", 3, 4, 1, 2));

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 5d6!! compounds explosions into one die
    public void CompoundingDiceExplodeIntoThemselves()
    {
        Assert.Equal(27m, Total("5d6!!", 6, 6, 2, 3, 4, 5, 1));
        Assert.Equal(5, Dice("5d6!!", 6, 6, 2, 3, 4, 5, 1).Count);
    }

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 5d6!p takes 1 off each die an explosion adds
    public void PenetratingDiceTakeOneOffEachExplosion() => Assert.Equal(25m, Total("5d6!p", 6, 6, 2, 3, 4, 5, 1));

    // ---- Successes and failures ----------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 3d6>3 counts dice of 3 or more; ties count
    public void ATargetCountsSuccessesIncludingTies() => Assert.Equal(2m, Total("3d6>3", 3, 2, 6));

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 10d6<4 counts dice of 4 or less
    public void ATargetBelowCountsLowDice() => Assert.Equal(8m, Total("10d6<4", 1, 2, 3, 4, 5, 6, 1, 2, 3, 4));

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 3d6>3f1 takes a success away for each 1
    public void FailuresTakeAwaySuccesses() => Assert.Equal(1m, Total("3d6>3f1", 1, 4, 5));

    [Fact]
    [Trait("Parity", "Recalled")] // Dice Reference: 10d6<4f>5
    public void FailuresCanBeAComparisonToo() => Assert.Equal(2m, Total("10d6<4f>5", 1, 2, 3, 4, 5, 6, 6, 5, 4, 1));

    // ---- Rerolls -------------------------------------------------------------------------------------

    [Theory]
    [Trait("Parity", "Documented")] // Dice Reference: r1, r<2, several r, and ro to reroll once
    [InlineData("2d8r1", new[] { 1, 5, 7 }, 12)]
    [InlineData("2d8r<2", new[] { 2, 1, 8, 3 }, 11)]
    [InlineData("2d8r8r2", new[] { 8, 2, 4, 6 }, 10)]
    [InlineData("2d8ro<2", new[] { 1, 1, 5 }, 6)]
    public void RerollsAsTheDiceReferenceDescribes(string query, int[] faces, int expected) =>
        Assert.Equal(expected, Total(query, faces));

    // ---- Critical success and failure ----------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: 1d20cs>10 and 1d20cf<3 only change how dice are shown
    public void CriticalThresholdsOnlyChangeHowDiceAreShown()
    {
        Assert.True(Dice("1d20cs>10", 12)[0].CriticalSuccess);
        Assert.Equal(12m, Total("1d20cs>10", 12));
        Assert.True(Dice("1d20cf<3", 3)[0].CriticalFailure);
    }

    [Fact]
    [Trait("Parity", "Community")] // forum: 1d20cs20cs10; without cs or cf, 20 and 1 are the criticals
    public void SeveralCriticalThresholdsAndTheDefaults()
    {
        Assert.True(Dice("1d20cs20cs10", 10)[0].CriticalSuccess);
        Assert.False(Dice("1d20cs20cs10", 15)[0].CriticalSuccess);
        Assert.True(Dice("1d20", 20)[0].CriticalSuccess);
        Assert.True(Dice("1d20", 1)[0].CriticalFailure);
        Assert.False(Dice("1d20cs10", 20)[0].CriticalSuccess);
    }

    // ---- Matching ------------------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // wiki: 2d6m adds up as usual, picking out matches; 2d6mt counts them
    public void MatchingPicksOutOrCountsMatches()
    {
        Assert.Equal(6m, Total("2d6m", 3, 3));
        Assert.All(Dice("2d6m", 3, 3), die => Assert.Equal(0, die.MatchSet));
        Assert.Equal(1m, Total("2d6mt", 3, 3));
        Assert.Equal(0m, Total("2d6mt", 2, 5));
    }

    [Fact]
    [Trait("Parity", "Documented")] // wiki: 6d6mt3 only counts sets of three or more
    public void MatchingCanNeedBiggerSets() => Assert.Equal(1m, Total("6d6mt3", 2, 2, 2, 5, 5, 1));

    [Fact]
    [Trait("Parity", "Inferred")] // wiki: a match can be limited to a result; the way it's written is a guess
    public void MatchingCanBeLimitedToSomeFaces() => Assert.Equal(1m, Total("6d6mt>4", 2, 2, 5, 5, 1, 3));

    // ---- Sorting -------------------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: s sorts lowest first, sd highest first
    public void SortingOrdersTheDice()
    {
        Assert.Equal([1, 3, 5, 6], Dice("4d6s", 3, 6, 1, 5).Select(die => die.Value));
        Assert.Equal([6, 5, 3, 1], Dice("4d6sd", 3, 6, 1, 5).Select(die => die.Value));
    }

    // ---- Groups --------------------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // Dice Reference: {4d6+2d8, 3d20+3, 5d10+1}>40 counts the rolls coming to 40 or more
    public void AGroupOfSeveralRollsCountsTheirTotals() =>
        Assert.Equal(2m, Total("{4d6+2d8, 3d20+3, 5d10+1}>40", 6, 6, 6, 6, 8, 8, 10, 10, 10, 10, 10, 10, 10, 1));

    [Fact]
    [Trait("Parity", "Community")] // community guide: {3d6,4d8}kh1 keeps the higher total
    public void AGroupKeepsTheHighestTotal() => Assert.Equal(32m, Total("{3d6,4d8}kh1", 1, 2, 3, 8, 8, 8, 8));

    [Fact]
    [Trait("Parity", "Community")] // the advantage roll
    public void AdvantageKeepsTheBetterD20() => Assert.Equal(17m, Total("{d20, d20}kh1", 4, 17));

    [Fact]
    [Trait("Parity", "Community")] // forum: {1d20+3,0d1+10}kl1, keeping the lower of the roll and 10
    public void AGroupCanCapARoll()
    {
        Assert.Equal(10m, Total("{1d20+3,0d1+10}kl1", 15));
        Assert.Equal(5m, Total("{1d20+3,0d1+10}kl1", 2));
    }

    [Theory]
    [Trait("Parity", "Documented")] // wiki: in a group of one roll, numbers are added to each die before it's compared
    [InlineData("{7d20+5}>15", new[] { 10, 11, 9, 20, 1, 15, 5 }, 4)]
    [InlineData("{3d6+1}<3", new[] { 1, 2, 3 }, 2)]
    public void AGroupOfOneRollComparesEachDie(string query, int[] faces, int expected) =>
        Assert.Equal(expected, Total(query, faces));

    [Fact]
    [Trait("Parity", "Community")] // forum: {2d6}>8 compares each d6 with 8, so always 0
    public void AGroupOfOneRollNeverComparesItsTotal() => Assert.Equal(0m, Total("{2d6}>8", 6, 6));

    [Fact]
    [Trait("Parity", "Community")] // forum: {3d6,0d6}kh1<12
    public void AGroupKeepsBeforeCounting() => Assert.Equal(0m, Total("{3d6,0d6}kh1<12", 6, 6, 6));

    [Fact]
    [Trait("Parity", "Inferred")] // a group of one roll keeps single dice, not the total
    public void AGroupOfOneRollKeepsSingleDice() => Assert.Equal(8m, Total("{4d6+3d8}kh1", 1, 2, 3, 4, 8, 5, 6));

    // ---- Inline rolls --------------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // wiki: [[2d6]]d5 rolls 2d6, then that many d5
    public void AnInlineRollCanBeTheNumberOfDice() => Assert.Equal(12m, Total("[[2d6]]d5", 1, 2, 3, 4, 5));

    [Fact]
    [Trait("Parity", "Documented")] // wiki: [[ 3d6>[[1+3]] ]], nested inline rolls
    public void InlineRollsNest() => Assert.Equal(2m, Total("[[ 3d6>[[1+3]] ]]", 4, 1, 6));

    [Fact]
    [Trait("Parity", "Documented")] // Order of Operations: inline rolls are rolled before anything else
    public void InlineRollsAreRolledFirst()
    {
        QueryResult result = Run("1d20+[[1d4]]", [3, 15]);

        Assert.Equal(18m, result.As<decimal>());
        InlineRoll inline = Assert.Single(result.Single.Inline);
        Assert.Equal(("1d4", 3m), (inline.Text, inline.Value));
    }

    // ---- Labels and rollable tables ------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Recalled")] // Dice Reference: inline labels, 1d20+5[Strength]
    public void LabelsNameTermsWithoutChangingThem()
    {
        Result result = Run("1d20+5[STR]+2[Prof]", [10]).Single;

        Assert.Equal(17m, result.As<decimal>());
        Assert.Equal([new LabelledValue("STR", "5"), new LabelledValue("Prof", "2")], result.Labels);
        Assert.Equal("Fire", Assert.Single(Run("2d6[Fire]", [1, 2]).Single.Rolls).Dice.Label);
    }

    [Fact]
    [Trait("Parity", "Recalled")] // Dice Reference: rollable tables, 1t[table]
    public void TablesAreRolledWithT() => Assert.Equal(2, Run("2t[Flaws]", []).As<Table>().Count);

    // ---- Roll queries --------------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Documented")] // Roll Query wiki: ?{Number of dice|1}d6, taking the default
    public void ARollQueryCanHaveADefault()
    {
        Assert.Equal(4m, Total("?{Number of dice|1}d6", 4));
        Assert.Equal(6m, Run("?{Number of dice|1}d6", [1, 2, 3], new Dictionary<string, string> { ["Number of dice"] = "3" }).As<decimal>());
    }

    [Fact]
    [Trait("Parity", "Documented")] // Roll Query wiki: the same question is only asked once
    public void TheSameQuestionIsAskedOnce()
    {
        var interpreter = new Interpreter(new Context(Tables, new Faces()));
        Assert.Single(interpreter.Prompts("?{Bonus|0} + ?{Bonus|0}"));
        Assert.Equal(4m, interpreter.Run("?{Bonus|0} + ?{Bonus|0}", new Dictionary<string, string> { ["Bonus"] = "2" }).As<decimal>());
    }

    [Fact]
    [Trait("Parity", "Documented")] // Roll Query wiki: options after pipes, label,value
    public void ARollQueryCanOfferOptions()
    {
        var interpreter = new Interpreter(new Context(Tables, new Faces()));
        RollPrompt prompt = Assert.Single(interpreter.Prompts("1d20 + ?{Weapon|Sword,5|Axe,7}"));

        Assert.Equal([new PromptOption("Sword", "5"), new PromptOption("Axe", "7")], prompt.Options);
        Assert.Equal(15m, Run("1d20 + ?{Weapon|Sword,5|Axe,7}", [10]).As<decimal>());
        Assert.Equal(17m, Run("1d20 + ?{Weapon|Sword,5|Axe,7}", [10], new Dictionary<string, string> { ["Weapon"] = "7" }).As<decimal>());
    }

    [Fact]
    [Trait("Parity", "Community")] // forum: HTML entities for commas, pipes and braces inside options
    public void OptionsCanHoldEscapedCommasPipesAndBraces()
    {
        // The first } ends a roll query, so the group's own } has to be escaped too.
        const string query = "?{Attack|Normal,1d20|Advantage,{1d20&#44;1d20&#125;kh1}";
        Assert.Equal(17m, Run(query, [4, 17], new Dictionary<string, string> { ["Attack"] = "{1d20,1d20}kh1" }).As<decimal>());

        const string nested = "?{Roll|Plain,1d20|Bonus,1d20+?{Bonus&#124;2&#125;}";
        Assert.Equal(12m, Run(nested, [10], new Dictionary<string, string> { ["Roll"] = "1d20+?{Bonus|2}" }).As<decimal>());
    }

    // ---- Commands, macros and character sheets -------------------------------------------------------

    [Theory]
    [Trait("Parity", "Documented")]
    [InlineData("/roll 1d20")]
    [InlineData("/r 1d20")]
    [InlineData("/gmroll 1d20")]
    public void ARollCommandInFrontIsIgnored(string query) => Assert.Equal(7m, Total(query, 7));

    [Fact]
    [Trait("Parity", "Deviation")] // Roll20 macros are its own; here #Name is a saved query
    public void AMacroIsASavedQuery() =>
        Assert.Equal(14m, Run("#Stats", [3, 6, 1, 5], macros: new Dictionary<string, string> { ["Stats"] = "4d6kh3" }).As<decimal>());

    [Fact]
    [Trait("Parity", "Deviation")] // Roll20 reads @{...} and %{...} from a character sheet; here they're asked for
    public void CharacterSheetValuesAreAskedFor()
    {
        var interpreter = new Interpreter(new Context(Tables, new Faces()));
        IReadOnlyList<RollPrompt> prompts = interpreter.Prompts("1d20 + @{strength} + @{Bob|dexterity}; %{Bob|Attack}");

        Assert.Equal(["@{strength}", "@{Bob|dexterity}", "%{Bob|Attack}"], prompts.Select(prompt => prompt.Key));
        Assert.All(prompts, prompt => Assert.True(prompt.FromCharacterSheet));
        Assert.Throws<PromptsNeededException>(() => interpreter.Run("1d20 + @{strength}"));

        var answers = new Dictionary<string, string> { ["@{strength}"] = "3", ["@{Bob|dexterity}"] = "2", ["%{Bob|Attack}"] = "1d20+5" };
        QueryResult result = Run("1d20 + @{strength} + @{Bob|dexterity}; %{Bob|Attack}", [10, 12], answers);
        Assert.Equal([15m, 17m], result.Results.Select(part => part.As<decimal>()));
    }

    [Fact]
    [Trait("Parity", "Recalled")] // values from a sheet are put in as text, so they take part in the order of operations
    public void ACharacterSheetValueIsPutInAsText() =>
        Assert.Equal(5m, Run("@{x}*2", [], new Dictionary<string, string> { ["@{x}"] = "1+2" }).As<decimal>());

    // ---- Where we differ -----------------------------------------------------------------------------

    [Fact]
    [Trait("Parity", "Deviation")] // dividing by 0 is a mistake here, not Infinity
    public void DividingByZeroIsAMistake() => Assert.Throws<QueryException>(() => Total("1/0"));

    [Fact]
    [Trait("Parity", "Deviation")] // dice are capped at 1000 sides
    public void DiceHaveAtMostAThousandSides() => Assert.Throws<QueryException>(() => Total("1d1001"));
}
