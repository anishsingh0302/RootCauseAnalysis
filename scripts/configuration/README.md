# Agentic Pipeline Configuration Guide

Complete setup guide for configuring AWS, GitHub, and Azure DevOps credentials for the Agentic Pipeline application.

---

##  Overview

This guide covers:
-  AWS SSO Credentials (for Bedrock AI services)
-  GitHub Personal Access Token (for commit analysis and auto-fix PRs)
-  Azure DevOps Personal Access Token (for fetching build logs)
-  Teams Webhook URL (for notifications)
-  What goes in appsettings.json vs environment variables

The `configure-environment.ps1` script handles all of this in one unified command.

---

##  Security Model

### Confidential (Environment Variables)
- Personal Access Tokens (PATs)
- Webhook secrets
- API keys

### Non-Confidential (appsettings.json)
- Organization names
- Project names
- Repository names
- Default values

---

##  Quick Setup

### Step 1: Update appsettings.json (Non-Confidential)

Edit: `src/AgenticPipeline.Api/appsettings.json`

```json
{
  "GitHub": {
    "PersonalAccessToken": "",  // LEAVE EMPTY
    "DefaultOwner": "anishsingh0302",  //  SET YOUR USERNAME
    "DefaultRepo": "Semantic-Kernel",  //  SET YOUR REPO
    "WebhookSecret": ""  // LEAVE EMPTY
  },
  "AzureDevOps": {
    "Organization": "your-org-name",  //  SET YOUR ORG NAME
    "PersonalAccessToken": "",  // LEAVE EMPTY
    "DefaultProject": "Semantic-Kernel",  //  SET YOUR PROJECT
    "ApiVersion": "7.1-preview.7"
  }
}
```

### Step 2: Create Personal Access Tokens

#### GitHub PAT (Required)
1. Go to: https://github.com/settings/tokens
2. Click: **Generate new token (classic)**
3. Name: `Agentic Pipeline`
4. Expiration: 90 days
5. Scopes:  `repo`,  `workflow`
6. Click **Generate token**
7. **COPY THE TOKEN** (starts with `ghp_`)

#### Azure DevOps PAT (Required for Azure DevOps integration)
1. Go to: `https://dev.azure.com/YOUR-ORG/_usersSettings/tokens`
2. Click: **+ New Token**
3. Name: `Agentic Pipeline`
4. Expiration: 90 days
5. Scopes:  **Build (Read)**,  **Code (Read)**
6. Click **Create**
7. **COPY THE TOKEN**

### Step 3: Set Environment Variables (Confidential)

Run the configuration script:

```powershell
cd C:\Anish\Source\Agentic_Pipeline\scripts\configuration

.\configure-environment.ps1 `
    -Token "ghp_your_github_token" `
    -Owner "anishsingh0302" `
    -Repo "Semantic-Kernel" `
    -AzureDevOpsPAT "your_azure_devops_pat"
```

**Optional: Add Teams notifications:**
```powershell
.\configure-environment.ps1 `
    -Token "ghp_your_github_token" `
    -Owner "anishsingh0302" `
    -Repo "Semantic-Kernel" `
    -AzureDevOpsPAT "your_azure_devops_pat" `
    -TeamsWebhookUrl "https://your-teams-webhook-url"
```

### Step 4: Verify Configuration

```powershell
# Check GitHub PAT
echo $env:GitHub__PersonalAccessToken

# Check Azure DevOps PAT
echo $env:AzureDevOps__PersonalAccessToken
```

### Step 5: Start API

```powershell
cd C:\Anish\Source\Agentic_Pipeline
dotnet run --project src/AgenticPipeline.Api
```

Test the configuration:
```powershell
curl http://localhost:5000/api/webhook/test
```

---

##  Environment Variables Reference

### Set by configure-environment.ps1:

**GitHub (API format):**
- `GitHub__PersonalAccessToken` → Your GitHub PAT
- `GitHub__DefaultOwner` → Your GitHub username
- `GitHub__DefaultRepo` → Your repository name

**Azure DevOps:**
- `AzureDevOps__PersonalAccessToken` → Your Azure DevOps PAT

**Notifications:**
- `Notifications__TeamsWebhookUrl` → Teams webhook URL (optional)

**For Tests:**
- `GITHUB_PAT` → Same as GitHub__PersonalAccessToken
- `GITHUB_OWNER` → Same as GitHub__DefaultOwner
- `GITHUB_REPO` → Same as GitHub__DefaultRepo

---

##  Manual Setup (Alternative)

If you prefer not to use the script:

```powershell
# GitHub
[Environment]::SetEnvironmentVariable("GitHub__PersonalAccessToken", "ghp_your_token", "User")
[Environment]::SetEnvironmentVariable("GitHub__DefaultOwner", "anishsingh0302", "User")
[Environment]::SetEnvironmentVariable("GitHub__DefaultRepo", "Semantic-Kernel", "User")

# Azure DevOps
[Environment]::SetEnvironmentVariable("AzureDevOps__PersonalAccessToken", "your_azure_pat", "User")

# For current session
$env:GitHub__PersonalAccessToken = "ghp_your_token"
$env:AzureDevOps__PersonalAccessToken = "your_azure_pat"
```

---

##  Configuration Checklist

### appsettings.json
- [ ] Set `GitHub.DefaultOwner` to your username
- [ ] Set `GitHub.DefaultRepo` to your repository name
- [ ] Set `AzureDevOps.Organization` to your Azure DevOps org
- [ ] Set `AzureDevOps.DefaultProject` to your project name

### Environment Variables
- [ ] Set `GitHub__PersonalAccessToken` (GitHub PAT)
- [ ] Set `AzureDevOps__PersonalAccessToken` (Azure DevOps PAT)
- [ ] Set `Notifications__TeamsWebhookUrl` (optional)

### Verification
- [ ] Run `echo $env:GitHub__PersonalAccessToken` (should show token)
- [ ] Run `echo $env:AzureDevOps__PersonalAccessToken` (should show token)
- [ ] Start API and test `/api/webhook/test` endpoint
- [ ] Verify `configured: true` in response

---

##  Security Best Practices

###  DO:
- Store tokens in environment variables
- Set token expiration dates (90 days)
- Revoke tokens immediately if exposed
- Use different tokens for different environments
- Keep appsettings.json in version control (no secrets!)

###  DON'T:
- Put tokens in `appsettings.json`
- Commit tokens to git
- Share tokens with others
- Use tokens without expiration
- Store secrets in code files

---

## 🆘 Troubleshooting

### Issue: "Configuration value not found"
**Solution:**
```powershell
# Re-run configuration script
.\scripts\configuration\configure-environment.ps1 -Token "ghp_xxx" -AzureDevOpsPAT "xxx"

# Restart API
```

### Issue: Webhook returns 404
**Solution:**
```powershell
# Rebuild API
dotnet build

# Restart API
dotnet run --project src/AgenticPipeline.Api
```

### Issue: "401 Unauthorized" from GitHub
**Solution:**
1. Check token hasn't expired: https://github.com/settings/tokens
2. Verify token has `repo` and `workflow` scopes
3. Regenerate token if needed

### Issue: "401 Unauthorized" from Azure DevOps
**Solution:**
1. Check PAT hasn't expired
2. Verify PAT has **Build (Read)** and **Code (Read)** scopes
3. Test PAT manually:
```powershell
$org = "your-org"
$pat = "your-pat"
$base64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$pat"))
$headers = @{Authorization = "Basic $base64"}
Invoke-RestMethod -Uri "https://dev.azure.com/$org/_apis/projects?api-version=7.0" -Headers $headers
```

### Issue: Environment variables not persisting
**Solution:**
```powershell
# Check if variables are set at USER level
[Environment]::GetEnvironmentVariable("GITHUB_PAT", "User")

# If empty, re-run script
.\scripts\configuration\configure-environment.ps1 -Token "ghp_xxx" -AzureDevOpsPAT "xxx"
```

---

##  Testing Your Setup

### Test GitHub Integration
```powershell
dotnet test tests/AgenticPipeline.IntegrationTests --filter "FullyQualifiedName~GitHubServiceIntegrationTests"
```

### Test Webhook Endpoint
```powershell
curl http://localhost:5000/api/webhook/test
```

Expected response:
```json
{
  "status": "Webhook endpoint is reachable",
  "gitHub": {
    "logServiceConfigured": true
  },
  "azureDevOps": {
    "configured": true,
    "organization": "your-org-name"
  }
}
```

---

##  Additional Resources

- [GitHub PAT Documentation](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
- [Azure DevOps PAT Documentation](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate)
- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration)

---

**Last Updated:** 2026-04-25
**Status:** Multi-platform support (GitHub + Azure DevOps)
