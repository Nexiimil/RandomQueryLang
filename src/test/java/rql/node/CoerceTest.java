package rql.node;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.Map;
import java.util.Random;

import org.junit.jupiter.api.Test;

import rql.table.Table;
import rql.table.Thing;

// Text in a query today is always written in quotes, so a query's own text is settled before anything
// rolls. These cover text converted while running, for when functions start producing text of their own.
class CoerceTest {
    private final Table loot = new Table("loot", List.of(new Thing("Sword", 10, 1)));
    private final Context context = new Context(Map.of("loot", loot), new Random(0));

    @Test
    void aValueAlreadyOfTheRightTypeIsLeftAlone() {
        Node<Integer> already = Literal.of(4);

        assertSame(already, Coerce.to(ValueType.NUMBER, already));
    }

    @Test
    void textIsReadAsANumberWhenItIsOne() {
        Node<Integer> converted = Coerce.to(ValueType.NUMBER, Literal.of(" 42 "));

        assertEquals(42, converted.evaluate(context));
    }

    @Test
    void textThatIsNotANumberBlamesTheTextItself() {
        Literal<String> text = Literal.of("Sword");

        EvalException error = assertThrows(EvalException.class,
                () -> Coerce.to(ValueType.NUMBER, text).evaluate(context));

        assertSame(text, error.blame());
    }

    @Test
    void aTableWithOneRowCanBeUsedAsText() {
        assertEquals("Sword", Coerce.to(ValueType.TEXT, Literal.of(loot)).evaluate(context));
    }

    @Test
    void aTableWithSeveralRowsSuggestsSum() {
        Table several = new Table("several", List.of(new Thing("A", 1, 0.5), new Thing("B", 2, 0.5)));

        EvalException error = assertThrows(EvalException.class,
                () -> Coerce.to(ValueType.NUMBER, Literal.of(several)).evaluate(context));

        assertTrue(error.getMessage().contains("Sum"), error.getMessage());
    }

    @Test
    void textCanNameATable() {
        assertEquals(loot, Coerce.to(ValueType.TABLE, Literal.of("loot")).evaluate(context));
    }

    @Test
    void textNamingNoTableIsAnError() {
        assertThrows(EvalException.class,
                () -> Coerce.to(ValueType.TABLE, Literal.of("nope")).evaluate(context));
    }

    @Test
    void diceUsedAsANumberGiveTheirTotal() {
        assertEquals(2, Coerce.to(ValueType.NUMBER, Literal.of(new Dice(2, 1))).evaluate(context));
    }

    @Test
    void diceUsedAsATableGiveARowPerDie() {
        Node<Table> rolled = Coerce.to(ValueType.TABLE, Literal.of(new Dice(3, 6)));

        assertEquals(3, rolled.evaluate(context).size());
    }

    @Test
    void convertingDiceCountsAsRollingSoItIsNeverSettledEarly() {
        assertTrue(Coerce.to(ValueType.TABLE, Literal.of(new Dice(3, 6))).rolls());
        assertTrue(Coerce.to(ValueType.NUMBER, Literal.of(new Dice(3, 6))).rolls());
        assertFalse(Coerce.to(ValueType.NUMBER, Literal.of("3")).rolls());
    }

    @Test
    void aNumberIsNeverATable() {
        assertFalse(Coerce.possible(ValueType.NUMBER, ValueType.TABLE));
        assertTrue(Coerce.possible(ValueType.TEXT, ValueType.TABLE));
    }

    @Test
    void nothingBecomesDiceExceptDice() {
        assertFalse(Coerce.possible(ValueType.NUMBER, ValueType.DICE));
        assertTrue(Coerce.possible(ValueType.DICE, ValueType.DICE));
    }

    @Test
    void conversionsKeepTheirSourceAsAChild() {
        Literal<String> text = Literal.of("3");
        Node<Integer> converted = Coerce.to(ValueType.NUMBER, text);

        assertInstanceOf(Coerce.TextToNumber.class, converted);
        assertEquals(List.of(text), converted.children());
    }
}
