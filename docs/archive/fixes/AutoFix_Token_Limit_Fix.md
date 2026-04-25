# Auto-Fix Token Limit Issue - Resolution

**Date:** 2026-04-25
**Issue:** Auto-fix generation failing with JSON parsing error due to response truncation
**Status:**  Fixed

---

## Problem

The auto-fix feature was failing with the following error:

```
System.Text.Json.JsonException: Expected end of string, but instead reached end of data.
Path: $.fileChanges[0].newContent | LineNumber: 10 | BytePositionInLine: 5612.
```

### Root Cause

1. **Low token limit**: `MaxTokens` was set to 2048 in appsettings.json
2. **Inefficient format**: The prompt asked Claude to return "Full new content of the file" in JSON
3. **Truncation**: Claude's response got cut off mid-string when hitting the 2048 token limit
4. **Invalid JSON**: The truncated response couldn't be parsed as valid JSON

---

## Solution

Implemented a three-part fix:

### 1. Increased MaxTokens (Quick Fix)

**File:** `src/AgenticPipeline.Api/appsettings.json`

```json
"Claude": {
    "MaxTokens": 8192  // Changed from 2048
}
```

This gives Claude 4x more room to generate responses.

### 2. Added Line-Based Changes (Efficiency Improvement)

**File:** `src/AgenticPipeline.Core/Models/AutoFixResult.cs`

Added new `LineChange` model and `LineChanges` property to `FileChange`:

```csharp
public class LineChange
{
    public int LineNumber { get; set; }
    public string? OriginalLine { get; set; }
    public string? NewLine { get; set; }
    public string ChangeType { get; set; } = "replace"; // "replace", "insert", "delete"
}

public class FileChange
{
    public string? NewContent { get; set; }  // For small files
    public List<LineChange>? LineChanges { get; set; }  // For large files (efficient)
    // ...
}
```

### 3. Updated Prompt and Application Logic

**File:** `src/AgenticPipeline.Infrastructure/Services/AutoFixService.cs`

**Changes:**
- Updated prompt to prefer `lineChanges` over `newContent` for efficiency
- Added `ApplyLineChanges()` method to apply line-based modifications
- Enhanced error handling to detect and report truncation issues
- Added detailed logging for diagnostics

**Example prompt format:**
```json
{
  "fileChanges": [
    {
      "filePath": "Customer.API.Tests/BasicTests.cs",
      "lineChanges": [
        {
          "lineNumber": 23,
          "originalLine": "Assert.Equal(20, customers.Count());",
          "newLine": "Assert.Equal(21, customers.Count());",
          "changeType": "replace"
        }
      ]
    }
  ]
}
```

---

## Benefits

### Token Efficiency
- **Before**: Full file content = 5000+ tokens
- **After**: Line-based changes = 50-200 tokens
- **Savings**: 95%+ reduction for typical fixes

### Scalability
- Can now handle files with thousands of lines
- Multiple file changes in single response
- More reliable JSON parsing

### Better Error Handling
- Detects truncation early with clear error messages
- Provides actionable suggestions (use lineChanges, increase MaxTokens)
- Enhanced logging for diagnostics

---

## Testing Steps

1. **Stop the API** (it's currently running)
2. **Rebuild the project:**
   ```powershell
   cd C:\Anish\Source\Agentic_Pipeline
   dotnet build
   ```
3. **Restart the API:**
   ```powershell
   dotnet run --project src/AgenticPipeline.Api
   ```
4. **Trigger a test build failure** in Azure DevOps
5. **Verify auto-fix generation** in the logs

---

## Expected Behavior

### Success Case
```
info: Auto-fix generation completed: FixGenerated=True, IsSafeToApply=True
info: Applied 1 line changes to Customer.API.Tests/BasicTests.cs
info: Created PR #123: [Auto-Fix] TestFailure: Expected count was incorrect
```

### If Still Fails
Check logs for:
- `Response was truncated` → Increase MaxTokens further (try 16384)
- `No valid JSON found` → Check Claude's response format
- `Line number out of range` → Verify file hasn't changed since analysis

---

## Configuration Reference

### appsettings.json
```json
"Claude": {
    "MaxTokens": 8192,  // Recommended: 8192-16384 for auto-fix
    "Temperature": 0.7
}
```

### When to Use Each Format

| Scenario | Use | Reason |
|----------|-----|--------|
| Small files (< 50 lines) | `newContent` | Simple, clear |
| Large files | `lineChanges` | Token efficient |
| Creating new files | `newContent` | No existing content to diff |
| Multiple changes | `lineChanges` | More precise |

---

## Files Modified

1.  `src/AgenticPipeline.Api/appsettings.json` - Increased MaxTokens to 8192
2.  `src/AgenticPipeline.Core/Models/AutoFixResult.cs` - Added LineChange model
3.  `src/AgenticPipeline.Infrastructure/Services/AutoFixService.cs` - Updated prompt and logic

---

## Rollback Plan

If issues occur, revert by:

1. Change MaxTokens back to 2048
2. Use this command:
   ```bash
   git checkout HEAD -- src/AgenticPipeline.Api/appsettings.json
   ```

---

## Next Steps

1. Test with real build failure
2. Monitor token usage and adjust MaxTokens if needed
3. Consider adding response streaming for very large fixes
4. Add unit tests for ApplyLineChanges method

---

**Status:** Ready for testing
**Risk:** Low - backwards compatible, fallback to newContent if lineChanges not provided
