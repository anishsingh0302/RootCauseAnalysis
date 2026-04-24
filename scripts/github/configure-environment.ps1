#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Configures environment variables for Agentic Pipeline

.DESCRIPTION
    This script securely sets GitHub credentials, webhook secrets, and Teams notification URLs
    as USER-level environment variables for use in tests and development.

.PARAMETER Token
    Your GitHub Personal Access Token (starts with ghp_ or github_pat_)

.PARAMETER Owner
    Your GitHub username (default repo owner)

.PARAMETER Repo
    Your default repository name

.PARAMETER WebhookSecret
    Optional webhook secret for GitHub webhook signature verification

.PARAMETER TeamsWebhookUrl
    Optional Microsoft Teams incoming webhook URL for failure notifications

.EXAMPLE
    .\configure-environment.ps1 -Token "ghp_yourtoken" -Owner "anishsingh0302" -Repo "AgenticPipeline"

.EXAMPLE
    .\configure-environment.ps1 -Token "ghp_yourtoken" -Owner "anishsingh0302" -Repo "AgenticPipeline" -WebhookSecret "my-secret-123"

.EXAMPLE
    .\configure-environment.ps1 -Token "ghp_yourtoken" -Owner "anishsingh0302" -Repo "AgenticPipeline" -WebhookSecret "my-secret-123" -TeamsWebhookUrl "https://your-org.webhook.office.com/webhookb2/..."
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Token,

    [Parameter(Mandatory=$false)]
    [string]$Owner = "anishsingh0302",

    [Parameter(Mandatory=$false)]
    [string]$Repo = "AgenticPipeline",

    [Parameter(Mandatory=$false)]
    [string]$WebhookSecret = "",

    [Parameter(Mandatory=$false)]
    [string]$TeamsWebhookUrl = ""
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Agentic Pipeline Environment Configuration" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Validate token format
if (-not ($Token -match '^(ghp_|github_pat_)')) {
    Write-Host "WARNING: Token doesn't start with 'ghp_' or 'github_pat_'" -ForegroundColor Yellow
    Write-Host "Make sure you're using a valid GitHub Personal Access Token" -ForegroundColor Yellow
    Write-Host ""
}

# Validate Teams webhook URL format if provided
if (-not [string]::IsNullOrWhiteSpace($TeamsWebhookUrl)) {
    if (-not ($TeamsWebhookUrl -match '^https://.*\.webhook\.office\.com/')) {
        Write-Host "WARNING: The Teams URL doesn't look like a valid webhook URL" -ForegroundColor Yellow
        Write-Host "Expected format: https://your-org.webhook.office.com/webhookb2/..." -ForegroundColor Yellow
        Write-Host ""
    }
}

# Set environment variables at USER level (persistent)
Write-Host "Setting environment variables..." -ForegroundColor Green

try {
    # For API (uses Section__Key format)
    [Environment]::SetEnvironmentVariable("GitHub__PersonalAccessToken", $Token, "User")
    [Environment]::SetEnvironmentVariable("GitHub__DefaultOwner", $Owner, "User")
    [Environment]::SetEnvironmentVariable("GitHub__DefaultRepo", $Repo, "User")

    # Set webhook secret if provided
    if (-not [string]::IsNullOrWhiteSpace($WebhookSecret)) {
        [Environment]::SetEnvironmentVariable("GitHub__WebhookSecret", $WebhookSecret, "User")
    }

    # Set Teams webhook URL if provided
    if (-not [string]::IsNullOrWhiteSpace($TeamsWebhookUrl)) {
        [Environment]::SetEnvironmentVariable("Notifications__TeamsWebhookUrl", $TeamsWebhookUrl, "User")
    }

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

    # Set webhook secret for current session if provided
    if (-not [string]::IsNullOrWhiteSpace($WebhookSecret)) {
        $env:GitHub__WebhookSecret = $WebhookSecret
    }

    # Set Teams webhook URL for current session if provided
    if (-not [string]::IsNullOrWhiteSpace($TeamsWebhookUrl)) {
        $env:Notifications__TeamsWebhookUrl = $TeamsWebhookUrl
    }

    Write-Host "Environment variables set successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "GitHub Configuration:" -ForegroundColor Cyan
    Write-Host "  GitHub__PersonalAccessToken = $($Token.Substring(0, 15))..." -ForegroundColor White
    Write-Host "  GitHub__DefaultOwner        = $Owner" -ForegroundColor White
    Write-Host "  GitHub__DefaultRepo         = $Repo" -ForegroundColor White
    if (-not [string]::IsNullOrWhiteSpace($WebhookSecret)) {
        $maskedSecret = if ($WebhookSecret.Length -gt 10) { "$($WebhookSecret.Substring(0, 10))..." } else { "***" }
        Write-Host "  GitHub__WebhookSecret       = $maskedSecret" -ForegroundColor White
    } else {
        Write-Host "  GitHub__WebhookSecret       = (not set)" -ForegroundColor Gray
    }
    Write-Host ""

    Write-Host "Teams Notification Configuration:" -ForegroundColor Cyan
    if (-not [string]::IsNullOrWhiteSpace($TeamsWebhookUrl)) {
        $maskedTeamsUrl = if ($TeamsWebhookUrl.Length -gt 50) { "$($TeamsWebhookUrl.Substring(0, 50))..." } else { "***" }
        Write-Host "  Notifications__TeamsWebhookUrl = $maskedTeamsUrl" -ForegroundColor White
    } else {
        Write-Host "  Notifications__TeamsWebhookUrl = (not set)" -ForegroundColor Gray
    }
    Write-Host ""

    Write-Host "Test Variables:" -ForegroundColor Cyan
    Write-Host "  GITHUB_PAT   = $($Token.Substring(0, 15))..." -ForegroundColor White
    Write-Host "  GITHUB_OWNER = $Owner" -ForegroundColor White
    Write-Host "  GITHUB_REPO  = $Repo" -ForegroundColor White
    Write-Host ""
    Write-Host "Variables are set at USER level (persistent across sessions)" -ForegroundColor Green
    Write-Host ""

    # Verify
    $verifyPat = [Environment]::GetEnvironmentVariable("GITHUB_PAT", "User")
    $verifyOwner = [Environment]::GetEnvironmentVariable("GITHUB_OWNER", "User")
    $verifyRepo = [Environment]::GetEnvironmentVariable("GITHUB_REPO", "User")
    $verifyWebhookSecret = [Environment]::GetEnvironmentVariable("GitHub__WebhookSecret", "User")
    $verifyTeamsUrl = [Environment]::GetEnvironmentVariable("Notifications__TeamsWebhookUrl", "User")

    $webhookSecretMatch = if ([string]::IsNullOrWhiteSpace($WebhookSecret)) {
        $true  # No secret provided, so no verification needed
    } else {
        $verifyWebhookSecret -eq $WebhookSecret
    }

    $teamsUrlMatch = if ([string]::IsNullOrWhiteSpace($TeamsWebhookUrl)) {
        $true  # No URL provided, so no verification needed
    } else {
        $verifyTeamsUrl -eq $TeamsWebhookUrl
    }

    if ($verifyPat -eq $Token -and $verifyOwner -eq $Owner -and $verifyRepo -eq $Repo -and $webhookSecretMatch -and $teamsUrlMatch) {
        Write-Host "Verification successful! Variables persisted correctly." -ForegroundColor Green
    } else {
        Write-Host "WARNING: Verification failed. Variables might not have persisted." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "  Next Steps:" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  1. Restart your API if currently running:" -ForegroundColor White
    Write-Host "     cd C:\Anish\Source\Agentic_Pipeline" -ForegroundColor Gray
    Write-Host "     dotnet run --project src/AgenticPipeline.Api" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. Verify webhook configuration:" -ForegroundColor White
    Write-Host "     curl https://localhost:7056/api/webhook/test" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. Test webhook with signature:" -ForegroundColor White
    Write-Host "     cd scripts" -ForegroundColor Gray
    Write-Host "     .\test-webhook.ps1 -IncludeSignature -WebhookSecret 'your-secret'" -ForegroundColor Gray
    Write-Host ""

    if (-not [string]::IsNullOrWhiteSpace($WebhookSecret)) {
        Write-Host "  GitHub Webhook Configuration:" -ForegroundColor Yellow
        Write-Host "     Use the SAME secret when configuring GitHub webhook" -ForegroundColor Gray
        Write-Host "     GitHub Repo -> Settings -> Webhooks -> Secret field" -ForegroundColor Gray
        Write-Host ""
    }

    if (-not [string]::IsNullOrWhiteSpace($TeamsWebhookUrl)) {
        Write-Host "  Teams Notification:" -ForegroundColor Yellow
        Write-Host "     Failure notifications will be sent to your Teams channel" -ForegroundColor Gray
        Write-Host "     Configure notification settings in appsettings.json" -ForegroundColor Gray
        Write-Host ""
    }

    Write-Host "  To verify variables in a new terminal:" -ForegroundColor White
    Write-Host "     echo `$env:GITHUB_PAT" -ForegroundColor Gray
    Write-Host "     echo `$env:Notifications__TeamsWebhookUrl" -ForegroundColor Gray
    Write-Host ""

} catch {
    Write-Host "ERROR setting environment variables: $_" -ForegroundColor Red
    exit 1
}
