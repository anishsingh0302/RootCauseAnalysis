# Agentic Pipeline Troubleshooter - .NET Implementation Plan

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                   CI/CD System                           │
│         GitHub Actions / Azure DevOps                    │
└──────────────────────┬──────────────────────────────────┘
                       │ Webhook (on failure)
┌────────────────────────────────────────────────────────┐
│              ASP.NET Web API (.NET 8)                     │
│         REST endpoints + Webhook receiver                │
└──────┬─────────┬──────────┬──────────┬──────────────────┘
       │         │          │          │
┌─────────┐ ┌───────┐ ┌─────┐ ┌─────────────────────┐
│ Log      │ │ Commit │ │ RCA  │ │ Fix Recommendation   │
│ Ingestion│ │ Analyzer│ │Plugin│ │ Plugin               │
│ Plugin   │ │ Plugin │ │      │ │                      │
└──────┬───┘ └───┬────┘ └──┬───┘ └───┬──────────────────┘
       │         │          │          │
┌─────────────────────────────────────────────────────┐
│          Semantic Kernel (Orchestration)                  │
│        Plugins + Planners + AWS Bedrock (Claude)         │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ├──> GitHub PR Comment (Octokit)
                       ├──> Slack/Teams Notification (optional)
                       │
┌────────────────────────────────────────────────────────┐
│         Data Layer (EF Core + SQLite/SQL Server)         │
│       Pipeline logs, history, embeddings (Qdrant)        │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Tech Stack

| Layer              | Technology                                  |
|--------------------|---------------------------------------------|
| Language           | C# / .NET 8                                 |
| LLM Orchestration  | Microsoft Semantic Kernel                   |
| LLM Provider       | AWS Bedrock (Anthropic Claude)              |
| Vector Store       | Semantic Kernel Memory (volatile) / Qdrant  |
| Backend API        | ASP.NET Core Web API                        |
| Database           | SQLite (MVP) / SQL Server (production)      |
| Output/Integration | GitHub PR Comments (Octokit.NET)            |
| ORM                | Entity Framework Core                       |
| CI/CD Integration  | Octokit.NET (GitHub) / Azure DevOps SDK     |
| Containerization   | Docker + Docker Compose                     |
| Testing            | xUnit + Moq + Playwright (e2e)              |

---

## 3. Key Concepts You Need to Learn (Only the New Stuff)

Since you already know C#, .NET, and Angular, here's what's actually new:

### 3.1 Semantic Kernel (Core - Must Learn)
**What:** Microsoft's open-source SDK for integrating LLMs into .NET apps. Think of it as the bridge between your C# code and AI models.

**Key concepts:**
- **Kernel** -- The central object. You register AI services and plugins on it.
- **Plugins** -- C# classes with methods the AI can call (our agents become plugins).
- **Functions** -- Individual methods within plugins, decorated with `[KernelFunction]`.
- **Prompt Templates** -- Reusable prompts with `{{$variable}}` placeholders.
- **Planner** -- Lets the AI decide which plugins/functions to call and in what order.
- **Memory** -- Store and retrieve text using embeddings (for historical failure matching).

**NuGet packages:**
```
Microsoft.SemanticKernel
Microsoft.SemanticKernel.Plugins.Memory
```

**Note:** We'll use AWS Bedrock directly for LLM calls initially, then refactor to a custom Semantic Kernel connector later.

### 3.2 AWS Bedrock with Claude (Must Learn)
**What:** AWS's managed service for foundation models, including Anthropic's Claude models.

**What to learn:**
- AWS Bedrock basics (regions, model IDs, IAM permissions)
- Claude API format (messages array, system prompt, model parameters)
- AWS SDK for .NET (`AWSSDK.BedrockRuntime`)
- Invoke model API vs streaming

**Minimal example:**
```csharp
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text.Json;

var client = new AmazonBedrockRuntimeClient(
    awsAccessKeyId: "YOUR_ACCESS_KEY",
    awsSecretAccessKey: "YOUR_SECRET_KEY",
    Amazon.RegionEndpoint.USEast1 // or your preferred region
);

var messages = new[]
{
    new { role = "user", content = "Analyze this CI/CD error log and identify the root cause: " + errorLogText }
};

var requestBody = JsonSerializer.Serialize(new
{
    anthropic_version = "bedrock-2023-05-31",
    max_tokens = 4096,
    messages = messages
});

var request = new InvokeModelRequest
{
    ModelId = "anthropic.claude-sonnet-4-5-v1:0", // or claude-opus-4-6
    ContentType = "application/json",
    Accept = "application/json",
    Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(requestBody))
};

var response = await client.InvokeModelAsync(request);
using var reader = new StreamReader(response.Body);
var responseBody = await reader.ReadToEndAsync();
var result = JsonSerializer.Deserialize<dynamic>(responseBody);
Console.WriteLine(result.content[0].text);
```

**AWS Setup (you'll do this):**
1. Go to AWS Console → Bedrock → Model access
2. Request access to Anthropic Claude models (if not already done)
3. Get your AWS Access Key and Secret Key (IAM user with Bedrock permissions)
4. Choose your region (us-east-1 is recommended for Claude availability)
5. Note your model IDs:
   - `anthropic.claude-sonnet-4-5-v1:0` (faster, cheaper - recommended for most tasks)
   - `anthropic.claude-opus-4-6-v1:0` (more capable, use for complex RCA)

### 3.3 Embeddings & Vector Search (Learn During Phase 3)
**What:** Converting text to numbers so you can find "similar" text. Used to match current errors against historical ones.

**What to learn:**
- What embeddings are (text -> float array)
- AWS Bedrock embeddings: `amazon.titan-embed-text-v2:0` or Claude's built-in capabilities
- Semantic Kernel's `ISemanticTextMemory` interface
- Saving memories: `memory.SaveInformationAsync()`
- Searching: `memory.SearchAsync()`

**Note:** For embeddings, you can use:
- AWS Titan Embeddings (via Bedrock)
- Or Qdrant's built-in models
- Or OpenAI embeddings API (just for embeddings, not chat)

### 3.4 CI/CD Concepts (Background Knowledge)
**What to learn:**
- What GitHub Actions / Azure DevOps Pipelines are
- YAML pipeline definitions
- What pipeline logs look like (build, test, deploy stages)
- Common failure types: build errors, test failures, dependency issues, deployment failures

---

## 4. Module Breakdown (.NET Implementation)

### Module 1: Log Ingestion Plugin
**Purpose:** Fetch and parse pipeline logs.

```csharp
public class LogIngestionPlugin
{
    [KernelFunction, Description("Fetches pipeline logs from GitHub Actions")]
    public async Task<string> FetchGitHubLogs(string owner, string repo, long runId) { }

    [KernelFunction, Description("Parses raw log text into structured format")]
    public PipelineLog ParseLog(string rawLog) { }
}
```

**Key files:**
- `Plugins/LogIngestionPlugin.cs`
- `Connectors/GitHubConnector.cs`
- `Connectors/AzureDevOpsConnector.cs`
- `Models/PipelineLog.cs`
- `Parsers/LogParser.cs`

### Module 2: Commit Analyzer Plugin
**Purpose:** Analyze recent commits correlated with failures.

```csharp
public class CommitAnalyzerPlugin
{
    [KernelFunction, Description("Fetches recent commits from a repository")]
    public async Task<List<CommitInfo>> GetRecentCommits(string owner, string repo, int count) { }

    [KernelFunction, Description("Analyzes diffs to find suspicious changes")]
    public async Task<CommitAnalysis> AnalyzeCommits(List<CommitInfo> commits, string failureTime) { }
}
```

**Key files:**
- `Plugins/CommitAnalyzerPlugin.cs`
- `Connectors/GitConnector.cs`
- `Models/CommitInfo.cs`
- `Models/CommitAnalysis.cs`

### Module 3: RCA (Root Cause Analysis) Plugin
**Purpose:** Use LLM to determine probable root cause.

```csharp
public class RcaPlugin
{
    [KernelFunction, Description("Analyzes logs and commits to identify root cause")]
    public async Task<RcaResult> AnalyzeRootCause(
        string parsedLog,
        string commitAnalysis,
        string historicalMatches) { }
}
```

**Key files:**
- `Plugins/RcaPlugin.cs`
- `Prompts/RcaPrompts.cs`
- `Models/RcaResult.cs`

### Module 4: Fix Recommendation Plugin
**Purpose:** Suggest fixes based on root cause.

```csharp
public class FixRecommendationPlugin
{
    [KernelFunction, Description("Generates fix suggestions for the identified root cause")]
    public async Task<FixResult> SuggestFixes(string rootCause, string codeContext) { }
}
```

**Key files:**
- `Plugins/FixRecommendationPlugin.cs`
- `Prompts/FixPrompts.cs`
- `Models/FixResult.cs`

### Module 5: Conflict Detection Plugin
**Purpose:** Detect inconsistencies between commits and pipeline.

**Key files:**
- `Plugins/ConflictDetectionPlugin.cs`
- `Models/ConflictResult.cs`

### Module 6: Pipeline Orchestrator
**Purpose:** Coordinate all plugins in sequence.

```csharp
public class PipelineOrchestrator
{
    private readonly Kernel _kernel;

    public async Task<AnalysisReport> RunFullAnalysis(AnalysisRequest request)
    {
        // Step 1: Ingest logs
        // Step 2: Analyze commits
        // Step 3: Search historical failures (vector memory)
        // Step 4: Run RCA
        // Step 5: Generate fix recommendations
        // Step 6: Detect conflicts
        // Step 7: Compile final report
    }
}
```

**Key files:**
- `Orchestration/PipelineOrchestrator.cs`
- `Orchestration/AnalysisState.cs`

### Module 7: API Layer (ASP.NET Web API)
**Endpoints:**
- `POST /api/analyze` -- Trigger full pipeline analysis (manual trigger via Swagger)
- `GET /api/reports/{id}` -- Get analysis report
- `GET /api/reports` -- List past analyses
- `POST /api/webhook` -- Receive CI/CD failure webhooks (auto-trigger from GitHub/Azure DevOps)

**Key files:**
- `Controllers/AnalysisController.cs`
- `Controllers/ReportsController.cs`
- `Controllers/WebhookController.cs`
- `DTOs/AnalysisRequest.cs`
- `DTOs/AnalysisResponse.cs`

### Module 8: Output Integrations
**Purpose:** Deliver analysis results where developers already work.

**Features:**
- **GitHub PR Comments:** Post RCA and fix recommendations as PR comments
- **Markdown Reports:** Generate formatted reports saved to database
- **Slack/Teams Integration (Optional):** Send notifications on failure analysis
- **Email Reports (Optional):** Send summary to team

**Key files:**
- `Integrations/GitHubCommentService.cs`
- `Integrations/SlackNotificationService.cs` (optional)
- `Templates/RcaMarkdownTemplate.cs`

---

## 5. Project Structure

```
Agentic_Pipeline/
├── src/
│   ├── AgenticPipeline.Api/                    # ASP.NET Web API
│   │   ├── Controllers/
│   │   │   ├── AnalysisController.cs
│   │   │   ├── ReportsController.cs
│   │   │   └── WebhookController.cs
│   │   ├── DTOs/
│   │   │   ├── AnalysisRequest.cs
│   │   │   ├── AnalysisResponse.cs
│   │   │   └── WebhookPayload.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── AgenticPipeline.Api.csproj
│   │
│   ├── AgenticPipeline.Core/                   # Business logic & Plugins
│   │   ├── Plugins/
│   │   │   ├── LogIngestionPlugin.cs
│   │   │   ├── CommitAnalyzerPlugin.cs
│   │   │   ├── RcaPlugin.cs
│   │   │   ├── FixRecommendationPlugin.cs
│   │   │   └── ConflictDetectionPlugin.cs
│   │   ├── Orchestration/
│   │   │   ├── PipelineOrchestrator.cs
│   │   │   └── AnalysisState.cs
│   │   ├── Prompts/
│   │   │   ├── RcaPrompts.cs
│   │   │   └── FixPrompts.cs
│   │   ├── Models/
│   │   │   ├── PipelineLog.cs
│   │   │   ├── CommitInfo.cs
│   │   │   ├── RcaResult.cs
│   │   │   ├── FixResult.cs
│   │   │   ├── ConflictResult.cs
│   │   │   └── AnalysisReport.cs
│   │   ├── Templates/
│   │   │   └── RcaMarkdownTemplate.cs
│   │   └── AgenticPipeline.Core.csproj
│   │
│   ├── AgenticPipeline.Infrastructure/          # External integrations
│   │   ├── Connectors/
│   │   │   ├── GitHubConnector.cs
│   │   │   ├── AzureDevOpsConnector.cs
│   │   │   └── GitConnector.cs
│   │   ├── Integrations/
│   │   │   ├── GitHubCommentService.cs
│   │   │   └── SlackNotificationService.cs
│   │   ├── LLM/
│   │   │   └── ClaudeService.cs
│   │   ├── Parsers/
│   │   │   └── LogParser.cs
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Memory/
│   │   │   └── FailureMemoryStore.cs
│   │   └── AgenticPipeline.Infrastructure.csproj
│   │
├── tests/
│   ├── AgenticPipeline.Core.Tests/
│   ├── AgenticPipeline.Api.Tests/
│   └── AgenticPipeline.Infrastructure.Tests/
│
├── data/
│   └── sample_logs/
│       ├── github_actions_build_fail.log
│       ├── github_actions_test_fail.log
│       └── azure_devops_deploy_fail.log
│
├── AgenticPipeline.sln
├── docker-compose.yml
├── Dockerfile
├── .env.example
├── Plan.md
├── DotNet_Implementation_Plan.md
├── Session.md
└── requirement.md
```

---

## 6. Implementation Phases

### Phase 1: Foundation + First Agent (MVP Core)
**Goal:** Get a working end-to-end flow with one agent.

- [ ] Create .NET solution with 3 projects (Api, Core, Infrastructure)
- [ ] Set up AWS Bedrock client with credentials (appsettings.json or environment variables)
- [ ] Create a `ClaudeService` wrapper class for Bedrock API calls
- [ ] Create sample pipeline log files (mock data)
- [ ] Implement `LogParser` to extract errors from logs
- [ ] Implement `RcaPlugin` that calls Claude via `ClaudeService` for analysis
- [ ] Set up Semantic Kernel and register the `RcaPlugin`
- [ ] Create `POST /api/analyze` endpoint that accepts a log and returns RCA
- [ ] Test via Swagger UI

**Deliverable:** Paste a log -> get root cause analysis back via API.

**Refactor Later:** Once working, create a custom Semantic Kernel connector to integrate Bedrock more cleanly.

### Phase 2: Commit Analysis + Better RCA
**Goal:** Add git context to make RCA smarter.

- [ ] Implement `GitHubConnector` using Octokit.NET
- [ ] Implement `CommitAnalyzerPlugin`
- [ ] Enhance RCA prompts to include commit context
- [ ] Add `LogIngestionPlugin` to fetch logs from GitHub Actions API
- [ ] Set up EF Core + SQLite for storing analysis results

**Deliverable:** Provide a repo + run ID -> system fetches logs AND commits -> smarter RCA.

### Phase 3: Historical Matching + Fix Suggestions
**Goal:** Learn from past failures.

- [ ] Set up Semantic Kernel Memory with embeddings
- [ ] Store past failure analyses as memories
- [ ] Implement similarity search for matching current errors to historical ones
- [ ] Implement `FixRecommendationPlugin`
- [ ] Implement `ConflictDetectionPlugin`

**Deliverable:** System suggests fixes and flags conflicts, referencing similar past failures.

### Phase 4: Orchestrator + Full Pipeline
**Goal:** Wire all agents into a single automated flow.

- [ ] Build `PipelineOrchestrator` to run all plugins in sequence
- [ ] Add webhook endpoint for CI/CD system integration (`WebhookController`)
- [ ] Implement webhook signature verification (GitHub/Azure DevOps)
- [ ] Error handling and retry logic
- [ ] Background job processing (optional: Hangfire)

**Deliverable:** Webhook receives CI/CD failure → Full analysis runs automatically.

### Phase 5: Output Integrations & Polish
**Goal:** Deliver results to developers where they work.

- [ ] Implement `GitHubCommentService` to post analysis as PR comments
- [ ] Format RCA report as Markdown with syntax highlighting
- [ ] Add Slack/Teams webhook integration (optional)
- [ ] Configure webhook in GitHub Actions / Azure DevOps (you'll do this in AWS/GitHub console)
- [ ] Docker containerization
- [ ] Add comprehensive Swagger documentation
- [ ] Integration testing with sample webhooks

**Deliverable:** CI/CD fails → Bot posts detailed RCA + fixes as PR comment automatically.

### Phase 6 (Optional - Stretch Goals)
**If time permits:**
- [ ] Simple admin UI using Razor Pages (list analyses, view reports)
- [ ] Auto-generate fix PRs (create branch + commit suggested changes)
- [ ] Trend analysis (common failure patterns over time)
- [ ] Multi-repository support with centralized dashboard

---

## 7. Learning Path (What's New For You)

Since you already know C#, .NET, Angular, here's your focused learning list:

### Must Learn Before Starting (Levels 1-2)
1. **Semantic Kernel basics**
   - NuGet: `Microsoft.SemanticKernel`
   - Create a Kernel (without chat completion for now - we'll use AWS Bedrock directly)
   - Create a Plugin class with `[KernelFunction]`
   - Use prompt templates with variables
   - **Exercise:** Write a console app with a simple plugin

2. **AWS Bedrock with Claude**
   - NuGet: `AWSSDK.BedrockRuntime`
   - Understand AWS IAM credentials (Access Key + Secret Key)
   - Claude model IDs: `anthropic.claude-sonnet-4-5-v1:0`, `anthropic.claude-opus-4-6-v1:0`
   - Request/response format (messages array, system prompt)
   - Understand tokens, max_tokens, temperature
   - **Exercise:** Call Claude via Bedrock from a C# console app, send a prompt, print response

**AWS Setup Steps (you do this):**
   - AWS Console → Bedrock → Model access → Enable Claude models
   - AWS Console → IAM → Create user with `AmazonBedrockFullAccess` policy
   - Get Access Key ID and Secret Access Key
   - Choose region: `us-east-1` (recommended) or `us-west-2`

### Learn During Phase 2-3
3. **Prompt engineering**
   - System prompt vs user prompt
   - Giving examples (few-shot)
   - Asking for structured JSON output
   - **Exercise:** Write a prompt that takes an error log and returns JSON with `root_cause`, `confidence`, `fix`

4. **Octokit.NET (GitHub API)**
   - You already know HTTP/APIs, this is just a NuGet wrapper
   - Fetch commits, pull requests, workflow runs, logs
   - **Exercise:** Fetch last 10 commits from a public repo

5. **Embeddings & Semantic Memory**
   - What embeddings are, how similarity search works
   - `Microsoft.SemanticKernel.Plugins.Memory`
   - **Exercise:** Store 10 error strings, search for similar ones

### Learn During Phase 5
6. **SignalR** (if you haven't used it)
   - Real-time web communication
   - You may already know this from .NET/Angular work

---

## 8. NuGet Packages

```xml
<!-- Core Semantic Kernel -->
<PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />
<PackageReference Include="Microsoft.SemanticKernel.Plugins.Memory" Version="1.*-*" />

<!-- AWS Bedrock -->
<PackageReference Include="AWSSDK.BedrockRuntime" Version="3.*" />
<PackageReference Include="AWSSDK.Bedrock" Version="3.*" />
<PackageReference Include="AWSSDK.Core" Version="3.*" />

<!-- GitHub Integration -->
<PackageReference Include="Octokit" Version="13.*" />

<!-- Database -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.*" />

<!-- API -->
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />

<!-- Optional: Background jobs -->
<!-- <PackageReference Include="Hangfire.Core" Version="1.*" /> -->
<!-- <PackageReference Include="Hangfire.AspNetCore" Version="1.*" /> -->

<!-- JSON handling -->
<PackageReference Include="System.Text.Json" Version="8.*" />
```

---

## 9. Hackathon Strategy

Given 30 days, here's a practical approach:

**Week 1: Learn + Setup**
- Learn Semantic Kernel basics (console app experiments)
- Set up AWS Bedrock access (IAM, model access, test connection)
- Get Claude working via AWS SDK in a test console app
- Create the .NET solution structure
- Create sample log files

**Week 2: Core Agents**
- Implement Phase 1 (Log parsing + RCA via API)
- Implement Phase 2 (Commit analysis + GitHub connector)
- Have a working API you can demo via Swagger

**Week 3: Full Pipeline + Memory**
- Implement Phase 3 (Historical matching + Fix suggestions)
- Implement Phase 4 (Orchestrator wiring all plugins)
- End-to-end flow working

**Week 4: Integrations + Polish**
- Implement Phase 5 (GitHub PR comments, Slack notifications)
- Configure webhooks in GitHub/Azure DevOps
- Docker setup
- Bug fixes, end-to-end testing
- Demo preparation: record a video showing webhook → analysis → PR comment
- Prepare presentation/demo script
