# AgenticPipeline.Api

**ASP.NET Core Web API** - The main entry point and REST API layer for the Agentic Pipeline Troubleshooter.

---

## 🎯 Purpose

This project serves as the **API Gateway** for the Agentic Pipeline Troubleshooter. It:

1. **Exposes REST API endpoints** for analyzing CI/CD pipeline failures
2. **Orchestrates AI analysis** using AWS Bedrock and Claude
3. **Provides interactive documentation** via Swagger UI
4. **Handles HTTP requests/responses** and error handling
5. **Configures dependency injection** for all services

---

## 🏗️ Architecture Role

```
User/CI/CD System
      ↓
[AgenticPipeline.Api] ← YOU ARE HERE
      ↓
RCA Plugin (Semantic Kernel)
      ↓
ClaudeService (AWS Bedrock)
      ↓
AWS Bedrock (Claude Sonnet 4-6)
```

This project is the **presentation layer** that sits between users and the AI analysis engine.

---

## 📁 Project Structure

```
AgenticPipeline.Api/
├── Controllers/
│   └── PipelineController.cs       # Main API endpoints
├── Models/
│   └── AnalysisRequest.cs          # Request DTOs
├── Program.cs                       # Application configuration & startup
├── appsettings.json                 # Configuration settings
├── appsettings.Development.json     # Dev-specific settings
└── README.md                        # This file
```

---

## 🔑 Key Components

### 1. **Program.cs** - Application Entry Point

**What it does:**
- Configures the ASP.NET Core web application
- Sets up dependency injection (DI) container
- Registers services (ClaudeService, RcaPlugin)
- Configures Swagger/OpenAPI documentation
- Sets up middleware pipeline (CORS, HTTPS, routing)

**Key Configuration:**
```csharp
// Registers Claude service with settings from appsettings.json
builder.Services.Configure<ClaudeSettings>(
    builder.Configuration.GetSection(ClaudeSettings.SectionName));

// Registers services as singletons (one instance for app lifetime)
builder.Services.AddSingleton<IClaudeService, ClaudeService>();
builder.Services.AddSingleton<RcaPlugin>();

// Configures Swagger UI at root URL
app.UseSwaggerUI(options => {
    options.RoutePrefix = string.Empty; // Swagger at "/"
});
```

**Why Singleton?**
- ClaudeService maintains an AWS Bedrock client (expensive to create)
- RcaPlugin is stateless and can be reused
- Better performance, lower memory usage

---

### 2. **PipelineController.cs** - API Endpoints

**Location:** `Controllers/PipelineController.cs`

**Endpoints:**

#### POST /api/pipeline/analyze
**Purpose:** Analyzes CI/CD pipeline failure logs using AI

**Request:**
```json
{
  "logContent": "Your pipeline failure log here..."
}
```

**Response:**
```json
{
  "rootCause": "Brief summary of the root cause",
  "detailedAnalysis": "Detailed explanation of what went wrong",
  "affectedComponents": ["file1.cs", "module2"],
  "errorType": "CompilationError",
  "confidenceScore": 0.95,
  "recommendations": [
    "Step 1: Fix this",
    "Step 2: Then this",
    "Step 3: Verify this"
  ],
  "tokenUsage": {
    "inputTokens": 245,
    "outputTokens": 180,
    "totalTokens": 425
  },
  "analyzedAt": "2026-04-01T10:30:00Z"
}
```

**Flow:**
1. Receives HTTP POST request with log content
2. Validates request (non-empty log)
3. Calls `RcaPlugin.AnalyzeFailureAsync(logContent)`
4. RCA Plugin calls Claude via `ClaudeService`
5. Claude analyzes and returns JSON
6. Controller parses JSON to `RcaResult`
7. Returns HTTP 200 OK with analysis

**Error Handling:**
- **400 Bad Request** - Empty or invalid log content
- **500 Internal Server Error** - Analysis failed or Claude error

---

#### GET /api/pipeline/health
**Purpose:** Health check endpoint for monitoring

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-04-01T10:30:00Z",
  "service": "Agentic Pipeline Troubleshooter"
}
```

**Use Cases:**
- Docker health checks
- Load balancer health probes
- Monitoring systems (Prometheus, Datadog, etc.)
- Quick verification that API is running

---

### 3. **AnalysisRequest.cs** - Request Model

**Location:** `Models/AnalysisRequest.cs`

**Purpose:**
- Data Transfer Object (DTO) for API requests
- ASP.NET Core uses this for model binding and validation

**Properties:**
```csharp
public class AnalysisRequest
{
    public string LogContent { get; set; } = string.Empty;
}
```

**How it works:**
- ASP.NET Core automatically deserializes JSON body to this object
- `[FromBody]` attribute in controller tells framework to use request body
- Can add validation attributes like `[Required]`, `[MaxLength]`, etc.

---

### 4. **appsettings.json** - Configuration

**Location:** `appsettings.json`

**Content:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Claude": {
    "ModelId": "us.anthropic.claude-sonnet-4-6",
    "Region": "us-west-2",
    "MaxTokens": 2048,
    "Temperature": 0.7
  }
}
```

**Claude Configuration Explained:**
- **ModelId**: AWS Bedrock inference profile ID
  - Must use `us.` prefix (inference profile)
  - Not direct model ID (would fail with on-demand throughput error)
- **Region**: AWS region where Bedrock is available
  - `us-west-2` (Oregon) has all Claude models
- **MaxTokens**: Maximum length of Claude's response
  - 2048 tokens ≈ 1500-2000 words
- **Temperature**: Creativity/randomness (0.0 to 1.0)
  - 0.7 = Balanced between creative and focused
  - Lower = more deterministic (better for technical analysis)

**Environment-Specific Settings:**
- `appsettings.Development.json` - Dev overrides
- `appsettings.Production.json` - Production overrides (if created)

---

## 🔄 Request Flow (Detailed)

### Complete Flow from HTTP Request to Response:

```
1. HTTP POST /api/pipeline/analyze
   ↓
2. ASP.NET Core Middleware Pipeline
   - CORS check (if enabled)
   - HTTPS redirection
   - Routing
   ↓
3. Model Binding
   - Deserialize JSON body to AnalysisRequest
   - Validate model
   ↓
4. PipelineController.AnalyzePipeline()
   - Log incoming request
   - Validate logContent is not empty
   ↓
5. Call RcaPlugin.AnalyzeFailureAsync(logContent)
   ↓
6. RCA Plugin
   - Build system prompt (instructions for Claude)
   - Build user message (log content)
   - Create ClaudeRequest object
   ↓
7. Call ClaudeService.InvokeModelAsync(request)
   ↓
8. Claude Service
   - Build JSON request body (Anthropic format)
   - Create AWS Bedrock InvokeModelRequest
   - Call AWS Bedrock API
   ↓
9. AWS Bedrock
   - Routes to Claude Sonnet 4-6 via inference profile
   - Claude analyzes the log
   - Returns JSON response
   ↓
10. Claude Service
    - Parse response JSON
    - Extract content and token usage
    - Return ClaudeResponse object
    ↓
11. RCA Plugin
    - Parse Claude's JSON into RcaResult
    - Return JSON string
    ↓
12. Controller
    - Deserialize to RcaResult object
    - Log completion
    - Return HTTP 200 OK with RcaResult
    ↓
13. ASP.NET Core
    - Serialize RcaResult to JSON
    - Add content-type header
    - Send HTTP response
    ↓
14. User/Client receives response
```

---

## 🔌 Dependencies

### NuGet Packages:
- **Microsoft.SemanticKernel** (1.74.0)
  - AI orchestration framework
  - Used by RCA Plugin (via Infrastructure layer)
- **Swashbuckle.AspNetCore** (10.1.7)
  - Swagger/OpenAPI documentation generator
  - Provides interactive API documentation UI

### Project References:
- **AgenticPipeline.Core**
  - Interfaces: `IClaudeService`
  - Models: `RcaResult`, `ClaudeRequest`, `ClaudeResponse`
- **AgenticPipeline.Infrastructure**
  - Services: `ClaudeService`, `ClaudeSettings`
  - Plugins: `RcaPlugin`

---

## 🚀 How to Run

### From Command Line:
```bash
cd src/AgenticPipeline.Api
dotnet run
```

### From Visual Studio:
1. Set `AgenticPipeline.Api` as startup project
2. Press **F5** (Debug) or **Ctrl+F5** (Run without debugging)

### Expected Output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7xxx
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5xxx
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Access Swagger UI:
Open browser to `https://localhost:7xxx` (port varies)

---

## 🧪 Testing

### Using Swagger UI (Easiest):
1. Start the API
2. Open browser to root URL
3. Expand **POST /api/pipeline/analyze**
4. Click **"Try it out"**
5. Paste log content from `SampleData/`
6. Click **"Execute"**
7. See response below

### Using curl:
```bash
curl -X POST "https://localhost:7xxx/api/pipeline/analyze" \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "paste your log here"
  }'
```

### Using Postman:
1. Create new POST request
2. URL: `https://localhost:7xxx/api/pipeline/analyze`
3. Body: Raw → JSON
4. Add request JSON
5. Send

---

## ⚙️ Configuration

### AWS Credentials:
The API needs AWS credentials to call Bedrock. Options:

**Option 1: Environment Variables (Current Setup)**
```powershell
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "XXXXXXXXX" -Region "us-west-2"
```
Sets:
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN`
- `AWS_REGION`

**Option 2: AWS Profile**
```bash
export AWS_PROFILE=XXXXXXXXX
export AWS_SDK_LOAD_CONFIG=1
```

**Option 3: IAM Role (Production)**
When running on AWS (ECS, EC2, Lambda):
- Attach IAM role with `AmazonBedrockFullAccess` policy
- No credentials needed (automatic)

---

## 📊 Logging

### Log Levels:
- **Information**: Request received, analysis completed
- **Warning**: Invalid requests, validation errors
- **Error**: Analysis failures, Claude errors

### Example Logs:
```
info: AgenticPipeline.Api.Controllers.PipelineController[0]
      Received pipeline analysis request. Log length: 1523 characters

info: AgenticPipeline.Api.Controllers.PipelineController[0]
      Analysis completed successfully. Error type: CompilationError, Confidence: 0.95
```

### Customize Logging:
Edit `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "AgenticPipeline": "Debug"  // More verbose for our code
    }
  }
}
```

---

## 🔐 Security Considerations

### Current State (Development):
- ✅ HTTPS enabled by default
- ✅ CORS allows all origins (for testing)
- ⚠️ No authentication/authorization
- ⚠️ No rate limiting

### Production Recommendations:
1. **Add Authentication:**
   ```csharp
   builder.Services.AddAuthentication("Bearer")
       .AddJwtBearer(options => { ... });
   ```

2. **Restrict CORS:**
   ```csharp
   policy.WithOrigins("https://yourdomain.com")
         .AllowAnyMethod()
         .AllowAnyHeader();
   ```

3. **Add Rate Limiting:**
   ```csharp
   builder.Services.AddRateLimiter(options => { ... });
   ```

4. **Add API Key Validation:**
   - Custom middleware or header validation

5. **Input Validation:**
   - Max log size limit
   - Content sanitization

---

## 📈 Performance

### Current Setup:
- **Singleton services** - One instance per app lifetime
- **Async all the way** - Non-blocking I/O
- **No caching** - Every request calls Claude (intentional for accuracy)

### Optimization Opportunities (Future):
1. **Response Caching:**
   - Cache identical logs (hash-based)
   - Redis or in-memory cache

2. **Request Queuing:**
   - Handle burst traffic with queue
   - Background processing

3. **Connection Pooling:**
   - AWS SDK handles this automatically

4. **Compression:**
   - Response compression middleware

---

## 🐛 Troubleshooting

### API won't start:
```
Error: Unable to get IAM security credentials
```
**Solution:** Run SSO credential script
```powershell
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "XXXXXXXXX" -Region "us-west-2"
```

---

### Swagger UI not showing:
**Check:** Is RoutePrefix set to empty?
```csharp
options.RoutePrefix = string.Empty; // Should be empty
```

**Access:** Go to root URL, not `/swagger`

---

### Analysis returns 500 error:
**Check logs** for specific error:
- Claude API error (check credentials, model ID)
- JSON parsing error (Claude returned invalid format)
- Network timeout (increase timeout in ClaudeService)

---

## 🔮 Future Enhancements

### Phase 2+ Features:
- [ ] Webhook endpoint for CI/CD integration
- [ ] GitHub PR comment posting
- [ ] Slack/Teams notifications
- [ ] Historical analysis storage (EF Core + SQLite)
- [ ] Commit analysis integration
- [ ] Similar failure detection (vector search)

---

## 📚 Related Documentation

- **Project Overview:** `../../README.md`
- **Claude Service:** `../AgenticPipeline.Infrastructure/Services/ClaudeService.cs`
- **RCA Plugin:** `../AgenticPipeline.Infrastructure/Plugins/RcaPlugin.cs`
- **Phase 1 Guide:** `../../docs/guides/PHASE1_COMPLETE.md`

---

## 🎯 Summary

**AgenticPipeline.Api is the gateway that:**
1. ✅ Receives pipeline failure logs via REST API
2. ✅ Routes them to AI analysis via RCA Plugin
3. ✅ Returns structured root cause analysis with recommendations
4. ✅ Provides interactive documentation (Swagger UI)
5. ✅ Handles all HTTP concerns (routing, serialization, errors)

**Key Strengths:**
- Clean separation of concerns (API vs Business Logic vs Infrastructure)
- Dependency injection for testability
- Comprehensive error handling
- Interactive documentation
- Production-ready architecture

---

**Version:** 1.0.0
**Last Updated:** 2026-04-01
**Status:** ✅ Phase 1 Complete
