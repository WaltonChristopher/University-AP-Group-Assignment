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

Install the Entity Framework Core command-line tools:

```bash
dotnet tool install --global dotnet-ef
```

Verify the installation:

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

Each message therefore has both a `SenderId` and a `ConversationId`, allowing messages to be associated with both the user who sent them and the conversation they belong to.

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