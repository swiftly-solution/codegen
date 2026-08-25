using System.Text.Json;

namespace SwiftlyS2.Codegen.CS2.Generators;

public class Datamaps : BaseGenerator
{
    private static readonly HttpClient httpClient = CreateHttpClient();
    private const string DatamapsUrl = "https://raw.githubusercontent.com/Swiftly-Tracker/CS2-Dumps/main/dump/datamaps.json";

    public override string Name => "Datamaps";
    public override string OutputPath => Path.Combine("output", "src", "SwiftlyS2.Generated", "Datamaps");

    private sealed record FunctionEntry(string FunctionName, string ClassName, string RawId, string StrippedId);

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

            var thinkFunctionOwners = ResolveThinkFunctionOwners(datamapsData.Datamaps);

            Progress.Report($"Processing {thinkFunctionOwners.Count} think functions...");

            var entries = BuildFunctionEntries(thinkFunctionOwners);

            foreach (var entry in entries)
            {
                var coreWriter = new CodeWriter();
                WriteFunctionCore(coreWriter, entry);
                await File.WriteAllTextAsync(
                    Path.Combine(OutputPath, "Classes", $"{entry.RawId}.cs"),
                    coreWriter.ToString());

                var sharedWriter = new CodeWriter();
                WriteFunctionShared(sharedWriter, entry);
                await File.WriteAllTextAsync(
                    Path.Combine(OutputPath, "Interfaces", $"{entry.RawId}.cs"),
                    sharedWriter.ToString());
            }

            Progress.Report("Writing per-class container files...");

            var groupedByClass = entries
                .GroupBy(e => e.ClassName)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            foreach (var group in groupedByClass)
            {
                var className = group.Key;
                var functions = group.OrderBy(e => e.StrippedId, StringComparer.Ordinal).ToList();

                var classCoreWriter = new CodeWriter();
                WriteClassContainerCore(classCoreWriter, className, functions);
                await File.WriteAllTextAsync(
                    Path.Combine(OutputPath, "Classes", $"GameHookDatamap{className}.cs"),
                    classCoreWriter.ToString());

                var classSharedWriter = new CodeWriter();
                WriteClassContainerShared(classSharedWriter, className, functions);
                await File.WriteAllTextAsync(
                    Path.Combine(OutputPath, "Interfaces", $"IGameHookDatamap{className}.cs"),
                    classSharedWriter.ToString());
            }

            Progress.Report("Writing root aggregate files...");

            var classNames = groupedByClass.Select(g => g.Key).ToList();

            var rootCoreWriter = new CodeWriter();
            WriteGameHookDatamapsCore(rootCoreWriter, classNames);
            await File.WriteAllTextAsync(
                Path.Combine(OutputPath, "Classes", "GameHookDatamaps.cs"),
                rootCoreWriter.ToString());

            var rootSharedWriter = new CodeWriter();
            WriteGameHookDatamapsShared(rootSharedWriter, classNames);
            await File.WriteAllTextAsync(
                Path.Combine(OutputPath, "Interfaces", "IGameHookDatamaps.cs"),
                rootSharedWriter.ToString());

            Progress.Report("Writing DatamapHookListener enum and dispatch...");

            var listenerWriter = new CodeWriter();
            WriteDatamapHookListener(listenerWriter, entries);
            await File.WriteAllTextAsync(
                Path.Combine(OutputPath, "Classes", "DatamapHookListener.cs"),
                listenerWriter.ToString());

            Progress.Report("Writing GameHooksService.Datamaps partial...");

            var serviceWriter = new CodeWriter();
            WriteGameHooksServiceDatamapsPartial(serviceWriter, groupedByClass);
            await File.WriteAllTextAsync(
                Path.Combine(OutputPath, "Classes", "GameHooksService.Datamaps.cs"),
                serviceWriter.ToString());

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

    private List<FunctionEntry> BuildFunctionEntries(Dictionary<string, string> thinkFunctionOwners)
    {
        var entries = new List<FunctionEntry>();
        var usedStrippedIdsByClass = new Dictionary<string, HashSet<string>>();

        foreach (var (functionName, className) in thinkFunctionOwners.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var rawId = SanitizeIdentifier(functionName);

            string strippedRaw;
            if (functionName.StartsWith(className, StringComparison.Ordinal))
            {
                strippedRaw = functionName.Substring(className.Length);
            }
            else
            {
                Progress.Report($"Warning: owner class '{className}' is not a string-prefix of function '{functionName}'; using full function name as the leaf property name.");
                strippedRaw = functionName;
            }

            var strippedId = SanitizeIdentifier(strippedRaw);
            if (strippedId.Length == 0)
            {
                strippedId = rawId;
            }
            else if (!(char.IsLetter(strippedId[0]) || strippedId[0] == '_'))
            {
                strippedId = "_" + strippedId;
            }

            if (!usedStrippedIdsByClass.TryGetValue(className, out var used))
            {
                used = new HashSet<string>();
                usedStrippedIdsByClass[className] = used;
            }

            if (!used.Add(strippedId))
            {
                Progress.Report($"Warning: stripped name '{strippedId}' collides under class '{className}'; falling back to raw id '{rawId}' for function '{functionName}'.");
                strippedId = rawId;
                used.Add(strippedId);
            }

            entries.Add(new FunctionEntry(functionName, className, rawId, strippedId));
        }

        return entries;
    }

    private static string SanitizeIdentifier(string raw) => raw.Replace("::", "_");

    private static void WriteFunctionCore(CodeWriter writer, FunctionEntry entry)
    {
        var rawId = entry.RawId;
        var className = entry.ClassName;

        writer.AddUsings(
            "Spectre.Console",
            "SwiftlyS2.Core.Natives",
            "SwiftlyS2.Shared",
            "SwiftlyS2.Shared.GameHooks",
            "SwiftlyS2.Shared.Memory",
            "SwiftlyS2.Shared.Misc",
            "SwiftlyS2.Shared.SchemaDefinitions"
        );
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.GameHooks;");
        writer.AddLine();

        writer.AddBlock("internal static partial class DatamapHooksPublisher", () =>
        {
            writer.AddLine($"private delegate void {rawId}Delegate(nint a1);");
            writer.AddLine();
            writer.AddLine($"private static IUnmanagedFunction<{rawId}Delegate>? {rawId}UnmanagedFunction;");
            writer.AddLine($"private static Guid {rawId}HookGuid;");
            writer.AddLine();

            writer.AddBlock($"private static IUnmanagedFunction<{rawId}Delegate> {rawId}GetUnmanagedFunction()", () =>
            {
                writer.AddBlock($"if ({rawId}UnmanagedFunction == null)", () =>
                {
                    writer.AddBlock("if (_core == null)", () =>
                    {
                        writer.AddLine("throw new InvalidOperationException(\"GameHooksCore is not initialized.\");");
                    });
                    writer.AddLine($"var address = NativeSchema.GetDatamapFunction(\"{EscapeString(className)}\", \"{EscapeString(entry.FunctionName)}\");");
                    writer.AddBlock("if (address == nint.Zero)", () =>
                    {
                        writer.AddLine($"throw new InvalidOperationException(\"Failed to find the address of the datamap function {EscapeString(className)}::{EscapeString(entry.FunctionName)}.\");");
                    });
                    writer.AddLine($"{rawId}UnmanagedFunction = _core.Memory.GetUnmanagedFunctionByAddress<{rawId}Delegate>(address);");
                });
                writer.AddLine($"return {rawId}UnmanagedFunction;");
            });
            writer.AddLine();

            writer.AddBlock($"internal static Guid Hook{rawId}()", () =>
            {
                writer.AddLine($"{rawId}HookGuid = {rawId}GetUnmanagedFunction().AddHook(next => (a1) => {rawId}Pipeline(a1, () => next()(a1)));");
                writer.AddLine($"return {rawId}HookGuid;");
            });
            writer.AddLine();

            writer.AddBlock($"internal static Guid Unhook{rawId}()", () =>
            {
                writer.AddLine($"{rawId}GetUnmanagedFunction().RemoveHook({rawId}HookGuid);");
                writer.AddLine("return Guid.Empty;");
            });
            writer.AddLine();

            writer.AddBlock($"private static void {rawId}Pipeline(nint a1, Action callOriginal)", () =>
            {
                writer.AddBlock("try", () =>
                {
                    writer.AddLine($"var schemaObject = Helper.AsSchema<{className}>(a1);");
                    writer.AddLine();
                    writer.AddLine($"var preCtx = new {rawId}PreContext {{ SchemaObject = schemaObject }};");
                    writer.AddLine($"Invoke{rawId}Pre(ref preCtx);");
                    writer.AddBlock("if (preCtx.HookResult == HookResult.Stop || preCtx.HookResult == HookResult.CancelOriginal)", () =>
                    {
                        writer.AddLine("return;");
                    });
                    writer.AddLine();
                    writer.AddLine("callOriginal();");
                    writer.AddLine();
                    writer.AddLine($"var postCtx = new {rawId}PostContext {{ SchemaObject = schemaObject }};");
                    writer.AddLine($"Invoke{rawId}Post(ref postCtx);");
                });
                writer.AddBlock("catch (Exception e)", () =>
                {
                    writer.AddBlock("if (!GlobalExceptionHandler.Handle(ref e))", () =>
                    {
                        writer.AddLine("return;");
                    });
                    writer.AddLine("AnsiConsole.WriteException(e);");
                });
            });
            writer.AddLine();

            writer.AddBlock($"internal static void Invoke{rawId}(nint a1)", () =>
            {
                writer.AddLine($"{rawId}GetUnmanagedFunction().CallOriginal(a1);");
            });
            writer.AddLine();

            writer.AddBlock($"internal static void Invoke{rawId}Pre(ref {rawId}PreContext ctx)", () =>
            {
                writer.AddBlock("lock (subscribersLock)", () =>
                {
                    writer.AddBlock("for (var i = 0; i < subscribers.Count; i++)", () =>
                    {
                        writer.AddLine($"subscribers[i].Invoke{rawId}Pre(ref ctx);");
                        writer.AddBlock("if (ctx.HookResult == HookResult.Stop || ctx.HookResult == HookResult.Handled)", () =>
                        {
                            writer.AddLine("return;");
                        });
                    });
                });
            });
            writer.AddLine();

            writer.AddBlock($"internal static void Invoke{rawId}Post(ref {rawId}PostContext ctx)", () =>
            {
                writer.AddBlock("lock (subscribersLock)", () =>
                {
                    writer.AddBlock("for (var i = 0; i < subscribers.Count; i++)", () =>
                    {
                        writer.AddLine($"subscribers[i].Invoke{rawId}Post(ref ctx);");
                        writer.AddBlock("if (ctx.HookResult == HookResult.Stop || ctx.HookResult == HookResult.Handled)", () =>
                        {
                            writer.AddLine("return;");
                        });
                    });
                });
            });
        });
        writer.AddLine();

        writer.AddBlock($"internal sealed class {rawId}Hook : I{rawId}Hook", () =>
        {
            writer.AddLine($"private event On{rawId}PreDelegate? _Pre;");
            writer.AddLine($"private event On{rawId}PostDelegate? _Post;");
            writer.AddLine();

            writer.AddBlock($"public event On{rawId}PreDelegate Pre", () =>
            {
                writer.AddBlock("add", () =>
                {
                    writer.AddBlock("if (_Pre == null)", () =>
                    {
                        writer.AddLine($"DatamapHooksPublisher.AddHookListener(DatamapHookListener.{rawId});");
                    });
                    writer.AddLine("_Pre += value;");
                });
                writer.AddBlock("remove", () =>
                {
                    writer.AddLine("_Pre -= value;");
                    writer.AddBlock("if (_Pre == null)", () =>
                    {
                        writer.AddLine($"DatamapHooksPublisher.RemoveHookListener(DatamapHookListener.{rawId});");
                    });
                });
            });
            writer.AddLine();

            writer.AddBlock($"public event On{rawId}PostDelegate Post", () =>
            {
                writer.AddBlock("add", () =>
                {
                    writer.AddBlock("if (_Post == null)", () =>
                    {
                        writer.AddLine($"DatamapHooksPublisher.AddHookListener(DatamapHookListener.{rawId});");
                    });
                    writer.AddLine("_Post += value;");
                });
                writer.AddBlock("remove", () =>
                {
                    writer.AddLine("_Post -= value;");
                    writer.AddBlock("if (_Post == null)", () =>
                    {
                        writer.AddLine($"DatamapHooksPublisher.RemoveHookListener(DatamapHookListener.{rawId});");
                    });
                });
            });
            writer.AddLine();

            writer.AddLine($"public void InvokePre(ref {rawId}PreContext ctx) => _Pre?.Invoke(ref ctx);");
            writer.AddLine($"public void InvokePost(ref {rawId}PostContext ctx) => _Post?.Invoke(ref ctx);");
            writer.AddLine();

            writer.AddLine("public bool HasPreListeners => _Pre != null;");
            writer.AddLine("public bool HasPostListeners => _Post != null;");
            writer.AddLine();

            writer.AddBlock("public void UnregisterListeners()", () =>
            {
                writer.AddBlock("if (_Pre != null)", () =>
                {
                    writer.AddLine($"DatamapHooksPublisher.RemoveHookListener(DatamapHookListener.{rawId});");
                });
                writer.AddBlock("if (_Post != null)", () =>
                {
                    writer.AddLine($"DatamapHooksPublisher.RemoveHookListener(DatamapHookListener.{rawId});");
                });
            });
            writer.AddLine();

            writer.AddLine($"public void Invoke({className} schemaObject) => DatamapHooksPublisher.Invoke{rawId}(schemaObject.Address);");
        });
    }

    private static void WriteFunctionShared(CodeWriter writer, FunctionEntry entry)
    {
        var rawId = entry.RawId;
        var className = entry.ClassName;

        writer.AddUsings(
            "SwiftlyS2.Shared.Misc",
            "SwiftlyS2.Shared.SchemaDefinitions"
        );
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Shared.GameHooks;");
        writer.AddLine();

        writer.AddBlock($"public ref struct {rawId}PreContext", () =>
        {
            writer.AddLine($"public {className} SchemaObject;");
            writer.AddLine("private HookResult _hookResult;");
            writer.AddLine("public void SetHookResult(HookResult result) => _hookResult = result;");
            writer.AddLine("internal HookResult HookResult => _hookResult;");
        });
        writer.AddLine();

        writer.AddBlock($"public ref struct {rawId}PostContext", () =>
        {
            writer.AddLine($"public {className} SchemaObject;");
            writer.AddLine("private HookResult _hookResult;");
            writer.AddLine("public void SetHookResult(HookResult result) => _hookResult = result;");
            writer.AddLine("internal HookResult HookResult => _hookResult;");
        });
        writer.AddLine();

        writer.AddLine($"public delegate void On{rawId}PreDelegate(ref {rawId}PreContext ctx);");
        writer.AddLine($"public delegate void On{rawId}PostDelegate(ref {rawId}PostContext ctx);");
        writer.AddLine();

        writer.AddBlock($"public interface I{rawId}Hook", () =>
        {
            writer.AddLine($"public event On{rawId}PreDelegate Pre;");
            writer.AddLine($"public event On{rawId}PostDelegate Post;");
            writer.AddLine();
            writer.AddLine($"public void Invoke({className} schemaObject);");
        });
    }

    private static void WriteClassContainerCore(CodeWriter writer, string className, List<FunctionEntry> functions)
    {
        writer.AddUsing("SwiftlyS2.Shared.GameHooks");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.GameHooks;");
        writer.AddLine();
        writer.AddBlock($"internal sealed class GameHookDatamap{className} : IGameHookDatamap{className}", () =>
        {
            foreach (var f in functions)
            {
                writer.AddLine($"internal readonly {f.RawId}Hook {f.RawId}Hook = new();");
            }
            writer.AddLine();
            foreach (var f in functions)
            {
                writer.AddLine($"public I{f.RawId}Hook {f.StrippedId} => {f.RawId}Hook;");
            }
            writer.AddLine();
            writer.AddBlock("internal void UnregisterListeners()", () =>
            {
                foreach (var f in functions)
                {
                    writer.AddLine($"{f.RawId}Hook.UnregisterListeners();");
                }
            });
        });
    }

    private static void WriteClassContainerShared(CodeWriter writer, string className, List<FunctionEntry> functions)
    {
        writer.AddLine("namespace SwiftlyS2.Shared.GameHooks;");
        writer.AddLine();
        writer.AddBlock($"public interface IGameHookDatamap{className}", () =>
        {
            foreach (var f in functions)
            {
                writer.AddLine($"public I{f.RawId}Hook {f.StrippedId} {{ get; }}");
            }
        });
    }

    private static void WriteGameHookDatamapsCore(CodeWriter writer, List<string> classNames)
    {
        writer.AddUsing("SwiftlyS2.Shared.GameHooks");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.GameHooks;");
        writer.AddLine();
        writer.AddBlock("internal sealed class GameHookDatamaps : IGameHookDatamaps", () =>
        {
            foreach (var className in classNames)
            {
                writer.AddLine($"internal readonly GameHookDatamap{className} {className}Hook = new();");
            }
            writer.AddLine();
            foreach (var className in classNames)
            {
                writer.AddLine($"public IGameHookDatamap{className} {className} => {className}Hook;");
            }
            writer.AddLine();
            writer.AddBlock("internal void UnregisterAllListeners()", () =>
            {
                foreach (var className in classNames)
                {
                    writer.AddLine($"{className}Hook.UnregisterListeners();");
                }
            });
        });
    }

    private static void WriteGameHookDatamapsShared(CodeWriter writer, List<string> classNames)
    {
        writer.AddLine("namespace SwiftlyS2.Shared.GameHooks;");
        writer.AddLine();
        writer.AddBlock("public interface IGameHookDatamaps", () =>
        {
            foreach (var className in classNames)
            {
                writer.AddLine($"public IGameHookDatamap{className} {className} {{ get; }}");
            }
        });
    }

    private static void WriteDatamapHookListener(CodeWriter writer, List<FunctionEntry> entries)
    {
        writer.AddLine("namespace SwiftlyS2.Core.GameHooks;");
        writer.AddLine();
        writer.AddBlock("internal enum DatamapHookListener", () =>
        {
            foreach (var f in entries)
            {
                writer.AddLine($"{f.RawId},");
            }
        });
        writer.AddLine();

        writer.AddBlock("internal static partial class DatamapHooksPublisher", () =>
        {
            writer.AddBlock("internal static Guid HookFunction(DatamapHookListener hookName)", () =>
            {
                writer.AddBlock("return hookName switch", () =>
                {
                    foreach (var f in entries)
                    {
                        writer.AddLine($"DatamapHookListener.{f.RawId} => Hook{f.RawId}(),");
                    }
                    writer.AddLine("_ => throw new ArgumentOutOfRangeException(nameof(hookName), $\"No hook found for {hookName}\"),");
                }, "{", "};");
            });
            writer.AddLine();

            writer.AddBlock("internal static Guid UnhookFunction(DatamapHookListener hookName)", () =>
            {
                writer.AddBlock("return hookName switch", () =>
                {
                    foreach (var f in entries)
                    {
                        writer.AddLine($"DatamapHookListener.{f.RawId} => Unhook{f.RawId}(),");
                    }
                    writer.AddLine("_ => throw new ArgumentOutOfRangeException(nameof(hookName), $\"No hook found for {hookName}\"),");
                }, "{", "};");
            });
        });
    }

    private static void WriteGameHooksServiceDatamapsPartial(CodeWriter writer, List<IGrouping<string, FunctionEntry>> groupedByClass)
    {
        writer.AddUsings(
            "Microsoft.Extensions.Logging",
            "SwiftlyS2.Shared.GameHooks"
        );
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.GameHooks;");
        writer.AddLine();
        writer.AddBlock("internal sealed partial class GameHooksService", () =>
        {
            writer.AddLine("internal readonly GameHookDatamaps DatamapsHook = new();");
            writer.AddLine("public IGameHookDatamaps Datamaps => DatamapsHook;");
            writer.AddLine();

            writer.AddBlock("internal void SubscribeDatamapsHooks()", () =>
            {
                writer.AddLine("DatamapHooksPublisher.Subscribe(this);");
            });
            writer.AddLine();

            writer.AddBlock("internal void DisposeDatamapsHooks()", () =>
            {
                writer.AddLine("DatamapsHook.UnregisterAllListeners();");
                writer.AddLine("DatamapHooksPublisher.Unsubscribe(this);");
            });
            writer.AddLine();

            foreach (var group in groupedByClass)
            {
                var className = group.Key;
                foreach (var f in group)
                {
                    writer.AddBlock($"internal void Invoke{f.RawId}Pre(ref {f.RawId}PreContext ctx)", () =>
                    {
                        writer.AddBlock($"if (!DatamapsHook.{className}Hook.{f.RawId}Hook.HasPreListeners)", () =>
                        {
                            writer.AddLine("return;");
                        });
                        writer.AddLine();
                        writer.AddBlock("try", () =>
                        {
                            writer.AddLine($"DatamapsHook.{className}Hook.{f.RawId}Hook.InvokePre(ref ctx);");
                        });
                        writer.AddBlock("catch (Exception e)", () =>
                        {
                            writer.AddBlock("if (GlobalExceptionHandler.Handle(ref e))", () =>
                            {
                                writer.AddLine($"logger.LogError(e, \"Error invoking GameHooks::Datamaps::{className}::{f.StrippedId}::Pre.\");");
                            });
                        });
                    });
                    writer.AddLine();

                    writer.AddBlock($"internal void Invoke{f.RawId}Post(ref {f.RawId}PostContext ctx)", () =>
                    {
                        writer.AddBlock($"if (!DatamapsHook.{className}Hook.{f.RawId}Hook.HasPostListeners)", () =>
                        {
                            writer.AddLine("return;");
                        });
                        writer.AddLine();
                        writer.AddBlock("try", () =>
                        {
                            writer.AddLine($"DatamapsHook.{className}Hook.{f.RawId}Hook.InvokePost(ref ctx);");
                        });
                        writer.AddBlock("catch (Exception e)", () =>
                        {
                            writer.AddBlock("if (GlobalExceptionHandler.Handle(ref e))", () =>
                            {
                                writer.AddLine($"logger.LogError(e, \"Error invoking GameHooks::Datamaps::{className}::{f.StrippedId}::Post.\");");
                            });
                        });
                    });
                    writer.AddLine();
                }
            }
        });
    }

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
}
