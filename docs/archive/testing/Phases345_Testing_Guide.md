# Comprehensive Testing Guide: Phases 3, 4, and 5

**Test Date:** ____________
**Tester:** ____________
**Environment:** ____________

This guide will help you test the complete end-to-end functionality of Phases 3, 4, and 5 together.

---

## Prerequisites

Before testing, ensure you have:

- [ ] AWS credentials configured (for embeddings and Claude)
- [ ] GitHub personal access token (for commit analysis and comments)
- [ ] Microsoft Teams webhook URL (optional, for notifications)
- [ ] Database initialized (pipeline.db)
- [ ] API running on localhost:5001 or deployed URL

---

## Test Suite Overview

| Phase | Features to Test |
|-------|-----------------|
| **Phase 3** | Embeddings, Similarity Search, Pattern Detection |
| **Phase 4** | Full Orchestration, Webhook Processing |
| **Phase 5** | Teams Notifications, GitHub Comments |

---

## Phase 3 Testing: ML Features

### Test 3.1: Embedding Generation

**Objective:** Verify embeddings are auto-generated when failures are saved.

**Steps:**
```bash
# 1. Start the API
cd C:\Anish\Source\Agentic_Pipeline\src\AgenticPipeline.Api
dotnet run

# 2. Analyze a failure (this auto-generates embedding)
curl -X POST https://localhost:5001/api/pipeline/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Error: NullReferenceException at DatabaseService.cs:line 42"
  }'

# 3. Check the response for a failure ID
# 4. Retrieve the failure and verify embedding exists
curl https://localhost:5001/api/pipeline/history/1
```

**Expected Result:**
- Failure saved successfully
- `embedding` field is not null
- `embeddingDimension` is 512

**Status:**  Pass  Fail

**Notes:**
```

```

---

### Test 3.2: Similarity Search

**Objective:** Find similar past failures using vector similarity.

**Prerequisites:** At least 2-3 failures already analyzed

**Steps:**
```bash
# 1. Analyze multiple different failures first
curl -X POST https://localhost:5001/api/pipeline/analyze \
  -H "Content-Type: application/json" \
  -d '{"logContent": "NullReferenceException in UserService"}'

curl -X POST https://localhost:5001/api/pipeline/analyze \
  -H "Content-Type: application/json" \
  -d '{"logContent": "TimeoutException in ApiClient"}'

curl -X POST https://localhost:5001/api/pipeline/analyze \
  -H "Content-Type: application/json" \
  -d '{"logContent": "NullReferenceException in OrderService"}'

# 2. Search for similar failures to failure #1
curl -X POST https://localhost:5001/api/pipeline/search/similar \
  -H "Content-Type: application/json" \
  -d '{
    "failureId": 1,
    "topN": 5,
    "minSimilarity": 0.5
  }'
```

**Expected Result:**
- Returns list of similar failures
- Similarity scores between 0.0 and 1.0
- Similar error types grouped together (NullRef with NullRef)
- Execution time < 5 seconds

**Actual Results:**
- Similar failures found: _____
- Highest similarity score: _____
- Execution time: _____ms

**Status:**  Pass  Fail

**Notes:**
```

```

---

### Test 3.3: Pattern Detection

**Objective:** Detect recurring failure patterns using K-means clustering.

**Prerequisites:** At least 10+ failures analyzed

**Steps:**
```bash
# 1. Analyze multiple failures of similar types
for i in {1..15}; do
  curl -X POST https://localhost:5001/api/pipeline/analyze \
    -H "Content-Type: application/json" \
    -d "{\"logContent\": \"NullReferenceException in Service $i\"}"
  sleep 2
done

# 2. Detect patterns
curl -X POST https://localhost:5001/api/pipeline/patterns/detect \
  -H "Content-Type: application/json" \
  -d '{
    "clusterCount": null,
    "lookbackDays": 30,
    "minClusterSize": 2
  }'
```

**Expected Result:**
- Returns 2-5 patterns
- Each pattern has:
  - Name and primary error type
  - Failure count >= minClusterSize
  - Frequency (failures/day)
  - Common root causes
  - Sample failure IDs
- Patterns are semantically meaningful

**Actual Results:**
- Patterns detected: _____
- Optimal K (cluster count): _____
- Largest pattern size: _____
- Execution time: _____ms

**Status:**  Pass  Fail

**Notes:**
```

```

---

## Phase 4 Testing: Orchestration

### Test 4.1: Full Pipeline Orchestration (No GitHub Context)

**Objective:** Execute complete pipeline: RCA + Similarity + Patterns.

**Steps:**
```bash
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Error: OutOfMemoryException\nFailed to allocate memory for image processing\nat ImageProcessor.LoadImage()\nat ImageController.Upload()",
    "similarFailuresCount": 5,
    "minSimilarity": 0.7,
    "enablePatternDetection": true,
    "enableCommitAnalysis": false,
    "source": "Manual Test"
  }'
```

**Expected Result:**
- Status: Completed or PartialSuccess
- `failure` object with RCA results
- `similarFailures` array (if historical data exists)
- `assignedPattern` object (if patterns exist)
- `executionSteps` with 3 steps all successful
- `report` field with comprehensive Markdown report
- Total duration < 20 seconds

**Actual Results:**
- Orchestration ID: _________________________
- Status: _________________________
- Error Type: _________________________
- Confidence: _____%
- Similar failures found: _____
- Pattern assigned: Yes  No 
- Execution time: _____s
- Step 1 (RCA): _____s
- Step 2 (Similarity): _____s
- Step 3 (Patterns): _____s

**Status:**  Pass  Fail

**Notes:**
```

```

---

### Test 4.2: Full Pipeline with GitHub Context

**Objective:** Execute pipeline with commit analysis.

**Prerequisites:**
- GitHub token configured
- Repository with recent commits

**Steps:**
```bash
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Test failed: UserServiceTests.GetUser\nExpected: User object\nActual: null",
    "gitHubOwner": "anishsingh0302",
    "gitHubRepo": "Semantic-Kernel",
    "commitLookbackDays": 7,
    "similarFailuresCount": 5,
    "minSimilarity": 0.7,
    "enablePatternDetection": true,
    "enableCommitAnalysis": true,
    "source": "Manual Test with GitHub"
  }'
```

**Expected Result:**
- All previous results PLUS:
- `relatedCommits` array with recent commits
- `commitCorrelation` with AI analysis
- Step 2 (Commit Analysis) successful
- Total duration < 30 seconds

**Actual Results:**
- Commits analyzed: _____
- Commit correlation present: Yes  No 
- Execution time: _____s

**Status:**  Pass  Fail

**Notes:**
```

```

---

### Test 4.3: Orchestration Status Check

**Objective:** Retrieve status of a running/completed orchestration.

**Steps:**
```bash
# 1. Start an orchestration (save the orchestrationId from response)
ORCHESTRATION_ID="<paste-from-previous-test>"

# 2. Check status
curl https://localhost:5001/api/pipeline/orchestrate/$ORCHESTRATION_ID
```

**Expected Result:**
- Returns same orchestration result
- Status shows final state (Completed/Failed)
- All data preserved

**Status:**  Pass  Fail

---

### Test 4.4: GitHub Webhook Processing

**Objective:** Verify webhook receives and processes GitHub Actions failures.

**Prerequisites:**
- Webhook secret configured (optional)
- Public URL for webhook (ngrok or deployed)

**Manual Test Steps:**
```bash
# 1. Test webhook endpoint is reachable
curl https://localhost:5001/api/webhook/test

# 2. Send test webhook payload
curl -X POST https://localhost:5001/api/webhook/github \
  -H "Content-Type: application/json" \
  -d '{
    "action": "completed",
    "workflow_run": {
      "id": 123456789,
      "name": "CI Build",
      "status": "completed",
      "conclusion": "failure",
      "html_url": "https://github.com/test/repo/actions/runs/123456789",
      "created_at": "2026-04-08T10:00:00Z",
      "updated_at": "2026-04-08T10:05:00Z",
      "head_branch": "main",
      "head_sha": "abc123"
    },
    "repository": {
      "id": 987654321,
      "name": "test-repo",
      "full_name": "owner/test-repo",
      "owner": {
        "login": "owner",
        "type": "User"
      }
    }
  }'
```

**Expected Result:**
- Returns 200 OK with "Webhook received and queued"
- Background processing starts
- Check logs for orchestration completion

**Status:**  Pass  Fail

**Notes:**
```

```

---

## Phase 5 Testing: Notifications & Integration

### Test 5.1: Microsoft Teams Notification

**Objective:** Verify Teams receives notifications for failures.

**Prerequisites:**
- Teams webhook URL configured in appsettings.json
- `NotifyOnAllFailures` or `NotifyOnNewPatterns` enabled

**Steps:**
```bash
# 1. Configure Teams webhook
# Edit appsettings.json:
{
  "Notifications": {
    "TeamsWebhookUrl": "https://your-org.webhook.office.com/...",
    "NotifyOnAllFailures": true,
    "MinConfidenceForNotification": 0.5
  }
}

# 2. Restart API

# 3. Trigger orchestration
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Critical Error: Database connection failed",
    "enablePatternDetection": true
  }'

# 4. Check Teams channel for notification
```

**Expected Result:**
- Adaptive card appears in Teams channel
- Card shows:
  - Status badge (/️/)
  - Error type and confidence
  - Root cause
  - Recommendations
  - Similar failures (if any)
  - Pattern info (if detected)
  - View Full Report button

**Actual Results:**
- Notification received: Yes  No 
- Card formatting correct: Yes  No 
- All sections present: Yes  No 
- Action button works: Yes  No 

**Status:**  Pass  Fail

**Screenshot:**
```
<Paste screenshot or description here>
```

---

### Test 5.2: Pattern Detection Notification

**Objective:** Verify Teams receives special notification for new patterns.

**Prerequisites:**
- `NotifyOnNewPatterns` enabled
- Enough failures for pattern detection

**Steps:**
```bash
# 1. Configure notifications
{
  "Notifications": {
    "TeamsWebhookUrl": "https://your-org.webhook.office.com/...",
    "NotifyOnNewPatterns": true,
    "MinConfidenceForNotification": 0.7
  }
}

# 2. Trigger orchestration that will detect/assign pattern
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Error matching existing pattern...",
    "enablePatternDetection": true
  }'

# 3. Check Teams for pattern notification (if new pattern detected)
```

**Expected Result:**
- If new pattern: Separate pattern notification card
- Pattern card shows:
  - Pattern name
  - Frequency and occurrences
  - Common root causes
  - Affected components

**Status:**  Pass  Fail  N/A (no new pattern)

---

### Test 5.3: Confidence Threshold Filtering

**Objective:** Verify notifications only sent for high-confidence analyses.

**Steps:**
```bash
# 1. Set high confidence threshold
{
  "Notifications": {
    "TeamsWebhookUrl": "https://your-org.webhook.office.com/...",
    "NotifyOnAllFailures": true,
    "MinConfidenceForNotification": 0.95
  }
}

# 2. Trigger analysis with ambiguous log (likely low confidence)
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Error occurred"
  }'

# 3. Check if notification was sent (should NOT be sent if confidence < 0.95)
```

**Expected Result:**
- Low confidence analysis completes
- NO Teams notification sent
- Log shows: "Skipping notifications due to low confidence"

**Status:**  Pass  Fail

---

### Test 5.4: GitHub Issue Creation for Patterns

**Objective:** Automatically create GitHub issues for new patterns.

**Prerequisites:**
- GitHub token with `repo` scope
- `CreateGitHubIssuesForPatterns` enabled

**Steps:**
```bash
# 1. Configure GitHub integration
{
  "Notifications": {
    "CreateGitHubIssuesForPatterns": true
  },
  "GitHub": {
    "PersonalAccessToken": "ghp_your_token_here"
  }
}

# 2. Trigger orchestration that detects a NEW pattern
# (frequency < 1.0 = new/rare pattern)

# 3. Check GitHub repository issues
```

**Expected Result:**
- New issue created in GitHub
- Issue title: "[Automated] Pattern Detected: <Pattern Name>"
- Issue labels: "automated", "failure-pattern", "<error-type>"
- Issue body contains:
  - Pattern details
  - Common root causes
  - Affected components
  - Recent example
  - Recommended actions

**Actual Results:**
- Issue created: Yes  No 
- Issue number: #_____
- Issue URL: _________________________

**Status:**  Pass  Fail

---

## Integration Testing

### Test INT.1: End-to-End Webhook Flow

**Objective:** Complete flow from GitHub webhook to Teams notification.

**Prerequisites:**
- Public webhook URL (ngrok or deployed)
- Real GitHub repository
- Teams webhook configured

**Steps:**
1. Set up GitHub webhook pointing to your API
2. Push a change that breaks tests
3. Wait for workflow to fail
4. GitHub sends webhook to your API
5. API processes webhook
6. Orchestration executes
7. Teams notification sent

**Expected Result:**
- Webhook received within seconds of failure
- Orchestration completes successfully
- Teams notification appears
- All analysis steps successful

**Actual Timeline:**
- Workflow failed at: _____
- Webhook received at: _____
- Orchestration completed at: _____
- Teams notification at: _____
- Total latency: _____seconds

**Status:**  Pass  Fail

---

### Test INT.2: Historical Learning

**Objective:** Verify system learns from past failures.

**Steps:**
1. Analyze failure A (first occurrence)
2. Note RCA and confidence
3. Analyze similar failure B
4. Check if similarity search finds failure A
5. Analyze similar failure C
6. Check if pattern is detected
7. Analyze similar failure D
8. Verify assigned to existing pattern

**Expected Result:**
- Failure B finds A as similar
- Failures A+B+C trigger pattern detection
- Failure D automatically assigned to pattern
- Pattern metadata shows frequency increase

**Actual Results:**
- Similarity detected: Yes  No 
- Pattern formed after ____ failures
- Pattern assignment successful: Yes  No 

**Status:**  Pass  Fail

---

### Test INT.3: Multi-Repository Support

**Objective:** Test orchestration with different repositories.

**Steps:**
```bash
# Repo 1
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Error in repo 1",
    "gitHubOwner": "owner",
    "gitHubRepo": "repo1",
    "enableCommitAnalysis": true
  }'

# Repo 2
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Error in repo 2",
    "gitHubOwner": "owner",
    "gitHubRepo": "repo2",
    "enableCommitAnalysis": true
  }'
```

**Expected Result:**
- Both analyses succeed
- Commits fetched from correct repositories
- Patterns and similarities tracked separately per repo

**Status:**  Pass  Fail

---

## Performance Testing

### Test PERF.1: Similarity Search Performance

**Objective:** Measure performance with large failure history.

**Prerequisites:** 100+ failures in database

**Steps:**
```bash
time curl -X POST https://localhost:5001/api/pipeline/search/similar \
  -H "Content-Type: application/json" \
  -d '{"failureId": 1, "topN": 10}'
```

**Benchmarks:**
- 100 failures: < 3 seconds
- 500 failures: < 10 seconds
- 1000 failures: < 20 seconds

**Actual Results:**
- Total failures in DB: _____
- Search time: _____seconds

**Status:**  Pass  Fail

---

### Test PERF.2: Pattern Detection Performance

**Objective:** Measure K-means clustering performance.

**Steps:**
```bash
time curl -X POST https://localhost:5001/api/pipeline/patterns/detect \
  -H "Content-Type: application/json" \
  -d '{"lookbackDays": 30}'
```

**Benchmarks:**
- 50 failures: < 5 seconds
- 100 failures: < 15 seconds
- 200 failures: < 30 seconds

**Actual Results:**
- Failures analyzed: _____
- Detection time: _____seconds

**Status:**  Pass  Fail

---

### Test PERF.3: Full Orchestration Performance

**Objective:** Measure complete pipeline execution time.

**Steps:**
```bash
time curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Complex error log...",
    "gitHubOwner": "owner",
    "gitHubRepo": "repo",
    "enableCommitAnalysis": true,
    "enablePatternDetection": true
  }'
```

**Benchmarks:**
- Without commits: < 15 seconds
- With commits: < 30 seconds

**Actual Results:**
- Total time: _____seconds
- RCA time: _____seconds
- Commit analysis: _____seconds
- Similarity search: _____seconds
- Pattern detection: _____seconds

**Status:**  Pass  Fail

---

## Error Handling Testing

### Test ERR.1: AWS Credentials Missing

**Objective:** Verify graceful handling when AWS unavailable.

**Steps:**
1. Remove/invalidate AWS credentials
2. Trigger orchestration
3. Check response and logs

**Expected Result:**
- Orchestration Status: Failed or PartialSuccess
- Error message indicates AWS credential issue
- No application crash
- Appropriate HTTP status code

**Status:**  Pass  Fail

---

### Test ERR.2: GitHub Token Invalid

**Objective:** Verify graceful handling of GitHub API errors.

**Steps:**
1. Set invalid GitHub token
2. Trigger orchestration with commit analysis
3. Check response

**Expected Result:**
- RCA still completes
- Commit analysis fails gracefully
- Orchestration Status: PartialSuccess
- Error logged in executionSteps

**Status:**  Pass  Fail

---

### Test ERR.3: Teams Webhook Unreachable

**Objective:** Verify notification failures don't break orchestration.

**Steps:**
1. Set invalid Teams webhook URL
2. Trigger orchestration
3. Check result

**Expected Result:**
- Orchestration completes successfully
- Notification failure logged
- Orchestration Status: Completed (not affected by notification)

**Status:**  Pass  Fail

---

## Test Summary

### Overall Results

| Category | Tests | Passed | Failed | Notes |
|----------|-------|--------|--------|-------|
| Phase 3 | 3 | | | |
| Phase 4 | 4 | | | |
| Phase 5 | 4 | | | |
| Integration | 3 | | | |
| Performance | 3 | | | |
| Error Handling | 3 | | | |
| **TOTAL** | **20** | | | |

### Critical Issues Found

```
1.

2.

3.
```

### Non-Critical Issues Found

```
1.

2.

3.
```

### Performance Metrics Summary

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Similarity Search (100 failures) | < 3s | ___s | |
| Pattern Detection (100 failures) | < 15s | ___s | |
| Full Orchestration (no commits) | < 15s | ___s | |
| Full Orchestration (with commits) | < 30s | ___s | |

### Sign-Off

**Tested By:** _________________________
**Date:** _________________________
**Overall Status:**  All Tests Passed  Some Failures  Major Issues

**Recommendation:**
 Ready for Production
 Requires Minor Fixes
 Requires Major Fixes
 Re-test Required

**Comments:**
```



```

---

**End of Testing Guide**
