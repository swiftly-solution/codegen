using System.Text;
using System.Text.Json;

namespace SwiftlyS2.Codegen.CS2.Generators;

/// <summary>
/// Generator for datamaps
/// </summary>
public class Datamaps : BaseGenerator
{
    private readonly string _datamapsPath;

    public override string Name => "Datamaps";
    public override string OutputPath => Path.Combine("output", "src", "SwiftlyS2.Generated", "Datamaps");

    public Datamaps(string datamapsPath)
    {
        _datamapsPath = datamapsPath;
    }

    public override async Task<GeneratorResult> GenerateFilesAsync()
    {
        try
        {
            Progress.Report("Starting datamaps generation...");

            // Clear and recreate output directories
            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }
            Directory.CreateDirectory(OutputPath);
            Directory.CreateDirectory(Path.Combine(OutputPath, "Interfaces"));
            Directory.CreateDirectory(Path.Combine(OutputPath, "Classes"));

            Progress.Report($"Reading datamaps from: {_datamapsPath}");

            // Read and parse the datamaps JSON file
            var jsonContent = await File.ReadAllTextAsync(_datamapsPath);
            var datamapsData = JsonSerializer.Deserialize<DatamapsRoot>(jsonContent);

            if (datamapsData?.Datamaps == null)
            {
                return new GeneratorResult
                {
                    Success = false,
                    ErrorMessage = "Failed to parse datamaps JSON or no datamaps found"
                };
            }

            var managerFunctions = new List<string>();
            var managerConstructors = new List<string>();
            var serviceFunctions = new List<string>();
            var serviceInterfaceFunctions = new List<string>();

            Progress.Report($"Processing {datamapsData.Datamaps.Count} datamap classes...");

            foreach (var clazz in datamapsData.Datamaps)
            {
                var className = clazz.ClassName;

                foreach (var field in clazz.Fields)
                {
                    if (field.IsFunction)
                    {
                        var name = field.FieldName.Replace("::", "_");
                        var hash = field.FunctionHash;

                        managerFunctions.Add($"public BaseDatamapFunction<{className}, DHook{name}> {name} {{ get; init; }}");
                        managerConstructors.Add($"        {name} = new(this, {hash});");
                        serviceFunctions.Add($"\n    public IDatamapFunctionOperator<{className}, DHook{name}> {name} {{ get; }} = manager.{name}.Get(ctx.Name, profiler);\n\n    IDatamapFunctionOperator<{className}, IDHook{name}> IDatamapFunctionService.{name} => {name};");
                        serviceInterfaceFunctions.Add($"\n    public IDatamapFunctionOperator<{className}, IDHook{name}> {name} {{ get; }}");

                        // Write hook context class
                        var hookContextWriter = new CodeWriter();
                        WriteHookContext(hookContextWriter, className, name);
                        await File.WriteAllTextAsync(
                            Path.Combine(OutputPath, "Classes", $"DHook{name}.cs"),
                            hookContextWriter.ToString());

                        // Write hook context interface
                        var hookContextInterfaceWriter = new CodeWriter();
                        WriteHookContextInterface(hookContextInterfaceWriter, className, name);
                        await File.WriteAllTextAsync(
                            Path.Combine(OutputPath, "Interfaces", $"IDHook{name}.cs"),
                            hookContextInterfaceWriter.ToString());
                    }
                }
            }

            Progress.Report("Writing manager and service files...");

            // Write DatamapFunctionManager.cs
            var managerWriter = new CodeWriter();
            WriteManager(managerWriter, managerFunctions, managerConstructors);
            await File.WriteAllTextAsync(
                Path.Combine(OutputPath, "Classes", "DatamapFunctionManager.cs"),
                managerWriter.ToString());

            // Write DatamapFunctionService.cs
            var serviceWriter = new CodeWriter();
            WriteService(serviceWriter, serviceFunctions);
            await File.WriteAllTextAsync(
                Path.Combine(OutputPath, "Classes", "DatamapFunctionService.cs"),
                serviceWriter.ToString());

            // Write IDatamapFunctionService.cs
            var serviceInterfaceWriter = new CodeWriter();
            WriteServiceInterface(serviceInterfaceWriter, serviceInterfaceFunctions);
            await File.WriteAllTextAsync(
                Path.Combine(OutputPath, "Interfaces", "IDatamapFunctionService.cs"),
                serviceInterfaceWriter.ToString());

            Progress.Report("Datamaps generation completed successfully!");

            return new GeneratorResult { Success = true };
        }
        catch (Exception ex)
        {
            Progress.Report($"Error: {ex.Message}");
            return new GeneratorResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }

    #region Code Generation Methods

    private void WriteManager(CodeWriter writer, List<string> managerFunctions, List<string> managerConstructors)
    {
        writer.AddLine("using SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine("using SwiftlyS2.Core.Hooks;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.Datamaps;");
        writer.AddLine();
        writer.AddBlock("internal partial class DatamapFunctionManager", () =>
        {
            writer.AddLine("public HookManager HookManager { get; }");
            writer.AddLine();

            foreach (var func in managerFunctions)
            {
                writer.AddLine(func);
            }

            writer.AddLine();
            writer.AddBlock("public DatamapFunctionManager(HookManager hookManager)", () =>
            {
                writer.AddLine("HookManager = hookManager;");
                foreach (var ctor in managerConstructors)
                {
                    writer.AddLine(ctor);
                }
            });
        });
    }

    private void WriteService(CodeWriter writer, List<string> serviceFunctions)
    {
        writer.AddLine("using SwiftlyS2.Shared.Datamaps;");
        writer.AddLine("using SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.Datamaps;");
        writer.AddLine();
        writer.AddBlock("internal partial class DatamapFunctionService : IDatamapFunctionService", () =>
        {
            foreach (var func in serviceFunctions)
            {
                writer.AddLines(func.Split('\n'));
            }
        });
    }

    private void WriteServiceInterface(CodeWriter writer, List<string> serviceInterfaceFunctions)
    {
        writer.AddLine("using SwiftlyS2.Shared.Datamaps;");
        writer.AddLine("using SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Shared.Datamaps;");
        writer.AddLine();
        writer.AddBlock("public partial interface IDatamapFunctionService", () =>
        {
            foreach (var func in serviceInterfaceFunctions)
            {
                writer.AddLines(func.Split('\n'));
            }
        });
    }

    private void WriteHookContext(CodeWriter writer, string className, string functionName)
    {
        writer.AddLine("using SwiftlyS2.Shared.Datamaps;");
        writer.AddLine("using SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.Datamaps;");
        writer.AddLine();
        writer.AddBlock($"internal class DHook{functionName} : BaseDatamapFunctionHookContext<{className}>, IDHook{functionName}", () =>
        {
        });
    }

    private void WriteHookContextInterface(CodeWriter writer, string className, string functionName)
    {
        writer.AddLine("using SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Shared.Datamaps;");
        writer.AddLine();
        writer.AddLine($"public interface IDHook{functionName} : IDatamapFunctionHookContext<{className}>");
        writer.AddLine("{");
        writer.AddLine("}");
    }

    #endregion

    #region JSON Models

    private class DatamapsRoot
    {
        [System.Text.Json.Serialization.JsonPropertyName("datamaps")]
        public List<DatamapClass> Datamaps { get; set; } = new();
    }

    private class DatamapClass
    {
        [System.Text.Json.Serialization.JsonPropertyName("class_name")]
        public string ClassName { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("fields")]
        public List<DatamapField> Fields { get; set; } = new();
    }

    private class DatamapField
    {
        [System.Text.Json.Serialization.JsonPropertyName("fieldName")]
        public string FieldName { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("isFunction")]
        public bool IsFunction { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("functionHash")]
        public ulong FunctionHash { get; set; }
    }

    #endregion
}
