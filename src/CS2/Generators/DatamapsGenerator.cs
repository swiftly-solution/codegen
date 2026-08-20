using System.Text.Json;

namespace SwiftlyS2.Codegen.CS2.Generators;

/// <summary>
/// Generator for datamaps
/// </summary>
public class Datamaps : BaseGenerator
{
    private static readonly HttpClient httpClient = new();
    private const string DatamapsUrl = "https://raw.githubusercontent.com/Swiftly-Tracker/CS2-Dumps/main/dump/datamaps.json";

    public override string Name => "Datamaps";
    public override string OutputPath => Path.Combine("output", "src", "SwiftlyS2.Generated", "Datamaps");

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

            var jsonContent = await FetchDatamapsJsonAsync();
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

            var thinkFunctionOwners = ResolveThinkFunctionOwners(datamapsData.Datamaps);

            Progress.Report($"Processing {thinkFunctionOwners.Count} think functions...");

            foreach (var (functionName, className) in thinkFunctionOwners)
            {
                var name = functionName.Replace("::", "_");

                managerFunctions.Add($"public BaseDatamapFunction<{className}, DHook{name}> {name} {{ get; init; }}");
                managerConstructors.Add($"{name} = new(this, \"{functionName}\");");
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

    private async Task<string> FetchDatamapsJsonAsync()
    {
        Progress.Report("Downloading datamaps.json from CS2-Dumps...");
        return await httpClient.GetStringAsync(DatamapsUrl);
    }

    private static Dictionary<string, string> ResolveThinkFunctionOwners(List<DatamapClass> classes)
    {
        var candidates = new Dictionary<string, List<string>>();
        foreach (var clazz in classes)
        {
            foreach (var functionName in clazz.ThinkFunctions)
            {
                if (!candidates.TryGetValue(functionName, out var list))
                {
                    list = new List<string>();
                    candidates[functionName] = list;
                }
                if (!list.Contains(clazz.ClassName))
                {
                    list.Add(clazz.ClassName);
                }
            }
        }

        var owners = new Dictionary<string, string>();
        foreach (var (functionName, classNames) in candidates)
        {
            var owner = classNames
                .Where(c => functionName.StartsWith(c, StringComparison.Ordinal))
                .OrderByDescending(c => c.Length)
                .FirstOrDefault() ?? classNames[0];
            owners[functionName] = owner;
        }
        return owners;
    }

    #region Code Generation Methods

    private static void WriteManager(CodeWriter writer, List<string> managerFunctions, List<string> managerConstructors)
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

    private static void WriteService(CodeWriter writer, List<string> serviceFunctions)
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

    private static void WriteServiceInterface(CodeWriter writer, List<string> serviceInterfaceFunctions)
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

    private static void WriteHookContext(CodeWriter writer, string className, string functionName)
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

    private static void WriteHookContextInterface(CodeWriter writer, string className, string functionName)
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

        [System.Text.Json.Serialization.JsonPropertyName("think_functions")]
        public List<string> ThinkFunctions { get; set; } = new();
    }

    #endregion
}
