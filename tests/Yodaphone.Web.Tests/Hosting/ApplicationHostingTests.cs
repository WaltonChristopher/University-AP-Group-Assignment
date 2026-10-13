using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Components;
using Yodaphone.Web.Data;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Hosting;

public sealed class ApplicationHostingTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Startup_MigratesDatabaseAndServesChat(string environment)
    {
        await using var factory = new TestApplicationFactory(environment, "  OFFLINE  ");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("How can I help with your account today?", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Single(await db.Conversations.ToListAsync());
        Assert.Equal(42, (await db.Users.SingleAsync()).UserId);
        Assert.IsType<OfflineChatAgent>(scope.ServiceProvider.GetRequiredService<IChatAgent>());
        Assert.IsType<ChatService>(scope.ServiceProvider.GetRequiredService<IChatService>());
        var logs = factory.ReadLogs();
        Assert.Contains("Database migrations completed", logs);
        Assert.Contains("HTTP GET / responded 200", logs);
        Assert.Contains("RequestId", logs);
        Assert.DoesNotContain("How can I help with your account today?", logs);
    }

    [Fact]
    public async Task MissingRoute_ReturnsNotFoundPage()
    {
        await using var factory = new TestApplicationFactory("Production", "offline");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not Found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ErrorRoute_DisplaysRequestIdentifier()
    {
        await using var factory = new TestApplicationFactory("Production", "offline");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/Error");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("An error occurred while processing your request.", html);
        Assert.Contains("Request ID:", html);
        var requestId = Regex.Match(html, "<code>([^<]+)</code>");
        Assert.True(requestId.Success);
        Assert.Contains(requestId.Groups[1].Value, factory.ReadLogs());
    }

    [Fact]
    public async Task UnhandledRequestFailure_LogsExceptionAndShowsSafeErrorPage()
    {
        await using var factory = new TestApplicationFactory("Production", "offline");
        await using var failingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<ICurrentUser>(_ => throw new InvalidOperationException("Hosting diagnostic marker"))));
        using var client = failingFactory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("An error occurred while processing your request.", html);
        Assert.DoesNotContain("Hosting diagnostic marker", html);
        var logs = factory.ReadLogs();
        Assert.Contains("[ERR]", logs);
        Assert.Contains("InvalidOperationException: Hosting diagnostic marker", logs);
        Assert.Contains("responded 500", logs);
        var requestId = Regex.Match(html, "<code>([^<]+)</code>");
        Assert.True(requestId.Success);
        Assert.Contains(requestId.Groups[1].Value, logs);
    }

    [Fact]
    public async Task Startup_CopilotConfigurationRegistersAgentAndServerOptions()
    {
        await using var factory = new TestApplicationFactory("Production", " COPILOT ");
        using var scope = factory.Services.CreateScope();
        Assert.IsType<GitHubCopilotAgent>(scope.ServiceProvider.GetRequiredService<IChatAgent>());
        Assert.IsType<CopilotClientFactory>(scope.ServiceProvider.GetRequiredService<ICopilotClientFactory>());
        Assert.Equal("test-server-token", scope.ServiceProvider.GetRequiredService<IOptions<CopilotOptions>>().Value.GitHubToken);
    }

    [Theory]
    [InlineData("agent", "InvalidOperationException", "Unknown chat agent: unsupported")]
    [InlineData("database", "SqliteException", "Applying database migrations")]
    public async Task StartupFailures_LogFatalExceptionAndExitWithFailure(string failure, string exceptionType, string diagnostic)
    {
        // Main reports startup failures with an exit code rather than propagating
        // them to WebApplicationFactory. Keep that exit code out of the test host.
        var contentRoot = Path.Combine(Path.GetTempPath(), $"yodaphone-startup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add(typeof(App).Assembly.Location);
        process.StartInfo.ArgumentList.Add("--contentRoot");
        process.StartInfo.ArgumentList.Add(contentRoot);
        process.StartInfo.ArgumentList.Add("--Chat:Agent");
        process.StartInfo.ArgumentList.Add(failure == "agent" ? "unsupported" : "offline");
        process.StartInfo.ArgumentList.Add("--ConnectionStrings:DefaultConnection");
        process.StartInfo.ArgumentList.Add($"Data Source={Path.Combine(contentRoot, "missing", "test.db")};Pooling=False");
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        var started = false;
        try
        {
            started = process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            var console = await standardOutput + await standardError;

            Assert.Equal(1, process.ExitCode);
            var logs = ReadLogs(contentRoot);
            Assert.Contains("[FTL]", logs);
            Assert.Contains("Yodaphone terminated unexpectedly", logs);
            Assert.Contains(exceptionType, logs);
            Assert.Contains(diagnostic, logs);
            Assert.Contains("Yodaphone terminated unexpectedly", console);
        }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static string ReadLogs(string contentRoot) =>
        string.Join(Environment.NewLine, Directory.GetFiles(Path.Combine(contentRoot, "Logs"), "app-*.log").Select(File.ReadAllText));

    private sealed class TestApplicationFactory(string environment, string agent) : WebApplicationFactory<App>
    {
        private readonly string contentRoot = Path.Combine(Path.GetTempPath(), $"yodaphone-host-tests-{Guid.NewGuid():N}");

        public string ReadLogs() => ApplicationHostingTests.ReadLogs(contentRoot);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(contentRoot);
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment(environment);
            builder.UseSetting("Serilog:MinimumLevel:Default", "Information");
            builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft.AspNetCore", "Warning");
            builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore", "Warning");
            builder.UseSetting("Chat:Agent", agent);
            builder.UseSetting("Chat:DevelopmentUserId", "42");
            builder.UseSetting("Chat:Copilot:GitHubToken", "test-server-token");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(contentRoot, "test.db")};Pooling=False");
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
        }
    }
}
