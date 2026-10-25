using Rql.Nodes;
using Rql.Syntax;

namespace Rql.Tests.Syntax;

public class DiceNotationTest
{
    [Theory]
    [InlineData("d20", "d20")]
    [InlineData("4d6kh3", "4d6kh3")]
    [InlineData("4d6k3", "4d6kh3")]
    [InlineData("2d20kl1", "2d20kl1")]
    [InlineData("4d6d1", "4d6dl1")]
    [InlineData("4d6dh", "4d6dh1")]
    [InlineData("d6!", "d6!")]
    [InlineData("d6!>5", "d6!>5")]
    [InlineData("d6!5", "d6!5")]
    [InlineData("5d6!!", "5d6!!")]
    [InlineData("d6!p", "d6!p")]
    [InlineData("d20r1", "d20r1")]
    [InlineData("d20r<2", "d20r<2")]
    [InlineData("d20ro1r20", "d20ro1r20")]
    [InlineData("5d10>8", "5d10>8")]
    [InlineData("5d10=10", "5d10=10")]
    [InlineData("5d10>8f1", "5d10>8f1")]
    [InlineData("4d6s", "4d6s")]
    [InlineData("4d6sd", "4d6sd")]
    [InlineData("4dF", "4dF")]
    [InlineData("8D6!KH3", "8d6!kh3")]
    public void ReadsRoll20Notation(string written, string display)
    {
        Assert.Equal(display, DiceNotation.Parse(written).Display());
    }

    [Theory]
    [InlineData("d6!<6", "every face would explode")]
    [InlineData("d1!", "every face would explode")]
    [InlineData("d6r<6", "every face would be rerolled")]
    [InlineData("4dF!", "Fate dice can't explode")]
    [InlineData("5d10f1", "give a success target")]
    [InlineData("4d6kh3kl1", "only have one keep or drop")]
    [InlineData("4d6x", "isn't a dice modifier")]
    [InlineData("d1001", "at most 1000 sides")]
    public void RejectsDiceThatCantBeRolled(string written, string problem)
    {
        Assert.Contains(problem, Assert.Throws<DiceNotationException>(() => DiceNotation.Parse(written)).Message);
    }

    [Theory]
    [InlineData("d6", true)]
    [InlineData("D20", true)]
    [InlineData("d6s", true)]
    [InlineData("dF", true)]
    [InlineData("d6x", false)]
    [InlineData("dragons", false)]
    [InlineData("d", false)]
    public void KnowsWhichWordsAreDice(string word, bool dice)
    {
        Assert.Equal(dice, DiceNotation.IsDice(word));
    }
}
