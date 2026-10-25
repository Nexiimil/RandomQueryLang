using Rql.Nodes;

namespace Rql.Evaluation;

/// <summary>
/// What one roll of a query came to, the label it was given, and the dice rolled along the way, so it can be
/// shown how the result came about.
/// </summary>
public sealed record Result(object Value, IReadOnlyList<DiceRoll> Rolls, string? Label = null)
{
    public Result(object value)
        : this(value, [])
    {
    }

    public Type Type => Value.GetType();

    /// <summary>Groups kept or counted while working this out.</summary>
    public IReadOnlyList<GroupRoll> Groups { get; init; } = [];

    /// <summary>Labelled terms and what they came to.</summary>
    public IReadOnlyList<LabelledValue> Labels { get; init; } = [];

    /// <summary>Inline rolls in this roll, rolled first and put in place as numbers.</summary>
    public IReadOnlyList<InlineRoll> Inline { get; init; } = [];

    /// <summary>How the value is shown to whoever ran the query.</summary>
    public string Display() => ValueTypes.Display(Value);

    /// <summary>Reads the value back, failing if the query produced a different type than expected.</summary>
    public T As<T>() => Value is T value
        ? value
        : throw new InvalidOperationException(
            $"the query gave {ValueTypes.Describe(Type)}, not {ValueTypes.Describe(typeof(T))}");
}

/// <summary>What a whole query came to: a result for each roll in it, in the order they were written.</summary>
public sealed record QueryResult(IReadOnlyList<Result> Results)
{
    /// <summary>The only result of a query of one roll.</summary>
    /// <exception cref="InvalidOperationException">The query had more than one roll.</exception>
    public Result Single => Results.Count == 1
        ? Results[0]
        : throw new InvalidOperationException($"the query had {Results.Count} rolls, not 1");

    /// <inheritdoc cref="Result.As{T}"/>
    public T As<T>() => Single.As<T>();

    /// <summary>Each result on its own lines, after its label, or its number when there's more than one.</summary>
    public string Display()
    {
        if (Results.Count == 1 && Results[0].Label is null)
        {
            return Results[0].Display();
        }
        return string.Join(Environment.NewLine, Results.Select((result, index) =>
        {
            string value = result.Display();
            string label = result.Label ?? $"#{index + 1}";
            return value.Contains('\n') ? $"{label}:{Environment.NewLine}{value}" : $"{label}: {value}";
        }));
    }
}
