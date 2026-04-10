# GitHub Credentials Setup

## 🔴 SECURITY ALERT

**Your GitHub token was exposed in `appsettings.json` (now removed).**

**CRITICAL: You MUST revoke the old token immediately!**

## 🔧 Setup Steps

### 1. Revoke the Old Token
1. Go to https://github.com/settings/tokens
2. Find the token you just used
3. Click **Delete** or **Revoke**

### 2. Generate a New Token
1. Go to https://github.com/settings/tokens
2. Click **Generate new token** → **Generate new token (classic)**
3. **Name:** `Agentic Pipeline`
4. **Expiration:** 90 days (or your preference)
5. **Scopes:** Check `repo` (or just `public_repo` if only using public repos)
6. Click **Generate token**
7. **COPY THE TOKEN** (starts with `ghp_` or `github_pat_`)

### 3. Set the Token Securely

Run the PowerShell script:

```powershell
cd C:\Anish\Source\Agentic_Pipeline

# Replace YOUR_NEW_TOKEN with the token you just copied
.\scripts\github\set-github-env.ps1 -Token "YOUR_NEW_TOKEN" -Owner "anishsingh0302" -Repo "Semantic-Kernel"
```

**What this does:**
- Sets environment variables at USER level (persistent)
- Sets both API format (`GitHub__PersonalAccessToken`) and test format (`GITHUB_PAT`)
- Token is NOT stored in any file
- Token is NOT committed to git

### 4. Restart Your IDE

After running the script:
1. **Close Visual Studio or VS Code completely**
2. **Reopen it**
3. Environment variables will now be available

### 5. Verify Setup

```powershell
# Verify environment variables are set
echo $env:GITHUB_PAT
echo $env:GitHub__PersonalAccessToken

# Should show your token (first 15 characters)
```

## ✅ Test Your Setup

```bash
# Run GitHub integration tests
dotnet test tests/AgenticPipeline.IntegrationTests --filter "FullyQualifiedName~GitHubServiceIntegrationTests"

# Should now use your token (5000 requests/hour instead of 60)
```

## 📝 Environment Variables Set

### For API (ASP.NET Core format):
- `GitHub__PersonalAccessToken` → Your token
- `GitHub__DefaultOwner` → `anishsingh0302`
- `GitHub__DefaultRepo` → `Semantic-Kernel`

### For Tests (simple format):
- `GITHUB_PAT` → Your token
- `GITHUB_OWNER` → `anishsingh0302`
- `GITHUB_REPO` → `Semantic-Kernel`

## 🔒 Security Best Practices

✅ **DO:**
- Store tokens in environment variables
- Use User Secrets for local development
- Set token expiration dates
- Revoke tokens immediately if exposed

❌ **DON'T:**
- Put tokens in `appsettings.json`
- Commit tokens to git
- Share tokens with others
- Use tokens without expiration

## 🆘 Troubleshooting

### Tests still not using token?
1. Make sure you restarted your IDE after setting variables
2. Check variables are set: `echo $env:GITHUB_PAT`
3. Re-run the script if needed

### Rate limit errors?
- Without token: 60 requests/hour
- With token: 5000 requests/hour
- Check on GitHub: https://api.github.com/rate_limit

### Token not working?
1. Verify token has correct scopes (`repo` or `public_repo`)
2. Check token hasn't expired
3. Make sure you copied the full token

---

**Created:** 2026-04-05
**Status:** Setup required before testing
