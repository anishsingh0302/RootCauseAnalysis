namespace AgenticPipeline.Core.Models;

/// <summary>
/// Detailed error analysis extracted from failure logs
/// </summary>
public class ErrorAnalysis
{
    /// <summary>
    /// Type of error (CompilationError, TestFailure, etc.)
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Exact error message from logs
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// File where error occurred
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Line number where error occurred
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Code snippet around the error
    /// </summary>
    public string CodeSnippet { get; set; } = string.Empty;

    /// <summary>
    /// Stack trace if available
    /// </summary>
    public List<string> StackTrace { get; set; } = new();

    /// <summary>
    /// Expected behavior
    /// </summary>
    public string ExpectedBehavior { get; set; } = string.Empty;

    /// <summary>
    /// Actual behavior observed
    /// </summary>
    public string ActualBehavior { get; set; } = string.Empty;

    /// <summary>
    /// List of affected files
    /// </summary>
    public List<string> AffectedFiles { get; set; } = new();

    /// <summary>
    /// Confidence in error extraction (0.0-1.0)
    /// </summary>
    public double ExtractionConfidence { get; set; }
}
