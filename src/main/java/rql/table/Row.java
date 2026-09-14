package rql.table;

public record Row(String name, int value, double chance) {
    /**
     * The value of a row that wasn't given one. A row named with a whole number is worth that number, so a
     * table of 1 to 6 acts like a die. Any other row is worth its position from the top of the table,
     * starting at 1 like Pick does.
     */
    public static int defaultValue(String name, int position) {
        try {
            return Integer.parseInt(name);
        } catch (NumberFormatException e) {
            return position;
        }
    }
}
