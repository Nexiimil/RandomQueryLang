package rql.table;

public class TableFormatException extends Exception {
    public TableFormatException(String tableName, int lineNumber, String message) {
        super(String.format("%s, line %d: %s", tableName, lineNumber, message));
    }

    public TableFormatException(String tableName, String message) {
        super(tableName + ": " + message);
    }
}
