# 🚀 START HERE - Day 9 (Tomorrow)

**Date:** 2026-04-02 (Tuesday)
**Phase:** Phase 2 - Day 9 (Commit Analysis)
**Status:** Day 8 Complete ✅ | Ready for Integration Test

---

## ⚡ Quick Start (First Thing Tomorrow)

### Step 1: Refresh AWS Credentials (2 minutes)
```powershell
cd C:\Anish\Source\Agentic_Pipeline
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YOUR_AWS_PROFILE" -Region "us-west-2"
```
Replace `YOUR_AWS_PROFILE` with your actual AWS SSO profile name.

### Step 2: Run Phase 2 Integration Test (10-15 minutes)
```bash
cd C:\Anish\Source\Agentic_Pipeline\tests\Phase2IntegrationTest
dotnet run
```

**What This Test Does:**
- **Scenario A**: Analyzes failure log WITHOUT commit context (baseline)
- **Scenario B**: Analyzes failure log WITH GitHub commit context (enhanced)
- Compares the results to show the value of commit-aware RCA
- Uses real AWS Bedrock/Claude + real GitHub API + real sample logs

**Expected Output:**
- ✅ Baseline RCA results (root cause, confidence, recommendations)
- ✅ Fetched commits from GitHub with file changes
- ✅ Enhanced RCA with suspected commit identification
- ✅ Side-by-side comparison showing improvement
- ⏱️ Takes 30-60 seconds to complete

---

## 📋 What We Did Yesterday (Day 8 Summary)

### ✅ Completed:
1. **GitHub Integration**
   - Installed Octokit 14.0.0
   - Created ICommitAnalyzer interface
   - Implemented GitHubService with full GitHub API integration
   - Connection test: PASSED ✅

2. **Commit Analyzer Plugin**
   - Created CommitAnalyzerPlugin with 2 Semantic Kernel functions:
     - `AnalyzeCommitContext()` - Fetches commits in time range
     - `CorrelateCommitsWithFailure()` - AI-powered commit-failure correlation
   - Plugin test: PASSED ✅

3. **EF Core Setup**
   - Installed EF Core SQLite 9.0.0 (compatible with .NET 9)
   - Installed EF Core Design tools
   - Ready for database implementation

4. **Integration Test Created**
   - Comprehensive end-to-end test ready
   - Waiting for AWS credentials to run

### 📊 Current Status:
- Phase 1: 100% ✅
- Phase 2 - Day 8: 100% ✅
- Phase 2 Overall: ~14% (Day 8 of 7 days)

---

## 📝 Today's Tasks (Day 9 - Tuesday)

### Morning (3-4 hours):
- [ ] **Run Integration Test** (15 min)
  - Validate commit-aware RCA improves analysis quality
  - Document results
  - Take screenshots if helpful

- [ ] **Update RcaResult Model** (30 min)
  - Add `SuspectedCommits` property
  - Add `CommitCorrelation` metadata
  - Update JSON serialization

- [ ] **Enhance RcaPlugin** (2 hours)
  - Add optional commit context parameter to `AnalyzeFailure()`
  - Update system prompt for commit-aware analysis
  - Test with real commit data

### Afternoon (3-4 hours):
- [ ] **Create Enhanced API Endpoint** (2 hours)
  - New endpoint: `POST /api/pipeline/analyze-with-commits`
  - Accept: log content + GitHub repo info (owner, repo, timestamp)
  - Orchestrate: GitHub commits → Commit analysis → Enhanced RCA
  - Return: Comprehensive response with commit correlation

- [ ] **Swagger Documentation** (30 min)
  - Document new endpoint
  - Add request/response examples
  - Test via Swagger UI

- [ ] **End-to-End Testing** (1 hour)
  - Test new endpoint with sample logs
  - Test with different repos
  - Verify error handling

### End of Day Checklist:
- [ ] Build: 0 warnings, 0 errors
- [ ] New endpoint working via Swagger UI
- [ ] Commit-aware RCA producing better results than baseline
- [ ] Documentation updated (session.md)
- [ ] Code committed to git

---

## 🎯 Success Criteria for Day 9

By end of day, you should have:
1. ✅ Integration test results showing commit context improves RCA
2. ✅ Enhanced RcaPlugin that accepts commit context
3. ✅ New API endpoint that combines everything
4. ✅ Working end-to-end flow: Log + GitHub → Commit Analysis → Enhanced RCA
5. ✅ All tests passing

---

## 📚 Reference Files

### Key Files to Work With Today:
- `src/AgenticPipeline.Core/Models/RcaResult.cs` - Add commit properties
- `src/AgenticPipeline.Infrastructure/Plugins/RcaPlugin.cs` - Enhance with commit context
- `src/AgenticPipeline.Api/Controllers/PipelineController.cs` - Add new endpoint
- `tests/Phase2IntegrationTest/Program.cs` - Run this first!

### Documentation:
- `docs/tracking/Session.md` - Updated with Day 8 work
- `docs/tracking/STATUS.md` - Current project status
- `docs/planning/Weekly_Breakdown.md` - Detailed day-by-day plan

---

## 💡 Tips

1. **Start with the integration test** - It will show you the value proposition immediately
2. **Keep commits small** - Commit after each major task (model update, plugin update, endpoint)
3. **Test frequently** - Don't wait until end of day to test
4. **Use Swagger UI** - Great for testing new endpoints interactively
5. **Check AWS credentials** - If you get auth errors, re-run the SSO script

---

## 🆘 If You Get Stuck

### AWS Credential Issues:
```powershell
# Re-run SSO script
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YOUR_AWS_PROFILE" -Region "us-west-2"

# Verify credentials
echo $env:AWS_ACCESS_KEY_ID  # Should show key starting with ASIA...
```

### GitHub Rate Limits:
- Unauthenticated: 60 requests/hour
- With PAT: 5000 requests/hour
- If needed, create PAT at: https://github.com/settings/tokens

### Build Errors:
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

---

## 📞 Quick Commands

```bash
# Navigate to project root
cd C:\Anish\Source\Agentic_Pipeline

# Run integration test
cd tests\Phase2IntegrationTest && dotnet run

# Build solution
dotnet build

# Run API
cd src\AgenticPipeline.Api && dotnet run

# Open Swagger UI
# Navigate to: http://localhost:5005/
```

---

**Ready to go! 🚀**

When you start tomorrow, just:
1. Open this file
2. Refresh AWS credentials (Step 1)
3. Run integration test (Step 2)
4. Follow the checklist

Good luck! The integration test will be exciting - you'll see the commit-aware RCA in action! 🎉
