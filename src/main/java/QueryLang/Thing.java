package QueryLang;

public class Thing implements ITerm {
    private String name;
    private int value;
    private float chance;

    public Thing(String name, int value, float chance) {
        this.name = name;
        this.value = value;
        this.chance = chance;
    }

    public String getName() {
        return name;
    }
    public int getValue() {
        return value;
    }
    public float getChance() {
        return chance;
    }

    public void setName(String name) {
        this.name = name;
    }

    public void setValue(int value) {
        this.value = value;
    }

    public void setChance(float chance) {
        this.chance = chance;
    }
    
    @Override
    public String getType() {
        return "Thing";
    }

    @Override
    public String toString() {
        return String.format("name=%s; value=%d; chance=%f", name, value, chance);
    }

    @Override
    public String displayValue() {
        return String.format("%s (%d, %.2f)", name, value, chance);
    }
}
