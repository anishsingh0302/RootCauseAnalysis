# Phase 4: Orchestrator & Webhook API Examples

Complete examples for using the Phase 4 Pipeline Orchestration and Webhook APIs.

---

## Table of Contents

1. [Full Pipeline Orchestration](#full-pipeline-orchestration)
2. [Orchestration Status Check](#orchestration-status-check)
3. [GitHub Webhook Setup](#github-webhook-setup)
4. [Webhook Testing](#webhook-testing)

---

## Full Pipeline Orchestration

Execute a complete analysis pipeline including RCA, commit analysis, similarity search, and pattern detection.

### Endpoint
```
POST /api/pipeline/orchestrate
```

### Example 1: Basic Orchestration (No GitHub Context)

**Request:**
```bash
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Error: NullReferenceException at DatabaseService.cs:line 42\nStack trace:\n  at DatabaseService.GetConnection()\n  at UserRepository.FindById(Int32 id)\n  at UserController.GetUser(Int32 id)",
    "similarFailuresCount": 5,
    "minSimilarity": 0.7,
    "enablePatternDetection": true,
    "enableCommitAnalysis": false,
    "source": "Manual API Test"
  }'
```

**Response (200 OK):**
```json
{
  "orchestrationId": "a1b2c3d4-5678-90ab-cdef-1234567890ab",
  "startedAt": "2026-04-08T10:30:00.000Z",
  "completedAt": "2026-04-08T10:30:12.450Z",
  "duration": "00:00:12.450",
  "status": "Completed",
  "failure": {
    "id": 42,
    "logContent": "Error: NullReferenceException at...",
    "rootCause": "Null reference when accessing database connection pool",
    "detailedAnalysis": "The error occurs because the connection pool is not initialized before first use...",
    "errorType": "NullReferenceException",
    "confidenceScore": 0.92,
    "analyzedAt": "2026-04-08T10:30:05.123Z",
    "affectedComponentsJson": "[\"DatabaseService\",\"UserRepository\"]",
    "recommendationsJson": "[\"Initialize connection pool in constructor\",\"Add null checks before use\"]",
    "inputTokens": 1245,
    "outputTokens": 687
  },
  "similarFailures": [
    {
      "failureId": 38,
      "errorType": "NullReferenceException",
      "rootCause": "Database connection null in UserRepository",
      "similarity": 0.89,
      "analyzedAt": "2026-04-05T14:22:00Z",
      "recommendations": [
        "Add null checks",
        "Initialize connection in constructor"
      ]
    },
    {
      "failureId": 23,
      "errorType": "NullReferenceException",
      "rootCause": "Connection pool not initialized",
      "similarity": 0.85,
      "analyzedAt": "2026-03-28T09:15:00Z",
      "recommendations": [
        "Check connection pool initialization",
        "Add defensive null checks"
      ]
    }
  ],
  "assignedPattern": {
    "id": 3,
    "name": "Database Connection Issues",
    "primaryErrorType": "NullReferenceException",
    "failureCount": 15,
    "averageSimilarity": 0.87,
    "averageConfidence": 0.89,
    "frequency": 2.5,
    "firstOccurrence": "2026-03-15T08:00:00Z",
    "lastOccurrence": "2026-04-08T10:30:00Z",
    "commonRootCauses": [
      "Null database connection",
      "Connection pool not initialized",
      "Connection timeout"
    ]
  },
  "patternSimilarity": 0.92,
  "relatedCommits": null,
  "commitCorrelation": null,
  "executionSteps": [
    {
      "stepName": "Root Cause Analysis",
      "startedAt": "2026-04-08T10:30:00.100Z",
      "completedAt": "2026-04-08T10:30:05.200Z",
      "duration": "00:00:05.100",
      "success": true,
      "error": null,
      "metadata": {
        "FailureId": 42,
        "ErrorType": "NullReferenceException",
        "Confidence": 0.92,
        "InputTokens": 1245,
        "OutputTokens": 687
      }
    },
    {
      "stepName": "Similarity Search",
      "startedAt": "2026-04-08T10:30:05.250Z",
      "completedAt": "2026-04-08T10:30:08.100Z",
      "duration": "00:00:02.850",
      "success": true,
      "error": null,
      "metadata": {
        "SimilarFailuresFound": 2,
        "CandidatesSearched": 41
      }
    },
    {
      "stepName": "Pattern Detection",
      "startedAt": "2026-04-08T10:30:08.150Z",
      "completedAt": "2026-04-08T10:30:12.400Z",
      "duration": "00:00:04.250",
      "success": true,
      "error": null,
      "metadata": {
        "PatternsDetected": 5,
        "OptimalK": 5,
        "FailuresAnalyzed": 42,
        "AssignedToPattern": true
      }
    }
  ],
  "errors": [],
  "totalInputTokens": 1245,
  "totalOutputTokens": 687,
  "report": "#  Comprehensive Failure Analysis Report\n\n**Orchestration ID:** `a1b2c3d4-5678-90ab-cdef-1234567890ab`\n**Analyzed At:** 2026-04-08 10:30:00 UTC\n**Duration:** 12.45s\n**Status:** Completed\n**Source:** Manual API Test\n\n##  Root Cause Analysis\n\n**Error Type:** NullReferenceException\n**Confidence:** 92%\n\n### Root Cause\nNull reference when accessing database connection pool\n\n### Detailed Analysis\nThe error occurs because the connection pool is not initialized before first use...\n\n### Affected Components\n- DatabaseService\n- UserRepository\n\n###  Recommendations\n1. Initialize connection pool in constructor\n2. Add null checks before use\n\n##  Similar Historical Failures\n\nThis failure is similar to the following past failures:\n\n### Failure #38 (89% similar)\n**Error Type:** NullReferenceException\n**Occurred:** 2026-04-05 14:22\n**Root Cause:** Database connection null in UserRepository\n**What worked:**\n- Add null checks\n- Initialize connection in constructor\n\n### Failure #23 (85% similar)\n**Error Type:** NullReferenceException\n**Occurred:** 2026-03-28 09:15\n**Root Cause:** Connection pool not initialized\n**What worked:**\n- Check connection pool initialization\n- Add defensive null checks\n\n##  Pattern Recognition\n\nThis failure belongs to pattern: **Database Connection Issues**\n**Pattern Similarity:** 92%\n**Pattern Type:** NullReferenceException\n**Frequency:** 2.50 failures/day\n**Total Occurrences:** 15\n\n**Common Root Causes in this Pattern:**\n- Null database connection\n- Connection pool not initialized\n- Connection timeout\n\n## ️ Execution Summary\n\n| Step | Status | Duration |\n|------|--------|----------|\n| Root Cause Analysis |  Success | 5.10s |\n| Similarity Search |  Success | 2.85s |\n| Pattern Detection |  Success | 4.25s |\n\n**Total Token Usage:** 1,932 (1,245 input + 687 output)\n\n---\n*Generated by Agentic Pipeline v4.0*"
}
```

### Example 2: Orchestration with GitHub Context

**Request:**
```bash
curl -X POST https://localhost:5001/api/pipeline/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "Test failed: UserServiceTests.GetUser_WithValidId_ReturnsUser\nExpected: User object\nActual: null",
    "gitHubOwner": "anishsingh0302",
    "gitHubRepo": "Semantic-Kernel",
    "failureTimestamp": "2026-04-08T10:25:00Z",
    "commitLookbackDays": 3,
    "similarFailuresCount": 5,
    "minSimilarity": 0.7,
    "enablePatternDetection": true,
    "enableCommitAnalysis": true,
    "source": "Manual with GitHub Context",
    "metadata": {
      "branch": "feature/user-api",
      "buildNumber": "123"
    }
  }'
```

**Response:** Includes additional `relatedCommits` and `commitCorrelation` fields:

```json
{
  "...": "...(similar to example 1)",
  "relatedCommits": [
    {
      "sha": "abc123def456...",
      "message": "Refactor user service to use async/await",
      "author": "John Doe",
      "authorEmail": "john@example.com",
      "commitDate": "2026-04-08T09:45:00Z",
      "filesChanged": [
        {
          "fileName": "Services/UserService.cs",
          "status": "modified",
          "additions": 25,
          "deletions": 10,
          "patch": null
        }
      ],
      "additions": 25,
      "deletions": 10,
      "url": "https://github.com/anishsingh0302/Semantic-Kernel/commit/abc123"
    }
  ],
  "commitCorrelation": "{\"success\":true,\"repository\":\"anishsingh0302/Semantic-Kernel\",\"commitsAnalyzed\":3,\"aiAnalysis\":\"...\"}"
}
```

---

## Orchestration Status Check

Check the status of a running or completed orchestration.

### Endpoint
```
GET /api/pipeline/orchestrate/{orchestrationId}
```

### Example

**Request:**
```bash
curl -X GET https://localhost:5001/api/pipeline/orchestrate/a1b2c3d4-5678-90ab-cdef-1234567890ab
```

**Response (200 OK):**
Same structure as POST response above.

**Response (404 Not Found):**
```json
{
  "title": "Not Found",
  "detail": "Orchestration with ID a1b2c3d4-5678-90ab-cdef-1234567890ab was not found",
  "status": 404
}
```

---

## GitHub Webhook Setup

Configure GitHub to automatically trigger analysis when workflows fail.

### Step 1: Configure Webhook Secret

Add to `appsettings.json`:
```json
{
  "GitHub": {
    "PersonalAccessToken": "ghp_your_token_here",
    "WebhookSecret": "your-secure-random-string-here"
  }
}
```

Generate a secure secret:
```bash
# PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))

# Bash
openssl rand -base64 32
```

### Step 2: Add Webhook in GitHub

1. Go to your repository settings
2. Navigate to **Webhooks** → **Add webhook**
3. Configure:
   - **Payload URL:** `https://your-domain.com/api/webhook/github`
   - **Content type:** `application/json`
   - **Secret:** (paste your webhook secret)
   - **SSL verification:** Enable (recommended)
   - **Which events:** Let me select individual events
     - ️ Workflow runs
   - **Active:** ️ Checked

4. Click **Add webhook**

### Step 3: Test Webhook

Test the webhook endpoint:

```bash
curl https://your-domain.com/api/webhook/test
```

**Response:**
```json
{
  "status": "Webhook endpoint is reachable",
  "timestamp": "2026-04-08T10:30:00Z",
  "signatureVerificationEnabled": true,
  "orchestratorConfigured": true,
  "logServiceConfigured": true
}
```

---

## Webhook Testing

### Manual Webhook Simulation

Simulate a GitHub webhook locally (for testing without GitHub):

```bash
# Generate test payload
cat > webhook_payload.json << 'EOF'
{
  "action": "completed",
  "workflow_run": {
    "id": 123456789,
    "name": "CI Build",
    "status": "completed",
    "conclusion": "failure",
    "html_url": "https://github.com/owner/repo/actions/runs/123456789",
    "created_at": "2026-04-08T10:00:00Z",
    "updated_at": "2026-04-08T10:05:00Z",
    "head_branch": "main",
    "head_sha": "abc123def456"
  },
  "repository": {
    "id": 987654321,
    "name": "my-repo",
    "full_name": "owner/my-repo",
    "owner": {
      "login": "owner",
      "type": "User"
    }
  }
}
EOF

# Send webhook (without signature - only works if webhook secret is not configured)
curl -X POST https://localhost:5001/api/webhook/github \
  -H "Content-Type: application/json" \
  -d @webhook_payload.json
```

**Response (200 OK):**
```json
{
  "message": "Webhook received and queued for processing",
  "workflowRunId": 123456789,
  "repository": "owner/my-repo"
}
```

### With Signature Verification

Generate HMAC-SHA256 signature:

```python
import hmac
import hashlib
import json

# Your webhook secret
secret = "your-webhook-secret"

# Payload
payload = json.dumps({
    "action": "completed",
    "workflow_run": { ... },
    "repository": { ... }
})

# Generate signature
signature = hmac.new(
    secret.encode('utf-8'),
    payload.encode('utf-8'),
    hashlib.sha256
).hexdigest()

print(f"X-Hub-Signature-256: sha256={signature}")
```

Then send with signature header:

```bash
curl -X POST https://localhost:5001/api/webhook/github \
  -H "Content-Type: application/json" \
  -H "X-Hub-Signature-256: sha256=<computed-signature>" \
  -d @webhook_payload.json
```

---

## Integration Examples

### Python Client

```python
import requests
import json

def trigger_orchestration(log_content, github_owner=None, github_repo=None):
    """Trigger full pipeline orchestration"""

    url = "https://localhost:5001/api/pipeline/orchestrate"

    payload = {
        "logContent": log_content,
        "similarFailuresCount": 5,
        "minSimilarity": 0.7,
        "enablePatternDetection": True,
        "enableCommitAnalysis": False,
        "source": "Python Client"
    }

    if github_owner and github_repo:
        payload["gitHubOwner"] = github_owner
        payload["gitHubRepo"] = github_repo
        payload["enableCommitAnalysis"] = True

    response = requests.post(url, json=payload, verify=False)
    response.raise_for_status()

    return response.json()

def get_orchestration_status(orchestration_id):
    """Check orchestration status"""

    url = f"https://localhost:5001/api/pipeline/orchestrate/{orchestration_id}"

    response = requests.get(url, verify=False)
    response.raise_for_status()

    return response.json()

# Example usage
if __name__ == "__main__":
    log = """
    Error: NullReferenceException at line 42
    Stack trace: ...
    """

    result = trigger_orchestration(log)

    print(f"Orchestration ID: {result['orchestrationId']}")
    print(f"Status: {result['status']}")
    print(f"Error Type: {result['failure']['errorType']}")
    print(f"Confidence: {result['failure']['confidenceScore']:.0%}")

    # Print report
    print("\n" + "="*60)
    print(result['report'])
```

### C# Client

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

public class AgenticPipelineClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public AgenticPipelineClient(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public async Task<OrchestrationResult> TriggerOrchestrationAsync(
        string logContent,
        string? gitHubOwner = null,
        string? gitHubRepo = null)
    {
        var request = new OrchestrationRequest
        {
            LogContent = logContent,
            GitHubOwner = gitHubOwner,
            GitHubRepo = gitHubRepo,
            SimilarFailuresCount = 5,
            MinSimilarity = 0.7f,
            EnablePatternDetection = true,
            EnableCommitAnalysis = gitHubOwner != null && gitHubRepo != null,
            Source = "C# Client"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/api/pipeline/orchestrate",
            content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OrchestrationResult>(responseJson)!;
    }

    public async Task<OrchestrationResult?> GetStatusAsync(Guid orchestrationId)
    {
        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/api/pipeline/orchestrate/{orchestrationId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OrchestrationResult>(json);
    }
}

// Usage
var client = new AgenticPipelineClient("https://localhost:5001");

var result = await client.TriggerOrchestrationAsync(
    logContent: "Error: NullReferenceException...",
    gitHubOwner: "anishsingh0302",
    gitHubRepo: "Semantic-Kernel");

Console.WriteLine($"Orchestration ID: {result.OrchestrationId}");
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Error Type: {result.Failure.ErrorType}");
Console.WriteLine($"Confidence: {result.Failure.ConfidenceScore:P0}");
Console.WriteLine();
Console.WriteLine(result.Report);
```

---

## Troubleshooting

### Orchestration Fails Immediately

**Problem:** Status is "Failed" with errors

**Check:**
1. AWS credentials configured correctly
2. Claude API access enabled
3. Database connection working
4. Logs for specific error messages

### Webhook Not Triggering

**Problem:** GitHub webhook shows error or no response

**Check:**
1. Webhook URL is publicly accessible (not localhost)
2. HTTPS is properly configured
3. Webhook secret matches configuration
4. Signature verification passing
5. Check webhook delivery logs in GitHub

### Partial Success Status

**Problem:** Some steps completed, others failed

**Check `executionSteps` array:** Each step has `success` and `error` fields showing exactly what failed and why.

Common issues:
- Commit analysis: GitHub token invalid or repo not accessible
- Similarity search: No historical failures to compare
- Pattern detection: Insufficient data (need 5+ failures with embeddings)

---

## Best Practices

1. **Always include source identifier** for tracking
2. **Use metadata** for build/branch information
3. **Enable commit analysis** when GitHub context available
4. **Check orchestration status** for long-running operations
5. **Store orchestration IDs** for future reference
6. **Monitor token usage** to optimize costs
7. **Set appropriate similarity thresholds** based on your needs (0.7-0.8 recommended)
8. **Use webhooks** for automated CI/CD integration

---

**Last Updated:** 2026-04-08
**Version:** 4.0
