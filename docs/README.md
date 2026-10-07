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

## Developer Setup

For ease of setup and consistency we are using a Docker development container. 

New to C#, .NET, or Blazor? Use the [Yodaphone developer cheatsheet](DEVELOPER-CHEATSHEET.md) for the daily workflow, code examples, debugging, testing, and common fixes.

### Required Software

- Git
- VS Code
  - VS Code `Dev Containers` extension

#### On Windows

- Windows Subsystem for Linux 2
  - Open powershell and run `wsl --install` then restart your computer
- Docker Desktop [Windows](https://www.docker.com/products/docker-desktop/)
  - Choose the WSL 2 instalation

#### On MacOS
- Docker Desktop for Mac

### Setting up the dev container

After installing the required software, make sure Docker is running, then reopen VS Code. You will be notified that you can open the project in a container; accept and the VS Code window will reload, download the container, and run it.
Alternatively, you can manually reopen in container by pressing `Ctrl+Shift+P` and selecting `Dev Containers: Reopen in Container`.

You will then need to set your Git global config variables by running:
`git config --global user.name ["Your name"]` and `git config --global user.email [Your email address]`

### Building and running the app

First you will need to download and resolve NuGet packages: `dotnet restore Yodaphone.sln`.

Then you can build the solution: `dotnet build Yodaphone.sln`.

Finally you can run the solution: `dotnet watch --project src/Yodaphone.Web --no-launch-profile`. This will update live as you make changes.

### GitHub Copilot authentication

The browser does not authenticate directly with Copilot. The Blazor server starts the
Copilot runtime, so configure a GitHub token for a Copilot-entitled service/developer
account on the server. Do not place it in `appsettings.json`, JavaScript, or a browser
cookie.

For local development, store it in user secrets:

```bash
dotnet user-secrets set "Chat:Copilot:GitHubToken" "YOUR_GITHUB_TOKEN" --project src/Yodaphone.Web
```

For a deployed app, set the equivalent environment variable in the server's secret
store: `Chat__Copilot__GitHubToken`. The SDK gives this explicit token precedence over
any local GitHub CLI/Copilot login. If no token is configured, local development can
fall back to an existing logged-in GitHub CLI/Copilot runtime identity; that fallback is
not appropriate for a shared or production server.

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