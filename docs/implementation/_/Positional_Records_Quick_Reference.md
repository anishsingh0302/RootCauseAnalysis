# Positional Records - Quick Reference

**Microsoft Docs:** [Use record types tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/tutorials/records)

---

## What Are Positional Records?

Positional records use **primary constructors** with a concise syntax. The compiler automatically generates:
- Public properties from constructor parameters
- Value-based equality (`Equals`, `GetHashCode`, `==`, `!=`)
- `ToString()` implementation
- `Deconstruct` method
- Copy constructor for `with` expressions

---

## Before vs After Examples

### ClaudeSettings

**Before (Verbose):**
```csharp
public record ClaudeSettings
{
    public string ModelId { get; init; } = "us.anthropic.claude-sonnet-4-6";
    public string Region { get; init; } = "us-west-2";
    public int MaxTokens { get; init; } = 2048;
    public double Temperature { get; init; } = 0.7;
}
```

**After (Positional):**
```csharp
public record ClaudeSettings(
    string ModelId,
    string Region,
    int MaxTokens,
    double Temperature)
{
    public ClaudeSettings() : this(
        "us.anthropic.claude-sonnet-4-6",
        "us-west-2",
        2048,
        0.7)
    { }
}
```

---

### ClaudeRequest & ClaudeResponse

**Before (Verbose):**
```csharp
public record ClaudeRequest
{
    public string Message { get; init; } = string.Empty;
    public int MaxTokens { get; init; } = 2048;
    public double Temperature { get; init; } = 0.7;
    public string? SystemPrompt { get; init; }
}

public record ClaudeResponse
{
    public string Content { get; init; } = string.Empty;
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public int TotalTokens => InputTokens + OutputTokens;
}
```

**After (Positional):**
```csharp
public record ClaudeRequest(
    string Message,
    int MaxTokens,
    double Temperature,
    string? SystemPrompt = null);

public record ClaudeResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    string ModelId)
{
    public int TotalTokens => InputTokens + OutputTokens;
}
```

---

### TokenUsage

**Before (Verbose):**
```csharp
public record TokenUsage
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
}
```

**After (Positional):**
```csharp
public record TokenUsage(int InputTokens, int OutputTokens)
{
    public int TotalTokens => InputTokens + OutputTokens;
}
```

---

### RcaJsonResponse

**Before (Private Class):**
```csharp
// In RcaPlugin.cs
private class RcaJsonResponse
{
    public string RootCause { get; set; } = string.Empty;
    public string DetailedAnalysis { get; set; } = string.Empty;
    public List<string>? AffectedComponents { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<string>? Recommendations { get; set; }
}
```

**After (Public Positional Record):**
```csharp
// In Core.Models/RcaJsonResponse.cs
public record RcaJsonResponse(
    string RootCause,
    string DetailedAnalysis,
    List<string>? AffectedComponents,
    string ErrorType,
    double ConfidenceScore,
    List<string>? Recommendations);
```

---

### RcaResult

**Before (Mutable Class):**
```csharp
public class RcaResult
{
    public string RootCause { get; set; } = string.Empty;
    public string DetailedAnalysis { get; set; } = string.Empty;
    public List<string> AffectedComponents { get; set; } = new();
    public string ErrorType { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public TokenUsage TokenUsage { get; set; } = new(0, 0);
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
```

**After (Immutable Positional Record):**
```csharp
public record RcaResult(
    string RootCause,
    string DetailedAnalysis,
    List<string> AffectedComponents,
    string ErrorType,
    double ConfidenceScore,
    List<string> Recommendations,
    TokenUsage TokenUsage,
    DateTime AnalyzedAt);
```

---

## Usage Examples

### Creating Instances

**Named Parameters (Recommended):**
```csharp
var request = new ClaudeRequest(
    Message: "Analyze this log",
    MaxTokens: 2048,
    Temperature: 0.3,
    SystemPrompt: "You are an expert");
```

**Positional Parameters:**
```csharp
var request = new ClaudeRequest("message", 2048, 0.3, "system");
```

**With Optional Parameter:**
```csharp
var request = new ClaudeRequest("message", 2048, 0.3); // SystemPrompt is null
```

### Creating Complex Records

```csharp
// RcaResult with all parameters
var result = new RcaResult(
    RootCause: "Missing semicolon on line 42",
    DetailedAnalysis: "The compilation failed due to a syntax error...",
    AffectedComponents: new List<string> { "Program.cs", "Controller.cs" },
    ErrorType: "CompilationError",
    ConfidenceScore: 0.95,
    Recommendations: new List<string>
    {
        "Step 1: Add semicolon at line 42",
        "Step 2: Rebuild the solution"
    },
    TokenUsage: new TokenUsage(245, 180),
    AnalyzedAt: DateTime.UtcNow);
```

### Using `with` Expressions

```csharp
var settings = new ClaudeSettings(
    ModelId: "us.anthropic.claude-sonnet-4-6",
    Region: "us-west-2",
    MaxTokens: 2048,
    Temperature: 0.7);

// Create a copy with modified properties
var devSettings = settings with { Temperature = 0.9 };
var prodSettings = settings with { Region = "us-east-1", MaxTokens = 4096 };

// Original is unchanged
Console.WriteLine(settings.Temperature); // 0.7
Console.WriteLine(devSettings.Temperature); // 0.9

// Complex record with 'with' expression
var updatedResult = result with
{
    ConfidenceScore = 0.98,
    AnalyzedAt = DateTime.UtcNow
};
```

### Deconstruction

```csharp
var response = new ClaudeResponse("content", 100, 200, "model-id");

// Deconstruct all properties
var (content, inputTokens, outputTokens, modelId) = response;

// Use deconstructed values
Console.WriteLine($"Model: {modelId}, Tokens: {inputTokens + outputTokens}");
```

### Value Equality

```csharp
var usage1 = new TokenUsage(100, 200);
var usage2 = new TokenUsage(100, 200);
var usage3 = new TokenUsage(150, 250);

Console.WriteLine(usage1 == usage2); // True (same values)
Console.WriteLine(usage1 == usage3); // False (different values)
```

### ToString() Auto-Generated

```csharp
var settings = new ClaudeSettings(
    ModelId: "us.anthropic.claude-sonnet-4-6",
    Region: "us-west-2",
    MaxTokens: 2048,
    Temperature: 0.7);

Console.WriteLine(settings);
// Output: ClaudeSettings { ModelId = us.anthropic.claude-sonnet-4-6, Region = us-west-2, MaxTokens = 2048, Temperature = 0.7 }
```

---

## Key Differences

| Feature | Before (Property Syntax) | After (Positional) |
|---------|-------------------------|-------------------|
| **Lines of Code** | 5-10 lines per record | 1-5 lines per record |
| **Constructor** | Manual or object initializer | Primary constructor auto-generated |
| **Properties** | Explicit `{ get; init; }` | Auto-generated from parameters |
| **XML Docs** | On properties | On parameters |
| **Initialization** | `new() { Prop = value }` | `new(value)` or `new(Prop: value)` |
| **Deconstruction** | Manual implementation | Auto-generated |

---

## Benefits

1. **Less Boilerplate** - 50-70% less code
2. **Primary Constructor** - Parameters become properties automatically
3. **Immutable by Default** - Init-only properties for `record class`
4. **Value Semantics** - Equality based on property values
5. **Built-in ToString()** - Human-readable representation
6. **Deconstruction** - Extract values easily
7. **`with` Expressions** - Non-destructive mutation
8. **Compiler Optimized** - Better performance

---

## When to Use Positional Records

✅ **Use for:**
- DTOs (Data Transfer Objects)
- Configuration models
- API request/response models
- Immutable value objects
- Simple data containers

❌ **Don't use for:**
- Complex objects with many properties (>6-8)
- Objects with complex validation logic
- Objects with mutable state requirements
- Entities with behavior-heavy logic

---

## Migration Pattern

**Step 1:** Convert property syntax to positional parameters
```csharp
// Before
public record Person
{
    public string FirstName { get; init; }
    public string LastName { get; init; }
}

// After
public record Person(string FirstName, string LastName);
```

**Step 2:** Update instantiation code
```csharp
// Before
var person = new Person { FirstName = "John", LastName = "Doe" };

// After (named parameters)
var person = new Person(FirstName: "John", LastName: "Doe");
```

**Step 3:** Keep computed properties in body
```csharp
public record Person(string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}";
}
```

---

**Reference:** [Microsoft - Use record types tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/tutorials/records)
