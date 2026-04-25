# Pattern Detection API - Complete Guide with Examples

**Last Updated:** 2026-04-21
**Phase:** Phase 3 - Pattern Recognition
**Endpoint:** `POST /api/pipeline/patterns/detect`

---

## Table of Contents

1. [The Big Picture](#the-big-picture)
2. [Real Example Walkthrough](#real-example-walkthrough)
3. [Step-by-Step Function Calls](#step-by-step-function-calls)
4. [Mathematical Algorithms Explained](#mathematical-algorithms-explained)
5. [Visual Summary](#visual-summary)
6. [Testing Guide](#testing-guide)

---

## The Big Picture

### Simple Analogy

**Imagine you have 50 socks scattered on your bed**. Some are red, some blue, some green. You want to:
1. **Group similar socks together** (Pattern Detection)
2. **Name each group** ("Red sock pattern", "Blue sock pattern")
3. **Count how many in each group**
4. **Find which group appears most often**

That's exactly what Pattern Detection does with CI/CD failures!

### Business Problem

Imagine you have a CI/CD pipeline that fails frequently. Over weeks/months, you accumulate hundreds of failure analyses. You need answers to:

1. **"Are there recurring issues?"** - Do the same types of failures keep happening?
2. **"What are the common patterns?"** - Can we group similar failures together?
3. **"Where should we focus?"** - Which failure pattern occurs most frequently?
4. **"Is this a new issue or known problem?"** - When a new failure occurs, does it match an existing pattern?

### Real-World Scenario

**Week 1-4: Your Pipeline Failures**
- 50 NullReferenceException failures
- 30 Missing dependency errors
- 20 Test timeout failures
- 15 Database connection issues
- 10 Configuration errors

**Without Pattern Detection:**
- You manually review 125 failures one-by-one
- Hard to see which issues are related
- Can't prioritize fixes effectively
- New failures seem unique each time

**With Pattern Detection:**
```
Pattern 1: NullReferenceException (50 failures, 2.5/day)
  Common cause: Uninitialized payment service
  Affected: PaymentController.cs, OrderService.cs

Pattern 2: Dependency Issues (30 failures, 1.5/day)
  Common cause: NuGet package restore failures
  Affected: Multiple projects

Pattern 3: Test Timeouts (20 failures, 1.0/day)
  Common cause: Database connection pool exhaustion
  Affected: IntegrationTests
```

**Benefits:**
-  **Prioritize:** Fix Pattern 1 first (highest frequency)
-  **Root Cause:** See that 50 failures = 1 underlying issue
-  **Track Progress:** After fix, watch Pattern 1 frequency drop
-  **Classify New Failures:** When new failure occurs, see if it matches existing pattern

---

## Real Example Walkthrough

Let's say your CI/CD pipeline has failed **20 times** in the last month. Here's what those failures look like:

```
Failure 1:  "NullReferenceException in PaymentService.cs line 42"
Failure 2:  "NullReferenceException in PaymentService.cs line 45"
Failure 3:  "Missing closing brace in IClaudeService.cs"
Failure 4:  "NullReferenceException in PaymentService.cs line 42"
Failure 5:  "Missing closing brace in IUserService.cs"
Failure 6:  "Test timeout in IntegrationTests.cs"
Failure 7:  "NullReferenceException in PaymentService.cs line 43"
Failure 8:  "Test timeout in DatabaseTests.cs"
Failure 9:  "Missing closing brace in IClaudeService.cs"
Failure 10: "Test timeout in ApiTests.cs"
Failure 11: "NullReferenceException in PaymentService.cs line 42"
Failure 12: "Missing closing brace in IUserService.cs"
Failure 13: "NullReferenceException in OrderService.cs line 88"
Failure 14: "Test timeout in IntegrationTests.cs"
Failure 15: "Missing closing brace in IClaudeService.cs"
Failure 16: "NullReferenceException in PaymentService.cs line 45"
Failure 17: "Test timeout in DatabaseTests.cs"
Failure 18: "Missing closing brace in IUserService.cs"
Failure 19: "NullReferenceException in PaymentService.cs line 42"
Failure 20: "Test timeout in ApiTests.cs"
```

**Your question:** "Are there patterns here? What's the most common problem?"

---

## Step-by-Step Function Calls

### Step 1: You Make the API Call

**Your Request:**
```json
POST /api/pipeline/patterns/detect
{
  "lookbackDays": 30,
  "minClusterSize": 2
}
```

**What this means:**
- Look at failures from the last 30 days
- Only show me patterns with at least 2 failures (ignore one-offs)

---

### Step 2: Controller Receives Your Request

**Function:** `PipelineController.DetectPatterns()`

**Code Location:** `PipelineController.cs:647`

**What happens:**
```csharp
// Line 679: Calculate cutoff date
var cutoffDate = DateTime.UtcNow.AddDays(-30);
```

**In simple terms:**
- Today is April 21, 2026
- 30 days ago = March 22, 2026
- "Only get me failures after March 22"

**Example:**
```
Today: April 21, 2026
Cutoff: March 22, 2026

 Failure from April 1 → Include
 Failure from March 25 → Include
 Failure from March 15 → Skip (too old)
```

---

### Step 3: Fetch Failures from Database

**Function:** `_failureRepository.GetHistoryAsync()`

**Code Location:** `PipelineController.cs:680-684`

**What happens:**
```csharp
var allFailures = await _failureRepository.GetHistoryAsync(
    limit: 10000,
    offset: 0,
    errorType: null,  // Get all types
    cancellationToken);
```

**SQL Query (behind the scenes):**
```sql
SELECT * FROM FailureEntities
WHERE AnalyzedAt >= '2026-03-22'
AND Embedding IS NOT NULL
ORDER BY AnalyzedAt DESC
LIMIT 10000
```

**Result:** Returns our 20 failures (all have embeddings)

---

### Step 4: Filter the Data

**Code Location:** `PipelineController.cs:686-689`

**What happens:**
```csharp
var recentFailures = allFailures
    .Where(f => f.AnalyzedAt >= cutoffDate)
    .Where(f => f.Embedding != null && f.Embedding.Length > 0)
    .ToList();
```

**In simple terms:**
- Start with 20 failures
- Remove any without embeddings → Still 20 (all have embeddings)
- Remove any older than cutoff → Still 20 (all recent)

**Result:** 20 failures ready for analysis

---

### Step 5: Determine How Many Groups (Clusters)

**Function:** `DetermineOptimalClusterCount()`

**Code Location:** `KMeansPatternRecognitionService.cs:153`

**What happens:**
```csharp
clusterCount = _patternRecognitionService.DetermineOptimalClusterCount(
    recentFailures,
    maxClusters: Math.Min(10, recentFailures.Count / 3));
```

**In simple terms:**
Think of this as: "How many piles should I sort these socks into?"

**The Algorithm Tries Different Numbers:**

**Try K=1 (all in one group):**
```
Group 1: All 20 failures
Score: 8.5 (very spread out, not good)
```

**Try K=2 (two groups):**
```
Group 1: Failures 1,2,4,7,11,13,16,19 (NullRef)
Group 2: Failures 3,5,6,8,9,10,12,14,15,17,18,20 (Others)
Score: 4.2 (better!)
```

**Try K=3 (three groups):**
```
Group 1: Failures 1,2,4,7,11,13,16,19 (NullRef)
Group 2: Failures 3,5,9,12,15,18 (Missing brace)
Group 3: Failures 6,8,10,14,17,20 (Test timeout)
Score: 1.8 (even better!)
```

**Try K=4 (four groups):**
```
Group 1: Failures 1,2,4,7,11,16,19 (NullRef in Payment)
Group 2: Failure 13 (NullRef in Order)
Group 3: Failures 3,5,9,12,15,18 (Missing brace)
Group 4: Failures 6,8,10,14,17,20 (Test timeout)
Score: 1.7 (only slightly better)
```

**Graph Visualization:**
```
Score
  │
9 │                          (K=1)
  │
6 │                          (K=2)
  │
3 │                          (K=3) ← ELBOW! Big improvement here
  │       ──               (K=4, K=5, K=6) Only small improvements
0 │___________________________
      1   2   3   4   5   6
                  Number of Groups (K)
```

**Decision:** K=3 is optimal (the "elbow" point)

**Result:** Use 3 groups/patterns

---

### Step 6: Run K-Means Clustering

**Function:** `RunKMeans()`

**Code Location:** `KMeansPatternRecognitionService.cs:275`

This is where the magic happens! Let me break it down into mini-steps:

---

#### Step 6.1: Initialize Starting Points (K-means++)

**Function:** `InitializeCentroids()`

**Code Location:** `KMeansPatternRecognitionService.cs:317`

**What happens:**
Think of centroids as "group leaders". We need to pick 3 leaders.

**Visual Representation:**
```
All 20 failures as dots in space:
(In reality, these are 512-dimensional vectors, but let's pretend it's 2D)

        
             
              
               
```

**Pick Centroid 1 (Random):**
```
        
             
              
               
         ↑
      Leader 1 (picked randomly)
```

**Pick Centroid 2 (Far from Centroid 1):**
```
Calculate distance from each failure to Leader 1
Pick next leader far away

          ← This cluster is farthest
             
              
               
         ↑      ↑
      Leader 1  Leader 2 (picked far from Leader 1)
```

**Pick Centroid 3 (Far from Leaders 1 & 2):**
```
        
             
                ← This cluster is now farthest
               
         ↑      ↑    ↑
      Leader 1  Leader 2  Leader 3
```

**Code:**
```csharp
// Lines 324-326: Pick first centroid randomly
var firstIdx = random.Next(embeddings.Count);
centroids.Add((float[])embeddings[firstIdx].Clone());
indices.Add(firstIdx);

// Lines 329-380: Pick remaining centroids based on distance
for (int i = 1; i < k; i++)
{
    var distances = new float[embeddings.Count];
    for (int j = 0; j < embeddings.Count; j++)
    {
        // Find minimum distance to existing centroids
        float minDist = float.MaxValue;
        foreach (var centroid in centroids)
        {
            var similarity = _similarityService.CalculateCosineSimilarity(
                embeddings[j],
                centroid);
            var distance = 1.0f - similarity;
            minDist = Math.Min(minDist, distance);
        }
        distances[j] = minDist * minDist; // Square for probability weighting
    }

    // Choose next centroid with probability proportional to squared distance
    // (Points far away are more likely to be chosen)
}
```

**Result:** 3 leaders placed far apart from each other

---

#### Step 6.2: Assign Failures to Groups (Iteration 1)

**Function:** `FindNearestCentroid()`

**Code Location:** `KMeansPatternRecognitionService.cs:385`

**What happens:**
For each failure, measure how similar it is to each leader, then join that leader's group.

**Example with Failure #1:**
```
Failure 1: "NullReferenceException in PaymentService.cs line 42"
Embedding: [0.52, 0.31, 0.78, 0.45, ... ] (512 numbers)

Similarity to Leader 1: 0.95 ← HIGHEST! (95% match)
Similarity to Leader 2: 0.42
Similarity to Leader 3: 0.38

Decision: Failure 1 joins Group 1
```

**Code:**
```csharp
// Lines 385-404: Find nearest centroid for each failure
private (int clusterId, float distance) FindNearestCentroid(
    float[] embedding,
    List<float[]> centroids)
{
    int nearestCluster = 0;
    float maxSimilarity = float.MinValue;

    for (int i = 0; i < centroids.Count; i++)
    {
        var similarity = _similarityService.CalculateCosineSimilarity(
            embedding,
            centroids[i]);
        if (similarity > maxSimilarity)
        {
            maxSimilarity = similarity;
            nearestCluster = i;
        }
    }

    var distance = 1.0f - maxSimilarity; // Convert similarity to distance
    return (nearestCluster, distance);
}
```

**Do this for all 20 failures:**

```
After Iteration 1:

Group 1 (Leader 1):
├─ Failure 1: NullRef in PaymentService line 42
├─ Failure 2: NullRef in PaymentService line 45
├─ Failure 4: NullRef in PaymentService line 42
├─ Failure 7: NullRef in PaymentService line 43
├─ Failure 11: NullRef in PaymentService line 42
├─ Failure 13: NullRef in OrderService line 88
├─ Failure 16: NullRef in PaymentService line 45
└─ Failure 19: NullRef in PaymentService line 42
(8 members)

Group 2 (Leader 2):
├─ Failure 3: Missing brace in IClaudeService
├─ Failure 5: Missing brace in IUserService
├─ Failure 9: Missing brace in IClaudeService
├─ Failure 12: Missing brace in IUserService
├─ Failure 15: Missing brace in IClaudeService
└─ Failure 18: Missing brace in IUserService
(6 members)

Group 3 (Leader 3):
├─ Failure 6: Test timeout in IntegrationTests
├─ Failure 8: Test timeout in DatabaseTests
├─ Failure 10: Test timeout in ApiTests
├─ Failure 14: Test timeout in IntegrationTests
├─ Failure 17: Test timeout in DatabaseTests
└─ Failure 20: Test timeout in ApiTests
(6 members)
```

---

#### Step 6.3: Update Leaders (Move to Center)

**Function:** `UpdateCentroids()`

**Code Location:** `KMeansPatternRecognitionService.cs:406`

**What happens:**
Each leader moves to the **center** (average position) of their group.

**Example for Group 1:**
```
Group 1 has 8 members:
Failure 1 embedding: [0.52, 0.31, 0.78, ...]
Failure 2 embedding: [0.48, 0.35, 0.81, ...]
Failure 4 embedding: [0.51, 0.32, 0.79, ...]
Failure 7 embedding: [0.49, 0.33, 0.80, ...]
Failure 11 embedding: [0.52, 0.31, 0.78, ...]
Failure 13 embedding: [0.55, 0.28, 0.74, ...]
Failure 16 embedding: [0.47, 0.36, 0.82, ...]
Failure 19 embedding: [0.52, 0.31, 0.78, ...]

Calculate average for each dimension:
New Leader 1 position:
  Dimension 1: (0.52+0.48+0.51+0.49+0.52+0.55+0.47+0.52) / 8 = 0.51
  Dimension 2: (0.31+0.35+0.32+0.33+0.31+0.28+0.36+0.31) / 8 = 0.32
  Dimension 3: (0.78+0.81+0.79+0.80+0.78+0.74+0.82+0.78) / 8 = 0.79
  ... (continue for all 512 dimensions)

New Leader 1: [0.51, 0.32, 0.79, ...]
```

**Code:**
```csharp
// Lines 406-447: Update centroids to cluster means
private List<float[]> UpdateCentroids(
    List<float[]> embeddings,
    List<ClusterAssignment> assignments,
    int clusterCount)
{
    var newCentroids = new List<float[]>();
    var embeddingDim = embeddings[0].Length;

    for (int k = 0; k < clusterCount; k++)
    {
        var clusterMembers = assignments
            .Where(a => a.ClusterId == k)
            .Select(a => assignments.IndexOf(a))
            .ToList();

        if (clusterMembers.Count == 0)
        {
            // Empty cluster - keep previous centroid or reinitialize
            newCentroids.Add(new float[embeddingDim]);
            continue;
        }

        // Calculate mean of all members
        var centroid = new float[embeddingDim];
        foreach (var memberIdx in clusterMembers)
        {
            for (int d = 0; d < embeddingDim; d++)
            {
                centroid[d] += embeddings[memberIdx][d];
            }
        }

        for (int d = 0; d < embeddingDim; d++)
        {
            centroid[d] /= clusterMembers.Count;
        }

        newCentroids.Add(centroid);
    }

    return newCentroids;
}
```

**Visual:**
```
Before:                After Update:
                           
                            
        ↑                  ↑    
                                  
      Leader 1               Leader moved to center!
```

**Result:** All 3 leaders move to the center of their groups

---

#### Step 6.4: Check if Done (Convergence)

**Function:** `HasConverged()`

**Code Location:** `KMeansPatternRecognitionService.cs:449`

**What happens:**
Check if leaders stopped moving. If they barely moved, we're done!

**Example:**
```
Leader 1:
  Old position: [0.50, 0.30, 0.78, ...]
  New position: [0.51, 0.32, 0.79, ...]

  How much did it move?
  similarity = 0.9998 (99.98% same position)
  movement = 1 - 0.9998 = 0.0002

  Is movement < 0.001? YES!  Converged

Leader 2:
  Old position: [0.65, 0.42, 0.33, ...]
  New position: [0.66, 0.43, 0.34, ...]
  movement = 0.0003 < 0.001  Converged

Leader 3:
  Old position: [0.21, 0.88, 0.55, ...]
  New position: [0.21, 0.89, 0.56, ...]
  movement = 0.0004 < 0.001  Converged

ALL LEADERS CONVERGED! Stop iterating.
```

**Code:**
```csharp
// Lines 449-468: Check convergence
private bool HasConverged(
    List<float[]> oldCentroids,
    List<float[]> newCentroids,
    float threshold)
{
    for (int i = 0; i < oldCentroids.Count; i++)
    {
        var similarity = _similarityService.CalculateCosineSimilarity(
            oldCentroids[i],
            newCentroids[i]);
        var movement = 1.0f - similarity;

        if (movement > threshold)
        {
            return false;
        }
    }

    return true;
}
```

**If NOT converged:** Go back to Step 6.2 and repeat (assign → update → check)

**Result:** After 7 iterations, all leaders barely moved. Done!

---

### Step 7: Generate Pattern Metadata

**Function:** `GeneratePatterns()`

**Code Location:** `KMeansPatternRecognitionService.cs:470`

**What happens:**
For each group, analyze what the failures have in common.

---

#### Pattern 1 Analysis (Group 1)

**Members:** 8 NullReferenceException failures

```csharp
// Line 491-496: Find most common error type
var errorTypeCounts = clusterFailures
    .GroupBy(f => f.ErrorType)
    .OrderByDescending(g => g.Count());

Result:
  NullReferenceException: 8 ← Primary type
```

```csharp
// Line 497: Calculate average confidence
var avgConfidence = clusterFailures.Average(f => f.ConfidenceScore);

Result: (0.92 + 0.91 + 0.93 + 0.90 + 0.92 + 0.88 + 0.91 + 0.92) / 8 = 0.91
```

```csharp
// Line 504-516: Calculate how similar failures are within the group
// Compare every pair of failures

Failure 1 vs Failure 2: similarity = 0.95
Failure 1 vs Failure 4: similarity = 0.99
Failure 1 vs Failure 7: similarity = 0.94
... (all pairs)

Average similarity = 0.89 (89% similar to each other!)
```

```csharp
// Line 519-536: Find common files
var allComponents = clusterFailures
    .Select(f => ExtractFiles(f.AffectedComponents))
    .GroupBy(file => file)
    .OrderByDescending(count);

Files mentioned:
  PaymentService.cs: 7 times ← Most common
  OrderService.cs: 1 time

Common components: ["PaymentService.cs", "OrderService.cs"]
```

```csharp
// Line 539-545: Find common root causes
var commonRootCauses = clusterFailures
    .Select(f => f.RootCause)
    .GroupBy(cause => cause);

Root causes:
  "Uninitialized payment object": 7 times ← Most common
  "Null order reference": 1 time

Common causes: ["Uninitialized payment object"]
```

```csharp
// Line 548-551: Calculate frequency
var firstOccurrence = April 1, 2026
var lastOccurrence = April 20, 2026
var daySpan = 19 days
var frequency = 8 failures / 19 days = 0.42 failures/day
```

**Final Pattern 1:**
```json
{
  "id": 0,
  "name": "Pattern 1: NullReferenceException",
  "description": "Cluster of 8 similar NullReferenceException failures",
  "primaryErrorType": "NullReferenceException",
  "failureCount": 8,
  "averageConfidence": 0.91,
  "averageSimilarity": 0.89,
  "firstOccurrence": "2026-04-01",
  "lastOccurrence": "2026-04-20",
  "frequency": 0.42,
  "sampleFailureIds": [1, 2, 4, 7, 11],
  "commonRootCauses": ["Uninitialized payment object"],
  "commonComponents": ["PaymentService.cs", "OrderService.cs"]
}
```

---

#### Pattern 2 Analysis (Group 2)

**Members:** 6 CompilationError failures

Same process:
- Primary error type: CompilationError (6/6)
- Average confidence: 0.94
- Average similarity: 0.92 (very tight cluster!)
- Common files: IClaudeService.cs (3), IUserService.cs (3)
- Common cause: "Missing closing brace"
- Frequency: 6 failures / 19 days = 0.32 failures/day

**Final Pattern 2:**
```json
{
  "id": 1,
  "name": "Pattern 2: CompilationError",
  "description": "Cluster of 6 similar CompilationError failures",
  "primaryErrorType": "CompilationError",
  "failureCount": 6,
  "averageConfidence": 0.94,
  "averageSimilarity": 0.92,
  "firstOccurrence": "2026-04-02",
  "lastOccurrence": "2026-04-19",
  "frequency": 0.32,
  "sampleFailureIds": [3, 5, 9, 12, 15],
  "commonRootCauses": ["Missing closing brace"],
  "commonComponents": ["IClaudeService.cs", "IUserService.cs"]
}
```

---

#### Pattern 3 Analysis (Group 3)

**Members:** 6 TestFailure failures

- Primary error type: TestFailure (6/6)
- Average confidence: 0.87
- Average similarity: 0.88
- Common files: IntegrationTests.cs, DatabaseTests.cs, ApiTests.cs
- Common cause: "Test timeout after 30 seconds"
- Frequency: 6 failures / 18 days = 0.33 failures/day

**Final Pattern 3:**
```json
{
  "id": 2,
  "name": "Pattern 3: TestFailure",
  "description": "Cluster of 6 similar TestFailure failures",
  "primaryErrorType": "TestFailure",
  "failureCount": 6,
  "averageConfidence": 0.87,
  "averageSimilarity": 0.88,
  "firstOccurrence": "2026-04-03",
  "lastOccurrence": "2026-04-20",
  "frequency": 0.33,
  "sampleFailureIds": [6, 8, 10, 14, 17],
  "commonRootCauses": ["Test timeout after 30 seconds"],
  "commonComponents": ["IntegrationTests.cs", "DatabaseTests.cs"]
}
```

---

### Step 8: Filter Small Patterns

**Code Location:** `KMeansPatternRecognitionService.cs:79-82`

**What happens:**
```csharp
patterns = patterns
    .Where(p => p.FailureCount >= parameters.MinClusterSize) // >= 2
    .OrderByDescending(p => p.FailureCount)
    .ToList();
```

**In simple terms:**
- Remove patterns with less than 2 failures (we set minClusterSize=2)
- Sort by count (biggest pattern first)

**Result:**
```
Pattern 1: 8 failures  Keep (>= 2)
Pattern 2: 6 failures  Keep (>= 2)
Pattern 3: 6 failures  Keep (>= 2)

Sorted by count:
1. Pattern 1 (8 failures) ← Biggest problem!
2. Pattern 2 (6 failures)
3. Pattern 3 (6 failures)
```

---

### Step 9: Return Final Result

**Code Location:** `KMeansPatternRecognitionService.cs:90-96`

**What happens:**
Package everything up and send it back to you!

**Your Final Response:**
```json
{
  "patterns": [
    {
      "id": 0,
      "name": "Pattern 1: NullReferenceException",
      "description": "Cluster of 8 similar NullReferenceException failures",
      "primaryErrorType": "NullReferenceException",
      "failureCount": 8,
      "averageConfidence": 0.91,
      "averageSimilarity": 0.89,
      "firstOccurrence": "2026-04-01T08:00:00Z",
      "lastOccurrence": "2026-04-20T14:30:00Z",
      "frequency": 0.42,
      "sampleFailureIds": [1, 2, 4, 7, 11],
      "commonRootCauses": [
        "Uninitialized payment object"
      ],
      "commonComponents": [
        "PaymentService.cs",
        "OrderService.cs"
      ]
    },
    {
      "id": 1,
      "name": "Pattern 2: CompilationError",
      "failureCount": 6,
      ...
    },
    {
      "id": 2,
      "name": "Pattern 3: TestFailure",
      "failureCount": 6,
      ...
    }
  ],
  "totalFailures": 20,
  "clusterCount": 3,
  "averageClusterSize": 6.67,
  "executionTimeMs": 187,
  "parameters": {
    "clusterCount": 3,
    "maxIterations": 100,
    "convergenceThreshold": 0.001,
    "minClusterSize": 2
  }
}
```

---

## Mathematical Algorithms Explained

### Algorithm 1: Cosine Similarity

**Purpose:** Measure how similar two embeddings (vectors) are.

**Formula:**
```
similarity = (A · B) / (||A|| × ||B||)

Where:
• A · B = dot product = Σ(A[i] × B[i])
• ||A|| = magnitude = sqrt(Σ(A[i]²))
• ||B|| = magnitude = sqrt(Σ(B[i]²))
```

**Example:**
```csharp
A = [0.5, 0.3, 0.8]  // Embedding 1 (simplified to 3-dim)
B = [0.6, 0.2, 0.7]  // Embedding 2

A · B = (0.5×0.6) + (0.3×0.2) + (0.8×0.7) = 0.3 + 0.06 + 0.56 = 0.92
||A|| = sqrt(0.5² + 0.3² + 0.8²) = sqrt(0.98) = 0.99
||B|| = sqrt(0.6² + 0.2² + 0.7²) = sqrt(0.89) = 0.94

similarity = 0.92 / (0.99 × 0.94) = 0.92 / 0.93 = 0.99  (99% similar!)
```

**Result Range:**
- `1.0` = Identical vectors
- `0.5` = 50% similar
- `0.0` = Orthogonal (unrelated)
- `-1.0` = Opposite vectors

**Code Location:** `CosineSimilarityService.cs`

---

### Algorithm 2: K-means Clustering

**Purpose:** Group similar failures into K clusters.

**High-Level Overview:**
1. **Initialize** - Pick K starting points (centroids)
2. **Assignment** - Assign each failure to nearest centroid
3. **Update** - Move centroids to center of their groups
4. **Repeat** - Until centroids stop moving (converged)

**Why it works:**
Each iteration improves the grouping by moving group centers closer to their members.

---

### Algorithm 3: Elbow Method (Optimal K)

**Purpose:** Automatically find the best number of clusters.

**How it works:**

```
Inertia = sum of squared distances from each point to its centroid

For K=1 to K=10:
  1. Run K-means with K clusters
  2. Calculate inertia
  3. Store (K, inertia) pair

Inertias: [5.2, 3.8, 2.1, 1.9, 1.85, 1.83, 1.82, 1.81]
           K=1  K=2  K=3  K=4  K=5   K=6   K=7   K=8

Find elbow: point where improvement slows down

Changes:
  K=1→2: 5.2 - 3.8 = 1.4  (big improvement!)
  K=2→3: 3.8 - 2.1 = 1.7  (even bigger!)
  K=3→4: 2.1 - 1.9 = 0.2  (small) ← ELBOW!
  K=4→5: 1.9 - 1.85 = 0.05 (tiny)

Change reductions:
  K=2: 1.4 - 1.7 = -0.3
  K=3: 1.7 - 0.2 = 1.5 ← MAXIMUM
  K=4: 0.2 - 0.05 = 0.15

Optimal K = 3 (where change reduction is maximum)
```

**Code Location:** `KMeansPatternRecognitionService.cs:596-621`

---

### Algorithm 4: Silhouette Score

**Purpose:** Measure how well-clustered the data is.

**Formula:**
```
For each point i:
  a(i) = average distance to points in same cluster
  b(i) = average distance to points in nearest other cluster

  silhouette(i) = (b(i) - a(i)) / max(a(i), b(i))

Overall score = average of all silhouette(i)

Range:
  +1.0 = Perfect clustering (far from other clusters)
   0.0 = On border between clusters
  -1.0 = Misclassified (closer to other cluster)

Interpretation:
  > 0.7 = Excellent
  > 0.5 = Good
  > 0.25 = Reasonable
  < 0.25 = Poor clustering
```

**Code Location:** `KMeansPatternRecognitionService.cs:195-271`

---

## Visual Summary

### Complete Architecture Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. API CONTROLLER LAYER (PipelineController)                    │
└─────────────────────────────────────────────────────────────────┘
          │
          │ POST /api/pipeline/patterns/detect
          │ Request: { lookbackDays: 30, minClusterSize: 2 }
          │
          
┌─────────────────────────────────────────────────────────────────┐
│ 2. DATA RETRIEVAL (FailureRepository)                          │
│    • Calculate cutoff date (UtcNow - lookbackDays)              │
│    • Query database: SELECT * FROM FailureEntities              │
│    • Filter: AnalyzedAt >= cutoffDate                           │
│    • Filter: Embedding IS NOT NULL                              │
│    • Filter: ErrorType = 'CompilationError' (if specified)     │
└─────────────────────────────────────────────────────────────────┘
          │
          │ Returns 20 failure records with embeddings
          │
          
┌─────────────────────────────────────────────────────────────────┐
│ 3. OPTIMAL K DETERMINATION (if not specified)                   │
│    KMeansPatternRecognitionService.DetermineOptimalClusterCount │
│                                                                 │
│    Try K=1,2,3,4,5... and find "elbow" point                   │
│    Result: K=3 is optimal                                       │
└─────────────────────────────────────────────────────────────────┘
          │
          
┌─────────────────────────────────────────────────────────────────┐
│ 4. K-MEANS CLUSTERING (RunKMeans)                               │
│                                                                 │
│  Step 1: Initialize 3 centroids (K-means++)                    │
│  Step 2: Assign each failure to nearest centroid               │
│  Step 3: Update centroids to cluster means                     │
│  Step 4: Check convergence                                     │
│  Repeat Steps 2-4 until converged                              │
│                                                                 │
│  Result: 3 groups formed                                        │
└─────────────────────────────────────────────────────────────────┘
          │
          
┌─────────────────────────────────────────────────────────────────┐
│ 5. PATTERN METADATA GENERATION (GeneratePatterns)               │
│                                                                 │
│  For each cluster:                                              │
│    • Primary error type                                         │
│    • Average confidence                                         │
│    • Average intra-cluster similarity                           │
│    • Common components/files                                    │
│    • Common root causes                                         │
│    • Time range & frequency                                     │
│    • Sample failure IDs                                         │
└─────────────────────────────────────────────────────────────────┘
          │
          
┌─────────────────────────────────────────────────────────────────┐
│ 6. FILTERING & SORTING                                          │
│    • Remove clusters < minClusterSize (2)                       │
│    • Sort by failure count descending                           │
└─────────────────────────────────────────────────────────────────┘
          │
          
┌─────────────────────────────────────────────────────────────────┐
│ 7. RETURN RESULT                                                │
│    PatternDetectionResult with 3 patterns                       │
└─────────────────────────────────────────────────────────────────┘
```

### Before vs After

**Before Pattern Detection:**
```
20 failures scattered everywhere, all look different

Failure 1: NullRef...
Failure 2: NullRef...
Failure 3: Missing brace...
Failure 4: NullRef...
... (hard to see patterns!)
```

**After Pattern Detection:**
```
3 Clear Patterns:

┌─────────────────────────┐
│ Pattern 1 (8 failures)  │ ← FIX THIS FIRST!
│ NullReferenceException  │
│ PaymentService.cs       │
│ 0.42 failures/day       │
└─────────────────────────┘

┌─────────────────────────┐
│ Pattern 2 (6 failures)  │
│ Missing closing brace   │
│ IClaudeService.cs       │
│ 0.32 failures/day       │
└─────────────────────────┘

┌─────────────────────────┐
│ Pattern 3 (6 failures)  │
│ Test timeouts           │
│ IntegrationTests.cs     │
│ 0.33 failures/day       │
└─────────────────────────┘
```

**Action Plan:**
1. Fix PaymentService initialization → Eliminates 8 failures
2. Review code for missing braces → Eliminates 6 failures
3. Investigate test timeout issue → Eliminates 6 failures

**Result:** 20 failures → 3 root causes → Clear priority!

---

## Testing Guide

### Prerequisites

You need at least 5-10 failures in the database with embeddings. Generate test data:

```bash
# Create several test failures
POST /api/pipeline/analyze
{
  "logContent": "Error: NullReferenceException at line 42"
}

POST /api/pipeline/analyze
{
  "logContent": "Compilation failed: missing closing bracket"
}

POST /api/pipeline/analyze
{
  "logContent": "Test failed: timeout after 30 seconds"
}
```

Run this 5-10 times with different variations to build up data.

---

### Test 1: Auto-detect Optimal K

**Request:**
```json
POST /api/pipeline/patterns/detect
{
  "lookbackDays": 30,
  "minClusterSize": 2
}
```

**Expected Response:**
```json
{
  "patterns": [
    {
      "id": 0,
      "name": "Pattern 1: CompilationError",
      "failureCount": 8,
      "primaryErrorType": "CompilationError",
      "averageConfidence": 0.92,
      "averageSimilarity": 0.85,
      "frequency": 0.42
    }
  ],
  "totalFailures": 15,
  "clusterCount": 3,
  "executionTimeMs": 187
}
```

---

### Test 2: Specify Cluster Count

**Request:**
```json
POST /api/pipeline/patterns/detect
{
  "clusterCount": 5,
  "lookbackDays": 30,
  "minClusterSize": 2,
  "randomSeed": 42
}
```

**What happens:** Forces 5 clusters instead of auto-detecting

---

### Test 3: Filter by Error Type

**Request:**
```json
POST /api/pipeline/patterns/detect
{
  "lookbackDays": 30,
  "errorType": "CompilationError",
  "minClusterSize": 1
}
```

**What happens:** Only analyzes CompilationError failures

---

### Test 4: Different Time Ranges

**Request:**
```json
POST /api/pipeline/patterns/detect
{
  "lookbackDays": 7,
  "minClusterSize": 2
}
```

**What happens:** Only looks at last 7 days of failures

---

## Key Insights

### What You Learned

**Before:** 20 random failures, all look different, hard to prioritize

**After:**
- Pattern 1: 8 NullRef in PaymentService (0.42/day) ← Fix this first!
- Pattern 2: 6 Missing braces (0.32/day)
- Pattern 3: 6 Test timeouts (0.33/day)

### Performance

- 50 failures: ~200ms
- 500 failures: ~2s
- 5000 failures: ~20s

### Limitations

- Requires at least 2 failures with embeddings
- K-means assumes spherical clusters (not always true in real data)
- Sensitive to outliers (handled by minClusterSize filter)

---

## Related Documentation

- [Phase 3 Completion Report](../tracking/Phase3_Completion_Report.md)
- [Similarity Search Guide](./Similarity_Search_Guide.md)
- [API Testing Guide](./API_Testing_Guide.md)
- [Usage Examples](./Usage_Examples.md)

---

**Last Updated:** 2026-04-21
**Maintained By:** Development Team
**Status:** Production Ready
