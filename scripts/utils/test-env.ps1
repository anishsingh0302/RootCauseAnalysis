# test-env.ps1
# Test if environment variables are set

Write-Host "`n=== Environment Variables Check ===" -ForegroundColor Cyan

if ($env:AWS_ACCESS_KEY_ID) {
    $masked = $env:AWS_ACCESS_KEY_ID.Substring(0, [Math]::Min(10, $env:AWS_ACCESS_KEY_ID.Length)) + "..."
    Write-Host "AWS_ACCESS_KEY_ID:     $masked" -ForegroundColor Green
} else {
    Write-Host "AWS_ACCESS_KEY_ID:     NOT SET" -ForegroundColor Red
}

if ($env:AWS_SECRET_ACCESS_KEY) {
    Write-Host "AWS_SECRET_ACCESS_KEY: ****" -ForegroundColor Green
} else {
    Write-Host "AWS_SECRET_ACCESS_KEY: NOT SET" -ForegroundColor Red
}

if ($env:AWS_SESSION_TOKEN) {
    Write-Host "AWS_SESSION_TOKEN:     SET" -ForegroundColor Green
} else {
    Write-Host "AWS_SESSION_TOKEN:     NOT SET" -ForegroundColor Red
}

if ($env:AWS_PROFILE) {
    Write-Host "AWS_PROFILE:           $env:AWS_PROFILE" -ForegroundColor Yellow
} else {
    Write-Host "AWS_PROFILE:           NOT SET" -ForegroundColor Gray
}

if ($env:AWS_REGION) {
    Write-Host "AWS_REGION:            $env:AWS_REGION" -ForegroundColor Green
} else {
    Write-Host "AWS_REGION:            NOT SET" -ForegroundColor Red
}

Write-Host ""
