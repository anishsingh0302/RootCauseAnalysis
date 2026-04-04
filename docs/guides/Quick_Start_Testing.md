# Quick Start - Testing Flow

**Simple step-by-step guide to test the Agentic Pipeline Troubleshooter**

---

## 🚀 Quick Testing Steps (5 Minutes)

### Step 1: Set AWS Credentials (1 minute)

```powershell
cd <PROJECT_ROOT>
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YOUR_AWS_PROFILE" -Region "us-west-2"
```

✅ **What this does:** Extracts AWS credentials from SSO and sets them as environment variables

---

### Step 2: Start the API (1 minute)

**Option A - Command Line:**
```powershell
cd src\AgenticPipeline.Api
dotnet run
```

**Option B - Visual Studio:**
- Open `AgenticPipeline.sln`
- Press **F5**

✅ **Look for:**
```
Now listening on: https://localhost:7xxx
```
**Remember this port number!**

---

### Step 3: Open Swagger UI (10 seconds)

Open your browser to: `https://localhost:7xxx/` (use your port from Step 2)

✅ **You should see:** Swagger UI with API documentation

---

### Step 4: Test Health Endpoint (10 seconds)

In Swagger UI:
1. Click on **GET /api/pipeline/health**
2. Click **"Try it out"**
3. Click **"Execute"**

✅ **Expected response:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-04-01...",
  "service": "Agentic Pipeline Troubleshooter"
}
```

---

### Step 5: Prepare Sample Log (30 seconds)

Open this file in Notepad:
```
<PROJECT_ROOT>\SampleData\build-failure-compilation.log
```

**Copy the entire contents** (Ctrl+A, Ctrl+C)

---

### Step 6: Analyze Pipeline Failure (3 minutes)

In Swagger UI:
1. Click on **POST /api/pipeline/analyze**
2. Click **"Try it out"**
3. **Paste** the log content into the `logContent` field
4. Click **"Execute"**
5. **Wait 2-5 seconds** for Claude to analyze

✅ **Expected response:**
```json
{
  "rootCause": "Missing semicolon and syntax errors...",
  "detailedAnalysis": "The code has compilation errors...",
  "affectedComponents": ["PipelineController.cs"],
  "errorType": "CompilationError",
  "confidenceScore": 0.95,
  "recommendations": [
    "Step 1: Add semicolon...",
    "Step 2: Fix variable name...",
    "Step 3: Rebuild solution"
  ],
  "tokenUsage": {
    "inputTokens": 245,
    "outputTokens": 180,
    "totalTokens": 425
  },
  "analyzedAt": "2026-04-01..."
}
```

---

## ✅ Success Indicators

You've successfully tested the complete flow if:

- ✅ Health endpoint returns "Healthy"
- ✅ Analysis endpoint returns 200 OK
- ✅ Response has `rootCause`, `recommendations`, etc.
- ✅ `confidenceScore` is between 0.0-1.0
- ✅ Token usage is shown
- ✅ Recommendations are specific and actionable

---

## 🔄 Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│ YOU (User/Client)                                           │
└────────────┬────────────────────────────────────────────────┘
             │ 1. POST /api/pipeline/analyze
             │    { "logContent": "build log..." }
             ▼
┌─────────────────────────────────────────────────────────────┐
│ AgenticPipeline.Api                                         │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ PipelineController                                      │ │
│ │ • Validates request                                     │ │
│ │ • Logs incoming request                                 │ │
│ └────────────┬────────────────────────────────────────────┘ │
└──────────────┼──────────────────────────────────────────────┘
               │ 2. Call AnalyzeFailureAsync(logContent)
               ▼
┌─────────────────────────────────────────────────────────────┐
│ AgenticPipeline.Infrastructure                              │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ RcaPlugin (Semantic Kernel)                             │ │
│ │ • Builds system prompt                                  │ │
│ │ • Builds user message with log                          │ │
│ │ • Creates ClaudeRequest                                 │ │
│ └────────────┬────────────────────────────────────────────┘ │
│              │ 3. Call InvokeModelAsync(request)            │
│              ▼                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ ClaudeService                                           │ │
│ │ • Formats request for Bedrock API                       │ │
│ │ • Calls AWS Bedrock InvokeModel                         │ │
│ └────────────┬────────────────────────────────────────────┘ │
└──────────────┼──────────────────────────────────────────────┘
               │ 4. HTTPS Request to AWS
               ▼
┌─────────────────────────────────────────────────────────────┐
│ AWS Bedrock (us-west-2)                                     │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Inference Profile: us.anthropic.claude-sonnet-4-6       │ │
│ └────────────┬────────────────────────────────────────────┘ │
└──────────────┼──────────────────────────────────────────────┘
               │ 5. Route to Claude Model
               ▼
┌─────────────────────────────────────────────────────────────┐
│ Claude Sonnet 4-6 (AI Model)                                │
│ • Analyzes the log content                                  │
│ • Identifies root cause                                     │
│ • Generates recommendations                                 │
│ • Returns JSON response                                     │
└────────────┬────────────────────────────────────────────────┘
             │ 6. JSON Response
             │ { "rootCause": "...", "recommendations": [...] }
             ▼
┌─────────────────────────────────────────────────────────────┐
│ ClaudeService                                               │
│ • Parses JSON response                                      │
│ • Extracts content and token usage                          │
│ • Returns ClaudeResponse                                    │
└────────────┬────────────────────────────────────────────────┘
             │ 7. ClaudeResponse
             ▼
┌─────────────────────────────────────────────────────────────┐
│ RcaPlugin                                                   │
│ • Deserializes to RcaJsonResponse                           │
│ • Maps to RcaResult record                                  │
│ • Adds TokenUsage and timestamp                             │
│ • Returns JSON string                                       │
└────────────┬────────────────────────────────────────────────┘
             │ 8. JSON string
             ▼
┌─────────────────────────────────────────────────────────────┐
│ PipelineController                                          │
│ • Deserializes to RcaResult                                 │
│ • Logs completion                                           │
│ • Returns HTTP 200 OK                                       │
└────────────┬────────────────────────────────────────────────┘
             │ 9. HTTP Response with RcaResult JSON
             ▼
┌─────────────────────────────────────────────────────────────┐
│ YOU (User/Client)                                           │
│ • Receives analysis result                                  │
│ • Reads root cause and recommendations                      │
│ • Takes action to fix the issue                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 Data Flow

```
Request (Your Browser/Postman)
    ↓ HTTP POST with logContent
┌───────────────────────────┐
│ AnalysisRequest           │ ← API receives this
│ { logContent: "..." }     │
└───────────┬───────────────┘
            ↓
┌───────────────────────────┐
│ ClaudeRequest             │ ← RcaPlugin creates this
│ { Message, SystemPrompt } │
└───────────┬───────────────┘
            ↓
        AWS Bedrock
            ↓
┌───────────────────────────┐
│ ClaudeResponse            │ ← Service receives this
│ { Content, Tokens, ... }  │
└───────────┬───────────────┘
            ↓
┌───────────────────────────┐
│ RcaJsonResponse           │ ← Plugin deserializes JSON
│ { RootCause, ErrorType }  │
└───────────┬───────────────┘
            ↓
┌───────────────────────────┐
│ RcaResult                 │ ← Final result with metadata
│ + TokenUsage              │
│ + AnalyzedAt              │
└───────────┬───────────────┘
            ↓
Response (Your Browser/Postman)
    JSON with complete analysis
```

---

## ⏱️ Timeline

| Step | Duration | What's Happening |
|------|----------|------------------|
| 1. Request arrives | < 1ms | API receives HTTP POST |
| 2. Validation | < 1ms | Check logContent not empty |
| 3. RCA Plugin | < 10ms | Build prompts, create request |
| 4. AWS Network | 100-300ms | Send to Bedrock |
| 5. Claude Analysis | **2-4 sec** | AI analyzes the log |
| 6. Response parsing | < 10ms | Parse JSON, map to models |
| 7. Send response | < 50ms | Return to client |
| **Total** | **2-5 sec** | End-to-end |

---

## 🎯 What Happens at Each Layer

### **API Layer** (`AgenticPipeline.Api`)
- ✅ Receives HTTP requests
- ✅ Validates input
- ✅ Handles errors
- ✅ Returns JSON responses
- ✅ Logs all operations

### **Infrastructure Layer** (`AgenticPipeline.Infrastructure`)
- ✅ RcaPlugin: Orchestrates analysis flow
- ✅ ClaudeService: Communicates with AWS Bedrock
- ✅ Manages prompts and context

### **Core Layer** (`AgenticPipeline.Core`)
- ✅ Defines all interfaces
- ✅ Defines all models (records)
- ✅ No dependencies on other layers

---

## 🐛 Troubleshooting Flow

```
Problem: API returns error
    ↓
Check 1: Is health endpoint working?
    NO → Check AWS credentials
    YES → Continue
    ↓
Check 2: Did you send valid JSON?
    NO → Fix JSON format
    YES → Continue
    ↓
Check 3: Is logContent not empty?
    NO → Add log content
    YES → Continue
    ↓
Check 4: Check API logs for specific error
    AWS Error → Refresh credentials
    JSON Parse Error → Claude returned invalid JSON (rare)
    Timeout → Increase timeout or reduce log size
```

---

## 📝 Testing Checklist

**Prerequisites:**
- [ ] AWS credentials set
- [ ] API is running
- [ ] Browser/Postman ready

**Health Check:**
- [ ] GET /api/pipeline/health returns 200
- [ ] Response has "Healthy" status

**Analysis:**
- [ ] Copied sample log content
- [ ] POST /api/pipeline/analyze returns 200
- [ ] Response has rootCause
- [ ] Response has recommendations
- [ ] Token usage is shown

**Validation:**
- [ ] Empty log returns 400 error
- [ ] Analysis completes in < 5 seconds
- [ ] Confidence score is reasonable

---

## 🎓 Try These Logs

1. **Start Easy:** `build-failure-compilation.log`
   - Clear syntax errors
   - Easy to understand

2. **Medium:** `build-failure-test.log`
   - Test failures
   - Assertion errors

3. **Advanced:** `build-failure-dependency.log`
   - NuGet package issues
   - Version conflicts

---

## ✅ You're Done When...

- ✅ You can start the API
- ✅ Health check works
- ✅ Analysis returns intelligent recommendations
- ✅ You understand the complete flow
- ✅ You've tested all 3 sample logs

---

**Total Time:** 5-10 minutes for complete testing! 🚀

**Next:** See full details in `Testing_Guide.md`
