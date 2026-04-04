# Scripts Directory

All PowerShell and Bash scripts for the project.

---

## 📁 Structure

```
scripts/
├── aws/        # AWS credential and setup scripts
└── utils/      # Utility and testing scripts
```

---

## 🔐 AWS Scripts (`aws/`)

### set-sso-creds-persistent.ps1 ⭐ **[RECOMMENDED]**
**Purpose:** Extracts AWS SSO credentials and sets them as USER-level environment variables

**Usage:**
```powershell
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "anishuser1" -Region "us-west-2"
```

**Features:**
- Sets persistent USER-level environment variables
- Automatically launches Visual Studio with the solution
- Credentials available system-wide until expiration
- Best for development with Visual Studio

**When to use:**
- Starting a new development session
- After SSO token expiration (8-12 hours)
- Before running the API from Visual Studio

---

### set-sso-creds.ps1
**Purpose:** Extracts AWS SSO credentials and sets them as session environment variables

**Usage:**
```powershell
.\scripts\aws\set-sso-creds.ps1 -ProfileName "anishuser1" -Region "us-west-2"
```

**Features:**
- Sets environment variables for current PowerShell session only
- Does not persist across windows
- Lighter weight, no VS launch

**When to use:**
- Quick credential refresh in current session
- Running from command line only
- Don't need Visual Studio

---

### aws-helper.ps1 / aws-helper.sh
**Purpose:** Helper scripts for common AWS operations

**Usage:**
```powershell
# PowerShell
.\scripts\aws\aws-helper.ps1 <command>

# Bash
./scripts/aws/aws-helper.sh <command>
```

**Commands:**
- `info` - Show AWS identity and username
- `list-keys` - List your access keys
- `create-key` - Create a new access key
- `check-bedrock` - Check Bedrock model access
- `verify-all` - Run complete verification

**Examples:**
```powershell
.\scripts\aws\aws-helper.ps1 info
.\scripts\aws\aws-helper.ps1 check-bedrock
.\scripts\aws\aws-helper.ps1 verify-all
```

---

## 🛠️ Utility Scripts (`utils/`)

### test-env.ps1
**Purpose:** Verifies that AWS environment variables are set correctly

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
AWS_PROFILE:           anishuser1
AWS_REGION:            us-west-2
```

**When to use:**
- After running set-sso-creds scripts
- Debugging credential issues
- Verifying environment before running API

---

## 🚀 Quick Start Workflow

### For Visual Studio Development:
```powershell
# 1. Set credentials (launches VS automatically)
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "anishuser1" -Region "us-west-2"

# 2. Press F5 in Visual Studio to run the API
```

### For Command Line Development:
```powershell
# 1. Set credentials for current session
.\scripts\aws\set-sso-creds.ps1 -ProfileName "anishuser1" -Region "us-west-2"

# 2. Verify environment
.\scripts\utils\test-env.ps1

# 3. Run the API
cd src\AgenticPipeline.Api
dotnet run
```

---

## ⚠️ Important Notes

### Credential Expiration
- SSO tokens expire after **8-12 hours**
- Re-run the credential script when you see authentication errors
- Error indicators: "ExpiredToken", "InvalidToken", "Unable to get IAM security credentials"

### Script Locations
- **All scripts moved to `scripts/` folder** (as of 2026-04-01)
- Old locations no longer valid
- Update any bookmarks or shortcuts

### Persistent vs Session
- **Persistent** (`set-sso-creds-persistent.ps1`):
  - ✅ Works across all applications
  - ✅ Works in Visual Studio
  - ✅ Persists after closing PowerShell
  - ❌ Need to clear manually (System → Environment Variables)

- **Session** (`set-sso-creds.ps1`):
  - ✅ Auto-clears when closing PowerShell
  - ✅ Doesn't affect other windows
  - ❌ Only works in current session
  - ❌ Need to re-run for each new window

---

## 🔍 Troubleshooting

### "The term 'aws' is not recognized"
Install AWS CLI: https://aws.amazon.com/cli/

### "The config profile could not be found"
Check your profile name in `~/.aws/config` - it might be lowercase (`anishuser1` not `anishUser1`)

### "Script is not digitally signed"
Run PowerShell as Administrator and execute:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Credentials not working in API
1. Verify with `.\scripts\utils\test-env.ps1`
2. If not set, re-run credential script
3. Make sure you're in the **same PowerShell session** (for session-based) or restart Visual Studio (for persistent)

---

## 📚 Related Documentation

- [AWS Setup Guide](../docs/setup/AWS_Setup_Guide.md) - Complete AWS configuration
- [Quick Reference](../docs/setup/QUICK_REFERENCE.md) - Command cheat sheet
- [Phase 1 Guide](../docs/guides/PHASE1_COMPLETE.md) - Testing the API

---

**Last Updated:** 2026-04-01
**Scripts Version:** 1.0
