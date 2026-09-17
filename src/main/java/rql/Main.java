package rql;

import java.io.IOException;
import java.nio.file.Path;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Scanner;
import java.util.random.RandomGenerator;

import rql.eval.Interpreter;
import rql.node.Context;
import rql.table.Table;
import rql.table.TableFormatException;
import rql.table.TableLoader;

public class Main {
    public static void main(String[] args) {
        Path directory = Path.of(args.length > 0 ? args[0] : ".");
        Map<String, Table> tables = loadTables(directory);

        System.out.println("This is RandomQueryLang!");
        if (tables.isEmpty()) {
            System.out.println("No " + TableLoader.EXTENSION + " files found in "
                    + directory.toAbsolutePath().normalize() + ".");
        } else {
            System.out.println("You have the following tables available from this directory:");
            for (String name : tables.keySet()) {
                System.out.println(" - " + name);
            }
        }
        System.out.println("Dice are written like d20 or 4d6.");
        System.out.println("Enter a query, e.g. Sum(DropLowest(4d6)), or exit to quit.");

        Interpreter interpreter = new Interpreter(new Context(tables, RandomGenerator.getDefault()));
        Scanner scanner = new Scanner(System.in);
        while (true) {
            System.out.print("> ");
            if (!scanner.hasNextLine()) {
                System.out.println();
                break;
            }
            String query = scanner.nextLine().trim();
            if (query.isEmpty()) {
                continue;
            }
            if (query.equalsIgnoreCase("exit") || query.equalsIgnoreCase("quit")) {
                break;
            }

            try {
                System.out.println(interpreter.run(query).display());
            } catch (QueryException e) {
                System.out.println(query);
                System.out.println(" ".repeat(e.getPosition()) + "^");
                System.out.println("Error: " + e.getMessage());
            }
        }
        scanner.close();
    }

    private static Map<String, Table> loadTables(Path directory) {
        Map<String, Table> tables = new LinkedHashMap<>();
        List<Path> files;
        try {
            files = TableLoader.findTableFiles(directory);
        } catch (IOException e) {
            System.err.println("Could not read " + directory + ": " + e.getMessage());
            return tables;
        }

        for (Path file : files) {
            try {
                Table table = TableLoader.load(file);
                tables.put(table.name(), table);
            } catch (IOException e) {
                System.err.println("Could not read " + file.getFileName() + ": " + e.getMessage());
            } catch (TableFormatException e) {
                System.err.println("Skipping table " + e.getMessage());
            }
        }
        return tables;
    }
}
