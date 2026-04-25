# Scripts Directory

**Essential scripts for configuring and running the Agentic Pipeline.**

---

##  Structure

```
scripts/
├── aws/                  # AWS credential setup (ESSENTIAL)
│   └── set-sso-creds-persistent.ps1
├── configuration/        # GitHub & Azure DevOps setup (ESSENTIAL)
│   ├── configure-environment.ps1
│   └── README.md
└── utils/               # Utilities
    └── test-env.ps1

Note: Testing and legacy scripts are archived in docs/archive/scripts/
```

---

##  Essential Scripts

These scripts are required to run the project:

### 1. AWS SSO Credentials Setup

**Script:** `aws/set-sso-creds-persistent.ps1`

**Purpose:** Extract AWS SSO credentials and set persistent environment variables for Bedrock access

**Usage:**
```powershell
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YourProfile" -Region "us-west-2"
```

**Features:**
- Sets USER-level environment variables (persist across sessions)
- Automatically launches Visual Studio with the solution
- Credentials available system-wide until expiration (8-12 hours)
- Best for development with Visual Studio

**When to use:**
- Starting a new development session
- After SSO token expiration
- Before running the API from Visual Studio

**Documentation:** See [AWS Setup Guide](../docs/archive/setup/AWS_Setup_Guide.md)

---

### 2. GitHub & Azure DevOps Configuration

**Script:** `configuration/configure-environment.ps1`

**Purpose:** Configure GitHub and Azure DevOps Personal Access Tokens

**Usage:**
```powershell
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_your_github_token" `
    -AzureDevOpsPAT "your_azure_devops_pat" `
    -Owner "your-github-username" `
    -Repo "your-repo-name"
```

**Features:**
- Sets GitHub Personal Access Token (for commit analysis and auto-fix PRs)
- Sets Azure DevOps Personal Access Token (for fetching build logs)
- Configures repository defaults
- Optional: Teams webhook URL for notifications

**Required for:**
- GitHub commit analysis
- Azure DevOps build log fetching
- Auto-fix PR creation
- Teams notifications (optional)

**Documentation:** See [Configuration README](configuration/README.md)

---

## ️ Utility Scripts

### test-env.ps1

**Location:** `utils/test-env.ps1`

**Purpose:** Verify AWS environment variables are set correctly

**Usage:**
```powershell
.\scripts\utils\test-env.ps1
```

**Output:**
```
=== Environment Variables Check ===
AWS_ACCESS_KEY_ID:     ASIA2KX4BX...
AWS_SECRET_ACCESS_KEY: ****
AWS_SESSION_TOKEN:     SET
AWS_PROFILE:           YourProfile
AWS_REGION:            us-west-2
```

**When to use:**
- After running AWS credential scripts
- Debugging authentication issues
- Verifying environment before running API

---

##  Archived Scripts

Historical and testing scripts have been moved to `docs/archive/scripts/`:

### Testing Scripts (`docs/archive/scripts/testing/`)
Component-specific test scripts:
- `test-webhook.ps1` - Test webhook endpoints
- `test-github-logs.ps1` - Test GitHub Actions log fetching
- `test-azure-logs.ps1` - Test Azure DevOps log fetching
- `test-autofix.ps1` - Test auto-fix generation
- `test-teams-notification.ps1` - Test Teams notifications
- `run-embedding-tests.ps1` - Test embedding generation
- `setup-test-scenario.ps1` - Set up test failure scenarios

### Utility Scripts (`docs/archive/scripts/utilities/`)
- `Problem_trobleshoot.ps1` - Troubleshooting helper

### Legacy AWS Scripts (`docs/archive/scripts/aws-legacy/`)
- `set-sso-creds.ps1` - Session-only credential setup (non-persistent)
- `get-sso-creds.ps1` - Retrieve credentials from AWS SSO
- `aws-helper.ps1` / `aws-helper.sh` - AWS CLI helpers

See **[Archived Scripts README](../docs/archive/scripts/README.md)** for complete archive documentation.

---

##  Quick Start Workflow

### For Visual Studio Development:

```powershell
# 1. Set AWS credentials (launches VS automatically)
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YourProfile" -Region "us-west-2"

# 2. Configure GitHub and Azure DevOps tokens (one-time setup)
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_XXX" `
    -AzureDevOpsPAT "XXX"

# 3. Press F5 in Visual Studio to run the API
```

### For Command Line Development:

```powershell
# 1. Set AWS credentials
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YourProfile" -Region "us-west-2"

# 2. Configure GitHub and Azure DevOps tokens (one-time setup)
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_XXX" `
    -AzureDevOpsPAT "XXX"

# 3. Verify environment
.\scripts\utils\test-env.ps1

# 4. Run the API
cd src\AgenticPipeline.Api
dotnet run
```

---

## ️ Important Notes

### Credential Expiration
- **AWS SSO tokens** expire after 8-12 hours
- **GitHub PAT** and **Azure DevOps PAT** expire based on your settings (typically 90 days)
- Re-run credential scripts when you see authentication errors
- Error indicators: "ExpiredToken", "InvalidToken", "Unable to get IAM security credentials", "401 Unauthorized"

### Persistent vs Session Credentials
- **Persistent** (`set-sso-creds-persistent.ps1`):
  -  Works across all applications
  -  Works in Visual Studio
  -  Persists after closing PowerShell
  -  Need to clear manually (System → Environment Variables)

- **Session** (archived: `set-sso-creds.ps1`):
  -  Auto-clears when closing PowerShell
  -  Doesn't affect other windows
  -  Only works in current session
  -  Need to re-run for each new window

### Security Best Practices
-  Store PATs in environment variables (done by configure-environment.ps1)
-  Never commit tokens to git
-  Set token expiration dates (90 days recommended)
-  Revoke tokens immediately if exposed
-  Don't put tokens in appsettings.json
-  Don't share tokens with others

---

##  Troubleshooting

### "The term 'aws' is not recognized"
**Solution:** Install AWS CLI: https://aws.amazon.com/cli/

### "The config profile could not be found"
**Solution:** Check your profile name in `~/.aws/config`

### "Script is not digitally signed"
**Solution:** Run PowerShell as Administrator:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Credentials not working in API
**Solution:**
1. Verify with `.\scripts\utils\test-env.ps1`
2. If not set, re-run credential scripts
3. Restart Visual Studio if using persistent credentials

### "401 Unauthorized" from Azure DevOps
**Solution:**
1. Check PAT hasn't expired in Azure DevOps settings
2. Verify PAT has **Build (Read)** and **Code (Read)** scopes
3. Re-run `configure-environment.ps1` with updated PAT

### "401 Unauthorized" from GitHub
**Solution:**
1. Check token hasn't expired: https://github.com/settings/tokens
2. Verify token has `repo` and `workflow` scopes
3. Re-run `configure-environment.ps1` with updated token

---

##  Related Documentation

### Setup Guides
- **[AWS Setup Guide](../docs/archive/setup/AWS_Setup_Guide.md)** - Complete AWS Bedrock configuration
- **[Configuration README](configuration/README.md)** - Detailed token setup guide
- **[Quick Reference](../docs/archive/setup/QUICK_REFERENCE.md)** - Command cheat sheet

### Integration Guides
- **[Azure DevOps Integration](../docs/archive/guides/Azure_DevOps_Integration_Guide.md)** - Full integration setup
- **[Azure DevOps Webhook Setup](../docs/archive/guides/Azure_DevOps_Webhook_Setup.md)** - Webhook configuration

### Main Documentation
- **[Documentation Index](../docs/README.md)** - Master table of contents
- **[Project README](../README.md)** - Project overview

---

##  Script Checklist

### First-Time Setup
- [ ] Run AWS credential script: `aws/set-sso-creds-persistent.ps1`
- [ ] Verify credentials: `utils/test-env.ps1`
- [ ] Create GitHub PAT: https://github.com/settings/tokens
- [ ] Create Azure DevOps PAT: https://dev.azure.com/YOUR-ORG/_usersSettings/tokens
- [ ] Run configuration script: `configuration/configure-environment.ps1`
- [ ] Update appsettings.json with org/repo names
- [ ] Test API connection

### Daily Workflow
- [ ] Check if AWS credentials expired (8-12 hours)
- [ ] Re-run AWS credential script if needed
- [ ] Verify with `test-env.ps1`
- [ ] Start API

### Token Renewal (Every 90 days)
- [ ] Regenerate GitHub PAT
- [ ] Regenerate Azure DevOps PAT
- [ ] Run `configure-environment.ps1` with new tokens
- [ ] Test integrations

---

**Last Updated:** 2026-04-25
**Scripts Version:** 2.0
**Project Status:** Production Ready 
