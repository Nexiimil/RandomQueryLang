using Rql.Projects;

namespace Rql.Web;

/// <summary>
/// The project open in one browser tab. It starts as the most recently opened project that still exists,
/// so reloading the page, or opening a new tab, carries on where things were.
/// </summary>
public sealed class ProjectSession
{
    private readonly RecentProjects recent;

    public ProjectSession(RecentProjects recent)
    {
        this.recent = recent;
        if (recent.All().FirstOrDefault(Directory.Exists) is string last)
        {
            Current = new Project(last);
        }
    }

    public Project? Current { get; private set; }

    /// <summary>Raised when a different project is opened.</summary>
    public event Action? Changed;

    /// <exception cref="DirectoryNotFoundException">There's no such folder.</exception>
    public void Open(string directory)
    {
        var project = new Project(directory);
        if (!project.Exists)
        {
            throw new DirectoryNotFoundException($"there's no folder at {project.Directory}");
        }
        Current = project;
        recent.Touch(project.Directory);
        Changed?.Invoke();
    }
}
