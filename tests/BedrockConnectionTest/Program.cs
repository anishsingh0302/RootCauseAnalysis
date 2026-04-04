using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using System.Text;
using System.Text.Json;

namespace BedrockConnectionTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("AWS Bedrock and Claude Connection Test\n");

        // Check for existing AWS credentials
        Console.WriteLine("Checking for existing AWS credentials - (SSO/Profile)");
        bool hasDefaultCredentials = await CheckDefaultCredentials();

        AmazonBedrockRuntimeClient client;
        string region;

        if (hasDefaultCredentials)
        {
            Console.WriteLine("\nFound existing AWS credentials");
            Console.WriteLine(); // Add blank line
            Console.Write("Use these credentials? (Y/n): ");
            string? useDefault = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(useDefault) || useDefault.ToLower() == "y")
            {
                // Use default credential chain (SSO, environment variables, or ~/.aws/credentials)
                string? envRegion = Environment.GetEnvironmentVariable("AWS_REGION");
                string defaultRegion = string.IsNullOrWhiteSpace(envRegion) ? "us-east-1" : envRegion;
                Console.Write($"\nEnter AWS Region (press Enter for {defaultRegion}): ");
                string? regionInput = Console.ReadLine();
                region = string.IsNullOrWhiteSpace(regionInput) ? defaultRegion : regionInput;

                var regionEndpoint = RegionEndpoint.GetBySystemName(region);
                client = new AmazonBedrockRuntimeClient(regionEndpoint);

                Console.WriteLine($"Using credentials with region: {region}");
            }
            else
            {
                // Manual credential input
                client = GetManualCredentials(out region);
            }
        }
        else
        {
            Console.WriteLine("\nNo credentials found via environment variables");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("1. Enter credentials manually");
            Console.WriteLine("2. Use AWS profile name");
            Console.Write("\nChoice -> 1 or 2: ");
            string? choice = Console.ReadLine();

            if (choice == "2")
            {
                client = GetProfileCredentials(out region);
            }
            else
            {
                client = GetManualCredentials(out region);
            }
        }

        // Test the connection
        await TestBedrockConnection(client, region);

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task<bool> CheckDefaultCredentials()
    {
        try
        {
            Console.WriteLine("Checking environment variables");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
            {
                Console.WriteLine("Found AWS_ACCESS_KEY_ID");
            }
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN")))
            {
                Console.WriteLine("Found AWS_SESSION_TOKEN (SSO)");
            }

            Console.WriteLine("Attempting to create AWS client...");

            // Try to create a client with default credentials
            var testClient = new AmazonBedrockRuntimeClient(RegionEndpoint.USEast2);

            Console.WriteLine("Testing credentials with AWS...");

            // Try a simple operation to verify credentials work
            var request = new InvokeModelRequest
            {
                ModelId = "test", // This will fail but will verify credentials first
                Body = new MemoryStream()
            };

            try
            {
                await testClient.InvokeModelAsync(request);
            }
            catch (ValidationException)
            {
                // Expected - invalid model ID, but credentials are valid
                Console.WriteLine("Credentials validated");
                return true;
            }
            catch (AccessDeniedException)
            {
                // Credentials are valid but no Bedrock access
                Console.WriteLine("Credentials valid - no Bedrock access");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Credential test failed: {ex.Message}");
                throw;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No credentials found");
            Console.WriteLine($"Debug: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    static AmazonBedrockRuntimeClient GetProfileCredentials(out string region)
    {
        Console.Write("\nEnter AWS Profile Name (press Enter for 'default'): ");
        string? profileName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            profileName = "default";
        }

        Console.Write("Enter AWS Region (press Enter for us-east-1): ");
        string? regionInput = Console.ReadLine();
        region = string.IsNullOrWhiteSpace(regionInput) ? "us-east-1" : regionInput;

        try
        {
            Console.WriteLine($"\nUsing AWS profile: {profileName}");

            // Set the profile in environment variable so SDK can find it
            
            Environment.SetEnvironmentVariable("AWS_PROFILE", profileName);
            Environment.SetEnvironmentVariable("AWS_SDK_LOAD_CONFIG", "1");

            var regionEndpoint = RegionEndpoint.GetBySystemName(region);
            var client = new AmazonBedrockRuntimeClient(regionEndpoint);

            Console.WriteLine($"Client created with profile '{profileName}' in region {region}");

            // VALIDATION: Test credentials immediately
            Console.WriteLine("\nValidating credentials...");
            ValidateCredentials(client).Wait();

            return client;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Failed to use profile '{profileName}': {ex.Message}");
            Console.WriteLine("\nTip: Make sure you've run 'aws sso login' with your profile");
            Environment.Exit(1);
            return null!;
        }
    }

    static async Task ValidateCredentials(AmazonBedrockRuntimeClient client)
    {
        try
        {
            // Try a simple operation to verify credentials work
            var testRequest = new InvokeModelRequest
            {
                ModelId = "test-validation",
                Body = new MemoryStream()
            };

            try
            {
                await client.InvokeModelAsync(testRequest);
            }
            catch (ValidationException)
            {
                // Expected - invalid model, but credentials worked!
                Console.WriteLine("Credentials validated successfully!");
                return;
            }
            catch (AccessDeniedException)
            {
                // Credentials work but no Bedrock access
                Console.WriteLine("Credentials valid (note: check Bedrock permissions)");
                return;
            }
            catch (AmazonServiceException ex) when (
                ex.Message.Contains("EC2 Instance Metadata") ||
                ex.Message.Contains("Unable to get IAM security credentials") ||
                ex.ErrorCode == "InvalidClientTokenId" ||
                ex.ErrorCode == "SignatureDoesNotMatch")
            {
                // Credential error - be specific
                Console.WriteLine($"Credential validation failed!");
                Console.WriteLine($"   Error: {ex.Message}");
                Console.WriteLine($"\n   This means the .NET SDK cannot access your SSO credentials.");
                Console.WriteLine($"   The profile exists, but .NET SDK can't read SSO cache.\n");
                Console.WriteLine($"   Solutions:");
                Console.WriteLine($"   1. Run: .\\set-sso-creds.ps1 (extracts SSO creds to env vars)");
                Console.WriteLine($"   2. Or use manual credentials (Option 1)\n");
                throw;
            }
        }
        catch (ValidationException)
        {
            // This is actually success
            Console.WriteLine("Credentials validated successfully!");
        }
        catch (AccessDeniedException)
        {
            Console.WriteLine("Credentials valid (note: check Bedrock permissions)");
        }
    }

    static AmazonBedrockRuntimeClient GetManualCredentials(out string region)
    {
        Console.WriteLine("\nNote: For SSO users, use Option 2 (AWS Profile) instead.\n");

        Console.Write("Enter your AWS Access Key ID: ");
        string? accessKey = Console.ReadLine();

        Console.Write("Enter your AWS Secret Access Key: ");
        string? secretKey = ReadPassword();
        Console.WriteLine();

        Console.Write("Enter Session Token (press Enter if none): ");
        string? sessionToken = Console.ReadLine();

        Console.Write("Enter AWS Region (press Enter for us-east-1): ");
        string? regionInput = Console.ReadLine();
        region = string.IsNullOrWhiteSpace(regionInput) ? "us-east-1" : regionInput;

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            Console.WriteLine("Error: AWS credentials are required!");
            Environment.Exit(1);
        }

        AWSCredentials credentials;
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            credentials = new SessionAWSCredentials(accessKey, secretKey, sessionToken);
        }
        else
        {
            credentials = new BasicAWSCredentials(accessKey, secretKey);
        }

        var regionEndpoint = RegionEndpoint.GetBySystemName(region);
        return new AmazonBedrockRuntimeClient(credentials, regionEndpoint);
    }

    static async Task TestBedrockConnection(AmazonBedrockRuntimeClient client, string region)
    {
        try
        {
            Console.WriteLine("\n--- Testing Connection to AWS Bedrock ---");
            Console.WriteLine($"Region: {region}");

            // Use Claude Sonnet 4-6 inference profile (required for on-demand throughput)
            string modelId = "us.anthropic.claude-sonnet-4-6";
            Console.WriteLine($"\n--- Testing with: {modelId} ---");
            Console.WriteLine("(Using inference profile for on-demand throughput)\n");

            bool success = await TryInvokeModel(client, modelId);

            if (!success)
            {
                // Try alternative models using inference profiles
                string[] alternativeModels = new[]
                {
                    "us.anthropic.claude-sonnet-4-5-20250929-v1:0",
                    "us.anthropic.claude-opus-4-6-v1",
                    "us.anthropic.claude-3-7-sonnet-20250219-v1:0",
                    "us.anthropic.claude-3-5-haiku-20241022-v1:0",
                    "anthropic.claude-3-sonnet-20240229-v1:0",
                    "anthropic.claude-3-haiku-20240307-v1:0"
                };

                Console.WriteLine("Primary model failed, trying alternatives...\n");

                foreach (var altModel in alternativeModels)
                {
                    Console.WriteLine($"Trying: {altModel}");
                    success = await TryInvokeModel(client, altModel);
                    if (success)
                    {
                        modelId = altModel;
                        break;
                    }
                }

                if (!success)
                {
                    Console.WriteLine("\nNone of the expected models worked.");
                    Console.WriteLine("Your available models might be different.");
                    Console.WriteLine("\nRun this command to see your models:");
                    Console.WriteLine($"aws bedrock list-foundation-models --region {region} --query \"modelSummaries[?contains(modelId, 'claude')].modelId\"");
                    return;
                }
            }

            Console.WriteLine("\n--- Next Steps ---");
            Console.WriteLine("1. Your AWS Bedrock connection is verified");
            Console.WriteLine("2. You can now start building the main project");
            Console.WriteLine($"3. Recommended model: {modelId}");
            Console.WriteLine("4. Your SSO credentials will work automatically in the main app");
        }
        catch (AmazonServiceException ex) when (ex.ErrorCode == "ExpiredToken" || ex.ErrorCode == "InvalidToken")
        {
            Console.WriteLine($"\nCredentials Error: {ex.Message}");
            Console.WriteLine("\nYour SSO session may have expired.");
            Console.WriteLine("Run: aws sso login");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine($"\nFull error:\n{ex}");
        }
    }

    static async Task<bool> TryInvokeModel(AmazonBedrockRuntimeClient client, string modelId)
    {
        try
        {
            // Build request body
            var messages = new[]
            {
                new
                {
                    role = "user",
                    content = "Hello! Please respond with: 'AWS Bedrock connection successful! I am Claude and ready to help with CI/CD pipeline analysis.'"
                }
            };

            var requestBody = new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 512,
                messages = messages,
                temperature = 0.7
            };

            string requestJson = JsonSerializer.Serialize(requestBody);

            // Invoke model
            var request = new InvokeModelRequest
            {
                ModelId = modelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson))
            };

            Console.WriteLine("  Sending request to Bedrock...");
            var response = await client.InvokeModelAsync(request);

            Console.WriteLine($" Response received! Status: {response.HttpStatusCode}");

            // Parse response
            using var reader = new StreamReader(response.Body);
            string responseBody = await reader.ReadToEndAsync();

            var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("content", out var contentArray))
            {
                var firstContent = contentArray[0];
                if (firstContent.TryGetProperty("text", out var textElement))
                {
                    string claudeResponse = textElement.GetString() ?? "";

                    Console.WriteLine("\n--- Claude's Response ---");
                    Console.WriteLine(claudeResponse);
                    Console.WriteLine("\nSUCCESS! AWS Bedrock connection is working!\n");
                }
            }

            // Show token usage
            if (root.TryGetProperty("usage", out var usage))
            {
                Console.WriteLine("--- Token Usage ---");
                if (usage.TryGetProperty("input_tokens", out var inputTokens))
                    Console.WriteLine($"Input tokens: {inputTokens}");
                if (usage.TryGetProperty("output_tokens", out var outputTokens))
                    Console.WriteLine($"Output tokens: {outputTokens}");
            }

            return true;
        }
        catch (ValidationException ex)
        {
            Console.WriteLine($"Model not available: {ex.Message}");
            return false;
        }
        catch (AccessDeniedException ex)
        {
            Console.WriteLine($"\nAccess Denied: {ex.Message}");
            Console.WriteLine("\nPossible causes:");
            Console.WriteLine("1. Your IAM role doesn't have bedrock:InvokeModel permission");
            Console.WriteLine("2. Model access not enabled in AWS Console");
            Console.WriteLine("\nTo fix:");
            Console.WriteLine("- Go to AWS Console → Bedrock → Model access");
            Console.WriteLine("- Enable Anthropic Claude models");
            throw; // Re-throw to stop trying other models
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return false;
        }
    }

    // Helper to read password without displaying it
    static string ReadPassword()
    {
        StringBuilder password = new StringBuilder();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);

            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
        }
        while (key.Key != ConsoleKey.Enter);

        return password.ToString();
    }
}

/* ============================================================================
 * ALTERNATIVE: Manual Credentials Method (For Reference)
 * ============================================================================
 *
 * If you need to use manual credentials instead of SSO, replace the Main
 * method with this simpler version:
 *
 * static async Task Main(string[] args)
 * {
 *     Console.WriteLine("=== AWS Bedrock + Claude Connection Test ===\n");
 *
 *     // Get AWS credentials from user
 *     Console.Write("Enter your AWS Access Key ID: ");
 *     string? accessKey = Console.ReadLine();
 *
 *     Console.Write("Enter your AWS Secret Access Key: ");
 *     string? secretKey = ReadPassword();
 *     Console.WriteLine();
 *
 *     Console.Write("Enter AWS Region (press Enter for us-east-1): ");
 *     string? regionInput = Console.ReadLine();
 *     string region = string.IsNullOrWhiteSpace(regionInput) ? "us-east-1" : regionInput;
 *
 *     if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
 *     {
 *         Console.WriteLine("Error: AWS credentials are required!");
 *         return;
 *     }
 *
 *     // Create Bedrock Runtime client with manual credentials
 *     var credentials = new BasicAWSCredentials(accessKey, secretKey);
 *     var regionEndpoint = RegionEndpoint.GetBySystemName(region);
 *     var client = new AmazonBedrockRuntimeClient(credentials, regionEndpoint);
 *
 *     // Test the connection
 *     await TestBedrockConnection(client, region);
 *
 *     Console.WriteLine("\nPress any key to exit...");
 *     Console.ReadKey();
 * }
 *
 * ============================================================================
 */
