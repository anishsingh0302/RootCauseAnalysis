# Agentic Pipeline Troubleshooter

AI-powered CI/CD pipeline failure analysis using AWS Bedrock and Claude

## 🚀 Phase 1 Complete & Tested!

### What's Working:
- ✅ .NET 9 solution structure with 4 projects
- ✅ AWS Bedrock Claude integration (us.anthropic.claude-sonnet-4-6)
- ✅ Root Cause Analysis (RCA) Plugin with Semantic Kernel
- ✅ REST API with Swagger UI
- ✅ Sample pipeline failure logs for testing
- ✅ Token usage tracking
- ✅ Structured JSON responses
- ✅ **End-to-end testing completed successfully**

---

## 📁 Project Structure

```
AgenticPipeline/
├── src/
│   ├── AgenticPipeline.Api/          # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   └── PipelineController.cs # Main API controller
│   │   ├── Models/
│   │   │   └── AnalysisRequest.cs   # Request models
│   │   ├── Program.cs                # App configuration
│   │   └── appsettings.json          # Configuration
│   │
│   ├── AgenticPipeline.Core/         # Domain models & interfaces
│   │   ├── Interfaces/
│   │   │   └── IClaudeService.cs     # Claude service interface
│   │   └── Models/
│   │       └── RcaResult.cs          # RCA result model
│   │
│   └── AgenticPipeline.Infrastructure/ # Implementation
│       ├── Plugins/
│       │   └── RcaPlugin.cs          # RCA plugin (Semantic Kernel)
│       └── Services/
│           └── ClaudeService.cs      # AWS Bedrock integration
│
├── tests/
│   ├── AgenticPipeline.Tests/        # Unit tests
│   └── BedrockConnectionTest/        # AWS connection test app
│
├── SampleData/
│   ├── build-failure-compilation.log  # Sample compilation error
│   ├── build-failure-test.log         # Sample test failure
│   └── build-failure-dependency.log   # Sample dependency issue
│
└── nuget.config                       # NuGet configuration
```

---

## 🔧 Configuration

### appsettings.json

```json
{
  "Claude": {
    "ModelId": "us.anthropic.claude-sonnet-4-6",
    "Region": "us-west-2",
    "MaxTokens": 2048,
    "Temperature": 0.7
  }
}
```

### AWS Credentials

The application uses AWS SSO credentials from environment variables:
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN`
- `AWS_REGION`

**To refresh credentials:**
```powershell
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "XXXXXXXXX" -Region "us-west-2"
```

See [Scripts Documentation](scripts/README.md) for all available scripts.

---

## 🏃‍♂️ Running the Application

### 1. Start the API

```bash
cd src/AgenticPipeline.Api
dotnet run
```

The API will start at `https://localhost:7xxx` (port varies)

### 2. Open Swagger UI

Navigate to the root URL in your browser - Swagger UI is configured as the default page.

### 3. Test the API

**Endpoint:** `POST /api/pipeline/analyze`

**Request Body:**
```json
{
  "logContent": "<paste your pipeline log here>"
}
```

**Sample Test:**

1. Copy content from `SampleData/build-failure-compilation.log`
2. Paste into the Swagger UI request body
3. Click "Execute"

**Response:**
```json
{
  "rootCause": "Missing semicolon and variable name mismatch...",
  "detailedAnalysis": "The code has 3 compilation errors...",
  "affectedComponents": ["PipelineController.cs"],
  "errorType": "CompilationError",
  "confidenceScore": 0.95,
  "recommendations": [
    "Step 1: Add semicolon on line 44",
    "Step 2: Fix variable name from 'analysisResult' to 'analysis'",
    "Step 3: Add closing brace for method"
  ],
  "tokenUsage": {
    "inputTokens": 245,
    "outputTokens": 150,
    "totalTokens": 395
  },
  "analyzedAt": "2026-04-01T10:30:00Z"
}
```

---

## 📊 API Endpoints

### POST /api/pipeline/analyze
Analyzes CI/CD pipeline failure logs

**Request:**
- `logContent` (string): The pipeline failure log

**Response:** RcaResult object with:
- `rootCause`: Brief summary
- `detailedAnalysis`: Detailed explanation
- `affectedComponents`: List of affected files
- `errorType`: Classification (CompilationError, TestFailure, etc.)
- `confidenceScore`: 0.0 to 1.0
- `recommendations`: Step-by-step fix suggestions
- `tokenUsage`: Token consumption tracking
- `analyzedAt`: Analysis timestamp

### GET /api/pipeline/health
Health check endpoint

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-04-01T10:30:00Z",
  "service": "Agentic Pipeline Troubleshooter"
}
```

---

## 🧪 Testing

### Using Sample Logs

Three sample logs are included:

1. **build-failure-compilation.log** - C# compilation errors
2. **build-failure-test.log** - Unit test failures
3. **build-failure-dependency.log** - NuGet package issues

### Using Swagger UI

1. Navigate to `https://localhost:xxxx`
2. Expand `POST /api/pipeline/analyze`
3. Click "Try it out"
4. Paste log content
5. Click "Execute"

### Using curl

```bash
curl -X POST "https://localhost:7xxx/api/pipeline/analyze" \
  -H "Content-Type: application/json" \
  -d '{
    "logContent": "your log content here"
  }'
```

---

## 🏗️ Technology Stack

- **.NET 9** - Runtime
- **ASP.NET Core** - Web API
- **Microsoft Semantic Kernel** - AI orchestration
- **AWS Bedrock** - LLM service
- **Claude Sonnet 4-6** - AI model
- **Swaggerbuckle** - API documentation
- **xUnit** - Testing framework

---

## 📦 NuGet Packages

**Api Project:**
- Microsoft.SemanticKernel (1.74.0)
- Swashbuckle.AspNetCore (10.1.7)

**Infrastructure Project:**
- AWSSDK.BedrockRuntime (4.0.17.1)
- AWSSDK.Core (4.0.3.27)
- Microsoft.Extensions.Options (10.0.5)
- Microsoft.SemanticKernel (1.74.0)

---

## 🔐 Security Notes

- **Never commit AWS credentials to git**
- SSO credentials expire after 8-12 hours
- Use `set-sso-creds-persistent.ps1` to refresh
- Environment variables are set at USER level (system-wide)
- Clear credentials manually from System Properties if needed

---

## 🚀 Next Steps (Phase 2+)

### Phase 2: Commit Analysis
- [ ] GitHub integration (Octokit)
- [ ] Commit analyzer plugin
- [ ] Correlate failures with code changes

### Phase 3: Historical Matching
- [ ] Vector embeddings (AWS Titan or OpenAI)
- [ ] Similar failure detection
- [ ] Fix recommendation from history

### Phase 4: Orchestration
- [ ] Webhook endpoint for CI/CD systems
- [ ] Multi-plugin orchestration
- [ ] Retry logic and error handling

### Phase 5: Output Integrations
- [ ] GitHub PR comments
- [ ] Slack notifications
- [ ] Markdown report templates
- [ ] Docker containerization

---

## 📚 Documentation

All documentation is organized in the `docs/` folder:

- **[Documentation Index](docs/README.md)** - Complete guide to all docs
- **[Phase 1 Complete Guide](docs/guides/PHASE1_COMPLETE.md)** - Test the API now!
- **[AWS Setup Guide](docs/setup/AWS_Setup_Guide.md)** - Complete AWS setup
- **[Session History](docs/tracking/Session.md)** - Development log (9 sessions)
- **[Project Status](docs/tracking/STATUS.md)** - Current phase and next steps
- **[Task List](docs/tracking/task.md)** - Task tracking
- **[.NET Implementation Plan](docs/implementation/DotNet_Implementation_Plan.md)** - Project architecture

---

## 🐛 Troubleshooting

### "Unable to get IAM security credentials"
Run the SSO credential script:
```powershell
.\tests\BedrockConnectionTest\set-sso-creds-persistent.ps1 -ProfileName "XXXXXXXXX" -Region "us-west-2"
```

### "Model not available" error
Check that you're using inference profile ID:
- ✅ `us.anthropic.claude-sonnet-4-6`
- ❌ `anthropic.claude-sonnet-4-6`

### Build errors
```bash
cd C:\XXXXX\Source\Agentic_Pipeline
dotnet clean
dotnet restore
dotnet build
```

---

## 👨‍💻 Development

### Build Solution
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Clean Build
```bash
dotnet clean
dotnet restore
dotnet build
```

---

## 📝 License

[Your License Here]

---

## 🤝 Contributing

This is a hackathon project for personal use.

---

**Last Updated:** 2026-04-01
**Version:** 1.0.0 (Phase 1 Complete & Tested)
**Status:** ✅ Tested & Working - Ready for Phase 2
