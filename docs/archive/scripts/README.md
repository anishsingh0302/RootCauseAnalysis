# Scripts Archive

**Testing, utility, and legacy scripts preserved for reference.**

This folder contains scripts used during development and testing that are not essential for running the project but may be useful for component testing or debugging.

**Location:** `docs/archive/scripts/` (moved from `scripts/archive/` to consolidate all archived content)

---

##  Archive Structure

```
docs/archive/scripts/
├── testing/         # Component test scripts
├── utilities/       # Helper and diagnostic scripts
└── aws-legacy/      # Legacy AWS credential scripts
```

---

##  Testing Scripts (`testing/`)

Component-specific test scripts for isolated testing:

### test-webhook.ps1
**Purpose:** Test webhook endpoints directly

**Usage:**
```powershell
.\docs\archive\scripts\testing\test-webhook.ps1
```

**When to use:**
- Testing webhook payload handling
- Debugging webhook integration
- Simulating Azure DevOps or GitHub webhook events

---

### test-github-logs.ps1
**Purpose:** Test GitHub Actions log fetching

**Usage:**
```powershell
.\docs\archive\scripts\testing\test-github-logs.ps1
```

**When to use:**
- Testing GitHub Actions integration
- Debugging log retrieval issues
- Verifying GitHub PAT permissions

---

### test-azure-logs.ps1
**Purpose:** Test Azure DevOps log fetching with multiple API versions

**Usage:**
```powershell
.\docs\archive\scripts\testing\test-azure-logs.ps1 -BuildId 7
```

**When to use:**
- Testing Azure DevOps API integration
- Debugging 404 or 401 errors
- Verifying Azure DevOps PAT permissions
- Testing different API versions

---

### test-autofix.ps1
**Purpose:** Test auto-fix generation and PR creation

**Usage:**
```powershell
.\docs\archive\scripts\testing\test-autofix.ps1
```

**When to use:**
- Testing auto-fix logic
- Debugging PR creation
- Verifying fix generation accuracy

---

### test-teams-notification.ps1
**Purpose:** Test Teams webhook notifications

**Usage:**
```powershell
.\docs\archive\scripts\testing\test-teams-notification.ps1
```

**When to use:**
- Testing Teams integration
- Debugging notification formatting
- Verifying webhook URL

---

### run-embedding-tests.ps1
**Purpose:** Test embedding generation with Titan

**Usage:**
```powershell
.\docs\archive\scripts\testing\run-embedding-tests.ps1
```

**When to use:**
- Testing AWS Bedrock Titan integration
- Debugging embedding generation
- Performance testing

---

### setup-test-scenario.ps1
**Purpose:** Set up test failure scenarios for end-to-end testing

**Usage:**
```powershell
.\docs\archive\scripts\testing\setup-test-scenario.ps1
```

**When to use:**
- Creating realistic test failures
- End-to-end pipeline testing
- Demo scenarios

---

## ️ Utility Scripts (`utilities/`)

### Problem_trobleshoot.ps1
**Purpose:** Diagnostic and troubleshooting helper

**Usage:**
```powershell
.\docs\archive\scripts\utilities\Problem_trobleshoot.ps1
```

**When to use:**
- Debugging general issues
- Environment diagnostics
- Gathering debug information

---

##  Legacy AWS Scripts (`aws-legacy/`)

### set-sso-creds.ps1 (Legacy)
**Purpose:** Session-only AWS credential setup (non-persistent)

**Usage:**
```powershell
.\docs\archive\scripts\aws-legacy\set-sso-creds.ps1 -ProfileName "YourProfile" -Region "us-west-2"
```

**Why legacy:** Use `scripts/aws/set-sso-creds-persistent.ps1` instead for better developer experience.

**When to use:**
- Need session-only credentials
- Don't want persistent environment variables
- Testing in isolated sessions

---

### get-sso-creds.ps1 (Legacy)
**Purpose:** Retrieve AWS SSO credentials from cache

**Usage:**
```powershell
.\docs\archive\scripts\aws-legacy\get-sso-creds.ps1 -ProfileName "YourProfile"
```

**When to use:**
- Debugging credential retrieval
- Manual credential inspection

---

### aws-helper.ps1 / aws-helper.sh (Legacy)
**Purpose:** AWS CLI helper scripts

**Usage:**
```powershell
# PowerShell
.\docs\archive\scripts\aws-legacy\aws-helper.ps1 verify-all

# Bash
./docs/archive/scripts/aws-legacy/aws-helper.sh verify-all
```

**Commands:**
- `info` - Show AWS identity
- `check-bedrock` - Verify Bedrock access
- `verify-all` - Complete verification

**When to use:**
- Debugging AWS access
- Verifying Bedrock permissions
- Manual AWS checks

---

## ️ Essential Scripts (Not Archived)

For production use, these scripts remain in the main folders:

### Essential Configuration
- **`aws/set-sso-creds-persistent.ps1`** - AWS credential setup (REQUIRED)
- **`configuration/configure-environment.ps1`** - GitHub/Azure DevOps setup (REQUIRED)

### Essential Utilities
- **`utils/test-env.ps1`** - Environment verification

See **[Scripts README](../../../scripts/README.md)** for essential script documentation.

---

##  When to Use Archive Scripts

### Component Testing
When you need to test a specific component in isolation:
1. Use the relevant test script (e.g., `test-azure-logs.ps1`)
2. Review output for specific component behavior
3. Debug issues without running the full pipeline

### Integration Debugging
When the full pipeline is failing:
1. Use test scripts to isolate which component is failing
2. Fix the component
3. Re-test end-to-end

### Performance Testing
When optimizing specific features:
1. Use `run-embedding-tests.ps1` for embedding performance
2. Use test scripts to measure component response times
3. Compare before/after optimizations

### Demo Scenarios
When demonstrating features:
1. Use `setup-test-scenario.ps1` to create failures
2. Trigger webhook manually with `test-webhook.ps1`
3. Show end-to-end flow

---

##  Archive vs Essential Scripts

| Script Type | Location | Purpose |
|-------------|----------|---------|
| **Essential Configuration** | `scripts/aws/`, `scripts/configuration/` | Required for running project |
| **Essential Utilities** | `scripts/utils/` | Verification and diagnostics |
| **Component Testing** | `scripts/archive/testing/` | Isolated component testing |
| **Legacy Scripts** | `scripts/archive/aws-legacy/` | Superseded by better versions |
| **Utilities** | `scripts/archive/utilities/` | Troubleshooting helpers |

---

##  Quick Reference

### Test GitHub Integration
```powershell
.\docs\archive\scripts\testing\test-github-logs.ps1
```

### Test Azure DevOps Integration
```powershell
.\docs\archive\scripts\testing\test-azure-logs.ps1 -BuildId 8
```

### Test Webhooks
```powershell
.\docs\archive\scripts\testing\test-webhook.ps1
```

### Test Auto-Fix
```powershell
.\docs\archive\scripts\testing\test-autofix.ps1
```

### Test Teams Notifications
```powershell
.\docs\archive\scripts\testing\test-teams-notification.ps1
```

### Verify AWS (Legacy)
```powershell
.\docs\archive\scripts\aws-legacy\aws-helper.ps1 verify-all
```

---

##  Related Documentation

- **[Scripts README](../../../scripts/README.md)** - Essential scripts documentation
- **[Documentation Index](../../README.md)** - Master documentation
- **[Testing Guides](../../testing/)** - Full test guides
- **[Troubleshooting](../../fixes/)** - Known issues and fixes

---

**Last Updated:** 2026-04-25
**Archive Purpose:** Component testing and debugging
**Usage:** As needed for isolated testing
