# aws-helper.ps1
# Helper script for common AWS operations with proper error handling

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('info', 'list-keys', 'create-key', 'check-bedrock', 'verify-all', 'help')]
    [string]$Command = 'help'
)

function Show-Help {
    Write-Host @"
AWS Helper Script - Common Operations Made Easy

Usage: .\aws-helper.ps1 -Command <command>

Commands:
  info          - Show your AWS identity and username
  list-keys     - List your access keys
  create-key    - Create a new access key
  check-bedrock - Check Bedrock model access
  verify-all    - Run complete verification
  help          - Show this help message

Examples:
  .\aws-helper.ps1 -Command info
  .\aws-helper.ps1 -Command list-keys
  .\aws-helper.ps1 -Command verify-all

"@ -ForegroundColor Cyan
}

function Get-MyUserName {
    try {
        $identity = aws sts get-caller-identity --output json 2>&1 | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to get identity"
        }
        return $identity.Arn.Split('/')[-1]
    }
    catch {
        Write-Host "Error: Cannot get your AWS identity. Make sure you've run 'aws configure'" -ForegroundColor Red
        return $null
    }
}

function Show-Info {
    Write-Host "`n=== AWS Identity Information ===" -ForegroundColor Cyan

    try {
        $identityJson = aws sts get-caller-identity --output json 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to get identity"
        }

        $identity = $identityJson | ConvertFrom-Json
        $userName = $identity.Arn.Split('/')[-1]

        Write-Host "`n✓ Successfully authenticated!" -ForegroundColor Green
        Write-Host "  User ARN:    $($identity.Arn)" -ForegroundColor White
        Write-Host "  User Name:   $userName" -ForegroundColor White
        Write-Host "  Account ID:  $($identity.Account)" -ForegroundColor White
        Write-Host "  User ID:     $($identity.UserId)" -ForegroundColor White

        $region = aws configure get region
        if ($region) {
            Write-Host "  Region:      $region" -ForegroundColor White
        } else {
            Write-Host "  Region:      Not set (will use default)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "`n✗ Not authenticated" -ForegroundColor Red
        Write-Host "Run: aws configure" -ForegroundColor Yellow
    }
}

function Show-AccessKeys {
    Write-Host "`n=== Your Access Keys ===" -ForegroundColor Cyan

    $userName = Get-MyUserName
    if (-not $userName) { return }

    Write-Host "`nUser: $userName" -ForegroundColor White

    try {
        $keysJson = aws iam list-access-keys --user-name $userName --output json 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to list keys"
        }

        $keys = ($keysJson | ConvertFrom-Json).AccessKeyMetadata

        if ($keys.Count -eq 0) {
            Write-Host "`n⚠ No access keys found" -ForegroundColor Yellow
        }
        else {
            Write-Host "`nFound $($keys.Count) access key(s):" -ForegroundColor Green
            foreach ($key in $keys) {
                Write-Host "`n  Access Key ID: $($key.AccessKeyId)" -ForegroundColor White
                Write-Host "  Status:        $($key.Status)" -ForegroundColor $(if($key.Status -eq "Active") {"Green"} else {"Yellow"})
                Write-Host "  Created:       $($key.CreateDate)" -ForegroundColor White
            }
        }

        Write-Host "`n⚠ Note: Secret Access Keys are NOT shown (AWS doesn't store them)" -ForegroundColor Yellow
    }
    catch {
        Write-Host "`n✗ Failed to list access keys" -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
    }
}

function New-MyAccessKey {
    Write-Host "`n=== Create New Access Key ===" -ForegroundColor Cyan

    $userName = Get-MyUserName
    if (-not $userName) { return }

    Write-Host "`nUser: $userName" -ForegroundColor White
    Write-Host "`n⚠ WARNING: You can only view the Secret Access Key ONCE!" -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to cancel, or any other key to continue..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

    try {
        Write-Host "`nCreating access key..." -ForegroundColor White
        $keyJson = aws iam create-access-key --user-name $userName --output json 2>&1

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create key"
        }

        $key = ($keyJson | ConvertFrom-Json).AccessKey

        Write-Host "`n✓ Access key created successfully!" -ForegroundColor Green
        Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Yellow
        Write-Host "║  SAVE THESE CREDENTIALS IMMEDIATELY!                       ║" -ForegroundColor Yellow
        Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Yellow
        Write-Host "`nAccess Key ID:" -ForegroundColor White
        Write-Host $key.AccessKeyId -ForegroundColor Cyan
        Write-Host "`nSecret Access Key:" -ForegroundColor White
        Write-Host $key.SecretAccessKey -ForegroundColor Cyan
        Write-Host "`nCreated: $($key.CreateDate)" -ForegroundColor White

        # Offer to save to file
        Write-Host "`n" -NoNewline
        $save = Read-Host "Save to file? (y/n)"
        if ($save -eq 'y' -or $save -eq 'Y') {
            $fileName = "aws-credentials-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
            $content = @"
AWS Credentials
Created: $($key.CreateDate)
User: $userName

Access Key ID: $($key.AccessKeyId)
Secret Access Key: $($key.SecretAccessKey)

⚠️ KEEP THIS FILE SECURE! Delete after saving to your password manager.
⚠️ DO NOT commit this file to git!
"@
            $content | Out-File -FilePath $fileName -Encoding UTF8
            Write-Host "✓ Saved to: $fileName" -ForegroundColor Green
            Write-Host "⚠️ Remember to delete this file after saving credentials elsewhere!" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "`n✗ Failed to create access key" -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
    }
}

function Test-BedrockAccess {
    Write-Host "`n=== Bedrock Model Access Check ===" -ForegroundColor Cyan

    $region = aws configure get region
    if (-not $region) {
        $region = "us-east-1"
        Write-Host "`n⚠ No region set, using default: $region" -ForegroundColor Yellow
    }
    else {
        Write-Host "`nRegion: $region" -ForegroundColor White
    }

    try {
        Write-Host "`nChecking Bedrock access..." -ForegroundColor White
        $modelsJson = aws bedrock list-foundation-models --region $region --output json 2>&1

        if ($LASTEXITCODE -ne 0) {
            throw "Cannot access Bedrock"
        }

        $models = ($modelsJson | ConvertFrom-Json).modelSummaries
        $claudeModels = $models | Where-Object { $_.modelId -like "*claude*" }

        if ($claudeModels.Count -gt 0) {
            Write-Host "`n✓ Bedrock access verified!" -ForegroundColor Green
            Write-Host "`n✓ Claude models available ($($claudeModels.Count)):" -ForegroundColor Green

            foreach ($model in $claudeModels) {
                $modelName = $model.modelId
                if ($modelName -like "*sonnet-4-5*" -or $modelName -like "*opus-4-6*") {
                    Write-Host "  ✓ $modelName" -ForegroundColor Green
                }
                else {
                    Write-Host "  - $modelName" -ForegroundColor White
                }
            }

            # Check for specific model
            $targetModel = "anthropic.claude-sonnet-4-5-v1:0"
            $found = $claudeModels | Where-Object { $_.modelId -eq $targetModel }
            if ($found) {
                Write-Host "`n✓ Target model available: $targetModel" -ForegroundColor Green
            }
            else {
                Write-Host "`n⚠ Target model not found: $targetModel" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "`n⚠ No Claude models found" -ForegroundColor Yellow
            Write-Host "Enable models: AWS Console → Bedrock → Model access" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "`n✗ Cannot access Bedrock" -ForegroundColor Red
        Write-Host "`nPossible causes:" -ForegroundColor Yellow
        Write-Host "  - IAM user missing AmazonBedrockFullAccess policy" -ForegroundColor White
        Write-Host "  - Model access not enabled in Bedrock console" -ForegroundColor White
        Write-Host "  - Bedrock not available in region: $region" -ForegroundColor White
    }
}

function Invoke-FullVerification {
    Show-Info
    Start-Sleep -Seconds 1
    Show-AccessKeys
    Start-Sleep -Seconds 1
    Test-BedrockAccess

    Write-Host "`n=== Verification Complete ===" -ForegroundColor Cyan
    Write-Host "`nNext step: Test Bedrock connection" -ForegroundColor Green
    Write-Host "cd tests\BedrockConnectionTest" -ForegroundColor White
    Write-Host "dotnet run" -ForegroundColor White
}

# Main execution
Clear-Host

switch ($Command) {
    'info'          { Show-Info }
    'list-keys'     { Show-AccessKeys }
    'create-key'    { New-MyAccessKey }
    'check-bedrock' { Test-BedrockAccess }
    'verify-all'    { Invoke-FullVerification }
    'help'          { Show-Help }
    default         { Show-Help }
}

Write-Host ""
