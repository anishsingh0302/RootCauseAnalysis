#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests Teams notification by sending a test adaptive card

.DESCRIPTION
    Sends a test notification to verify Teams webhook integration

.PARAMETER TeamsWebhookUrl
    Your Microsoft Teams webhook URL (optional, reads from env if not provided)

.EXAMPLE
    .\test-teams-notification.ps1

.EXAMPLE
    .\test-teams-notification.ps1 -TeamsWebhookUrl "https://..."
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$TeamsWebhookUrl = $env:Notifications__TeamsWebhookUrl
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Teams Notification Test" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrWhiteSpace($TeamsWebhookUrl)) {
    Write-Host "ERROR: No Teams webhook URL provided" -ForegroundColor Red
    Write-Host "Either set environment variable or pass -TeamsWebhookUrl parameter" -ForegroundColor Yellow
    exit 1
}

Write-Host "Teams Webhook URL: $($TeamsWebhookUrl.Substring(0, 50))..." -ForegroundColor Yellow
Write-Host ""

# Create a test adaptive card payload
$adaptiveCard = @{
    type = "message"
    attachments = @(
        @{
            contentType = "application/vnd.microsoft.card.adaptive"
            content = @{
                '$schema' = "http://adaptivecards.io/schemas/adaptive-card.json"
                type = "AdaptiveCard"
                version = "1.4"
                body = @(
                    @{
                        type = "TextBlock"
                        text = "Test Notification"
                        weight = "Bolder"
                        size = "Large"
                        color = "Accent"
                    }
                    @{
                        type = "TextBlock"
                        text = "This is a test notification from Agentic Pipeline"
                        wrap = $true
                    }
                    @{
                        type = "FactSet"
                        facts = @(
                            @{
                                title = "Status"
                                value = "Working"
                            }
                            @{
                                title = "Timestamp"
                                value = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
                            }
                            @{
                                title = "Source"
                                value = "PowerShell Test Script"
                            }
                        )
                    }
                )
                actions = @(
                    @{
                        type = "Action.OpenUrl"
                        title = "View Documentation"
                        url = "https://github.com/anishsingh0302/AgenticPipeline"
                    }
                )
            }
        }
    )
} | ConvertTo-Json -Depth 20

Write-Host "Sending test notification to Teams..." -ForegroundColor Cyan
Write-Host ""

try {
    $response = Invoke-RestMethod `
        -Uri $TeamsWebhookUrl `
        -Method Post `
        -Body $adaptiveCard `
        -ContentType "application/json" `
        -ErrorAction Stop

    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "  SUCCESS - Notification Sent!" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Check your Teams channel for the test notification!" -ForegroundColor White
    Write-Host ""

} catch {
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "  ERROR - Notification Failed" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""

    if ($_.ErrorDetails.Message) {
        Write-Host "Error Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Verify the webhook URL is correct" -ForegroundColor Gray
    Write-Host "  2. Check if the workflow is 'Active' in Teams" -ForegroundColor Gray
    Write-Host "  3. Make sure the webhook hasn't been deleted" -ForegroundColor Gray
    Write-Host ""

    exit 1
}
