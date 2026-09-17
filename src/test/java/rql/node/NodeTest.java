package rql.node;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.Map;
import java.util.Random;

import org.junit.jupiter.api.Test;

import rql.node.function.DropLowest;
import rql.node.function.Pick;
import rql.node.function.RollDice;
import rql.node.function.RollTable;
import rql.node.function.Sum;
import rql.table.Table;
import rql.table.TableFormatException;
import rql.table.TableParser;
import rql.table.Thing;

class NodeTest {
    private final Context context = new Context(Map.of(), new Random(1234));

    private static Table loot() throws TableFormatException {
        return TableParser.parse("loot", List.of("Sword;10", "Shield;5", "Potion;1"));
    }

    @Test
    void treesAreStitchedTogetherByConstructor() throws TableFormatException {
        // Sum(DropLowest(4d6)), built in Java rather than parsed.
        Node<Integer> query = new Sum(
                new DropLowest(
                        new RollDice(Literal.of(new Dice(4, 6)), Literal.of(1)),
                        Literal.of(1)));

        int total = query.evaluate(context);

        assertTrue(total >= 3 && total <= 18, "3d6 worth of dice, got " + total);
    }

    @Test
    void childrenGiveAWalkOverTheWholeTree() throws TableFormatException {
        Node<Table> query = new Pick(Literal.of(loot()), Literal.of(2));

        assertEquals(3, count(query));
    }

    private static int count(Node<?> node) {
        return 1 + node.children().stream().mapToInt(NodeTest::count).sum();
    }

    @Test
    void errorsBlameTheArgumentRatherThanAPosition() throws TableFormatException {
        Literal<Integer> row = Literal.of(99);
        Node<Table> query = new Pick(Literal.of(loot()), row);

        EvalException error = assertThrows(EvalException.class, () -> query.evaluate(context));

        assertSame(row, error.blame());
        assertTrue(error.getMessage().contains("has rows 1 to 3"), error.getMessage());
    }

    @Test
    void signatureFillsInDefaultsForArgumentsLeftOut() throws TableFormatException {
        Node<Table> rolled = RollTable.SIGNATURE.build(List.of(Literal.of(loot())));

        assertEquals(1, rolled.evaluate(context).size());
    }

    @Test
    void signatureRejectsAnArgumentOfTheWrongType() {
        assertThrows(IllegalArgumentException.class,
                () -> Sum.SIGNATURE.build(List.of(Literal.of(3))));
    }

    @Test
    void rollingDiceSeveralTimesGivesARowPerDie() {
        Node<Table> rolled = new RollDice(Literal.of(new Dice(4, 6)), Literal.of(2));

        assertEquals(8, rolled.evaluate(context).size());
    }

    @Test
    void aSettledContextRefusesToRoll() throws TableFormatException {
        Node<Table> rolling = new RollTable(Literal.of(loot()), Literal.of(1));

        assertThrows(IllegalStateException.class, () -> rolling.evaluate(Context.settled(Map.of())));
    }

    @Test
    void aSettledSubtreeCanBeWorkedOutWithoutRolling() throws TableFormatException {
        Node<Integer> settled = new Sum(Literal.of(loot()));

        assertEquals(16, settled.evaluate(Context.settled(Map.of())));
    }

    @Test
    void everyFunctionIsUsable() {
        for (Signature<?> signature : Functions.all()) {
            assertTrue(signature.required() <= signature.params().size(), signature.usage());
        }
    }

    @Test
    void droppingKeepsTheRestOfTheTable() {
        Table ties = new Table("ties", List.of(
                new Thing("A", 1, 0.25), new Thing("B", 1, 0.25), new Thing("C", 9, 0.5)));
        Node<Table> query = new DropLowest(Literal.of(ties), Literal.of(1));

        Table kept = query.evaluate(context);

        assertEquals(2, kept.size());
        assertTrue(kept.things().stream().anyMatch(thing -> thing.name().equals("C")));
    }
}
