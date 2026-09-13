package rql.value;

public record TextValue(String value) implements Value {
    @Override
    public Type type() {
        return Type.TEXT;
    }

    @Override
    public String display() {
        return value;
    }
}
