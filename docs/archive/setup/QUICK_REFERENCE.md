# AWS Commands - Quick Reference

Quick copy-paste commands for bash/shell users.

---

##  Easiest Way: Use Helper Script

```bash
# Run complete verification
./aws-helper.sh verify-all

# Or individual commands:
./aws-helper.sh info          # Show your AWS identity
./aws-helper.sh list-keys     # List your access keys
./aws-helper.sh create-key    # Create new access key
./aws-helper.sh check-bedrock # Check Bedrock access
```

---

## Manual Commands (Bash/Linux/Mac/Git Bash)

### Get Your AWS Identity
```bash
aws sts get-caller-identity
```

### Extract Your Username
```bash
userName=$(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')
echo "Your username: $userName"
```

### List Your Access Keys
```bash
userName=$(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')
aws iam list-access-keys --user-name $userName
```

### Create New Access Key
```bash
userName=$(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')
aws iam create-access-key --user-name $userName
```
**️ SAVE THE SECRET KEY IMMEDIATELY - YOU'LL ONLY SEE IT ONCE!**

### Check Your Region
```bash
aws configure get region
```

### Set Region
```bash
aws configure set region us-east-1
```

### List Bedrock Models
```bash
# All models
aws bedrock list-foundation-models --region us-east-1

# Claude models only
aws bedrock list-foundation-models --region us-east-1 \
  --query "modelSummaries[?contains(modelId, 'claude')].modelId" \
  --output table
```

### Check Specific Claude Model
```bash
aws bedrock list-foundation-models --region us-east-1 \
  --query "modelSummaries[?modelId=='anthropic.claude-sonnet-4-5-v1:0']"
```

---

## Initial Setup (First Time)

```bash
# 1. Configure AWS CLI with your credentials
aws configure
# Enter: Access Key ID, Secret Access Key, Region (us-east-1), Output format (json)

# 2. Verify it works
aws sts get-caller-identity

# 3. Check Bedrock access
aws bedrock list-foundation-models --region us-east-1 \
  --query "modelSummaries[?contains(modelId, 'claude')].modelId"
```

---

## Test Bedrock Connection

```bash
# Navigate to test directory
cd tests/BedrockConnectionTest

# Run the test
dotnet run

# Enter your credentials when prompted
```

---

## Common Errors & Fixes

### Error: "Must specify userName"
```bash
# DON'T do this:
aws iam list-access-keys

# DO this instead (with username):
userName=$(aws sts get-caller-identity --query "Arn" --output text | awk -F'/' '{print $NF}')
aws iam list-access-keys --user-name $userName
```

### Error: "Unable to locate credentials"
```bash
# Fix: Configure AWS CLI
aws configure
```

### Error: "An error occurred (AccessDeniedException)"
```
Fix: Enable models in AWS Console
→ Bedrock → Model access → Enable Anthropic Claude models
```

### Error: "command not found: aws"
```
Fix: Install AWS CLI
→ https://aws.amazon.com/cli/
```

---

## One-Liner Complete Check

```bash
# Check everything in one command
echo "=== Identity ===" && \
aws sts get-caller-identity && \
echo -e "\n=== Region ===" && \
aws configure get region && \
echo -e "\n=== Claude Models ===" && \
aws bedrock list-foundation-models --region us-east-1 \
  --query "modelSummaries[?contains(modelId, 'claude')].modelId" \
  --output table
```

---

## Save Credentials to File

```bash
# After creating access key, save to file
cat > aws-credentials-$(date +%Y%m%d).txt << EOF
Access Key ID: YOUR_ACCESS_KEY_ID
Secret Access Key: YOUR_SECRET_ACCESS_KEY
Region: us-east-1
Created: $(date)
EOF

echo "Saved to: aws-credentials-$(date +%Y%m%d).txt"
```

**️ Remember to delete this file after saving to password manager!**

---

## Environment Variables (Alternative to aws configure)

```bash
# Set for current session
export AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
export AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
export AWS_DEFAULT_REGION="us-east-1"

# Test
aws sts get-caller-identity
```

---

**Last Updated:** 2026-04-01
