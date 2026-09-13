package rql.eval;

import rql.QueryException;
import rql.table.Table;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Type;
import rql.value.Value;

/**
 * Converts values to the type a function needs. Text can stand in for a number when it is one, or for
 * a table by naming it. A table with one row can stand in for that row's value or name. Dice are
 * rolled, giving a row per die when a table is needed, or their total when a number is.
 */
final class Coercion {
    private Coercion() {
    }

    static Value convert(Type type, Value value, Environment environment, int position) {
        return switch (type) {
            case NUMBER -> new NumberValue(toNumber(value, environment, position));
            case TEXT -> new TextValue(toText(value, environment, position));
            case TABLE -> new TableValue(toTable(value, environment, position));
            case DICE -> {
                if (value instanceof DiceValue) {
                    yield value;
                }
                throw new QueryException("expected dice but got " + value.type().description(), position);
            }
        };
    }

    static Table findTable(Environment environment, String name, int position) {
        return environment.findTable(name)
                .orElseThrow(() -> new QueryException("no table named \"" + name + "\"", position));
    }

    private static int toNumber(Value value, Environment environment, int position) {
        return switch (value) {
            case NumberValue number -> number.value();
            case TextValue text -> {
                try {
                    yield Integer.parseInt(text.value().trim());
                } catch (NumberFormatException e) {
                    throw new QueryException(
                            "expected a number but got the text \"" + text.value() + "\"", position);
                }
            }
            case DiceValue dice -> total(dice.roll(environment.random()), position);
            case TableValue table when table.table().size() == 1 -> table.table().things().getFirst().value();
            case TableValue table -> throw new QueryException("expected a number but got a table with "
                    + table.table().size() + " rows (use Sum to add them up)", position);
        };
    }

    private static String toText(Value value, Environment environment, int position) {
        return switch (value) {
            case TextValue text -> text.value();
            case NumberValue number -> Integer.toString(number.value());
            case DiceValue dice -> Integer.toString(total(dice.roll(environment.random()), position));
            case TableValue table when table.table().size() == 1 -> table.table().things().getFirst().name();
            case TableValue table -> throw new QueryException(
                    "expected text but got a table with " + table.table().size() + " rows", position);
        };
    }

    private static Table toTable(Value value, Environment environment, int position) {
        return switch (value) {
            case TableValue table -> table.table();
            case TextValue text -> findTable(environment, text.value(), position);
            case DiceValue dice -> dice.roll(environment.random());
            case NumberValue number -> throw new QueryException(
                    "expected a table but got the number " + number.value(), position);
        };
    }

    private static int total(Table table, int position) {
        long total = table.total();
        if (total < Integer.MIN_VALUE || total > Integer.MAX_VALUE) {
            throw new QueryException("the total " + total + " is too large", position);
        }
        return (int) total;
    }
}
