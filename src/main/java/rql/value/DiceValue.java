package rql.value;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Row;
import rql.table.Table;

/** Dice such as 4d6. They're rolled fresh each time they're used, rather than kept as a table of faces. */
public record DiceValue(int count, int sides) implements Value {
    @Override
    public Type type() {
        return Type.DICE;
    }

    @Override
    public String display() {
        return (count == 1 ? "" : Integer.toString(count)) + "d" + sides;
    }

    /** Rolls every die, giving a row per die named and valued after the face it landed on. */
    public Table roll(RandomGenerator random) {
        List<Row> faces = new ArrayList<>(count);
        for (int i = 0; i < count; i++) {
            int face = random.nextInt(sides) + 1;
            faces.add(new Row(Integer.toString(face), face, 1.0 / count));
        }
        return new Table(display(), faces);
    }
}
