# API Testing Guide - Swagger UI

**Date:** 2026-04-08
**Phase:** 2 Day 12
**Status:** Ready for End-to-End Testing

---

## Starting the API

```bash
# Navigate to API project
cd C:\Anish\Source\Agentic_Pipeline\src\AgenticPipeline.Api

# Run the API (opens Swagger UI automatically)
dotnet run
```

The API will start and Swagger UI will be available at: `https://localhost:7001` (or the port shown in console)

---

## Available Endpoints

### 1. POST /api/pipeline/analyze
**Description:** Analyze a pipeline failure log and save to database

**Request Body:**
```json
{
  "logContent": "ERROR: NullReferenceException at PaymentService.ProcessPayment()\nObject reference not set to an instance of an object\nStack trace: at PaymentService.cs:line 42"
}
```

**Expected Response:**
```json
{
  "rootCause": "NullReferenceException in PaymentService",
  "detailedAnalysis": "...",
  "affectedComponents": ["PaymentService"],
  "errorType": "NullReferenceException",
  "confidence": 0.95,
  "recommendations": ["Add null check before processing payment"],
  "tokenUsage": {
    "inputTokens": 150,
    "outputTokens": 200
  },
  "analyzedAt": "2026-04-08T10:30:00Z"
}
```

**What happens:**
- Claude AI analyzes the log
- Result is saved to database
- Returns RcaResult with analysis

---

### 2. POST /api/pipeline/analyze-with-commits
**Description:** Analyze failure with GitHub commit correlation

**Request Body:**
```json
{
  "logContent": "ERROR: NullReferenceException at PaymentService.ProcessPayment()",
  "owner": "anishsingh0302",
  "repo": "Semantic-Kernel",
  "failureTimestamp": "2026-04-08T10:30:00Z",
  "lookbackHours": 24
}
```

**Expected Response:**
```json
{
  "rootCause": "NullReferenceException in PaymentService",
  "detailedAnalysis": "...",
  "affectedComponents": ["PaymentService"],
  "errorType": "NullReferenceException",
  "confidence": 0.95,
  "recommendations": ["Add null check"],
  "suspectedCommits": [
    {
      "sha": "abc123...",
      "shortSha": "abc123",
      "message": "Update payment processing",
      "author": "John Doe",
      "commitDate": "2026-04-08T05:00:00Z",
      "confidence": 0.88,
      "reason": "Modified PaymentService.cs",
      "suspectedFiles": ["PaymentService.cs"],
      "evidence": "Added new payment method without null check"
    }
  ],
  "commitCorrelation": {
    "repository": "anishsingh0302/Semantic-Kernel",
    "totalCommitsAnalyzed": 15,
    "timeRangeStart": "2026-04-07T10:30:00Z",
    "timeRangeEnd": "2026-04-08T10:30:00Z",
    "lookbackHours": 24
  },
  "tokenUsage": {
    "inputTokens": 450,
    "outputTokens": 350
  },
  "analyzedAt": "2026-04-08T10:30:00Z"
}
```

**What happens:**
- Fetches commits from GitHub
- AI correlates commits with failure
- Performs enhanced RCA with commit context
- Saves result and suspected commits to database
- Returns comprehensive analysis

---

### 3. GET /api/pipeline/history
**Description:** Retrieve past failure analyses with pagination

**Query Parameters:**
- `limit` (optional, default: 50, max: 100) - Number of records to return
- `offset` (optional, default: 0) - Number of records to skip
- `errorType` (optional) - Filter by error type

**Example URLs:**
```
GET /api/pipeline/history
GET /api/pipeline/history?limit=10&offset=0
GET /api/pipeline/history?errorType=NullReferenceException
GET /api/pipeline/history?limit=5&offset=5&errorType=TimeoutException
```

**Expected Response:**
```json
[
  {
    "id": 3,
    "logContent": "ERROR: ...",
    "rootCause": "...",
    "detailedAnalysis": "...",
    "errorType": "NullReferenceException",
    "confidenceScore": 0.95,
    "analyzedAt": "2026-04-08T10:30:00Z",
    "affectedComponentsJson": "[\"PaymentService\"]",
    "recommendationsJson": "[\"Add null check\"]",
    "inputTokens": 150,
    "outputTokens": 200,
    "commits": [...]
  },
  {
    "id": 2,
    "logContent": "...",
    ...
  }
]
```

**Notes:**
- Results ordered by most recent first
- Each failure includes related commits if they exist

---

### 4. GET /api/pipeline/history/{id}
**Description:** Retrieve a specific failure analysis by ID

**Query Parameters:**
- `includeCommits` (optional, default: true) - Include related commits

**Example URLs:**
```
GET /api/pipeline/history/1
GET /api/pipeline/history/5?includeCommits=true
GET /api/pipeline/history/5?includeCommits=false
```

**Expected Response:**
```json
{
  "id": 1,
  "logContent": "ERROR: NullReferenceException...",
  "rootCause": "Null reference in payment processing",
  "detailedAnalysis": "...",
  "errorType": "NullReferenceException",
  "confidenceScore": 0.95,
  "analyzedAt": "2026-04-08T10:30:00Z",
  "affectedComponentsJson": "[\"PaymentService\"]",
  "recommendationsJson": "[\"Add null check\"]",
  "inputTokens": 150,
  "outputTokens": 200,
  "commits": [
    {
      "id": 1,
      "sha": "abc123...",
      "author": "John Doe",
      "commitDate": "2026-04-08T05:00:00Z",
      "message": "Update payment processing",
      "filesChangedJson": "[\"PaymentService.cs\"]",
      "confidenceScore": 0.88,
      "suspectedReason": "Modified PaymentService",
      "evidence": "Added new method without null check",
      "failureEntityId": 1
    }
  ]
}
```

**Error Response (404):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Failure analysis with ID 999 was not found"
}
```

---

### 5. GET /api/pipeline/stats
**Description:** Get aggregate statistics about all failure analyses

**Example URL:**
```
GET /api/pipeline/stats
```

**Expected Response:**
```json
{
  "totalFailures": 15,
  "averageConfidence": 0.87,
  "totalInputTokens": 4500,
  "totalOutputTokens": 3200,
  "failuresByErrorType": {
    "NullReferenceException": 8,
    "TimeoutException": 4,
    "ArgumentException": 2,
    "IOException": 1
  },
  "mostRecentAnalysis": "2026-04-08T10:30:00Z",
  "oldestAnalysis": "2026-04-01T08:15:00Z"
}
```

**Empty Database Response:**
```json
{
  "totalFailures": 0,
  "averageConfidence": 0,
  "totalInputTokens": 0,
  "totalOutputTokens": 0,
  "failuresByErrorType": {},
  "mostRecentAnalysis": null,
  "oldestAnalysis": null
}
```

---

### 6. GET /api/pipeline/health
**Description:** Health check endpoint

**Expected Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-04-08T10:30:00Z",
  "service": "Agentic Pipeline Troubleshooter"
}
```

---

## End-to-End Testing Workflow

### Test Scenario 1: Basic Analysis Flow
```
1. POST /api/pipeline/analyze
   - Submit a failure log
   - Note the ID from response (check logs)

2. GET /api/pipeline/history
   - Verify your analysis appears in history

3. GET /api/pipeline/history/{id}
   - Retrieve the specific analysis by ID
   - Verify all data persisted correctly

4. GET /api/pipeline/stats
   - Verify statistics show 1 failure
```

### Test Scenario 2: Enhanced Analysis with Commits
```
1. POST /api/pipeline/analyze-with-commits
   - Submit failure log with GitHub repo info
   - Note suspected commits in response

2. GET /api/pipeline/history/{id}?includeCommits=true
   - Verify commits were saved to database
   - Check commit details match response

3. GET /api/pipeline/stats
   - Verify token usage increased
```

### Test Scenario 3: Pagination & Filtering
```
1. Create 10 failures with different error types
   - 5x NullReferenceException
   - 3x TimeoutException
   - 2x ArgumentException

2. GET /api/pipeline/history?limit=5&offset=0
   - Verify first 5 results (most recent)

3. GET /api/pipeline/history?limit=5&offset=5
   - Verify next 5 results

4. GET /api/pipeline/history?errorType=NullReferenceException
   - Verify only NullReferenceException failures returned (5 total)

5. GET /api/pipeline/stats
   - Verify failuresByErrorType breakdown matches
```

---

## Viewing Database Contents

### Using DB Browser for SQLite

1. **Download DB Browser:** https://sqlitebrowser.org/dl/

2. **Open Database:**
   ```
   File → Open Database
   Navigate to: C:\Anish\Source\Agentic_Pipeline\src\AgenticPipeline.Api\pipeline.db
   ```

3. **View Tables:**
   - `Failures` - All failure analyses
   - `Commits` - Related commits

4. **Sample Queries:**
   ```sql
   -- View all failures
   SELECT * FROM Failures ORDER BY AnalyzedAt DESC;

   -- View failures with commits
   SELECT f.Id, f.ErrorType, f.ConfidenceScore, COUNT(c.Id) as CommitCount
   FROM Failures f
   LEFT JOIN Commits c ON f.Id = c.FailureEntityId
   GROUP BY f.Id;

   -- View token usage
   SELECT SUM(InputTokens) as TotalInput,
          SUM(OutputTokens) as TotalOutput
   FROM Failures;
   ```

---

## Troubleshooting

### Issue: API won't start
```bash
# Check if port is in use
netstat -ano | findstr :7001

# Kill process if needed
taskkill /PID <process_id> /F

# Try different port
dotnet run --urls "https://localhost:7002"
```

### Issue: AWS credentials not found
```bash
# Set environment variables
set AWS_ACCESS_KEY_ID=your_access_key
set AWS_SECRET_ACCESS_KEY=your_secret_key
set AWS_REGION=us-east-1

# Or configure AWS CLI
aws configure
```

### Issue: GitHub rate limit
```bash
# Set GitHub token
set GitHub__PersonalAccessToken=your_github_token

# Restart API
```

### Issue: Database locked
```bash
# Stop all running instances
dotnet build --no-incremental

# Delete lock files
del src\AgenticPipeline.Api\*.db-shm
del src\AgenticPipeline.Api\*.db-wal

# Restart
dotnet run --project src\AgenticPipeline.Api
```

---

## Expected Test Results

### All Endpoints Should:
 Return 200 OK for valid requests
 Return proper error codes (400, 404, 500) for invalid requests
 Save data to database (verify via GET endpoints)
 Include comprehensive logging in console
 Return consistent JSON format

### Data Persistence:
 Restart API and verify history still exists
 Check database file exists in API folder
 Verify relationships (failures have commits)

### Performance:
 Analysis endpoints respond within 5-15 seconds (depends on Claude API)
 History endpoints respond within 100ms
 Stats endpoint responds within 200ms

---

## Next Steps

After successful Swagger testing:
1. Document any issues found
2. Test edge cases (large logs, many commits, etc.)
3. Verify error handling works correctly
4. Test concurrent requests
5. Move to Day 13-14: Comprehensive testing & documentation
