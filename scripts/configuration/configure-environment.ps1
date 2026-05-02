#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Configures environment variables for Agentic Pipeline

.DESCRIPTION
    This script securely sets AWS credentials, GitHub credentials, Azure DevOps credentials,
    webhook secrets, and Teams notification URLs as USER-level environment variables for use
    in tests and development.

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

.PARAMETER AzureDevOpsPAT
    Optional Azure DevOps Personal Access Token for fetching build logs
    Note: Organization and Project names are configured in appsettings.json (not confidential)

.PARAMETER AWSProfileName
    Optional AWS SSO profile name (default: "default")

.PARAMETER AWSRegion
    Optional AWS region (default: "us-east-1")

.PARAMETER SkipAWS
    Skip AWS credential configuration

.EXAMPLE
    .\configure-environment.ps1 -Token "ghp_yourtoken" -Owner "anishsingh0302" -Repo "Semantic-Kernel"

.EXAMPLE
    .\configure-environment.ps1 -Token "ghp_yourtoken" -Owner "anishsingh0302" -Repo "Semantic-Kernel" -WebhookSecret "my-secret-123"

.EXAMPLE
    .\configure-environment.ps1 -Token "ghp_yourtoken" -Owner "anishsingh0302" -Repo "Semantic-Kernel" -AzureDevOpsPAT "your-azure-pat"

.EXAMPLE
    # Full configuration with all options including AWS
    .\configure-environment.ps1 `
        -Token "ghp_yourtoken" `
        -Owner "anishsingh0302" `
        -Repo "Semantic-Kernel" `
        -WebhookSecret "my-secret-123" `
        -TeamsWebhookUrl "https://your-org.webhook.office.com/webhookb2/..." `
        -AzureDevOpsPAT "your-azure-pat" `
        -AWSProfileName "my-aws-profile" `
        -AWSRegion "us-west-2"

.EXAMPLE
    # Skip AWS configuration
    .\configure-environment.ps1 -Token "ghp_yourtoken" -SkipAWS
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
    [string]$TeamsWebhookUrl = "",

    [Parameter(Mandatory=$false)]
    [string]$AzureDevOpsPAT = "",

    [Parameter(Mandatory=$false)]
    [string]$AWSProfileName = "default",

    [Parameter(Mandatory=$false)]
    [string]$AWSRegion = "us-east-1",

    [Parameter(Mandatory=$false)]
    [switch]$SkipAWS
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Agentic Pipeline Environment Configuration" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# AWS Configuration
# ============================================================
if (-not $SkipAWS) {
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host "  AWS SSO Credential Configuration" -ForegroundColor Yellow
    Write-Host "============================================================" -ForegroundColor Yellow
    Write-Host ""

    # Check if AWS CLI is installed
    try {
        $awsVersion = aws --version 2>&1
        Write-Host "AWS CLI Version: $awsVersion" -ForegroundColor Green
    } catch {
        Write-Host "WARNING: AWS CLI not found. Skipping AWS configuration." -ForegroundColor Yellow
        Write-Host "Install AWS CLI from: https://aws.amazon.com/cli/" -ForegroundColor Yellow
        Write-Host ""
        $SkipAWS = $true
    }

    if (-not $SkipAWS) {
        # Login to SSO
        Write-Host "Step 1: Ensuring SSO login..." -ForegroundColor Cyan
        Write-Host "Checking if already logged in..."

        $identity = aws sts get-caller-identity --profile $AWSProfileName 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Not logged in. Running 'aws sso login'..." -ForegroundColor Yellow
            aws sso login --profile $AWSProfileName

            if ($LASTEXITCODE -ne 0) {
                Write-Host "ERROR: SSO login failed" -ForegroundColor Red
                Write-Host "Skipping AWS configuration..." -ForegroundColor Yellow
                Write-Host ""
                $SkipAWS = $true
            }
        } else {
            Write-Host "Already logged in!" -ForegroundColor Green
        }

        if (-not $SkipAWS) {
            # Get credentials from SSO cache
            Write-Host "`nStep 2: Retrieving credentials from SSO cache..." -ForegroundColor Cyan

            # Try to find credentials in the CLI cache directory
            $cacheDir = Join-Path $env:USERPROFILE ".aws\cli\cache"
            $credentialsFound = $false

            if (Test-Path $cacheDir) {
                $cacheFiles = Get-ChildItem -Path $cacheDir -Filter "*.json" | Sort-Object LastWriteTime -Descending

                foreach ($file in $cacheFiles) {
                    try {
                        $content = Get-Content $file.FullName -Raw | ConvertFrom-Json
                        if ($content.Credentials) {
                            Write-Host "Found cached credentials!" -ForegroundColor Green

                            # Set USER-level environment variables (persist across sessions)
                            [System.Environment]::SetEnvironmentVariable("AWS_ACCESS_KEY_ID", $content.Credentials.AccessKeyId, "User")
                            [System.Environment]::SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", $content.Credentials.SecretAccessKey, "User")
                            [System.Environment]::SetEnvironmentVariable("AWS_SESSION_TOKEN", $content.Credentials.SessionToken, "User")

                            # Also set for current session
                            $env:AWS_ACCESS_KEY_ID = $content.Credentials.AccessKeyId
                            $env:AWS_SECRET_ACCESS_KEY = $content.Credentials.SecretAccessKey
                            $env:AWS_SESSION_TOKEN = $content.Credentials.SessionToken

                            $credentialsFound = $true
                            break
                        }
                    } catch {
                        # Skip invalid cache files
                        continue
                    }
                }
            }

            # If cache method didn't work, try using profile
            if (-not $credentialsFound) {
                Write-Host "Could not extract credentials from cache." -ForegroundColor Yellow
                Write-Host "Setting AWS_PROFILE instead..." -ForegroundColor Yellow
                [System.Environment]::SetEnvironmentVariable("AWS_PROFILE", $AWSProfileName, "User")
                [System.Environment]::SetEnvironmentVariable("AWS_SDK_LOAD_CONFIG", "1", "User")
                $env:AWS_PROFILE = $AWSProfileName
                $env:AWS_SDK_LOAD_CONFIG = "1"
            }

            # Set region
            [System.Environment]::SetEnvironmentVariable("AWS_REGION", $AWSRegion, "User")
            [System.Environment]::SetEnvironmentVariable("AWS_DEFAULT_REGION", $AWSRegion, "User")
            $env:AWS_REGION = $AWSRegion
            $env:AWS_DEFAULT_REGION = $AWSRegion

            # Verify credentials work
            Write-Host "`nStep 3: Verifying AWS credentials..." -ForegroundColor Cyan
            $verifyIdentity = aws sts get-caller-identity --profile $AWSProfileName 2>&1

            if ($LASTEXITCODE -eq 0) {
                Write-Host "AWS Credentials verified!" -ForegroundColor Green
                $identityObj = $verifyIdentity | ConvertFrom-Json
                Write-Host "  Logged in as: $($identityObj.Arn)" -ForegroundColor White
                Write-Host "  Account: $($identityObj.Account)" -ForegroundColor White
            } else {
                Write-Host "WARNING: Could not verify credentials" -ForegroundColor Yellow
            }

            # Display what was set
            Write-Host "`nAWS Environment Variables Set (USER level - persistent):" -ForegroundColor Cyan

            if ($env:AWS_ACCESS_KEY_ID) {
                $maskedKey = $env:AWS_ACCESS_KEY_ID.Substring(0, [Math]::Min(10, $env:AWS_ACCESS_KEY_ID.Length)) + "..."
                Write-Host "  AWS_ACCESS_KEY_ID:     $maskedKey" -ForegroundColor White
                Write-Host "  AWS_SECRET_ACCESS_KEY: ****" -ForegroundColor White
                Write-Host "  AWS_SESSION_TOKEN:     Set" -ForegroundColor White
            } else {
                Write-Host "  AWS_PROFILE:           $env:AWS_PROFILE" -ForegroundColor White
                Write-Host "  AWS_SDK_LOAD_CONFIG:   $env:AWS_SDK_LOAD_CONFIG" -ForegroundColor White
            }

            Write-Host "  AWS_REGION:            $env:AWS_REGION" -ForegroundColor White
            Write-Host ""
        }
    }
} else {
    Write-Host "Skipping AWS configuration..." -ForegroundColor Gray
    Write-Host ""
}

# ============================================================
# GitHub, Azure DevOps, and Teams Configuration
# ============================================================
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "  GitHub, Azure DevOps & Teams Configuration" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Yellow
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

    # Set Azure DevOps PAT if provided
    if (-not [string]::IsNullOrWhiteSpace($AzureDevOpsPAT)) {
        [Environment]::SetEnvironmentVariable("AzureDevOps__PersonalAccessToken", $AzureDevOpsPAT, "User")
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

    # Set Azure DevOps PAT for current session if provided
    if (-not [string]::IsNullOrWhiteSpace($AzureDevOpsPAT)) {
        $env:AzureDevOps__PersonalAccessToken = $AzureDevOpsPAT
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

    Write-Host "Azure DevOps Configuration:" -ForegroundColor Cyan
    if (-not [string]::IsNullOrWhiteSpace($AzureDevOpsPAT)) {
        $maskedAzurePAT = if ($AzureDevOpsPAT.Length -gt 15) { "$($AzureDevOpsPAT.Substring(0, 15))..." } else { "***" }
        Write-Host "  AzureDevOps__PersonalAccessToken = $maskedAzurePAT" -ForegroundColor White
        Write-Host "  (Organization and Project are configured in appsettings.json)" -ForegroundColor Gray
    } else {
        Write-Host "  AzureDevOps__PersonalAccessToken = (not set)" -ForegroundColor Gray
        Write-Host "  Note: Azure DevOps integration requires PAT to be set" -ForegroundColor Yellow
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
    Write-Host "  Configuration Summary" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""

    if (-not $SkipAWS -and $env:AWS_REGION) {
        Write-Host "AWS Credentials:              Configured" -ForegroundColor Green
    } else {
        Write-Host "AWS Credentials:              Not configured" -ForegroundColor Gray
    }
    Write-Host "GitHub Token:                 Set" -ForegroundColor Green
    Write-Host "Azure DevOps PAT:             $(if ($AzureDevOpsPAT) {'Set'} else {'Not set'})" -ForegroundColor $(if ($AzureDevOpsPAT) {'Green'} else {'Gray'})
    Write-Host "Teams Webhook:                $(if ($TeamsWebhookUrl) {'Set'} else {'Not set'})" -ForegroundColor $(if ($TeamsWebhookUrl) {'Green'} else {'Gray'})
    Write-Host ""

    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "  Next Steps:" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  1. Run the API:" -ForegroundColor White
    Write-Host "     cd C:\Anish\Source\Agentic_Pipeline" -ForegroundColor Gray
    Write-Host "     dotnet run --project src/AgenticPipeline.Api" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. Test the API:" -ForegroundColor White
    Write-Host "     Open browser: https://localhost:7056" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. Configure webhooks (optional):" -ForegroundColor White
    Write-Host "     - GitHub: Settings -> Webhooks -> Add webhook" -ForegroundColor Gray
    Write-Host "     - Azure DevOps: Project Settings -> Service Hooks" -ForegroundColor Gray
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
    Write-Host "     echo `$env:AWS_REGION" -ForegroundColor Gray
    Write-Host "     echo `$env:GITHUB_PAT" -ForegroundColor Gray
    Write-Host "     echo `$env:AzureDevOps__PersonalAccessToken" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Or run verification script:" -ForegroundColor White
    Write-Host "     .\\scripts\\utils\\test-env.ps1" -ForegroundColor Gray
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "  All environment variables set successfully!" -ForegroundColor Green
    Write-Host "  Variables are persistent (USER level)" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""

    # Launch Visual Studio with the solution
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "  Launching Visual Studio..." -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""

    $solutionPath = "C:\Anish\Source\Agentic_Pipeline\AgenticPipeline.sln"

    # Find Visual Studio (try multiple possible paths)
    $vsPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe"
    )

    $vsFound = $false
    foreach ($vsPath in $vsPaths) {
        if (Test-Path $vsPath) {
            Write-Host "Found Visual Studio at: $vsPath" -ForegroundColor Green
            Write-Host "Opening solution..." -ForegroundColor Green
            Start-Process -FilePath $vsPath -ArgumentList $solutionPath
            Write-Host ""
            Write-Host "Visual Studio launched with the solution!" -ForegroundColor Green
            Write-Host "Press F5 in Visual Studio to run the API" -ForegroundColor White
            $vsFound = $true
            break
        }
    }

    if (-not $vsFound) {
        Write-Host "Could not find Visual Studio automatically." -ForegroundColor Yellow
        Write-Host "Please open Visual Studio manually and load the solution:" -ForegroundColor Yellow
        Write-Host "  $solutionPath" -ForegroundColor White
        Write-Host ""
        Write-Host "Or run the API from command line:" -ForegroundColor Yellow
        Write-Host "  cd C:\Anish\Source\Agentic_Pipeline\src\AgenticPipeline.Api" -ForegroundColor White
        Write-Host "  dotnet run" -ForegroundColor White
    }

    Write-Host ""

} catch {
    Write-Host "ERROR setting environment variables: $_" -ForegroundColor Red
    exit 1
}
