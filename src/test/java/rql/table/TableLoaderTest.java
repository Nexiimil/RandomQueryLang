package rql.table;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

class TableLoaderTest {
    @TempDir
    Path directory;

    @Test
    void loadsTableFilesNamedAfterTheFile() throws Exception {
        Files.writeString(directory.resolve("loot.RQLtable"), "\uFEFFSword;10\r\nShield;5\r\n");
        Files.writeString(directory.resolve("notes.txt"), "not a table");

        List<Path> files = TableLoader.findTableFiles(directory);
        assertEquals(List.of(directory.resolve("loot.RQLtable")), files);

        Table table = TableLoader.load(files.getFirst());
        assertEquals("loot", table.name());
        assertEquals(List.of(new Row("Sword", 10, 0.5), new Row("Shield", 5, 0.5)), table.rows());
    }
}
