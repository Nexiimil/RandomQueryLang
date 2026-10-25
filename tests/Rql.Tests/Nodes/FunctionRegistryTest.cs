using Rql.Evaluation;
using Rql.Functions;
using Rql.Nodes;
using Rql.Tables;

namespace Rql.Tests.Nodes;

/// <summary>A function a game might add of its own, written in nothing but the record.</summary>
[Function("Repeat")]
public sealed record Repeat(Node<string> Text, [Default(2)] Node<decimal> Times) : Function<string>
{
    public override string Evaluate(Context context) =>
        string.Concat(Enumerable.Repeat(Text.Evaluate(context), (int)Times.Evaluate(context)));
}

[Function("Broken")]
public sealed record TakesARawNumber(int Count) : Function<decimal>
{
    public override decimal Evaluate(Context context) => Count;
}

[Function("Broken")]
public sealed record DefaultOfTheWrongType(Node<decimal> Count, [Default("three")] Node<decimal> Other) : Function<decimal>
{
    public override decimal Evaluate(Context context) => Count.Evaluate(context);
}

[Function("Broken")]
public sealed record RequiredAfterOptional([Default(1)] Node<decimal> First, Node<decimal> Second) : Function<decimal>
{
    public override decimal Evaluate(Context context) => First.Evaluate(context);
}

public sealed record NotMarked(Node<decimal> Count) : Function<decimal>
{
    public override decimal Evaluate(Context context) => Count.Evaluate(context);
}

[Function("Roll")]
public sealed record AnotherTableRoll(Node<Table> Table) : Function<Table>
{
    public override Table Evaluate(Context context) => Table.Evaluate(context);
}

public class FunctionRegistryTest
{
    private static readonly Dictionary<string, Table> NoTables = [];

    [Fact]
    public void EveryBuiltinFunctionIsUsable()
    {
        foreach (Signature signature in FunctionRegistry.Builtins().All)
        {
            Assert.True(signature.Required <= signature.Parameters.Count, signature.Usage);
        }
    }

    [Fact]
    public void AGameCanAddItsOwnFunctions()
    {
        var functions = FunctionRegistry.Builtins().Add<Repeat>();
        var interpreter = new Interpreter(new Context(NoTables, new Random(0)), functions);

        Assert.Equal("abab", interpreter.Run("Repeat(\"ab\")").As<string>());
        Assert.Equal("777", interpreter.Run("repeat(7, 3)").As<string>());
    }

    [Theory]
    [InlineData(typeof(TakesARawNumber), "every parameter must be a Node")]
    [InlineData(typeof(DefaultOfTheWrongType), "default for Other")]
    [InlineData(typeof(RequiredAfterOptional), "comes after an optional parameter")]
    [InlineData(typeof(NotMarked), "no [Function] attribute")]
    [InlineData(typeof(string), "no [Function] attribute")]
    public void AFunctionThatCantBeCalledIsRejectedWhenAdded(Type function, string problem)
    {
        var error = Assert.Throws<ArgumentException>(() => new FunctionRegistry().Add(function));

        Assert.Contains(problem, error.Message);
    }

    [Fact]
    public void TwoFunctionsCantShareANameAndFirstArgumentType()
    {
        var error = Assert.Throws<ArgumentException>(() => FunctionRegistry.Builtins().Add<AnotherTableRoll>());

        Assert.Contains("a table", error.Message);
    }

    [Fact]
    public void FunctionsAreFoundIgnoringCase()
    {
        Assert.Equal(2, FunctionRegistry.Builtins().Named("ROLL").Count);
        Assert.Empty(FunctionRegistry.Builtins().Named("Frobnicate"));
    }
}
