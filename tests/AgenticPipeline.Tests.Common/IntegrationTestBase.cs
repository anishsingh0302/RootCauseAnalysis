using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AgenticPipeline.Tests.Common;

/// <summary>
/// Base class for all integration tests with common setup and utilities
/// </summary>
public abstract class IntegrationTestBase
{
    protected ITestOutputHelper Output { get; }
    protected ILoggerFactory LoggerFactory { get; }

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(output));
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    protected ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    protected void WriteLine(string message) => Output.WriteLine(message);

    protected void WriteSection(string title)
    {
        Output.WriteLine("");
        Output.WriteLine("═══════════════════════════════════════════════════════════════════");
        Output.WriteLine($"  {title}");
        Output.WriteLine("═══════════════════════════════════════════════════════════════════");
        Output.WriteLine("");
    }
}
