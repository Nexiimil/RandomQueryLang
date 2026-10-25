#:package NuGet.Versioning@6.14.0

// Keeps the Rql package's changelog honest and its versions semantic. Run from the repository root:
//
//   dotnet run build/Changelog.cs -- lint                 checks the changelog is well formed
//   dotnet run build/Changelog.cs -- pr <base ref>        ...and that a change to the library adds to [Unreleased]
//   dotnet run build/Changelog.cs -- release <tag>        ...and that the tag is a fitting next version
//
// pr and release write baseline=<version> to $GITHUB_OUTPUT when the public API has to stay compatible with
// that released version, which the workflow then checks with package validation. release also writes the
// version's notes to release-notes.md.

using System.Diagnostics;
using System.Text.RegularExpressions;
using NuGet.Versioning;

const string ChangelogPath = "src/Rql/CHANGELOG.md";
const string LibraryFolder = "src/Rql/";

string[] kinds = ["Added", "Changed", "Deprecated", "Removed", "Fixed", "Security"];

try
{
    switch (args)
    {
        case ["lint"]:
            Parse(File.ReadAllText(ChangelogPath));
            Console.WriteLine("The changelog is well formed.");
            break;
        case ["pr", string baseRef]:
            CheckPullRequest(baseRef);
            break;
        case ["release", string tag]:
            CheckRelease(tag);
            break;
        default:
            Console.Error.WriteLine("usage: Changelog.cs lint | pr <base ref> | release <tag>");
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

void CheckPullRequest(string baseRef)
{
    Changelog head = Parse(File.ReadAllText(ChangelogPath));
    string[] changed = Git("diff", "--name-only", $"{baseRef}...HEAD")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    // Markdown in the library folder is the readme and the changelog itself, neither of which changes the package's code
    string[] library = [.. changed.Where(path => path.StartsWith(LibraryFolder) && !path.EndsWith(".md"))];
    if (library.Length == 0)
    {
        Console.WriteLine("Nothing in the library changed, so the changelog doesn't need to.");
    }
    else
    {
        string? before = GitOrNull("show", $"{baseRef}:{ChangelogPath}");
        IReadOnlyList<string> was = before is null ? [] : Parse(before).Unreleased.Entries;
        if (!head.Unreleased.Entries.Except(was).Any())
        {
            throw new ChangelogException(
                $"This changes the library ({string.Join(", ", library.Take(5))}{(library.Length > 5 ? ", ..." : "")}) "
                + $"but adds nothing under [Unreleased] in {ChangelogPath}. Say what changed for someone using the package.");
        }
        Console.WriteLine($"The library changed and [Unreleased] has {head.Unreleased.Entries.Count} entries.");
    }

    SemanticVersion? latest = Releases().Where(version => !version.IsPrerelease).Max();
    if (latest is not null && !head.Unreleased.IsBreaking)
    {
        Console.WriteLine($"No breaking change is declared, so the public API must still be compatible with {latest}.");
        Output("baseline", latest.ToNormalizedString());
    }
}

void CheckRelease(string tag)
{
    if (!tag.StartsWith('v') || !SemanticVersion.TryParse(tag[1..], out SemanticVersion? version))
    {
        throw new ChangelogException($"The tag {tag} isn't a semantic version: tag releases like v1.2.0 or v1.2.0-beta.1.");
    }

    SemanticVersion[] earlier = [.. Releases().Where(release => release != version)];
    if (earlier.Max() is { } highest && version <= highest)
    {
        throw new ChangelogException($"{version} isn't higher than the last release, {highest}.");
    }

    Changelog changelog = Parse(File.ReadAllText(ChangelogPath));
    if (changelog.Unreleased.Entries.Count > 0)
    {
        throw new ChangelogException(
            $"[Unreleased] still has entries. Move them under [{version}] before tagging, so they're part of this release.");
    }
    Section section = changelog.Versions.FirstOrDefault(found => found.Version == version)
        ?? throw new ChangelogException($"There's no [{version}] section in the changelog for this release.");
    if (section.Entries.Count == 0)
    {
        throw new ChangelogException($"[{version}] has nothing in it. Say what's in this release.");
    }

    File.WriteAllText("release-notes.md", section.Body);

    // A prerelease promises nothing about compatibility, so only a full release is held to these
    SemanticVersion? previous = earlier.Where(release => !release.IsPrerelease).Max();
    if (version.IsPrerelease || previous is null)
    {
        Console.WriteLine($"{version} is a prerelease or the first release, so there's nothing to keep compatible with.");
        return;
    }

    bool major = version.Major > previous.Major;
    bool minor = major || version.Minor > previous.Minor;
    // Before 1.0, a minor bump is allowed to break things, the way a major one is after
    bool breakingAllowed = previous.Major == 0 ? minor : major;

    if (section.IsBreaking && !breakingAllowed)
    {
        throw new ChangelogException(
            $"[{version}] has breaking changes (a Removed section or a **Breaking** entry), "
            + $"so it has to be {(previous.Major == 0 ? "a minor or major" : "a major")} release after {previous}.");
    }
    if (previous.Major > 0 && !minor && (section.Has("Added") || section.Has("Deprecated")))
    {
        throw new ChangelogException(
            $"[{version}] adds or deprecates something, so it has to be at least a minor release after {previous}, not a patch.");
    }

    if (!breakingAllowed)
    {
        Console.WriteLine($"{version} can't break anything, so its public API must still be compatible with {previous}.");
        Output("baseline", previous.ToNormalizedString());
    }
}

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
                if (!SemanticVersion.TryParse(name, out version))
                {
                    throw new ChangelogException($"{where}: [{name}] isn't a semantic version.");
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
            current.Kinds.Add(kind);
        }
        else if (line.StartsWith("- "))
        {
            if (current is null || kind is null)
            {
                throw new ChangelogException($"{where}: an entry has to be under a version and one of {string.Join(", ", kinds)}.");
            }
            current.Entries.Add(line);
            current.IsBreaking |= kind == "Removed" || line.Contains("**Breaking**");
        }
        if (current is not null)
        {
            current.Lines.Add(line);
        }
    }

    if (sections.Count == 0 || sections[0].Name != "Unreleased")
    {
        throw new ChangelogException("The first version heading has to be ## [Unreleased], where changes wait for the next release.");
    }
    if (sections.Skip(1).Any(section => section.Version is null))
    {
        throw new ChangelogException("There can only be one [Unreleased] section, at the top.");
    }
    for (int i = 2; i < sections.Count; i++)
    {
        if (sections[i].Version! >= sections[i - 1].Version!)
        {
            throw new ChangelogException($"Versions go newest first, but [{sections[i].Name}] comes after [{sections[i - 1].Name}].");
        }
    }
    return new Changelog(sections[0], [.. sections.Skip(1)]);
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

record Changelog(Section Unreleased, IReadOnlyList<Section> Versions);

class Section(string name, SemanticVersion? version)
{
    public string Name => name;
    public SemanticVersion? Version => version;
    public List<string> Kinds { get; } = [];
    public List<string> Entries { get; } = [];
    public List<string> Lines { get; } = [];
    public bool IsBreaking { get; set; }
    public bool Has(string kind) => Kinds.Contains(kind) && Entries.Count > 0;

    // Everything under the heading, for the release's notes
    public string Body => string.Join('\n', Lines.Skip(1)).Trim() + "\n";
}

class ChangelogException(string message) : Exception(message);
