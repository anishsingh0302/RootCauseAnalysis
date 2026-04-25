# Auto-Fix PR Creation - Testing Guide

## Prerequisites

### 1. Configuration Setup

Ensure your `appsettings.Development.json` has auto-fix enabled:

```json
{
  "Notifications": {
    "ApiBaseUrl": "https://your-ngrok-url.ngrok-free.dev",
    "TeamsWebhookUrl": "your-teams-webhook-url",
    "NotifyOnAllFailures": true,
    "MinConfidenceForNotification": 0.7,
    "CreateAutoFixPRs": true,
    "MinConfidenceForAutoFixPR": 0.85
  }
}
```

### 2. Required Tokens and Permissions

Your GitHub Personal Access Token needs these permissions:
-  `repo` (full control of private repositories)
-  `workflow` (update GitHub Action workflows)
-  `write:packages` (if applicable)

Verify token is set:
```powershell
$env:GITHUB__PERSONALACCESSTOKEN
```

### 3. AWS Credentials

Ensure AWS credentials are configured for Claude/Bedrock:
```powershell
aws configure list
```

## Test Scenario 1: Missing Using Statement (Simple Syntax Error)

This is the **easiest test** - introduces a simple syntax error that Claude should fix.

### Step 1: Create a Test File with Error

Create a new file in your test repository:

**File: `src/TestAutoFix/SimpleService.cs`**
```csharp
namespace TestAutoFix;

public class SimpleService
{
    public string SerializeData(object data)
    {
        // Missing using System.Text.Json;
        return JsonSerializer.Serialize(data);  // This will cause compilation error
    }
}
```

Commit and push this file to your repository.

### Step 2: Create/Update Workflow

Ensure you have a workflow that will build and fail:

**File: `.github/workflows/test-autofix.yml`**
```yaml
name: Test Auto-Fix

on:
  workflow_dispatch:
  push:
    branches:
      - master
    paths:
      - 'src/TestAutoFix/**'
      - '.github/workflows/test-autofix.yml'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build (will fail due to missing using)
        run: |
          echo "Building test file..."
          dotnet build src/TestAutoFix/SimpleService.cs
```

### Step 3: Start the API

```powershell
cd C:\Anish\Source\Agentic_Pipeline\src\AgenticPipeline.Api
dotnet run
```

Watch for:
```
info: AgenticPipeline.Api.Program[0]
      Database initialized successfully
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7056
```

### Step 4: Ensure ngrok is Running

```powershell
ngrok http 7056
```

Note your ngrok URL (should match `ApiBaseUrl` in config).

### Step 5: Trigger the Workflow

Go to GitHub Actions and manually trigger the workflow, or push the changes.

### Step 6: Monitor the Process

#### In API Logs, watch for:

**1. Webhook received:**
```
info: AgenticPipeline.Api.Controllers.WebhookController[0]
      Received workflow_run webhook: action=completed, status=completed, conclusion=failure
```

**2. Processing workflow:**
```
info: AgenticPipeline.Api.Controllers.WebhookController[0]
      Processing workflow failure: RunId={id}, Repo=anishsingh0302/Semantic-Kernel
```

**3. RCA Analysis:**
```
info: AgenticPipeline.Infrastructure.Services.PipelineOrchestrator[0]
      Performing root cause analysis...
```

**4. Auto-fix attempt:**
```
info: AgenticPipeline.Api.Controllers.WebhookController[0]
      Attempting to generate auto-fix for orchestration {guid}
```

**5. Fix generated:**
```
info: AgenticPipeline.Infrastructure.Services.AutoFixService[0]
      Auto-fix generation completed: FixGenerated=True, IsSafeToApply=True
```

**6. PR created:**
```
info: AgenticPipeline.Api.Controllers.WebhookController[0]
      Created auto-fix PR #123 for orchestration {guid}
```

### Step 7: Verify the PR

Go to your GitHub repository and check:

1. **New Branch Created:**
   - Name format: `autofix/compilationerror-20260424123456`

2. **Pull Request Opened:**
   - Title: `[Auto-Fix] CompilationError: JsonSerializer not recognized`
   - Check the PR description for:
     - Confidence level
     - Fix description
     - Original failure analysis
     - List of changes

3. **Files Changed:**
   - Should show `src/TestAutoFix/SimpleService.cs`
   - Should have added `using System.Text.Json;`

4. **Commit Message:**
   - Format: `Auto-fix: Add missing System.Text.Json using directive`

### Step 8: Test the Fix

```powershell
# Checkout the auto-fix branch
git fetch origin
git checkout autofix/compilationerror-20260424123456

# Verify it builds
dotnet build src/TestAutoFix/SimpleService.cs
```

### Step 9: Clean Up

If the fix works:
- Merge the PR (or close if you're just testing)
- Delete the test file/branch

If testing again:
- Delete the branch: `git push origin --delete autofix/...`

---

## Test Scenario 2: Missing NuGet Package Reference

This tests if auto-fix can identify and add missing package references.

### Step 1: Create Test Project

**File: `src/TestAutoFix/TestAutoFix.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <!-- Missing: <PackageReference Include="Newtonsoft.Json" Version="13.0.3" /> -->
</Project>
```

**File: `src/TestAutoFix/JsonService.cs`**
```csharp
using Newtonsoft.Json;  // Package not referenced

namespace TestAutoFix;

public class JsonService
{
    public string Serialize(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }
}
```

### Step 2: Update Workflow

```yaml
- name: Restore dependencies
  run: dotnet restore src/TestAutoFix/TestAutoFix.csproj

- name: Build
  run: dotnet build src/TestAutoFix/TestAutoFix.csproj
```

### Step 3: Follow Steps 3-9 from Scenario 1

Expected fix: Add `<PackageReference>` to `.csproj` file

---

## Test Scenario 3: Configuration Value Error

This tests fixing simple configuration mistakes.

### Step 1: Create Invalid Configuration

**File: `appsettings.Test.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=database.db;Mode=ReadWrit"  // Typo: ReadWrit
  }
}
```

**File: `src/TestAutoFix/ConfigTest.cs`**
```csharp
// Code that validates connection string format
// Should fail with "Invalid mode: ReadWrit"
```

### Expected Fix

Change `ReadWrit` to `ReadWrite` in the JSON file.

---

## Monitoring and Debugging

### Check API Logs

```powershell
# Run with verbose logging
$env:Logging__LogLevel__Default = "Debug"
dotnet run
```

### Key Log Points to Check

1. **Webhook received and verified:**
```
Webhook signature verified successfully
```

2. **Logs fetched:**
```
Fetched workflow logs: {size} characters
```

3. **RCA completed:**
```
Root cause analysis completed: ErrorType={type}, Confidence={confidence}
```

4. **Confidence check:**
```
Skipping auto-fix due to low confidence: {confidence} < {threshold}
# OR
Attempting to generate auto-fix for orchestration {id}
```

5. **Fix generation:**
```
Auto-fix generation completed: FixGenerated=True, IsSafeToApply=True
# OR
Auto-fix not generated or not safe to apply: {reason}
```

6. **GitHub operations:**
```
Created branch autofix/{name}
Updated file {path}
Created PR #{number}: {title}
```

### Common Issues and Solutions

#### Issue: "AutoFixService not available"

**Solution:**
Check that `IAutoFixService` is registered in `Program.cs`:
```csharp
builder.Services.AddScoped<IAutoFixService, AutoFixService>();
```

#### Issue: "Skipping auto-fix due to low confidence"

**Causes:**
- RCA didn't identify root cause clearly
- Error is too complex
- Logs don't have enough information

**Solution:**
- Lower `MinConfidenceForAutoFixPR` temporarily (0.7 for testing)
- Check RCA output in logs
- Try simpler error scenarios first

#### Issue: "Auto-fix not generated or not safe to apply"

**Reasons Claude might reject:**
- Error is too complex (logic error, architectural issue)
- Involves security-sensitive code
- Requires human judgment
- Insufficient context to understand fix

**Check logs for:**
```
Auto-fix not generated or not safe to apply: {reason}
```

**Solution:**
- Review the failure analysis
- Try simpler error types
- Check Claude's response in logs (if logged)

#### Issue: "Failed to create auto-fix PR"

**Possible causes:**
- GitHub token permissions insufficient
- Branch already exists
- File conflicts
- Network/API errors

**Solution:**
```powershell
# Check token permissions
gh auth status

# Delete existing branch if needed
git push origin --delete autofix/branch-name

# Check GitHub API rate limits
gh api rate_limit
```

#### Issue: PR created but with wrong changes

**This is expected behavior** - that's why human review is required!

**Actions:**
1. Close the PR with explanation
2. Review what Claude misunderstood
3. Consider if error was too complex for auto-fix
4. May need to adjust safety rules or prompts

---

## Testing Checklist

Use this checklist for comprehensive testing:

### Pre-Test Setup
- [ ] `CreateAutoFixPRs` set to `true` in appsettings.Development.json
- [ ] `MinConfidenceForAutoFixPR` set to appropriate value (0.85 for dev)
- [ ] GitHub token has `repo` permissions
- [ ] AWS credentials configured for Claude
- [ ] ngrok running and URL matches `ApiBaseUrl`
- [ ] API running and healthy (`/api/webhook/test`)

### Test Execution
- [ ] Created test scenario with simple fixable error
- [ ] Workflow runs and fails as expected
- [ ] Webhook triggers and is verified
- [ ] Logs are fetched successfully
- [ ] RCA completes with high confidence
- [ ] Auto-fix generation attempted
- [ ] Fix is generated and marked safe to apply

### PR Verification
- [ ] New branch created with correct naming
- [ ] PR opened with descriptive title
- [ ] PR body includes all required information
- [ ] File changes are correct and minimal
- [ ] Commit messages are descriptive
- [ ] Fix actually resolves the issue

### Post-Test
- [ ] Test the fix by checking out branch
- [ ] Verify build succeeds with fix
- [ ] Review PR for quality
- [ ] Merge or close PR
- [ ] Clean up test branches/files

---

## Advanced Testing

### Test Different Error Types

1. **Syntax Errors:**
   - Missing semicolons
   - Unclosed braces
   - Invalid syntax

2. **Missing Dependencies:**
   - Missing using statements
   - Missing NuGet packages
   - Missing assembly references

3. **Configuration Errors:**
   - Typos in config files
   - Invalid JSON
   - Wrong connection strings

4. **Simple Logic Errors:**
   - Off-by-one errors
   - Wrong comparison operators
   - Incorrect string formatting

### Test Edge Cases

1. **Multiple Files Affected:**
   - Error spans multiple files
   - Verify all files are changed

2. **Large Files:**
   - Test with files > 1000 lines
   - Verify Claude handles large context

3. **Complex Projects:**
   - Multi-project solutions
   - Nested dependencies

### Test Safety Boundaries

Intentionally create scenarios that **should not** be auto-fixed:

1. **Security Code:**
   - Authentication logic
   - Authorization checks
   - Encryption/decryption

2. **Database Schema:**
   - Migration files
   - Entity models with relationships

3. **Complex Logic:**
   - Business rule implementations
   - Algorithm changes
   - State machine logic

**Expected behavior:** Auto-fix should return `IsSafeToApply=false`

---

## Performance Testing

### Measure Timings

Add timing logs to measure:

1. **Webhook Processing:** Webhook received → RCA complete
2. **Fix Generation:** RCA complete → Fix generated
3. **PR Creation:** Fix generated → PR opened
4. **Total Time:** Webhook → PR opened

**Expected timings:**
- RCA: 10-30 seconds
- Fix generation: 5-10 seconds
- PR creation: 2-5 seconds
- **Total: 20-45 seconds**

### Load Testing

1. Trigger multiple failures simultaneously
2. Verify all get processed
3. Check for rate limiting
4. Monitor API performance

---

## Troubleshooting Commands

```powershell
# Check API is running
curl https://localhost:7056/api/webhook/test

# Check ngrok tunnel
curl https://your-ngrok-url.ngrok-free.dev/api/webhook/test

# Check GitHub webhook deliveries
gh api repos/anishsingh0302/Semantic-Kernel/hooks

# Check recent workflow runs
gh run list --limit 5

# View specific run logs
gh run view {run-id} --log

# List all auto-fix branches
git branch -r | grep autofix

# Delete all auto-fix branches (cleanup)
git branch -r | grep autofix | sed 's/origin\///' | xargs -I {} git push origin --delete {}

# Check database for failures
sqlite3 pipeline.db "SELECT FailureId, ErrorType, ConfidenceScore FROM Failures ORDER BY AnalyzedAt DESC LIMIT 10;"

# Check orchestration results
sqlite3 pipeline.db "SELECT OrchestrationId, Status, StepsCompletedCount FROM OrchestrationResults ORDER BY StartedAt DESC LIMIT 5;"
```

---

## Appendix: Sample Test Repository Setup

Create a dedicated test repository for auto-fix testing:

```powershell
# Create new repo
gh repo create agentic-pipeline-autofix-tests --private --clone

cd agentic-pipeline-autofix-tests

# Create directory structure
mkdir -p src/TestCases
mkdir -p .github/workflows

# Create test workflow (see above examples)

# Create multiple test scenarios
# Scenario 1: Missing using
# Scenario 2: Missing package
# Scenario 3: Config typo

# Push and configure webhook
git push origin master
```

This gives you a controlled environment for testing without affecting production code.
