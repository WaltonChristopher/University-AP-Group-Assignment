using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;
using Yodaphone.Web.Logging;

namespace Yodaphone.Web.Tests.Logging;

public sealed class ApplicationLoggingTests
{
    [Fact]
    public void Injected_loggers_write_to_content_root_with_scopes_exceptions_and_configured_levels()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "Yodaphone-Logging-" + Guid.NewGuid());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Information",
            ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore"] = "Warning"
        }).Build();

        try
        {
            using (var logger = ApplicationLogging.CreateLogger(configuration, contentRoot))
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSerilog(logger, dispose: false);
                using var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<ILoggerFactory>();
                var applicationLogger = factory.CreateLogger<ApplicationLoggingTests>();
                using (applicationLogger.BeginScope(new Dictionary<string, object> { ["OperationId"] = "test-operation" }))
                {
                    applicationLogger.LogError(new InvalidOperationException("Diagnostic exception"), "File logging marker");
                }

                applicationLogger.LogDebug("Filtered debug marker");
                var frameworkLogger = factory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
                frameworkLogger.LogInformation("Filtered database marker");
                frameworkLogger.LogError("Database error marker");
            }

            var logFile = Assert.Single(Directory.GetFiles(Path.Combine(contentRoot, "Logs"), "app-*.log"));
            var contents = File.ReadAllText(logFile);
            Assert.Contains("File logging marker", contents);
            Assert.Contains("[ERR]", contents);
            Assert.Contains(nameof(ApplicationLoggingTests), contents);
            Assert.Contains("test-operation", contents);
            Assert.Contains("InvalidOperationException: Diagnostic exception", contents);
            Assert.Contains("Database error marker", contents);
            Assert.DoesNotContain("Filtered debug marker", contents);
            Assert.DoesNotContain("Filtered database marker", contents);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }
}
