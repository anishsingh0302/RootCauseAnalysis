 # get-sso-creds.ps1
  # Login first
  aws sso login

  # Get credentials from cache (this varies by AWS CLI version)
  $profile = "default"  # Change this to your profile name if different

  Write-Host "Getting credentials for profile: $profile"

  # Use aws configure to get the values
  $accessKey = aws configure get aws_access_key_id --profile $profile
  $secretKey = aws configure get aws_secret_access_key --profile $profile
  $sessionToken = aws configure get aws_session_token --profile $profile
  $region = aws configure get region --profile $profile

  if ([string]::IsNullOrEmpty($region)) {
      $region = "us-east-1"
  }

  # Set environment variables
  $env:AWS_ACCESS_KEY_ID = $accessKey
  $env:AWS_SECRET_ACCESS_KEY = $secretKey
  $env:AWS_SESSION_TOKEN = $sessionToken
  $env:AWS_REGION = $region

  Write-Host "Environment variables set:"
  Write-Host "  AWS_ACCESS_KEY_ID: $($accessKey.Substring(0,10))..."
  Write-Host "  AWS_REGION: $region"

  if (-not [string]::IsNullOrEmpty($sessionToken)) {
      Write-Host "  AWS_SESSION_TOKEN: Set (SSO)"
  }

  Write-Host "`nNow run: dotnet run"