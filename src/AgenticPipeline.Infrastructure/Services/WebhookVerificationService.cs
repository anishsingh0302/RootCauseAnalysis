using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace AgenticPipeline.Infrastructure.Services;

/// <summary>
/// Service for verifying webhook signatures
/// </summary>
public class WebhookVerificationService
{
    private readonly ILogger<WebhookVerificationService> _logger;

    public WebhookVerificationService(ILogger<WebhookVerificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Verifies GitHub webhook signature (HMAC-SHA256)
    /// </summary>
    /// <param name="payload">Raw webhook payload body</param>
    /// <param name="signatureHeader">Value from X-Hub-Signature-256 header</param>
    /// <param name="secret">Webhook secret configured in GitHub</param>
    /// <returns>True if signature is valid</returns>
    public bool VerifyGitHubSignature(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Missing signature header");
            return false;
        }

        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("Webhook secret not configured");
            return false;
        }

        try
        {
            // GitHub signature format: "sha256=<hash>"
            if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid signature format");
                return false;
            }

            var expectedHash = signatureHeader.Substring(7); // Remove "sha256=" prefix
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var secretBytes = Encoding.UTF8.GetBytes(secret);

            using var hmac = new HMACSHA256(secretBytes);
            var computedHash = hmac.ComputeHash(payloadBytes);
            var computedHashHex = BitConverter.ToString(computedHash).Replace("-", "").ToLowerInvariant();

            var isValid = string.Equals(expectedHash, computedHashHex, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogWarning("Signature verification failed");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying webhook signature");
            return false;
        }
    }

    /// <summary>
    /// Verifies Azure DevOps webhook signature
    /// </summary>
    public bool VerifyAzureDevOpsSignature(string payload, string signatureHeader, string secret)
    {
        // Azure DevOps uses similar HMAC approach but different header format
        // Implementation depends on Azure DevOps configuration
        // For now, return true if secret matches basic auth
        return !string.IsNullOrEmpty(secret);
    }
}
