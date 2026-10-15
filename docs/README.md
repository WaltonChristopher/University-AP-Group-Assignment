# Yodaphone AI-powered Customer Service Chatbot

## Project Overview

This project is part of an assignment for Cranfield University Digital and Technology Solutions apprentices. A detailed description of the task, given in the assignment brief, can be found under the heading `The Assignment Task`.

## Dependencies

This project requires the following dependencies:

| Dependency | Version |
| --- | --- |
| .NET SDK | 9.0 |
| ASP.NET Core | 9.0 |
| Blazor | 9.0 |
| Docker | 24.0 or later |
| Docker Compose | 2.20 or later |
| NuGet packages | Versions pinned in the project `.csproj` files |

The .NET, ASP.NET Core, and Blazor versions must match the SDK specified by
`global.json` (if present). NuGet package versions are managed centrally by the
solution/project files and should be restored with `dotnet restore Yodaphone.sln`.

### Note

The Copilot SDK version as specified in `Yodaphone.Web.csproj` is `1.0.13-preview.2`. This is required as it contains a workaround for a known dotnet issue on MacOS (https://github.com/dotnet/sdk/issues/54309). As this is a preview version, any upgrades will need to be checked for compatibility before implementation.

**## Building and running the app**

### Setting appsettings.json

`appsettings.json` contains the following configurable values:

```json
{
  "Chat": {
    "Agent": "offline",
    "DevelopmentUserId": 42,
    "Copilot": {
      "ResponseTimeoutSeconds": 30
    }
  }
}
```

`Agent` represents which AI agent to use for chat replies. It may be `"offline"` or `"copilot"`. For the copilot agent, refer to the [Copilot setup](#github-copilot-authentication) below.

When `Agent` is `"copilot"`, a missing, empty, or failed reply automatically switches the current chat session to offline mode and saves an offline assistant reply. While the chat page is open, it checks for recovery every `ResponseTimeoutSeconds` seconds. A successful check restores Copilot mode and saves a message in the selected active conversation: "Connection to the Yodaphone chatbot has been restored. You can continue chatting with us." The automatic switch does not change `appsettings.json`. Configuring `Agent` as `"offline"` disables recovery checks.

Recovery uses the SDK's `ListModelsAsync` metadata request with a fresh client, without creating a chat session, sending a prompt, or generating a model response. The check and model metadata never enter customer history. Checks and their cached results are shared across browser sessions on each server, with at most one check per timeout interval and no overlapping checks. Leaving the page cancels its recovery loop. The SDK's `PingAsync` only checks the local CLI connection, so it cannot establish remote service availability. A model-list check is a non-inference availability signal; a later real reply can still fail, in which case the session falls back offline again.

Replies blocked by the response sanitisation rules are replaced with a canned explanation that the response could not be provided and an invitation to rephrase the question. The session stays in Copilot mode, so the next message still goes to Copilot. Only the explanation is saved and included in future chat history; the blocked content is discarded.

`Chat:Copilot:ResponseTimeoutSeconds` limits the entire Copilot request, including startup and cleanup (default `30`, allowed range `1`–`3600`). Requests that exceed this limit switch offline, even if the SDK is still finishing in the background. Caller cancellation cancels the request without switching modes.

Offline replies explain that the chat service is currently unavailable and direct users to `01632 960123` for support. This is a fictional UK landline in [Ofcom's reserved drama range](https://www.ofcom.org.uk/phones-and-broadband/phone-numbers/numbers-for-drama), used for this demonstration.

`DevelopmentUserId` is a placeholder value for the user's ID and should be replaced once we have user authentication working. It can be set to any positive integer.

First you will need to download and resolve NuGet packages:

```bash
dotnet restore Yodaphone.sln
```

Then build the solution:

```bash
dotnet build Yodaphone.sln
```

### Database setup

The application uses **Entity Framework Core with SQLite**. The database is stored locally at:

```text
src/Yodaphone.Web/Data/Yodaphone.db
```

The database schema is managed using Entity Framework Core migrations.

#### First-time setup

The Dev Container automatically restores the Entity Framework Core command-line tools during creation. For an existing container, select **Dev Containers: Rebuild Container** in VS Code, or run the following from the repository root. Developers working outside the container should also run this command:

```bash
dotnet tool restore
```

The tool version is pinned in `.config/dotnet-tools.json` to match the project's Entity Framework Core packages. Update both together when upgrading Entity Framework Core.

Verify the installation from the repository root:

```bash
dotnet ef --version
```

Create the initial database migration:

```bash
dotnet ef migrations add InitialCreate --project src/Yodaphone.Web/Yodaphone.Web.csproj
```

Apply the migration and create the local SQLite database:

```bash
dotnet ef database update --project src/Yodaphone.Web/Yodaphone.Web.csproj
```

This creates the `Yodaphone.db` SQLite database containing the `Users`, `Conversations`, and `Messages` tables.

#### Starting the application

Once the initial database has been created, the application can be started normally:

```bash
dotnet watch --project src/Yodaphone.Web --no-launch-profile
```

The application automatically checks for pending Entity Framework Core migrations during startup. Therefore, developers do not normally need to run `dotnet ef database update` each time the application is started.

#### Making database changes

When changes are made to the database entity classes, create a new migration:

```bash
dotnet ef migrations add <MigrationName> --project src/Yodaphone.Web/Yodaphone.Web.csproj
```

For example:

```bash
dotnet ef migrations add AddCustomerDetails --project src/Yodaphone.Web/Yodaphone.Web.csproj
```

The pending migration will be applied automatically when the application next starts.

### Database structure

The current database contains three main tables:

* **Users** — stores users and identifies whether they are support staff.
* **Conversations** — stores conversations belonging to users.
* **Messages** — stores messages belonging to conversations and identifies their sender.

The relationships are:

```text
User
 ├── Conversations
 └── Messages

Conversation
 ├── User
 └── Messages

Message
 ├── Sender → User
 └── Conversation → Conversation
```

Each message has both a `SenderId` and a `ConversationId`, associating it with a user record and the conversation it belongs to. The chatbot's current mapping of sender and role is described below.

#### Conversation persistence

`ChatService` stores conversations through `IConversationRepository`. The SQLite implementation maps domain GUIDs to the separate `Id` columns; the integer `ConversationId` and `MessageId` columns are database keys. 

Timestamps are stored in UTC, and the conversation's closed status and message order are restored on load.

Each call to `IChatService.StartConversationAsync()` creates a fresh conversation GUID owned by the same current user, retaining previous chats. `GetConversationsAsync()` returns that user's active and closed conversations with their messages, ordered by most recent activity first; it returns an empty list for a user with no chats. `GetConversationAsync(conversationId)` loads an individual chat owned by that user. Starting another conversation does not close the previous one; closing remains a separate operation.

The owner ID comes from `ICurrentUser`. Development currently uses `Chat:DevelopmentUserId` (default `1`); authentication can replace that identity source later. These backend methods are ready for a future chat history UI.

`Message.Status` stores the message role (`Customer`, `Assistant`, or `System`), while `SenderId` references the conversation owner for all three roles.

The conversation repository uses `IDbContextFactory<ApplicationDbContext>` to create and dispose a database context for each operation. This prevents a context and its tracked entities from living for the whole Blazor circuit. Customer messages are committed before requesting an AI reply, and assistant replies are saved afterward, so an AI failure or cancellation retains the customer message without holding a database context open during the network call.

### GitHub Copilot authentication

The browser does not authenticate directly with Copilot. The Blazor server starts the Copilot runtime, so configure a GitHub token for a Copilot-entitled service/developer account on the server. Do not place it in `appsettings.json`, JavaScript, or a browser cookie.

For local development, store it in user secrets:

```bash
dotnet user-secrets set "Chat:Copilot:GitHubToken" "YOUR_GITHUB_TOKEN" --project src/Yodaphone.Web
```

For a deployed app, set the equivalent environment variable in the server's secret store: `Chat__Copilot__GitHubToken`. The SDK gives this explicit token precedence over any local GitHub CLI/Copilot login. If no token is configured, local development can fall back to an existing logged-in GitHub CLI/Copilot runtime identity. *This fallback is not appropriate for a shared or production server.*

### Test coverage

In VS Code, select **Terminal → Run Task → Test: Coverage**. Alternatively, run this from the repository root in the dev container or a Bash terminal:

```bash
bash scripts/coverage.sh
```

The script restores the pinned local tools, runs both xUnit test projects with Coverlet, and combines their results using ReportGenerator. Each run uses a fresh results directory so previous results cannot inflate coverage. Generated files live under the git-ignored `coverage/` directory.

To view the results:

1. Open the Command Palette (`Ctrl+Shift+P`, or `Cmd+Shift+P` on macOS) and run **Coverage Gutters: Watch**. Open a C# source file under `src/Yodaphone.Web` to see green (covered), red (uncovered), and yellow (partially covered) marks.
2. Run **Coverage Gutters: Preview Coverage Report** for the combined HTML report. You can also open `coverage/report/index.html` in a browser.
3. Re-run **Test: Coverage** after changing code or tests to refresh the results.

The terminal prints line and branch coverage totals. Coverage measures the application assembly, excludes test assemblies and compiler-generated files under `obj`, and retains application startup and database migrations. Inspect the HTML report's file details to understand the overall percentage.

## The Assignment Task

### Task

- As part of a UK-based telecommunications company (your group must create a realistic company name and organisational profile), you have been appointed to the Software Development Team to develop an AI-enabled Customer Service Chatbot.
- The organisation is experiencing increasing volumes of customer enquiries, technical support requests, service-related issues, and customer complaints. Existing support processes are becoming inefficient, resulting in longer response times, reduced customer satisfaction, and increased operational costs. Senior management has commissioned your team to design and develop an intelligent software solution that can automate routine customer interactions, improve service delivery, and enhance organisational productivity.
- Your task is to design, develop, test, and evaluate a functional AI-based customer service chatbot that addresses identified business requirements and demonstrates the application of advanced programming concepts and professional software engineering practices.

**You are required to:**

- Define and justify the business problem clearly, including its relevance to organisational objectives and your role as a software developer.
- Conduct a requirements analysis to identify both functional and non-functional requirements of the proposed system.
- Design an appropriate software solution and produce suitable design artefacts, such as:
  - Use Case Diagrams
  - Class Diagrams
  - Sequence Diagrams
  - Activity Diagrams
  - System Architecture Diagrams
  - User Interface Wireframes
- Justify all design decisions by demonstrating how they contribute to system usability, maintainability, scalability, security and performance.
- Implement the solution using an appropriate programming language and development framework, adhering to recognised coding standards, software engineering principles and industry best practices.
- Demonstrate the application of advanced programming techniques, including:
  - GUI development
  - Object-Oriented Programming principles
  - Multithreading, concurrency control, or asynchronous programming
  - Networking and data communication
  - Data storage, retrieval, and management
  - Error and exception handling
  - Integration with AI services, APIs, or machine learning components (where appropriate)
- Demonstrate the effective use of professional advanced programming tools and practices, such as:
  - Version control systems (e.g., Git)
  - Build and deployment processes
  - Automated testing
  - Debugging and logging tools
  - Code reviews and documentation
- Develop and execute an appropriate testing strategy, providing evidence of:
  - Unit testing
  - Integration testing
  - User acceptance testing
  - Test results and evaluation
- Evaluate and test your solution, ensuring both functional and non-functional requirements are met.
- Deliver a 15-minute presentation showcasing your software solution, supported by a poster and a live demonstration. This should be aimed at both technical and non-technical audiences, highlighting the problem addressed, your design and implementation approach, and the impact of your solution.

## Contributors
- S44635

## AI Use Acknowledgement

ChatGPT 5.6 Sol, Terra, and ChatGPT 6 Astra (OpenAI, https://chatgpt.com/). Used for code generation and to assist with code reviews.

GitHub Copilot (auto/balanced model selection) (GitHub, https://github.com/features/copilot). Used for code completion within VSCode and for pull requests in GitHub.

Claude Sonnet 5.5, Haiku 4.5 (Anthropic, https://claude.ai). Used as a starting point for implementing UI wireframes in Blazor.
