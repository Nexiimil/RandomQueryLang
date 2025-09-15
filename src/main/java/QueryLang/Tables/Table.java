package QueryLang.Tables;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

import QueryLang.ITerm;

public class Table implements ITerm{
    private String name;
    private List<IPredicate> baseEntries;
    private List<IPredicate> tempEntries;

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

    public List<IPredicate> getBaseEntries() {
        return baseEntries;
    }
    
    public void addBaseEntry(String entry) {
        baseEntries.add(PredicateFactory.ProcessQuery(entry));
    }

    public List<IPredicate> getTemporaryEntries() {
        return tempEntries;
    }

    public void addTemporaryEntry(IPredicate entry) {
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