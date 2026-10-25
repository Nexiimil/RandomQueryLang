using Rql.Nodes;
using Rql.Syntax;

namespace Rql.Evaluation;

/// <summary>
/// Runs a query the way Roll20 does: put macros, character sheet values and answers to roll queries in
/// place, roll the inline rolls innermost first and put their results in place, then read what's left, build
/// a tree for each roll in it, report what can be reported without rolling, and roll.
/// </summary>
/// <remarks>
/// There is no type checking here. A tree the binder produced is a tree whose types line up, so the only
/// thing left to go wrong is something that depends on what comes up. Every roll in a query is checked
/// before any of them is rolled, so a mistake in the last one doesn't leave the first rolled for nothing.
/// </remarks>
public sealed class Interpreter(Context context, FunctionRegistry? functions = null)
{
    /// <summary>What's put in place of anything asked for while a query is only being checked.</summary>
    private const string Placeholder = "1";

    private readonly FunctionRegistry functions = functions ?? FunctionRegistry.Builtins();

    public Context Context => context;

    /// <summary>The queries <c>#Name</c> stands for, by name, ignoring case: a project's saved queries.</summary>
    public IReadOnlyDictionary<string, string> Macros { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Runs a query, answering what it asks from <paramref name="answers"/>, by each prompt's key, and
    /// otherwise with the prompt's default.
    /// </summary>
    /// <exception cref="PromptsNeededException">It asks something with no answer and no default.</exception>
    /// <exception cref="QueryException">The query is malformed, or something that came up couldn't be used.</exception>
    public QueryResult Run(string query, IReadOnlyDictionary<string, string>? answers = null)
    {
        var expansion = new Expansion(MacroTable(), prompt => answers?.GetValueOrDefault(prompt.Key) ?? prompt.Default);
        MappedText text = expansion.Expand(query);
        List<InlineRoll> inline = [];
        text = RollInline(text, inline, rolling: true);

        var results = new List<Result>();
        var statements = Prepare(text);
        for (int i = 0; i < statements.Count; i++)
        {
            var (statement, bound) = statements[i];
            Context run = context with { Log = new RollLog() };
            object value;
            try
            {
                value = bound.Root.Evaluate(run);
            }
            catch (EvalException e)
            {
                throw new QueryException(e.Message, text.OriginOf(bound.PositionOf(e.Blame)));
            }
            int from = text.OriginOf(statement.Start);
            int until = i + 1 < statements.Count ? text.OriginOf(statements[i + 1].Statement.Start) : int.MaxValue;
            results.Add(new Result(value, run.Log.Rolls, statement.Label)
            {
                Groups = run.Log.Groups,
                Labels = run.Log.Labels,
                Inline = [.. inline.Where(roll => roll.Position >= from && roll.Position < until)],
            });
        }
        return new QueryResult(results);
    }

    /// <summary>
    /// Everything a query asks for, given the answers so far, which can change what it asks, since an
    /// answer can hold another question.
    /// </summary>
    public IReadOnlyList<RollPrompt> Prompts(string query, IReadOnlyDictionary<string, string>? answers = null)
    {
        var expansion = new Expansion(MacroTable(), prompt => answers?.GetValueOrDefault(prompt.Key) ?? prompt.Default ?? Placeholder);
        try
        {
            expansion.Expand(query);
        }
        catch (QueryException)
        {
            // What's asked before the mistake is still worth asking; Validate says what the mistake is.
        }
        return expansion.Asked;
    }

    /// <summary>
    /// Everything <see cref="Run"/> does short of rolling, so a query can be checked as it's typed. Anything
    /// the query asks for is taken to be its default, or 1.
    /// </summary>
    /// <exception cref="QueryException">The query would fail no matter what was rolled.</exception>
    public void Validate(string query)
    {
        var expansion = new Expansion(MacroTable(), prompt => prompt.Default ?? Placeholder);
        MappedText text = expansion.Expand(query);
        Prepare(RollInline(text, [], rolling: false));
    }

    private Dictionary<string, string> MacroTable() => new(Macros, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rolls each inline roll, <c>[[...]]</c>, innermost first, putting its result in its place; or when only
    /// checking, checks it and puts 1 in its place.
    /// </summary>
    private MappedText RollInline(MappedText text, List<InlineRoll> rolled, bool rolling)
    {
        while (true)
        {
            var (open, close) = Innermost(text);
            if (open < 0)
            {
                return text;
            }

            MappedText inner = text.Slice(open + 2, close);
            BoundQuery bound = Bind(inner, number: true);
            string value = Placeholder;
            if (rolling)
            {
                Context run = context with { Log = new RollLog() };
                decimal result;
                try
                {
                    result = (decimal)bound.Root.Evaluate(run);
                }
                catch (EvalException e)
                {
                    throw new QueryException(e.Message, inner.OriginOf(bound.PositionOf(e.Blame)));
                }
                rolled.Add(new InlineRoll(inner.Text.Trim(), result, text.OriginOf(open))
                {
                    Rolls = run.Log.Rolls,
                    Groups = run.Log.Groups,
                    Labels = run.Log.Labels,
                });
                value = Numbers.Display(result);
            }
            text = text.Replace(open, close + 2, value);
        }
    }

    /// <summary>
    /// The first inline roll to close, which has no other inside it, as where its <c>[[</c> and <c>]]</c> are,
    /// or -1 if there are none. Labels, <c>[STR]</c>, are kept track of, so <c>[[d20+5[STR]]]</c> closes at the end.
    /// </summary>
    private static (int Open, int Close) Innermost(MappedText text)
    {
        string source = text.Text;
        var opens = new Stack<int>();
        int labels = 0;
        bool quoted = false;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quoted)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == '[' && i + 1 < source.Length && source[i + 1] == '[' && labels == 0)
            {
                opens.Push(i);
                i++;
            }
            else if (c == '[')
            {
                labels++;
            }
            else if (c == ']' && labels > 0)
            {
                labels--;
            }
            else if (c == ']' && i + 1 < source.Length && source[i + 1] == ']')
            {
                return opens.Count > 0
                    ? (opens.Pop(), i)
                    : throw new QueryException("]] has no [[ before it", text.OriginOf(i));
            }
        }
        return opens.Count > 0
            ? throw new QueryException("[[ is missing its closing ]]", text.OriginOf(opens.Last()))
            : (-1, -1);
    }

    private List<(Statement Statement, BoundQuery Bound)> Prepare(MappedText text)
    {
        IReadOnlyList<Statement> statements;
        try
        {
            statements = Parser.ParseQuery(text.Text);
        }
        catch (QueryException e)
        {
            throw new QueryException(e.Message, text.OriginOf(e.Position));
        }
        return [.. statements.Select(statement => (statement, Check(text, Binder.Bind, statement.Expr)))];
    }

    /// <summary>Reads, binds and checks an inline roll, which has to come to a number to be put in place.</summary>
    private BoundQuery Bind(MappedText text, bool number)
    {
        Expr expr;
        try
        {
            expr = Parser.Parse(text.Text);
        }
        catch (QueryException e)
        {
            throw new QueryException(e.Message, text.OriginOf(e.Position));
        }
        return Check(text, number ? Binder.BindNumber : Binder.Bind, expr);
    }

    private BoundQuery Check(MappedText text, Func<Expr, Tables.TableCatalog, FunctionRegistry, BoundQuery> bind, Expr expr)
    {
        try
        {
            BoundQuery bound = bind(expr, context.Tables, functions);
            Evaluation.Check.BeforeRolling(bound, context);
            return bound;
        }
        catch (QueryException e)
        {
            throw new QueryException(e.Message, text.OriginOf(e.Position));
        }
    }
}
