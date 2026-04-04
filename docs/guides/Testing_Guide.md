# Testing Guide - Agentic Pipeline Troubleshooter

**Complete End-to-End Testing Instructions**

---

## 🎯 What You'll Test

The complete flow:
```
1. HTTP Request → API Controller
2. API → RCA Plugin
3. RCA Plugin → Claude Service
4. Claude Service → AWS Bedrock
5. AWS Bedrock → Claude Sonnet 4-6 (AI Analysis)
6. Response back through the chain
7. JSON Response → Your browser/client
```

---

## ✅ Prerequisites

### 1. AWS Credentials Setup

**Option A: Using SSO Script (Recommended)**
```powershell
cd C:\Anish\Source\Agentic_Pipeline
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "anishuser1" -Region "us-west-2"
```

This will:
- Extract AWS credentials from SSO cache
- Set persistent environment variables
- Automatically launch Visual Studio (optional)

**Option B: Manual Environment Variables**
```powershell
# Set these in your terminal before running
$env:AWS_ACCESS_KEY_ID = "your-access-key"
$env:AWS_SECRET_ACCESS_KEY = "your-secret-key"
$env:AWS_SESSION_TOKEN = "your-session-token"
$env:AWS_REGION = "us-west-2"
```

**Verify Credentials:**
```powershell
.\scripts\utils\test-env.ps1
```

Expected output:
```
✓ AWS_ACCESS_KEY_ID is set
✓ AWS_SECRET_ACCESS_KEY is set
✓ AWS_SESSION_TOKEN is set
✓ AWS_REGION is set (us-west-2)
```

---

## 🚀 Step 1: Start the API

### Option A: Visual Studio
1. Open `AgenticPipeline.sln`
2. Set `AgenticPipeline.Api` as startup project
3. Press **F5** (Debug) or **Ctrl+F5** (Run without debugging)

### Option B: Command Line
```powershell
cd C:\Anish\Source\Agentic_Pipeline\src\AgenticPipeline.Api
dotnet run
```

### Option C: Specific Configuration
```powershell
# Development mode
dotnet run --launch-profile "https"

# Watch mode (auto-reload on file changes)
dotnet watch run
```

### Expected Output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7xxx
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5xxx
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Note the HTTPS port number** (e.g., 7123) - you'll need it!

---

## 🧪 Step 2: Test the Health Endpoint

Before testing the main functionality, verify the API is running.

### Browser
Open: `https://localhost:7xxx/api/pipeline/health`

### PowerShell
```powershell
$port = 7xxx  # Replace with your port
Invoke-RestMethod -Uri "https://localhost:$port/api/pipeline/health" -Method GET -SkipCertificateCheck
```

### Expected Response:
```json
{
  "status": "Healthy",
  "timestamp": "2026-04-01T10:30:00Z",
  "service": "Agentic Pipeline Troubleshooter"
}
```

✅ If you see this, the API is running correctly!

---

## 📊 Step 3: Test Pipeline Analysis

### Method 1: Swagger UI (Easiest - Recommended)

1. **Open Swagger UI**
   - Navigate to: `https://localhost:7xxx/` (root URL)
   - Swagger UI loads automatically!

2. **Expand the POST endpoint**
   - Click on **POST /api/pipeline/analyze**
   - Click **"Try it out"** button

3. **Prepare Sample Log**
   - Open one of these sample files:
     - `C:\Anish\Source\Agentic_Pipeline\SampleData\build-failure-compilation.log`
     - `C:\Anish\Source\Agentic_Pipeline\SampleData\build-failure-test.log`
     - `C:\Anish\Source\Agentic_Pipeline\SampleData\build-failure-dependency.log`
   - Copy the entire contents

4. **Send Request**
   - Paste the log content into the `logContent` field in Swagger UI
   - Click **"Execute"**

5. **View Results**
   - Scroll down to see the response
   - Status code should be **200**
   - Response body contains the RCA analysis

---

### Method 2: PowerShell / curl

**PowerShell:**
```powershell
# Set your port
$port = 7xxx  # Replace with actual port

# Read sample log file
$logContent = Get-Content "C:\Anish\Source\Agentic_Pipeline\SampleData\build-failure-compilation.log" -Raw

# Create request body
$body = @{
    logContent = $logContent
} | ConvertTo-Json

# Send request
$response = Invoke-RestMethod `
    -Uri "https://localhost:$port/api/pipeline/analyze" `
    -Method POST `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck

# Display results
$response | ConvertTo-Json -Depth 10
```

**curl (PowerShell):**
```powershell
curl.exe -k -X POST "https://localhost:7xxx/api/pipeline/analyze" `
  -H "Content-Type: application/json" `
  -d '{\"logContent\": \"Your log content here...\"}'
```

**curl (Bash/Linux):**
```bash
curl -k -X POST "https://localhost:7xxx/api/pipeline/analyze" \
  -H "Content-Type: application/json" \
  -d @- << 'EOF'
{
  "logContent": "Your log content here..."
}
EOF
```

---

### Method 3: Postman

1. **Create New Request**
   - Method: `POST`
   - URL: `https://localhost:7xxx/api/pipeline/analyze`

2. **Configure Headers**
   - Add header: `Content-Type: application/json`

3. **Set Body**
   - Select **Body** tab
   - Choose **raw** and **JSON** format
   - Paste:
   ```json
   {
     "logContent": "Paste your sample log content here..."
   }
   ```

4. **Disable SSL Verification** (for localhost testing)
   - Settings → General → SSL certificate verification → OFF

5. **Send Request**
   - Click **Send**
   - View response below

---

### Method 4: REST Client (VS Code Extension)

Create a file `test.http`:

```http
### Health Check
GET https://localhost:7xxx/api/pipeline/health

### Analyze Pipeline Failure
POST https://localhost:7xxx/api/pipeline/analyze
Content-Type: application/json

{
  "logContent": "{{$dotenv SAMPLE_LOG}}"
}
```

Click "Send Request" above each request.

---

## 📋 Sample Logs to Test

### Compilation Error Log
**File:** `SampleData\build-failure-compilation.log`

**Expected Analysis:**
- **Error Type:** `CompilationError`
- **Root Cause:** Missing semicolons, undefined variables, syntax errors
- **Confidence:** High (0.85-0.95)
- **Recommendations:** Specific line-by-line fixes

---

### Test Failure Log
**File:** `SampleData\build-failure-test.log`

**Expected Analysis:**
- **Error Type:** `TestFailure`
- **Root Cause:** Assertion failures, mock setup issues
- **Confidence:** High (0.85-0.95)
- **Recommendations:** Fix test expectations, update mocks

---

### Dependency Issue Log
**File:** `SampleData\build-failure-dependency.log`

**Expected Analysis:**
- **Error Type:** `DependencyIssue`
- **Root Cause:** Missing NuGet packages, version conflicts
- **Confidence:** High (0.90-0.95)
- **Recommendations:** Restore packages, resolve version conflicts

---

## ✅ Expected Response Format

```json
{
  "rootCause": "Brief 1-2 sentence summary of the root cause",
  "detailedAnalysis": "Detailed explanation of what went wrong, why it happened, and the context",
  "affectedComponents": [
    "src/Controllers/PipelineController.cs",
    "src/Services/AnalysisService.cs"
  ],
  "errorType": "CompilationError",
  "confidenceScore": 0.95,
  "recommendations": [
    "Step 1: Add semicolon after line 44 in PipelineController.cs",
    "Step 2: Change 'analysisResult' to 'analysis' on line 48",
    "Step 3: Add closing brace for the method on line 52",
    "Step 4: Rebuild the solution and verify the fix"
  ],
  "tokenUsage": {
    "inputTokens": 245,
    "outputTokens": 180,
    "totalTokens": 425
  },
  "analyzedAt": "2026-04-01T14:30:00.1234567Z"
}
```

---

## 🎯 What to Verify

### ✅ Successful Response Checklist

1. **HTTP Status Code:** `200 OK`
2. **Response Structure:**
   - ✅ `rootCause` is not empty
   - ✅ `detailedAnalysis` has detailed explanation
   - ✅ `affectedComponents` lists relevant files
   - ✅ `errorType` is one of: CompilationError, TestFailure, DependencyIssue, ConfigurationError, RuntimeError, NetworkError
   - ✅ `confidenceScore` is between 0.0 and 1.0
   - ✅ `recommendations` has actionable steps
   - ✅ `tokenUsage` shows input/output token counts
   - ✅ `analyzedAt` has timestamp

3. **Claude Analysis Quality:**
   - ✅ Root cause is accurate and specific
   - ✅ Recommendations are actionable and ordered
   - ✅ Analysis mentions specific line numbers/files
   - ✅ Confidence score seems reasonable

---

## ⚠️ Common Errors & Solutions

### Error 1: API Won't Start
```
Unable to get IAM security credentials
```

**Solution:**
```powershell
# Refresh AWS credentials
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "anishuser1" -Region "us-west-2"

# Verify
.\scripts\utils\test-env.ps1
```

---

### Error 2: 400 Bad Request
```json
{
  "title": "Invalid Request",
  "detail": "Log content cannot be empty",
  "status": 400
}
```

**Solution:** Ensure `logContent` field is not empty or null.

---

### Error 3: 500 Internal Server Error
```json
{
  "title": "Analysis Error",
  "detail": "Failed to invoke Claude model: ...",
  "status": 500
}
```

**Possible Causes:**
1. **AWS Credentials Expired**
   - Re-run SSO credential script
   - Tokens expire after 8-12 hours

2. **Wrong Model ID**
   - Check `appsettings.json` has: `us.anthropic.claude-sonnet-4-6`
   - Must use inference profile (with `us.` prefix)

3. **AWS Bedrock Not Available**
   - Verify region is `us-west-2`
   - Check AWS account has Bedrock access

**Check API Logs:**
```powershell
# Look for detailed error messages in console output
# The API logs show:
# - Incoming request details
# - Claude invocation
# - Error stack traces
```

---

### Error 4: SSL Certificate Error
```
The SSL connection could not be established
```

**Solution for curl:**
```bash
curl -k ...  # Add -k flag to skip cert verification
```

**Solution for PowerShell:**
```powershell
Invoke-RestMethod ... -SkipCertificateCheck
```

**Solution for Postman:**
- Settings → General → SSL certificate verification → OFF

---

## 🔍 Monitoring & Debugging

### View API Logs
The API console shows:
```
info: AgenticPipeline.Api.Controllers.PipelineController[0]
      Received pipeline analysis request. Log length: 1523 characters

info: AgenticPipeline.Api.Controllers.PipelineController[0]
      Analysis completed successfully. Error type: CompilationError, Confidence: 0.95
```

### Check Token Usage
Monitor Claude API usage in response:
```json
"tokenUsage": {
  "inputTokens": 245,    // Your log content + system prompt
  "outputTokens": 180,   // Claude's analysis
  "totalTokens": 425     // Total cost
}
```

**Typical Token Counts:**
- Small log (100 lines): ~200-400 input tokens
- Medium log (500 lines): ~800-1500 input tokens
- Claude's response: ~150-300 output tokens

---

## 📊 Performance Testing

### Response Times
**Expected:**
- Health endpoint: < 100ms
- Analysis endpoint: 2-5 seconds (Claude processing time)

**Measure with PowerShell:**
```powershell
Measure-Command {
    Invoke-RestMethod `
        -Uri "https://localhost:7xxx/api/pipeline/analyze" `
        -Method POST `
        -Body $body `
        -ContentType "application/json" `
        -SkipCertificateCheck
}
```

---

## 🧪 Advanced Testing

### Test Different Error Types

**Compilation Error:**
```json
{ "logContent": "error CS0103: The name 'undefined' does not exist..." }
```

**Test Failure:**
```json
{ "logContent": "Expected: True\nActual: False\nTest failed..." }
```

**Dependency Issue:**
```json
{ "logContent": "NU1101: Unable to find package 'MissingPackage'..." }
```

### Test Edge Cases

**Empty Log:**
```json
{ "logContent": "" }
```
Expected: `400 Bad Request`

**Very Long Log:**
```json
{ "logContent": "<10000+ lines of log>" }
```
Expected: Still works, but higher token usage

**Invalid JSON:**
```
{ logContent: "missing quotes" }
```
Expected: `400 Bad Request` with JSON parsing error

---

## 📝 Testing Checklist

Use this checklist for complete testing:

- [ ] AWS credentials are set and valid
- [ ] API starts successfully
- [ ] Health endpoint returns 200 OK
- [ ] Swagger UI loads at root URL
- [ ] Compilation error log analysis works
- [ ] Test failure log analysis works
- [ ] Dependency issue log analysis works
- [ ] Response has all required fields
- [ ] Token usage is tracked correctly
- [ ] Confidence scores are reasonable
- [ ] Recommendations are actionable
- [ ] Error handling works (empty log → 400)
- [ ] Response time is acceptable (< 5 seconds)

---

## 🎓 Next Steps After Testing

Once basic testing works:

1. **Phase 2: Commit Analysis**
   - Add GitHub integration
   - Analyze code changes that caused failures

2. **Phase 3: Historical Matching**
   - Store previous analyses
   - Find similar failures

3. **Phase 4: CI/CD Integration**
   - Add webhook endpoint
   - Auto-analyze on build failures

4. **Phase 5: Notifications**
   - GitHub PR comments
   - Slack notifications

---

## 📚 Additional Resources

- **API Documentation:** `src/AgenticPipeline.Api/README.md`
- **Project Overview:** `README.md` (root)
- **AWS Setup:** `docs/setup/AWS_Setup_Guide.md`
- **Quick Reference:** `docs/setup/QUICK_REFERENCE.md`
- **Sample Logs:** `SampleData/*.log`

---

## 💡 Tips

1. **Use Swagger UI** for interactive testing - it's the easiest!
2. **Start with small logs** to verify functionality
3. **Check token usage** to estimate costs
4. **Monitor API logs** for debugging
5. **Save successful requests** in Postman/REST Client for reuse
6. **Test all 3 error types** to see Claude's versatility

---

**Ready to test? Start with the health endpoint, then try Swagger UI!** 🚀

---

**Last Updated:** 2026-04-01
**Status:** ✅ Complete Testing Guide
