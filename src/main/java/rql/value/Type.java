package rql.value;

public enum Type {
    NUMBER("a number"),
    TEXT("text"),
    TABLE("a table"),
    DICE("dice");

    private final String description;

    Type(String description) {
        this.description = description;
    }

    public String description() {
        return description;
    }
}
