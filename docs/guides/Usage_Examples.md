# Usage Examples - Agentic Pipeline Troubleshooter

**Date:** 2026-04-08
**Version:** Phase 2 Complete
**Status:** Production Ready

---

## Table of Contents
1. [Basic RCA Analysis](#basic-rca-analysis)
2. [Enhanced Analysis with GitHub Commits](#enhanced-analysis-with-github-commits)
3. [Retrieving Historical Analyses](#retrieving-historical-analyses)
4. [Viewing Statistics](#viewing-statistics)
5. [Common Workflows](#common-workflows)
6. [Error Handling](#error-handling)
7. [Best Practices](#best-practices)

---

## Basic RCA Analysis

### Use Case: Analyze a pipeline failure log

**Scenario:** Your CI/CD pipeline fails and you want to understand the root cause.

**Request:**
```http
POST /api/pipeline/analyze
Content-Type: application/json

{
  "logContent": "ERROR: Build failed\nNullReferenceException at PaymentService.ProcessPayment()\nStack trace: at PaymentService.cs:line 42\nObject reference not set to an instance of an object"
}
```

**Response:**
```json
{
  "rootCause": "NullReferenceException in PaymentService.ProcessPayment method",
  "detailedAnalysis": "The payment service is attempting to access a property or method on a null object. This occurs at line 42 of PaymentService.cs during the ProcessPayment operation.",
  "affectedComponents": [
    "PaymentService",
    "Payment Processing Pipeline"
  ],
  "errorType": "NullReferenceException",
  "confidence": 0.95,
  "recommendations": [
    "Add null check before accessing payment object properties",
    "Implement defensive programming with null-conditional operators",
    "Review payment object initialization in the calling code"
  ],
  "tokenUsage": {
    "inputTokens": 150,
    "outputTokens": 200
  },
  "analyzedAt": "2026-04-08T10:30:00Z"
}
```

**What Happens:**
1. Claude AI analyzes the log content
2. Identifies root cause and error type
3. Provides actionable recommendations
4. Saves result to database for historical reference
5. Returns comprehensive analysis

---

## Enhanced Analysis with GitHub Commits

### Use Case: Correlate failure with recent code changes

**Scenario:** Your pipeline started failing after recent commits. You want to identify which commit likely caused the issue.

**Request:**
```http
POST /api/pipeline/analyze-with-commits
Content-Type: application/json

{
  "logContent": "ERROR: Test failed - Expected 200, got 500\nIntegrationTests.PaymentApiTests.TestSuccessfulPayment\nSystem.Net.Http.HttpRequestException: Response status code does not indicate success: 500 (Internal Server Error)",
  "owner": "anishsingh0302",
  "repo": "Semantic-Kernel",
  "failureTimestamp": "2026-04-08T10:30:00Z",
  "lookbackHours": 24
}
```

**Response:**
```json
{
  "rootCause": "API endpoint returning 500 Internal Server Error due to unhandled exception in payment processing",
  "detailedAnalysis": "The integration test is failing because the payment API endpoint is returning a 500 error instead of the expected 200 success response. This suggests a recent code change introduced a bug in the payment processing logic.",
  "affectedComponents": [
    "Payment API",
    "PaymentService",
    "Integration Tests"
  ],
  "errorType": "HttpRequestException",
  "confidence": 0.92,
  "recommendations": [
    "Review the suspected commit that modified PaymentService.cs",
    "Check for missing null checks in the recent payment processing changes",
    "Add error logging to payment endpoint to capture exception details"
  ],
  "suspectedCommits": [
    {
      "sha": "abc123def456...",
      "shortSha": "abc123d",
      "message": "Refactor payment processing logic",
      "author": "John Doe",
      "commitDate": "2026-04-08T08:15:00Z",
      "confidence": 0.88,
      "reason": "Modified PaymentService.cs which is directly related to the failing test",
      "suspectedFiles": [
        "src/PaymentService.cs",
        "src/PaymentController.cs"
      ],
      "evidence": "The test failure coincides with changes to payment processing. The commit modified the exact class mentioned in the error trace."
    },
    {
      "sha": "def789ghi012...",
      "shortSha": "def789g",
      "message": "Update payment validation",
      "author": "Jane Smith",
      "commitDate": "2026-04-08T06:30:00Z",
      "confidence": 0.75,
      "reason": "Added validation logic that might reject valid payment requests",
      "suspectedFiles": [
        "src/PaymentValidator.cs"
      ],
      "evidence": "Changes to validation logic could cause legitimate payment requests to fail"
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
    "outputTokens": 380
  },
  "analyzedAt": "2026-04-08T10:30:00Z"
}
```

**What Happens:**
1. Fetches commits from GitHub for the past 24 hours
2. AI analyzes which commits likely caused the failure
3. Provides confidence scores for each suspected commit
4. Enhanced RCA includes commit context
5. Saves failure and related commits to database

---

## Retrieving Historical Analyses

### Use Case 1: View Recent Failures

**Request:**
```http
GET /api/pipeline/history?limit=10&offset=0
```

**Response:**
```json
[
  {
    "id": 15,
    "logContent": "ERROR: ...",
    "rootCause": "NullReferenceException in PaymentService",
    "errorType": "NullReferenceException",
    "confidenceScore": 0.95,
    "analyzedAt": "2026-04-08T10:30:00Z",
    "commits": [...],
    ...
  },
  {
    "id": 14,
    "errorType": "TimeoutException",
    "analyzedAt": "2026-04-08T09:15:00Z",
    ...
  }
]
```

### Use Case 2: Filter by Error Type

**Request:**
```http
GET /api/pipeline/history?errorType=NullReferenceException&limit=20
```

**Response:**
```json
[
  {
    "id": 15,
    "errorType": "NullReferenceException",
    ...
  },
  {
    "id": 12,
    "errorType": "NullReferenceException",
    ...
  }
]
```

### Use Case 3: Get Specific Analysis

**Request:**
```http
GET /api/pipeline/history/15?includeCommits=true
```

**Response:**
```json
{
  "id": 15,
  "logContent": "ERROR: ...",
  "rootCause": "NullReferenceException in PaymentService",
  "detailedAnalysis": "...",
  "errorType": "NullReferenceException",
  "confidenceScore": 0.95,
  "analyzedAt": "2026-04-08T10:30:00Z",
  "commits": [
    {
      "id": 1,
      "sha": "abc123...",
      "author": "John Doe",
      "message": "Refactor payment processing",
      "confidenceScore": 0.88,
      ...
    }
  ]
}
```

---

## Viewing Statistics

### Use Case: Dashboard Metrics

**Request:**
```http
GET /api/pipeline/stats
```

**Response:**
```json
{
  "totalFailures": 150,
  "averageConfidence": 0.87,
  "totalInputTokens": 45000,
  "totalOutputTokens": 32000,
  "failuresByErrorType": {
    "NullReferenceException": 65,
    "TimeoutException": 40,
    "HttpRequestException": 25,
    "ArgumentException": 15,
    "IOException": 5
  },
  "mostRecentAnalysis": "2026-04-08T10:30:00Z",
  "oldestAnalysis": "2026-03-15T08:00:00Z"
}
```

**Use Cases:**
- Monitor failure trends over time
- Track token usage for cost management
- Identify most common error types
- Plan infrastructure improvements

---

## Common Workflows

### Workflow 1: Daily Failure Review

```bash
# 1. Get today's failures
GET /api/pipeline/history?limit=50

# 2. Filter by specific error type if pattern emerges
GET /api/pipeline/history?errorType=NullReferenceException

# 3. Review specific failure details
GET /api/pipeline/history/{id}

# 4. Check overall statistics
GET /api/pipeline/stats
```

### Workflow 2: Incident Investigation

```bash
# 1. Analyze the failure with commit context
POST /api/pipeline/analyze-with-commits
{
  "logFilePath": "/path/to/failure.log",
  "owner": "your-org",
  "repo": "your-repo",
  "lookbackHours": 48
}

# 2. Review suspected commits
# Response includes commits with confidence scores

# 3. Investigate highest confidence commit
# Review the commit on GitHub

# 4. Save analysis for future reference
# Already saved automatically to database
```

### Workflow 3: Pattern Analysis

```bash
# 1. Get all NullReferenceException failures
GET /api/pipeline/history?errorType=NullReferenceException&limit=100

# 2. Analyze patterns in affected components
# Review responses to identify common components

# 3. Check statistics for error distribution
GET /api/pipeline/stats

# 4. Prioritize fixes based on frequency
# Use failuresByErrorType data
```

---

## Error Handling

### Invalid Request - Missing Log Content

**Request:**
```http
POST /api/pipeline/analyze
{
  "logContent": ""
}
```

**Response (400 Bad Request):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid Request",
  "status": 400,
  "detail": "Either 'logContent' or 'logFilePath' must be provided"
}
```

### File Not Found

**Request:**
```http
POST /api/pipeline/analyze
{
  "logFilePath": "/nonexistent/file.log"
}
```

**Response (400 Bad Request):**
```json
{
  "title": "File Not Found",
  "status": 400,
  "detail": "The log file '/nonexistent/file.log' does not exist or cannot be accessed"
}
```

### Record Not Found

**Request:**
```http
GET /api/pipeline/history/99999
```

**Response (404 Not Found):**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Failure analysis with ID 99999 was not found"
}
```

### Invalid Pagination Parameters

**Request:**
```http
GET /api/pipeline/history?limit=200
```

**Response (400 Bad Request):**
```json
{
  "title": "Invalid Request",
  "status": 400,
  "detail": "Limit must be between 1 and 100"
}
```

---

## Best Practices

### 1. Log Content Quality

**Do:**
```
✅ Include full error messages
✅ Include stack traces
✅ Include relevant context (file paths, line numbers)
✅ Include timestamps
✅ Keep logs under 50KB for optimal performance
```

**Don't:**
```
❌ Submit only partial error messages
❌ Remove stack traces
❌ Submit logs without error information
❌ Submit logs larger than 100KB
```

### 2. GitHub Integration

**Do:**
```
✅ Set GitHub Personal Access Token for higher rate limits
✅ Use appropriate lookback hours (24-72 hours typical)
✅ Provide accurate failure timestamp
✅ Use specific repo owner and name
```

**Don't:**
```
❌ Use very long lookback periods (>168 hours) unless necessary
❌ Analyze without authentication (hits rate limits quickly)
❌ Ignore commit correlation data
```

### 3. Query Optimization

**Do:**
```
✅ Use pagination (limit + offset) for large result sets
✅ Filter by error type when investigating patterns
✅ Use includeCommits=false when commits aren't needed
✅ Cache statistics for dashboard displays
```

**Don't:**
```
❌ Request all records without limit
❌ Query without filters when you know the error type
❌ Always include commits if not needed
❌ Poll statistics endpoint too frequently
```

### 4. Cost Management

**Monitor Token Usage:**
```bash
# Check total token consumption
GET /api/pipeline/stats

# Calculate cost (example with Claude pricing)
# Input tokens: $0.003 per 1K
# Output tokens: $0.015 per 1K

Total Cost = (InputTokens / 1000 * $0.003) + (OutputTokens / 1000 * $0.015)
```

**Optimization Tips:**
- Use basic analysis when commits aren't needed
- Implement log size limits (< 10KB optimal)
- Archive old analyses after 6-12 months
- Set up alerts for unusual token consumption

### 5. Error Type Categorization

**Common Error Types:**
```
NullReferenceException    → Missing null checks
TimeoutException          → Performance/network issues
HttpRequestException      → API/service failures
ArgumentException         → Invalid input validation
IOException               → File system issues
```

**Use Filtering:**
```bash
# Focus on most critical errors first
GET /api/pipeline/history?errorType=NullReferenceException

# Track timeout issues separately
GET /api/pipeline/history?errorType=TimeoutException
```

### 6. Integration with CI/CD

**Example: GitHub Actions Integration**
```yaml
- name: Analyze Failure
  if: failure()
  run: |
    curl -X POST https://your-api/api/pipeline/analyze-with-commits \
      -H "Content-Type: application/json" \
      -d '{
        "logFilePath": "${{ github.workspace }}/build.log",
        "owner": "${{ github.repository_owner }}",
        "repo": "${{ github.event.repository.name }}",
        "lookbackHours": 24
      }'
```

### 7. Database Maintenance

**Regular Maintenance:**
```bash
# Archive old records (>6 months)
# Vacuum database periodically (SQLite)
# Monitor database size growth
# Set up automated backups
```

**Performance Thresholds:**
```
< 1,000 records   → Excellent performance
1,000 - 10,000    → Good performance, pagination recommended
> 10,000 records  → Consider archival strategy
```

---

## Production Checklist

### Before Deployment

- [ ] Configure production database (PostgreSQL/SQL Server recommended)
- [ ] Set up AWS credentials (access key, secret key, region)
- [ ] Configure GitHub Personal Access Token
- [ ] Set up HTTPS certificates
- [ ] Configure CORS for production domains
- [ ] Set up logging (application insights, ELK, etc.)
- [ ] Configure connection pooling
- [ ] Set up monitoring and alerting
- [ ] Implement rate limiting on API endpoints
- [ ] Configure automated database backups

### After Deployment

- [ ] Test all endpoints in production
- [ ] Monitor token usage and costs
- [ ] Set up alerts for high error rates
- [ ] Configure log retention policies
- [ ] Document internal usage guidelines
- [ ] Train team on API usage
- [ ] Set up dashboard for statistics
- [ ] Implement archival strategy

---

## Troubleshooting

### Issue: Slow Response Times

**Symptoms:**
- Analysis takes > 30 seconds
- API times out

**Solutions:**
1. Check Claude API latency (normal: 5-15s)
2. Reduce log size (< 10KB optimal)
3. Use basic analysis when commits not needed
4. Check network connectivity to AWS

### Issue: High Token Usage

**Symptoms:**
- Unexpected AWS costs
- Token usage higher than expected

**Solutions:**
1. Review log sizes being submitted
2. Implement log preprocessing to remove noise
3. Set maximum log size limits
4. Monitor via statistics endpoint

### Issue: Missing Commit Data

**Symptoms:**
- Enhanced analysis returns no commits
- Empty suspectedCommits array

**Solutions:**
1. Verify GitHub token is set
2. Check lookback hours isn't too restrictive
3. Verify failure timestamp is correct
4. Ensure repo has commits in time range

### Issue: Database Performance

**Symptoms:**
- Slow query responses
- Pagination delays

**Solutions:**
1. Check database size (> 10K records?)
2. Implement archival for old records
3. Verify indexes are in place
4. Consider migration to PostgreSQL

---

## Support

For issues, questions, or feature requests:
- **GitHub Issues:** [Create an issue](https://github.com/anishsingh0302/Agentic-Pipeline/issues)
- **Documentation:** See `docs/` folder
- **API Reference:** Access Swagger UI at `/` when running

---

**Version:** Phase 2 Complete
**Last Updated:** 2026-04-08
**Status:** Production Ready ✅
