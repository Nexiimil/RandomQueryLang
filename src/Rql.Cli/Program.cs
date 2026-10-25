using Rql;
using Rql.Evaluation;
using Rql.Nodes;
using Rql.Projects;
using Rql.Tables;

if (args.Length > 0 && args[0] == "import")
{
    return Import(args[1..]);
}

var project = new Project(args.Length > 0 ? args[0] : ".");
LoadedTables loaded = project.Tables.LoadAll();
foreach (TableProblem problem in loaded.Problems)
{
    Console.Error.WriteLine($"Skipping table {problem.Name}: {problem.Message}");
}

IReadOnlyList<SavedQuery> saved = [];
try
{
    saved = project.Queries.All();
}
catch (InvalidDataException e)
{
    Console.Error.WriteLine(e.Message);
}

Console.WriteLine("This is RandomQueryLang!");
if (loaded.Tables.Count == 0)
{
    Console.WriteLine($"No {TableStore.Extension} files found in {project.Directory}.");
}
else
{
    Console.WriteLine("You have the following tables available from this directory:");
    foreach (string name in loaded.Tables.Keys.Order(StringComparer.Ordinal))
    {
        string shortest = loaded.Tables.ShortestName(name);
        Console.WriteLine(shortest == name ? $" - {name}" : $" - {name} (or {shortest})");
    }
}
if (saved.Count > 0)
{
    Console.WriteLine("Saved queries, run with @ and their name:");
    foreach (SavedQuery query in saved)
    {
        Console.WriteLine($" - @{query.Name}: {query.Query}");
    }
}
Console.WriteLine("Dice are written in Roll20's notation, like d20, 4d6kh3 or {d20, d20}kh1, and several rolls can be made at once: Attack: d20 + 5; Damage: 2d6 + 3");
Console.WriteLine("A saved query can be rolled with #Name, or @Name on its own.");
Console.WriteLine("Enter a query, e.g. Sum(DropLowest(4d6)), or exit to quit.");

var interpreter = new Interpreter(new Context(loaded.Tables, Random.Shared))
{
    Macros = saved.GroupBy(query => query.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(same => same.Key, same => same.First().Query),
};
var answers = new Dictionary<string, string>();
while (true)
{
    Console.Write("> ");
    string? line = Console.ReadLine();
    if (line is null)
    {
        Console.WriteLine();
        break;
    }
    string query = line.Trim();
    if (query.Length == 0)
    {
        continue;
    }
    if (query.Equals("exit", StringComparison.OrdinalIgnoreCase) || query.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }
    if (query.StartsWith('@') && !query.StartsWith("@{"))
    {
        string name = query[1..].Trim();
        SavedQuery? found = saved.FirstOrDefault(other => string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            Console.WriteLine($"Error: no saved query named \"{name}\"");
            continue;
        }
        query = found.Query;
        Console.WriteLine(query);
    }

    try
    {
        Ask(interpreter, query, answers);
        Show(interpreter.Run(query, answers));
    }
    catch (QueryException e)
    {
        Console.WriteLine(query);
        Console.WriteLine(new string(' ', Math.Max(0, e.Position)) + "^");
        Console.WriteLine($"Error: {e.Message}");
    }
}

return 0;

// Asks for anything the query needs: roll queries, and values Roll20 would read from a character sheet.
// An answer can hold another question, so this goes round until nothing new is asked. Answers are kept
// for next time, offered as the default.
static void Ask(Interpreter interpreter, string query, Dictionary<string, string> answers)
{
    var asked = new HashSet<string>();
    while (interpreter.Prompts(query, answers).Where(prompt => asked.Add(prompt.Key)).ToList() is { Count: > 0 } prompts)
    {
        foreach (RollPrompt prompt in prompts)
        {
            string? fallback = answers.GetValueOrDefault(prompt.Key) ?? prompt.Default;
            if (prompt.Options.Count > 0)
            {
                Console.WriteLine($"{prompt.Question}:");
                for (int i = 0; i < prompt.Options.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {prompt.Options[i].Label}{(prompt.Options[i].Value == fallback ? " (default)" : "")}");
                }
                Console.Write("? ");
                string? picked = Console.ReadLine()?.Trim();
                answers[prompt.Key] = int.TryParse(picked, out int number) && number >= 1 && number <= prompt.Options.Count
                    ? prompt.Options[number - 1].Value
                    : fallback ?? prompt.Options[0].Value;
            }
            else
            {
                Console.Write(fallback is null ? $"{prompt.Question}? " : $"{prompt.Question} [{fallback}]? ");
                string? typed = Console.ReadLine();
                answers[prompt.Key] = string.IsNullOrWhiteSpace(typed) ? fallback ?? "0" : typed.Trim();
            }
        }
    }
}

// Each roll's result, then what went into it: inline rolls, dice, groups and labelled terms, a few at most
// so a big roll doesn't scroll away.
static void Show(QueryResult result)
{
    const int Shown = 10;
    bool several = result.Results.Count > 1;
    for (int i = 0; i < result.Results.Count; i++)
    {
        Result part = result.Results[i];
        string value = part.Display();
        string? label = part.Label ?? (several ? $"#{i + 1}" : null);
        Console.WriteLine(label is null ? value : value.Contains('\n') ? $"{label}:\n{value}" : $"{label}: {value}");

        var details = new List<string>();
        foreach (InlineRoll inline in part.Inline)
        {
            details.Add($"[[{inline.Text}]] = {Numbers.Display(inline.Value)}");
            details.AddRange(inline.Rolls.Select(roll => "  " + roll.Describe()));
        }
        details.AddRange(part.Rolls.Select(roll => roll.Describe()));
        details.AddRange(part.Groups.Select(group => group.Describe()));
        if (part.Labels.Count > 0)
        {
            details.Add(string.Join(", ", part.Labels.Select(labelled => $"{labelled.Label}: {labelled.Value}")));
        }
        foreach (string detail in details.Take(Shown))
        {
            Console.WriteLine($"  {detail}");
        }
        if (details.Count > Shown)
        {
            Console.WriteLine($"  ...and {details.Count - Shown} more");
        }
    }
}

// rql import FILE... [--project DIR] [--folder NAME] [--replace]: reads tables from CSV, JSON or table files
// into a project, leaving any table that's already there alone unless told to replace it.
static int Import(string[] args)
{
    var files = new List<string>();
    string directory = ".", folder = "";
    bool replace = false;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--project" when i + 1 < args.Length:
                directory = args[++i];
                break;
            case "--folder" when i + 1 < args.Length:
                folder = args[++i].Replace('/', '.').Trim('.');
                break;
            case "--replace":
                replace = true;
                break;
            default:
                files.Add(args[i]);
                break;
        }
    }
    if (files.Count == 0)
    {
        Console.Error.WriteLine("usage: import FILE... [--project DIR] [--folder NAME] [--replace]");
        Console.Error.WriteLine("Reads tables from CSV, TSV, JSON or .RQLtable files into the project (the current directory unless given).");
        return 2;
    }

    var store = new Project(directory).Tables;
    int failed = 0;
    foreach (string file in files)
    {
        IReadOnlyList<TableDefinition> tables;
        try
        {
            string text = File.ReadAllText(file);
            tables = TableImport.Import(text, Path.GetFileName(file), TableImport.Detect(text, file));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TableFormatException)
        {
            Console.Error.WriteLine($"{file}: {e.Message}");
            failed++;
            continue;
        }

        foreach (TableDefinition table in tables)
        {
            string name = folder.Length == 0 ? table.Name : $"{folder}.{table.Name}";
            try
            {
                Table resolved = (table with { Name = name }).Resolve();
                if (store.Exists(name) && !replace)
                {
                    Console.Error.WriteLine($"{file}: skipped {name}, which is already a table (--replace to replace it)");
                    failed++;
                    continue;
                }
                store.Save(table with { Name = name });
                Console.WriteLine($"{file}: imported {name} ({resolved.Count} row{(resolved.Count == 1 ? "" : "s")})");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or TableFormatException)
            {
                Console.Error.WriteLine($"{file}: {e.Message}");
                failed++;
            }
        }
    }
    return failed == 0 ? 0 : 1;
}
