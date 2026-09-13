package rql.table;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.stream.Stream;

public final class TableLoader {
    public static final String EXTENSION = ".RQLtable";

    private static final String BYTE_ORDER_MARK = "\uFEFF";

    private TableLoader() {
    }

    public static List<Path> findTableFiles(Path directory) throws IOException {
        try (Stream<Path> files = Files.list(directory)) {
            return files
                    .filter(file -> file.getFileName().toString().endsWith(EXTENSION))
                    .filter(Files::isRegularFile)
                    .sorted()
                    .toList();
        }
    }

    public static Table load(Path file) throws IOException, TableFormatException {
        String fileName = file.getFileName().toString();
        String tableName = fileName.substring(0, fileName.length() - EXTENSION.length());

        List<String> lines = Files.readAllLines(file);
        // Some Windows editors start UTF-8 files with a byte order mark, which would end up in the
        // first row's name.
        if (!lines.isEmpty() && lines.getFirst().startsWith(BYTE_ORDER_MARK)) {
            lines.set(0, lines.getFirst().substring(BYTE_ORDER_MARK.length()));
        }
        return TableParser.parse(tableName, lines);
    }
}
