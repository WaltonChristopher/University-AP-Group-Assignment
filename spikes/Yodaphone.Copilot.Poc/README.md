# Yodaphone GitHub Copilot SDK proof of concept

This isolated console application verifies that a developer's GitHub Copilot
entitlement can provide customer-service responses before the SDK is connected
to the Blazor application.

It deliberately has no tools or access to Yodaphone application data. The
assistant can generate guidance, but it cannot inspect accounts or perform
actions.

## Prerequisites

- .NET 8 or later; this repository uses .NET 10.
- An active GitHub Copilot subscription, including Copilot Student.
- GitHub Copilot authentication.

The .NET SDK bundles its matching Copilot CLI runtime. Authentication can come
from an existing GitHub Copilot CLI sign-in or from the
`COPILOT_GITHUB_TOKEN` environment variable. Never put a token in this
repository.

This spike pins a preview SDK version because that is the current package
published by GitHub. Keep the spike isolated until the API has been evaluated
and the team is ready to absorb possible package updates.


## Initial setup from scratch for university developers

Make sure you have access to GitHub Education. 
   - In your GitHub account settings, under 'Emails', add your university email address
   - Apply for GitHub education: navigate to 'Billing and Licensing' -> 'Education benefits' and apply.

Create a fine-grained token for Copilot [using this link.](https://github.com/settings/personal-access-tokens/new?name=Yodaphone%20Copilot%20POC&description=Temporary%20token%20for%20the%20local%20Yodaphone%20proof%20of%20concept&copilot_requests=write) Set the permissions as follows:
- Expiration: suggest after the assingment is due (e.g. 60 days)
- Repository access: Public repositories
- Permissions: Copilot requests

Copy the token and store it somewhere safely and securely on your computer.

## Setup for each session

Each time you work on the assignment, you will need to set up and test your token. Run:
```
read -rsp "GitHub Copilot token: " COPILOT_GITHUB_TOKEN
printf '\n'
export COPILOT_GITHUB_TOKEN
```
Enter your access token when prompted.

To test you have set your token, run:
```
if [ -n "$COPILOT_GITHUB_TOKEN" ]; then
  echo "Copilot token is set"
else
  echo "Copilot token is not set"
fi
```

After you finish, unset the token: `unset COPILOT_GITHUB_TOKEN`.

## Run the proof of concept

From the repository root, restore and run:

```powershell
dotnet restore spikes/Yodaphone.Copilot.Poc/Yodaphone.Copilot.Poc.csproj
dotnet run --project spikes/Yodaphone.Copilot.Poc/Yodaphone.Copilot.Poc.csproj
```

Ask a harmless fictional question such as:

```text
My fictional Yodaphone has no mobile signal. What should I check first?
```

Enter `/exit` to stop.

## Authentication troubleshooting

First activate Copilot Student in the
[GitHub Education benefits page](https://github.com/settings/education/benefits).
If the bundled runtime cannot find a previous sign-in, use one of these
approaches:

1. Install GitHub Copilot CLI, run `copilot login`, and complete its browser
   device flow.
2. Create a fine-grained personal access token owned by your personal account
   with the **Copilot Requests** account permission. Set it only for the current
   terminal session.

PowerShell, without placing the token in command history:

```powershell
$copilotToken = Read-Host "GitHub token" -AsSecureString
$env:COPILOT_GITHUB_TOKEN = ConvertFrom-SecureString $copilotToken -AsPlainText
dotnet run --project spikes/Yodaphone.Copilot.Poc/Yodaphone.Copilot.Poc.csproj
Remove-Item Env:COPILOT_GITHUB_TOKEN
```

Bash, including the Dev Container terminal:

```bash
read -rsp "GitHub token: " COPILOT_GITHUB_TOKEN
export COPILOT_GITHUB_TOKEN
printf '\n'
dotnet run --project spikes/Yodaphone.Copilot.Poc/Yodaphone.Copilot.Poc.csproj
unset COPILOT_GITHUB_TOKEN
```

Classic personal access tokens beginning `ghp_` are not supported. A supported
fine-grained token begins `github_pat_`.

Do not paste a token into `Program.cs`, a tracked settings file, a command
shown in screenshots, or a shared terminal transcript.

## Success criteria

The spike succeeds when it:

1. Starts without a separate Copilot CLI installation.
2. Authenticates using the developer's Copilot entitlement.
3. Answers a fictional customer-service question.
4. Remembers context for a follow-up question in the same run.
5. Refuses to claim access to a real customer account.

If these checks succeed, the next step is to move the SDK interaction behind
the application's `IChatAgent` interface. This spike should remain separate
from production code.
