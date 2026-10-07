using Microsoft.EntityFrameworkCore;
using Yodaphone.Web.Data;
using Yodaphone.Web.Components;
using Yodaphone.Web.Services;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Chat.Agents.Copilot;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddScoped<IChatService, ChatService>();
        builder.Services.Configure<CopilotOptions>(
            builder.Configuration.GetSection(CopilotOptions.SectionName));

        // Register the chat agent based on configuration.
        var agentName = builder.Configuration["Chat:Agent"] ?? "Offline";
        switch (agentName.Trim().ToLowerInvariant())
        {
            case "offline":
                builder.Services.AddScoped<IChatAgent, OfflineChatAgent>();
                break;
            case "copilot":
                builder.Services.AddScoped<ICopilotClientFactory, CopilotClientFactory>();
                builder.Services.AddScoped<IChatAgent, GitHubCopilotAgent>();
                break;
            default:
                throw new InvalidOperationException($"Unknown chat agent: {agentName}");
        }

builder.Services.AddDbContext<ApplicationDbContext>(options =>
   options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}
// Configure the HTTP request pipeline.
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
    }
}
