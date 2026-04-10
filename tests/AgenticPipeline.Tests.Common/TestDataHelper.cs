namespace AgenticPipeline.Tests.Common;

/// <summary>
/// Helper for loading test data and sample files
/// </summary>
public static class TestDataHelper
{
    private static string? _sampleDataPath;

    public static string SampleDataPath
    {
        get
        {
            if (_sampleDataPath == null)
            {
                // Navigate from test project to SampleData folder
                var currentDir = Directory.GetCurrentDirectory();
                var solutionDir = FindSolutionDirectory(currentDir);
                _sampleDataPath = Path.Combine(solutionDir, "SampleData");
            }
            return _sampleDataPath;
        }
    }

    private static string FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find solution directory");
    }

    public static async Task<string> LoadSampleLogAsync(string filename)
    {
        var path = Path.Combine(SampleDataPath, filename);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Sample log not found: {path}");
        }
        return await File.ReadAllTextAsync(path);
    }

    public static async Task<string> LoadCompilationErrorLogAsync()
    {
        return await LoadSampleLogAsync("build-failure-compilation.log");
    }

    public static async Task<string> LoadTestFailureLogAsync()
    {
        return await LoadSampleLogAsync("build-failure-test.log");
    }

    public static async Task<string> LoadDependencyErrorLogAsync()
    {
        return await LoadSampleLogAsync("build-failure-dependency.log");
    }
}
