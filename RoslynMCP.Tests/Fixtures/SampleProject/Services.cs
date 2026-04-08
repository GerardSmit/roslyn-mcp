namespace SampleProject;

/// <summary>
/// Provides string formatting utilities.
/// </summary>
public interface IStringFormatter
{
    /// <summary>
    /// Formats a value for display.
    /// </summary>
    string FormatDisplayValue(int value);
}

/// <summary>
/// Represents processing status codes.
/// </summary>
public enum ProcessingStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Computes aggregate statistics from calculation results.
/// </summary>
public class StatisticsCalculator : IStringFormatter
{
    private readonly List<Result> _results = [];

    /// <summary>
    /// Adds a calculation result to the statistics tracker.
    /// </summary>
    public void AddResult(Result result) => _results.Add(result);

    /// <summary>
    /// Computes the average sum from all tracked results.
    /// </summary>
    public double ComputeAverageSum()
    {
        if (_results.Count == 0) return 0;
        return _results.Average(r => r.Sum);
    }

    /// <inheritdoc/>
    public string FormatDisplayValue(int value) => $"Value: {value}";
}
