# Task List - Agentic Pipeline Troubleshooter

**Last Updated:** 2026-04-01
**Current Status:** ✅ Phase 1 Complete, Tested & Pushed to Git
**Current Phase:** Ready for Phase 2 - Commit Analysis

---

## ✅ Completed Tasks - Phase 1

### 1. AWS Bedrock Setup ✅
**Status:** ✅ Complete

#### A. Enable Claude Models in AWS Console
- [x] Go to AWS Bedrock Console
- [x] Navigate to **Model access** (left sidebar)
- [x] Find and enable:
  - ✅ **Claude Sonnet 4.6** (`us.anthropic.claude-sonnet-4-6`)
  - ✅ **Claude Opus 4.6** (`us.anthropic.claude-opus-4-6-v1`)
  - ✅ **Claude 3.7 Sonnet** (`us.anthropic.claude-3-7-sonnet-20250219-v1:0`)
- [x] Verified model access in region **us-west-2**

#### B. AWS SSO Authentication Setup
- [x] Using federated SSO
- [x] Created `set-sso-creds-persistent.ps1` to extract SSO credentials
- [x] Set USER-level environment variables for persistent credentials
- [x] Automated Visual Studio launch after credential setup
- [x] Region: **us-west-2**

#### C. AWS Bedrock Inference Profiles
- [x] Discovered inference profile requirement for newer models
- [x] Updated model IDs to use `us.` prefix
- [x] Available profiles verified

---

### 2. Test Bedrock Connection ✅
**Status:** ✅ Complete

#### Completed Steps:
- [x] Created test console application
- [x] Added AWS SDK packages: `AWSSDK.BedrockRuntime`, `AWSSDK.Core`
- [x] Implemented credential detection (environment variables, AWS profile)
- [x] Added credential validation before API calls
- [x] Implemented automatic model fallback
- [x] Successfully connected to Claude Sonnet 4-6
- [x] Verified response: "AWS Bedrock connection successful!"
- [x] Token usage tracked: Input 36 tokens, Output 66 tokens

---

### 3. Create Main .NET Solution Structure ✅
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
- [x] Manual API testing with Swagger UI
- [x] API tested and verified working by user

---

### 4. Model Refactoring ✅
**Status:** ✅ Complete

#### A. Extract Models to Centralized Location
- [x] Extract `ClaudeSettings` from `ClaudeService.cs` to `Core/Models/ClaudeSettings.cs`
- [x] Extract `ClaudeRequest` and `ClaudeResponse` to `Core/Models/ClaudeModels.cs`
- [x] Extract `RcaJsonResponse` from `RcaPlugin.cs` to `Core/Models/RcaJsonResponse.cs`
- [x] Update `TokenUsage` to record type in `RcaResult.cs`
- [x] Update all references across solution

#### B. Convert to Positional Records
- [x] Convert `ClaudeSettings` to positional record (with default constructor)
- [x] Convert `ClaudeRequest` to positional record (with optional SystemPrompt)
- [x] Convert `ClaudeResponse` to positional record (with computed TotalTokens)
- [x] Convert `RcaJsonResponse` to positional record
- [x] Convert `TokenUsage` to positional record (with computed TotalTokens)
- [x] Convert `RcaResult` to positional record
- [x] Convert `AnalysisRequest` to positional record (with default parameters)
- [x] Update all instantiation code to use positional syntax
- [x] Build successful: 0 warnings, 0 errors

---

### 5. Enhanced API Features ✅
**Status:** ✅ Complete

#### A. File Path Support
- [x] Update `AnalysisRequest` to accept both `LogContent` and `LogFilePath`
- [x] Implement file reading in `PipelineController`
- [x] Add comprehensive error handling for file operations:
  - FileNotFound
  - UnauthorizedAccess
  - IOException
- [x] Refactor into helper methods: `ResolveLogContentAsync`, `ReadLogFromFileAsync`

#### B. Error Handling
- [x] 400 Bad Request for validation errors
- [x] 500 Internal Server Error for processing failures
- [x] Detailed error messages in ProblemDetails format
- [x] Logging throughout request pipeline

---

### 6. Documentation & Testing ✅
**Status:** ✅ Complete

#### A. Testing Documentation
- [x] Create `docs/guides/Testing_Guide.md` (580+ lines)
  - Complete end-to-end testing instructions
  - AWS credentials setup
  - API startup guide
  - Swagger UI usage
  - Sample logs with expected responses
  - PowerShell, curl, Postman examples
  - Troubleshooting guide
- [x] Create `docs/guides/Quick_Start_Testing.md`
  - 5-minute quick start
  - Complete flow diagrams
  - Data flow visualization
  - Timeline showing analysis duration

#### B. Model Documentation
- [x] Create `docs/implementation/Model_Refactoring.md`
- [x] Create `docs/implementation/Positional_Records_Quick_Reference.md`
- [x] Create `docs/implementation/All_Models_Summary.md`
- [x] Create `docs/implementation/AnalysisRequest_Record_Conversion.md`

#### C. Project Documentation
- [x] Update main `README.md` with:
  - Phase 1 complete and tested status
  - Project structure
  - Quick start guide
  - API documentation
  - Testing status
- [x] Create `SECURITY_AUDIT.md`

---

### 7. Security Audit & Sanitization ✅
**Status:** ✅ Complete

#### A. Security Scan
- [x] Scan entire solution for hardcoded credentials
- [x] Verify no AWS keys, passwords, tokens, API keys
- [x] Check configuration files (appsettings.json, appsettings.Development.json)
- [x] Verify connection strings don't contain credentials

#### B. Personal Information Sanitization
- [x] Replace "anishuser1" with "YOUR_AWS_PROFILE" in:
  - `docs/guides/Testing_Guide.md` (2 instances)
  - `docs/guides/Quick_Start_Testing.md` (1 instance)
  - `tests/BedrockConnectionTest/README.md` (1 instance)
- [x] Replace "C:\Anish\Source\Agentic_Pipeline" with "<PROJECT_ROOT>" in:
  - `docs/guides/Testing_Guide.md` (4 instances)
  - `docs/guides/Quick_Start_Testing.md` (2 instances)
  - `tests/BedrockConnectionTest/README.md` (2 instances)
  - `docs/implementation/All_Models_Summary.md` (1 instance)
  - `docs/implementation/AnalysisRequest_Record_Conversion.md` (6 instances)

#### C. .gitignore Updates
- [x] Add `.claude/` directory
- [x] Add `*.env` and `.env.*` files
- [x] Add `appsettings.*.local.json` local configuration files
- [x] Add `**/appsettings.Local.json`

#### D. Security Documentation
- [x] Create `SECURITY_AUDIT.md` with:
  - Complete scan results
  - Sanitization changes
  - Verification commands
  - Final checklist
  - Recommendations

---

### 8. Git Repository ✅
**Status:** ✅ Complete - PUSHED

- [x] All security fixes applied
- [x] All sensitive information sanitized
- [x] Documentation updated with testing status
- [x] .gitignore properly configured
- [x] **Project pushed to git repository**

---

## 📊 Phase 1 Summary

### Completion Metrics:
- ✅ **Build:** 0 warnings, 0 errors
- ✅ **Testing:** End-to-end verified and working
- ✅ **Security:** 100% clean audit
- ✅ **Documentation:** Complete coverage
- ✅ **Git:** Clean repository, pushed to remote

### Technical Achievements:
- ✅ 7 immutable positional records
- ✅ 60-75% code reduction
- ✅ Dual input support (content or file path)
- ✅ Token usage tracking
- ✅ Confidence scoring
- ✅ Structured JSON responses
- ✅ Comprehensive error handling

### Time Spent:
**Total:** ~12 hours
- AWS Setup & Testing: 2-3 hours
- Solution Implementation: 6-7 hours
- Model Refactoring: 1-2 hours
- Testing & Documentation: 2-3 hours
- Security Audit: 1 hour

---

## 🚀 Phase 2 Tasks (Ready to Start)

### Phase 2: Commit Analysis

#### A. GitHub Integration
- [ ] Install Octokit.NET NuGet package
- [ ] Create `ICommitAnalyzer` interface in Core
- [ ] Implement GitHub authentication (Personal Access Token)
- [ ] Add GitHub configuration to appsettings.json
- [ ] Test GitHub API connection

#### B. Commit Analyzer Plugin
- [ ] Create `CommitAnalyzerPlugin.cs` in Infrastructure
- [ ] Implement `[KernelFunction] AnalyzeCommit()` method
- [ ] Parse commit data (files changed, diff, message)
- [ ] Extract code changes relevant to failure
- [ ] Correlate timestamps (failure vs commit time)

#### C. Enhanced RCA with Commit Context
- [ ] Update `RcaPlugin` to accept commit context
- [ ] Modify system prompt to include commit analysis
- [ ] Update `RcaResult` model with commit information
- [ ] Add API endpoint: `POST /api/pipeline/analyze-with-commits`

#### D. Database Setup
- [ ] Install Entity Framework Core packages
- [ ] Create `AgenticPipelineDbContext`
- [ ] Define entities: `FailureAnalysis`, `CommitSnapshot`
- [ ] Configure SQLite connection
- [ ] Create initial migration
- [ ] Add database configuration to appsettings.json

#### E. Testing & Documentation
- [ ] Test commit analysis with real GitHub repos
- [ ] Update README with Phase 2 features
- [ ] Create Phase 2 testing guide
- [ ] Update STATUS.md and task.md

**Estimated Time:** 1-2 weeks

---

## Future Phases (Reference Only)

### Phase 3: Historical Matching (Week 3)
- [ ] Set up vector embeddings (AWS Titan)
- [ ] Implement similarity search
- [ ] Build Fix Recommendation Plugin
- [ ] Store historical analysis results

### Phase 4: Orchestration (Week 3-4)
- [ ] Create webhook endpoint for CI/CD systems
- [ ] Build multi-plugin orchestrator
- [ ] Add retry logic and rate limiting
- [ ] Implement async processing

### Phase 5: Output Integrations (Week 4)
- [ ] GitHub PR comment service
- [ ] Markdown report templates
- [ ] Slack/Teams notifications (optional)
- [ ] Docker containerization
- [ ] End-to-end integration testing

---

## 📝 Notes & Reminders

### AWS Credentials Security
- **DO NOT** commit credentials to git
- **Current Setup:** AWS SSO with persistent USER-level environment variables
- **Script:** Run `set-sso-creds-persistent.ps1` when credentials expire
- **Token Lifetime:** SSO tokens typically expire after 8-12 hours

### AWS Bedrock Model Configuration
- **Primary Model:** `us.anthropic.claude-sonnet-4-6` (inference profile)
- **Region:** `us-west-2` (Oregon)
- **Important:** Use inference profile IDs (prefixed with `us.` or `global.`)

### Hackathon Timeline
- ✅ **Week 1:** AWS setup + Phase 1 (COMPLETE & TESTED)
- 🔜 **Week 2:** Phase 2 (Commit Analysis)
- 🔜 **Week 3:** Phase 3-4 (Historical Matching + Orchestration)
- 🔜 **Week 4:** Phase 5 (Integrations, Polish, Demo Prep)

---

## 🎯 Current Focus

**Status:** ✅ Phase 1 Complete, Tested, and Pushed to Git
**Next:** Phase 2 - Commit Analysis
**Priority:** GitHub integration and commit context in RCA

---

**Last Updated:** 2026-04-01
**Version:** 1.0.0 (Phase 1 Complete & Tested)
