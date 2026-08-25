package QueryLang.Predicates;
import QueryLang.Tables.Table;
import QueryLang.Thing;

import java.util.List;

class RollTable implements IThingPredicate {

    private Table table;

    public RollTable(Table table) {
        this.table = table;
    }

    @Override
    public Thing execute() {
        Thing result;

        List<Thing> entries = table.getAllEntries();
        if (entries.isEmpty()) {
            result = new Thing("No entries in table: " + table.getName());
        } else {
            int baseEntriesNormalisedChance = entries.stream()
                    .mapToInt(x -> (int) (x.getChance() * 10))
                    .sum();
            result = entries.get(randomIndex);
        }
        return result;
    }

    @Override
    public String getSymbol() {
        return "";
    }

    @Override
    public String getPredicateName() {
        return "RollTable";
    }

    @Override
    public String getType() {
        return "ThingPredicate";
    }

    @Override
    public String displayValue() {
        return "";
    }
}