package rql.syntax;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.List;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

import rql.QueryException;
import rql.syntax.Expr.Call;
import rql.syntax.Expr.DiceLiteral;
import rql.syntax.Expr.Identifier;
import rql.syntax.Expr.NumberLiteral;
import rql.syntax.Expr.TextLiteral;

class ParserTest {
    @Test
    void parsesNestedCalls() {
        Expr expected = new Call("Sum", List.of(
                new Call("DropLowest", List.of(
                        new Call("Roll", List.of(new Identifier("loot", 20), new NumberLiteral(4, 26)), 15)),
                        4)),
                0);

        assertEquals(expected, Parser.parse("Sum(DropLowest(Roll(loot, 4)))"));
    }

    @Test
    void parsesDice() {
        assertEquals(new DiceLiteral(1, 20, 0), Parser.parse("d20"));
        assertEquals(new DiceLiteral(4, 6, 0), Parser.parse("4D6"));
        assertEquals(new Call("Roll", List.of(new DiceLiteral(3, 8, 5)), 0), Parser.parse("Roll(3d8)"));
    }

    @Test
    void wordsThatOnlyStartLikeDiceAreTableNames() {
        assertEquals(new Identifier("dragons", 0), Parser.parse("dragons"));
        assertEquals(new Identifier("d6x", 0), Parser.parse("d6x"));
    }

    @Test
    void parsesTextWithEscapes() {
        assertEquals(new TextLiteral("My \"Big\" Table\\", 0), Parser.parse("\"My \\\"Big\\\" Table\\\\\""));
    }

    @Test
    void parsesNegativeNumbersAndCallsWithoutArguments() {
        assertEquals(new NumberLiteral(-3, 0), Parser.parse("-3"));
        assertEquals(new Call("Roll", List.of(), 0), Parser.parse("Roll()"));
    }

    @ParameterizedTest
    @CsvSource(delimiter = '|', value = {
            "Roll(d6, 4       | 10",
            "Roll(d6,, 4)     | 8",
            "Roll(d6) extra   | 9",
            "4dragons         | 1",
            "Roll(\"abc       | 5",
            "Roll(d6 # 2)     | 8",
            "Roll(\"\\n\")    | 6",
            "99999999999      | 0",
            "Roll(d0)         | 5",
            "Roll(0d6)        | 5",
            "Roll(-2d6)       | 5",
            "d99999999999     | 0",
    })
    void reportsWhereTheQueryIsBroken(String query, int position) {
        QueryException error = assertThrows(QueryException.class, () -> Parser.parse(query));

        assertEquals(position, error.getPosition(), error.getMessage());
    }

    @Test
    void anEmptyQueryIsAnError() {
        assertThrows(QueryException.class, () -> Parser.parse(""));
    }
}
