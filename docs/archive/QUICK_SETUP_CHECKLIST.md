# Azure DevOps Integration - Quick Setup Checklist

## What You Need

Before starting, gather these items:

### 1. Azure DevOps Personal Access Token (PAT)
- [ ] Go to: `https://dev.azure.com/YOUR-ORG/_usersSettings/tokens`
- [ ] Click: **+ New Token**
- [ ] Name: `Agentic Pipeline`
- [ ] Scopes: **Build (Read)**, **Code (Read)**, **Project and Team (Read)**
- [ ] Copy the token: `________________________________`

### 2. Azure DevOps Organization Name
- [ ] Check your URL: `https://dev.azure.com/YOUR-ORG-NAME`
- [ ] Organization: `_____________________`

### 3. GitHub Personal Access Token (PAT)
- [ ] Go to: `https://github.com/settings/tokens`
- [ ] Click: **Generate new token (classic)**
- [ ] Scopes: **repo**, **workflow**
- [ ] Copy the token: `________________________________`

### 4. ngrok (for webhook)
- [ ] Download from: `https://ngrok.com/download`
- [ ] Extract to: `C:\ngrok` (or your preferred location)
- [ ] Sign up and get auth token
- [ ] Run: `ngrok config add-authtoken YOUR-TOKEN`

---

## Step-by-Step Setup

### Step 1: Start Agentic Pipeline API 

Open PowerShell in `C:\Anish\Source\Agentic_Pipeline`:

**Option A: Using the helper script (Recommended)**
```powershell
.\start-api.ps1 `
    -AzureDevOpsPAT "your-azure-devops-pat" `
    -AzureDevOpsOrg "your-org-name" `
    -AzureDevOpsProject "Semantic-Kernel" `
    -GitHubPAT "your-github-pat"
```

**Option B: Manual environment variables**
```powershell
$env:AZUREDEVOPS__PERSONALACCESSTOKEN = "your-azure-devops-pat"
$env:AZUREDEVOPS__ORGANIZATION = "your-org-name"
$env:AZUREDEVOPS__DEFAULTPROJECT = "Semantic-Kernel"
$env:GITHUB__PERSONALACCESSTOKEN = "your-github-pat"

dotnet run --project src\AgenticPipeline.Api
```

**Expected Output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7056
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

 API is running when you see "Now listening on..."

---

### Step 2: Start ngrok 

Open a **NEW PowerShell window**:

```powershell
ngrok http 5000
```

**Expected Output:**
```
Session Status    online
Forwarding        https://abc123.ngrok-free.app -> http://localhost:5000
```

 Copy the ngrok URL (the `https://` one)

**Your ngrok URL:** `_____________________________________`

---

### Step 3: Configure Azure DevOps Service Hook 

1. Go to: `https://dev.azure.com/YOUR-ORG/Semantic-Kernel/_settings/serviceHooks`

2. Click: **+ Create subscription**

3. Select: **Web Hooks** → Click **Next**

4. Configure Trigger:
   - [ ] Event: **Build and release** → **Build completed**
   - [ ] Filters:
     - [ ] Build status:  **Failed**,  **Partially succeeded**
     - [ ] Build pipeline: (Leave blank or select your pipeline)
   - [ ] Click **Next**

5. Configure Action:
   - [ ] URL: `https://YOUR-NGROK-URL.ngrok-free.app/api/webhook/azuredevops`
     - Replace `YOUR-NGROK-URL` with your actual ngrok URL from Step 2
   - [ ] Resource version: **Latest and greatest**
   - [ ] Messages to send: **All**
   - [ ] Click **Test** (should show "Status: 200 OK")
   - [ ] Click **Finish**

 Service hook created and tested successfully

---

### Step 4: Test the Integration 

**Create a failing test:**

1. Open: `C:\Anish\Source\Semantic-kernel\Customer.API.Tests\BasicTests.cs`

2. Add this test:
```csharp
[Fact]
public void Test_RCA_Integration_Demo()
{
    // This will fail to trigger RCA pipeline
    var expected = 100;
    var actual = 50;

    Assert.Equal(expected, actual);
}
```

3. Commit and push:
```powershell
cd "C:\Anish\Source\Semantic-kernel"
git add .
git commit -m "Test RCA integration with Azure DevOps"
git push origin master
```

**Watch the Flow:**

1.  Azure DevOps pipeline triggers and runs
2.  Test fails (as expected)
3.  Service hook fires webhook
4.  Agentic Pipeline API receives webhook
5.  Logs appear in API console:
   ```
   info: Received Azure DevOps webhook: EventType=build.complete
   info: Fetching build logs...
   info: RCA analysis starting...
   ```
6.  GitHub PR created with auto-fix (if enabled)

---

### Step 5: Verify Everything Works 

**In Agentic Pipeline Console:**
- [ ] See: `Received Azure DevOps webhook`
- [ ] See: `Fetching logs for build`
- [ ] See: `RCA analysis completed`
- [ ] See: `Creating GitHub PR` (if auto-fix enabled)

**In Azure DevOps:**
- [ ] Go to: Project Settings → Service hooks → Your webhook → History
- [ ] See: Successful POST with Status 200

**In GitHub:**
- [ ] Go to: Your repository → Pull Requests
- [ ] See: New PR with auto-fix (if enabled)

---

### Step 6: Clean Up Test 

Remove the failing test after verification:

1. Open: `Customer.API.Tests\BasicTests.cs`
2. Delete the `Test_RCA_Integration_Demo` method
3. Commit:
```powershell
git add .
git commit -m "Remove RCA integration test"
git push
```

 This build should pass now

---

## Troubleshooting Quick Reference

### API won't start
```powershell
# Check if port is in use
netstat -ano | findstr :5000

# Kill process if needed
taskkill /PID <process-id> /F
```

### Webhook not received
1. Check ngrok is running: `http://localhost:4040`
2. Verify webhook URL in Azure DevOps matches ngrok URL
3. Check API console for errors

### "401 Unauthorized" fetching logs
1. Verify Azure DevOps PAT is set correctly
2. Check PAT hasn't expired
3. Verify PAT has Build (Read) scope

### No GitHub PR created
1. Check GitHub PAT is set: `$env:GITHUB__PERSONALACCESSTOKEN`
2. Verify `CreateAutoFixPRs: true` in appsettings.Development.json
3. Check console logs for errors

---

## Current Configuration Summary

**Fill this in for reference:**

| Item | Value |
|------|-------|
| Azure DevOps Org | `___________________` |
| Azure DevOps Project | `Semantic-Kernel` |
| GitHub Repo | `anishsingh0302/Semantic-Kernel` |
| API URL (local) | `http://localhost:5000` |
| API URL (ngrok) | `https://____________.ngrok-free.app` |
| Webhook Endpoint | `/api/webhook/azuredevops` |
| Agent Pool | `Default` |
| Agent Status | Online  |

---

## What Happens on Each Failure

```
1. Code pushed to GitHub
   ↓
2. Azure DevOps pipeline triggered
   ↓
3. Tests run on self-hosted agent
   ↓
4. Test fails 
   ↓
5. Service hook fires (build.complete event)
   ↓
6. POST to Agentic Pipeline via ngrok
   ↓
7. API fetches build logs from Azure DevOps
   ↓
8. RCA Plugin analyzes failure with Claude AI
   ↓
9. CommitAnalyzer checks recent commits
   ↓
10. Pattern detection finds similar failures
   ↓
11. Auto-fix generates code fix (if confidence > 85%)
   ↓
12. GitHub PR created with fix
   ↓
13. Teams notification sent (if configured)
```

---

## Success Checklist

Before considering setup complete, verify:

- [ ] API starts without errors
- [ ] ngrok tunnel is active
- [ ] Service hook is created and enabled
- [ ] Test failure triggers webhook
- [ ] Webhook received in API (see console logs)
- [ ] Build logs fetched successfully
- [ ] RCA analysis completes
- [ ] GitHub PR created (optional, if auto-fix enabled)

---

## Next Steps

After successful setup:

1. **Configure Teams Notifications** (Optional)
   - Get Teams webhook URL
   - Add to `appsettings.Development.json`

2. **Adjust Auto-Fix Settings**
   - Edit `MinConfidenceForAutoFixPR` in appsettings
   - Set to `0.9` for high confidence only

3. **Monitor Pattern Detection**
   - Check similar failures dashboard
   - Identify recurring issues

4. **Production Deployment** (Future)
   - Replace ngrok with permanent URL
   - Deploy API to Azure App Service
   - Use Azure Key Vault for PATs

---

**You're ready to go!** 

Any issues? Check the detailed guide: `Azure_DevOps_Webhook_Setup.md`
