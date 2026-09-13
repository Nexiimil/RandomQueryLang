package rql.table;

import java.util.ArrayList;
import java.util.List;

/**
 * Reads table rows of the form {@code name;value;chance}. The value and chance can be left out, or
 * left empty as in {@code Sword;;0.5}.
 */
public final class TableParser {
    private static final double EPSILON = 1e-9;

    private record Row(String name, Integer value, Double chance) {
    }

    private TableParser() {
    }

    public static Table parse(String tableName, List<String> lines) throws TableFormatException {
        List<Row> rows = new ArrayList<>();
        for (int i = 0; i < lines.size(); i++) {
            String line = lines.get(i).trim();
            if (!line.isEmpty()) {
                rows.add(parseRow(tableName, i + 1, line));
            }
        }

        double[] chances = resolveChances(tableName, rows);
        List<Thing> things = new ArrayList<>();
        for (int i = 0; i < rows.size(); i++) {
            Row row = rows.get(i);
            int value = row.value() != null ? row.value() : defaultValue(row.name(), i);
            things.add(new Thing(row.name(), value, chances[i]));
        }
        return new Table(tableName, things);
    }

    private static Row parseRow(String tableName, int lineNumber, String line)
            throws TableFormatException {
        String[] fields = line.split(";", -1);
        if (fields.length > 3) {
            throw new TableFormatException(tableName, lineNumber,
                    "expected at most 3 fields (name;value;chance) but found " + fields.length);
        }

        String name = fields[0].trim();
        if (name.isEmpty()) {
            throw new TableFormatException(tableName, lineNumber, "row has no name");
        }

        Integer value = null;
        if (fields.length > 1 && !fields[1].isBlank()) {
            String text = fields[1].trim();
            try {
                value = Integer.parseInt(text);
            } catch (NumberFormatException e) {
                throw new TableFormatException(tableName, lineNumber,
                        "value \"" + text + "\" is not a whole number");
            }
        }

        Double chance = null;
        if (fields.length > 2 && !fields[2].isBlank()) {
            String text = fields[2].trim();
            try {
                chance = Double.parseDouble(text);
            } catch (NumberFormatException e) {
                throw new TableFormatException(tableName, lineNumber,
                        "chance \"" + text + "\" is not a decimal number");
            }
            if (!(chance >= 0 && chance <= 1)) {
                throw new TableFormatException(tableName, lineNumber,
                        "chance must be between 0 and 1 but was " + text);
            }
        }

        return new Row(name, value, chance);
    }

    // Rows without a chance split whatever the rows with one leave over, equally.
    private static double[] resolveChances(String tableName, List<Row> rows)
            throws TableFormatException {
        double specifiedTotal = 0;
        int unspecifiedCount = 0;
        for (Row row : rows) {
            if (row.chance() == null) {
                unspecifiedCount++;
            } else {
                specifiedTotal += row.chance();
            }
        }

        if (specifiedTotal > 1 + EPSILON) {
            throw new TableFormatException(tableName,
                    String.format("chances add up to %.3f, which is more than 1", specifiedTotal));
        }
        double remainder = Math.max(0, 1 - specifiedTotal);
        if (unspecifiedCount > 0 && remainder <= EPSILON) {
            throw new TableFormatException(tableName,
                    "chances already add up to 1, so rows without a chance could never be rolled");
        }

        double[] chances = new double[rows.size()];
        for (int i = 0; i < rows.size(); i++) {
            Double chance = rows.get(i).chance();
            chances[i] = chance != null ? chance : remainder / unspecifiedCount;
        }
        return chances;
    }

    // A row named with a whole number is worth that number, so a table of 1 to 6 acts like a die.
    // Any other row is worth its position from the top of the table, starting at 1 like Pick does.
    private static int defaultValue(String name, int index) {
        try {
            return Integer.parseInt(name);
        } catch (NumberFormatException e) {
            return index + 1;
        }
    }
}
