package rql.eval;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.Set;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.junit.jupiter.params.provider.ValueSource;

import rql.QueryException;
import rql.table.Table;
import rql.table.TableFormatException;
import rql.table.TableParser;
import rql.table.Thing;
import rql.value.NumberValue;
import rql.value.TableValue;

class InterpreterTest {
    private Interpreter interpreter;

    @BeforeEach
    void setUp() throws TableFormatException {
        Map<String, Table> tables = Map.of(
                "loot", TableParser.parse("loot", List.of("Sword;10", "Shield;5", "Potion;1")),
                "weighted", TableParser.parse("weighted", List.of("Never;0;0", "Always")),
                "mostly", TableParser.parse("mostly", List.of("A;;0.9", "B")),
                "ties", TableParser.parse("ties", List.of("A;1", "B;1", "C;2")));
        interpreter = new Interpreter(new Environment(tables, new Random(1234)));
    }

    @Test
    void rollPicksTheGivenNumberOfRowsFromTheTable() {
        List<String> rolled = rows("Roll(loot, 5)");

        assertEquals(5, rolled.size());
        assertTrue(List.of("Sword", "Shield", "Potion").containsAll(rolled), rolled.toString());
    }

    @Test
    void rollRollsOnceByDefault() {
        assertEquals(1, rows("Roll(loot)").size());
    }

    @Test
    void rollNeverPicksARowWithNoChance() {
        assertEquals(Set.of("Always"), Set.copyOf(rows("Roll(weighted, 1000)")));
    }

    @Test
    void rollFollowsTheChanceOfEachRow() {
        long timesA = rows("Roll(mostly, 10000)").stream().filter("A"::equals).count();

        assertTrue(timesA > 8700 && timesA < 9300, "A was rolled " + timesA + " times out of 10000");
    }

    @Test
    void pickCountsRowsFromTheTopStartingAtOne() {
        assertEquals(List.of("Sword"), rows("Pick(loot, 1)"));
        assertEquals(List.of("Potion"), rows("Pick(loot, 3)"));
    }

    @ParameterizedTest
    @ValueSource(strings = {"Pick(loot, 0)", "Pick(loot, 4)"})
    void pickingOutsideTheTableIsAnError(String query) {
        assertEquals(11, error(query).getPosition());
    }

    @Test
    void dropLowestRemovesTheLowestValuesAndKeepsTheOrder() {
        assertEquals(List.of("Sword", "Shield"), rows("DropLowest(loot)"));
        assertEquals(List.of("Sword"), rows("DropLowest(loot, 2)"));
        assertEquals(List.of(), rows("DropLowest(loot, 10)"));
    }

    @Test
    void dropHighestRemovesTheHighestValues() {
        assertEquals(List.of("Shield", "Potion"), rows("DropHighest(loot)"));
    }

    @Test
    void droppingBreaksTiesAtRandom() {
        Set<List<String>> outcomes = new HashSet<>();
        for (int i = 0; i < 100; i++) {
            outcomes.add(rows("DropLowest(ties)"));
        }

        assertEquals(Set.of(List.of("B", "C"), List.of("A", "C")), outcomes);
    }

    @Test
    void rollingDiceGivesARowPerDieWorthTheFaceItLandedOn() {
        List<Thing> faces = things("Roll(d6, 50)");

        assertEquals(50, faces.size());
        for (Thing face : faces) {
            assertTrue(face.value() >= 1 && face.value() <= 6, face.toString());
            assertEquals(Integer.toString(face.value()), face.name());
        }
    }

    @Test
    void rollingSeveralDiceRollsEachOfThem() {
        assertEquals(4, things("Roll(4d6)").size());
        assertEquals(8, things("Roll(4d6, 2)").size());
    }

    @Test
    void diceUsedAsATableAreRolled() {
        assertEquals(3, number("Sum(3d1)"));
        for (int i = 0; i < 200; i++) {
            int total = number("Sum(DropLowest(4d6))");
            assertTrue(total >= 3 && total <= 18, "rolled " + total);
        }
    }

    @Test
    void diceUsedAsANumberAreRolledAndAddedUp() {
        assertEquals(3, rows("Roll(loot, 3d1)").size());
        for (int i = 0; i < 100; i++) {
            int size = rows("Roll(loot, d4)").size();
            assertTrue(size >= 1 && size <= 4, "rolled " + size);
        }
    }

    @Test
    void diceCanHaveAnyNumberOfSides() {
        int total = number("Sum(d2000000000)");

        assertTrue(total >= 1 && total <= 2_000_000_000, "rolled " + total);
    }

    @Test
    void diceOnTheirOwnAreNotRolled() {
        assertEquals("4d6", interpreter.run("4d6").display());
    }

    @Test
    void functionNamesIgnoreCase() {
        assertEquals(List.of("Sword"), rows("pick(loot, 1)"));
    }

    @Test
    void textCanNameATable() {
        assertEquals(List.of("Sword"), rows("Pick(\"loot\", 1)"));
    }

    @Test
    void textThatIsANumberCanBeUsedAsANumber() {
        assertEquals(3, rows("Roll(loot, \"3\")").size());
    }

    @Test
    void aTableWithOneRowCanBeUsedAsANumber() {
        // Potion is worth 1, so this picks row 1.
        assertEquals(List.of("Sword"), rows("Pick(loot, Pick(loot, 3))"));
    }

    @Test
    void aTableWithSeveralRowsIsNotANumber() {
        QueryException error = error("Roll(loot, Roll(d6, 2))");

        assertEquals(11, error.getPosition());
        assertTrue(error.getMessage().contains("Sum"), error.getMessage());
    }

    @Test
    void mistakesAreReportedBeforeAnythingIsRolled() {
        // Rolling -1 times would fail when run, but the text that isn't a number is caught first.
        assertEquals(21, error("Roll(Roll(loot, -1), \"abc\")").getPosition());
    }

    @ParameterizedTest
    @CsvSource(delimiter = '|', value = {
            "Roll(nope, 1)       | 5",
            "Roll(\"nope\", 1)   | 5",
            "Roll(Sum(loot), 1)  | 5",
            "Pick(Sum(4d6), 1)   | 5",
            "Roll(3)             | 5",
            "Frobnicate(loot)    | 0",
            "Roll()              | 0",
            "Pick(loot)          | 0",
            "Roll(loot, 1, 2)    | 0",
    })
    void rejectsQueriesThatCanNeverWork(String query, int position) {
        QueryException error = error(query);

        assertEquals(position, error.getPosition(), error.getMessage());
    }

    private List<Thing> things(String query) {
        return assertInstanceOf(TableValue.class, interpreter.run(query)).table().things();
    }

    private List<String> rows(String query) {
        return things(query).stream().map(Thing::name).toList();
    }

    private int number(String query) {
        return assertInstanceOf(NumberValue.class, interpreter.run(query)).value();
    }

    private QueryException error(String query) {
        return assertThrows(QueryException.class, () -> interpreter.run(query));
    }
}
