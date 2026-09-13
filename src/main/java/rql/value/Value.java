package rql.value;

/** The result of evaluating a query, or any part of one. */
public sealed interface Value permits NumberValue, TextValue, TableValue, DiceValue {
    Type type();

    String display();
}
