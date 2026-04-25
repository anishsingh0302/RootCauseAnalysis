Great question! Let me give you the exact JSON to copy-paste into Swagger UI.

  Prerequisites First

  Before testing Pattern Detection, you need at least 5-10 failures in your database. Let me give you quick test data first:

  ---
  Step 1: Create Test Data (Run 5-10 times)

  Endpoint: POST /api/pipeline/analyze

  Copy these and run them one by one:

  Test Failure 1 (NullReference pattern)

  {
    "logContent": "System.NullReferenceException: Object reference not set to an instance of an object.\n   at PaymentService.ProcessPayment(Order order) in
   PaymentService.cs:line 42\n   at OrderController.Checkout(OrderRequest request) in OrderController.cs:line 88"
  }

  Test Failure 2 (Another NullReference)

  {
    "logContent": "System.NullReferenceException: Object reference not set to an instance of an object.\n   at PaymentService.ValidateCard(CreditCard card)
  in PaymentService.cs:line 67\n   at PaymentController.ProcessTransaction() in PaymentController.cs:line 123"
  }

  Test Failure 3 (Compilation error pattern)

  {
    "logContent": "CSC : error CS1513: } expected [C:\\Projects\\MyApp\\MyApp.csproj]\nCompilation failed.\n   at IClaudeService.cs:line 45\nBuild FAILED."
  }

  Test Failure 4 (Another compilation error)

  {
    "logContent": "CSC : error CS1513: } expected [C:\\Projects\\MyApp\\MyApp.csproj]\nCompilation failed.\n   at IUserService.cs:line 32\nBuild FAILED."
  }

  Test Failure 5 (Test timeout pattern)

  {
    "logContent": "Test IntegrationTests.DatabaseConnectionTest.TestConnection timed out after 30000ms\n   at IntegrationTests.cs:line 156\nTest run failed:
   1 test failed, 0 passed, 0 skipped"
  }

  Test Failure 6 (Another test timeout)

  {
    "logContent": "Test IntegrationTests.ApiTests.TestEndpoint timed out after 30000ms\n   at ApiTests.cs:line 89\nTest run failed: 1 test failed, 0 passed,
   0 skipped"
  }

  Test Failure 7 (Third NullReference)

  {
    "logContent": "System.NullReferenceException: Object reference not set to an instance of an object.\n   at PaymentService.ProcessPayment(Order order) in
   PaymentService.cs:line 42\n   at CheckoutService.FinalizeOrder() in CheckoutService.cs:line 203"
  }

  Test Failure 8 (Third compilation error)

  {
    "logContent": "CSC : error CS1513: } expected [C:\\Projects\\MyApp\\MyApp.csproj]\nCompilation failed.\n   at IClaudeService.cs:line 45\nBuild FAILED."
  }

  Action: Run each of these through /api/pipeline/analyze - this creates 8 failures with embeddings.

  ---
  Step 2: Now Test Pattern Detection

  Option 1: Basic Auto-Detect (Simplest)

  Endpoint: POST /api/pipeline/patterns/detect

  Copy this into Swagger Request Body:
  {
    "lookbackDays": 30,
    "minClusterSize": 2
  }

  What this does:
  - Looks at last 30 days of failures
  - Automatically determines optimal number of patterns
  - Only shows patterns with at least 2 failures

  Expected Result: 3 patterns (NullRef, Compilation, Test Timeout)

  ---
  Option 2: Specify Number of Patterns

  Copy this:
  {
    "clusterCount": 3,
    "lookbackDays": 30,
    "minClusterSize": 2
  }

  What this does:
  - Forces exactly 3 patterns/clusters
  - Good when you know how many categories you expect

  ---
  Option 3: Filter by Error Type

  Copy this:
  {
    "lookbackDays": 30,
    "errorType": "CompilationError",
    "minClusterSize": 1
  }

  What this does:
  - Only analyzes CompilationError failures
  - Useful for focusing on specific problem types

  ---
  Option 4: Last 7 Days Only

  Copy this:
  {
    "lookbackDays": 7,
    "minClusterSize": 2
  }

  What this does:
  - Only looks at recent failures (last week)
  - Good for current/active issues

  ---
  Option 5: Full Control (Advanced)

  Copy this:
  {
    "clusterCount": 4,
    "lookbackDays": 90,
    "minClusterSize": 3,
    "maxIterations": 100,
    "randomSeed": 42
  }

  What each field means:
  - clusterCount: Exact number of patterns to find (optional, auto-detects if omitted)
  - lookbackDays: How many days back to search (1-365)
  - minClusterSize: Minimum failures per pattern (filters out tiny patterns)
  - maxIterations: Max K-means iterations (default 100)
  - randomSeed: For reproducible results (same seed = same results)

  ---
  Complete Swagger UI Instructions

  1. Open Swagger

  Navigate to: https://localhost:7001/swagger (or your configured port)

  2. Expand the Pattern Detection Endpoint

  Scroll to: POST /api/pipeline/patterns/detect
  Click to expand

  3. Click "Try it out"

  On the right side, click the blue "Try it out" button

  4. Paste JSON

  In the Request body text area, paste one of the JSON examples above

  5. Click "Execute"

  Click the blue "Execute" button

  6. View Results

  Scroll down to see the response

  ---
  Expected Response Example

  If you have 8 test failures, you should see something like this:

  {
    "patterns": [
      {
        "id": 0,
        "name": "Pattern 1: NullReferenceException",
        "description": "Cluster of 3 similar NullReferenceException failures",
        "primaryErrorType": "NullReferenceException",
        "failureCount": 3,
        "averageConfidence": 0.91,
        "averageSimilarity": 0.89,
        "firstOccurrence": "2026-04-21T10:15:00Z",
        "lastOccurrence": "2026-04-21T14:30:00Z",
        "frequency": 1.5,
        "sampleFailureIds": [1, 2, 7],
        "commonRootCauses": [
          "Uninitialized payment service object"
        ],
        "commonComponents": [
          "PaymentService.cs",
          "OrderController.cs",
          "PaymentController.cs"
        ]
      },
      {
        "id": 1,
        "name": "Pattern 2: CompilationError",
        "description": "Cluster of 3 similar CompilationError failures",
        "primaryErrorType": "CompilationError",
        "failureCount": 3,
        "averageConfidence": 0.94,
        "averageSimilarity": 0.92,
        "firstOccurrence": "2026-04-21T10:20:00Z",
        "lastOccurrence": "2026-04-21T14:35:00Z",
        "frequency": 1.5,
        "sampleFailureIds": [3, 4, 8],
        "commonRootCauses": [
          "Missing closing brace"
        ],
        "commonComponents": [
          "IClaudeService.cs",
          "IUserService.cs"
        ]
      },
      {
        "id": 2,
        "name": "Pattern 3: TestFailure",
        "description": "Cluster of 2 similar TestFailure failures",
        "primaryErrorType": "TestFailure",
        "failureCount": 2,
        "averageConfidence": 0.87,
        "averageSimilarity": 0.88,
        "firstOccurrence": "2026-04-21T10:25:00Z",
        "lastOccurrence": "2026-04-21T14:40:00Z",
        "frequency": 1.0,
        "sampleFailureIds": [5, 6],
        "commonRootCauses": [
          "Test timeout after 30 seconds"
        ],
        "commonComponents": [
          "IntegrationTests.cs",
          "ApiTests.cs"
        ]
      }
    ],
    "totalFailures": 8,
    "clusterCount": 3,
    "averageClusterSize": 2.67,
    "executionTimeMs": 187,
    "parameters": {
      "clusterCount": 3,
      "maxIterations": 100,
      "convergenceThreshold": 0.001,
      "minClusterSize": 2,
      "randomSeed": null
    }
  }

  ---
  Quick Test Checklist

   Step 1: Create 8 test failures using /api/pipeline/analyze
   Step 2: Verify data exists using GET /api/pipeline/history
   Step 3: Test Pattern Detection with basic request:
  {
    "lookbackDays": 30,
    "minClusterSize": 2
  }
   Step 4: Check response shows 3 patterns
   Step 5: Try different parameters to explore features

  ---
  Troubleshooting

  "No patterns found" or empty response

  Problem: Not enough data in database

  Solution:
  # Check how many failures you have
  GET /api/pipeline/history?limit=50

  If less than 5 failures, create more test data.

  ---
  "Fewer failures than requested clusters"

  Problem: You specified clusterCount: 10 but only have 5 failures

  Solution: Remove clusterCount to auto-detect, or reduce the number:
  {
    "lookbackDays": 30,
    "minClusterSize": 2
  }

  ---
  "Invalid LookbackDays"

  Problem: lookbackDays must be between 1 and 365

  Solution:
  {
    "lookbackDays": 30,  // Valid: 1-365
    "minClusterSize": 2
  }

  ---
  Quick Copy-Paste Summary

  Simplest Test (Start Here):
  {
    "lookbackDays": 30,
    "minClusterSize": 2
  }

  With Specific Cluster Count:
  {
    "clusterCount": 3,
    "lookbackDays": 30,
    "minClusterSize": 2
  }

  Filter by Error Type:
  {
    "lookbackDays": 30,
    "errorType": "CompilationError",
    "minClusterSize": 1
  }

  That's it! Start with the simplest test and work your way up. Let me know what results you get!