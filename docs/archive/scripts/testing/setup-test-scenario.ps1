#!/usr/bin/env pwsh
# Script to set up test scenario for Auto-Fix testing

param(
    [Parameter(Mandatory=$false)]
    [string]$TargetRepo = "C:\Anish\Source\Semantic-Kernel",

    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Auto-Fix Test Scenario Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get the source directory (where this script is located)
$scriptDir = Split-Path -Parent $PSScriptRoot
$sourceTestFile = Join-Path $scriptDir "test-autofix.cs"
$sourceWorkflow = Join-Path $scriptDir ".github\workflows\test-autofix.yml"

# Check if target repository exists
if (-not (Test-Path $TargetRepo)) {
    Write-Host "Error: Target repository not found at: $TargetRepo" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please specify the correct path:" -ForegroundColor Yellow
    Write-Host "  .\scripts\setup-test-scenario.ps1 -TargetRepo 'C:\path\to\your\repo'" -ForegroundColor Gray
    exit 1
}

Write-Host "Target Repository: $TargetRepo" -ForegroundColor Green
Write-Host ""

# Check if files already exist
$targetTestFile = Join-Path $TargetRepo "test-autofix.cs"
$targetWorkflowDir = Join-Path $TargetRepo ".github\workflows"
$targetWorkflow = Join-Path $targetWorkflowDir "test-autofix.yml"

$filesExist = (Test-Path $targetTestFile) -or (Test-Path $targetWorkflow)

if ($filesExist -and -not $Force) {
    Write-Host "Warning: Test files already exist in target repository:" -ForegroundColor Yellow
    if (Test-Path $targetTestFile) {
        Write-Host "  - test-autofix.cs" -ForegroundColor Gray
    }
    if (Test-Path $targetWorkflow) {
        Write-Host "  - .github/workflows/test-autofix.yml" -ForegroundColor Gray
    }
    Write-Host ""
    $overwrite = Read-Host "Overwrite existing files? (y/N)"
    if ($overwrite -ne 'y') {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 0
    }
}

# Copy test file
Write-Host "Copying test-autofix.cs..." -ForegroundColor Cyan
Copy-Item $sourceTestFile $targetTestFile -Force
Write-Host "  ✓ Copied to: $targetTestFile" -ForegroundColor Green

# Create workflow directory if needed
if (-not (Test-Path $targetWorkflowDir)) {
    Write-Host "Creating .github/workflows directory..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $targetWorkflowDir -Force | Out-Null
}

# Copy workflow file
Write-Host "Copying test-autofix.yml..." -ForegroundColor Cyan
Copy-Item $sourceWorkflow $targetWorkflow -Force
Write-Host "  ✓ Copied to: $targetWorkflow" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host ""
Write-Host "1. Review the test file:" -ForegroundColor Yellow
Write-Host "   code $targetTestFile" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Commit and push the changes:" -ForegroundColor Yellow
Write-Host "   cd '$TargetRepo'" -ForegroundColor Gray
Write-Host "   git add test-autofix.cs .github/workflows/test-autofix.yml" -ForegroundColor Gray
Write-Host "   git commit -m 'Add auto-fix test scenario'" -ForegroundColor Gray
Write-Host "   git push" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Trigger the workflow:" -ForegroundColor Yellow
Write-Host "   - Go to GitHub Actions" -ForegroundColor Gray
Write-Host "   - Select 'Test Auto-Fix' workflow" -ForegroundColor Gray
Write-Host "   - Click 'Run workflow' button" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Watch API logs for auto-fix creation:" -ForegroundColor Yellow
Write-Host "   Look for: 'Created auto-fix PR #...'" -ForegroundColor Gray
Write-Host ""
Write-Host "Expected result:" -ForegroundColor White
Write-Host "  A PR will be created with 'using System.Text.Json;' added" -ForegroundColor Green
Write-Host ""

# Show file contents
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test File Preview" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Get-Content $targetTestFile | Select-Object -First 10
Write-Host "..." -ForegroundColor Gray
Write-Host ""
Write-Host "This file is missing: using System.Text.Json;" -ForegroundColor Yellow
Write-Host "The auto-fix should add it!" -ForegroundColor Green
Write-Host ""
