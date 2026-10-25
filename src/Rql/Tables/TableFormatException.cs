namespace Rql.Tables;

/// <summary>A table that can't be rolled on as written, saying which table and, where it can, which row.</summary>
public sealed class TableFormatException : Exception
{
    public TableFormatException(string tableName, string message)
        : base($"{tableName}: {message}")
    {
        TableName = tableName;
        Problem = message;
    }

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

    public string TableName { get; }

    /// <summary>The message without the table or row in front of it, for showing beside the row.</summary>
    public string Problem { get; }

    public int? Row { get; }

    public int? Line { get; }
}
