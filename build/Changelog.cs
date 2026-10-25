#:package NuGet.Versioning@6.14.0

// Keeps the Rql package's changelog honest and its versions semantic. Every pull request that changes the package
// gives it a new version in src/Rql/CHANGELOG.md, and merging the pull request publishes that version. Run from the
// repository root:
//
//   dotnet run build/Changelog.cs -- version [major|minor|patch]   turns [Unreleased] into the next version, dated today
//   dotnet run build/Changelog.cs -- lint                          checks the changelog is well formed
//   dotnet run build/Changelog.cs -- pr <base ref>                 ...and that a change to the package comes with its version
//   dotnet run build/Changelog.cs -- release                       ...and whether the newest version still needs publishing
//
// pr and release write baseline=<version> to $GITHUB_OUTPUT when the public API has to stay compatible with that
// version, which the workflows check with package validation. release also writes version=<version> when there's a
// version to publish, and that version's notes to release-notes.md.

using System.Diagnostics;
using System.Text.RegularExpressions;
using NuGet.Versioning;

const string ChangelogPath = "src/Rql/CHANGELOG.md";
const string VersionCommand = "dotnet run build/Changelog.cs -- version";

string[] kinds = ["Added", "Changed", "Deprecated", "Removed", "Fixed", "Security"];

try
{
    switch (args)
    {
        case ["version"]:
            Version(null);
            break;
        case ["version", string level] when Enum.TryParse(level, ignoreCase: true, out Level chosen):
            Version(chosen);
            break;
        case ["lint"]:
            Parse(File.ReadAllText(ChangelogPath));
            Console.WriteLine("The changelog is well formed.");
            break;
        case ["pr", string baseRef]:
            CheckPullRequest(baseRef);
            break;
        case ["release"]:
            CheckRelease();
            break;
        default:
            Console.Error.WriteLine("usage: Changelog.cs version [major|minor|patch] | lint | pr <base ref> | release");
            return 2;
    }
    return 0;
}
catch (ChangelogException e)
{
    // Shown as an error on the run's summary as well as in its log
    Console.WriteLine($"::error file={ChangelogPath}::{e.Message}");
    return 1;
}

// Anything that goes into the package or decides how it's built. The changelog itself is left out so its wording
// can be fixed without making a release.
static bool AltersPackage(string path) =>
    (path.StartsWith("src/Rql/") && path != ChangelogPath) || path == "Directory.Build.props";

void Version(Level? chosen)
{
    string text = File.ReadAllText(ChangelogPath);
    Changelog changelog = Parse(text);
    Section unreleased = changelog.Unreleased
        ?? throw new ChangelogException($"There's no [Unreleased] section to give a version. Write what changed under ## [Unreleased] at the top first.");
    if (unreleased.Entries.Count == 0)
    {
        throw new ChangelogException("[Unreleased] has nothing in it. Say what changed first.");
    }

    SemanticVersion? previous = changelog.Versions.FirstOrDefault()?.Version;
    SemanticVersion next;
    if (previous is null)
    {
        next = new SemanticVersion(0, 1, 0);
    }
    else
    {
        Level required = Required(previous, unreleased);
        if (chosen < required)
        {
            throw new ChangelogException($"These changes need at least a {required.ToString().ToLowerInvariant()} release.");
        }
        next = Bump(previous, chosen ?? required);
    }

    string dated = $"## [{next}] - {DateTime.UtcNow:yyyy-MM-dd}";
    File.WriteAllText(ChangelogPath, new Regex(@"^## \[Unreleased\][ \t]*$", RegexOptions.Multiline).Replace(text, dated, 1));
    Console.WriteLine($"[Unreleased] is now {dated[3..]}. Merging this publishes {next}.");
}

void CheckPullRequest(string baseRef)
{
    Changelog head = Parse(File.ReadAllText(ChangelogPath));
    if (head.Unreleased is not null)
    {
        throw new ChangelogException($"Merging publishes the package, so [Unreleased] has to become this change's version first: run `{VersionCommand}`.");
    }

    string[] package = [.. Git("diff", "--name-only", $"{baseRef}...HEAD")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(AltersPackage)];
    Changelog? before = GitOrNull("show", $"{baseRef}:{ChangelogPath}") is { } text ? Parse(text) : null;
    HashSet<SemanticVersion> known = [.. before?.Versions.Select(section => section.Version!) ?? []];
    Section[] added = [.. head.Versions.Where(section => !known.Contains(section.Version!))];
    SemanticVersion? previous = before?.Versions.FirstOrDefault()?.Version;

    if (package.Length == 0)
    {
        if (added.Length > 0)
        {
            throw new ChangelogException($"This adds [{added[0].Name}] without changing the package, which would publish the same package again.");
        }
        Console.WriteLine("Nothing in the package changed, so there's no new version to check.");
        return;
    }

    string files = string.Join(", ", package.Take(5)) + (package.Length > 5 ? ", ..." : "");
    if (added.Length == 0)
    {
        throw new ChangelogException(
            $"This changes the package ({files}) but doesn't give it a new version. Write what changed for someone using the "
            + $"package under ## [Unreleased] at the top of {ChangelogPath}, then run `{VersionCommand}` to turn it into the right version.");
    }
    if (added.Length > 1 || added[0] != head.Versions[0])
    {
        throw new ChangelogException(
            $"A pull request makes one new version, at the top of the changelog, but this adds {string.Join(", ", added.Select(section => $"[{section.Name}]"))}.");
    }

    Section version = added[0];
    Level? level = CheckNext(previous, version);
    Console.WriteLine($"This changes the package and merging it publishes {version.Name}.");
    if (previous is not null && !BreakingAllowed(previous, level!.Value))
    {
        Console.WriteLine($"{version.Name} can't break anything, so its public API must still be compatible with {previous}.");
        Output("baseline", previous.ToNormalizedString());
    }
}

void CheckRelease()
{
    Changelog changelog = Parse(File.ReadAllText(ChangelogPath));
    if (changelog.Unreleased is not null)
    {
        throw new ChangelogException("The changelog has an [Unreleased] section, which the pull request check should have turned away.");
    }
    if (changelog.Versions.FirstOrDefault() is not { } newest)
    {
        Console.WriteLine("The changelog has no versions yet, so there's nothing to publish.");
        return;
    }

    SemanticVersion[] released = [.. Releases()];
    if (released.Contains(newest.Version!))
    {
        Console.WriteLine($"{newest.Name} is already published, so there's nothing to do.");
        return;
    }

    SemanticVersion? previous = released.Max();
    if (previous is not null && newest.Version! <= previous)
    {
        throw new ChangelogException($"{newest.Name} isn't higher than the last release, {previous}.");
    }
    Level? level = CheckNext(previous, newest);

    File.WriteAllText("release-notes.md", newest.Body);
    Output("version", newest.Name);
    Console.WriteLine($"Publishing {newest.Name}.");
    if (previous is not null && !BreakingAllowed(previous, level!.Value))
    {
        Console.WriteLine($"{newest.Name} can't break anything, so its public API must still be compatible with {previous}.");
        Output("baseline", previous.ToNormalizedString());
    }
}

// Checks a new version is the next one after the last, by as much as its changes need, and says how big a step it is.
// Nothing comes before the first version, so any version can be first.
Level? CheckNext(SemanticVersion? previous, Section section)
{
    if (previous is null)
    {
        return null;
    }
    Level[] levels = [Level.Patch, Level.Minor, Level.Major];
    Level? level = levels.Cast<Level?>().FirstOrDefault(candidate => Bump(previous, candidate!.Value) == section.Version);
    if (level is null)
    {
        throw new ChangelogException(
            $"[{section.Name}] isn't a next version after {previous}: it can be {string.Join(", ", levels.Select(candidate => Bump(previous, candidate)))}. "
            + $"`{VersionCommand}` picks the right one.");
    }
    Level required = Required(previous, section);
    if (level < required)
    {
        throw new ChangelogException(
            $"[{section.Name}] has to be at least a {required.ToString().ToLowerInvariant()} release after {previous}, "
            + $"{(section.IsBreaking ? "since it has breaking changes (a Removed section or a **Breaking** entry)" : "since it adds or deprecates something")}, "
            + $"so it's {Bump(previous, required)}. `{VersionCommand}` picks the right one.");
    }
    return level;
}

// Before 1.0 a minor release is allowed to break things and everything else is a patch, as Cargo and npm read 0.x versions
static Level Required(SemanticVersion previous, Section section) =>
    previous.Major == 0
        ? section.IsBreaking ? Level.Minor : Level.Patch
        : section.IsBreaking ? Level.Major : section.Has("Added") || section.Has("Deprecated") ? Level.Minor : Level.Patch;

static bool BreakingAllowed(SemanticVersion previous, Level level) =>
    level == Level.Major || (previous.Major == 0 && level == Level.Minor);

static SemanticVersion Bump(SemanticVersion version, Level level) => level switch
{
    Level.Major => new SemanticVersion(version.Major + 1, 0, 0),
    Level.Minor => new SemanticVersion(version.Major, version.Minor + 1, 0),
    _ => new SemanticVersion(version.Major, version.Minor, version.Patch + 1),
};

Changelog Parse(string text)
{
    var heading = new Regex(@"^## \[(?<name>[^\]]+)\](?: - (?<date>\d{4}-\d{2}-\d{2}))?\s*$");
    var sections = new List<Section>();
    Section? current = null;
    string? kind = null;
    string[] lines = text.ReplaceLineEndings("\n").Split('\n');

    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i];
        string where = $"line {i + 1}";
        if (line.StartsWith("## "))
        {
            Match match = heading.Match(line);
            if (!match.Success)
            {
                throw new ChangelogException($"{where}: a version heading looks like ## [1.2.0] - 2026-10-25, or ## [Unreleased].");
            }
            string name = match.Groups["name"].Value;
            SemanticVersion? version = null;
            if (name != "Unreleased")
            {
                if (!SemanticVersion.TryParse(name, out version) || version.IsPrerelease || version.HasMetadata)
                {
                    throw new ChangelogException($"{where}: [{name}] isn't a version like 1.2.0.");
                }
                if (!match.Groups["date"].Success || !DateOnly.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd", out _))
                {
                    throw new ChangelogException($"{where}: [{name}] needs the date it was released, like ## [{name}] - 2026-10-25.");
                }
            }
            current = new Section(name, version);
            sections.Add(current);
            kind = null;
        }
        else if (line.StartsWith("### "))
        {
            kind = line[4..].Trim();
            if (current is null || !kinds.Contains(kind))
            {
                throw new ChangelogException($"{where}: changes go under a version, in one of {string.Join(", ", kinds)}.");
            }
        }
        else if (line.StartsWith("- "))
        {
            if (current is null || kind is null)
            {
                throw new ChangelogException($"{where}: an entry has to be under a version and one of {string.Join(", ", kinds)}.");
            }
            current.Add(kind, line);
        }
        current?.Lines.Add(line);
    }

    Section? unreleased = sections.FirstOrDefault()?.Version is null ? sections.FirstOrDefault() : null;
    Section[] versions = [.. sections.Skip(unreleased is null ? 0 : 1)];
    if (versions.Any(section => section.Version is null))
    {
        throw new ChangelogException("There can only be one [Unreleased] section, at the top.");
    }
    for (int i = 1; i < versions.Length; i++)
    {
        if (versions[i].Version! >= versions[i - 1].Version!)
        {
            throw new ChangelogException($"Versions go newest first, but [{versions[i].Name}] comes after [{versions[i - 1].Name}].");
        }
    }
    return new Changelog(unreleased, versions);
}

IEnumerable<SemanticVersion> Releases() =>
    Git("tag", "--list", "v*").Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(tag => SemanticVersion.TryParse(tag[1..], out SemanticVersion? version) ? version : null)
        .OfType<SemanticVersion>();

void Output(string name, string value)
{
    if (Environment.GetEnvironmentVariable("GITHUB_OUTPUT") is { Length: > 0 } path)
    {
        File.AppendAllText(path, $"{name}={value}\n");
    }
}

string Git(params string[] arguments) =>
    GitOrNull(arguments) ?? throw new ChangelogException($"git {string.Join(' ', arguments)} failed.");

string? GitOrNull(params string[] arguments)
{
    var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (string argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }
    using Process git = Process.Start(start)!;
    string output = git.StandardOutput.ReadToEnd();
    git.WaitForExit();
    return git.ExitCode == 0 ? output : null;
}

enum Level { Patch, Minor, Major }

record Changelog(Section? Unreleased, IReadOnlyList<Section> Versions);

class Section(string name, SemanticVersion? version)
{
    private readonly HashSet<string> kindsWithEntries = [];

    public string Name => name;
    public SemanticVersion? Version => version;
    public List<string> Entries { get; } = [];
    public List<string> Lines { get; } = [];
    public bool IsBreaking { get; private set; }

    public bool Has(string kind) => kindsWithEntries.Contains(kind);

    public void Add(string kind, string entry)
    {
        Entries.Add(entry);
        kindsWithEntries.Add(kind);
        IsBreaking |= kind == "Removed" || entry.Contains("**Breaking**");
    }

    // Everything under the heading, for the release's notes
    public string Body => string.Join('\n', Lines.Skip(1)).Trim() + "\n";
}

class ChangelogException(string message) : Exception(message);
