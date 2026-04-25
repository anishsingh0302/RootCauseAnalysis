#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests if GitHub workflow logs are available for download

.DESCRIPTION
    Checks if logs are available for a specific workflow run and attempts to download them

.PARAMETER RunId
    GitHub workflow run ID to check

.PARAMETER Owner
    Repository owner (default: anishsingh0302)

.PARAMETER Repo
    Repository name (default: AgenticPipeline)

.PARAMETER Token
    GitHub Personal Access Token (reads from env if not provided)

.EXAMPLE
    .\test-github-logs.ps1 -RunId 24886439601

.EXAMPLE
    .\test-github-logs.ps1 -RunId 24886439601 -Token "ghp_..."
#>

param(
    [Parameter(Mandatory=$true)]
    [long]$RunId,

    [Parameter(Mandatory=$false)]
    [string]$Owner = "anishsingh0302",

    [Parameter(Mandatory=$false)]
    [string]$Repo = "AgenticPipeline",

    [Parameter(Mandatory=$false)]
    [string]$Token = $env:GitHub__PersonalAccessToken
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  GitHub Workflow Logs Diagnostic" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Host "ERROR: No GitHub token provided" -ForegroundColor Red
    Write-Host "Either set GitHub__PersonalAccessToken environment variable or pass -Token parameter" -ForegroundColor Yellow
    exit 1
}

Write-Host "Repository: $Owner/$Repo" -ForegroundColor Yellow
Write-Host "Workflow Run ID: $RunId" -ForegroundColor Yellow
Write-Host "Token: $($Token.Substring(0, 10))..." -ForegroundColor Yellow
Write-Host ""

# Step 1: Check if workflow run exists
Write-Host "Step 1: Checking if workflow run exists..." -ForegroundColor Cyan

$headers = @{
    "Accept" = "application/vnd.github+json"
    "Authorization" = "Bearer $Token"
    "X-GitHub-Api-Version" = "2022-11-28"
}

try {
    $runUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs/$RunId"
    $run = Invoke-RestMethod -Uri $runUrl -Headers $headers -ErrorAction Stop

    Write-Host "  [OK] Workflow run found" -ForegroundColor Green
    Write-Host "  Name: $($run.name)" -ForegroundColor Gray
    Write-Host "  Status: $($run.status)" -ForegroundColor Gray
    Write-Host "  Conclusion: $($run.conclusion)" -ForegroundColor Gray
    Write-Host "  Created: $($run.created_at)" -ForegroundColor Gray
    Write-Host "  Updated: $($run.updated_at)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "  [FAIL] Workflow run not found" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Check jobs
Write-Host "Step 2: Checking workflow jobs..." -ForegroundColor Cyan

try {
    $jobsUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs/$RunId/jobs"
    $jobs = Invoke-RestMethod -Uri $jobsUrl -Headers $headers -ErrorAction Stop

    Write-Host "  [OK] Found $($jobs.total_count) job(s)" -ForegroundColor Green
    foreach ($job in $jobs.jobs) {
        Write-Host "  - Job: $($job.name)" -ForegroundColor Gray
        Write-Host "    Status: $($job.status), Conclusion: $($job.conclusion)" -ForegroundColor Gray
        Write-Host "    Started: $($job.started_at), Completed: $($job.completed_at)" -ForegroundColor Gray
    }
    Write-Host ""
}
catch {
    Write-Host "  [WARN] Could not fetch jobs" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host ""
}

# Step 3: Try to download logs
Write-Host "Step 3: Attempting to download logs..." -ForegroundColor Cyan

try {
    $logsUrl = "https://api.github.com/repos/$Owner/$Repo/actions/runs/$RunId/logs"

    # Note: This returns a redirect to the actual ZIP file
    $response = Invoke-WebRequest -Uri $logsUrl -Headers $headers -MaximumRedirection 0 -ErrorAction SilentlyContinue

    if ($response.StatusCode -eq 302 -or $response.StatusCode -eq 307) {
        $downloadUrl = $response.Headers.Location
        Write-Host "  [OK] Logs are available" -ForegroundColor Green
        Write-Host "  Download URL: $($downloadUrl.Substring(0, 50))..." -ForegroundColor Gray

        # Try to download the actual ZIP
        Write-Host ""
        Write-Host "  Downloading logs ZIP file..." -ForegroundColor Cyan
        $tempFile = [System.IO.Path]::GetTempFileName() + ".zip"
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile -ErrorAction Stop

        $fileSize = (Get-Item $tempFile).Length
        Write-Host "  [OK] Downloaded $fileSize bytes" -ForegroundColor Green

        # Try to extract and preview
        Write-Host ""
        Write-Host "  Extracting logs..." -ForegroundColor Cyan
        $extractPath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "github-logs-$RunId")
        if (Test-Path $extractPath) {
            Remove-Item $extractPath -Recurse -Force
        }
        Expand-Archive -Path $tempFile -DestinationPath $extractPath -Force

        $logFiles = Get-ChildItem -Path $extractPath -Recurse -Filter "*.txt"
        Write-Host "  [OK] Found $($logFiles.Count) log file(s)" -ForegroundColor Green
        foreach ($logFile in $logFiles) {
            $size = $logFile.Length
            Write-Host "  - $($logFile.FullName.Replace($extractPath, '')): $size bytes" -ForegroundColor Gray
        }

        # Cleanup
        Remove-Item $tempFile -Force
        Remove-Item $extractPath -Recurse -Force

        Write-Host ""
        Write-Host "============================================================" -ForegroundColor Green
        Write-Host "  SUCCESS - Logs are available and downloadable" -ForegroundColor Green
        Write-Host "============================================================" -ForegroundColor Green
    }
    else {
        Write-Host "  [WARN] Unexpected response: $($response.StatusCode)" -ForegroundColor Yellow
        Write-Host "  Body: $($response.Content)" -ForegroundColor Gray
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__

    if ($statusCode -eq 404) {
        Write-Host "  [FAIL] Logs not found (404)" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Possible reasons:" -ForegroundColor Yellow
        Write-Host "  1. Logs are not yet available (workflow just completed)" -ForegroundColor Gray
        Write-Host "  2. Workflow did not generate logs" -ForegroundColor Gray
        Write-Host "  3. Logs have expired or been deleted" -ForegroundColor Gray
        Write-Host "  4. Token lacks required permissions (needs 'repo' and 'workflow' scopes)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Try again in a few minutes if the workflow just completed." -ForegroundColor Yellow
    }
    else {
        Write-Host "  [FAIL] Error downloading logs" -ForegroundColor Red
        Write-Host "  Status: $statusCode" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "  FAILED - Logs not available" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red

    exit 1
}

Write-Host ""
