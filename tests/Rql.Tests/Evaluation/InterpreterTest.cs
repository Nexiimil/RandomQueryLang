using Rql.Evaluation;
using Rql.Nodes;
using Rql.Tables;

namespace Rql.Tests.Evaluation;

public class InterpreterTest
{
    private readonly Interpreter interpreter;

    public InterpreterTest()
    {
        var tables = new Dictionary<string, Table>
        {
            ["loot"] = TableDefinition.Parse("loot", ["Sword;10", "Shield;5", "Potion;1"]).Resolve(),
            ["weighted"] = TableDefinition.Parse("weighted", ["Never;0;0", "Always"]).Resolve(),
            ["mostly"] = TableDefinition.Parse("mostly", ["A;;0.9", "B"]).Resolve(),
            ["ties"] = TableDefinition.Parse("ties", ["A;1", "B;1", "C;2"]).Resolve(),
        };
        interpreter = new Interpreter(new Context(tables, new Random(1234)));
    }

    [Fact]
    public void RollPicksTheGivenNumberOfRowsFromTheTable()
    {
        List<string> rolled = Rows("Roll(loot, 5)");

        Assert.Equal(5, rolled.Count);
        Assert.All(rolled, name => Assert.Contains(name, new[] { "Sword", "Shield", "Potion" }));
    }

    [Fact]
    public void RollRollsOnceByDefault()
    {
        Assert.Single(Rows("Roll(loot)"));
    }

    [Fact]
    public void RollNeverPicksARowWithNoChance()
    {
        Assert.Equal(["Always"], Rows("Roll(weighted, 1000)").Distinct());
    }

    [Fact]
    public void RollFollowsTheChanceOfEachRow()
    {
        int timesA = Rows("Roll(mostly, 10000)").Count(name => name == "A");

        Assert.InRange(timesA, 8701, 9299);
    }

    [Fact]
    public void PickCountsRowsFromTheTopStartingAtOne()
    {
        Assert.Equal(["Sword"], Rows("Pick(loot, 1)"));
        Assert.Equal(["Potion"], Rows("Pick(loot, 3)"));
    }

    [Theory]
    [InlineData("Pick(loot, 0)")]
    [InlineData("Pick(loot, 4)")]
    public void PickingOutsideTheTableIsAnError(string query)
    {
        Assert.Equal(11, Error(query).Position);
    }

    [Fact]
    public void DropLowestRemovesTheLowestValuesAndKeepsTheOrder()
    {
        Assert.Equal(["Sword", "Shield"], Rows("DropLowest(loot)"));
        Assert.Equal(["Sword"], Rows("DropLowest(loot, 2)"));
        Assert.Empty(Rows("DropLowest(loot, 10)"));
    }

    [Fact]
    public void DropHighestRemovesTheHighestValues()
    {
        Assert.Equal(["Shield", "Potion"], Rows("DropHighest(loot)"));
    }

    [Fact]
    public void DroppingBreaksTiesAtRandom()
    {
        var outcomes = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            outcomes.Add(string.Join(",", Rows("DropLowest(ties)")));
        }

        Assert.Equal(new HashSet<string> { "B,C", "A,C" }, outcomes);
    }

    [Fact]
    public void RollingDiceGivesARowPerDieWorthTheFaceItLandedOn()
    {
        IReadOnlyList<Thing> faces = Things("Roll(d6, 50)");

        Assert.Equal(50, faces.Count);
        Assert.All(faces, face =>
        {
            Assert.InRange(face.Value, 1, 6);
            Assert.Equal(face.Value.ToString(), face.Name);
        });
    }

    [Fact]
    public void RollingSeveralDiceRollsEachOfThem()
    {
        Assert.Equal(4, Things("Roll(4d6)").Count);
        Assert.Equal(8, Things("Roll(4d6, 2)").Count);
    }

    [Fact]
    public void DiceUsedAsATableAreRolled()
    {
        Assert.Equal(3, Number("Sum(3d1)"));
        for (int i = 0; i < 200; i++)
        {
            Assert.InRange(Number("Sum(DropLowest(4d6))"), 3, 18);
        }
    }

    [Fact]
    public void DiceUsedAsANumberAreRolledAndAddedUp()
    {
        Assert.Equal(3, Rows("Roll(loot, 3d1)").Count);
        for (int i = 0; i < 100; i++)
        {
            Assert.InRange(Rows("Roll(loot, d4)").Count, 1, 4);
        }
    }

    [Fact]
    public void DiceCanHaveUpToAThousandSides()
    {
        Assert.InRange(Number("d1000"), 1, 1000);
        QueryException error = Error("2 + 3d1001");

        Assert.Equal(6, error.Position);
        Assert.Contains("at most 1000 sides", error.Message);
    }

    [Fact]
    public void DiceOnTheirOwnAreRolled()
    {
        Assert.Equal(3, Number("3d1"));
        Assert.Equal("3d1: 1, 1, 1 = 3", Assert.Single(interpreter.Run("3d1").Single.Rolls).Describe());
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("10 - 2 - 3", 5)]
    [InlineData("7 / 2", 3.5)]
    [InlineData("-7 / 2", -3.5)]
    [InlineData("7 % 3", 1)]
    [InlineData("-(2 + 3)", -5)]
    [InlineData("3d1 + 2", 5)]
    [InlineData("2 * 4d1kh3", 6)]
    [InlineData("Sum(loot) + Pick(loot, 3)", 17)]
    [InlineData("\"4\" + 1", 5)]
    public void ArithmeticWorksOnAnythingThatIsANumber(string query, double expected)
    {
        Assert.Equal((decimal)expected, Number(query));
    }

    [Fact]
    public void DividingByZeroBlamesWhatWasDividedBy()
    {
        Assert.Equal(5, Error("d6 / 0").Position);
        // Nothing rolls, so it's caught before anything is rolled.
        Assert.Equal(7, Assert.Throws<QueryException>(() => interpreter.Validate("6 / (1 - 1)")).Position);
    }

    [Fact]
    public void ArithmeticCanBeAnArgument()
    {
        Assert.Equal(4, Rows("Roll(loot, 2d1 + 2)").Count);
    }

    [Fact]
    public void SeveralRollsGiveSeveralResultsEachWithItsOwnDice()
    {
        QueryResult result = interpreter.Run("Attack: 1d1 + 5; Damage: 2d1 + 3; Pick(loot, 1)");

        Assert.Equal(["Attack", "Damage", null], result.Results.Select(part => part.Label));
        Assert.Equal([6, 5], result.Results.Take(2).Select(part => part.As<decimal>()));
        Assert.Equal(["d1: 1 = 1"], result.Results[0].Rolls.Select(roll => roll.Describe()));
        Assert.Equal(["2d1: 1, 1 = 2"], result.Results[1].Rolls.Select(roll => roll.Describe()));
        Assert.Empty(result.Results[2].Rolls);
        Assert.Contains("Damage: 5", result.Display());
    }

    [Fact]
    public void AQueryOfSeveralRollsPrintsAsItsDisplay()
    {
        QueryResult result = interpreter.Run("Attack: 1d1 + 5; Damage: 2d1 + 3");

        Assert.Equal(result.Display(), result.ToString());
    }

    [Fact]
    public void AMistakeInAnyRollStopsTheWholeQueryBeforeRolling()
    {
        Assert.Equal(17, Error("Roll(loot); Roll(nope)").Position);
    }

    [Fact]
    public void SuccessesCountAsTheNumber()
    {
        Assert.Equal(5, Number("5d1>1"));
        Assert.Equal(0, Number("5d1>2"));
    }

    [Fact]
    public void FunctionNamesIgnoreCase()
    {
        Assert.Equal(["Sword"], Rows("pick(loot, 1)"));
    }

    [Fact]
    public void TextCanNameATable()
    {
        Assert.Equal(["Sword"], Rows("Pick(\"loot\", 1)"));
    }

    [Fact]
    public void TextThatIsANumberCanBeUsedAsANumber()
    {
        Assert.Equal(3, Rows("Roll(loot, \"3\")").Count);
    }

    [Fact]
    public void ATableWithOneRowCanBeUsedAsANumber()
    {
        // Potion is worth 1, so this picks row 1.
        Assert.Equal(["Sword"], Rows("Pick(loot, Pick(loot, 3))"));
    }

    [Fact]
    public void ATableWithSeveralRowsIsNotANumber()
    {
        QueryException error = Error("Roll(loot, Roll(d6, 2))");

        Assert.Equal(11, error.Position);
        Assert.Contains("Sum", error.Message);
    }

    [Fact]
    public void MistakesAreReportedBeforeAnythingIsRolled()
    {
        // Rolling -1 times would fail when run, but the text that isn't a number is caught first.
        Assert.Equal(21, Error("Roll(Roll(loot, -1), \"abc\")").Position);
    }

    [Theory]
    [InlineData("Roll(nope, 1)", 5)]
    [InlineData("Roll(\"nope\", 1)", 5)]
    [InlineData("Roll(Sum(loot), 1)", 5)]
    [InlineData("Pick(Sum(4d6), 1)", 5)]
    [InlineData("Roll(3)", 5)]
    [InlineData("Frobnicate(loot)", 0)]
    [InlineData("Roll()", 0)]
    [InlineData("Pick(loot)", 0)]
    [InlineData("Roll(loot, 1, 2)", 0)]
    [InlineData("d6!<6", 0)]
    [InlineData("Roll(4d6kq)", 9)]
    public void RejectsQueriesThatCanNeverWork(string query, int position)
    {
        QueryException error = Error(query);

        Assert.True(position == error.Position, error.Message);
    }

    [Fact]
    public void ValidatingReportsTheSameMistakesWithoutRolling()
    {
        Assert.Equal(21, Assert.Throws<QueryException>(() => interpreter.Validate("Roll(Roll(loot, -1), \"abc\")")).Position);
        interpreter.Validate("Pick(loot, d6)");
    }

    [Fact]
    public void ANumberIsStillNotATableWhenRollingTakesSomethingElse()
    {
        Assert.Contains("a table", Error("Roll(Sum(loot))").Message);
    }

    private IReadOnlyList<Thing> Things(string query) => interpreter.Run(query).As<Table>().Things;

    private List<string> Rows(string query) => [.. Things(query).Select(thing => thing.Name)];

    private decimal Number(string query) => interpreter.Run(query).As<decimal>();

    private QueryException Error(string query) => Assert.Throws<QueryException>(() => interpreter.Run(query));
}
