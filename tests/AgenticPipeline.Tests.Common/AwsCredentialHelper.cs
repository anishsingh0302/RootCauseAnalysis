namespace AgenticPipeline.Tests.Common;

/// <summary>
/// Helper for AWS credential validation in tests
/// </summary>
public static class AwsCredentialHelper
{
    public static bool HasValidCredentials()
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey);
    }

    public static void EnsureCredentials()
    {
        if (!HasValidCredentials())
        {
            throw new InvalidOperationException(
                "AWS credentials not found. Please run the SSO credential script first:\n" +
                @"  .\scripts\aws\set-sso-creds-persistent.ps1 -ProfileName ""YOUR_AWS_PROFILE"" -Region ""us-west-2"""
            );
        }
    }

    public static (string accessKey, string secretKey, string? sessionToken) GetCredentials()
    {
        EnsureCredentials();

        return (
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")!,
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")!,
            Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN")
        );
    }
}
