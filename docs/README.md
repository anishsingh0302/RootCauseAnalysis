# Agentic Pipeline Documentation

**Complete documentation for the AI-powered CI/CD failure analysis and auto-fix pipeline.**

---

##  Table of Contents

- [Quick Start](#quick-start)
- [Setup & Configuration](#setup--configuration)
- [User Guides](#user-guides)
- [API Documentation](#api-documentation)
- [Testing & Troubleshooting](#testing--troubleshooting)
- [Release Notes & Fixes](#release-notes--fixes)
- [Archive & Historical Docs](#archive--historical-docs)

---

##  Quick Start

**New to the project? Start here:**

1. **[Quick Reference Guide](archive/setup/QUICK_REFERENCE.md)** - Essential commands and workflows
2. **[AWS Setup Guide](archive/setup/AWS_Setup_Guide.md)** - Configure AWS credentials for Bedrock
3. **[Azure DevOps Integration](archive/guides/Azure_DevOps_Integration_Guide.md)** - Set up Azure DevOps webhooks
4. **[Quick Start Testing](archive/guides/Quick_Start_Testing.md)** - Run your first analysis

---

## ️ Setup & Configuration

### Initial Setup
- **[AWS Setup Guide](archive/setup/AWS_Setup_Guide.md)**
  - AWS SSO configuration
  - Bedrock model access
  - Credential management
  - Environment variables

- **[Quick Reference](archive/setup/QUICK_REFERENCE.md)**
  - Command cheat sheet
  - Common workflows
  - Troubleshooting tips

### Integration Setup
- **[Azure DevOps Integration Guide](archive/guides/Azure_DevOps_Integration_Guide.md)**
  - Complete setup walkthrough
  - Service hooks configuration
  - Build pipeline integration
  - Testing and verification

- **[Azure DevOps Webhook Setup](archive/guides/Azure_DevOps_Webhook_Setup.md)**
  - Detailed webhook configuration
  - Payload structure
  - Debugging tips

- **[Configuration README](../scripts/configuration/README.md)**
  - GitHub and Azure DevOps PAT setup
  - Environment variable management
  - Security best practices

### Platform Comparison
- **[Azure DevOps vs GitHub Actions](archive/guides/Azure_DevOps_vs_GitHub_Actions.md)**
  - Feature comparison
  - Use cases
  - Migration guide

---

##  User Guides

### Core Features
- **[Auto-Fix PR Guide](archive/guides/Auto_Fix_PR_Guide.md)**
  - How auto-fix works
  - PR creation process
  - Safety checks and confidence scoring
  - Manual review process

- **[Pattern Detection Explained](archive/guides/Pattern_Detection_Explained.md)**
  - K-means clustering algorithm
  - Similarity analysis
  - Pattern recognition workflow
  - Use cases and benefits

- **[Testing Guide](archive/guides/Testing_Guide.md)**
  - Integration testing
  - Manual testing workflows
  - Test scenarios

### Usage Examples
- **[Usage Examples](archive/guides/Usage_Examples.md)**
  - Common scenarios
  - CLI commands
  - API calls
  - Webhook payloads

---

##  API Documentation

### API Guides
- **[Phase 3 API Guide](archive/api/Phase3_API_Guide.md)**
  - RCA endpoint
  - Commit analysis
  - Pattern detection
  - Response formats

- **[Phase 4 API Examples](archive/api/Phase4_API_Examples.md)**
  - Working code examples
  - Request/response samples
  - Error handling
  - Best practices

### Endpoints Overview

| Endpoint | Purpose | Documentation |
|----------|---------|---------------|
| `POST /archive/api/webhook/azuredevops` | Receive build failure events | [Azure DevOps Guide](archive/guides/Azure_DevOps_Integration_Guide.md) |
| `POST /archive/api/pipeline/orchestrate` | Manual orchestration | [Phase 3 API Guide](archive/api/Phase3_API_Guide.md) |
| `GET /archive/api/pipeline/orchestrate/{id}` | Get orchestration status | [Phase 3 API Guide](archive/api/Phase3_API_Guide.md) |
| `GET /archive/api/pipeline/orchestrate/{id}/html` | Get HTML report | [Phase 3 API Guide](archive/api/Phase3_API_Guide.md) |
| `POST /archive/api/pipeline/rca` | Run root cause analysis | [Phase 3 API Guide](archive/api/Phase3_API_Guide.md) |
| `POST /archive/api/pipeline/commit-analysis` | Analyze commits | [Phase 3 API Guide](archive/api/Phase3_API_Guide.md) |

---

##  Testing & Troubleshooting

### Testing Guides
- **[Quick Start Testing](archive/guides/Quick_Start_Testing.md)**
  - Fast verification workflow
  - Smoke tests
  - Health checks

- **[Auto-Fix Testing Guide](archive/testing/Auto_Fix_Testing_Guide.md)**
  - Testing auto-fix generation
  - PR creation testing
  - Safety validation

- **[Phases 3-4-5 Testing Guide](archive/testing/Phases345_Testing_Guide.md)**
  - Comprehensive testing
  - Integration scenarios
  - End-to-end workflows

### Troubleshooting
- **[Auto-Fix Token Limit Fix](archive/fixes/AutoFix_Token_Limit_Fix.md)**
  - JSON truncation issue resolution
  - Token optimization
  - Line-based changes implementation

---

##  Release Notes & Fixes

### Recent Fixes
- **[Auto-Fix Token Limit Fix](archive/fixes/AutoFix_Token_Limit_Fix.md)** (2026-04-25)
  - Issue: JSON parsing failure due to token truncation
  - Solution: Increased MaxTokens, added line-based changes
  - Status:  Fixed and tested

### Completion Reports (Archived)
Historical completion reports have been moved to `archive/tracking/`:
- Phase 2: GitHub integration and commit analysis
- Phase 3: Pattern detection and orchestration
- Phase 4: Azure DevOps integration
- Phase 5: Auto-fix PR generation
- Phase 6: Teams notifications

---

##  Archive & Historical Docs

Development and tracking documents have been archived for reference:

### Archive Structure
```
docs/archive/
├── implementation/     # Implementation plans and technical specs
├── tracking/          # Daily summaries and completion reports
├── archive/testing/           # Historical test results
└── planning/          # Project planning documents
```

### Accessing Archived Docs
- **Implementation Plans**: `docs/archive/implementation/`
- **Progress Tracking**: `docs/archive/tracking/`
- **Test Results**: `docs/archive/archive/testing/`
- **Planning Docs**: `docs/archive/planning/`

These documents are preserved for:
- Historical reference
- Understanding design decisions
- Tracking project evolution
- Onboarding new team members

---

## ️ Scripts Reference

Essential scripts for running the project:

### Configuration Scripts
- **[AWS SSO Setup](../scripts/aws/set-sso-creds-persistent.ps1)**
  - Set persistent AWS credentials
  - Usage: `.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "XXX" -Region "us-west-2"`

- **[Environment Configuration](../scripts/configuration/configure-environment.ps1)**
  - Configure GitHub and Azure DevOps tokens
  - Usage: `.\scripts\configuration\configure-environment.ps1 -Token "ghp_XXX" -AzureDevOpsPAT "XXX"`

### Testing & Utilities (Archived)
Additional scripts are available in `docs/archive/scripts/`:
- `archive/testing/` - Test scripts for individual components
- `utilities/` - Helper and diagnostic scripts
- `aws-legacy/` - Legacy AWS credential scripts

See **[Scripts README](../scripts/README.md)** for essential scripts and **[Archived Scripts](archive/scripts/README.md)** for archive/testing/legacy scripts.

---

##  Additional Resources

### External Documentation
- [AWS Bedrock Documentation](https://docs.aws.amazon.com/bedrock/)
- [Azure DevOps REST API](https://learn.microsoft.com/en-us/rest/archive/api/azure/devops/)
- [GitHub REST API](https://docs.github.com/en/rest)
- [Octokit.NET](https://github.com/octokit/octokit.net)

### Project Repository
- **Location**: `C:\Anish\Source\Agentic_Pipeline`
- **Main Branch**: `master`
- **CI/CD**: Azure DevOps pipelines

---

##  Getting Help

### Quick Troubleshooting
1. Check [Quick Reference](archive/setup/QUICK_REFERENCE.md) for common commands
2. Review [AWS Setup Guide](archive/setup/AWS_Setup_Guide.md) for credential issues
3. See [Auto-Fix Token Limit Fix](archive/fixes/AutoFix_Token_Limit_Fix.md) for recent issues

### Common Issues
- **AWS Credentials**: See [AWS Setup Guide](archive/setup/AWS_Setup_Guide.md#troubleshooting)
- **Azure DevOps 401**: See [Configuration README](../scripts/configuration/README.md#troubleshooting)
- **Auto-Fix Failures**: See [Auto-Fix Token Limit Fix](archive/fixes/AutoFix_Token_Limit_Fix.md)

---

##  Document Status Legend

| Status | Meaning |
|--------|---------|
|  Current | Up-to-date and actively maintained |
|  Archived | Preserved for reference, may be outdated |
|  In Progress | Under active development |

---

## ️ Document Index

### By Category

**Setup (Essential)**
- AWS Setup Guide
- Quick Reference
- Configuration README

**Integration (Essential)**
- Azure DevOps Integration Guide
- Azure DevOps Webhook Setup
- Azure DevOps vs GitHub Actions

**Usage (Essential)**
- Auto-Fix PR Guide
- Pattern Detection Explained
- Testing Guide
- Quick Start Testing

**API (Reference)**
- Phase 3 API Guide
- Phase 4 API Examples

**Troubleshooting (Essential)**
- Auto-Fix Token Limit Fix
- Auto-Fix Testing Guide

**Archive (Historical)**
- Implementation plans
- Tracking documents
- Test results
- Planning documents

---

**Last Updated:** 2026-04-25
**Documentation Version:** 1.0
**Project Status:** Production Ready 
