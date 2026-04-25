# Test Azure DevOps Build Logs API
param(
    [int]$BuildId = 7,
    [string]$Organization = "anishsingh0302",
    [string]$Project = "AgenticPipeline"
)

Write-Host "Testing Azure DevOps Build Logs API" -ForegroundColor Cyan
Write-Host ""

# Get PAT from environment
$pat = $env:AzureDevOps__PersonalAccessToken
if ([string]::IsNullOrEmpty($pat)) {
    Write-Host "ERROR: PAT not found" -ForegroundColor Red
    exit 1
}

Write-Host "PAT loaded: $($pat.Length) chars" -ForegroundColor Green
Write-Host ""

# Create auth header
$base64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$pat"))
$headers = @{
    Authorization = "Basic $base64"
    Accept = "application/json"
}

# Test 1: Get Build Info
Write-Host "Test 1: Getting build info..." -ForegroundColor Yellow
$buildUrl = "https://dev.azure.com/$Organization/$Project/_apis/build/builds/$BuildId`?api-version=7.1-preview.7"
try {
    $buildInfo = Invoke-RestMethod -Uri $buildUrl -Headers $headers
    Write-Host "SUCCESS - Build $($buildInfo.buildNumber) - Status: $($buildInfo.status)" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Try logs with different API versions
$versions = @("7.1-preview.7", "7.1", "7.0", "6.0")

foreach ($ver in $versions) {
    Write-Host "Test 2: Trying logs with API version $ver..." -ForegroundColor Yellow
    $logsUrl = "https://dev.azure.com/$Organization/$Project/_apis/build/builds/$BuildId/logs?api-version=$ver"

    try {
        $logsList = Invoke-RestMethod -Uri $logsUrl -Headers $headers
        Write-Host "SUCCESS with version $ver - Found $($logsList.count) logs" -ForegroundColor Green

        if ($logsList.count -gt 0) {
            Write-Host "Log entries:" -ForegroundColor Gray
            foreach ($log in $logsList.value) {
                Write-Host "  ID: $($log.id) - Type: $($log.type) - Lines: $($log.lineCount)" -ForegroundColor Gray
            }

            # Try fetching first log
            Write-Host ""
            Write-Host "Test 3: Fetching log content..." -ForegroundColor Yellow
            $logUrl = "https://dev.azure.com/$Organization/$Project/_apis/build/builds/$BuildId/logs/$($logsList.value[0].id)?api-version=$ver"
            try {
                $content = Invoke-RestMethod -Uri $logUrl -Headers $headers
                Write-Host "SUCCESS - Got $($content.Length) chars" -ForegroundColor Green
                Write-Host "First 300 chars:" -ForegroundColor Gray
                Write-Host $content.Substring(0, [Math]::Min(300, $content.Length)) -ForegroundColor DarkGray
            } catch {
                Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host "No logs found yet (build might still be running)" -ForegroundColor Yellow
        }

        break
    } catch {
        Write-Host "FAILED with $ver`: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Testing complete" -ForegroundColor Cyan
