# Azure DevOps to Agentic Pipeline - Webhook Setup

## Overview

This guide configures Azure DevOps to trigger your Agentic Pipeline when builds fail, enabling automatic RCA analysis.

---

## Prerequisites

✅ Azure DevOps pipeline is running
✅ Self-hosted agent configured
✅ Agentic Pipeline API is running
✅ ngrok or public URL available

---

## Step 1: Configure Environment Variables

You need to set these environment variables before starting the API:

### Required Variables:

```powershell
# Azure DevOps PAT (for fetching build logs)
$env:AZUREDEVOPS__PERSONALACCESSTOKEN = "your-azure-devops-pat-here"

# Azure DevOps Organization
$env:AZUREDEVOPS__ORGANIZATION = "your-org-name"

# Azure DevOps Project
$env:AZUREDEVOPS__DEFAULTPROJECT = "Semantic-Kernel"

# GitHub PAT (for commit analysis and creating PRs)
$env:GITHUB__PERSONALACCESSTOKEN = "your-github-pat-here"
```

### How to Get Azure DevOps PAT:

1. Go to `https://dev.azure.com/YOUR-ORG`
2. Click **User Settings** (person icon, top right)
3. Select **Personal access tokens**
4. Click **+ New Token**
5. Configure:
   - **Name**: `Agentic Pipeline Integration`
   - **Expiration**: 90 days
   - **Scopes**:
     - ✅ **Build** (Read)
     - ✅ **Code** (Read) - if using Azure Repos
     - ✅ **Project and Team** (Read)
6. Click **Create**
7. **Copy the token** (you won't see it again!)

### How to Get GitHub PAT:

1. Go to `https://github.com/settings/tokens`
2. Click **Generate new token (classic)**
3. Configure:
   - **Note**: `Agentic Pipeline`
   - **Expiration**: 90 days
   - **Scopes**:
     - ✅ `repo` (Full control of private repositories)
     - ✅ `workflow` (Update GitHub Action workflows)
4. Click **Generate token**
5. **Copy the token**

---

## Step 2: Start Agentic Pipeline API

```powershell
# Navigate to project
cd "C:\Anish\Source\Agentic_Pipeline"

# Set environment variables (from Step 1)
$env:AZUREDEVOPS__PERSONALACCESSTOKEN = "your-azure-devops-pat"
$env:AZUREDEVOPS__ORGANIZATION = "your-org-name"
$env:AZUREDEVOPS__DEFAULTPROJECT = "Semantic-Kernel"
$env:GITHUB__PERSONALACCESSTOKEN = "your-github-pat"

# Start the API
dotnet run --project src\AgenticPipeline.Api
```

**Expected output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7056
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

## Step 3: Expose API with ngrok

Open a **new PowerShell window**:

```powershell
# Start ngrok (expose port 5000)
ngrok http 5000
```

**Copy the Forwarding URL:**
```
Forwarding   https://abc123.ngrok-free.app -> http://localhost:5000
```

**Important:** Copy the `https://abc123.ngrok-free.app` URL - you'll need it next.

---

## Step 4: Configure Azure DevOps Service Hook

### 4.1 Go to Project Settings

1. Open Azure DevOps: `https://dev.azure.com/YOUR-ORG/Semantic-Kernel`
2. Click **Project Settings** (bottom left, gear icon)
3. Under **General**, click **Service hooks**

### 4.2 Create New Service Hook

1. Click **+ Create subscription**
2. Select **Web Hooks**
3. Click **Next**

### 4.3 Configure Trigger

**Select the trigger event:**
- **Service**: Build and release
- **Event**: Build completed
- **Filters**:
  - **Build status**: `Failed` ✅, `Partially succeeded` ✅
  - **Build pipeline**: Select your test pipeline (or leave blank for all)
  - **Branch**: `master` (or leave blank for all branches)

Click **Next**

### 4.4 Configure Action

**Configure the action:**
- **URL**: `https://YOUR-NGROK-URL.ngrok-free.app/api/webhook/azuredevops`
  - Replace `YOUR-NGROK-URL` with your actual ngrok URL from Step 3
  - Example: `https://abc123.ngrok-free.app/api/webhook/azuredevops`
- **Resource version**: `Latest and greatest`
- **Messages to send**: `All`
- **Detailed messages to send**: `All`

**Test the webhook:**
- Click **Test** button
- If successful, you should see:
  ```
  Status: 200 OK
  Response: {"status":"Accepted","message":"Test webhook received"}
  ```

Click **Finish**

---

## Step 5: Verify Webhook is Active

1. Go to **Project Settings** → **Service hooks**
2. You should see your webhook listed:
   - **Service**: Web Hooks
   - **Event**: Build completed
   - **Status**: Enabled ✅

---

## Step 6: Test the Integration

Now let's create a failing test to trigger the full flow:

### 6.1 Create Failing Test

```powershell
cd "C:\Anish\Source\Semantic-kernel\Customer.API.Tests"
```

Edit `BasicTests.cs` and add this test:

```csharp
[Fact]
public void Test_RCA_Integration()
{
    // This test will fail to trigger Azure DevOps webhook
    var expectedValue = 100;
    var actualValue = 50;

    Assert.Equal(expectedValue, actualValue); // This will fail
}
```

### 6.2 Commit and Push

```powershell
git add .
git commit -m "Test RCA integration with failing test"
git push origin master
```

### 6.3 Watch the Flow

**In Azure DevOps:**
1. Go to **Pipelines**
2. See build queued and running
3. Build fails ❌

**In Agentic Pipeline Console:**
You should see logs like:
```
info: Received Azure DevOps webhook: EventType=build.complete, BuildId=123
info: Processing Azure DevOps build failure: BuildId=123, Project=Semantic-Kernel
info: Fetching logs for build 123...
info: Successfully fetched logs: 5000 characters
info: Starting RCA analysis...
info: RCA completed: Found 1 root causes
info: Analyzing recent commits...
info: Pattern detection starting...
info: Generating auto-fix...
info: Creating GitHub PR with fix...
```

**In GitHub:**
1. Go to your repository
2. Check **Pull Requests**
3. You should see a new PR: "Auto-fix: ..." with the fix

---

## Step 7: Verify Components

### Check Logs in Agentic Pipeline Console:

```
✅ Webhook received from Azure DevOps
✅ Build logs fetched successfully
✅ RCA analysis completed
✅ Commit analysis completed
✅ Pattern detection completed
✅ Auto-fix generated
✅ GitHub PR created (if enabled)
✅ Teams notification sent (if configured)
```

### Check Azure DevOps:

1. Go to **Project Settings** → **Service hooks**
2. Click on your webhook
3. Click **History** tab
4. See successful POST requests with Status 200

---

## Architecture Flow

```
┌─────────────────────────────────────┐
│ Developer pushes code to GitHub     │
│ (with failing test)                 │
└────────────────┬────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────┐
│ Azure DevOps Pipeline Triggers      │
│ - Clones GitHub repo                │
│ - Runs on self-hosted agent         │
│ - Executes tests                    │
│ - Tests FAIL ❌                     │
└────────────────┬────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────┐
│ Azure DevOps Service Hook           │
│ POST /api/webhook/azuredevops       │
│ Payload: Build ID, Project, Logs    │
└────────────────┬────────────────────┘
                 │
                 ▼ (ngrok tunnel)
┌─────────────────────────────────────┐
│ Agentic Pipeline API                │
│ WebhookController                   │
│ .ReceiveAzureDevOpsWebhook()        │
└────────────────┬────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────┐
│ AzureDevOpsLogService               │
│ - Fetches full build logs via API  │
│ - Uses Azure DevOps PAT             │
└────────────────┬────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────┐
│ PipelineOrchestrator                │
│ 1. RCA Analysis (Claude)            │
│ 2. Commit Analysis (GitHub API)     │
│ 3. Pattern Detection (Embeddings)   │
│ 4. Similar Failures (Vector Search) │
└────────────────┬────────────────────┘
                 │
                 ├─► Auto-Fix Generation
                 │   └─► GitHub PR Created
                 │
                 └─► Notifications
                     ├─► Teams (if configured)
                     └─► GitHub Comment
```

---

## Troubleshooting

### Issue: Webhook not received

**Check:**
1. Is API running? `dotnet run --project src\AgenticPipeline.Api`
2. Is ngrok running? `ngrok http 5000`
3. Is webhook URL correct in Azure DevOps?
4. Check ngrok web interface: `http://localhost:4040`

### Issue: "401 Unauthorized" when fetching logs

**Fix:**
```powershell
# Verify Azure DevOps PAT is set
$env:AZUREDEVOPS__PERSONALACCESSTOKEN = "your-pat"

# Verify PAT has correct scopes (Build: Read)

# Test PAT manually:
$org = "your-org"
$pat = $env:AZUREDEVOPS__PERSONALACCESSTOKEN
$base64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$pat"))
$headers = @{Authorization = "Basic $base64"}
Invoke-RestMethod -Uri "https://dev.azure.com/$org/_apis/projects?api-version=7.0" -Headers $headers
```

### Issue: RCA runs but no PR created

**Check:**
1. Is GitHub PAT set? `$env:GITHUB__PERSONALACCESSTOKEN`
2. Is `CreateAutoFixPRs` enabled in appsettings?
3. Check console logs for errors
4. Verify GitHub PAT has `repo` scope

### Issue: Service hook shows "Failed"

**Check:**
1. Go to Service Hooks → Your webhook → History
2. Click on failed request
3. See error details
4. Common issues:
   - ngrok expired (free tier expires after 2 hours)
   - API not running
   - Wrong URL format

---

## Configuration Summary

**Environment Variables Required:**

```powershell
# Azure DevOps
$env:AZUREDEVOPS__PERSONALACCESSTOKEN = "azure-devops-pat"
$env:AZUREDEVOPS__ORGANIZATION = "your-org"
$env:AZUREDEVOPS__DEFAULTPROJECT = "Semantic-Kernel"

# GitHub
$env:GITHUB__PERSONALACCESSTOKEN = "github-pat"
```

**Azure DevOps Service Hook:**
- **Event**: Build completed (Failed, Partially succeeded)
- **URL**: `https://your-ngrok.ngrok-free.app/api/webhook/azuredevops`

**Test Command:**
```powershell
# Add failing test → Commit → Push → Watch magic happen!
```

---

## Next Steps

After successful setup:

1. ✅ Remove the failing test (or fix it)
2. ✅ Configure Teams notifications (optional)
3. ✅ Adjust auto-fix confidence threshold
4. ✅ Monitor pattern detection for recurring issues
5. ✅ Review GitHub PRs created by auto-fix

---

## Production Considerations

### For Production Use:

1. **Replace ngrok** with:
   - Azure App Service
   - Docker container on public IP
   - Reverse proxy (nginx/IIS)

2. **Secure webhook** with:
   - Webhook secrets
   - IP allowlisting
   - API key authentication

3. **Persistent tokens**:
   - Use Azure Key Vault
   - Or environment variables on server

4. **Monitoring**:
   - Add Application Insights
   - Log aggregation (ELK, Seq)
   - Alerts on failures

---

**You're all set!** 🎉

Your Azure DevOps pipeline will now automatically trigger RCA analysis on test failures.
