# Quick Start: Testing Auto-Fix PR Creation

## Easiest Test (2 Commands!)

**1. Copy test files to your Semantic-Kernel repo:**
```powershell
.\scripts\setup-test-scenario.ps1 -TargetRepo "C:\Anish\Source\Semantic-Kernel"
```

**2. Commit and push:**
```powershell
cd C:\Anish\Source\Semantic-Kernel
git add test-autofix.cs .github/workflows/test-autofix.yml
git commit -m "Add auto-fix test"
git push
```

**That's it!** The workflow will trigger automatically, fail, and the auto-fix PR will be created within ~30 seconds.

---

## 5-Minute Test

### Prerequisites
1. API running: `dotnet run --project src/AgenticPipeline.Api`
2. ngrok running: `ngrok http 7056`
3. `CreateAutoFixPRs: true` in `appsettings.Development.json`

### Run Test Helper
```powershell
.\scripts\test-autofix.ps1
```

This script will:
-  Check all prerequisites
-  Verify API is running
-  Show configuration status
-  Provide testing options

---

## Manual Test (Step-by-Step)

### 1. Use the Pre-Created Test Files

**Good news!** Test files are already created in this repository:
- `test-autofix.cs` - Test file with missing using statement
- `.github/workflows/test-autofix.yml` - Workflow that will fail

**To use in your Semantic-Kernel repository**, copy these files or create them manually:

**File: `test-autofix.cs`**
```csharp
namespace Test;

public class AutoFixTest
{
    public string TestMethod(object data)
    {
        // This will fail - missing using System.Text.Json;
        return JsonSerializer.Serialize(data);
    }
}
```

### 2. Create Workflow to Build It

**File: `.github/workflows/test-autofix.yml`**
```yaml
name: Test Auto-Fix

on:
  workflow_dispatch:
  push:
    branches:
      - master
    paths:
      - 'test-autofix.cs'
      - '.github/workflows/test-autofix.yml'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build test file (will fail)
        run: |
          echo "Building test file..."
          dotnet build test-autofix.cs
```

### 3. Commit and Push

```bash
git add test-autofix.cs .github/workflows/test-autofix.yml
git commit -m "Test auto-fix functionality"
git push
```

### 4. Trigger Workflow

Go to GitHub Actions → "Test Auto-Fix" workflow → "Run workflow"

### 5. Watch API Logs

In your API console, look for:

```
 Received workflow_run webhook
 Processing workflow failure
 Root cause analysis completed
 Attempting to generate auto-fix
 Auto-fix generated successfully. Creating PR...
 Created auto-fix PR #123
```

### 6. Check GitHub

Go to your repository → Pull Requests

You should see:
- **Title:** `[Auto-Fix] CompilationError: JsonSerializer not recognized`
- **Branch:** `autofix/compilationerror-TIMESTAMP`
- **Changes:** Added `using System.Text.Json;`

---

## What to Expect

### Success Case
1. Workflow fails with compilation error
2. Webhook triggers within seconds
3. API analyzes failure (10-30 seconds)
4. Claude generates fix (5-10 seconds)
5. PR is created automatically (2-5 seconds)
6. **Total time: 20-45 seconds**

### Expected Fix
The PR should add this line at the top of the file:
```csharp
using System.Text.Json;
```

### PR Description Will Include
- Fix confidence level (e.g., 95%)
- Description of what was fixed
- Original error analysis
- List of files changed with reasons
- Warning to review before merging

---

## Troubleshooting

### "No PR created"

Check logs for:

**If you see:** `Skipping auto-fix due to low confidence`
- RCA didn't identify the issue clearly
- Try lowering `MinConfidenceForAutoFixPR` to 0.7

**If you see:** `Auto-fix not generated or not safe to apply`
- Claude determined it's too complex
- Error type might not be suitable for auto-fix
- Try a simpler syntax error

**If you see:** `AutoFixService not available`
- Service not registered - check Program.cs
- Restart the API

### "API not receiving webhook"

1. Check ngrok is running: `curl https://your-url.ngrok-free.dev/api/webhook/test`
2. Check webhook is configured in GitHub repository settings
3. Check webhook deliveries in GitHub for errors

### "Build fails but no webhook"

Verify webhook is triggered on `workflow_run` completion:
- Go to Settings → Webhooks
- Check "Recent Deliveries"
- Ensure `workflow_run` event is selected

---

## Quick Commands

```powershell
# Check if API is running
curl https://localhost:7056/api/webhook/test

# List auto-fix PRs
gh pr list --label autofix

# List auto-fix branches
git branch -r | grep autofix

# Clean up test branches
.\scripts\test-autofix.ps1 -CleanupOnly

# View recent failures in database
sqlite3 src/AgenticPipeline.Api/pipeline.db "SELECT ErrorType, ConfidenceScore, datetime(AnalyzedAt, 'localtime') FROM Failures ORDER BY AnalyzedAt DESC LIMIT 5;"
```

---

## Next Steps After Successful Test

1. **Review the PR**
   - Check if fix is correct
   - Verify it builds successfully
   - Test the changes

2. **Merge or Close**
   - Merge if fix is good (validates the system works!)
   - Close if you're just testing

3. **Clean Up**
   - Delete the test file
   - Delete the test workflow
   - Delete the auto-fix branch

4. **Fine-tune Configuration**
   - Adjust `MinConfidenceForAutoFixPR` based on results
   - Enable/disable for production

---

## More Complex Testing

See detailed guide: **`docs/testing/Auto_Fix_Testing_Guide.md`**

Includes:
- Multiple test scenarios
- Different error types
- Edge cases
- Performance testing
- Safety boundary testing

---

## Need Help?

1. Check API logs for detailed error messages
2. Check `docs/testing/Auto_Fix_Testing_Guide.md` for comprehensive troubleshooting
3. Review `docs/guides/Auto_Fix_PR_Guide.md` for feature documentation
