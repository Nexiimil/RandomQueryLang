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
import rql.table.Row;
import rql.table.Table;
import rql.table.TableFormatException;
import rql.table.TableParser;
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
                "ties", TableParser.parse("ties", List.of("A;1", "B;1", "C;2")),
                "classes", TableParser.parse("classes", List.of("Fighter;10", "Wizard;6")),
                "Fighter Skills", TableParser.parse("Fighter Skills", List.of("Athletics", "Intimidation", "Survival")),
                "Wizard Skills", TableParser.parse("Wizard Skills", List.of("Arcana", "History", "Investigation")));
        interpreter = new Interpreter(new Environment(tables, new Random(1234)));
    }

    @Test
    void rollPicksTheGivenNumberOfRowsFromTheTable() {
        List<String> rolled = names("Roll(loot, 5)");

        assertEquals(5, rolled.size());
        assertTrue(List.of("Sword", "Shield", "Potion").containsAll(rolled), rolled.toString());
    }

    @Test
    void rollRollsOnceByDefault() {
        assertEquals(1, names("Roll(loot)").size());
    }

    @Test
    void rollNeverPicksARowWithNoChance() {
        assertEquals(Set.of("Always"), Set.copyOf(names("Roll(weighted, 1000)")));
    }

    @Test
    void rollFollowsTheChanceOfEachRow() {
        long timesA = names("Roll(mostly, 10000)").stream().filter("A"::equals).count();

        assertTrue(timesA > 8700 && timesA < 9300, "A was rolled " + timesA + " times out of 10000");
    }

    @Test
    void pickCountsRowsFromTheTopStartingAtOne() {
        assertEquals(List.of("Sword"), names("Pick(loot, 1)"));
        assertEquals(List.of("Potion"), names("Pick(loot, 3)"));
    }

    @ParameterizedTest
    @ValueSource(strings = {"Pick(loot, 0)", "Pick(loot, 4)"})
    void pickingOutsideTheTableIsAnError(String query) {
        assertEquals(11, error(query).getPosition());
    }

    @Test
    void dropLowestRemovesTheLowestValuesAndKeepsTheOrder() {
        assertEquals(List.of("Sword", "Shield"), names("DropLowest(loot)"));
        assertEquals(List.of("Sword"), names("DropLowest(loot, 2)"));
        assertEquals(List.of(), names("DropLowest(loot, 10)"));
    }

    @Test
    void dropHighestRemovesTheHighestValues() {
        assertEquals(List.of("Shield", "Potion"), names("DropHighest(loot)"));
    }

    @Test
    void droppingBreaksTiesAtRandom() {
        Set<List<String>> outcomes = new HashSet<>();
        for (int i = 0; i < 100; i++) {
            outcomes.add(names("DropLowest(ties)"));
        }

        assertEquals(Set.of(List.of("B", "C"), List.of("A", "C")), outcomes);
    }

    @Test
    void rollingDiceGivesARowPerDieWorthTheFaceItLandedOn() {
        List<Row> faces = rows("Roll(d6, 50)");

        assertEquals(50, faces.size());
        for (Row face : faces) {
            assertTrue(face.value() >= 1 && face.value() <= 6, face.toString());
            assertEquals(Integer.toString(face.value()), face.name());
        }
    }

    @Test
    void rollingSeveralDiceRollsEachOfThem() {
        assertEquals(4, rows("Roll(4d6)").size());
        assertEquals(8, rows("Roll(4d6, 2)").size());
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
        assertEquals(3, names("Roll(loot, 3d1)").size());
        for (int i = 0; i < 100; i++) {
            int size = names("Roll(loot, d4)").size();
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
        assertEquals(List.of("Sword"), names("pick(loot, 1)"));
    }

    @Test
    void textCanNameATable() {
        assertEquals(List.of("Sword"), names("Pick(\"loot\", 1)"));
    }

    @Test
    void textThatIsANumberCanBeUsedAsANumber() {
        assertEquals(3, names("Roll(loot, \"3\")").size());
    }

    @Test
    void aTableWithOneRowCanBeUsedAsANumber() {
        // Potion is worth 1, so this picks row 1.
        assertEquals(List.of("Sword"), names("Pick(loot, Pick(loot, 3))"));
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

    @Test
    void drawNeverPicksTheSameRowTwice() {
        for (int i = 0; i < 20; i++) {
            List<String> drawn = names("Draw(loot, 3)");

            assertEquals(3, drawn.size());
            assertEquals(Set.of("Sword", "Shield", "Potion"), Set.copyOf(drawn));
        }
        assertEquals(1, names("Draw(loot)").size());
    }

    @Test
    void drawingMoreRowsThanCanBeRolledIsAnError() {
        assertEquals(List.of("Always"), names("Draw(weighted)"));
        assertEquals(15, error("Draw(weighted, 2)").getPosition());
    }

    @Test
    void keepLowestAndKeepHighestKeepTheOrder() {
        assertEquals(List.of("Sword"), names("KeepHighest(loot)"));
        assertEquals(List.of("Shield", "Potion"), names("KeepLowest(loot, 2)"));
        assertEquals(List.of("Sword", "Shield", "Potion"), names("KeepHighest(loot, 10)"));
    }

    @Test
    void keepingBreaksTiesAtRandom() {
        Set<List<String>> outcomes = new HashSet<>();
        for (int i = 0; i < 100; i++) {
            outcomes.add(names("KeepLowest(ties)"));
        }

        assertEquals(Set.of(List.of("A"), List.of("B")), outcomes);
    }

    @Test
    void countGivesTheNumberOfRows() {
        assertEquals(3, number("Count(loot)"));
        assertEquals(0, number("Count(DropLowest(loot, 10))"));
    }

    @Test
    void addSubtractAndMultiplyWorkOnNumbersAndDice() {
        assertEquals(5, number("Add(2, 3)"));
        assertEquals(-3, number("Subtract(2, 5)"));
        assertEquals(30, number("Multiply(3d1, 10)"));
        assertEquals(4, error("Add(2000000000, 2000000000)").getPosition());
    }

    @Test
    void divideRoundsDown() {
        assertEquals(3, number("Divide(7, 2)"));
        assertEquals(-1, number("Divide(-1, 2)"));
        assertEquals(10, error("Divide(1, 0)").getPosition());
    }

    @Test
    void diceCanBeMadeFromNumbers() {
        assertEquals(3, number("Sum(Dice(3, 1))"));
        assertEquals(6, number("Sum(Dice(Pick(classes, 2), 1))"));
        assertEquals(5, error("Dice(0, 6)").getPosition());
    }

    @Test
    void repeatRunsItsQueryAgainEachTime() {
        List<Row> scores = rows("Repeat(Sum(DropLowest(4d6)), 6)");

        assertEquals(6, scores.size());
        for (Row score : scores) {
            assertTrue(score.value() >= 3 && score.value() <= 18, score.toString());
        }
        assertTrue(Set.copyOf(names("Repeat(Sum(d1000000), 20)")).size() > 1);
        assertEquals(List.of("Sword", "Sword"), names("Repeat(Pick(loot, 1), 2)"));
        assertEquals(List.of(), names("Repeat(Roll(loot), 0)"));
    }

    @Test
    void letUsesTheSameRollEverywhereItsNameIsUsed() {
        assertEquals(0, number("Let(x, Sum(d1000000), Subtract(x, x))"));
        // Dice given to Let are rolled once, rather than every time they're used.
        assertEquals(0, number("Let(x, 4d1000000, Subtract(Sum(x), Sum(x)))"));
        // Repeat runs the whole Let again, so each repeat gets a new roll.
        assertTrue(Set.copyOf(names("Repeat(Let(x, Sum(d1000000), x), 20)")).size() > 1);
    }

    @Test
    void aNameGivenByLetHidesATableWithTheSameName() {
        assertEquals(6, number("Let(loot, 5, Add(loot, 1))"));
    }

    @Test
    void labelNamesResults() {
        assertEquals(List.of(new Row("Gold", 5, 1)), rows("Label(\"Gold\", 5)"));
        assertEquals(List.of(new Row("Gold", 3, 1)), rows("Label(\"Gold\", 3d1)"));
        assertEquals(List.of("Item: Sword", "Item: Shield"), names("Label(\"Item\", DropLowest(loot))"));
        assertEquals(List.of("Note: hi"), names("Label(\"Note\", \"hi\")"));
    }

    @Test
    void nameAndConcatMakeText() {
        assertEquals("Shield", interpreter.run("Name(Pick(loot, 2))").display());
        assertEquals("a1Sword", interpreter.run("Concat(\"a\", 1, Pick(loot, 1))").display());
        assertEquals(List.of("Sword"), names("Pick(Concat(\"lo\", \"ot\"), 1)"));
    }

    @Test
    void joinPutsTablesTogetherInOrder() {
        assertEquals(List.of("Sword", "Potion", "Sword", "Shield", "Potion"),
                names("Join(Pick(loot, 1), Pick(loot, 3), loot)"));

        String message = error("Join()").getMessage();
        assertTrue(message.contains("Join(table, [more...])"), message);
    }

    @Test
    void rollsACharacter() {
        List<Row> sheet = rows("""
                Let(class, Roll(classes), Let(con, Sum(DropLowest(4d6)), Join(
                    Label("Constitution", con),
                    Label("Class", class),
                    Label("Hit points", Add(class, Divide(Subtract(con, 10), 2))),
                    Label("Skill", Draw(Concat(class, " Skills"), 2)),
                    Label("Other scores", Repeat(Sum(DropLowest(4d6)), 5)))))
                """);

        assertEquals(10, sheet.size());
        Row constitution = sheet.get(0);
        Row characterClass = sheet.get(1);
        assertEquals(characterClass.value() + Math.floorDiv(constitution.value() - 10, 2), sheet.get(2).value());
        List<String> skills = characterClass.name().equals("Class: Fighter")
                ? List.of("Skill: Athletics", "Skill: Intimidation", "Skill: Survival")
                : List.of("Skill: Arcana", "Skill: History", "Skill: Investigation");
        assertTrue(skills.containsAll(List.of(sheet.get(3).name(), sheet.get(4).name())), sheet.toString());
        assertTrue(sheet.subList(5, 10).stream().allMatch(row -> row.name().startsWith("Other scores: ")));
    }

    @ParameterizedTest
    @CsvSource(delimiter = '|', value = {
            "Let(1, 2, 3)                              | 4",
            "Let(x, 1)                                 | 0",
            "Let(x, 5, Roll(x))                        | 15",
            "Add(Let(x, 1, x), x)                      | 18",
            "Let(x, Roll(loot, -1), Roll(x, \"abc\"))  | 31",
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

    private List<Row> rows(String query) {
        return assertInstanceOf(TableValue.class, interpreter.run(query)).table().rows();
    }

    private List<String> names(String query) {
        return rows(query).stream().map(Row::name).toList();
    }

    private int number(String query) {
        return assertInstanceOf(NumberValue.class, interpreter.run(query)).value();
    }

    private QueryException error(String query) {
        return assertThrows(QueryException.class, () -> interpreter.run(query));
    }
}
