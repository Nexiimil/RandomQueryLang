using Rql.Nodes;
using Rql.Syntax;
using Rql.Tables;

namespace Rql.Evaluation;

/// <summary>
/// Turns what was written into a tree. Everything that can be settled without rolling is settled here:
/// which function was meant, whether it was given the right number of arguments, which tables the names
/// refer to, and what has to be converted to what.
/// </summary>
/// <remarks>
/// A tree that comes out of here has types that already line up, which is why nothing downstream checks
/// them again.
/// </remarks>
public sealed class Binder
{
    private readonly TableCatalog tables;
    private readonly FunctionRegistry functions;
    private readonly Dictionary<INode, int> positions = new(ReferenceEqualityComparer.Instance);

    private Binder(TableCatalog tables, FunctionRegistry functions)
    {
        this.tables = tables;
        this.functions = functions;
    }

    private static readonly Parameter Operand = new("operand", typeof(decimal), null);

    /// <summary>
    /// Binds one roll of a query. Dice that are the whole of it are rolled, as <c>d20</c> on its own means
    /// to roll it.
    /// </summary>
    public static BoundQuery Bind(Expr expr, TableCatalog tables, FunctionRegistry functions)
    {
        var binder = new Binder(tables, functions);
        INode root = binder.Node(expr);
        if (root.ValueType == typeof(Dice))
        {
            root = binder.Convert(Operand, root, expr);
        }
        return new BoundQuery(root, binder.positions);
    }

    /// <summary>Binds something that has to come to a number, such as an inline roll.</summary>
    public static BoundQuery BindNumber(Expr expr, TableCatalog tables, FunctionRegistry functions)
    {
        var binder = new Binder(tables, functions);
        INode root = binder.Node(expr);
        if (root.ValueType != typeof(decimal))
        {
            if (!Coerce.Possible(root.ValueType, typeof(decimal)))
            {
                throw new QueryException($"an inline roll has to come to a number, but this is {ValueTypes.Describe(root.ValueType)}", expr.Position);
            }
            root = binder.Convert(Operand, root, expr);
        }
        return new BoundQuery(root, binder.positions);
    }

    private INode Node(Expr expr)
    {
        INode bound = expr switch
        {
            NumberLiteral number => Literal.Of(number.Value),
            TextLiteral text => Literal.Of(text.Value),
            DiceLiteral dice => Literal.Of(dice.Dice),
            Binary binary => new Arithmetic(binary.Operator,
                (Node<decimal>)Convert(Operand, Node(binary.Left), binary.Left),
                (Node<decimal>)Convert(Operand, Node(binary.Right), binary.Right)),
            Negate negate => new Negation((Node<decimal>)Convert(Operand, Node(negate.Operand), negate.Operand)),
            TableRoll roll => new Functions.RollTable(
                Literal.Of(Table(new Identifier(roll.Name, roll.Position))), Literal.Of((decimal)roll.Count)),
            Labelled { Inner: DiceLiteral dice } labelled => Literal.Of(dice.Dice with { Label = labelled.Label }),
            Labelled labelled => Labelling.Label(Node(labelled.Inner), labelled.Label),
            Group group => Group(group),
            Identifier identifier => Literal.Of(Table(identifier)),
            Call call => Call(call),
            _ => throw new InvalidOperationException($"nothing binds {expr.GetType().Name}"),
        };
        positions.TryAdd(bound, expr.Position);
        return bound;
    }

    private INode Group(Group group)
    {
        if (group.Items.Count == 1 && Pool(group.Items[0], 1) is { Dice.Count: > 0 } pool)
        {
            var (dice, signs, offset) = pool;
            List<Node<Dice>> nodes = [.. dice.Select(literal =>
            {
                var node = Literal.Of(literal.Dice);
                positions.TryAdd(node, literal.Position);
                return (Node<Dice>)node;
            })];
            return new GroupOfDice(nodes, signs, offset, group.Modifiers, group.Text);
        }
        List<Node<decimal>> items = [.. group.Items.Select(item => (Node<decimal>)Convert(Operand, Node(item), item))];
        return new GroupOfRolls(items, [.. group.Items.Select((_, index) => $"#{index + 1}")], group.Modifiers, group.Text);
    }

    /// <summary>
    /// Dice and numbers only added or taken away, as the dice with whether each is taken away and the
    /// numbers added up, or null for anything else.
    /// </summary>
    private static (List<DiceLiteral> Dice, List<int> Signs, decimal Offset)? Pool(Expr expr, int sign)
    {
        switch (expr)
        {
            case DiceLiteral dice:
                return ([dice], [sign], 0);
            case NumberLiteral number:
                return ([], [], sign * number.Value);
            case Negate negate:
                return Pool(negate.Operand, -sign);
            case Binary { Operator: "+" or "-" } binary:
                var left = Pool(binary.Left, sign);
                var right = Pool(binary.Right, binary.Operator == "+" ? sign : -sign);
                if (left is null || right is null)
                {
                    return null;
                }
                return ([.. left.Value.Dice, .. right.Value.Dice], [.. left.Value.Signs, .. right.Value.Signs],
                    left.Value.Offset + right.Value.Offset);
            default:
                return null;
        }
    }

    private INode Call(Call call)
    {
        IReadOnlyList<Signature> candidates = functions.Named(call.Name);
        if (candidates.Count == 0)
        {
            throw new QueryException($"unknown function {call.Name}", call.Position);
        }

        List<INode> arguments = [.. call.Arguments.Select(Node)];

        Signature signature = Choose(candidates, arguments);
        if (arguments.Count < signature.Required || arguments.Count > signature.Parameters.Count)
        {
            throw new QueryException(
                $"expected {signature.Usage} but got {arguments.Count} argument{(arguments.Count == 1 ? "" : "s")}",
                call.Position);
        }

        var converted = new List<INode>(arguments.Count);
        for (int i = 0; i < arguments.Count; i++)
        {
            converted.Add(Convert(signature.Parameters[i], arguments[i], call.Arguments[i]));
        }
        return signature.Build(converted);
    }

    /// <summary>
    /// Picks between functions sharing a name by the type of the first argument, which is what keeps
    /// rolling dice and rolling on a table separate nodes. Failing an exact match, it takes the first that
    /// the argument can be converted for, and failing that the first of them, so that the mistake is
    /// reported against the usual shape of the function.
    /// </summary>
    private static Signature Choose(IReadOnlyList<Signature> candidates, List<INode> arguments)
    {
        if (candidates.Count == 1 || arguments.Count == 0)
        {
            return candidates[0];
        }
        Type given = arguments[0].ValueType;
        Type? First(Signature candidate) => candidate.Parameters.FirstOrDefault()?.Type;
        return candidates.FirstOrDefault(candidate => First(candidate) == given)
            ?? candidates.FirstOrDefault(candidate => First(candidate) is Type wanted && Coerce.Possible(given, wanted))
            ?? candidates[0];
    }

    private INode Convert(Parameter parameter, INode argument, Expr wrote)
    {
        if (argument.ValueType == parameter.Type)
        {
            return argument;
        }
        if (!Coerce.Possible(argument.ValueType, parameter.Type))
        {
            throw new QueryException(
                $"expected {ValueTypes.Describe(parameter.Type)} but {Describe(argument, wrote)}", wrote.Position);
        }
        INode coerced = Coerce.To(parameter.Type, argument);
        positions.TryAdd(coerced, wrote.Position);
        return coerced;
    }

    private static string Describe(INode argument, Expr wrote) => wrote switch
    {
        Call call => $"{call.Name} gives {ValueTypes.Describe(argument.ValueType)}",
        NumberLiteral number => $"got the number {Numbers.Display(number.Value)}",
        _ => $"got {ValueTypes.Describe(argument.ValueType)}",
    };

    private Table Table(Identifier identifier) =>
        tables.Find(identifier.Name, out string? problem) ?? throw new QueryException(problem!, identifier.Position);
}
