#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests the webhook endpoint with sample GitHub workflow failure payload

.DESCRIPTION
    Sends a test webhook request to your local API to simulate GitHub webhook delivery

.PARAMETER ApiUrl
    The base URL of your API (default: https://localhost:7056)

.PARAMETER IncludeSignature
    Include X-Hub-Signature-256 header (requires WebhookSecret)

.PARAMETER WebhookSecret
    Your webhook secret for signature calculation

.EXAMPLE
    .\test-webhook.ps1

.EXAMPLE
    .\test-webhook.ps1 -IncludeSignature -WebhookSecret "my-secret"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "https://localhost:7056",

    [Parameter(Mandatory=$false)]
    [switch]$IncludeSignature,

    [Parameter(Mandatory=$false)]
    [string]$WebhookSecret = ""
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  GitHub Webhook Test Script" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Webhook payload
$payload = @'
{
  "action": "completed",
  "WorkflowRun": {
    "id": 24836331620,
    "name": "Test Webhook Integration",
    "status": "completed",
    "conclusion": "failure",
    "html_url": "https://github.com/anishsingh0302/AgenticPipeline/actions/runs/24836331620/workflow",
    "created_at": "2026-04-24T12:01:08Z",
    "updated_at": "2026-04-24T13:05:00Z",
    "head_branch": "master",
    "head_sha": "61f0196a1fa663387660b02391a56dc058a85cd3"
  },
  "repository": {
    "id": 1201454310,
    "name": "AgenticPipeline",
    "full_name": "anishsingh0302/AgenticPipeline",
    "owner": {
      "login": "anishsingh0302",
      "type": "User"
    }
  }
}
'@

$webhookUrl = "$ApiUrl/api/webhook/github"

Write-Host "Target URL: $webhookUrl" -ForegroundColor Yellow
Write-Host "Payload Size: $($payload.Length) bytes" -ForegroundColor Yellow
Write-Host ""

# Prepare headers
$headers = @{
    "Content-Type" = "application/json"
}

# Calculate signature if requested
if ($IncludeSignature) {
    if ([string]::IsNullOrWhiteSpace($WebhookSecret)) {
        Write-Host "ERROR: -IncludeSignature specified but no -WebhookSecret provided" -ForegroundColor Red
        Write-Host "    Usage: .\test-webhook.ps1 -IncludeSignature -WebhookSecret 'your-secret'" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Calculating HMAC-SHA256 signature..." -ForegroundColor Cyan

    try {
        $hmac = New-Object System.Security.Cryptography.HMACSHA256
        $hmac.Key = [Text.Encoding]::UTF8.GetBytes($WebhookSecret)
        $hash = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))
        $signature = "sha256=" + [BitConverter]::ToString($hash).Replace("-","").ToLower()

        $headers["X-Hub-Signature-256"] = $signature

        Write-Host "Signature calculated: $($signature.Substring(0, 20))..." -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "ERROR calculating signature: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "INFO: Sending without signature (signature verification will be skipped)" -ForegroundColor Gray
    Write-Host ""
}

# Send request
Write-Host "Sending webhook request..." -ForegroundColor Cyan
Write-Host ""

# Skip certificate validation for self-signed certs (development only)
if ($PSVersionTable.PSVersion.Major -lt 6) {
    # Windows PowerShell 5.1 and below
    add-type @"
        using System.Net;
        using System.Security.Cryptography.X509Certificates;
        public class TrustAllCertsPolicy : ICertificatePolicy {
            public bool CheckValidationResult(
                ServicePoint srvPoint, X509Certificate certificate,
                WebRequest request, int certificateProblem) {
                return true;
            }
        }
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

try {
    # Use -SkipCertificateCheck for PowerShell Core, manual override for Windows PowerShell
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $response = Invoke-RestMethod `
            -Uri $webhookUrl `
            -Method Post `
            -Body $payload `
            -Headers $headers `
            -SkipCertificateCheck `
            -ErrorAction Stop
    }
    else {
        $response = Invoke-RestMethod `
            -Uri $webhookUrl `
            -Method Post `
            -Body $payload `
            -Headers $headers `
            -ErrorAction Stop
    }

    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "  SUCCESS - Webhook Accepted" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor White
    Write-Host ""

    if ($response.message -like "*queued for processing*") {
        Write-Host "Webhook is being processed in background" -ForegroundColor Green
        Write-Host "   Check API console logs for processing details" -ForegroundColor Gray
        Write-Host ""
        Write-Host "To view results:" -ForegroundColor Yellow
        Write-Host "   GET $ApiUrl/api/pipeline/history?limit=5" -ForegroundColor Gray
    }
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $errorBody = $_.ErrorDetails.Message

    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "  ERROR - Webhook Rejected" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Status Code: $statusCode" -ForegroundColor Yellow
    Write-Host ""

    if ($errorBody) {
        Write-Host "Error Details:" -ForegroundColor Cyan
        try {
            $errorJson = $errorBody | ConvertFrom-Json
            $errorJson | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor Red
        }
        catch {
            Write-Host $errorBody -ForegroundColor Red
        }
    }
    else {
        Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""

    # Provide troubleshooting tips
    if ($statusCode -eq 401) {
        Write-Host "Troubleshooting Tips:" -ForegroundColor Yellow
        Write-Host "   - If signature verification is enabled, use:" -ForegroundColor Gray
        Write-Host "     .\test-webhook.ps1 -IncludeSignature -WebhookSecret 'your-secret'" -ForegroundColor Gray
        Write-Host "   - Or disable signature verification by clearing webhook secret" -ForegroundColor Gray
    }
    elseif ($statusCode -eq 501) {
        Write-Host "Troubleshooting Tips:" -ForegroundColor Yellow
        Write-Host "   - Orchestrator or log service not configured" -ForegroundColor Gray
        Write-Host "   - Check Program.cs for service registrations" -ForegroundColor Gray
    }
    elseif ($_.Exception.Message -like "*connection*") {
        Write-Host "Troubleshooting Tips:" -ForegroundColor Yellow
        Write-Host "   - Make sure API is running: dotnet run --project src/AgenticPipeline.Api" -ForegroundColor Gray
        Write-Host "   - Check if URL is correct: $ApiUrl" -ForegroundColor Gray
    }

    Write-Host ""
    exit 1
}


Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""