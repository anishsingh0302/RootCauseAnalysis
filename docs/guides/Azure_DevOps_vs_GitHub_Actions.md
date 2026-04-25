# Azure DevOps Pipelines vs GitHub Actions

## Overview

Both Azure DevOps Pipelines and GitHub Actions are CI/CD platforms that automate build, test, and deployment workflows. While they serve similar purposes, they have different approaches, ecosystems, and use cases.

---

## Quick Comparison

| Aspect | Azure DevOps Pipelines | GitHub Actions |
|--------|------------------------|----------------|
| **Hosting** | Microsoft Azure | GitHub (Microsoft) |
| **Primary Focus** | Enterprise CI/CD | Developer workflows + CI/CD |
| **Configuration** | YAML or Classic UI | YAML only |
| **Location** | `.azure-pipelines.yml` (root) | `.github/workflows/*.yml` |
| **Free Tier** | 1,800 min/month (Microsoft-hosted) | 2,000 min/month (public repos unlimited) |
| **Runners** | Microsoft-hosted or self-hosted agents | GitHub-hosted or self-hosted runners |
| **Marketplace** | Azure DevOps Extensions | GitHub Actions Marketplace |
| **Integration** | Deep Azure integration | Deep GitHub integration |

---

## Core Concepts

### Azure DevOps Pipelines

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main

pool:
  vmImage: 'ubuntu-latest'

stages:
  - stage: Build
    jobs:
      - job: BuildJob
        steps:
          - task: DotNetCoreCLI@2
            inputs:
              command: 'build'
              projects: '**/*.csproj'

          - task: DotNetCoreCLI@2
            inputs:
              command: 'test'
              projects: '**/*Tests.csproj'
```

**Key Concepts:**
- **Pipelines**: Top-level container
- **Stages**: Major phases (Build, Test, Deploy)
- **Jobs**: Units of work that run on an agent
- **Steps**: Individual tasks/scripts
- **Tasks**: Reusable building blocks (from marketplace)
- **Agents**: Build/deployment machines

### GitHub Actions

```yaml
# .github/workflows/build.yml
name: Build and Test

on:
  push:
    branches:
      - main

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build
        run: dotnet build

      - name: Test
        run: dotnet test
```

**Key Concepts:**
- **Workflows**: Top-level automation
- **Events**: Triggers (push, pull_request, schedule, etc.)
- **Jobs**: Units of work that run on a runner
- **Steps**: Individual actions/commands
- **Actions**: Reusable units (from marketplace)
- **Runners**: Execution machines

---

## Detailed Comparison

### 1. Triggers & Events

#### Azure DevOps
```yaml
trigger:
  branches:
    include:
      - main
      - develop
    exclude:
      - feature/*
  paths:
    include:
      - src/*
    exclude:
      - docs/*

pr:
  branches:
    include:
      - main

schedules:
  - cron: "0 0 * * *"
    displayName: Daily midnight build
    branches:
      include:
        - main
```

**Available triggers:**
- Push to branches
- Pull requests
- Scheduled (cron)
- Manual (pipeline trigger)
- Resource triggers (another pipeline, repo, package)
- Webhooks (external services)

#### GitHub Actions
```yaml
on:
  push:
    branches:
      - main
      - develop
    paths:
      - 'src/**'
      - '!docs/**'

  pull_request:
    branches:
      - main

  schedule:
    - cron: '0 0 * * *'

  workflow_dispatch:  # Manual trigger

  workflow_run:       # Triggered by another workflow
    workflows: ["CI"]
    types:
      - completed
```

**Available triggers:**
- Push, Pull Request, Branch/Tag creation
- Issues, Issue comments, Pull request reviews
- Releases, Deployments
- Scheduled (cron)
- Manual (workflow_dispatch)
- Webhooks (repository_dispatch)
- Many more GitHub-specific events

**Winner**: **GitHub Actions** - More event types, especially for GitHub-specific events

---

### 2. Runner/Agent Environment

#### Azure DevOps

**Microsoft-hosted agents:**
- Ubuntu (20.04, 22.04, latest)
- Windows (2019, 2022, latest)
- macOS (11, 12, 13, latest)

**Configuration:**
```yaml
pool:
  vmImage: 'ubuntu-latest'
  # OR
  name: 'MyAgentPool'  # Self-hosted
```

**Self-hosted agents:**
- Register agent with Azure DevOps
- Can be Windows, Linux, macOS, or Docker
- Organized into agent pools
- Requires agent software installation

#### GitHub Actions

**GitHub-hosted runners:**
- Ubuntu (20.04, 22.04, latest)
- Windows (2019, 2022, latest)
- macOS (11, 12, 13, 14, latest)

**Configuration:**
```yaml
runs-on: ubuntu-latest
# OR
runs-on: [self-hosted, linux, x64]
```

**Self-hosted runners:**
- Register runner with GitHub
- Can be Windows, Linux, macOS, or Docker
- Assigned by labels
- Simpler setup than Azure DevOps agents

**Winner**: **Tie** - Both offer similar hosted options and self-hosted capabilities

---

### 3. Secrets & Variables

#### Azure DevOps

**Variable Groups:**
```yaml
variables:
  - group: 'Production-Variables'  # From library
  - name: BuildConfiguration
    value: Release
```

**Secrets:**
- Stored in Variable Groups (Library)
- Can be scoped to specific pipelines
- Can be linked to Azure Key Vault
- Access controlled by permissions

**Service Connections:**
- Dedicated connections to external services
- Azure subscriptions, Docker registries, Kubernetes, etc.
- Managed separately from pipelines

#### GitHub Actions

**Secrets:**
```yaml
steps:
  - name: Use secret
    run: echo "${{ secrets.MY_SECRET }}"
    env:
      API_KEY: ${{ secrets.API_KEY }}
```

**Storage:**
- Repository secrets (per repo)
- Environment secrets (per environment)
- Organization secrets (shared across repos)
- Dependabot secrets (for dependency updates)

**No dedicated service connections** - secrets are used directly

**Winner**: **Azure DevOps** - More structured approach with Key Vault integration and service connections

---

### 4. Reusability

#### Azure DevOps

**Templates:**
```yaml
# templates/build-template.yml
parameters:
  - name: buildConfiguration
    type: string
    default: 'Release'

steps:
  - task: DotNetCoreCLI@2
    inputs:
      command: 'build'
      configuration: ${{ parameters.buildConfiguration }}

# Main pipeline
stages:
  - template: templates/build-template.yml
    parameters:
      buildConfiguration: 'Release'
```

**Task Groups:**
- Reusable sequences of tasks
- Managed through UI
- Can be versioned

**Extensions:**
- Custom tasks
- Published to marketplace
- Written in Node.js or PowerShell

#### GitHub Actions

**Reusable Workflows:**
```yaml
# .github/workflows/reusable-build.yml
on:
  workflow_call:
    inputs:
      configuration:
        required: true
        type: string

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet build --configuration ${{ inputs.configuration }}

# Caller workflow
jobs:
  call-workflow:
    uses: ./.github/workflows/reusable-build.yml
    with:
      configuration: 'Release'
```

**Composite Actions:**
```yaml
# .github/actions/setup-app/action.yml
name: 'Setup Application'
description: 'Setup .NET and restore dependencies'
runs:
  using: "composite"
  steps:
    - uses: actions/setup-dotnet@v4
    - run: dotnet restore
      shell: bash
```

**Custom Actions:**
- JavaScript actions
- Docker container actions
- Published to marketplace

**Winner**: **GitHub Actions** - More flexible reusability patterns (composite actions, reusable workflows)

---

### 5. Approvals & Environments

#### Azure DevOps

**Environments:**
```yaml
stages:
  - stage: Production
    jobs:
      - deployment: DeployWeb
        environment: 'production'
        strategy:
          runOnce:
            deploy:
              steps:
                - script: echo "Deploying to production"
```

**Features:**
- Pre-deployment approvals
- Post-deployment approvals
- Branch policies
- Approval timeouts
- Approval history
- Checks (invoke Azure Functions, REST API, etc.)

**Very robust approval system** with:
- Multiple approvers
- Approval order (parallel/sequential)
- Auto-approval based on identity
- Integration with Azure resources

#### GitHub Actions

**Environments:**
```yaml
jobs:
  deploy:
    runs-on: ubuntu-latest
    environment:
      name: production
      url: https://example.com
    steps:
      - run: echo "Deploying to production"
```

**Features:**
- Required reviewers (1-6 people)
- Wait timer (delay deployment)
- Environment secrets
- Deployment branches (restrict which branches can deploy)
- Environment protection rules

**Simpler approval system:**
- Fewer options than Azure DevOps
- No post-deployment approvals
- Basic reviewer requirements

**Winner**: **Azure DevOps** - Much more sophisticated approval and gate system

---

### 6. Artifacts & Caching

#### Azure DevOps

**Publishing Artifacts:**
```yaml
- task: PublishBuildArtifacts@1
  inputs:
    PathtoPublish: '$(Build.ArtifactStagingDirectory)'
    ArtifactName: 'drop'
    publishLocation: 'Container'

- task: DownloadBuildArtifacts@0
  inputs:
    artifactName: 'drop'
```

**Pipeline Caching:**
```yaml
- task: Cache@2
  inputs:
    key: 'nuget | "$(Agent.OS)" | **/packages.lock.json'
    path: $(Pipeline.Workspace)/.nuget/packages
```

**Features:**
- Artifacts stored for 30 days (configurable)
- Can publish to Azure Artifacts (NuGet, npm, Maven feeds)
- File shares and Azure Storage

#### GitHub Actions

**Artifacts:**
```yaml
- name: Upload artifacts
  uses: actions/upload-artifact@v4
  with:
    name: build-output
    path: ./dist

- name: Download artifacts
  uses: actions/download-artifact@v4
  with:
    name: build-output
```

**Caching:**
```yaml
- name: Cache dependencies
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/packages.lock.json') }}
```

**Features:**
- Artifacts stored for 90 days (configurable)
- 500MB per artifact (free tier)
- Can publish to GitHub Packages

**Winner**: **GitHub Actions** - Simpler syntax, longer retention (90 vs 30 days default)

---

### 7. Matrix Builds

#### Azure DevOps

```yaml
strategy:
  matrix:
    Linux:
      vmImage: 'ubuntu-latest'
    Windows:
      vmImage: 'windows-latest'
    Mac:
      vmImage: 'macOS-latest'

pool:
  vmImage: $(vmImage)

steps:
  - script: dotnet build
```

#### GitHub Actions

```yaml
jobs:
  build:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        dotnet-version: ['7.0', '8.0', '9.0']

    runs-on: ${{ matrix.os }}

    steps:
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet-version }}
      - run: dotnet build
```

**Winner**: **Tie** - Both support matrix builds well

---

### 8. Integrations

#### Azure DevOps

**Native Integrations:**
- Azure (compute, storage, databases, etc.)
- Azure Active Directory
- Azure Boards (work items)
- Azure Repos (Git)
- Azure Test Plans
- Azure Artifacts

**Third-party:**
- Via marketplace extensions
- Service hooks
- REST API

**Best for:**
- Azure-heavy workloads
- Enterprise requiring full ALM solution
- Teams using Azure Boards for project management

#### GitHub Actions

**Native Integrations:**
- GitHub Issues, Pull Requests, Projects
- GitHub Packages
- GitHub Pages
- GitHub Releases
- GitHub Security (Dependabot, CodeQL)
- GitHub Discussions

**Third-party:**
- Massive marketplace (thousands of actions)
- Webhooks
- REST API / GraphQL API

**Best for:**
- GitHub-first development
- Open source projects
- Modern cloud-native applications

**Winner**: Depends on ecosystem
- **Azure DevOps** for Azure/Microsoft stack
- **GitHub Actions** for GitHub ecosystem and open source

---

### 9. Monitoring & Logging

#### Azure DevOps

**Features:**
- Pipeline analytics (built-in dashboard)
- Pass rate tracking
- Duration tracking
- Test results integration
- Work item tracking
- Customizable dashboards
- Integration with Azure Monitor

**Logs:**
- Detailed, structured logs
- Download logs
- Search within logs
- Retention based on retention policy

#### GitHub Actions

**Features:**
- Workflow insights (pass/fail rates, duration)
- Dependency graph for workflows
- Download logs
- Re-run failed jobs
- Integration with GitHub Insights

**Logs:**
- Structured logs with collapsible groups
- Live streaming
- Search within logs
- 90-day retention

**Winner**: **Azure DevOps** - More comprehensive analytics and monitoring capabilities

---

### 10. Pricing

#### Azure DevOps

**Free Tier:**
- **Public projects**: Unlimited build minutes (10 parallel jobs)
- **Private projects**:
  - 1,800 minutes/month (1 parallel job)
  - 5 users free
  - Additional users: $6/user/month

**Paid:**
- Microsoft-hosted: $40/parallel job/month
- Self-hosted: $15/parallel job/month (unlimited minutes)

#### GitHub Actions

**Free Tier:**
- **Public repositories**: Unlimited minutes
- **Private repositories**:
  - 2,000 minutes/month (Windows/Linux)
  - 3 concurrent jobs
  - macOS minutes count 10x

**Paid (per-minute billing after free tier):**
- Linux: $0.008/minute
- Windows: $0.016/minute
- macOS: $0.08/minute

**Winner**:
- **GitHub Actions** for public repos (unlimited)
- **Azure DevOps** for private heavy use (flat rate parallel jobs)

---

## YAML Syntax Comparison

### Simple Build & Test

**Azure DevOps:**
```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'

  - script: dotnet build
    displayName: 'Build'

  - script: dotnet test
    displayName: 'Test'
```

**GitHub Actions:**
```yaml
name: Build & Test

on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet build
      - run: dotnet test
```

### Multi-Stage Deployment

**Azure DevOps:**
```yaml
stages:
  - stage: Build
    jobs:
      - job: BuildJob
        steps:
          - script: dotnet build

  - stage: Deploy_Dev
    dependsOn: Build
    jobs:
      - deployment: DeployDev
        environment: development
        strategy:
          runOnce:
            deploy:
              steps:
                - script: echo "Deploy to dev"

  - stage: Deploy_Prod
    dependsOn: Deploy_Dev
    jobs:
      - deployment: DeployProd
        environment: production
        strategy:
          runOnce:
            deploy:
              steps:
                - script: echo "Deploy to prod"
```

**GitHub Actions:**
```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet build

  deploy-dev:
    needs: build
    runs-on: ubuntu-latest
    environment: development
    steps:
      - run: echo "Deploy to dev"

  deploy-prod:
    needs: deploy-dev
    runs-on: ubuntu-latest
    environment: production
    steps:
      - run: echo "Deploy to prod"
```

---

## When to Use Which?

### Use Azure DevOps Pipelines When:

✅ **You're heavily invested in Azure ecosystem**
- Using Azure services extensively
- Need tight integration with Azure resources
- Using Azure Active Directory for auth

✅ **You need enterprise-grade ALM**
- Using Azure Boards for work tracking
- Need comprehensive test management
- Require advanced approval workflows

✅ **You have complex deployment scenarios**
- Multi-stage deployments with gates
- Complex approval workflows
- Need deployment groups

✅ **You prefer UI-based configuration**
- Classic pipelines (visual designer)
- Easier for non-YAML users

✅ **You need better cost predictability**
- High usage on private repos
- Flat rate for parallel jobs

### Use GitHub Actions When:

✅ **Your code is on GitHub**
- Natural integration
- No context switching
- Better developer experience

✅ **You're building open source**
- Unlimited minutes for public repos
- Huge marketplace of actions
- Community-driven ecosystem

✅ **You want modern, flexible workflows**
- Event-driven architecture
- More trigger types
- Better reusability patterns

✅ **You prefer simplicity**
- Easier to get started
- Simpler YAML syntax
- Less ceremony

✅ **You need GitHub-specific features**
- Integration with Issues, PRs, Projects
- GitHub Packages
- GitHub Security features

---

## Migration Considerations

### Azure DevOps → GitHub Actions

**Challenges:**
- Service connections → Secrets/OIDC
- Variable groups → Repository/org secrets
- Classic pipelines → Must rewrite in YAML
- Azure Artifacts → GitHub Packages or external
- Work item integration → GitHub Issues

**Tools:**
- GitHub Actions Importer (CLI tool for conversion)
- https://github.com/github/gh-actions-importer

### GitHub Actions → Azure DevOps

**Challenges:**
- Actions → Tasks (may need custom extensions)
- Reusable workflows → Templates
- GitHub-specific events → Custom webhooks
- GitHub Packages → Azure Artifacts

**Tools:**
- Manual conversion required
- No official migration tool

---

## Summary Table

| Feature | Azure DevOps | GitHub Actions | Winner |
|---------|--------------|----------------|--------|
| Ease of Setup | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| GitHub Integration | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| Azure Integration | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Azure DevOps |
| Enterprise Features | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Azure DevOps |
| Marketplace | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| Approvals/Gates | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Azure DevOps |
| Event Triggers | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| Reusability | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| Cost (Public) | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| Cost (Private) | ⭐⭐⭐⭐ | ⭐⭐⭐ | Azure DevOps |
| Learning Curve | ⭐⭐⭐ | ⭐⭐⭐⭐ | GitHub |
| Community | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |

---

## Conclusion

**Choose GitHub Actions if:**
- Your code is on GitHub
- You want modern, event-driven workflows
- You're building open source or have public repos
- You prefer simplicity and quick setup

**Choose Azure DevOps if:**
- You're heavily using Azure
- You need enterprise ALM features
- You require advanced approval workflows
- You have high usage on private repos

**Both are excellent choices** - the decision often comes down to your existing ecosystem and specific requirements rather than technical superiority.

---

## Your Current Project

For the **Agentic Pipeline** project, you're using **GitHub Actions** which is the right choice because:

✅ Your code is on GitHub (Semantic-Kernel repo)
✅ You leverage GitHub webhooks for triggering
✅ You can use `workflow_run` event for failure detection
✅ Simpler setup for webhook integration
✅ Better for open source/public repos

If this were an Azure-heavy enterprise project with Azure Boards integration, Azure DevOps Pipelines might be better suited.
