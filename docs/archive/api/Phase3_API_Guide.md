# Phase 3 API Guide: Intelligent Failure Analysis

This guide covers the new intelligent failure analysis APIs introduced in Phase 3.

---

## Table of Contents
1. [Overview](#overview)
2. [Similarity Search API](#similarity-search-api)
3. [Pattern Detection API](#pattern-detection-api)
4. [Common Use Cases](#common-use-cases)
5. [Best Practices](#best-practices)

---

## Overview

Phase 3 introduces two powerful APIs for intelligent failure analysis:

1. **Similarity Search** - Find similar past failures using AI embeddings
2. **Pattern Detection** - Automatically discover failure patterns using clustering

Both APIs leverage machine learning to provide insights that would be impossible to find manually.

---

## Similarity Search API

### Endpoint
```
POST /api/pipeline/search/similar
```

### Purpose
Find failures similar to a given failure using semantic vector similarity. This helps you:
- Discover related failures across different time periods
- Learn from past resolutions
- Identify recurring issues
- Find failures with similar root causes

### Request

#### Search by Existing Failure ID
```json
POST /api/pipeline/search/similar
Content-Type: application/json

{
  "failureId": 123,
  "topN": 10,
  "minSimilarity": 0.7
}
```

#### Search by Raw Text
```json
POST /api/pipeline/search/similar
Content-Type: application/json

{
  "failureText": "NullReferenceException in PaymentService.ProcessPayment() - Object reference not set to an instance of an object",
  "topN": 5,
  "minSimilarity": 0.6,
  "errorType": "NullReferenceException",
  "lookbackDays": 90
}
```

#### Request Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `failureId` | integer |  | - | ID of existing failure to find similar matches |
| `failureText` | string |  | - | Raw failure text to search (generates embedding) |
| `topN` | integer | No | 10 | Number of results to return (1-100) |
| `minSimilarity` | float | No | 0.5 | Minimum similarity threshold (0.0-1.0) |
| `errorType` | string | No | null | Filter results by error type |
| `lookbackDays` | integer | No | null | Only search failures from last N days |

 Either `failureId` OR `failureText` is required (not both)

### Response

```json
{
  "query": {
    "queryType": "failure_id",
    "failureId": 123,
    "errorType": "NullReferenceException",
    "minSimilarity": 0.7,
    "topN": 10
  },
  "results": [
    {
      "id": 125,
      "errorType": "NullReferenceException",
      "rootCause": "Payment object was null when ProcessPayment was called",
      "detailedAnalysis": "The payment processor received a null payment object...",
      "analyzedAt": "2026-04-05T14:30:00Z",
      "similarityScore": 0.95,
      "similarityPercentage": 95.0,
      "confidenceScore": 0.92,
      "commitCount": 2,
      "repository": "myorg/payment-service"
    },
    {
      "id": 118,
      "errorType": "NullReferenceException",
      "rootCause": "Uninitialized payment gateway in PaymentService",
      "detailedAnalysis": "The payment gateway was not properly initialized...",
      "analyzedAt": "2026-04-01T10:15:00Z",
      "similarityScore": 0.87,
      "similarityPercentage": 87.0,
      "confidenceScore": 0.88,
      "commitCount": 1,
      "repository": "myorg/payment-service"
    }
  ],
  "totalCandidates": 150,
  "resultCount": 2,
  "executionTimeMs": 45
}
```

#### Response Fields

**Query Info:**
- `queryType`: "failure_id" or "failure_text"
- `failureId`: Query failure ID (if applicable)
- `errorType`: Error type filter applied
- `minSimilarity`: Minimum similarity threshold used
- `topN`: Maximum results requested

**Results (array):**
- `id`: Failure ID
- `errorType`: Type of error
- `rootCause`: Brief root cause summary
- `detailedAnalysis`: Full analysis text
- `analyzedAt`: When failure was analyzed (ISO 8601)
- `similarityScore`: Cosine similarity (0.0-1.0)
- `similarityPercentage`: Similarity as percentage (0-100)
- `confidenceScore`: Original RCA confidence (0.0-1.0)
- `commitCount`: Number of related commits
- `repository`: GitHub repository (if analyzed)

**Metadata:**
- `totalCandidates`: Total failures searched
- `resultCount`: Number of results returned
- `executionTimeMs`: Search execution time

### Error Responses

#### 400 Bad Request
```json
{
  "title": "Invalid Request",
  "detail": "Either FailureId or FailureText must be provided",
  "status": 400
}
```

#### 404 Not Found
```json
{
  "title": "Failure Not Found",
  "detail": "Failure with ID 123 not found",
  "status": 404
}
```

### cURL Examples

#### Search by Failure ID
```bash
curl -X POST http://localhost:5000/api/pipeline/search/similar \
  -H "Content-Type: application/json" \
  -d '{
    "failureId": 123,
    "topN": 5,
    "minSimilarity": 0.7
  }'
```

#### Search by Text with Filters
```bash
curl -X POST http://localhost:5000/api/pipeline/search/similar \
  -H "Content-Type: application/json" \
  -d '{
    "failureText": "TimeoutException connecting to database after 30 seconds",
    "topN": 10,
    "minSimilarity": 0.6,
    "errorType": "TimeoutException",
    "lookbackDays": 30
  }'
```

---

## Pattern Detection API

### Endpoint
```
POST /api/pipeline/patterns/detect
```

### Purpose
Automatically discover patterns and clusters in your failure history using K-means clustering. This helps you:
- Identify recurring failure patterns
- Understand failure trends over time
- Prioritize fixes for high-frequency patterns
- Detect systemic issues
- Track pattern evolution

### Request

#### Auto-Detect Optimal Clusters
```json
POST /api/pipeline/patterns/detect
Content-Type: application/json

{
  "lookbackDays": 30,
  "minClusterSize": 3
}
```

#### Specify Exact Cluster Count
```json
POST /api/pipeline/patterns/detect
Content-Type: application/json

{
  "clusterCount": 5,
  "maxIterations": 100,
  "errorType": "NullReferenceException",
  "lookbackDays": 60,
  "minClusterSize": 2,
  "randomSeed": 42
}
```

#### Request Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `clusterCount` | integer | No | auto | Number of patterns to detect (1-50) |
| `maxIterations` | integer | No | 100 | Max K-means iterations |
| `errorType` | string | No | null | Only analyze this error type |
| `lookbackDays` | integer | No | 30 | Analyze last N days (1-365) |
| `minClusterSize` | integer | No | 2 | Minimum failures per pattern |
| `randomSeed` | integer | No | null | Seed for reproducible results |

**Note:** If `clusterCount` is not specified, the system uses the elbow method to automatically determine the optimal number of clusters.

### Response

```json
{
  "patterns": [
    {
      "id": 0,
      "name": "Pattern 1: NullReferenceException",
      "description": "Cluster of 15 similar NullReferenceException failures",
      "primaryErrorType": "NullReferenceException",
      "failureCount": 15,
      "averageConfidence": 0.92,
      "averageSimilarity": 0.85,
      "firstOccurrence": "2026-03-15T08:00:00Z",
      "lastOccurrence": "2026-04-08T14:30:00Z",
      "frequency": 0.625,
      "sampleFailureIds": [101, 105, 112, 118, 125],
      "commonRootCauses": [
        "Payment object was null",
        "Uninitialized payment gateway",
        "Missing payment validation"
      ],
      "commonComponents": [
        "PaymentService",
        "PaymentProcessor",
        "OrderService"
      ]
    },
    {
      "id": 1,
      "name": "Pattern 2: TimeoutException",
      "description": "Cluster of 8 similar TimeoutException failures",
      "primaryErrorType": "TimeoutException",
      "failureCount": 8,
      "averageConfidence": 0.88,
      "averageSimilarity": 0.78,
      "firstOccurrence": "2026-03-20T10:00:00Z",
      "lastOccurrence": "2026-04-07T16:45:00Z",
      "frequency": 0.444,
      "sampleFailureIds": [103, 109, 115, 120, 128],
      "commonRootCauses": [
        "Database connection timeout",
        "External API timeout"
      ],
      "commonComponents": [
        "DatabaseService",
        "ApiClient"
      ]
    }
  ],
  "totalFailures": 50,
  "clusterCount": 3,
  "averageClusterSize": 16.67,
  "executionTimeMs": 125,
  "parameters": {
    "clusterCount": 3,
    "maxIterations": 100,
    "convergenceThreshold": 0.001,
    "minClusterSize": 2,
    "randomSeed": null
  }
}
```

#### Response Fields

**Patterns (array):**
- `id`: Pattern/cluster ID (0-indexed)
- `name`: Human-readable pattern name
- `description`: Pattern description
- `primaryErrorType`: Most common error type in pattern
- `failureCount`: Number of failures in this pattern
- `averageConfidence`: Average RCA confidence (0.0-1.0)
- `averageSimilarity`: Average intra-cluster similarity (0.0-1.0)
- `firstOccurrence`: When pattern first appeared (ISO 8601)
- `lastOccurrence`: When pattern last appeared (ISO 8601)
- `frequency`: Failures per day
- `sampleFailureIds`: Sample failure IDs (max 5)
- `commonRootCauses`: Most common root causes (max 3)
- `commonComponents`: Most common affected components (max 5)

**Summary:**
- `totalFailures`: Total failures analyzed
- `clusterCount`: Number of patterns found
- `averageClusterSize`: Average failures per pattern
- `executionTimeMs`: Detection execution time

**Parameters:**
- `clusterCount`: K value used
- `maxIterations`: Max iterations allowed
- `convergenceThreshold`: Convergence threshold used
- `minClusterSize`: Minimum cluster size filter
- `randomSeed`: Random seed (if specified)

### Interpreting Results

#### Pattern Similarity
- **> 0.8**: Highly cohesive pattern (very similar failures)
- **0.6 - 0.8**: Good pattern (related failures)
- **0.4 - 0.6**: Moderate pattern (loosely related)
- **< 0.4**: Weak pattern (may need more analysis)

#### Pattern Frequency
- **> 1.0**: Multiple failures per day (high priority)
- **0.5 - 1.0**: Failure every 1-2 days (moderate priority)
- **0.1 - 0.5**: Failure every 2-10 days (low priority)
- **< 0.1**: Rare occurrences

### Error Responses

#### 400 Bad Request
```json
{
  "title": "Invalid LookbackDays",
  "detail": "LookbackDays must be between 1 and 365",
  "status": 400
}
```

### cURL Examples

#### Auto-Detect Patterns
```bash
curl -X POST http://localhost:5000/api/pipeline/patterns/detect \
  -H "Content-Type: application/json" \
  -d '{
    "lookbackDays": 30,
    "minClusterSize": 3
  }'
```

#### Specify Cluster Count with Filters
```bash
curl -X POST http://localhost:5000/api/pipeline/patterns/detect \
  -H "Content-Type: application/json" \
  -d '{
    "clusterCount": 5,
    "errorType": "NullReferenceException",
    "lookbackDays": 60,
    "randomSeed": 42
  }'
```

---

## Common Use Cases

### Use Case 1: Investigating a New Failure

**Scenario:** A new production failure just occurred. You want to find similar past failures to understand potential root causes and solutions.

**Steps:**
1. Run RCA analysis on the new failure
2. Use similarity search to find related failures
3. Review common root causes and solutions
4. Apply learnings to resolve faster

```bash
# Step 1: Analyze the failure
curl -X POST http://localhost:5000/api/pipeline/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "ERROR: NullReferenceException in PaymentService..."
  }'

# Step 2: Find similar failures
curl -X POST http://localhost:5000/api/pipeline/search/similar \
  -H "Content-Type: application/json" \
  -d '{
    "failureText": "NullReferenceException in PaymentService...",
    "topN": 5,
    "minSimilarity": 0.7
  }'
```

### Use Case 2: Weekly Pattern Review

**Scenario:** Every week, review failure patterns to identify systemic issues and prioritize fixes.

**Steps:**
1. Detect patterns in last 7 days
2. Sort by frequency (failures/day)
3. Focus on high-frequency patterns
4. Track pattern evolution over time

```bash
# Detect weekly patterns
curl -X POST http://localhost:5000/api/pipeline/patterns/detect \
  -H "Content-Type: application/json" \
  -d '{
    "lookbackDays": 7,
    "minClusterSize": 2
  }'
```

### Use Case 3: Root Cause Analysis Enhancement

**Scenario:** Enhance RCA by checking if similar failures have been seen before.

**Steps:**
1. Perform RCA analysis
2. Search for similar failures (>80% similarity)
3. If found, include historical context in analysis
4. Save enhanced analysis

```bash
# After RCA, check for similar failures
curl -X POST http://localhost:5000/api/pipeline/search/similar \
  -H "Content-Type: application/json" \
  -d '{
    "failureId": 150,
    "topN": 3,
    "minSimilarity": 0.8
  }'
```

### Use Case 4: Sprint Planning

**Scenario:** Identify which failure patterns to fix in the next sprint based on impact.

**Steps:**
1. Detect patterns over last 30 days
2. Calculate impact: `frequency × failureCount`
3. Sort by impact
4. Create fix tasks for top patterns

```bash
# Get 30-day patterns
curl -X POST http://localhost:5000/api/pipeline/patterns/detect \
  -H "Content-Type: application/json" \
  -d '{
    "lookbackDays": 30,
    "minClusterSize": 3
  }'

# Calculate impact in your code:
# impact = pattern.frequency * pattern.failureCount
```

### Use Case 5: Post-Incident Review

**Scenario:** After a major incident, find if similar issues occurred before that were missed.

**Steps:**
1. Search for similar failures in the past
2. Expand time range (90+ days)
3. Lower similarity threshold (0.6)
4. Review historical patterns to prevent recurrence

```bash
curl -X POST http://localhost:5000/api/pipeline/search/similar \
  -H "Content-Type: application/json" \
  -d '{
    "failureText": "Incident description...",
    "topN": 20,
    "minSimilarity": 0.6,
    "lookbackDays": 90
  }'
```

---

## Best Practices

### Similarity Search

1. **Start with Higher Thresholds**
   - Use `minSimilarity: 0.7-0.8` initially
   - Lower to 0.5-0.6 if no results found
   - Very low thresholds (<0.5) may return noise

2. **Use Appropriate TopN**
   - `topN: 5-10` for quick investigation
   - `topN: 20-50` for comprehensive analysis
   - Larger values increase response time

3. **Filter by Error Type**
   - Improves relevance when you know the error type
   - Reduces search space for better performance

4. **Leverage Lookback Days**
   - Recent failures (7-30 days) are often more relevant
   - Extend to 90+ days for rare issues
   - Balance between coverage and relevance

### Pattern Detection

1. **Let Auto-Detection Work**
   - Don't specify `clusterCount` unless you have a reason
   - Elbow method usually finds optimal K
   - Manual K is useful for reproducibility

2. **Adjust Min Cluster Size**
   - `minClusterSize: 3-5` for stable patterns
   - `minClusterSize: 2` includes more patterns (noisier)
   - Higher values focus on significant patterns

3. **Review Patterns Regularly**
   - Weekly reviews catch emerging patterns
   - Monthly reviews show long-term trends
   - Track pattern evolution over time

4. **Use Random Seed for Reproducibility**
   - Specify `randomSeed` for consistent results
   - Useful for automated reporting
   - Different seeds may reveal different patterns

5. **Interpret Metrics Holistically**
   - High frequency + high count = urgent fix needed
   - High similarity = well-defined pattern
   - Low similarity = may need investigation

### Performance Optimization

1. **Limit Candidate Size**
   - Use `errorType` filter to reduce search space
   - Use `lookbackDays` to limit time range
   - System limits to 10,000 candidates automatically

2. **Batch Operations**
   - Search/detect once, cache results
   - Don't call APIs in tight loops
   - Consider rate limiting in production

3. **Monitor Execution Time**
   - Most queries: < 100ms
   - Alert if consistently > 500ms
   - May indicate need for vector database

---

## Integration Examples

### Python
```python
import requests

# Similarity search
def find_similar_failures(failure_id, top_n=10, min_similarity=0.7):
    response = requests.post(
        'http://localhost:5000/api/pipeline/search/similar',
        json={
            'failureId': failure_id,
            'topN': top_n,
            'minSimilarity': min_similarity
        }
    )
    return response.json()

# Pattern detection
def detect_patterns(lookback_days=30):
    response = requests.post(
        'http://localhost:5000/api/pipeline/patterns/detect',
        json={
            'lookbackDays': lookback_days,
            'minClusterSize': 3
        }
    )
    return response.json()

# Usage
similar = find_similar_failures(failure_id=123)
print(f"Found {similar['resultCount']} similar failures")

patterns = detect_patterns(lookback_days=7)
print(f"Detected {patterns['clusterCount']} patterns")
```

### C#
```csharp
using System.Net.Http;
using System.Text.Json;

public class FailureAnalysisClient
{
    private readonly HttpClient _client;

    public FailureAnalysisClient(string baseUrl)
    {
        _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<SimilaritySearchResponse> FindSimilarFailuresAsync(
        int failureId, int topN = 10, float minSimilarity = 0.7f)
    {
        var request = new {
            failureId,
            topN,
            minSimilarity
        };

        var response = await _client.PostAsJsonAsync(
            "/api/pipeline/search/similar", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SimilaritySearchResponse>();
    }

    public async Task<PatternDetectionResult> DetectPatternsAsync(int lookbackDays = 30)
    {
        var request = new {
            lookbackDays,
            minClusterSize = 3
        };

        var response = await _client.PostAsJsonAsync(
            "/api/pipeline/patterns/detect", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PatternDetectionResult>();
    }
}

// Usage
var client = new FailureAnalysisClient("http://localhost:5000");
var similar = await client.FindSimilarFailuresAsync(failureId: 123);
Console.WriteLine($"Found {similar.ResultCount} similar failures");
```

---

## Troubleshooting

### No Similar Failures Found
- **Cause**: minSimilarity threshold too high or no similar failures exist
- **Solution**: Lower minSimilarity to 0.5 or 0.6, expand lookbackDays

### Pattern Detection Returns 0 Patterns
- **Cause**: Insufficient failures with embeddings, or all clusters too small
- **Solution**: Increase lookbackDays, decrease minClusterSize, check if embeddings are being generated

### Slow Response Times (>500ms)
- **Cause**: Too many candidates being searched
- **Solution**: Add errorType filter, reduce lookbackDays, or consider vector database

### Embedding Not Found Error
- **Cause**: Failure doesn't have an embedding (older failures)
- **Solution**: Re-save old failures to trigger embedding generation

---

## Additional Resources

- [Phase 3 Completion Report](../tracking/Phase3_Completion_Report.md)
- [Main API Documentation](../api/API_Documentation.md)
- [AWS Bedrock Titan Embeddings](https://docs.aws.amazon.com/bedrock/latest/userguide/titan-embeddings.html)
- [K-means Clustering Algorithm](https://en.wikipedia.org/wiki/K-means_clustering)
- [Cosine Similarity](https://en.wikipedia.org/wiki/Cosine_similarity)

---

**Last Updated:** 2026-04-08
