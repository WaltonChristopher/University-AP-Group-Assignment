using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Diagnostics;
using Yodaphone.Web.Data;
using Yodaphone.Web.Components;
using Yodaphone.Web.Services;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Logging;

public static class Program
{
    public static void Main(string[] args)
    {
        // Configuration loading can fail before the file logger is available.
        using var bootstrapLogger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        Log.Logger = bootstrapLogger;

        try
        {
            var builder = WebApplication.CreateBuilder(args);
            Log.Logger = ApplicationLogging.CreateLogger(builder.Configuration, builder.Environment.ContentRootPath);
            builder.Services.AddSerilog(Log.Logger, dispose: false);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IConversationRepository, SqliteConversationRepository>();
            builder.Services.AddScoped<ICurrentUser, ConfigurationCurrentUser>();
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

            // A scoped Blazor service can live for an entire user circuit. Instead, we inject a
            // factory so each conversations repository operation gets its own short-lived context.
            builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseSqlite(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            using var app = builder.Build();
            Log.Information("Starting Yodaphone with chat agent {ChatAgent}", agentName);
            using (var scope = app.Services.CreateScope())
            {
                // Apply pending schema changes with a dedicated startup context,
                // disposed before the application begins handling conversations.
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                using var db = dbFactory.CreateDbContext();
                Log.Information("Applying database migrations");
                db.Database.Migrate();
                Log.Information("Database migrations completed");
            }
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
            }
            app.UseSerilogRequestLogging(options =>
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                    diagnosticContext.Set("RequestId", Activity.Current?.Id ?? httpContext.TraceIdentifier));
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
        catch (Exception exception) when (exception is not HostAbortedException)
        {
            // HostAbortedException is used by EF tooling to inspect the host.
            Log.Fatal(exception, "Yodaphone terminated unexpectedly");
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
