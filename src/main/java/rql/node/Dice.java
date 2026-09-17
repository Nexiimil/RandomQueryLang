package rql.node;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Table;
import rql.table.Thing;

/**
 * Dice such as 4d6, described rather than rolled. Holding the description means a node can roll them
 * as many times as it needs to, and that nothing is rolled until something asks.
 */
public record Dice(int count, int sides) {
    public Dice {
        if (count < 1) {
            throw new IllegalArgumentException("can't roll " + count + " dice");
        }
        if (sides < 1) {
            throw new IllegalArgumentException("a die needs at least 1 side");
        }
    }

    public String display() {
        return (count == 1 ? "" : Integer.toString(count)) + "d" + sides;
    }

    /** Rolls every die, giving a row per die named and valued after the face it landed on. */
    public Table roll(RandomGenerator random) {
        List<Thing> faces = new ArrayList<>(count);
        for (int i = 0; i < count; i++) {
            int face = random.nextInt(sides) + 1;
            faces.add(new Thing(Integer.toString(face), face, 1.0 / count));
        }
        return new Table(display(), faces);
    }
}
