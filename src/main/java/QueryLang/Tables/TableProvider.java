package QueryLang.Tables;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

public class TableProvider {
    public List<Table> loadTables() {
        List<Table> tables = new ArrayList<>();

        // List all RQLtable files in the current directory
        File dir = new File(".");
        File[] files = dir.listFiles((d, name) -> name.endsWith(".RQLtable"));

        if (files != null && files.length > 0) {
            for (File file : files) {
                try {
                    Table table = new Table(file.getName().replace(".RQLtable", ""));
                    table.loadFromFile(file);
                    tables.add(table);
                } catch (IOException e) {
                    System.out.println("Error loading table from file: " + file.getName());
                }
            }
        } else {
            System.out.println("No RQLtable files found.");
        }
        
        return tables;
    }
}