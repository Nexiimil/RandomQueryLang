namespace Rql.Syntax;

/// <summary>A parsed query, before anything has been looked up or rolled.</summary>
internal abstract record Expr(int Position);

internal sealed record NumberLiteral(decimal Value, int Position) : Expr(Position);

internal sealed record TextLiteral(string Value, int Position) : Expr(Position);

/// <summary>Dice as written, already read into what they mean, such as <c>4d6kh3</c>.</summary>
internal sealed record DiceLiteral(Rql.Nodes.Dice Dice, int Position) : Expr(Position);

/// <summary>A bare name, which refers to a table.</summary>
internal sealed record Identifier(string Name, int Position) : Expr(Position);

internal sealed record Call(string Name, IReadOnlyList<Expr> Arguments, int Position) : Expr(Position)
{
    // A record compares lists by reference, so two calls written the same way need telling that they are.
    public bool Equals(Call? other) =>
        other is not null && Name == other.Name && Position == other.Position
        && Arguments.SequenceEqual(other.Arguments);

    public override int GetHashCode() => HashCode.Combine(Name, Position, Arguments.Count);
}

/// <summary>Arithmetic on two numbers: <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c> or <c>**</c>, at the operator's position.</summary>
internal sealed record Binary(string Operator, Expr Left, Expr Right, int Position) : Expr(Position);

/// <summary>A minus in front of something that isn't just a number, like <c>-d6</c>.</summary>
internal sealed record Negate(Expr Operand, int Position) : Expr(Position);

/// <summary>A roll on a table in Roll20's notation, <c>2t[Flaws]</c>.</summary>
internal sealed record TableRoll(int Count, string Name, int Position) : Expr(Position);

/// <summary>A term with a label after it, like <c>5[STR]</c>; the label is shown, and changes nothing.</summary>
internal sealed record Labelled(Expr Inner, string Label, int Position) : Expr(Position);

/// <summary>
/// A group of rolls in braces, <c>{d20, d20}kh1</c>, with what to keep or count among them. <c>Text</c> is
/// the group as written, for showing beside how it came out.
/// </summary>
internal sealed record Group(IReadOnlyList<Expr> Items, Rql.Nodes.GroupModifiers Modifiers, string Text, int Position) : Expr(Position)
{
    public bool Equals(Group? other) =>
        other is not null && Position == other.Position && Modifiers == other.Modifiers && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => HashCode.Combine(Position, Items.Count);
}

/// <summary>
/// One roll in a query, with the label it was given, if any, as in <c>Attack: d20 + 5</c>, and where in the
/// query it starts and ends.
/// </summary>
internal sealed record Statement(string? Label, Expr Expr, int Start = 0, int End = 0);
