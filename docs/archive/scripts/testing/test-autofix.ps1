#!/usr/bin/env pwsh
# Test script for Auto-Fix PR Creation functionality

param(
    [string]$RepoOwner = "anishsingh0302",
    [string]$RepoName = "Semantic-Kernel",
    [string]$Branch = "master",
    [switch]$CleanupOnly,
    [switch]$SkipApiCheck
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Auto-Fix PR Testing Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to check if command exists
function Test-Command {
    param($Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Cleanup function
function Remove-AutoFixBranches {
    Write-Host "Fetching branches..." -ForegroundColor Yellow

    $branches = gh api "repos/$RepoOwner/$RepoName/branches" --jq '.[].name' | Where-Object { $_ -like "autofix/*" }

    if ($branches.Count -eq 0) {
        Write-Host "No auto-fix branches found." -ForegroundColor Green
        return
    }

    Write-Host "Found $($branches.Count) auto-fix branch(es):" -ForegroundColor Yellow
    foreach ($branch in $branches) {
        Write-Host "  - $branch" -ForegroundColor Gray
    }

    $confirm = Read-Host "Delete all auto-fix branches? (y/N)"
    if ($confirm -eq 'y') {
        foreach ($branch in $branches) {
            Write-Host "Deleting branch: $branch" -ForegroundColor Yellow
            gh api -X DELETE "repos/$RepoOwner/$RepoName/git/refs/heads/$branch" 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✓ Deleted" -ForegroundColor Green
            } else {
                Write-Host "  ✗ Failed to delete" -ForegroundColor Red
            }
        }
    }
}

# If cleanup only, run and exit
if ($CleanupOnly) {
    Remove-AutoFixBranches
    exit 0
}

# Step 1: Check prerequisites
Write-Host "Step 1: Checking prerequisites..." -ForegroundColor Cyan

# Check for required commands
$requiredCommands = @("gh", "dotnet", "curl")
foreach ($cmd in $requiredCommands) {
    if (Test-Command $cmd) {
        Write-Host "  ✓ $cmd is available" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $cmd is not available. Please install it." -ForegroundColor Red
        exit 1
    }
}

# Check GitHub authentication
Write-Host "  Checking GitHub authentication..." -ForegroundColor Gray
$ghStatus = gh auth status 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ GitHub CLI is authenticated" -ForegroundColor Green
} else {
    Write-Host "  ✗ GitHub CLI is not authenticated. Run 'gh auth login'" -ForegroundColor Red
    exit 1
}

# Check GitHub token in environment
if ($env:GITHUB__PERSONALACCESSTOKEN) {
    Write-Host "  ✓ GitHub token found in environment" -ForegroundColor Green
} else {
    Write-Host "  ⚠ GitHub token not found in GITHUB__PERSONALACCESSTOKEN" -ForegroundColor Yellow
    Write-Host "    Make sure it's set in appsettings.Development.json or environment" -ForegroundColor Yellow
}

# Check AWS credentials
Write-Host "  Checking AWS credentials..." -ForegroundColor Gray
$awsCheck = aws configure list 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ AWS credentials configured" -ForegroundColor Green
} else {
    Write-Host "  ⚠ AWS credentials not configured. Auto-fix won't work without Claude access." -ForegroundColor Yellow
}

Write-Host ""

# Step 2: Check API status
if (-not $SkipApiCheck) {
    Write-Host "Step 2: Checking API status..." -ForegroundColor Cyan

    try {
        $apiResponse = Invoke-RestMethod -Uri "https://localhost:7056/api/webhook/test" -SkipCertificateCheck
        Write-Host "  ✓ API is responding" -ForegroundColor Green
        Write-Host "    Orchestrator: $($apiResponse.OrchestratorConfigured)" -ForegroundColor Gray
        Write-Host "    Log Service: $($apiResponse.LogServiceConfigured)" -ForegroundColor Gray
        Write-Host "    Teams: $($apiResponse.TeamsNotificationConfigured)" -ForegroundColor Gray
    } catch {
        Write-Host "  ✗ API is not responding at https://localhost:7056" -ForegroundColor Red
        Write-Host "    Start the API with: dotnet run --project src/AgenticPipeline.Api" -ForegroundColor Yellow
        $continue = Read-Host "Continue anyway? (y/N)"
        if ($continue -ne 'y') {
            exit 1
        }
    }
    Write-Host ""
}

# Step 3: Check configuration
Write-Host "Step 3: Checking configuration..." -ForegroundColor Cyan

$configPath = Join-Path (Split-Path $PSScriptRoot -Parent) "src/AgenticPipeline.Api/appsettings.Development.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json

    Write-Host "  Notifications.CreateAutoFixPRs: $($config.Notifications.CreateAutoFixPRs)" -ForegroundColor Gray
    Write-Host "  Notifications.MinConfidenceForAutoFixPR: $($config.Notifications.MinConfidenceForAutoFixPR)" -ForegroundColor Gray

    if ($config.Notifications.CreateAutoFixPRs -eq $true) {
        Write-Host "  ✓ Auto-fix is enabled" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Auto-fix is disabled in configuration" -ForegroundColor Red
        Write-Host "    Set 'CreateAutoFixPRs: true' in appsettings.Development.json" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "  ⚠ Could not find configuration file" -ForegroundColor Yellow
}

Write-Host ""

# Step 4: Show testing options
Write-Host "Step 4: Test scenario selection" -ForegroundColor Cyan
Write-Host ""
Write-Host "Available test scenarios:" -ForegroundColor White
Write-Host ""
Write-Host "  1. Simple Syntax Error (Recommended)" -ForegroundColor Yellow
Write-Host "     - Missing using statement" -ForegroundColor Gray
Write-Host "     - Easy for Claude to fix" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Check Recent Workflow Runs" -ForegroundColor Yellow
Write-Host "     - View recent failures" -ForegroundColor Gray
Write-Host "     - Check if any qualify for auto-fix" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Monitor Live Logs" -ForegroundColor Yellow
Write-Host "     - Watch API logs in real-time" -ForegroundColor Gray
Write-Host "     - See auto-fix process" -ForegroundColor Gray
Write-Host ""
Write-Host "  4. View Auto-Fix PRs" -ForegroundColor Yellow
Write-Host "     - List open auto-fix PRs" -ForegroundColor Gray
Write-Host "     - Review created PRs" -ForegroundColor Gray
Write-Host ""
Write-Host "  5. Cleanup Auto-Fix Branches" -ForegroundColor Yellow
Write-Host "     - Remove old test branches" -ForegroundColor Gray
Write-Host ""
Write-Host "  6. Check Database" -ForegroundColor Yellow
Write-Host "     - View recent failures in DB" -ForegroundColor Gray
Write-Host "     - Check confidence scores" -ForegroundColor Gray
Write-Host ""

$choice = Read-Host "Select option (1-6, or Q to quit)"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "Creating test scenario: Missing using statement" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "To test this scenario:" -ForegroundColor White
        Write-Host ""
        Write-Host "1. Create a file in your repo with this content:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host @"
namespace TestAutoFix;

public class SimpleService
{
    public string SerializeData(object data)
    {
        // Missing: using System.Text.Json;
        return JsonSerializer.Serialize(data);
    }
}
"@ -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Create a workflow that builds this file" -ForegroundColor Yellow
        Write-Host "3. Push and trigger the workflow" -ForegroundColor Yellow
        Write-Host "4. Watch API logs for auto-fix creation" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Expected result: PR with 'using System.Text.Json;' added" -ForegroundColor Green
    }

    "2" {
        Write-Host ""
        Write-Host "Recent workflow runs:" -ForegroundColor Cyan
        gh run list --repo "$RepoOwner/$RepoName" --limit 10 --json "conclusion,displayTitle,headBranch,startedAt,workflowName,databaseId" --jq '.[] | select(.conclusion=="failure")'
    }

    "3" {
        Write-Host ""
        Write-Host "Monitoring API logs (press Ctrl+C to stop)..." -ForegroundColor Cyan
        Write-Host "Look for lines containing: 'auto-fix', 'AutoFixService', 'Created PR'" -ForegroundColor Gray
        Write-Host ""

        # If API is running locally
        $apiProcess = Get-Process -Name "AgenticPipeline.Api" -ErrorAction SilentlyContinue
        if ($apiProcess) {
            Write-Host "Note: API is running. Check console where you started it." -ForegroundColor Yellow
        } else {
            Write-Host "API is not running. Start it with:" -ForegroundColor Yellow
            Write-Host "  dotnet run --project src/AgenticPipeline.Api" -ForegroundColor Gray
        }
    }

    "4" {
        Write-Host ""
        Write-Host "Auto-Fix Pull Requests:" -ForegroundColor Cyan
        $prs = gh pr list --repo "$RepoOwner/$RepoName" --json "number,title,headRefName,url" --jq '.[] | select(.headRefName | startswith("autofix/"))'

        if ($prs) {
            $prs | ConvertFrom-Json | ForEach-Object {
                Write-Host ""
                Write-Host "  PR #$($_.number): $($_.title)" -ForegroundColor Green
                Write-Host "  Branch: $($_.headRefName)" -ForegroundColor Gray
                Write-Host "  URL: $($_.url)" -ForegroundColor Blue
            }
        } else {
            Write-Host "  No auto-fix PRs found." -ForegroundColor Yellow
            Write-Host "  (PRs on branches starting with 'autofix/')" -ForegroundColor Gray
        }
    }

    "5" {
        Write-Host ""
        Remove-AutoFixBranches
    }

    "6" {
        Write-Host ""
        Write-Host "Recent failures from database:" -ForegroundColor Cyan
        $dbPath = Join-Path (Split-Path $PSScriptRoot -Parent) "src/AgenticPipeline.Api/pipeline.db"

        if (Test-Path $dbPath) {
            $query = "SELECT
                FailureId,
                ErrorType,
                ROUND(ConfidenceScore, 2) as Confidence,
                datetime(AnalyzedAt, 'localtime') as AnalyzedAt,
                substr(RootCause, 1, 50) as RootCause
            FROM Failures
            ORDER BY AnalyzedAt DESC
            LIMIT 10;"

            sqlite3 $dbPath $query -header -column
        } else {
            Write-Host "  Database not found at: $dbPath" -ForegroundColor Yellow
        }
    }

    default {
        Write-Host "Exiting..." -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "For detailed testing guide, see:" -ForegroundColor White
Write-Host "docs/testing/Auto_Fix_Testing_Guide.md" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Cyan
