namespace Rql.Tables;

/// <summary>A table that can't be rolled on as written, saying which table and, where it can, which row.</summary>
public sealed class TableFormatException : Exception
{
    /// <summary>A problem with the table as a whole.</summary>
    /// <param name="tableName">The table at fault.</param>
    /// <param name="message">What's wrong with it.</param>
    public TableFormatException(string tableName, string message)
        : base($"{tableName}: {message}")
    {
        TableName = tableName;
        Problem = message;
    }

    /// <summary>A problem with one row of the table.</summary>
    /// <param name="tableName">The table at fault.</param>
    /// <param name="message">What's wrong with the row.</param>
    /// <param name="row">Which row of the table is at fault, counting from 1.</param>
    /// <param name="line">Which line of the file it was read from, when it was read from one.</param>
    public TableFormatException(string tableName, string message, int row, int? line = null)
        : base($"{tableName}, {(line is null ? "row " + row : "line " + line)}: {message}")
    {
        TableName = tableName;
        Problem = message;
        Row = row;
        Line = line;
    }

    /// <summary>The table at fault.</summary>
    public string TableName { get; }

    /// <summary>The message without the table or row in front of it, for showing beside the row.</summary>
    public string Problem { get; }

    /// <summary>Which row is at fault, counting from 1, or null when it's the table as a whole.</summary>
    public int? Row { get; }

    /// <summary>Which line of the file the row was read from, or null when it wasn't read from one.</summary>
    public int? Line { get; }
}
