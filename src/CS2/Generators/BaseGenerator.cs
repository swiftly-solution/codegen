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
    /// Generates the files asynchronously
    /// </summary>
    /// <returns>A task representing the asynchronous operation with the result</returns>
    public abstract Task<GeneratorResult> GenerateFilesAsync();
}
