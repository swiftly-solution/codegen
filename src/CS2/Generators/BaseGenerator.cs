namespace SwiftlyS2.Codegen.CS2.Generators;

/// <summary>
/// Represents the result of a generator execution
/// </summary>
public class GeneratorResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Progress reporter for generators
/// </summary>
public class GeneratorProgress
{
    private readonly List<string> _history = new();
    private string _currentStatus = "Initializing...";
    private readonly object _lock = new();

    public IReadOnlyList<string> History
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }
    }

    public string CurrentStatus
    {
        get
        {
            lock (_lock)
            {
                return _currentStatus;
            }
        }
    }

    public void Report(string status)
    {
        lock (_lock)
        {
            _currentStatus = status;
            _history.Add($"[{DateTime.Now:HH:mm:ss}] {status}");
        }
    }
}

/// <summary>
/// Base abstract class for all code generators
/// </summary>
public abstract class BaseGenerator
{
    /// <summary>
    /// Gets the name of the generator
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the output path for the generated files
    /// </summary>
    public abstract string OutputPath { get; }

    /// <summary>
    /// Gets the data path for input files (e.g., natives, gameevents)
    /// </summary>
    public virtual string? DataPath { get; protected set; }

    /// <summary>
    /// Gets the progress reporter for this generator
    /// </summary>
    public GeneratorProgress Progress { get; } = new();

    /// <summary>
    /// Generates the files asynchronously
    /// </summary>
    /// <returns>A task representing the asynchronous operation with the result</returns>
    public abstract Task<GeneratorResult> GenerateFilesAsync();
}
