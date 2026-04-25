# Start Agentic Pipeline API with Environment Variables
# Run this script to start the API with proper configuration

param(
    [Parameter(Mandatory=$false)]
    [string]$AzureDevOpsPAT = "",

    [Parameter(Mandatory=$false)]
    [string]$AzureDevOpsOrg = "",

    [Parameter(Mandatory=$false)]
    [string]$AzureDevOpsProject = "Semantic-Kernel",

    [Parameter(Mandatory=$false)]
    [string]$GitHubPAT = ""
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Agentic Pipeline API Startup" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Check if PATs are provided
if ([string]::IsNullOrEmpty($AzureDevOpsPAT)) {
    Write-Host "WARNING: Azure DevOps PAT not provided" -ForegroundColor Yellow
    Write-Host "Set with: -AzureDevOpsPAT 'your-pat'" -ForegroundColor Gray
    Write-Host ""
}

if ([string]::IsNullOrEmpty($GitHubPAT)) {
    Write-Host "WARNING: GitHub PAT not provided" -ForegroundColor Yellow
    Write-Host "Set with: -GitHubPAT 'your-pat'" -ForegroundColor Gray
    Write-Host ""
}

# Set environment variables if provided
if (-not [string]::IsNullOrEmpty($AzureDevOpsPAT)) {
    $env:AZUREDEVOPS__PERSONALACCESSTOKEN = $AzureDevOpsPAT
    Write-Host "✓ Azure DevOps PAT configured" -ForegroundColor Green
}

if (-not [string]::IsNullOrEmpty($AzureDevOpsOrg)) {
    $env:AZUREDEVOPS__ORGANIZATION = $AzureDevOpsOrg
    Write-Host "✓ Azure DevOps Organization: $AzureDevOpsOrg" -ForegroundColor Green
}

if (-not [string]::IsNullOrEmpty($AzureDevOpsProject)) {
    $env:AZUREDEVOPS__DEFAULTPROJECT = $AzureDevOpsProject
    Write-Host "✓ Azure DevOps Project: $AzureDevOpsProject" -ForegroundColor Green
}

if (-not [string]::IsNullOrEmpty($GitHubPAT)) {
    $env:GITHUB__PERSONALACCESSTOKEN = $GitHubPAT
    Write-Host "✓ GitHub PAT configured" -ForegroundColor Green
}

Write-Host ""
Write-Host "Starting Agentic Pipeline API..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host ""
Write-Host "Endpoints:" -ForegroundColor Cyan
Write-Host "  - https://localhost:7056" -ForegroundColor Gray
Write-Host "  - http://localhost:5000" -ForegroundColor Gray
Write-Host ""
Write-Host "Webhook endpoint:" -ForegroundColor Cyan
Write-Host "  - POST http://localhost:5000/api/webhook/azuredevops" -ForegroundColor Gray
Write-Host ""

# Start the API
dotnet run --project src\AgenticPipeline.Api
