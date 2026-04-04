#!/bin/bash
# aws-helper.sh
# Helper script for common AWS operations (bash version)

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

function show_help() {
    echo -e "${CYAN}AWS Helper Script - Common Operations Made Easy${NC}"
    echo ""
    echo "Usage: ./aws-helper.sh <command>"
    echo ""
    echo "Commands:"
    echo "  info          - Show your AWS identity and username"
    echo "  list-keys     - List your access keys"
    echo "  create-key    - Create a new access key"
    echo "  check-bedrock - Check Bedrock model access"
    echo "  verify-all    - Run complete verification"
    echo "  help          - Show this help message"
    echo ""
    echo "Examples:"
    echo "  ./aws-helper.sh info"
    echo "  ./aws-helper.sh list-keys"
    echo "  ./aws-helper.sh verify-all"
    echo ""
}

function get_username() {
    aws sts get-caller-identity --query "Arn" --output text 2>/dev/null | awk -F'/' '{print $NF}'
}

function show_info() {
    echo -e "\n${CYAN}=== AWS Identity Information ===${NC}\n"

    if ! identity=$(aws sts get-caller-identity --output json 2>&1); then
        echo -e "${RED}✗ Not authenticated${NC}"
        echo -e "${YELLOW}Run: aws configure${NC}"
        return 1
    fi

    arn=$(echo "$identity" | jq -r '.Arn')
    account=$(echo "$identity" | jq -r '.Account')
    userId=$(echo "$identity" | jq -r '.UserId')
    userName=$(echo "$arn" | awk -F'/' '{print $NF}')

    echo -e "${GREEN}✓ Successfully authenticated!${NC}"
    echo -e "  User ARN:    $arn"
    echo -e "  User Name:   ${WHITE}$userName${NC}"
    echo -e "  Account ID:  $account"
    echo -e "  User ID:     $userId"

    region=$(aws configure get region 2>/dev/null)
    if [ -n "$region" ]; then
        echo -e "  Region:      $region"
    else
        echo -e "${YELLOW}  Region:      Not set (will use default)${NC}"
    fi
}

function show_access_keys() {
    echo -e "\n${CYAN}=== Your Access Keys ===${NC}\n"

    userName=$(get_username)
    if [ -z "$userName" ]; then
        echo -e "${RED}✗ Cannot get username. Make sure you've run 'aws configure'${NC}"
        return 1
    fi

    echo -e "User: ${WHITE}$userName${NC}\n"

    if ! keys=$(aws iam list-access-keys --user-name "$userName" --output json 2>&1); then
        echo -e "${RED}✗ Failed to list access keys${NC}"
        echo -e "${RED}$keys${NC}"
        return 1
    fi

    keyCount=$(echo "$keys" | jq -r '.AccessKeyMetadata | length')

    if [ "$keyCount" -eq 0 ]; then
        echo -e "${YELLOW}⚠ No access keys found${NC}"
    else
        echo -e "${GREEN}Found $keyCount access key(s):${NC}\n"

        echo "$keys" | jq -r '.AccessKeyMetadata[] | "  Access Key ID: \(.AccessKeyId)\n  Status:        \(.Status)\n  Created:       \(.CreateDate)\n"'
    fi

    echo -e "${YELLOW}⚠ Note: Secret Access Keys are NOT shown (AWS doesn't store them)${NC}"
}

function create_access_key() {
    echo -e "\n${CYAN}=== Create New Access Key ===${NC}\n"

    userName=$(get_username)
    if [ -z "$userName" ]; then
        echo -e "${RED}✗ Cannot get username. Make sure you've run 'aws configure'${NC}"
        return 1
    fi

    echo -e "User: ${WHITE}$userName${NC}\n"
    echo -e "${YELLOW}⚠ WARNING: You can only view the Secret Access Key ONCE!${NC}"
    read -p "Press Ctrl+C to cancel, or Enter to continue..." dummy

    echo -e "\nCreating access key..."

    if ! key=$(aws iam create-access-key --user-name "$userName" --output json 2>&1); then
        echo -e "${RED}✗ Failed to create access key${NC}"
        echo -e "${RED}$key${NC}"
        return 1
    fi

    accessKeyId=$(echo "$key" | jq -r '.AccessKey.AccessKeyId')
    secretAccessKey=$(echo "$key" | jq -r '.AccessKey.SecretAccessKey')
    createDate=$(echo "$key" | jq -r '.AccessKey.CreateDate')

    echo -e "\n${GREEN}✓ Access key created successfully!${NC}\n"
    echo -e "${YELLOW}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${YELLOW}║  SAVE THESE CREDENTIALS IMMEDIATELY!                       ║${NC}"
    echo -e "${YELLOW}╚════════════════════════════════════════════════════════════╝${NC}\n"
    echo -e "Access Key ID:"
    echo -e "${CYAN}$accessKeyId${NC}\n"
    echo -e "Secret Access Key:"
    echo -e "${CYAN}$secretAccessKey${NC}\n"
    echo -e "Created: $createDate\n"

    read -p "Save to file? (y/n): " save
    if [ "$save" = "y" ] || [ "$save" = "Y" ]; then
        fileName="aws-credentials-$(date +%Y%m%d-%H%M%S).txt"
        cat > "$fileName" <<EOF
AWS Credentials
Created: $createDate
User: $userName

Access Key ID: $accessKeyId
Secret Access Key: $secretAccessKey

⚠️ KEEP THIS FILE SECURE! Delete after saving to your password manager.
⚠️ DO NOT commit this file to git!
EOF
        echo -e "${GREEN}✓ Saved to: $fileName${NC}"
        echo -e "${YELLOW}⚠️ Remember to delete this file after saving credentials elsewhere!${NC}"
    fi
}

function check_bedrock() {
    echo -e "\n${CYAN}=== Bedrock Model Access Check ===${NC}\n"

    region=$(aws configure get region 2>/dev/null)
    if [ -z "$region" ]; then
        region="us-east-1"
        echo -e "${YELLOW}⚠ No region set, using default: $region${NC}\n"
    else
        echo -e "Region: $region\n"
    fi

    echo -e "Checking Bedrock access..."

    if ! models=$(aws bedrock list-foundation-models --region "$region" --output json 2>&1); then
        echo -e "${RED}✗ Cannot access Bedrock${NC}\n"
        echo -e "${YELLOW}Possible causes:${NC}"
        echo -e "  - IAM user missing AmazonBedrockFullAccess policy"
        echo -e "  - Model access not enabled in Bedrock console"
        echo -e "  - Bedrock not available in region: $region"
        return 1
    fi

    claudeModels=$(echo "$models" | jq -r '.modelSummaries[] | select(.modelId | contains("claude")) | .modelId')

    if [ -n "$claudeModels" ]; then
        echo -e "\n${GREEN}✓ Bedrock access verified!${NC}"
        count=$(echo "$claudeModels" | wc -l | tr -d ' ')
        echo -e "\n${GREEN}✓ Claude models available ($count):${NC}\n"

        while IFS= read -r model; do
            if [[ "$model" == *"sonnet-4-5"* ]] || [[ "$model" == *"opus-4-6"* ]]; then
                echo -e "  ${GREEN}✓ $model${NC}"
            else
                echo -e "  - $model"
            fi
        done <<< "$claudeModels"

        # Check for specific model
        targetModel="anthropic.claude-sonnet-4-5-v1:0"
        if echo "$claudeModels" | grep -q "$targetModel"; then
            echo -e "\n${GREEN}✓ Target model available: $targetModel${NC}"
        else
            echo -e "\n${YELLOW}⚠ Target model not found: $targetModel${NC}"
        fi
    else
        echo -e "\n${YELLOW}⚠ No Claude models found${NC}"
        echo -e "${YELLOW}Enable models: AWS Console → Bedrock → Model access${NC}"
    fi
}

function verify_all() {
    show_info
    sleep 1
    show_access_keys
    sleep 1
    check_bedrock

    echo -e "\n${CYAN}=== Verification Complete ===${NC}\n"
    echo -e "${GREEN}Next step: Test Bedrock connection${NC}"
    echo -e "cd tests/BedrockConnectionTest"
    echo -e "dotnet run\n"
}

# Main execution
clear

case "${1:-help}" in
    info)
        show_info
        ;;
    list-keys)
        show_access_keys
        ;;
    create-key)
        create_access_key
        ;;
    check-bedrock)
        check_bedrock
        ;;
    verify-all)
        verify_all
        ;;
    help|*)
        show_help
        ;;
esac

echo ""
