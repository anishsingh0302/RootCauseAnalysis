# AnalysisRequest - Record Conversion

**Date:** 2026-04-01
**Status:**  Complete

---

## Overview

Converted `AnalysisRequest` from a mutable class to an **immutable positional record** with default parameters, following Microsoft's recommended pattern.

---

## Before (Mutable Class)

```csharp
namespace AgenticPipeline.Api.Models;

/// <summary>
/// Request model for pipeline failure analysis
/// </summary>
public class AnalysisRequest
{
    /// <summary>
    /// The CI/CD pipeline failure log content to analyze (optional if LogFilePath is provided)
    /// </summary>
    public string? LogContent { get; set; }

    /// <summary>
    /// Path to the log file to analyze (optional if LogContent is provided)
    /// </summary>
    public string? LogFilePath { get; set; }
}
```

**Issues:**
- Mutable (`set` properties)
- Verbose (12 lines)
- Can be modified after creation

---

## After (Immutable Positional Record)

```csharp
namespace AgenticPipeline.Api.Models;

/// <summary>
/// Request model for pipeline failure analysis
/// </summary>
/// <param name="LogContent">The CI/CD pipeline failure log content to analyze (optional if LogFilePath is provided)</param>
/// <param name="LogFilePath">Path to the log file to analyze (optional if LogContent is provided)</param>
public record AnalysisRequest(
    string? LogContent = null,
    string? LogFilePath = null);
```

**Benefits:**
- Immutable (init-only properties)
- Concise (3 lines)
- Default parameters (both optional)
- Value-based equality
- Built-in ToString()

---

## Usage Examples

### Creating Instances

#### **With Log Content Only:**
```csharp
var request1 = new AnalysisRequest(LogContent: "log content here");
var request2 = new AnalysisRequest("log content here"); // Positional
```

#### **With File Path Only:**
```csharp
var request1 = new AnalysisRequest(LogFilePath: "<PROJECT_ROOT>\\SampleData\\log.txt");
var request2 = new AnalysisRequest(null, "<PROJECT_ROOT>\\SampleData\\log.txt"); // Positional
```

#### **With Both:**
```csharp
var request = new AnalysisRequest(
    LogContent: "direct content",
    LogFilePath: "<PROJECT_ROOT>\\SampleData\\file.log");
```

#### **Empty Request (Both null):**
```csharp
var request = new AnalysisRequest(); // Both default to null
```

---

## JSON Deserialization (ASP.NET Core)

### **Request with File Path:**
```json
{
  "logFilePath": "<PROJECT_ROOT>\\SampleData\\build-failure-compilation.log"
}
```

Deserializes to:
```csharp
// LogContent = null
// LogFilePath = "<PROJECT_ROOT>\\SampleData\\build-failure-compilation.log"
```

### **Request with Content:**
```json
{
  "logContent": "[2026-04-01 10:15:23] BUILD STARTED..."
}
```

Deserializes to:
```csharp
// LogContent = "[2026-04-01 10:15:23] BUILD STARTED..."
// LogFilePath = null
```

### **Request with Both:**
```json
{
  "logContent": "some content",
  "logFilePath": "<PROJECT_ROOT>\\SampleData\\file.log"
}
```

Deserializes to:
```csharp
// LogContent = "some content"
// LogFilePath = "<PROJECT_ROOT>\\SampleData\\file.log"
```

---

## Controller Usage

The controller remains the same - no changes needed:

```csharp
[HttpPost("analyze")]
public async Task<IActionResult> AnalyzePipeline(
    [FromBody] AnalysisRequest request,
    CancellationToken cancellationToken)
{
    // Access properties normally
    if (!string.IsNullOrWhiteSpace(request.LogFilePath))
    {
        // Read from file
    }
    else if (!string.IsNullOrWhiteSpace(request.LogContent))
    {
        // Use direct content
    }
    // ...
}
```

---

## Testing with Swagger UI

### **Test 1: File Path**
```json
{
  "logFilePath": "<PROJECT_ROOT>\\SampleData\\build-failure-compilation.log"
}
```

### **Test 2: Direct Content**
```json
{
  "logContent": "[2026-04-01] BUILD FAILED..."
}
```

### **Test 3: Both (File path takes priority in controller)**
```json
{
  "logContent": "fallback content",
  "logFilePath": "<PROJECT_ROOT>\\SampleData\\file.log"
}
```

---

## Record Features

### **Value Equality:**
```csharp
var req1 = new AnalysisRequest(LogContent: "test");
var req2 = new AnalysisRequest(LogContent: "test");

Console.WriteLine(req1 == req2); // True (same values)
```

### **ToString():**
```csharp
var request = new AnalysisRequest(
    LogContent: "log data",
    LogFilePath: "path.log");

Console.WriteLine(request);
// Output: AnalysisRequest { LogContent = log data, LogFilePath = path.log }
```

### **Deconstruction:**
```csharp
var request = new AnalysisRequest("content", "path");
var (content, path) = request;

Console.WriteLine($"Content: {content}, Path: {path}");
```

### **With Expression:**
```csharp
var request1 = new AnalysisRequest(LogContent: "original");
var request2 = request1 with { LogContent = "modified" };

Console.WriteLine(request1.LogContent); // "original" (unchanged)
Console.WriteLine(request2.LogContent); // "modified"
```

---

## Benefits Summary

| Aspect | Before (Class) | After (Record) | Improvement |
|--------|---------------|----------------|-------------|
| **Lines of Code** | 12 | 3 | 75% reduction |
| **Mutability** | Mutable | Immutable |  Safer |
| **Value Equality** | Reference | Value-based |  Better |
| **ToString()** | Default | Auto-generated |  Readable |
| **Deconstruction** | Manual | Auto-generated |  Convenient |
| **Pattern** | Verbose | Microsoft recommended |  Modern |

---

## Compatibility

###  **Fully Backward Compatible**

All existing API calls work exactly the same:
- JSON deserialization 
- Model binding 
- Swagger UI 
- Property access 

### **No Breaking Changes**

The API contract remains identical - clients don't need to change.

---

## All Models Now Records

With this change, **ALL models** in the solution are now immutable positional records:

### **Core.Models:**
1.  `ClaudeSettings` - Configuration record
2.  `ClaudeRequest` - API request record
3.  `ClaudeResponse` - API response record
4.  `RcaJsonResponse` - Intermediate deserialization record
5.  `RcaResult` - Final result record
6.  `TokenUsage` - Nested token tracking record

### **Api.Models:**
7.  `AnalysisRequest` - API request record ← **NEW!**

---

## Build Status

```
 Build Succeeded
   0 Warnings
   0 Error(s)
```

---

## Summary

`AnalysisRequest` is now:
-  Immutable positional record
-  75% less code
-  Value-based equality
-  Default parameters for both fields
-  Fully backward compatible
-  Following Microsoft's recommended pattern

**All models in the solution are now consistent, immutable records!** 

---

**Version:** 1.0.0
**Last Updated:** 2026-04-01
**Status:**  Complete
