using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Rql.Nodes;
using Rql.Projects;
using Rql.Tables;

namespace Rql.Web.Tests;

/// <summary>Pages rendered with a project of their own open, in a folder of its own.</summary>
public abstract class WebTest : BunitContext
{
    protected WebTest()
    {
        Root = System.IO.Directory.CreateTempSubdirectory("rql-web-").FullName;
        Directory = System.IO.Path.Combine(Root, "project");
        System.IO.Directory.CreateDirectory(Directory);
        Recent = new RecentProjects(System.IO.Path.Combine(Root, "recent.json"));
        Recent.Touch(Directory);

        Services.AddSingleton(Recent);
        Services.AddSingleton(FunctionRegistry.Builtins());
        Services.AddScoped<ProjectSession>();
        Services.AddScoped<RollSession>();
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    /// <summary>A folder holding the project and anything else a test needs beside it.</summary>
    protected string Root { get; }

    protected string Directory { get; }

    protected RecentProjects Recent { get; }

    protected Project Project => new(Directory);

    protected TableStore Store => Project.Tables;

    protected NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    protected void WriteTable(string name, string text)
    {
        string path = System.IO.Path.Combine(Directory, name.Replace('/', System.IO.Path.DirectorySeparatorChar) + TableStore.Extension);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    protected string ReadTable(string name) =>
        File.ReadAllText(System.IO.Path.Combine(Directory, name.Replace('/', System.IO.Path.DirectorySeparatorChar) + TableStore.Extension));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        System.IO.Directory.Delete(Root, recursive: true);
    }
}
