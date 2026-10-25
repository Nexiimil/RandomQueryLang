using Rql.Nodes;
using Rql.Syntax;

namespace Rql.Tests.Nodes;

public class DiceTest
{
    private static DiceRoll Roll(string dice, params int[] faces) => DiceNotation.Parse(dice).Roll(new Faces(faces));

    private static List<int> Values(DiceRoll roll) => [.. roll.Faces.Select(die => die.Value)];

    [Fact]
    public void KeepingTheHighestDropsTheRest()
    {
        DiceRoll roll = Roll("4d6kh3", 3, 6, 1, 5);

        Assert.Equal(14, roll.Total);
        Assert.Equal([false, false, true, false], roll.Faces.Select(die => die.Dropped));
    }

    [Fact]
    public void KeepingTheLowestOfTiesKeepsTheFirst()
    {
        DiceRoll roll = Roll("2d20kl1", 7, 7);

        Assert.Equal(7, roll.Total);
        Assert.Equal([false, true], roll.Faces.Select(die => die.Dropped));
    }

    [Fact]
    public void DroppingTheLowestIsTheSameAsKeepingTheRest()
    {
        Assert.Equal(14, Roll("4d6d1", 3, 6, 1, 5).Total);
        Assert.Equal(4, Roll("4d6dh2", 3, 6, 1, 5).Total);
    }

    [Fact]
    public void ExplodingAddsADieForEachHighestFace()
    {
        DiceRoll roll = Roll("2d6!", 6, 6, 2, 4);

        Assert.Equal([6, 6, 2, 4], Values(roll));
        Assert.Equal([false, true, true, false], roll.Faces.Select(die => die.Exploded));
        Assert.Equal(18, roll.Total);
    }

    [Fact]
    public void ExplodingCanBeOnAnyFaceMatched()
    {
        Assert.Equal([5, 6, 1], Values(Roll("d6!>5", 5, 6, 1)));
    }

    [Fact]
    public void CompoundingDiceExplodeIntoThemselves()
    {
        DiceRoll roll = Roll("2d6!!", 6, 6, 3, 2);

        Assert.Equal([15, 2], Values(roll));
        Assert.True(roll.Faces[0].Exploded);
    }

    [Fact]
    public void PenetratingDiceTakeOneOffEachDieAdded()
    {
        Assert.Equal([6, 5, 2], Values(Roll("d6!p", 6, 6, 3)));
    }

    [Fact]
    public void RerollingKeepsGoingUntilTheFaceDoesntMatch()
    {
        DiceRoll roll = Roll("d20r<2", 1, 2, 1, 15);

        Assert.Equal(15, roll.Total);
        Assert.Equal([1, 2, 1], roll.Faces[0].Rerolled);
    }

    [Fact]
    public void RerollingOnceKeepsTheSecondFaceWhateverItIs()
    {
        Assert.Equal(1, Roll("d20ro1", 1, 1).Total);
    }

    [Fact]
    public void ASuccessTargetCountsSuccessesLessFailures()
    {
        DiceRoll roll = Roll("5d10>8f1", 9, 1, 10, 4, 8);

        Assert.Equal(2, roll.Total);
        Assert.Equal([true, false, true, false, true], roll.Faces.Select(die => die.Success));
        Assert.True(roll.Faces[1].Failure);
    }

    [Fact]
    public void DroppedDiceDontCountAsSuccesses()
    {
        Assert.Equal(1, Roll("2d20kh1>10", 15, 12).Total);
    }

    [Fact]
    public void SortingOrdersTheDice()
    {
        Assert.Equal([1, 3, 5, 6], Values(Roll("4d6s", 3, 6, 1, 5)));
        Assert.Equal([6, 5, 3, 1], Values(Roll("4d6sd", 3, 6, 1, 5)));
    }

    [Fact]
    public void FateDiceAreMinusOneZeroOrOne()
    {
        DiceRoll roll = DiceNotation.Parse("4dF").Roll(new Faces(-1, 0, 1, 1) { Fate = true });

        Assert.Equal([-1, 0, 1, 1], Values(roll));
        Assert.Equal(1, roll.Total);
    }

    [Fact]
    public void ATableOfDiceHasOnlyTheKeptOnes()
    {
        Assert.Equal(["6", "5", "3"], Roll("4d6kh3", 3, 6, 1, 5).AsTable().Things.Select(thing => thing.Name).Order().Reverse());
    }

    [Fact]
    public void DescribingARollShowsWhatBecameOfEachDie()
    {
        Assert.Equal("4d6kh3: 3, 6, (1), 5 = 14", Roll("4d6kh3", 3, 6, 1, 5).Describe());
        Assert.Equal("d6!: 6, 2! = 8", Roll("d6!", 6, 2).Describe());
        Assert.Equal("d20r1: 1→12 = 12", Roll("d20r1", 1, 12).Describe());
    }

    [Fact]
    public void DiceThatWouldNeverStopAreStoppedAnyway()
    {
        var always = new Faces([.. Enumerable.Repeat(6, Dice.MostDice + 1)]);

        Assert.Throws<DiceRollException>(() => DiceNotation.Parse("d6!").Roll(always));
    }
}
