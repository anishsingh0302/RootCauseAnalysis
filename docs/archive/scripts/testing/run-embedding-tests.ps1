# Run embedding tests with AWS credentials from USER environment
# This ensures dotnet test picks up the credentials set by set-sso-creds-persistent.ps1

Write-Host "Loading AWS credentials from USER environment..." -ForegroundColor Cyan

# Load credentials from USER-level environment variables into current session
$env:AWS_ACCESS_KEY_ID = [System.Environment]::GetEnvironmentVariable('AWS_ACCESS_KEY_ID', 'User')
$env:AWS_SECRET_ACCESS_KEY = [System.Environment]::GetEnvironmentVariable('AWS_SECRET_ACCESS_KEY', 'User')
$env:AWS_SESSION_TOKEN = [System.Environment]::GetEnvironmentVariable('AWS_SESSION_TOKEN', 'User')
$env:AWS_REGION = [System.Environment]::GetEnvironmentVariable('AWS_REGION', 'User')
$env:AWS_DEFAULT_REGION = [System.Environment]::GetEnvironmentVariable('AWS_DEFAULT_REGION', 'User')

# Verify credentials are loaded
if ([string]::IsNullOrEmpty($env:AWS_ACCESS_KEY_ID)) {
    Write-Host "ERROR: AWS credentials not found!" -ForegroundColor Red
    Write-Host "Please run: .\scripts\aws\set-sso-creds-persistent.ps1" -ForegroundColor Yellow
    exit 1
}

$maskedKey = $env:AWS_ACCESS_KEY_ID.Substring(0, [Math]::Min(10, $env:AWS_ACCESS_KEY_ID.Length)) + "..."
Write-Host "  AWS_ACCESS_KEY_ID: $maskedKey" -ForegroundColor Green
Write-Host "  AWS_REGION: $env:AWS_REGION" -ForegroundColor Green
Write-Host ""

# Run the embedding service tests
Write-Host "Running embedding service integration tests..." -ForegroundColor Cyan
Write-Host ""

dotnet test "tests\AgenticPipeline.IntegrationTests\AgenticPipeline.IntegrationTests.csproj" `
    --filter "FullyQualifiedName~EmbeddingServiceTests" `
    --logger "console;verbosity=detailed"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Some tests failed. See output above for details." -ForegroundColor Red
}

exit $LASTEXITCODE
