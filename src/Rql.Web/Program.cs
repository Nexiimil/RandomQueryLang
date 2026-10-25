using Rql.Nodes;
using Rql.Web;
using Rql.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// --project <dir> (or --tables <dir>) opens that folder as a project, making it if it isn't there yet.
// Otherwise the app opens the project used last.
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--project"] = "Project",
    ["--tables"] = "Project",
});
var recent = new RecentProjects(builder.Configuration["RecentProjectsFile"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RandomQueryLang", "recent-projects.json"));
if (builder.Configuration["Project"] is { Length: > 0 } start)
{
    Directory.CreateDirectory(start);
    recent.Touch(start);
}

builder.Services.AddSingleton(recent);
builder.Services.AddSingleton(FunctionRegistry.Builtins());
builder.Services.AddScoped<ProjectSession>();
builder.Services.AddScoped<RollSession>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
