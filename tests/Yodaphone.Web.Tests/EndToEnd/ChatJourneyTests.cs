using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace Yodaphone.Web.Tests.EndToEnd;

public sealed class ChatJourneyTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    public async Task Customer_can_send_a_message_and_restore_it_after_page_reload()
    {
        await using var application = await RunningChatApplication.StartAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(application.BaseAddress);
        await page.GetByText("How can I help with your account today?").WaitForAsync();
        var input = page.GetByPlaceholder("Enter text here");
        var sendButton = page.GetByRole(AriaRole.Button, new() { Name = "Send" });
        for (var attempt = 0; attempt < 40 && !await sendButton.IsEnabledAsync(); attempt++)
        {
            // The initial HTML is prerendered; retry the input event until the
            // Blazor Server circuit has attached its event handlers.
            await input.FillAsync("I need help with my plan");
            await Task.Delay(250);
        }

        Assert.True(await sendButton.IsEnabledAsync(), "The chat input did not become interactive.");
        await sendButton.ClickAsync();

        var customerMessage = page.Locator(".yp-message-row--user .yp-bubble__content");
        await customerMessage.WaitForAsync();
        Assert.Equal("I need help with my plan", await customerMessage.InnerTextAsync());
        await page.Locator(".yp-message-row--bot").Nth(1).WaitForAsync();

        await page.ReloadAsync();
        await page.GetByText("How can I help with your account today?").WaitForAsync();
        Assert.Equal("I need help with my plan", await customerMessage.InnerTextAsync());
        Assert.Equal(2, await page.Locator(".yp-message-row--bot").CountAsync());
    }

    private sealed class RunningChatApplication : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string temporaryDirectory;
        private readonly StringBuilder output = new();

        private RunningChatApplication(Process process, string temporaryDirectory, string baseAddress)
        {
            this.process = process;
            this.temporaryDirectory = temporaryDirectory;
            BaseAddress = baseAddress;
        }

        public string BaseAddress { get; }

        public static async Task<RunningChatApplication> StartAsync()
        {
            var repositoryRoot = FindRepositoryRoot();
            var webProject = Path.Combine(repositoryRoot, "src", "Yodaphone.Web", "Yodaphone.Web.csproj");
            var projectDirectory = Path.GetDirectoryName(webProject)!;
            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(), $"yodaphone-e2e-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);

            var port = GetAvailablePort();
            var baseAddress = $"http://127.0.0.1:{port}";
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = projectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--no-launch-profile");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(webProject);
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["ASPNETCORE_URLS"] = baseAddress;
            startInfo.Environment["ConnectionStrings__DefaultConnection"] =
                $"Data Source={Path.Combine(temporaryDirectory, "chat.db")};Pooling=False";
            startInfo.Environment["Chat__Agent"] = "offline";
            startInfo.Environment["Chat__DevelopmentUserId"] = "42";

            var process = new Process { StartInfo = startInfo };
            var application = new RunningChatApplication(process, temporaryDirectory, baseAddress);
            process.OutputDataReceived += (_, eventArgs) => application.CaptureOutput(eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => application.CaptureOutput(eventArgs.Data);

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("The test application process did not start.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await application.WaitUntilReadyAsync();
                return application;
            }
            catch
            {
                await application.DisposeAsync();
                throw;
            }
        }

        private static string FindRepositoryRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Yodaphone.sln")))
                {
                    return directory.FullName;
                }
            }

            throw new DirectoryNotFoundException("Could not locate Yodaphone.sln from the test output directory.");
        }

        private static int GetAvailablePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private void CaptureOutput(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (output)
            {
                output.AppendLine(line);
            }
        }

        private async Task WaitUntilReadyAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var timeout = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < timeout)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The test application exited before becoming ready.{Environment.NewLine}{GetOutput()}");
                }

                try
                {
                    using var response = await client.GetAsync(BaseAddress);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                }
                catch (TaskCanceledException) when (!process.HasExited)
                {
                }

                await Task.Delay(200);
            }

            throw new TimeoutException(
                $"The test application did not become ready.{Environment.NewLine}{GetOutput()}");
        }

        private string GetOutput()
        {
            lock (output)
            {
                return output.ToString();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process.Dispose();
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
