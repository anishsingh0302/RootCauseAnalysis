# set-sso-creds-persistent.ps1
# Extracts SSO credentials and sets them as USER-level environment variables
# These persist across all processes (PowerShell, Visual Studio, etc.)

param(
    [string]$ProfileName = "default",
    [string]$Region = "us-east-1"
)

Write-Host "=== AWS SSO Credential Helper (Persistent) ===" -ForegroundColor Cyan
Write-Host ""

# Check if AWS CLI is installed
try {
    $awsVersion = aws --version 2>&1
    Write-Host "AWS CLI Version: $awsVersion" -ForegroundColor Green
} catch {
    Write-Host "ERROR: AWS CLI not found. Please install it first." -ForegroundColor Red
    exit 1
}

# Login to SSO
Write-Host "`nStep 1: Ensuring SSO login..." -ForegroundColor Yellow
Write-Host "Checking if already logged in..."

$identity = aws sts get-caller-identity --profile $ProfileName 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Not logged in. Running 'aws sso login'..." -ForegroundColor Yellow
    aws sso login --profile $ProfileName

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: SSO login failed" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Already logged in!" -ForegroundColor Green
}

# Get credentials from SSO cache
Write-Host "`nStep 2: Retrieving credentials from SSO cache..." -ForegroundColor Yellow

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
    [System.Environment]::SetEnvironmentVariable("AWS_PROFILE", $ProfileName, "User")
    [System.Environment]::SetEnvironmentVariable("AWS_SDK_LOAD_CONFIG", "1", "User")
    $env:AWS_PROFILE = $ProfileName
    $env:AWS_SDK_LOAD_CONFIG = "1"
}

# Set region
[System.Environment]::SetEnvironmentVariable("AWS_REGION", $Region, "User")
[System.Environment]::SetEnvironmentVariable("AWS_DEFAULT_REGION", $Region, "User")
$env:AWS_REGION = $Region
$env:AWS_DEFAULT_REGION = $Region

# Verify credentials work
Write-Host "`nStep 3: Verifying credentials..." -ForegroundColor Yellow
$verifyIdentity = aws sts get-caller-identity --profile $ProfileName 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "Credentials verified!" -ForegroundColor Green
    $identityObj = $verifyIdentity | ConvertFrom-Json
    Write-Host "  Logged in as: $($identityObj.Arn)" -ForegroundColor White
    Write-Host "  Account: $($identityObj.Account)" -ForegroundColor White
} else {
    Write-Host "WARNING: Could not verify credentials" -ForegroundColor Yellow
    Write-Host "Will try to proceed anyway..." -ForegroundColor Yellow
}

# Display what was set
Write-Host "`nStep 4: Environment variables set (USER level - persistent):" -ForegroundColor Yellow

if ($env:AWS_ACCESS_KEY_ID) {
    $maskedKey = $env:AWS_ACCESS_KEY_ID.Substring(0, [Math]::Min(10, $env:AWS_ACCESS_KEY_ID.Length)) + "..."
    Write-Host "  AWS_ACCESS_KEY_ID:     $maskedKey" -ForegroundColor Green
    Write-Host "  AWS_SECRET_ACCESS_KEY: ****" -ForegroundColor Green
    Write-Host "  AWS_SESSION_TOKEN:     Set" -ForegroundColor Green
} else {
    Write-Host "  AWS_PROFILE:           $env:AWS_PROFILE" -ForegroundColor Green
    Write-Host "  AWS_SDK_LOAD_CONFIG:   $env:AWS_SDK_LOAD_CONFIG" -ForegroundColor Green
}

Write-Host "  AWS_REGION:            $env:AWS_REGION" -ForegroundColor Green

Write-Host "`n=== Ready! ===" -ForegroundColor Cyan
Write-Host "These credentials are now available system-wide." -ForegroundColor White
Write-Host ""
Write-Host "WARNING: These credentials will remain set until you:" -ForegroundColor Yellow
Write-Host "  - Run this script again with new credentials" -ForegroundColor Yellow
Write-Host "  - Or manually remove them from System Environment Variables" -ForegroundColor Yellow
Write-Host ""

# Launch Visual Studio with the solution
Write-Host "Step 5: Launching Visual Studio..." -ForegroundColor Yellow

#$solutionPath = "C:\Anish\Source\Agentic_Pipeline\tests\BedrockConnectionTest\BedrockConnectionTest.sln"
$solutionPath = "C:\Anish\Source\Agentic_Pipeline\AgenticPipeline.sln"

# Find Visual Studio Professional 2022
#$vsPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe"
$vsPath = "${env:ProgramFiles}\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe"

if (Test-Path $vsPath) {
    Write-Host "Found Visual Studio at: $vsPath" -ForegroundColor Green
    Write-Host "Opening solution..." -ForegroundColor Green
    Start-Process -FilePath $vsPath -ArgumentList $solutionPath
    Write-Host ""
    Write-Host "Visual Studio launched with the solution!" -ForegroundColor Green
    Write-Host "Press F5 in Visual Studio to run the test" -ForegroundColor White
} else {
    Write-Host "Could not find Visual Studio automatically." -ForegroundColor Yellow
    Write-Host "Please open Visual Studio manually and load the solution" -ForegroundColor Yellow
}

Write-Host ""
