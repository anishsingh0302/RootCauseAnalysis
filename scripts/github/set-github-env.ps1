#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Sets GitHub credentials as environment variables for testing

.DESCRIPTION
    This script securely sets your GitHub Personal Access Token and default repo
    as USER-level environment variables for use in tests and development.

.PARAMETER Token
    Your GitHub Personal Access Token (starts with ghp_ or github_pat_)

.PARAMETER Owner
    Your GitHub username (default repo owner)

.PARAMETER Repo
    Your default repository name

.EXAMPLE
    .\set-github-env.ps1 -Token "ghp_yourtoken" -Owner "anishsingh0302" -Repo "Semantic-Kernel"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Token,

    [Parameter(Mandatory=$false)]
    [string]$Owner = "anishsingh0302",

    [Parameter(Mandatory=$false)]
    [string]$Repo = "Semantic-Kernel"
)

Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  GitHub Environment Variables Setup" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Validate token format
if (-not ($Token -match '^(ghp_|github_pat_)')) {
    Write-Host "⚠️  Warning: Token doesn't start with 'ghp_' or 'github_pat_'" -ForegroundColor Yellow
    Write-Host "   Make sure you're using a valid GitHub Personal Access Token" -ForegroundColor Yellow
    Write-Host ""
}

# Set environment variables at USER level (persistent)
Write-Host "Setting environment variables..." -ForegroundColor Green

try {
    # For API (uses Section__Key format)
    [Environment]::SetEnvironmentVariable("GitHub__PersonalAccessToken", $Token, "User")
    [Environment]::SetEnvironmentVariable("GitHub__DefaultOwner", $Owner, "User")
    [Environment]::SetEnvironmentVariable("GitHub__DefaultRepo", $Repo, "User")

    # For Tests (simple format)
    [Environment]::SetEnvironmentVariable("GITHUB_PAT", $Token, "User")
    [Environment]::SetEnvironmentVariable("GITHUB_OWNER", $Owner, "User")
    [Environment]::SetEnvironmentVariable("GITHUB_REPO", $Repo, "User")

    # Also set for current session
    $env:GitHub__PersonalAccessToken = $Token
    $env:GitHub__DefaultOwner = $Owner
    $env:GitHub__DefaultRepo = $Repo
    $env:GITHUB_PAT = $Token
    $env:GITHUB_OWNER = $Owner
    $env:GITHUB_REPO = $Repo

    Write-Host "✅ Environment variables set successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Variables set (for API):" -ForegroundColor Cyan
    Write-Host "  GitHub__PersonalAccessToken = $($Token.Substring(0, 15))..." -ForegroundColor White
    Write-Host "  GitHub__DefaultOwner        = $Owner" -ForegroundColor White
    Write-Host "  GitHub__DefaultRepo         = $Repo" -ForegroundColor White
    Write-Host ""
    Write-Host "Variables set (for Tests):" -ForegroundColor Cyan
    Write-Host "  GITHUB_PAT   = $($Token.Substring(0, 15))..." -ForegroundColor White
    Write-Host "  GITHUB_OWNER = $Owner" -ForegroundColor White
    Write-Host "  GITHUB_REPO  = $Repo" -ForegroundColor White
    Write-Host ""
    Write-Host "✅ Variables are set at USER level (persistent across sessions)" -ForegroundColor Green
    Write-Host ""

    # Verify
    $verifyPat = [Environment]::GetEnvironmentVariable("GITHUB_PAT", "User")
    $verifyOwner = [Environment]::GetEnvironmentVariable("GITHUB_OWNER", "User")
    $verifyRepo = [Environment]::GetEnvironmentVariable("GITHUB_REPO", "User")

    if ($verifyPat -eq $Token -and $verifyOwner -eq $Owner -and $verifyRepo -eq $Repo) {
        Write-Host "✅ Verification successful! Variables persisted correctly." -ForegroundColor Green
    } else {
        Write-Host "⚠️  Warning: Verification failed. Variables might not have persisted." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Next Steps:" -ForegroundColor Cyan
    Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  1. Close and reopen any open terminals/IDEs" -ForegroundColor White
    Write-Host "  2. Run tests:" -ForegroundColor White
    Write-Host "     cd C:\Anish\Source\Agentic_Pipeline" -ForegroundColor Gray
    Write-Host "     dotnet test tests/AgenticPipeline.IntegrationTests" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  To verify variables in a new terminal:" -ForegroundColor White
    Write-Host "     echo `$env:GITHUB_PAT" -ForegroundColor Gray
    Write-Host ""

} catch {
    Write-Host "❌ Error setting environment variables: $_" -ForegroundColor Red
    exit 1
}
