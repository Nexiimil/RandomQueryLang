using System.Reflection;
using Rql.Functions;

namespace Rql.Nodes;

/// <summary>
/// The functions a query can call, looked up by name ignoring case. A game can add its own alongside the
/// built in ones by writing a <see cref="Function{T}"/> record and passing it to <see cref="Add{TFunction}"/>.
/// </summary>
public sealed class FunctionRegistry
{
    private readonly Dictionary<string, List<Signature>> byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Signature> all = [];

    /// <summary>Every function that comes with RandomQueryLang.</summary>
    public static FunctionRegistry Builtins() => new FunctionRegistry()
        .Add<RollTable>()
        .Add<RollDice>()
        .Add<Pick>()
        .Add<DropLowest>()
        .Add<DropHighest>()
        .Add<Sum>()
        .Add<Floor>()
        .Add<Ceil>()
        .Add<Round>()
        .Add<Abs>();

    /// <summary>Every function, in the order they were added.</summary>
    public IReadOnlyList<Signature> All => all;

    /// <summary>Adds a function record so queries can call it.</summary>
    /// <exception cref="ArgumentException">
    /// The type can't be called as a function, or a function of the same name already takes the same type
    /// first, so a query couldn't say which it meant.
    /// </exception>
    public FunctionRegistry Add<TFunction>() where TFunction : INode => Add(typeof(TFunction));

    /// <exception cref="ArgumentException">
    /// The type can't be called as a function, or a function of the same name already takes the same type
    /// first, so a query couldn't say which it meant.
    /// </exception>
    public FunctionRegistry Add(Type function)
    {
        Signature signature = Signature.Of(function);
        if (!byName.TryGetValue(signature.Name, out var sharing))
        {
            byName[signature.Name] = sharing = [];
        }
        Type? first = signature.Parameters.FirstOrDefault()?.Type;
        if (sharing.Any(other => other.Parameters.FirstOrDefault()?.Type == first))
        {
            throw new ArgumentException(
                $"{signature.Name} already has a version taking {(first is null ? "nothing" : ValueTypes.Describe(first))} first");
        }
        sharing.Add(signature);
        all.Add(signature);
        return this;
    }

    /// <summary>Adds every type in an assembly marked with <see cref="FunctionAttribute"/>.</summary>
    public FunctionRegistry AddAll(Assembly assembly)
    {
        foreach (Type type in assembly.GetTypes().Where(type => type.IsDefined(typeof(FunctionAttribute), false)))
        {
            Add(type);
        }
        return this;
    }

    /// <summary>The functions written with this name, or none.</summary>
    public IReadOnlyList<Signature> Named(string name) =>
        byName.TryGetValue(name, out var signatures) ? signatures : [];
}
