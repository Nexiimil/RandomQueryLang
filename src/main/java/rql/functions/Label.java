package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Row;
import rql.table.Table;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Type;
import rql.value.Value;

/**
 * Labels a result so it can be told apart from others in a table. A number becomes a row named after the
 * label, like "Strength (15)". Text and the rows of a table get the label in front of their names, like
 * "Class: Fighter (10)".
 */
public final class Label implements Function {
    @Override
    public String name() {
        return "Label";
    }

    @Override
    public List<Parameter> params() {
        return List.of(
                Parameter.required("label", Type.TEXT),
                Parameter.required("value", Type.NUMBER, Type.TEXT, Type.TABLE));
    }

    @Override
    public Type returnType() {
        return Type.TABLE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        String label = args.text(0);
        List<Row> rows = switch (args.value(1)) {
            case NumberValue number -> List.of(new Row(label, number.value(), 1));
            case TextValue text -> List.of(
                    new Row(label + ": " + text.value(), Row.defaultValue(text.value(), 1), 1));
            case TableValue table -> table.table().rows().stream()
                    .map(row -> new Row(label + ": " + row.name(), row.value(), row.chance()))
                    .toList();
            case DiceValue dice -> throw new IllegalStateException("dice are converted to a number before Label runs");
        };
        return new TableValue(new Table(label, rows));
    }
}
