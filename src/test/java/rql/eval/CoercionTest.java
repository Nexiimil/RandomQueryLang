package rql.eval;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.List;
import java.util.Map;
import java.util.Random;

import org.junit.jupiter.api.Test;

import rql.QueryException;
import rql.table.Table;
import rql.table.Thing;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Type;

// Text in a query today is always written in quotes, so the interpreter checks it before rolling. These
// cover text converted at runtime, for when functions start producing text of their own.
class CoercionTest {
    private final Environment environment = new Environment(Map.of(), new Random(0));

    @Test
    void textIsReadAsANumberWhenItIsOne() {
        assertEquals(new NumberValue(42), Coercion.convert(Type.NUMBER, new TextValue(" 42 "), environment, 0));
    }

    @Test
    void textThatIsNotANumberIsAnErrorAtItsPosition() {
        QueryException error = assertThrows(QueryException.class,
                () -> Coercion.convert(Type.NUMBER, new TextValue("Sword"), environment, 7));

        assertEquals(7, error.getPosition());
    }

    @Test
    void aTableWithOneRowCanBeUsedAsText() {
        Table table = new Table("loot", List.of(new Thing("Sword", 10, 1)));

        assertEquals(new TextValue("Sword"), Coercion.convert(Type.TEXT, new TableValue(table), environment, 0));
    }

    @Test
    void diceUsedAsANumberGiveTheirTotal() {
        assertEquals(new NumberValue(2), Coercion.convert(Type.NUMBER, new DiceValue(2, 1), environment, 0));
    }

    @Test
    void diceUsedAsATableGiveARowPerDie() {
        TableValue rolled = assertInstanceOf(TableValue.class,
                Coercion.convert(Type.TABLE, new DiceValue(3, 6), environment, 0));

        assertEquals(3, rolled.table().size());
    }
}
