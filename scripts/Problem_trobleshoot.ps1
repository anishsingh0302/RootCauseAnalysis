 <#
 # Test if the NEW token can download logs
  $token = $env:GitHub__PersonalAccessToken
  $runId = 24836331620

  Write-Host "Testing token: $($token.Substring(0, 15))..." -ForegroundColor Yellow

  Invoke-RestMethod `
      -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/runs/$runId/logs" `
      -Headers @{
          Authorization = "Bearer $token"
          Accept = "application/vnd.github+json"
      } `
      -OutFile "test-logs.zip"

  if (Test-Path "test-logs.zip") {
      Write-Host "SUCCESS: New token can download logs!" -ForegroundColor Green
      Remove-Item "test-logs.zip"
  } else {
      Write-Host "FAILED: New token still can't download logs" -ForegroundColor Red
  }
  #>

  <#--------------------------------------------------------------------------------------------------------
  # Get the most recent workflow runs
  $token = $env:GitHub__PersonalAccessToken
  $response = Invoke-RestMethod `
      -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/runs?per_page=10" `
      -Headers @{
          Authorization = "Bearer $token"
          Accept = "application/vnd.github+json"
      }

  Write-Host "Recent workflow runs:" -ForegroundColor Cyan
  $response.workflow_runs | Select-Object -First 10 | ForEach-Object {
      Write-Host "ID: $($_.id) | Status: $($_.status) | Conclusion: $($_.conclusion) | Created: $($_.created_at)" -ForegroundColor White
  }

  #>

  <#-----
  $token = $env:GitHub__PersonalAccessToken

  foreach ($runId in @(24836331620, 24834542800)) {
      Write-Host "`nTesting run ID: $runId" -ForegroundColor Yellow

      try {
          Invoke-RestMethod `
              -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/runs/$runId/logs" `
              -Headers @{
                  Authorization = "Bearer $token"
                  Accept = "application/vnd.github+json"
              } `
              -OutFile "test-$runId.zip"

          if (Test-Path "test-$runId.zip") {
              $size = (Get-Item "test-$runId.zip").Length
              Write-Host "SUCCESS: Downloaded logs ($size bytes)" -ForegroundColor Green
              Remove-Item "test-$runId.zip"
              Write-Host "USE THIS RUN ID: $runId" -ForegroundColor Cyan
              break
          }
      } catch {
          Write-Host "FAILED: Cannot download logs for $runId" -ForegroundColor Red
      }
  }

  #>

<#
   $token = $env:GitHub__PersonalAccessToken
  $runId = 24836331620

  $run = Invoke-RestMethod `
      -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/runs/$runId" `
      -Headers @{
          Authorization = "Bearer $token"
          Accept = "application/vnd.github+json"
      }

  Write-Host "Workflow Details:" -ForegroundColor Cyan
  Write-Host "  Repository: $($run.repository.full_name)" -ForegroundColor White
  Write-Host "  Workflow Name: $($run.name)" -ForegroundColor White
  Write-Host "  Path: $($run.path)" -ForegroundColor White
  Write-Host "  Status: $($run.status)" -ForegroundColor White
  Write-Host "  Conclusion: $($run.conclusion)" -ForegroundColor White
  Write-Host "  Logs URL: $($run.logs_url)" -ForegroundColor White

  #>

  <#

   $token = $env:GitHub__PersonalAccessToken

  # Trigger the workflow
  try {
      Invoke-RestMethod `
          -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/workflows/test-webhook.yml/dispatches" `
          -Method Post `
          -Headers @{
              Authorization = "Bearer $token"
              Accept = "application/vnd.github+json"
              "X-GitHub-Api-Version" = "2022-11-28"
          } `
          -Body '{"ref":"master"}' `
          -ContentType "application/json"

      Write-Host "Workflow triggered successfully! Waiting for it to start..." -ForegroundColor Green

      # Wait 10 seconds
      Start-Sleep -Seconds 10

      # Get the latest run
      $runs = Invoke-RestMethod `
          -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/runs?per_page=1" `
          -Headers @{
              Authorization = "Bearer $token"
              Accept = "application/vnd.github+json"
          }

      $latestRun = $runs.workflow_runs[0]
      Write-Host "`nLatest run created:" -ForegroundColor Cyan
      Write-Host "  Run ID: $($latestRun.id)" -ForegroundColor White
      Write-Host "  Status: $($latestRun.status)" -ForegroundColor White
      Write-Host "  URL: https://github.com/anishsingh0302/AgenticPipeline/actions/runs/$($latestRun.id)" -ForegroundColor Yellow

  } catch {
      Write-Host "Failed to trigger workflow. The workflow might not have 'workflow_dispatch' trigger." -ForegroundColor Red
      Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Yellow
  }

  #>

  <#

   $token = $env:GitHub__PersonalAccessToken
  $runs = Invoke-RestMethod `
      -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/runs?per_page=5" `
      -Headers @{
          Authorization = "Bearer $token"
          Accept = "application/vnd.github+json"
      }

  Write-Host "Recent workflow runs:" -ForegroundColor Cyan
  $runs.workflow_runs | ForEach-Object {
      $icon = if ($_.conclusion -eq "failure") { "X" } elseif ($_.conclusion -eq "success") { "✓" } else { "?" }
      Write-Host "$icon ID: $($_.id) | Status: $($_.status) | Conclusion: $($_.conclusion) | Created: $($_.created_at)" -ForegroundColor $(if ($_.status -eq
   "completed") { "Green" } else { "Yellow" })
  }

  # Find the most recent completed failure
  $completedFailure = $runs.workflow_runs | Where-Object { $_.status -eq "completed" -and $_.conclusion -eq "failure" } | Select-Object -First 1

  if ($completedFailure) {
      Write-Host "`nFound completed failure:" -ForegroundColor Green
      Write-Host "  Run ID: $($completedFailure.id)" -ForegroundColor Cyan
      Write-Host "  Use this ID for testing!" -ForegroundColor Yellow
  }

  #>

  <#

   # Test if we can download logs for the most recent failure
  $token = $env:GitHub__PersonalAccessToken
  $runId = 24884561022

  Write-Host "Testing logs for run $runId..." -ForegroundColor Yellow

  try {
      Invoke-RestMethod `
          -Uri "https://api.github.com/repos/anishsingh0302/AgenticPipeline/actions/runs/$runId/logs" `
          -Headers @{
              Authorization = "Bearer $token"
              Accept = "application/vnd.github+json"
          } `
          -OutFile "test-logs-$runId.zip"

      if (Test-Path "test-logs-$runId.zip") {
          $size = (Get-Item "test-logs-$runId.zip").Length
          Write-Host "SUCCESS! Logs downloaded ($size bytes)" -ForegroundColor Green
          Remove-Item "test-logs-$runId.zip"

          Write-Host "`nThis run ID WORKS! Updating test script..." -ForegroundColor Cyan

          # Update the test-webhook.ps1 with this run ID
          $scriptPath = "C:\Anish\Source\Agentic_Pipeline\scripts\test-webhook.ps1"
          (Get-Content $scriptPath) -replace '"id": \d+,', "`"id`": $runId," -replace 'runs/\d+/workflow', "runs/$runId/workflow" | Set-Content $scriptPath

          Write-Host "Script updated with run ID: $runId" -ForegroundColor Green
          Write-Host "`nNow run the webhook test:" -ForegroundColor Yellow
          Write-Host "  .\test-webhook.ps1 -IncludeSignature -WebhookSecret 'your-secret'" -ForegroundColor Cyan
      }
  } catch {
      Write-Host "FAILED: Cannot download logs" -ForegroundColor Red
      Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Yellow
  }

  #>

  <#

  
   # Create a test with sample failure logs
  $logContent = @"
  Run actions/checkout@v4
  Syncing repository: anishsingh0302/AgenticPipeline
  Getting Git version info
  Checking out the ref
  /usr/bin/git checkout --progress --force refs/remotes/origin/master
  HEAD is now at abc1234 Update test
  Run actions/setup-dotnet@v3
  Setting up .NET SDK
  .NET SDK version 9.0.0 installed
  Run dotnet build
  Microsoft (R) Build Engine version 17.9.0
  Build FAILED.
      0 Warning(s)
      5 Error(s)
  Error CS0246: The type or namespace name 'InvalidType' could not be found
  Error CS1002: ; expected at Program.cs:25
  Error CS0103: The name 'undefinedVar' does not exist in the current context
"@

  $testPayload = @{
      logContent = $logContent
      gitHubOwner = "anishsingh0302"
      gitHubRepo = "AgenticPipeline"
      enableCommitAnalysis = $true
      enablePatternDetection = $true
      source = "Manual Test"
  } | ConvertTo-Json

  Write-Host "Testing full orchestration pipeline..." -ForegroundColor Cyan

  # Skip certificate check for localhost (only if not already loaded)
  if ($PSVersionTable.PSVersion.Major -lt 6) {
      if (-not ([System.Management.Automation.PSTypeName]'TrustAllCertsPolicy').Type) {
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
      }
      [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
  }

  try {
      $response = Invoke-RestMethod `
          -Uri "https://localhost:7056/api/pipeline/orchestrate" `
          -Method Post `
          -Body $testPayload `
          -ContentType "application/json"

      Write-Host "`n============================================================" -ForegroundColor Green
      Write-Host "  SUCCESS - Pipeline Orchestration Completed!" -ForegroundColor Green
      Write-Host "============================================================" -ForegroundColor Green
      Write-Host "`nOrchestration ID: $($response.orchestrationId)" -ForegroundColor Cyan
      Write-Host "Status: $($response.status)" -ForegroundColor White
      Write-Host "`nCheck your Teams channel for the notification!" -ForegroundColor Yellow

  } catch {
      Write-Host "`n============================================================" -ForegroundColor Red
      Write-Host "  ERROR" -ForegroundColor Red
      Write-Host "============================================================" -ForegroundColor Red
      Write-Host "`nError: $($_.Exception.Message)" -ForegroundColor Red

      if ($_.ErrorDetails.Message) {
          Write-Host "`nDetails:" -ForegroundColor Yellow
          Write-Host $_.ErrorDetails.Message -ForegroundColor Red
      }
  }

  #>

  ########Azure Devops troubleshoot #######################

  $org = "anishsingh0302"
  $project = "AgenticPipeline"
  $buildId = 6
  $pat = $env:AzureDevOps__PersonalAccessToken

  Write-Host "Testing PAT: $($pat.Substring(0,10))..." -ForegroundColor Yellow

  $base64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$pat"))
  $headers = @{Authorization = "Basic $base64"}

  try {
      $result = Invoke-RestMethod -Uri "https://dev.azure.com/$org/$project/_apis/build/builds/$buildId?api-version=7.1-preview.7" -Headers $headers
      Write-Host "✓ PAT WORKS! Build info retrieved successfully" -ForegroundColor Green
      $result | ConvertTo-Json -Depth 2
  } catch {
      Write-Host "✗ PAT FAILED: $($_.Exception.Message)" -ForegroundColor Red
      Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
  }