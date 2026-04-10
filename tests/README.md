# Test Architecture

## Overview

The test suite has been refactored to follow best practices with proper **Arrange-Act-Assert** pattern, shared test infrastructure, and focused integration tests.

## Project Structure

```
tests/
├── AgenticPipeline.Tests.Common/          # Shared test infrastructure
│   ├── IntegrationTestBase.cs             # Base class for all integration tests
│   ├── TestServiceFactory.cs              # Centralized service creation
│   ├── TestDataHelper.cs                  # Sample data loading utilities
│   ├── AwsCredentialHelper.cs             # AWS credential validation
│   └── XUnitLoggerProvider.cs             # xUnit output logging
│
├── AgenticPipeline.IntegrationTests/      # 🆕 Main integration tests (xUnit)
│   ├── RcaPluginIntegrationTests.cs       # RCA Plugin tests
│   ├── GitHubServiceIntegrationTests.cs   # GitHub API integration tests
│   ├── CommitAnalyzerPluginIntegrationTests.cs  # Commit analyzer tests
│   └── EndToEndIntegrationTests.cs        # Complete workflow tests
│
├── AgenticPipeline.Tests/                 # Unit tests (xUnit)
├── BedrockConnectionTest/                 # AWS Bedrock connection test
├── GitHubConnectionTest/                  # GitHub API connection test
├── CommitAnalyzerTest/                    # Commit analyzer standalone test
└── Phase2IntegrationTest/                 # Legacy console integration test
```

## 🎯 New Test Architecture Benefits

### 1. **Shared Test Infrastructure** (`AgenticPipeline.Tests.Common`)

All common code is centralized and reusable:

- **IntegrationTestBase**: Base class with logging and test output helpers
- **TestServiceFactory**: Creates service instances with consistent configuration
- **TestDataHelper**: Loads sample logs from `SampleData/` folder
- **AwsCredentialHelper**: Validates AWS credentials before tests run
- **XUnitLoggerProvider**: Routes logs to xUnit test output

### 2. **Proper xUnit Tests** (`AgenticPipeline.IntegrationTests`)

All tests follow **Arrange-Act-Assert** pattern:

```csharp
[Fact]
public async Task AnalyzeFailure_WithCompilationError_ReturnsStructuredAnalysis()
{
    // Arrange - Set up test data and dependencies
    AwsCredentialHelper.EnsureCredentials();
    var factory = new TestServiceFactory(LoggerFactory);
    var rcaPlugin = factory.CreateRcaPlugin();
    var logContent = await TestDataHelper.LoadCompilationErrorLogAsync();

    // Act - Execute the test
    var result = await rcaPlugin.AnalyzeFailureAsync(logContent);

    // Assert - Verify results
    Assert.NotNull(result);
    var json = JsonDocument.Parse(result);
    Assert.True(json.RootElement.TryGetProperty("rootCause", out _));
}
```

### 3. **Focused Test Classes**

Tests are split by component/concern:

- **RcaPluginIntegrationTests**: Tests RCA plugin with real Claude AI
  - ✅ Compilation errors
  - ✅ Test failures
  - ✅ Dependency errors
  - ✅ Token usage tracking

- **GitHubServiceIntegrationTests**: Tests GitHub API integration
  - ✅ Fetching recent commits
  - ✅ Getting commit details with file changes
  - ✅ Filtering by date range
  - ✅ Error handling

- **CommitAnalyzerPluginIntegrationTests**: Tests commit analysis
  - ✅ Commit context analysis
  - ✅ Commit-to-failure correlation
  - ✅ Empty commit range handling

- **EndToEndIntegrationTests**: Tests complete workflows
  - ✅ Baseline vs Enhanced RCA comparison (Phase 2 validation)
  - ✅ Multiple failure types
  - ✅ Performance testing

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test tests/AgenticPipeline.IntegrationTests
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~RcaPluginIntegrationTests"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~AnalyzeFailure_WithCompilationError_ReturnsStructuredAnalysis"
```

### Run Tests by Category
```bash
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run only end-to-end tests
dotnet test --filter "Category=EndToEnd"

# Run tests that require AWS
dotnet test --filter "RequiresAWS=true"
```

## Prerequisites

### AWS Credentials
Integration tests require AWS credentials:

```powershell
.\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName "YOUR_AWS_PROFILE" -Region "us-west-2"
```

Tests will automatically check for credentials and provide clear error messages if missing.

### GitHub API (Optional)
Most GitHub tests work without authentication (60 requests/hour limit). For higher limits, set a Personal Access Token:

```csharp
var githubSettings = new GitHubSettings(
    PersonalAccessToken: "ghp_your_token_here",
    DefaultOwner: "",
    DefaultRepo: ""
);
```

## Test Traits

Tests are tagged with traits for easy filtering:

| Trait | Description | Example |
|-------|-------------|---------|
| `Category=Integration` | Integration tests | All tests in `AgenticPipeline.IntegrationTests` |
| `Category=EndToEnd` | End-to-end workflow tests | `EndToEndIntegrationTests` |
| `RequiresAWS=true` | Needs AWS credentials | RCA and Commit analysis tests |
| `RequiresGitHub=true` | Calls GitHub API | GitHub service tests |

## Test Output

Tests write to xUnit output, visible in:
- **Visual Studio**: Test Explorer → Test Output
- **VS Code**: Test Explorer → Show Output
- **Command Line**: Detailed output with `-v detailed`

```bash
dotnet test -v detailed
```

## Sample Test Output

```
✅ Analysis completed in 2.3 seconds
Root Cause: Missing NuGet package reference
Error Type: CompilationError
Confidence: 95%
Components: 2
Recommendations: 3
Token usage: 1234 input + 567 output = 1801 total
```

## Legacy Tests

### Phase2IntegrationTest (Console App)
The original console-based integration test is preserved for manual testing:

```bash
cd tests\Phase2IntegrationTest
dotnet run
```

This provides a detailed narrative output with side-by-side comparison of baseline vs enhanced RCA.

### When to Use Each

| Use Case | Recommended Test Type |
|----------|----------------------|
| **Automated CI/CD** | xUnit tests (`AgenticPipeline.IntegrationTests`) |
| **Manual validation** | Console test (`Phase2IntegrationTest`) |
| **Debugging specific component** | Individual xUnit test classes |
| **Quick smoke test** | Connection tests (`BedrockConnectionTest`, `GitHubConnectionTest`) |

## Best Practices

### Writing New Tests

1. **Inherit from IntegrationTestBase**
   ```csharp
   public class MyIntegrationTests : IntegrationTestBase
   {
       public MyIntegrationTests(ITestOutputHelper output) : base(output) { }
   }
   ```

2. **Use TestServiceFactory**
   ```csharp
   var factory = new TestServiceFactory(LoggerFactory);
   var service = factory.CreateRcaPlugin();
   ```

3. **Load sample data from TestDataHelper**
   ```csharp
   var log = await TestDataHelper.LoadCompilationErrorLogAsync();
   ```

4. **Follow AAA pattern**
   - **Arrange**: Set up test data and dependencies
   - **Act**: Execute the operation
   - **Assert**: Verify the results

5. **Add test traits**
   ```csharp
   [Trait("Category", "Integration")]
   [Trait("RequiresAWS", "true")]
   ```

6. **Write descriptive test names**
   ```csharp
   // Good: Describes what is tested and expected result
   public async Task AnalyzeFailure_WithCompilationError_ReturnsStructuredAnalysis()

   // Bad: Vague or generic names
   public async Task Test1()
   ```

## Migration from Console Tests

To convert console app tests to xUnit:

1. Create new test class inheriting from `IntegrationTestBase`
2. Convert main logic to test methods with `[Fact]` attribute
3. Use AAA pattern to structure the test
4. Replace `Console.WriteLine` with `WriteLine()`
5. Replace `return 0/1` with `Assert` statements

## Future Improvements

- [ ] Add unit tests with mocked dependencies
- [ ] Add test coverage reporting
- [ ] Create test data builders for complex objects
- [ ] Add custom assertions for common patterns
- [ ] Implement test fixtures for expensive setup (e.g., one-time AWS connection)

## Related Documentation

- [Testing Guide](../docs/guides/Testing_Guide.md) - Complete testing instructions
- [Quick Start Testing](../docs/guides/Quick_Start_Testing.md) - 5-minute quick start
- [Weekly Breakdown](../docs/planning/Weekly_Breakdown.md) - Day 9 testing tasks

---

**Last Updated:** 2026-04-05
**Status:** ✅ Test refactoring complete
