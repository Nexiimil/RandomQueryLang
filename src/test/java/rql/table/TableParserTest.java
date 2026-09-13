package rql.table;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class TableParserTest {
    @Test
    void readsNameValueAndChance() throws TableFormatException {
        Table table = TableParser.parse("loot", List.of("Sword;10;0.25", "Shield;5;0.75"));

        assertEquals("loot", table.name());
        assertEquals(List.of(new Thing("Sword", 10, 0.25), new Thing("Shield", 5, 0.75)), table.things());
    }

    @Test
    void valueDefaultsToPositionFromTheTopStartingAtOne() throws TableFormatException {
        Table table = TableParser.parse("monsters", List.of("Goblin", "Orc", "Troll"));

        assertEquals(List.of(1, 2, 3), values(table));
    }

    @Test
    void valueDefaultsToTheNameWhenTheNameIsAWholeNumber() throws TableFormatException {
        Table table = TableParser.parse("cards", List.of("4", "7", "Joker"));

        assertEquals(List.of(4, 7, 3), values(table));
    }

    @Test
    void rowsWithoutAChanceShareWhatIsLeftOver() throws TableFormatException {
        Table table = TableParser.parse("loot", List.of("Sword;;0.5", "Shield", "Potion"));

        assertEquals(List.of(0.5, 0.25, 0.25), chances(table));
    }

    @Test
    void rowsShareChanceEquallyWhenNoneIsGiven() throws TableFormatException {
        Table table = TableParser.parse("letters", List.of("A", "B", "C", "D"));

        assertEquals(List.of(0.25, 0.25, 0.25, 0.25), chances(table));
    }

    @Test
    void skipsBlankLinesAndTrimsFields() throws TableFormatException {
        Table table = TableParser.parse("loot", List.of(" Sword ; 3 ; 0.5 ", "", "   ", "Shield"));

        assertEquals(List.of(new Thing("Sword", 3, 0.5), new Thing("Shield", 2, 0.5)), table.things());
    }

    @Test
    void reportsTheLineOfABadRow() {
        TableFormatException error = assertThrows(TableFormatException.class,
                () -> TableParser.parse("loot", List.of("Sword;1", "", "Shield;heavy")));

        assertTrue(error.getMessage().contains("line 3"), error.getMessage());
    }

    @ParameterizedTest
    @ValueSource(strings = {"Sword;1;0.5;extra", ";1", "Sword;1.5", "Sword;1;half", "Sword;1;1.5", "Sword;1;-0.1"})
    void rejectsMalformedRows(String line) {
        assertThrows(TableFormatException.class, () -> TableParser.parse("loot", List.of(line)));
    }

    @Test
    void rejectsChancesThatAddUpToMoreThanOne() {
        assertThrows(TableFormatException.class,
                () -> TableParser.parse("loot", List.of("Sword;;0.6", "Shield;;0.6")));
    }

    @Test
    void rejectsRowsThatCouldNeverBeRolled() {
        assertThrows(TableFormatException.class,
                () -> TableParser.parse("loot", List.of("Sword;;0.5", "Shield;;0.5", "Potion")));
    }

    private static List<Integer> values(Table table) {
        return table.things().stream().map(Thing::value).toList();
    }

    private static List<Double> chances(Table table) {
        return table.things().stream().map(Thing::chance).toList();
    }
}
