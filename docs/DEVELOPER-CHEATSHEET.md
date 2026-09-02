# Yodaphone developer cheatsheet

**This file has been AI generated**

This guide is for team members who are new to C#, .NET, Blazor, or multi-project applications. Keep it open while working. You do not need to memorise it.

## The five-minute mental model

- **C#** is the programming language.
- **.NET** is the SDK, runtime, and standard libraries used to build and run the application.
- **ASP.NET Core** is the web framework and local web server.
- **Blazor** lets us create browser interfaces using Razor components and C#.
- **Razor** files use the `.razor` extension and combine HTML with C#.
- **NuGet** is the package manager for .NET libraries.
- **A project** is a `.csproj` file plus its source files. It produces an application or library.
- **A solution** is a `.sln` file that groups related projects so they can be built and tested together.
- **The Dev Container** gives every developer the same Linux-based .NET environment.

Our browser and server both run on the developer's computer:

```text
Browser at http://localhost:5000
                |
                v
Yodaphone.Web in the Dev Container
  - Blazor components render the UI
  - ASP.NET Core hosts the application
  - C# handles events and business logic
```

Blazor is configured for **Interactive Server** rendering. C# executes on the server, while Blazor sends UI events and updates between the browser and server over a live connection.

## Current repository map

```text
Yodaphone.sln                         Groups all .NET projects
global.json                           Selects the .NET 10 SDK
.devcontainer/devcontainer.json       Defines the shared development environment
docs/README.md                         Project brief and environment setup
src/Yodaphone.Web/
  Yodaphone.Web.csproj                 Web project definition
  Program.cs                           Application startup and service registration
  appsettings.json                     Non-secret configuration
  Components/
    App.razor                          Root HTML and Blazor component
    Routes.razor                       Finds pages from their routes
    _Imports.razor                     Shared Razor namespaces and directives
    Layout/
      MainLayout.razor                 Page shell
      NavMenu.razor                    Navigation links
    Pages/
      Home.razor                       Home page
      Counter.razor                    Simple interactive example
      Weather.razor                    Data-loading example
  wwwroot/
    app.css                            Application-wide styles
```

Files under `bin/` and `obj/` are generated. Do not edit or commit them.

## First setup

1. Install Git, Docker Desktop, VS Code, and the VS Code **Dev Containers** extension.
2. Clone the repository.
3. Start Docker Desktop.
4. Open the repository folder in VS Code.
5. Open the Command Palette:
   - Windows/Linux: `Ctrl+Shift+P`
   - macOS: `Cmd+Shift+P`
6. Select **Dev Containers: Reopen in Container**.
7. Wait for package restoration and extension installation to finish.
8. Check that the bottom-left of VS Code indicates the Dev Container is active.

Use the terminal inside the Dev Container for all `dotnet` commands.

## Daily workflow

At the start of a work session:

```bash
git pull
dotnet restore Yodaphone.sln
dotnet build Yodaphone.sln --no-restore
dotnet watch --project src/Yodaphone.Web/Yodaphone.Web.csproj --no-launch-profile
```

Open <http://localhost:5000>. Stop the application with `Ctrl+C` in the terminal.

Before sharing work:

```bash
dotnet build Yodaphone.sln
dotnet test Yodaphone.sln
git status
```

`dotnet test` will have nothing to run until the solution contains a test project.

## Essential .NET commands

| Command | Purpose |
| --- | --- |
| `dotnet --version` | Show the SDK selected by `global.json` |
| `dotnet sln Yodaphone.sln list` | List projects in the solution |
| `dotnet restore Yodaphone.sln` | Download and resolve NuGet packages |
| `dotnet build Yodaphone.sln` | Compile every project |
| `dotnet test Yodaphone.sln` | Build and run all automated tests |
| `dotnet run --project src/Yodaphone.Web` | Run the web app once |
| `dotnet watch --project src/Yodaphone.Web --no-launch-profile` | Run and react to source changes |
| `dotnet clean Yodaphone.sln` | Remove normal generated build output |
| `dotnet format Yodaphone.sln` | Apply standard C# formatting |

The first useful line in an error is often above the final `Build failed` message. Read upward and look for a filename, line number, or error code.

## C# essentials

### Variables and common types

```csharp
string customerName = "Alex";
int retryCount = 0;
bool isConnected = true;
decimal accountBalance = 12.50m;
DateTimeOffset receivedAt = DateTimeOffset.UtcNow;
```

Use `var` when the type is obvious from the right-hand side:

```csharp
var message = new ChatMessage("Hello", DateTimeOffset.UtcNow);
```

### Nullable reference types

Nullable checking is enabled in this project:

```csharp
string requiredName = "Alex";
string? optionalReference = null;

if (optionalReference is not null)
{
    Console.WriteLine(optionalReference.Length);
}
```

Do not silence nullable warnings with `!` unless you can prove the value cannot be null.

### Conditions and loops

```csharp
if (isConnected)
{
    Console.WriteLine("Connected");
}
else
{
    Console.WriteLine("Offline");
}

foreach (var item in messages)
{
    Console.WriteLine(item.Text);
}
```

### Classes, records, and interfaces

Use a class for an object with behaviour or changing state:

```csharp
public sealed class Conversation
{
    public Guid Id { get; } = Guid.NewGuid();
    public List<ChatMessage> Messages { get; } = [];

    public void Add(ChatMessage message)
    {
        Messages.Add(message);
    }
}
```

Use a record for a small data value:

```csharp
public sealed record ChatMessage(string Text, DateTimeOffset SentAt);
```

Use an interface to describe behaviour without tying code to one implementation:

```csharp
public interface IChatAgent
{
    Task<string> SendAsync(string message, CancellationToken cancellationToken);
}
```

This will let us switch between a real Copilot integration and a local demonstration agent.

### Asynchronous code

Network, database, and AI calls should normally be asynchronous:

```csharp
public async Task<string> GetReplyAsync(
    string message,
    CancellationToken cancellationToken)
{
    var reply = await chatAgent.SendAsync(message, cancellationToken);
    return reply;
}
```

- Return `Task` when there is no result.
- Return `Task<T>` when the operation produces a result.
- Use `await`; do not call `.Result` or `.Wait()`.
- Pass a `CancellationToken` through long-running operations.

### Exceptions

Catch exceptions only when you can handle them, add useful context, or show a safe message:

```csharp
try
{
    await chatAgent.SendAsync(message, cancellationToken);
}
catch (HttpRequestException exception)
{
    logger.LogError(exception, "The chat service could not be reached");
    errorMessage = "The service is temporarily unavailable. Please try again.";
}
```

Do not show exception details, stack traces, credentials, or customer data to users.

## Razor and Blazor essentials

A Razor component combines markup and C#:

```razor
@page "/hello"

<PageTitle>Hello</PageTitle>

<h1>Hello, @name</h1>

<input @bind="name" />
<button class="btn btn-primary" @onclick="ClearName">Clear</button>

@code {
    private string name = "Customer";

    private void ClearName()
    {
        name = string.Empty;
    }
}
```

Important Razor syntax:

| Syntax | Meaning |
| --- | --- |
| `@page "/chat"` | Makes the component available at a URL |
| `@name` | Writes a C# value into the markup |
| `@code { ... }` | Contains the component's C# members |
| `@onclick="Method"` | Runs a method after a click |
| `@bind="value"` | Two-way binds an input to a C# value |
| `@if (...) { ... }` | Conditionally renders markup |
| `@foreach (...) { ... }` | Repeats markup for a collection |
| `@inject IService Service` | Requests a registered service |
| `[Parameter]` | Accepts data from a parent component |

Because global Interactive Server rendering is enabled, ordinary pages in this project can handle events without adding `@rendermode` to every page.

### Rendering a collection

```razor
@if (messages.Count == 0)
{
    <p>No messages yet.</p>
}
else
{
    @foreach (var message in messages)
    {
        <p>@message.Text</p>
    }
}
```

Use `@key` when rendering changing lists so Blazor can track each item:

```razor
@foreach (var message in messages)
{
    <p @key="message.Id">@message.Text</p>
}
```

### Component parameters

Child component:

```razor
<p class="message">@Text</p>

@code {
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;
}
```

Parent component:

```razor
<MessageBubble Text="Hello from Yodaphone" />
```

### Component lifecycle

Use `OnInitializedAsync` for initial asynchronous loading:

```csharp
protected override async Task OnInitializedAsync()
{
    messages = await conversationService.GetMessagesAsync();
}
```

Avoid doing network or database work in a component constructor.

## Adding a new page

1. Create `Components/Pages/Chat.razor`.
2. Give it a route with `@page "/chat"`.
3. Add a link in `Components/Layout/NavMenu.razor`.
4. Save while `dotnet watch` is running.
5. Visit <http://localhost:5000/chat>.

Starter page:

```razor
@page "/chat"

<PageTitle>Customer service chat</PageTitle>

<h1>Customer service chat</h1>

@foreach (var message in messages)
{
    <p @key="message">@message</p>
}

<label for="message">Message</label>
<input id="message" @bind="draft" />
<button class="btn btn-primary" @onclick="SendAsync" disabled="@isSending">
    @(isSending ? "Sending..." : "Send")
</button>

@if (!string.IsNullOrWhiteSpace(errorMessage))
{
    <p role="alert" class="text-danger">@errorMessage</p>
}

@code {
    private readonly List<string> messages = [];
    private string draft = string.Empty;
    private string? errorMessage;
    private bool isSending;

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(draft) || isSending)
        {
            return;
        }

        isSending = true;
        errorMessage = null;

        try
        {
            messages.Add(draft.Trim());
            draft = string.Empty;
            await Task.Delay(250);
        }
        catch (Exception)
        {
            errorMessage = "The message could not be sent.";
        }
        finally
        {
            isSending = false;
        }
    }
}
```

This is only a UI exercise. Move AI, networking, and storage logic into injected services rather than growing one very large component.

## Services and dependency injection

ASP.NET Core creates and supplies registered services. Register them in `Program.cs`:

```csharp
builder.Services.AddScoped<IChatAgent, DemoChatAgent>();
```

Request the service in a Razor component:

```razor
@inject IChatAgent ChatAgent
```

Then call it from the component:

```csharp
var reply = await ChatAgent.SendAsync(draft, cancellationToken);
```

Common lifetimes:

| Lifetime | One instance per | Typical use |
| --- | --- | --- |
| `AddTransient` | Request for the service | Small stateless helpers |
| `AddScoped` | Blazor user circuit/request scope | User-facing application services |
| `AddSingleton` | Entire application process | Shared immutable or thread-safe state |

Do not store individual customer state in a singleton.

## Configuration and secrets

Use `appsettings.json` for non-secret defaults:

```json
{
  "ChatAgent": {
    "Provider": "Demo"
  }
}
```

Use development secrets for credentials:

```bash
dotnet user-secrets init --project src/Yodaphone.Web
dotnet user-secrets set "ChatAgent:Copilot:Endpoint" "VALUE" --project src/Yodaphone.Web
dotnet user-secrets list --project src/Yodaphone.Web
```

Each developer configures their own secrets. Never commit API keys, access tokens, passwords, customer data, or `secrets.json`.

Read configuration through the options pattern rather than scattering configuration lookups across the application.

## Debugging in VS Code

1. Set a breakpoint by clicking next to a C# line number.
2. Open the Command Palette.
3. Select **Debug: Select and Start Debugging** or use the project's **Start New Instance** action.
4. Trigger the relevant action in the browser.
5. Inspect local variables and the call stack.

Useful logging:

```csharp
logger.LogInformation("Starting conversation {ConversationId}", conversationId);
logger.LogWarning("Chat provider did not respond within the timeout");
logger.LogError(exception, "Chat request failed");
```

Use structured placeholders as shown above. Do not build log messages by concatenating sensitive values.

## Testing basics

The project brief requires unit, integration, and user acceptance testing.

- **Unit test:** checks one class or behaviour without real networking or databases.
- **Integration test:** checks that several components work together, such as an HTTP endpoint and database.
- **User acceptance test:** records whether a realistic user journey meets an agreed requirement.

A basic xUnit test looks like:

```csharp
public sealed class MessageValidatorTests
{
    [Fact]
    public void Empty_message_is_rejected()
    {
        var validator = new MessageValidator();

        var result = validator.IsValid(string.Empty);

        Assert.False(result);
    }
}
```

Use the Arrange–Act–Assert structure:

1. **Arrange:** create inputs and dependencies.
2. **Act:** call the behaviour being tested.
3. **Assert:** verify the result.

Add a test project through VS Code by selecting the solution and creating an **xUnit Test Project**, then add the relevant project reference. Run all tests with `dotnet test Yodaphone.sln`.

## Git teamwork

Before starting work:

```bash
git switch main
git pull
git switch -c feature/short-description
```

While working:

```bash
git status
git diff
git add path/to/file
git commit -m "Add clear description of change"
```

Team rules:

- Work on a branch rather than directly on `main`.
- Keep commits small and focused.
- Pull before beginning and before opening a pull request.
- Review your own diff before committing.
- Do not commit `bin`, `obj`, secrets, local databases, or editor-specific files.
- Ask for review; explain what changed and how it was tested.
- Resolve merge conflicts collaboratively when code ownership is unclear.

## Common problems

### NuGet permission denied

If the error mentions `/home/vscode/.nuget`, rebuild the Dev Container. Its creation command repairs ownership of the mounted NuGet directory.

### `dotnet watch` looks for `obj\Debug` on Linux or macOS

This is a known .NET SDK issue. The project currently contains this workaround:

```xml
<IntermediateOutputPath>obj/$(Configuration)/</IntermediateOutputPath>
```

Do not remove it until the team has moved to an SDK containing the upstream fix.

### Application uses the wrong port

Run with `--no-launch-profile`. The Dev Container supplies port `5000` through `ASPNETCORE_HTTP_PORTS`.

### A package or SDK cannot be found

Check:

```bash
dotnet --version
dotnet nuget list source
dotnet restore Yodaphone.sln
```

### Changes are not appearing

1. Check that `dotnet watch` is still running.
2. Save the file.
3. Look for a build error in the terminal.
4. Press `Ctrl+R` in the watch terminal to restart the app.
5. Refresh the browser.

### The container configuration changed

Run **Dev Containers: Rebuild and Reopen in Container**. Reloading the VS Code window alone does not rebuild the container.

## Definition of done for a change

- The relevant requirement or acceptance criterion is clear.
- The code is understandable and uses meaningful names.
- Networking and database operations are asynchronous.
- Expected failures produce safe, useful messages.
- Customer data and secrets are not logged or committed.
- New behaviour has appropriate automated tests.
- `dotnet build Yodaphone.sln` succeeds.
- `dotnet test Yodaphone.sln` succeeds once tests exist.
- The feature has been tried in the browser.
- Documentation or diagrams are updated where appropriate.
- Another team member can review the change.

## Suggested learning order

1. Run the existing application and edit `Home.razor`.
2. Understand the interactive `Counter.razor` example.
3. Create a small routed Razor page.
4. Practise C# classes, records, collections, and nullable values.
5. Move logic from a component into an injected service.
6. Write a unit test for that service.
7. Call a mock HTTP service asynchronously.
8. Add persistence and integration tests.
9. Integrate the real AI provider behind an interface.

## Official references

- [.NET documentation](https://learn.microsoft.com/dotnet/)
- [C# documentation](https://learn.microsoft.com/dotnet/csharp/)
- [ASP.NET Core Blazor](https://learn.microsoft.com/aspnet/core/blazor/)
- [Blazor components](https://learn.microsoft.com/aspnet/core/blazor/components/)
- [Dependency injection in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection)
- [Unit testing C# with `dotnet test`](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
- [Safe storage of development secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)

