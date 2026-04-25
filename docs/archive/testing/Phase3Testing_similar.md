  Overview: Similarity Search API

  Endpoint: POST /api/pipeline/search/similar

  What it does: Uses AI embeddings (512-dimensional vectors) to find failures similar to:
  1. An existing failure in your database (by ID)
  2. New failure text you provide

  Step-by-Step Testing Guide

  Prerequisites: Create Test Data First

  Before testing similarity search, you need some failures in the database. Run these 3 times with different errors:

  Test Failure 1 - NullReferenceException:
  POST /api/pipeline/analyze
  {
    "logContent": "System.NullReferenceException: Object reference not set to an instance of an object. at PaymentService.ProcessPayment(Order order) in
  PaymentService.cs:line 42"
  }

  Test Failure 2 - Another NullReferenceException (similar):
  POST /api/pipeline/analyze
  {
    "logContent": "NullReferenceException in UserService.GetProfile method. The user object was null at line 89"
  }

  Test Failure 3 - CompilationError (different):
  POST /api/pipeline/analyze
  {
    "logContent": "error CS0246: The type or namespace name 'JsonSerializer' could not be found. Missing using directive in DataProcessor.cs line 15"
  }

  Now check you have data:
  GET /api/pipeline/history?limit=10

  You should see at least 3 failures with IDs (1, 2, 3, etc.).

  ---
  Method 1: Search by Failure ID

  Find failures similar to an existing failure in your database.

  Example Request:

  POST /api/pipeline/search/similar
  {
    "failureId": 1,
    "topN": 5,
    "minSimilarity": 0.5
  }

  Parameters Explained:

  - failureId: The ID of an existing failure (get from /history)
  - topN: How many similar results to return (max 100)
  - minSimilarity: Minimum similarity score (0.0 to 1.0)
    - 0.9 = Very similar (90%+)
    - 0.7 = Somewhat similar (70%+)
    - 0.5 = Moderately similar (50%+)
    - 0.3 = Loosely similar (30%+)

  Expected Response:

  {
    "query": {
      "queryType": "failure_id",
      "failureId": 1,
      "errorType": "NullReferenceException",
      "minSimilarity": 0.5,
      "topN": 5
    },
    "results": [
      {
        "id": 2,
        "errorType": "NullReferenceException",
        "rootCause": "Null reference in UserService",
        "similarityScore": 0.87,
        "confidenceScore": 0.95,
        "analyzedAt": "2026-04-23T04:15:00Z"
      },
      {
        "id": 3,
        "errorType": "CompilationError",
        "rootCause": "Missing using directive",
        "similarityScore": 0.42,
        "confidenceScore": 0.88,
        "analyzedAt": "2026-04-23T04:16:00Z"
      }
    ],
    "resultCount": 2,
    "totalCandidates": 3,
    "executionTimeMs": 45
  }

  Note: Results are sorted by similarity score (highest first).

  ---
  Method 2: Search by Failure Text

  Find failures similar to new text (not yet in database).

  Example Request:

  POST /api/pipeline/search/similar
  {
    "failureText": "NullReferenceException when accessing customer.Address property",
    "topN": 5,
    "minSimilarity": 0.6
  }

  Parameters Explained:

  - failureText: New error text to search for
  - topN: Number of results
  - minSimilarity: Similarity threshold

  This generates an embedding on-the-fly and searches your database.

  Use Case:

  "I'm seeing a new error. Has anything similar happened before?"

  ---
  Advanced Options

  Filter by Error Type:

  Only search within specific error types:

  POST /api/pipeline/search/similar
  {
    "failureId": 1,
    "topN": 10,
    "minSimilarity": 0.5,
    "errorType": "NullReferenceException"
  }

  Filter by Time Range:

  Only search recent failures:

  POST /api/pipeline/search/similar
  {
    "failureText": "Connection timeout in database",
    "topN": 10,
    "minSimilarity": 0.5,
    "lookbackDays": 30
  }

  Only searches failures from the last 30 days.

  ---
  Testing Scenarios in Swagger

  Scenario 1: Find Similar Past Failures

  1. Get a failure ID from /history:
  GET /api/pipeline/history?limit=10

  2. Pick an ID (e.g., 1)
  3. Search for similar:
  POST /api/pipeline/search/similar
  {
    "failureId": 1,
    "topN": 5,
    "minSimilarity": 0.5
  }

  Scenario 2: Check New Error Against History

  POST /api/pipeline/search/similar
  {
    "failureText": "OutOfMemoryException in data processing pipeline",
    "topN": 5,
    "minSimilarity": 0.4
  }

  Scenario 3: Find Highly Similar Only

  POST /api/pipeline/search/similar
  {
    "failureId": 2,
    "topN": 10,
    "minSimilarity": 0.8
  }

  Only returns failures that are 80%+ similar.

  ---
  Understanding Similarity Scores

  - 0.95 - 1.0: Nearly identical errors (same root cause)
  - 0.8 - 0.95: Very similar (same error type, similar context)
  - 0.6 - 0.8: Somewhat similar (related issues)
  - 0.4 - 0.6: Loosely related
  - < 0.4: Probably not related

  ---
  Troubleshooting

  Error: "No Embedding"

  {
    "title": "No Embedding",
    "detail": "Failure X does not have an embedding"
  }

  Cause: The failure was analyzed before embeddings were enabled.

  Fix: Re-analyze the failure or use a different failure ID.

  Empty Results

  {
    "results": [],
    "resultCount": 0
  }

  Possible causes:
  1. MinSimilarity is too high (try lowering to 0.3)
  2. No other failures in database
  3. ErrorType filter is too restrictive

  Error: "Either FailureId or FailureText must be provided"

  You must provide one of:
  - failureId (search by existing failure)
  - failureText (search by new text)

  Not both, not neither.

  ---
  Real-World Use Cases

  1. Duplicate Detection

  {
    "failureId": 15,
    "topN": 3,
    "minSimilarity": 0.9
  }
  Find if this failure has happened before (90%+ similar).

  2. Pattern Analysis

  {
    "failureText": "Timeout connecting to payment gateway",
    "topN": 20,
    "minSimilarity": 0.5,
    "lookbackDays": 7
  }
  Check if payment timeouts are a recurring pattern this week.

  3. Root Cause Research

  {
    "failureId": 42,
    "topN": 10,
    "minSimilarity": 0.6
  }
  Find similar failures to understand root cause patterns.

  ---
  Quick Test Script

  Try this in Swagger:

  Step 1: Create test data
  POST /api/pipeline/analyze
  {
    "logContent": "NullReferenceException at PaymentService line 42"
  }

  Step 2: Create another similar one
  POST /api/pipeline/analyze
  {
    "logContent": "NullReferenceException at UserService line 89"
  }

  Step 3: Search for similarity
  POST /api/pipeline/search/similar
  {
    "failureId": 1,
    "topN": 5,
    "minSimilarity": 0.5
  }

  You should see failure 2 with a high similarity score (0.7-0.9).