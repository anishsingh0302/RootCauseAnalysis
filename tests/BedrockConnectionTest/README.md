# AWS Bedrock Connection Test

This is a simple console app to verify your AWS Bedrock connection with Claude models before starting the main project.

## ✅ Connection Status: SUCCESSFUL

**Last Test:** 2026-04-01
**Model Used:** `us.anthropic.claude-sonnet-4-6` (Claude Sonnet 4-6 via inference profile)
**Region:** `us-west-2` (Oregon)
**Authentication:** AWS SSO with persistent environment variables
**Token Usage:** Input 36 tokens, Output 66 tokens
**Response:** "AWS Bedrock connection successful! I am Claude and ready to help with CI/CD pipeline analysis."

---

## SSO Authentication Setup (Recommended)

For users with AWS SSO (federated login), use the persistent credential script:

### Quick Start:
1. Open PowerShell (as Administrator recommended)
2. Navigate to the test directory:
   ```powershell
   cd C:\Anish\Source\Agentic_Pipeline\tests\BedrockConnectionTest
   ```
3. Run the SSO credential script:
   ```powershell
   .\set-sso-creds-persistent.ps1 -ProfileName "anishuser1" -Region "us-west-2"
   ```
4. The script will:
   - Verify AWS CLI is installed
   - Ensure SSO login (prompts if needed)
   - Extract credentials from `~/.aws/cli/cache`
   - Set USER-level environment variables (persistent)
   - Launch Visual Studio with the solution
5. Press F5 in Visual Studio to run the test

### What the Script Does:
- Extracts temporary SSO credentials from AWS CLI cache
- Sets these environment variables system-wide:
  - `AWS_ACCESS_KEY_ID`
  - `AWS_SECRET_ACCESS_KEY`
  - `AWS_SESSION_TOKEN`
  - `AWS_REGION`
- Credentials persist across all applications until they expire (8-12 hours)
- Automatically launches Visual Studio with BedrockConnectionTest solution

### When to Re-run:
- When SSO tokens expire (typically 8-12 hours)
- Error message: "ExpiredToken" or "InvalidToken"

---

## Manual Authentication Setup (Alternative)

## Prerequisites: Get Your AWS Credentials

### Step 1: Login to AWS Console
1. Go to [AWS Console](https://console.aws.amazon.com/)
2. Sign in with your account

### Step 2: Find Your AWS Region
The region is shown in the **top-right corner** of the AWS Console (next to your username).
- Example: `N. Virginia (us-east-1)`, `Oregon (us-west-2)`
- Recommended: **us-east-1** (N. Virginia) - best Claude model availability
- Note down the region code (e.g., `us-east-1`)

### Step 3: Get Access Key and Secret Key

**Important:** Your console login password is NOT the same as API credentials. You need programmatic access keys.

#### A. Navigate to IAM
1. In AWS Console, search for **"IAM"** in the top search bar
2. Click on **IAM** (Identity and Access Management)

#### B. Option 1: Use Your Existing User
1. Click **Users** in the left sidebar
2. Click on **your username**
3. Click the **Security credentials** tab
4. Scroll down to **Access keys** section
5. If you see existing access keys:
   - You can use an existing active key (you'll need to know the Secret Key)
   - Or create a new one (see next step)

#### C. Option 2: Create New Access Key
1. In the **Access keys** section, click **Create access key**
2. Select use case: **"Application running outside AWS"** or **"CLI"**
3. Click **Next**
4. (Optional) Add description: "Bedrock Pipeline Test"
5. Click **Create access key**
6. **IMPORTANT:** You'll see two values:
   - **Access Key ID** - Example: `AKIAIOSFODNN7EXAMPLE`
   - **Secret Access Key** - Example: `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`

   ⚠️ **SAVE BOTH IMMEDIATELY!** You can only see the Secret Key once!

7. Click **Download .csv file** or copy both values to a safe place
8. Click **Done**

#### D. Option 3: Create New IAM User (If You Don't Have One)
1. Go to IAM → **Users** → **Create user**
2. Enter username: `bedrock-pipeline-user`
3. Click **Next**
4. Select **Attach policies directly**
5. Search and select: **AmazonBedrockFullAccess**
6. Click **Next** → **Create user**
7. Click on the newly created user
8. Go to **Security credentials** tab
9. Follow steps in "Option 2: Create New Access Key" above

### Step 4: Enable Bedrock Model Access
1. In AWS Console, search for **"Bedrock"**
2. Click on **Amazon Bedrock**
3. In left sidebar, click **Model access**
4. Click **Enable specific models** or **Manage model access**
5. Find **Anthropic** section
6. Check the box next to:
   - ✅ **Claude Sonnet 4.5**
   - ✅ **Claude Opus 4.6** (optional)
7. Click **Request model access** or **Save changes**
8. Wait for status to show **"Access granted"** (usually instant)

---

## How to Run the Test

1. **Navigate to the test directory:**
   ```bash
   cd C:\Anish\Source\Agentic_Pipeline\tests\BedrockConnectionTest
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

3. **Enter your AWS credentials when prompted:**
   - **AWS Access Key ID:** Paste the Access Key from Step 3 (e.g., `AKIAIOSFODNN7EXAMPLE`)
   - **AWS Secret Access Key:** Paste the Secret Key from Step 3 (hidden with `*`)
   - **AWS Region:** Enter the region code from Step 2 (e.g., `us-east-1`) or press Enter for default

## What It Does

- Creates an AWS Bedrock Runtime client
- Calls Claude Sonnet 4.5 with a test prompt
- Displays the response
- Shows token usage
- Provides helpful error messages if something goes wrong

## Expected Output

If successful, you should see:
```
✅ SUCCESS! AWS Bedrock connection is working!
```

## Common Issues

### Access Denied Error
**Cause:** Model access not enabled or IAM permissions missing

**Fix:**
1. Go to AWS Console → Bedrock → Model access
2. Click "Enable specific models"
3. Enable Anthropic Claude models
4. Ensure your IAM user has `AmazonBedrockFullAccess` policy

### Validation Error
**Cause:** Model ID not available in your region

**Try:**
- Use `us-east-1` region (best Claude availability)
- If that doesn't work, the app will suggest alternative model IDs

### Invalid Credentials
**Cause:** Incorrect Access Key or Secret Key

**Fix:**
- Double-check your credentials in AWS Console → IAM → Users → Security credentials

## Next Steps

Once this test succeeds:
1. You're ready to start building the main project
2. Store credentials securely (appsettings.json with .gitignore or environment variables)
3. We'll create the actual solution structure next
