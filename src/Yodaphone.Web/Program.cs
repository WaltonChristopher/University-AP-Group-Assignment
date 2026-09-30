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

        var app = builder.Build();

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
