#  Start Here - Agentic Pipeline

**AI-powered CI/CD failure analysis and auto-fix pipeline - Production Ready**

---

##  Quick Navigation

**New User?** Start here:
1.  **[Documentation Index](docs/README.md)** - Master table of contents
2. ️ **[Scripts Documentation](scripts/README.md)** - Setup and configuration scripts
3. ️ **[Organization Summary](ORGANIZATION_SUMMARY.md)** - How everything is organized

---

##  Quick Start (5 Minutes)

### Step 1: Configure AWS Credentials
```powershell
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YourProfile" -Region "us-west-2"
```
[ Detailed Guide](docs/archive/setup/AWS_Setup_Guide.md)

### Step 2: Configure GitHub & Azure DevOps Tokens
```powershell
.\scripts\configuration\configure-environment.ps1 `
    -Token "ghp_your_github_token" `
    -AzureDevOpsPAT "your_azure_devops_pat"
```
[ Detailed Guide](scripts/configuration/README.md)

### Step 3: Run the API
```powershell
cd src\AgenticPipeline.Api
dotnet run
```

### Step 4: Test
Open browser to: `https://localhost:7056`

---

##  Essential Documentation

### Setup & Configuration
- **[AWS Setup Guide](docs/archive/setup/AWS_Setup_Guide.md)** - Configure AWS Bedrock
- **[Quick Reference](docs/archive/setup/QUICK_REFERENCE.md)** - Command cheat sheet
- **[Configuration README](scripts/configuration/README.md)** - GitHub & Azure DevOps tokens

### Integration
- **[Azure DevOps Integration](docs/archive/guides/Azure_DevOps_Integration_Guide.md)** - Complete setup guide
- **[Azure DevOps Webhook Setup](docs/archive/guides/Azure_DevOps_Webhook_Setup.md)** - Webhook configuration
- **[Azure DevOps vs GitHub Actions](docs/archive/guides/Azure_DevOps_vs_GitHub_Actions.md)** - Platform comparison

### Features
- **[Auto-Fix PR Guide](docs/archive/guides/Auto_Fix_PR_Guide.md)** - How auto-fix works
- **[Pattern Detection](docs/archive/guides/Pattern_Detection_Explained.md)** - Machine learning pattern recognition
- **[Testing Guide](docs/archive/guides/Testing_Guide.md)** - Test the pipeline

### API
- **[Phase 3 API Guide](docs/archive/api/Phase3_API_Guide.md)** - API endpoints
- **[Phase 4 API Examples](docs/archive/api/Phase4_API_Examples.md)** - Working examples

---

## ️ Essential Scripts

### Configuration (Required)
```powershell
# AWS credentials (daily/as needed)
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "XXX" -Region "us-west-2"

# GitHub & Azure DevOps tokens (one-time setup)
.\scripts\configuration\configure-environment.ps1 -Token "ghp_XXX" -AzureDevOpsPAT "XXX"

# Verify environment
.\scripts\utils\test-env.ps1
```

[ Full Scripts Documentation](scripts/README.md)

---

## ️ Project Structure

```
C:\Anish\Source\Agentic_Pipeline\
│
├── docs/                       DOCUMENTATION
│   ├── README.md              Master Table of Contents
│   ├── setup/                Essential setup guides
│   ├── guides/               User guides and features
│   ├── api/                  API documentation
│   ├── testing/              Test guides
│   ├── fixes/                Troubleshooting and fixes
│   └── archive/              Historical docs and scripts (preserved)
│       ├── implementation/   Technical specs
│       ├── tracking/         Development history
│       ├── testing/          Historical tests
│       ├── planning/         Project planning
│       └── scripts/          Testing and legacy scripts
│
├── scripts/                    SCRIPTS
│   ├── README.md              Scripts documentation
│   ├── aws/                  AWS credential setup (ESSENTIAL)
│   ├── configuration/        GitHub/Azure DevOps setup (ESSENTIAL)
│   └── utils/                Utilities and verification
│
├── src/                        SOURCE CODE
│   ├── AgenticPipeline.Api/          API endpoints
│   ├── AgenticPipeline.Core/         Core models and interfaces
│   └── AgenticPipeline.Infrastructure/ Services and implementations
│
└── START_HERE.md              YOU ARE HERE
```

---

##  Common Tasks

### First-Time Setup
```powershell
# 1. Set AWS credentials
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YourProfile" -Region "us-west-2"

# 2. Configure tokens
.\scripts\configuration\configure-environment.ps1 -Token "ghp_XXX" -AzureDevOpsPAT "XXX"

# 3. Update appsettings.json with your org/repo names

# 4. Run API
cd src\AgenticPipeline.Api
dotnet run
```

### Daily Development
```powershell
# Check if AWS credentials expired
.\scripts\utils\test-env.ps1

# If expired, refresh:
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YourProfile" -Region "us-west-2"

# Run API
cd src\AgenticPipeline.Api
dotnet run
```

### Testing a Feature
```powershell
# See testing guides:
# - docs/archive/guides/Quick_Start_Testing.md
# - docs/archive/testing/Auto_Fix_Testing_Guide.md
# - docs/archive/testing/Phases345_Testing_Guide.md
```

### Troubleshooting
```powershell
# Check environment
.\scripts\utils\test-env.ps1

# See troubleshooting docs:
# - docs/archive/fixes/AutoFix_Token_Limit_Fix.md
# - scripts/README.md#troubleshooting
# - docs/archive/setup/AWS_Setup_Guide.md#troubleshooting
```

---

##  Find What You Need

### "How do I set up AWS?"
→ [docs/archive/setup/AWS_Setup_Guide.md](docs/archive/setup/AWS_Setup_Guide.md)

### "How do I configure Azure DevOps?"
→ [docs/archive/guides/Azure_DevOps_Integration_Guide.md](docs/archive/guides/Azure_DevOps_Integration_Guide.md)

### "How does auto-fix work?"
→ [docs/archive/guides/Auto_Fix_PR_Guide.md](docs/archive/guides/Auto_Fix_PR_Guide.md)

### "What are the API endpoints?"
→ [docs/archive/api/Phase3_API_Guide.md](docs/archive/api/Phase3_API_Guide.md)

### "How do I test the pipeline?"
→ [docs/archive/guides/Quick_Start_Testing.md](docs/archive/guides/Quick_Start_Testing.md)

### "I'm getting an error..."
→ [docs/archive/fixes/](docs/archive/fixes/) or README troubleshooting sections

### "What scripts do I need?"
→ [scripts/README.md](scripts/README.md)

### "Where are the old docs and scripts?"
→ [docs/archive/](docs/archive/) - all preserved for reference (includes `docs/archive/scripts/`)

---

##  Project Status

**Version:** 1.0 Production Ready 

**Features:**
-  Root Cause Analysis with AWS Bedrock Claude
-  GitHub commit correlation
-  Azure DevOps build log integration
-  Pattern detection with K-means clustering
-  Auto-fix PR generation
-  Teams notifications
-  Webhook integration (Azure DevOps + GitHub)
-  HTML reports

**Integrations:**
-  AWS Bedrock (Claude Sonnet 4.6)
-  AWS Bedrock (Titan Embeddings v2)
-  GitHub (via Octokit)
-  Azure DevOps (REST API)
-  Microsoft Teams (via webhooks)

**Tested:** End-to-end 

---

##  Organization Summary

**Recent Changes (2026-04-25):**
-  Documentation reorganized with master TOC
-  Development docs archived (preserved)
-  Essential docs easily accessible
-  Scripts organized by importance
-  Testing scripts archived
-  Cross-references added throughout

**Result:**
-  17 essential documentation files
-  27 archived documentation files (historical)
-  3 essential scripts
-  12 archived scripts in `docs/archive/scripts/` (testing/legacy)

See **[ORGANIZATION_SUMMARY.md](ORGANIZATION_SUMMARY.md)** for complete details.

---

## 🆘 Getting Help

### Documentation
- **[Master Index](docs/README.md)** - All documentation
- **[Scripts Guide](scripts/README.md)** - All scripts

### Troubleshooting
- **[Common Issues](scripts/README.md#troubleshooting)** - Script errors
- **[AWS Issues](docs/archive/setup/AWS_Setup_Guide.md#troubleshooting)** - Credential problems
- **[Recent Fixes](docs/archive/fixes/)** - Known issues and solutions

### Quick Commands
- **[Quick Reference](docs/archive/setup/QUICK_REFERENCE.md)** - Command cheat sheet

---

##  You're Ready!

**Next Steps:**
1. Follow the [Quick Start](#quick-start-5-minutes) above
2. Read the [Master Documentation Index](docs/README.md)
3. Configure your integrations
4. Test the pipeline

**Need Help?**
- Check the troubleshooting sections in READMEs
- Review [docs/archive/fixes/](docs/archive/fixes/) for recent issues
- Ensure credentials are configured correctly

---

**Project Location:** `C:\Anish\Source\Agentic_Pipeline`
**Last Updated:** 2026-04-25
**Status:** Production Ready 
