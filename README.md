# Agentic Pipeline - AI-Powered Iterative Auto-Fix System

[![.NET](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
[![Claude AI](https://img.shields.io/badge/Claude-Sonnet%204.5-orange)](https://www.anthropic.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Automatically fix CI/CD pipeline failures using AI-powered root cause analysis and iterative retry logic.**

---

## 📋 Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Architecture](#architecture)
- [How It Works](#how-it-works)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Usage](#usage)
- [API Endpoints](#api-endpoints)
- [Troubleshooting](#troubleshooting)
- [Project Structure](#project-structure)
- [Contributing](#contributing)

---

## 🎯 Overview

**Agentic Pipeline** is an intelligent system that automatically diagnoses and fixes CI/CD pipeline failures using AI. When your build or test fails, the system:

1. **Analyzes** the failure logs using Claude AI
2. **Identifies** the root cause and affected files
3. **Generates** a fix automatically
4. **Validates** the fix before applying it
5. **Commits** the fix to a new branch atomically
6. **Waits** for the pipeline to run
7. **Retries** with accumulated context if the fix fails
8. **Creates a PR** when the pipeline succeeds

### Why Agentic Pipeline?

- ⚡ **Saves Time**: Automatically fixes common build/test failures
- 🔄 **Iterative**: Learns from failed attempts and retries with better context
- 🎯 **Accurate**: Uses AI to understand context, not just pattern matching
- 🔒 **Safe**: Validates fixes before committing, uses atomic commits
- 📊 **Intelligent**: Tracks failure patterns and similar issues
- 🤖 **Autonomous**: Works 24/7 without human intervention

---

## ✨ Key Features

### 🔍 Intelligent Root Cause Analysis (RCA)
- Uses **Claude AI** to analyze failure logs
- Identifies error type, root cause, and affected files
- Correlates with recent commits using git history
- Confidence scoring for reliability

### 🔧 Iterative Auto-Fix with Retry Logic
- **Single branch approach**: All retry attempts on same branch
- **Atomic commits**: All file changes in one commit (clean git history)
- **Accumulated context**: Each retry learns from previous failures
- **Smart validation**: Rejects unsafe or incorrect fixes
- **Adaptive thresholds**: Different confidence levels for different error types

### 🎯 Primary Error Focus
- Extracts THE primary error (not all warnings/secondary errors)
- Filters out build step names and configuration noise
- Focuses fixes on one issue at a time

### ✅ Multi-Phase Validation
1. **Error Extraction** (98%+ confidence required)
2. **Fix Generation** (70-97% confidence)
3. **Fix Validation** (65-90% confidence based on error type)
4. **Pre-Validation** (File relevance checks)

### 🔄 Session Management
- Tracks active fix sessions per repository
- Prevents duplicate sessions
- Waits for pipeline completion before next action
- Handles concurrent failures gracefully

### 📊 Pattern Recognition
- K-Means clustering to detect failure patterns
- Cosine similarity search for similar failures
- Learns from historical data (vector embeddings)

### 🌐 Multi-Platform Support
- ✅ **Azure DevOps** (primary)
- ✅ **GitHub Actions** (supported)
- Webhook-driven architecture

---

## 🏗️ Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                     CI/CD Platform                               │
│              (Azure DevOps / GitHub Actions)                     │
└────────────┬────────────────────────────────┬───────────────────┘
             │                                │
             │ Failure Webhook                │ Success Webhook
             ▼                                ▼
┌────────────────────────────────────────────────────────────────┐
│                   WebhookController                             │
│  - ReceiveAzureDevOpsWebhook()                                 │
│  - ReceiveGitHubWebhook()                                      │
└────────────┬───────────────────────────────────────────────────┘
             │
             ▼
┌────────────────────────────────────────────────────────────────┐
│                 PipelineOrchestrator                            │
│  1. Root Cause Analysis (RCA)                                  │
│  2. Commit Analysis                                             │
│  3. Similarity Search                                           │
│  4. Pattern Detection                                           │
└────────────┬───────────────────────────────────────────────────┘
             │
             ▼
┌────────────────────────────────────────────────────────────────┐
│          IterativeAutoFixOrchestrator                           │
│  - StartNewSessionAsync()      (initial failure)               │
│  - RetryOnExistingBranchAsync() (pipeline failed)              │
│  - HandleSuccessfulPipelineAsync() (pipeline succeeded)        │
└────────────┬───────────────────────────────────────────────────┘
             │
             ▼
┌────────────────────────────────────────────────────────────────┐
│                   AutoFixService                                │
│  Phase 1: Error Extraction                                      │
│  Phase 2: Fix Generation with Refinement (3 attempts)          │
│  Phase 3: Validation                                            │
└────────────┬───────────────────────────────────────────────────┘
             │
             ▼
┌────────────────────────────────────────────────────────────────┐
│                  GitHub API / Git Data API                      │
│  - Create atomic commit (all files at once)                    │
│  - Create branch autofix/session-{timestamp}                   │
│  - Wait for pipeline (NO PR yet)                               │
└─────────────────────────────────────────────────────────────────┘
```

### Data Flow

```
Master Build Fails
    ↓
Webhook → RCA → Fix Generation → Validation
    ↓
Create Branch + Atomic Commit
    ↓
Mark Session as "WAITING FOR PIPELINE"
    ↓
Pipeline Runs on autofix/* branch
    ↓
┌────────────────┬─────────────────┐
│                │                 │
▼                ▼                 ▼
PASS            FAIL              TIMEOUT
│                │                 │
Create PR       Retry             Mark Failed
                (accumulated      (max retries
                 context)          reached)
```

---

## 🚀 How It Works

### Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ Step 1: Initial Failure                                         │
│ Master branch build fails → Webhook arrives                     │
└────────────┬────────────────────────────────────────────────────┘
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 2: Root Cause Analysis                                     │
│ • Fetch full logs (no truncation)                               │
│ • Send to Claude AI                                             │
│ • Extract: Error Type, Root Cause, Affected Files               │
│ • Filter out: Build step names, secondary errors                │
└────────────┬────────────────────────────────────────────────────┘
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 3: Fix Generation (up to 3 attempts)                       │
│ • Phase 1: Extract exact file and line number                   │
│ • Phase 2: Generate fix with Claude                             │
│   - Fetch file contents from GitHub                             │
│   - Send to Claude with test failure guidance                   │
│   - Claude generates line changes                               │
│ • Phase 3: Validation                                            │
│   - Pre-validation: Check file relevance                        │
│   - Claude validation: Review fix quality                       │
│   - Calculate overall confidence                                │
└────────────┬────────────────────────────────────────────────────┘
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 4: Atomic Commit                                            │
│ • Create blobs for ALL changed files                            │
│ • Create single tree with all blobs                             │
│ • Create ONE commit                                              │
│ • Create branch: autofix/session-{timestamp}                    │
│ • Preserve line endings (no spurious diffs)                     │
└────────────┬────────────────────────────────────────────────────┘
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 5: Wait for Pipeline                                        │
│ Session marked as "WAITING FOR PIPELINE"                        │
│ System waits for webhook (no duplicate actions)                 │
└────────────┬────────────────────────────────────────────────────┘
             ▼
┌─────────────────────────────────────────────────────────────────┐
│ Step 6: Pipeline Completion                                      │
│                                                                  │
│ ┌──────────────┐        ┌──────────────┐                        │
│ │   FAILURE    │        │   SUCCESS    │                        │
│ └──────┬───────┘        └──────┬───────┘                        │
│        │                       │                                 │
│        ▼                       ▼                                 │
│ ┌──────────────┐        ┌──────────────┐                        │
│ │ RETRY        │        │ CREATE PR    │                        │
│ │ • Build      │        │ • Title with │                        │
│ │   context    │        │   attempts   │                        │
│ │ • New fix    │        │ • Session    │                        │
│ │ • Commit     │        │   history    │                        │
│ │ • Wait again │        │ • Mark done  │                        │
│ └──────────────┘        └──────────────┘                        │
└─────────────────────────────────────────────────────────────────┘
```

### Key Improvements

✅ **Test Failure Handling**:
- Fixes test expectations, not implementation
- Does NOT add fake data to pass tests
- Lower confidence threshold (0.65 vs 0.90)

✅ **Single Error Focus**:
- Fixes ONE error at a time
- Ignores secondary warnings
- No mixing test failures + config changes

✅ **File Relevance**:
- TestFailure should NOT modify .csproj files
- Pre-validation rejects unrelated changes

✅ **Line Ending Preservation**:
- Detects original line ending (LF/CRLF)
- Preserves it in modified files
- Clean diffs (only changed lines show)

---

## 🛠️ Getting Started

### Prerequisites

- **.NET 9.0 SDK** or later
- **AWS Account** (for Claude AI via Bedrock)
- **GitHub Account** (for repository access)
- **Azure DevOps** or **GitHub Actions** (for CI/CD)

### Quick Start

1. **Clone the repository**:
   ```bash
   git clone https://github.com/yourusername/agentic-pipeline.git
   cd agentic-pipeline
   ```

2. **Configure AWS credentials**:
   ```bash
   aws configure sso
   # OR
   export AWS_ACCESS_KEY_ID=your-key
   export AWS_SECRET_ACCESS_KEY=your-secret
   export AWS_REGION=us-east-1
   ```

3. **Configure GitHub token**:
   ```bash
   # Create token with: repo, workflow permissions
   export GITHUB_TOKEN=ghp_your_token_here
   ```

4. **Update `appsettings.json`**:
   ```json
   {
     "GitHub": {
       "Token": "ghp_your_token_here",
       "DefaultOwner": "your-username",
       "DefaultRepo": "your-repo"
     },
     "AutoFixSettings": {
       "EnableAutoFix": true,
       "EnableIterativeRetry": true,
       "MaxBranchRetries": 3
     }
   }
   ```

5. **Run the API**:
   ```bash
   dotnet build
   dotnet run --project src/AgenticPipeline.Api/AgenticPipeline.Api.csproj
   ```

   API starts on: `https://localhost:7056`

### Configure Webhooks

#### Azure DevOps:
1. Project Settings → Service Hooks → + Create subscription
2. Event: **Build completed**
3. Filters: Status = `Succeeded`, `Failed`
4. URL: `https://your-api-url/api/webhook/azuredevops`

#### GitHub Actions:
1. Repository Settings → Webhooks → Add webhook
2. Payload URL: `https://your-api-url/api/webhook/github`
3. Events: **Workflow runs**

#### Configure Pipeline Triggers:

**Azure Pipelines**:
```yaml
trigger:
  branches:
    include:
    - master
    - main
    - autofix/*  # ← REQUIRED
```

**GitHub Actions**:
```yaml
on:
  push:
    branches:
      - master
      - 'autofix/**'  # ← REQUIRED
```

---

## ⚙️ Configuration

### Key Settings

```json
{
  "GitHub": {
    "Token": "ghp_...",           // GitHub PAT
    "DefaultOwner": "your-org",
    "DefaultRepo": "your-repo"
  },

  "Claude": {
    "Region": "us-east-1",
    "Model": "us.anthropic.claude-sonnet-4-5-20250929-v1:0",
    "MaxTokens": 4096
  },

  "AutoFixSettings": {
    "EnableAutoFix": true,               // Master switch
    "EnableIterativeRetry": true,        // Allow retries
    "MaxBranchRetries": 3,               // Max attempts per session
    "MaxFixRefinementAttempts": 3,       // Fix generation retries
    "MinConfidenceForCommit": 0.9,       // Threshold (non-test errors)
    "BaseBranch": "master"
  },

  "AzureDevOps": {
    "PAT": "your-pat",                   // Azure DevOps PAT
    "Organization": "your-org",
    "Project": "your-project"
  }
}
```

**Note**: TestFailure errors automatically use **0.65 threshold** (lower risk).

---

## 💻 Usage

### Automatic Trigger

Auto-fix triggers when:
1. Build/test fails on `master`/`main` branch
2. Webhook received by API
3. RCA confidence > 0.8

### Monitor Sessions

Check logs:
```
info: Created auto-fix session {SessionId} on branch autofix/session-{timestamp}
info: Session marked as WAITING FOR PIPELINE
info: Session pipeline completed with failure. Processing retry...
info: Created PR #{PrNumber}
```

### Expected PR

```markdown
Title: [Auto-Fix] Fix applied after 2 attempt(s)

Body:
## Auto-Fix Session Summary

**Session ID**: abc-123
**Branch**: autofix/session-20260428-205630
**Total Attempts**: 2

## Fix Attempts

### Attempt 1
**Error Type**: TestFailure
**Fix Applied**: Changed test expectation from 21 to 20
**Why It Failed**: Validation rejected - confidence too low

### Attempt 2
**Error Type**: TestFailure
**Fix Applied**: Updated test expectation with better context
**Result**: Pipeline passed ✅
```

---

## 📡 API Endpoints

### Webhooks

```
POST /api/webhook/azuredevops    # Azure DevOps webhooks
POST /api/webhook/github         # GitHub webhooks
```

### Management

```
GET  /api/pipeline/orchestration/{id}     # Get orchestration result
GET  /api/pipeline/failures                # Get failure history
GET  /api/pipeline/failures/{id}/similar  # Find similar failures
GET  /api/pipeline/patterns                # Get failure patterns
```

---

## 🔧 Troubleshooting

### Common Issues

**1. Session not detecting pipeline completion**
- ✅ Check webhook configured for SUCCESS and FAILURE
- ✅ Verify branch name matches (no `refs/heads/`)
- ✅ Ensure `autofix/*` in pipeline triggers

**2. Entire file showing as changed in PR**
- ✅ Fixed! Restart API (line ending preservation now working)

**3. Validation rejecting all fixes**
- ✅ Fixed! TestFailure now uses 0.65 threshold

**4. Multiple branches for same failure**
- ✅ Check `AutoFixSessionManager` is Singleton
- ✅ Verify orchestration IDs are different

### Debug Logs

```bash
# Find sessions
grep "Created auto-fix session" logs/app.log

# Find retries
grep "Processing retry" logs/app.log

# Find PRs
grep "Created PR" logs/app.log
```

---

## 📁 Project Structure

```
AgenticPipeline/
├── src/
│   ├── AgenticPipeline.Api/              # Web API
│   │   ├── Controllers/
│   │   │   └── WebhookController.cs      # Webhooks
│   │   ├── Program.cs                    # DI setup
│   │   └── appsettings.json              # Config
│   │
│   ├── AgenticPipeline.Core/             # Domain models
│   │   ├── Interfaces/
│   │   │   ├── IAutoFixService.cs
│   │   │   └── IRcaService.cs
│   │   └── Models/
│   │       ├── AutoFixSession.cs         # Session state
│   │       ├── ErrorAnalysis.cs          # Error details
│   │       └── ...
│   │
│   └── AgenticPipeline.Infrastructure/   # Implementation
│       ├── Services/
│       │   ├── AutoFixService.cs                    # Core fix logic
│       │   ├── IterativeAutoFixOrchestrator.cs      # Retry orchestration
│       │   ├── AutoFixSessionManager.cs             # Session management
│       │   ├── ErrorExtractionService.cs            # Error extraction
│       │   ├── FixValidationService.cs              # Fix validation
│       │   └── PipelineOrchestrator.cs              # Main orchestrator
│       ├── Plugins/
│       │   ├── RcaPlugin.cs                         # RCA
│       │   └── CommitAnalyzerPlugin.cs              # Commit analysis
│       └── Repositories/
│           └── FailureRepository.cs                 # Data access
│
├── docs/                                  # Documentation
└── README.md                              # This file
```

---

## 📊 Performance

### Token Usage (per fix)

| Operation | Tokens | Cost (Sonnet) |
|-----------|--------|---------------|
| RCA | 30K-50K | $0.15-0.25 |
| Fix Generation | 5K-15K | $0.05-0.10 |
| Validation | 3K-8K | $0.02-0.05 |
| **Total** | **38K-73K** | **$0.22-0.40** |

With retries (3 attempts): ~$0.66-1.20 per failure

### Processing Time

- Webhook → RCA: 5-15s
- Fix Generation: 10-30s
- Validation: 5-10s
- Git Operations: 2-5s
- **Total**: 22-60 seconds per attempt

---

## 🤝 Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/amazing-feature`
3. Commit changes: `git commit -m 'Add amazing feature'`
4. Push: `git push origin feature/amazing-feature`
5. Open Pull Request

---

## 📝 License

This project is licensed under the MIT License.

---

## 🙏 Acknowledgments

- **Claude AI** (Anthropic) - Intelligent RCA and fix generation
- **GitHub API** - Git Data API for atomic commits
- **Azure DevOps** - Webhook integrations
- **.NET Team** - Excellent tooling

---

## 📧 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/agentic-pipeline/issues)
- **Documentation**: See `/docs` folder
- **Email**: your.email@example.com

---

## 🗺️ Roadmap

- [ ] GitLab/Jenkins support
- [ ] Web UI for monitoring
- [ ] Slack/Teams notifications
- [ ] Custom validation rules
- [ ] Fix templates library
- [ ] Performance metrics dashboard

---

**Built with ❤️ using .NET 9.0 and Claude AI**

---

## ✅ Quick Start Checklist

- [ ] Install .NET 9.0 SDK
- [ ] Configure AWS credentials
- [ ] Create GitHub PAT
- [ ] Update `appsettings.json`
- [ ] Configure webhooks
- [ ] Add `autofix/*` to pipeline triggers
- [ ] Run: `dotnet run`
- [ ] Test with failing build
- [ ] Watch the magic! ✨
