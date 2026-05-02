# Scripts Directory

**Essential scripts for configuring and running the Agentic Pipeline.**

---

##  Structure

```
scripts/
├── configuration/        # Complete environment setup (ESSENTIAL)
│   ├── configure-environment.ps1
│   └── README.md
└── utils/               # Utilities
    └── test-env.ps1

Note: Legacy AWS scripts and testing scripts are archived in docs/archive/scripts/
```

---

##  Essential Script

All configuration has been consolidated into a single script for ease of use.

### Configure Environment (All-in-One Setup)

**Script:** `configuration/configure-environment.ps1`

**Purpose:** Configure AWS credentials, GitHub tokens, Azure DevOps tokens, and Teams notifications

**Usage:**

**Full configuration (recommended):**
```powershell
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_your_github_token" `
    -AzureDevOpsPAT "your_azure_devops_pat" `
    -AWSProfileName "YourAWSProfile" `
    -AWSRegion "us-west-2" `
    -Owner "your-github-username" `
    -Repo "your-repo-name"
```

**Without AWS (if already configured):**
```powershell
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_your_github_token" `
    -AzureDevOpsPAT "your_azure_devops_pat" `
    -SkipAWS
```

**Minimal configuration (GitHub only):**
```powershell
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_your_github_token"
```

**Features:**
- AWS SSO login and credential extraction
- Sets USER-level environment variables (persist across sessions)
- GitHub Personal Access Token configuration
- Azure DevOps Personal Access Token configuration
- Teams webhook URL configuration (optional)
- Webhook secret configuration (optional)
- Credentials available system-wide until expiration

**What it configures:**
- AWS credentials for Bedrock access (AWS SSO)
- GitHub token for commit analysis and auto-fix PRs
- Azure DevOps PAT for fetching build logs
- Teams webhook for failure notifications
- Repository defaults

**Documentation:**
- [Configuration README](configuration/README.md)
- [AWS Setup Guide](../docs/archive/setup/AWS_Setup_Guide.md)

**Note:** Legacy standalone AWS script (`set-sso-creds-persistent.ps1`) has been moved to `docs/archive/scripts/aws-legacy/` and merged into this unified configuration script.

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
- `set-sso-creds-persistent.ps1` - OLD standalone AWS setup (merged into configure-environment.ps1)
- `set-sso-creds.ps1` - Session-only credential setup (non-persistent)
- `get-sso-creds.ps1` - Retrieve credentials from AWS SSO
- `aws-helper.ps1` / `aws-helper.sh` - AWS CLI helpers

See **[Archived Scripts README](../docs/archive/scripts/README.md)** for complete archive documentation.

---

##  Quick Start Workflow

### One-Command Setup (Recommended):

```powershell
# Configure everything in one command
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_your_github_token" `
    -AzureDevOpsPAT "your_azure_pat" `
    -AWSProfileName "YourAWSProfile" `
    -AWSRegion "us-west-2"

# Run the API
cd src\AgenticPipeline.Api
dotnet run
```

### Separate Steps (If Preferred):

```powershell
# 1. Configure all credentials (AWS + GitHub + Azure DevOps)
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_XXX" `
    -AzureDevOpsPAT "XXX" `
    -AWSProfileName "YourProfile" `
    -AWSRegion "us-west-2"

# 2. Verify environment
.\scripts\utils\test-env.ps1

# 3. Run the API
cd src\AgenticPipeline.Api
dotnet run
```

### Skip AWS Configuration:

```powershell
# If AWS is already configured separately
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_XXX" `
    -AzureDevOpsPAT "XXX" `
    -SkipAWS
```

---

## ️ Important Notes

### Credential Expiration
- **AWS SSO tokens** expire after 8-12 hours
- **GitHub PAT** and **Azure DevOps PAT** expire based on your settings (typically 90 days)
- Re-run credential scripts when you see authentication errors
- Error indicators: "ExpiredToken", "InvalidToken", "Unable to get IAM security credentials", "401 Unauthorized"

### Persistent Credentials
The `configure-environment.ps1` script sets **USER-level** environment variables that:
-  Work across all applications
-  Work in Visual Studio
-  Persist after closing PowerShell
-  Persist after system reboot
-  Can be cleared manually (System → Environment Variables)

**Note:** Legacy session-only credential scripts (`set-sso-creds.ps1`) are archived in `docs/archive/scripts/aws-legacy/` for reference.

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
