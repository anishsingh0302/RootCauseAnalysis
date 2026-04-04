# Task List - Agentic Pipeline Troubleshooter

## Current Phase: Phase 1 - Main Project Structure

---

## Completed Tasks ✅

### 1. AWS Bedrock Setup
**Status:** ✅ Complete

#### A. Enable Claude Models in AWS Console
- [x] Go to [AWS Bedrock Console](https://console.aws.amazon.com/bedrock)
- [x] Navigate to **Model access** (left sidebar)
- [x] Find and enable:
  - ✅ **Claude Sonnet 4.6** (`us.anthropic.claude-sonnet-4-6`)
  - ✅ **Claude Opus 4.6** (`us.anthropic.claude-opus-4-6-v1`)
  - ✅ **Claude 3.7 Sonnet** (`us.anthropic.claude-3-7-sonnet-20250219-v1:0`)
- [x] Verified model access in region **us-west-2**

#### B. AWS SSO Authentication Setup
- [x] Using federated SSO with profile `anishuser1`
- [x] Created `set-sso-creds-persistent.ps1` to extract SSO credentials
- [x] Set USER-level environment variables for persistent credentials
- [x] Automated Visual Studio launch after credential setup
- [x] Account ID: `710271940792`
- [x] Role: `AIAccess-GL`
- [x] Region: **us-west-2**

#### C. AWS Bedrock Inference Profiles
- [x] Discovered inference profile requirement for newer models
- [x] Updated model IDs to use `us.` prefix
- [x] Available profiles verified: `us.anthropic.claude-sonnet-4-6`, `us.anthropic.claude-opus-4-6-v1`, etc.

---

### 2. Test Bedrock Connection
**Status:** ✅ Complete

**Location:** `C:\Anish\Source\Agentic_Pipeline\tests\BedrockConnectionTest`

#### Completed Steps:
1. [x] Created test console application
2. [x] Added AWS SDK packages: `AWSSDK.BedrockRuntime`, `AWSSDK.Core`
3. [x] Implemented credential detection (environment variables, AWS profile)
4. [x] Added credential validation before API calls
5. [x] Implemented automatic model fallback
6. [x] Successfully connected to Claude Sonnet 4-6
7. [x] Verified response: "AWS Bedrock connection successful! I am Claude and ready to help with CI/CD pipeline analysis."
8. [x] Token usage tracked: Input 36 tokens, Output 66 tokens

#### Key Files:
- `Program.cs` - Main test application with SSO support
- `set-sso-creds-persistent.ps1` - SSO credential extraction script
- `test-env.ps1` - Environment variable verification

---

## Current Tasks (Do Now)

### 3. Create Main .NET Solution Structure
**Status:** ✅ Complete

#### A. Solution and Project Setup
- [x] Create solution: `AgenticPipeline.sln` in project root
- [x] Create project: `AgenticPipeline.Api` (ASP.NET Core Web API)
- [x] Create project: `AgenticPipeline.Core` (Class Library)
- [x] Create project: `AgenticPipeline.Infrastructure` (Class Library)
- [x] Create project: `AgenticPipeline.Tests` (xUnit Test Project)
- [x] Set up project references:
  - Api → Core, Infrastructure
  - Infrastructure → Core
  - Tests → Core, Infrastructure

#### B. Install NuGet Packages
**AgenticPipeline.Api:**
- [x] `Microsoft.SemanticKernel` (1.74.0)
- [x] `Swashbuckle.AspNetCore` (10.1.7)

**AgenticPipeline.Infrastructure:**
- [x] `AWSSDK.BedrockRuntime` (4.0.17.1)
- [x] `AWSSDK.Core` (4.0.3.27)
- [x] `Microsoft.Extensions.Options` (10.0.5)
- [x] `Microsoft.SemanticKernel` (1.74.0)

#### C. Implement ClaudeService Wrapper
- [x] Create `Services/ClaudeService.cs` in Infrastructure
- [x] Create `IClaudeService` interface in Core
- [x] Implement methods:
  - `Task<string> SendMessageAsync(string message)`
  - `Task<ClaudeResponse> InvokeModelAsync(ClaudeRequest request)`
- [x] Add configuration for:
  - Model ID: `us.anthropic.claude-sonnet-4-6`
  - Region: `us-west-2`
  - Max tokens, temperature
- [x] Add error handling and retry logic
- [x] Add token usage tracking

#### D. Create Sample Pipeline Log Files
- [x] Create `SampleData/` folder
- [x] Add `build-failure-compilation.log` (compilation error example)
- [x] Add `build-failure-test.log` (test failure example)
- [x] Add `build-failure-dependency.log` (dependency issue example)

#### E. Build RCA Plugin (Root Cause Analysis)
- [x] Create `Plugins/RcaPlugin.cs` with Semantic Kernel
- [x] Implement method: `[KernelFunction] async Task<string> AnalyzeFailure(string logContent)`
- [x] Create prompt template for RCA
- [x] Test with sample logs
- [x] Output format: JSON with:
  - Root cause summary
  - Affected components
  - Error type classification
  - Confidence score
  - Recommendations
  - Token usage

#### F. Set Up API Endpoints
- [x] Create `Controllers/PipelineController.cs`
- [x] Endpoint: `POST /api/pipeline/analyze` - Analyze logs
- [x] Endpoint: `GET /api/pipeline/health` - Health check
- [x] Configure Swagger UI (at root URL)
- [x] Add request/response models in Core

#### G. End-to-End Testing
- [x] Build: SUCCESS (0 warnings, 0 errors)
- [x] Verify: All packages installed correctly
- [x] Verify: Project references working
- [x] Ready for: Manual API testing with Swagger UI

---

### 4. Documentation Updates
**Status:** 🟡 Ongoing
- [ ] Update `DotNet_Implementation_Plan.md` with actual model ID (`us.anthropic.claude-sonnet-4-6`)
- [ ] Document SSO credential setup process
- [ ] Add `README.md` in main project root with:
  - Quick start guide
  - Architecture diagram
  - API documentation
  - Configuration instructions

---

## Future Phases (Reference Only)

### Phase 2: Commit Analysis (Week 2)
- [ ] Implement GitHub connector
- [ ] Implement Commit Analyzer Plugin
- [ ] Enhance RCA with commit context
- [ ] Set up EF Core + SQLite

### Phase 3: Historical Matching (Week 3)
- [ ] Set up embeddings (AWS Titan or OpenAI)
- [ ] Implement vector search
- [ ] Build Fix Recommendation Plugin
- [ ] Build Conflict Detection Plugin

### Phase 4: Orchestration (Week 3-4)
- [ ] Build Pipeline Orchestrator
- [ ] Add webhook endpoint
- [ ] Webhook signature verification
- [ ] Error handling and retry logic

### Phase 5: Output Integrations (Week 4)
- [ ] GitHub PR comment service
- [ ] Markdown report templates
- [ ] Slack/Teams notifications (optional)
- [ ] Docker containerization
- [ ] End-to-end testing

---

## Notes & Reminders

### AWS Credentials Security
- **DO NOT** commit credentials to git
- **Current Setup:** AWS SSO with persistent USER-level environment variables
- **Script:** Run `set-sso-creds-persistent.ps1` when credentials expire
- **Token Lifetime:** SSO tokens typically expire after 8-12 hours
- For production deployment:
  - Use IAM roles (ECS/EC2)
  - Or AWS Secrets Manager
  - Never hardcode credentials

### AWS Bedrock Model Configuration
- **Primary Model:** `us.anthropic.claude-sonnet-4-6` (inference profile)
- **Region:** `us-west-2` (Oregon)
- **Important:** Use inference profile IDs (prefixed with `us.` or `global.`) for on-demand throughput
- **Fallback Models:**
  - `us.anthropic.claude-opus-4-6-v1` (most capable)
  - `us.anthropic.claude-3-7-sonnet-20250219-v1:0`
  - `anthropic.claude-3-sonnet-20240229-v1:0` (older, no prefix needed)

### SSO Credential Workflow
1. Run `set-sso-creds-persistent.ps1` with profile `anishuser1`
2. Script extracts credentials from `~/.aws/cli/cache/*.json`
3. Sets USER-level environment variables
4. Launches Visual Studio automatically
5. .NET app reads credentials from environment

### Hackathon Timeline
- **Week 1:** AWS setup + Phase 1 (basic RCA working)
- **Week 2:** Phase 2 (commit analysis)
- **Week 3:** Phase 3-4 (full pipeline working)
- **Week 4:** Phase 5 (integrations, polish, demo prep)

---

## Questions or Issues?

If you encounter problems:
1. Check the error message in the test app
2. Verify AWS Console settings (model access, IAM permissions)
3. Double-check credentials
4. Let me know the specific error and I'll help troubleshoot

---

---

## Summary

### ✅ What's Complete:
- AWS Bedrock access configured and verified
- SSO authentication working with credential extraction script
- Test application successfully connecting to Claude Sonnet 4-6
- Inference profile requirement identified and implemented
- Token usage tracking confirmed working
- Model ID: `us.anthropic.claude-sonnet-4-6`
- Region: `us-west-2`

### 🟡 What's Next:
1. Create main .NET solution structure (4 projects)
2. Implement ClaudeService wrapper
3. Build RCA Plugin with Semantic Kernel
4. Set up API endpoints with Swagger
5. Create sample pipeline logs
6. End-to-end testing

### ⏱️ Estimated Time for Phase 1:
- Solution structure: 30 minutes
- ClaudeService: 1-2 hours
- RCA Plugin: 2-3 hours
- API setup: 1-2 hours
- Testing: 1 hour
- **Total: ~1 day of focused work**

---

**Last Updated:** 2026-04-01 (Session 8 - Bedrock Connection Successful)
