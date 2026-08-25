package QueryLang.Tables;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

import QueryLang.ITerm;
import QueryLang.Predicates.IPredicate;
import QueryLang.Predicates.PredicateFactory;
import QueryLang.Thing;

public class Table implements ITerm{
    private String name;
    private List<Thing> baseEntries;
    private List<Thing> tempEntries;

    @Override
    public String getType() {
        return "Table";
    }

    @Override
    public String displayValue() {
        return name;
    }

    public Table(String name) {
        this.name = name;
        this.baseEntries = new ArrayList<>();
        this.tempEntries = new ArrayList<>();
    }

    public String getName() {
        return name;
    }

    public List<Thing> getBaseEntries() {
        return baseEntries;
    }

    public List<Thing> getNormalisedBaseEntries() {
        List<Thing> normalisedBaseEntries = new ArrayList<Thing>();
        normalisedBaseEntries.addAll(this.getBaseEntries());
        int totalChance = 0;
        for(Thing thing : normalisedBaseEntries)
        {
            totalChance += thing.getChance();
        }
        return baseEntries;
    }

    public List<Thing> getTemporaryEntries() {
        return tempEntries;
    }

    public List<Thing> getAllEntries() {
        List<Thing> allEntries = new ArrayList<>(baseEntries);
        allEntries.addAll(tempEntries);
        return allEntries;
    }

    public List<Thing> getAllEntriesWithNormalisedChance() {
        List<Thing> allEntries = new ArrayList<>(baseEntries);
        allEntries.addAll(tempEntries);
        return allEntries;
    }

    public void addBaseEntry(String entry) {
        baseEntries.add(PredicateFactory.ProcessQuery(entry));
    }

    public void addTemporaryEntry(Thing entry) {
        tempEntries.add(entry);
    }

    public void loadFromFile(File file) throws IOException {
        // Load table structure from file
        try (BufferedReader reader = new BufferedReader(new FileReader(file))) {
            String line;
            while ((line = reader.readLine()) != null) {
                // Assume each line is a column name
                addBaseEntry(line.trim());
            }
        }
    }
}