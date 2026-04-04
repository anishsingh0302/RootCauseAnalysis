# AWS Bedrock Setup Guide

Complete guide for setting up AWS Bedrock with Claude models for the Agentic Pipeline project.

---

## Table of Contents
1. [Option A: Web Console Setup (Recommended for First Time)](#option-a-web-console-setup)
2. [Option B: AWS CLI / PowerShell Commands](#option-b-aws-cli--powershell-commands)
3. [Verification Scripts](#verification-scripts)
4. [Troubleshooting](#troubleshooting)

---

## Option A: Web Console Setup (Recommended for First Time)

### Step 1: Login to AWS Console
1. Go to https://console.aws.amazon.com/
2. Sign in with your AWS account

### Step 2: Find Your AWS Region
- Look at the **top-right corner** of the AWS Console (next to your username)
- Example: "N. Virginia (us-east-1)" or "Oregon (us-west-2)"
- **Recommended:** `us-east-1` (N. Virginia) - best Claude model availability
- Click the dropdown to change regions if needed
- **Note down the region code** (e.g., `us-east-1`)

### Step 3: Enable Bedrock Model Access

1. **Navigate to Bedrock:**
   - In the AWS Console search bar (top), type: `Bedrock`
   - Click on **Amazon Bedrock**

2. **Access Model Access Page:**
   - In the left sidebar, click **Model access**
   - Click **Enable specific models** or **Manage model access** button

3. **Enable Claude Models:**
   - Scroll to find the **Anthropic** section
   - Check the boxes next to:
     - ✅ **Claude 3.5 Sonnet** or **Claude Sonnet 4.5**
     - ✅ **Claude Opus 4.6** (optional but recommended)
   - Click **Request model access** or **Save changes** button

4. **Wait for Approval:**
   - Status should change to **"Access granted"** (usually instant)
   - If pending, refresh the page after a few minutes

### Step 4: Create IAM User for Bedrock Access

#### A. Navigate to IAM
1. Search for `IAM` in the top search bar
2. Click on **IAM** (Identity and Access Management)

#### B. Create New User
1. Click **Users** in the left sidebar
2. Click **Create user** button
3. Enter **User name:** `bedrock-pipeline-user` (or any name)
4. Click **Next**

#### C. Attach Permissions
1. Select **Attach policies directly**
2. In the search box, type: `Bedrock`
3. Check the box next to: **AmazonBedrockFullAccess**
4. Click **Next**
5. Review and click **Create user**

#### D. Create Access Key
1. Click on the newly created user (`bedrock-pipeline-user`)
2. Click the **Security credentials** tab
3. Scroll down to **Access keys** section
4. Click **Create access key** button
5. Select use case: **"Application running outside AWS"** or **"Command Line Interface (CLI)"**
6. Click **Next**
7. (Optional) Add description tag: `Bedrock Pipeline Test`
8. Click **Create access key**

#### E. SAVE YOUR CREDENTIALS ⚠️
You'll see:
```
✓ Access key created successfully!

Access Key ID: AKIAIOSFODNN7EXAMPLE
Secret Access Key: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY

⚠️ This is the only time you can view the secret access key!
```

**IMPORTANT:**
- Click **Download .csv file** button
- Or manually copy both values to a secure location
- You **CANNOT** retrieve the Secret Access Key later!
- Click **Done** when saved

---

## Option B: AWS CLI / PowerShell Commands

### Quick Start: Use the Helper Script ⚡

We've created helper scripts that handle username extraction automatically!

#### For PowerShell (Windows):
**Location:** `aws-helper.ps1` in project root

**Usage:**
```powershell
# Show your AWS info
.\aws-helper.ps1 -Command info

# List your access keys (no username needed!)
.\aws-helper.ps1 -Command list-keys

# Create new access key
.\aws-helper.ps1 -Command create-key

# Check Bedrock access
.\aws-helper.ps1 -Command check-bedrock

# Run complete verification
.\aws-helper.ps1 -Command verify-all
```

#### For Bash (Linux/Mac/Git Bash):
**Location:** `aws-helper.sh` in project root

**Usage:**
```bash
# Show your AWS info
./aws-helper.sh info

# List your access keys (no username needed!)
./aws-helper.sh list-keys

# Create new access key
./aws-helper.sh create-key

# Check Bedrock access
./aws-helper.sh check-bedrock

# Run complete verification
./aws-helper.sh verify-all
```

**Features:**
- ✓ Automatically gets your username
- ✓ Color-coded output
- ✓ Error handling
- ✓ Option to save new credentials to file
- ✓ Complete verification in one command

---

### Prerequisites

**Important:** You need existing AWS credentials configured before using these commands. Use Option A (Web Console) to create your FIRST access key, then use these commands for future management.

### Installation

#### Install AWS CLI
**Windows:**
```powershell
# Download and install from:
# https://aws.amazon.com/cli/

# Verify installation
aws --version
# Expected output: aws-cli/2.x.x Python/3.x.x Windows/...
```

**Alternative: AWS Tools for PowerShell**
```powershell
Install-Module -Name AWS.Tools.Common -Force
Install-Module -Name AWS.Tools.IdentityManagement -Force
Install-Module -Name AWS.Tools.Bedrock -Force
Install-Module -Name AWS.Tools.SecurityToken -Force
```

### Initial Configuration

**First-time setup (requires credentials from Option A):**
```powershell
# Configure AWS CLI with your credentials
aws configure

# You'll be prompted for:
# AWS Access Key ID [None]: AKIAIOSFODNN7EXAMPLE
# AWS Secret Access Key [None]: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
# Default region name [None]: us-east-1
# Default output format [None]: json
```

### Common Commands

#### 1. Get Your Current IAM User Info
```powershell
# Using AWS CLI
aws sts get-caller-identity

# Expected output:
# {
#     "UserId": "AIDAXXXXXXXXXX",
#     "Account": "123456789012",
#     "Arn": "arn:aws:iam::123456789012:user/your-username"
# }

# Using PowerShell
Get-STSCallerIdentity
```

#### 2. List Existing Access Keys

**First, get your username:**
```bash
# Bash/Linux/Mac/Git Bash
userName=$(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')
echo "Your IAM username: $userName"

# PowerShell (Windows)
$userName = (aws sts get-caller-identity --query "Arn" --output text).Split('/')[-1]
Write-Host "Your IAM username: $userName"
```

**Then list your access keys:**
```bash
# Bash - Method 1 (using variable)
userName=$(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')
aws iam list-access-keys --user-name $userName

# Bash - Method 2 (one line)
aws iam list-access-keys --user-name $(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')

# PowerShell (Windows)
$userName = (aws sts get-caller-identity --query "Arn" --output text).Split('/')[-1]
aws iam list-access-keys --user-name $userName

# Expected output:
# {
#     "AccessKeyMetadata": [
#         {
#             "UserName": "your-username",
#             "AccessKeyId": "AKIAIOSFODNN7EXAMPLE",
#             "Status": "Active",
#             "CreateDate": "2024-01-15T10:30:00Z"
#         }
#     ]
# }

# Using AWS PowerShell Tools
$userName = (Get-STSCallerIdentity).Arn.Split('/')[-1]
Get-IAMAccessKey -UserName $userName
```

**Note:** This only shows Access Key IDs, NOT Secret Keys (AWS doesn't store those).

#### 3. Create NEW Access Key
```bash
# Bash - automatic (gets your username first)
userName=$(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')
aws iam create-access-key --user-name $userName

# PowerShell (Windows)
$userName = (aws sts get-caller-identity --query "Arn" --output text).Split('/')[-1]
aws iam create-access-key --user-name $userName

# ⚠️ Output includes SecretAccessKey - SAVE IT IMMEDIATELY!
# {
#     "AccessKey": {
#         "UserName": "your-username",
#         "AccessKeyId": "AKIAIOSFODNN7EXAMPLE",
#         "Status": "Active",
#         "SecretAccessKey": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
#         "CreateDate": "2024-01-15T10:30:00Z"
#     }
# }

# Using AWS PowerShell Tools
$userName = (Get-STSCallerIdentity).Arn.Split('/')[-1]
New-IAMAccessKey -UserName $userName
```

#### 4. Get/Set Region
```powershell
# Get current configured region
aws configure get region

# Set default region
aws configure set region us-east-1

# Using PowerShell
Set-DefaultAWSRegion -Region us-east-1
Get-DefaultAWSRegion
```

#### 5. Check Bedrock Model Access
```powershell
# List all Bedrock foundation models
aws bedrock list-foundation-models --region us-east-1

# Filter for Claude models only
aws bedrock list-foundation-models --region us-east-1 --query "modelSummaries[?contains(modelId, 'claude')]"

# Check specific Claude Sonnet model
aws bedrock list-foundation-models --region us-east-1 --query "modelSummaries[?modelId=='anthropic.claude-sonnet-4-5-v1:0']"
```

#### 6. Test Bedrock Connection
```powershell
# Simple test to see if you can access Bedrock
aws bedrock list-foundation-models --region us-east-1 --output json

# If successful, you'll see a JSON list of models
# If you get AccessDenied, check IAM permissions and model access
```

---

## Verification Scripts

### PowerShell: Complete AWS Setup Verification

Save this as `test-aws-setup.ps1`:

```powershell
# test-aws-setup.ps1
# Verifies AWS CLI, credentials, region, and Bedrock access

Write-Host "=== AWS Setup Verification ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check if AWS CLI is installed
Write-Host "1. Checking AWS CLI Installation..." -ForegroundColor Yellow
try {
    $awsVersion = aws --version 2>&1
    Write-Host "   ✓ AWS CLI installed: $awsVersion" -ForegroundColor Green
} catch {
    Write-Host "   ✗ AWS CLI not installed" -ForegroundColor Red
    Write-Host "   Install from: https://aws.amazon.com/cli/" -ForegroundColor Yellow
    exit
}

# 2. Check credentials
Write-Host ""
Write-Host "2. Checking AWS Credentials..." -ForegroundColor Yellow
try {
    $identityJson = aws sts get-caller-identity 2>&1
    if ($LASTEXITCODE -eq 0) {
        $identity = $identityJson | ConvertFrom-Json
        Write-Host "   ✓ Authenticated as: $($identity.Arn)" -ForegroundColor Green
        Write-Host "   ✓ Account ID: $($identity.Account)" -ForegroundColor Green
        Write-Host "   ✓ User ID: $($identity.UserId)" -ForegroundColor Green
    } else {
        throw "Authentication failed"
    }
} catch {
    Write-Host "   ✗ Not authenticated" -ForegroundColor Red
    Write-Host "   Run: aws configure" -ForegroundColor Yellow
    exit
}

# 3. Check region
Write-Host ""
Write-Host "3. Checking AWS Region..." -ForegroundColor Yellow
$region = aws configure get region
if ($region) {
    Write-Host "   ✓ Default region: $region" -ForegroundColor Green

    if ($region -ne "us-east-1") {
        Write-Host "   ⚠ Recommended region is us-east-1 for best Claude availability" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ⚠ No default region set" -ForegroundColor Yellow
    Write-Host "   Run: aws configure set region us-east-1" -ForegroundColor Yellow
    $region = "us-east-1"
}

# 4. Check IAM permissions
Write-Host ""
Write-Host "4. Checking IAM Permissions..." -ForegroundColor Yellow
try {
    $policies = aws iam list-attached-user-policies --user-name $($identity.Arn.Split('/')[-1]) 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✓ Can access IAM" -ForegroundColor Green
    }
} catch {
    Write-Host "   ⚠ Cannot check IAM policies (might be OK if using roles)" -ForegroundColor Yellow
}

# 5. Check Bedrock access
Write-Host ""
Write-Host "5. Checking Bedrock Model Access..." -ForegroundColor Yellow
try {
    $modelsJson = aws bedrock list-foundation-models --region $region 2>&1

    if ($LASTEXITCODE -eq 0) {
        $models = $modelsJson | ConvertFrom-Json
        $claudeModels = $models.modelSummaries | Where-Object { $_.modelId -like "*claude*" }

        if ($claudeModels.Count -gt 0) {
            Write-Host "   ✓ Bedrock access verified" -ForegroundColor Green
            Write-Host "   ✓ Claude models available:" -ForegroundColor Green
            foreach ($model in $claudeModels) {
                Write-Host "     - $($model.modelId)" -ForegroundColor White
            }
        } else {
            Write-Host "   ⚠ No Claude models found" -ForegroundColor Yellow
            Write-Host "   Enable models in: AWS Console → Bedrock → Model access" -ForegroundColor Yellow
        }
    } else {
        throw "Cannot access Bedrock"
    }
} catch {
    Write-Host "   ✗ Cannot access Bedrock" -ForegroundColor Red
    Write-Host "   Possible causes:" -ForegroundColor Yellow
    Write-Host "   - IAM user missing AmazonBedrockFullAccess policy" -ForegroundColor Yellow
    Write-Host "   - Model access not enabled in Bedrock console" -ForegroundColor Yellow
    Write-Host "   - Bedrock not available in region: $region" -ForegroundColor Yellow
}

# 6. Test specific Claude model
Write-Host ""
Write-Host "6. Testing Claude Sonnet 4.5 Availability..." -ForegroundColor Yellow
$targetModel = "anthropic.claude-sonnet-4-5-v1:0"
try {
    $modelCheck = aws bedrock list-foundation-models --region $region --query "modelSummaries[?modelId=='$targetModel']" 2>&1

    if ($LASTEXITCODE -eq 0 -and $modelCheck -ne "[]") {
        Write-Host "   ✓ $targetModel is available!" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ $targetModel not found" -ForegroundColor Yellow
        Write-Host "   Try alternative: anthropic.claude-3-5-sonnet-20241022-v2:0" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ✗ Cannot check specific model" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Verification Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next step: Run the Bedrock connection test" -ForegroundColor Green
Write-Host "cd tests\BedrockConnectionTest" -ForegroundColor White
Write-Host "dotnet run" -ForegroundColor White
```

**Run it:**
```powershell
cd C:\Anish\Source\Agentic_Pipeline
.\test-aws-setup.ps1
```

---

## Troubleshooting

### Issue 1: "aws: command not found" or "aws is not recognized"

**Cause:** AWS CLI not installed or not in PATH

**Fix:**
1. Download AWS CLI from: https://aws.amazon.com/cli/
2. Run the installer
3. Restart PowerShell/Terminal
4. Verify: `aws --version`

### Issue 2: "Unable to locate credentials"

**Cause:** AWS CLI not configured with credentials

**Fix:**
```powershell
aws configure

# Enter your credentials when prompted
```

**Alternative:** Set environment variables
```powershell
$env:AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
$env:AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
$env:AWS_DEFAULT_REGION="us-east-1"
```

### Issue 3: "An error occurred (AccessDeniedException)"

**Cause:** IAM user lacks permissions

**Fix:**
1. Go to AWS Console → IAM → Users → [Your User]
2. Click "Add permissions"
3. Attach policy: **AmazonBedrockFullAccess**
4. Save changes

### Issue 4: "No Claude models found"

**Cause:** Model access not enabled

**Fix:**
1. AWS Console → Bedrock → Model access
2. Click "Enable specific models"
3. Enable Anthropic Claude models
4. Wait for "Access granted" status

### Issue 5: "ValidationException: The provided model identifier is invalid"

**Cause:** Model ID wrong or not available in your region

**Fix:**
1. Try region `us-east-1`:
   ```powershell
   aws configure set region us-east-1
   ```
2. List available models:
   ```powershell
   aws bedrock list-foundation-models --region us-east-1 --query "modelSummaries[?contains(modelId, 'claude')].modelId"
   ```
3. Use an available model ID from the list

### Issue 6: Cannot retrieve Secret Access Key

**Cause:** AWS doesn't store Secret Access Keys - they're only shown once

**Fix:**
1. You MUST create a NEW access key:
   ```powershell
   # Get your username first
   $userName = (aws sts get-caller-identity --query "Arn" --output text).Split('/')[-1]

   # Create new key
   aws iam create-access-key --user-name $userName
   ```
2. Save the Secret Access Key immediately
3. (Optional) Delete old access key:
   ```powershell
   aws iam delete-access-key --user-name $userName --access-key-id OLD_KEY_ID
   ```

### Issue 7: "Must specify userName when calling with non-User credentials"

**Error:**
```
aws: [ERROR]: An error occurred (ValidationError) when calling the ListAccessKeys operation:
Must specify userName when calling with non-User credentials
```

**Cause:** The IAM command requires `--user-name` parameter

**Fix:**
```powershell
# Get your username from your identity
$userName = (aws sts get-caller-identity --query "Arn" --output text).Split('/')[-1]
Write-Host "Your username: $userName"

# Now use it in IAM commands
aws iam list-access-keys --user-name $userName
```

---

## Security Best Practices

### 1. Store Credentials Securely

**Never commit credentials to git!**

**Option A: Use environment variables**
```powershell
# Set for current session
$env:AWS_ACCESS_KEY_ID="YOUR_KEY"
$env:AWS_SECRET_ACCESS_KEY="YOUR_SECRET"
$env:AWS_DEFAULT_REGION="us-east-1"

# Set permanently (Windows)
[System.Environment]::SetEnvironmentVariable('AWS_ACCESS_KEY_ID', 'YOUR_KEY', 'User')
[System.Environment]::SetEnvironmentVariable('AWS_SECRET_ACCESS_KEY', 'YOUR_SECRET', 'User')
[System.Environment]::SetEnvironmentVariable('AWS_DEFAULT_REGION', 'us-east-1', 'User')
```

**Option B: Use AWS credentials file**
```powershell
# AWS CLI stores credentials here:
# Windows: C:\Users\USERNAME\.aws\credentials
# Linux/Mac: ~/.aws/credentials

# Content:
[default]
aws_access_key_id = AKIAIOSFODNN7EXAMPLE
aws_secret_access_key = wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
region = us-east-1
```

**Option C: Use appsettings.json (with .gitignore)**
```json
{
  "AWS": {
    "AccessKeyId": "AKIAIOSFODNN7EXAMPLE",
    "SecretAccessKey": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
    "Region": "us-east-1"
  }
}
```

Add to `.gitignore`:
```
appsettings.json
appsettings.*.json
.env
.aws/
```

### 2. Rotate Access Keys Regularly

```powershell
# Create new key
aws iam create-access-key --user-name YOUR_USERNAME

# Update your configuration with new key
aws configure

# Delete old key
aws iam delete-access-key --user-name YOUR_USERNAME --access-key-id OLD_KEY_ID
```

### 3. Use Least Privilege

Instead of `AmazonBedrockFullAccess`, create custom policy:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "bedrock:InvokeModel",
        "bedrock:ListFoundationModels"
      ],
      "Resource": "*"
    }
  ]
}
```

---

## Summary: Recommended Setup Flow

For first-time setup:

1. ✅ **Use Web Console** (Option A) to:
   - Enable Bedrock models
   - Create IAM user
   - Generate first access key

2. ✅ **Configure AWS CLI:**
   ```powershell
   aws configure
   # Enter your credentials
   ```

3. ✅ **Run verification script:**
   ```powershell
   .\test-aws-setup.ps1
   ```

4. ✅ **Test Bedrock connection:**
   ```powershell
   cd tests\BedrockConnectionTest
   dotnet run
   ```

5. ✅ **Start building** once test succeeds!

---

**Last Updated:** 2026-04-01
**Project:** Agentic Pipeline Troubleshooter
**For:** AWS Bedrock + Claude Integration
