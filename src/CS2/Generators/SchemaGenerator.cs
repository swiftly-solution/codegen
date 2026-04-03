using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic.FileIO;
using SwiftlyS2.Codegen.CS2.Schema;

namespace SwiftlyS2.Codegen.CS2.Generators;


/// <summary>
/// Generator for schema
/// </summary>
public class SchemaGenerator : BaseGenerator
{
    /// <inheritdoc />
    public override string Name => "Schema";

    /// <inheritdoc />
    public override string OutputPath => Path.Combine(Entrypoint.ProjectRootPath, "output", "src", "SwiftlyS2.Generated", "Schemas");

    private readonly string _schemasPath;

    /// <inheritdoc />
    public override string? DataPath
    {
        get => _schemasPath;
        protected set { }
    }

    private static HashSet<string> ErasedGenerics = [
        "CUtlVector",
        "CUtlVectorFixedGrowable",
        "CUtlVectorEmbeddedNetworkVar",
        "CNetworkUtlVectorBase",
    ];

    /// <summary>
    /// Initializes a new instance of the Schema generator
    /// </summary>
    /// <param name="schemasPath">Path to the schemas folder</param>
    public SchemaGenerator(string? schemasPath = null)
    {
        _schemasPath = schemasPath ?? Path.Combine(Entrypoint.ProjectRootPath, "data", "schemas");
    }

    public override async Task<GeneratorResult> GenerateFilesAsync()
    {
        try
        {
            Progress.Report("Loading SDK file...");
            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }
            Directory.CreateDirectory(OutputPath);
            Directory.CreateDirectory(Path.Combine(OutputPath, "Interfaces"));
            Directory.CreateDirectory(Path.Combine(OutputPath, "Classes"));
            Directory.CreateDirectory(Path.Combine(OutputPath, "Enums"));

            Progress.Report($"Reading SDK from: {_schemasPath}");

            var jsonContent = await File.ReadAllTextAsync(Path.Combine(_schemasPath, "sdk.json"));
            var sdkData = JsonSerializer.Deserialize<SDK>(jsonContent);
            Progress.Report($"Loaded SDK with {sdkData!.Enums.Count} enums and {sdkData!.Classes.Count} classes.");

            var entitySystemJsonContent = await File.ReadAllTextAsync(Path.Combine(_schemasPath, "entitysystem.json"));
            var entitySystemData = JsonSerializer.Deserialize<EntitySystem>(entitySystemJsonContent);

            Progress.Report($"Loaded Entity System with {entitySystemData!.EntityClasses.Count} entity classes.");

            var allClassNames = sdkData.Classes.Select(c => c.Name.Replace(":", "_")).ToList();
            var allEnumNames = sdkData.Enums.Select(e => e.Name.Replace(":", "_")).ToList();

            var enumsTask = Task.Run(() =>
            {
                foreach (var enumeration in sdkData.Enums)
                {
                    WriteEnum(enumeration);
                }
                Progress.Report($"Generated {sdkData.Enums.Count} enums.");
            });

            var classesTask = Task.Run(() =>
            {
                foreach (var @class in sdkData.Classes)
                {
                    WriteClass(@class, entitySystemData, allClassNames, allEnumNames);
                }

                Progress.Report($"Generated {sdkData.Classes.Count} classes.");
            });

            await Task.WhenAll(enumsTask, classesTask);

            WriteClassConvertor(entitySystemData);

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

    private void WriteClassConvertor(EntitySystem entitySystemData)
    {
        var writer = new CodeWriter();
        writer.AddLine("using SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine("");
        writer.AddLine("namespace SwiftlyS2.Core.SchemaDefinitions;");
        writer.AddLine("");
        writer.AddBlock("internal static class ClassConvertor", () =>
        {
            writer.AddBlock("public static CEntityInstance ConvertEntityByDesignerName( nint address, string designerName )", () =>
            {
                writer.AddBlock("return designerName switch", () =>
                {
                    foreach (var entityClass in entitySystemData.EntityClasses)
                    {
                        writer.AddLine($"\"{entityClass.DesignerName}\" => new {ClassNameConvertor.GetImplementationName(entityClass.ClassName)}(address),");
                    }
                    writer.AddLine($"_ => new {ClassNameConvertor.GetImplementationName("CEntityInstance")}(address),");
                }, closeBrace: "};");
            });
        });

        var filePath = Path.Combine(OutputPath, "ClassConvertor.cs");
        File.WriteAllText(filePath, writer.GetCode());
    }

    private void WriteClass(Class @class, EntitySystem entitySystemData, List<string> allClassNames, List<string> allEnumNames)
    {
        if (FieldTypeParser.GetBlacklistedClasses().Contains(@class.Name))
        {
            Progress.Report($"Skipping blacklisted class: {@class.Name}");
            return;
        }

        WriteInterface(@class, entitySystemData, allClassNames, allEnumNames);
        WriteImplementation(@class, entitySystemData, allClassNames, allEnumNames);
    }

    private void WriteImplementation(Class @class, EntitySystem entitySystemData, List<string> allClassNames, List<string> allEnumNames)
    {
        var duplicatedCounter = 0;
        List<string> existingNames = [];

        var interfaceName = ClassNameConvertor.GetInterfaceName(@class.Name);
        var implementationName = ClassNameConvertor.GetImplementationName(@class.Name);

        var baseClass = @class.BaseClassesCount > 0 ? @class.BaseClasses!.First() : "SchemaClass";
        var baseClassImplementationName = ClassNameConvertor.GetImplementationName(baseClass);

        var writer = new CodeWriter();

        writer.AddLine("// <auto-generated />");
        writer.AddLine("#pragma warning disable CS0108");
        writer.AddLine("#nullable enable");
        writer.AddLine();
        writer.AddLine("using System;");
        writer.AddLine("using System.Threading;");
        writer.AddLine("using SwiftlyS2.Core.Schemas;");
        writer.AddLine("using SwiftlyS2.Shared.Schemas;");
        writer.AddLine("using SwiftlyS2.Shared.Natives;");
        writer.AddLine("using SwiftlyS2.Core.Extensions;");
        writer.AddLine("using SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.SchemaDefinitions;");
        writer.AddLine();

        writer.AddBlock($"internal partial class {implementationName} : {baseClassImplementationName}, {interfaceName}", () =>
        {
            writer.AddLine($"public {implementationName}(nint handle) : base(handle) {{ }}");
            writer.AddLine();
            if (@class.FieldsCount > 0)
            {
                List<string> Updators = [];

                foreach (var rawField in @class.Fields!)
                {
                    var field = FieldParser.ParseField(rawField, allClassNames, allEnumNames);

                    if (existingNames.Contains(field.Name))
                    {
                        duplicatedCounter++;
                        field.Name += $"{duplicatedCounter}";
                    }
                    else
                    {
                        existingNames.Add(field.Name);
                    }

                    if (field.Networked)
                    {
                        Updators.Add($"public void {field.Name}Updated() => Schema.Update(_Handle, {field.Hash});");
                    }

                    field.RefMethod = field.Kind == "ptr" ? "Deref" : "AsRef";

                    writer.AddLine($"private static nint? _{field.Name}Offset;");
                    writer.AddLine();
                    if (field.IsFixedCharString)
                    {
                        writer.AddBlock($"public string {field.Name}", () =>
                        {
                            writer.AddBlock("get", () =>
                            {
                                writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                writer.AddLine($"return Schema.GetString(_Handle + _{field.Name}Offset!.Value);");
                            });

                            writer.AddBlock("set", () =>
                            {
                                writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                writer.AddLine($"Schema.SetFixedString(_Handle, _{field.Name}Offset!.Value, value, {field.ElementCount});");
                            });
                        });
                    }
                    else if (field.IsUtlStringHandle)
                    {
                        if (field.Kind == "fixed_array")
                        {
                            field.ImplementationType = "SchemaUtlStringFixedArray";
                            field.InterfaceType = "ISchemaUtlStringFixedArray";

                            writer.RemoveLine();
                            writer.RemoveLine();

                            writer.AddBlock($"public {field.InterfaceType} {field.Name}", () =>
                            {
                                writer.AddLine($"get => new {field.ImplementationType}(_Handle, {field.Hash}, {field.ElementCount}, {field.ElementSize}, {field.ElementAlignment});");
                            });
                        }
                        else
                        {
                            writer.AddBlock($"public string {field.Name}", () =>
                            {
                                writer.AddBlock("get", () =>
                                {
                                    writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                    writer.AddLine($"return Schema.GetCUtlString(_Handle.Read<nint>(_{field.Name}Offset!.Value));");
                                });

                                writer.AddBlock("set", () =>
                                {
                                    writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                    writer.AddLine($"Schema.SetCUtlString(_Handle, _{field.Name}Offset!.Value, value);");
                                });
                            });
                        }
                    }
                    else if (field.IsCharPtrString || field.IsStringHandle)
                    {
                        if (field.Kind == "fixed_array")
                        {
                            field.ImplementationType = "SchemaStringFixedArray";
                            field.InterfaceType = "ISchemaStringFixedArray";

                            writer.RemoveLine();
                            writer.RemoveLine();

                            writer.AddBlock($"public {field.InterfaceType} {field.Name}", () =>
                            {
                                writer.AddLine($"get => new {field.ImplementationType}(_Handle, {field.Hash}, {field.ElementCount}, {field.ElementSize}, {field.ElementAlignment});");
                            });
                        }
                        else
                        {
                            writer.AddBlock($"public string {field.Name}", () =>
                            {
                                writer.AddBlock("get", () =>
                                {
                                    writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                    writer.AddLine($"return Schema.GetString(_Handle.Read<nint>(_{field.Name}Offset!.Value));");
                                });

                                writer.AddBlock("set", () =>
                                {
                                    writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                    writer.AddLine($"Schema.SetString(_Handle, _{field.Name}Offset!.Value, value);");
                                });
                            });
                        }
                    }
                    else if (field.Kind == "fixed_array" && field.ImplementationType != "SchemaUntypedField")
                    {
                        writer.RemoveLine();
                        writer.RemoveLine();

                        writer.AddBlock($"public {field.InterfaceType} {field.Name}", () =>
                        {
                            writer.AddLine($"get => new {field.ImplementationType}(_Handle, {field.Hash}, {field.ElementCount}, {field.ElementSize}, {field.ElementAlignment});");
                        });
                    }
                    else if (field.IsValueType)
                    {
                        writer.AddBlock($"public ref {field.ImplementationType} {field.Name}", () =>
                        {
                            writer.AddBlock("get", () =>
                            {
                                writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                writer.AddLine($"return ref _Handle.{field.RefMethod}<{field.ImplementationType}>(_{field.Name}Offset!.Value);");
                            });
                        });
                    }
                    else
                    {
                        if (field.Kind == "ptr" && !FieldTypeParser.GetManagedTypes().Contains(field.ImplementationType))
                        {
                            writer.AddBlock($"public {field.InterfaceType}? {field.Name}", () =>
                            {
                                writer.AddBlock("get", () =>
                                {
                                    writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                    writer.AddLine($"var ptr = _Handle.Read<nint>(_{field.Name}Offset!.Value);");
                                    writer.AddLine($"return ptr.IsValidPtr() ? new {field.ImplementationType}(ptr) : null;");
                                });
                            });
                        }
                        else
                        {
                            writer.AddBlock($"public {field.InterfaceType} {field.Name}", () =>
                            {
                                writer.AddBlock("get", () =>
                                {
                                    writer.AddLine($"_{field.Name}Offset = _{field.Name}Offset ?? Schema.GetOffset({field.Hash});");
                                    writer.AddLine($"return new {field.ImplementationType}(_Handle + _{field.Name}Offset!.Value);");
                                });
                            });
                        }
                    }
                }

                writer.AddLine();
                writer.AddLines(Updators);
            }
        });

        var filePath = Path.Combine(OutputPath, "Classes", $"{implementationName}.cs");
        File.WriteAllText(filePath, writer.GetCode());
    }

    private void WriteInterface(Class @class, EntitySystem entitySystemData, List<string> allClassNames, List<string> allEnumNames)
    {
        var duplicatedCounter = 0;
        List<string> existingNames = [];

        var interfaceName = ClassNameConvertor.GetInterfaceName(@class.Name);
        var implementationName = ClassNameConvertor.GetImplementationName(@class.Name);

        var baseClass = @class.BaseClassesCount > 0 ? @class.BaseClasses!.First() : "SchemaClass";
        var baseClassInterfaceName = ClassNameConvertor.GetInterfaceName(baseClass);

        var designerName = "null";
        var entityClass = entitySystemData.EntityClasses.FirstOrDefault(ec => ec.ClassName == @class.Name);
        if (entityClass != null)
        {
            designerName = $"\"{entityClass.DesignerName}\"";
        }

        var writer = new CodeWriter();

        writer.AddLine("// <auto-generated />");
        writer.AddLine("#pragma warning disable CS0108");
        writer.AddLine("#nullable enable");
        writer.AddLine();
        writer.AddLine("using SwiftlyS2.Shared.Schemas;");
        writer.AddLine("using SwiftlyS2.Shared.Natives;");
        writer.AddLine("using SwiftlyS2.Core.SchemaDefinitions;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine();
        writer.AddBlock($"public partial interface {interfaceName} : {(baseClass == "SchemaClass" ? "" : $"{baseClassInterfaceName}, ")}ISchemaClass<{interfaceName}>", () =>
        {
            writer.AddLine($"static {interfaceName} ISchemaClass<{interfaceName}>.From(nint handle) => new {implementationName}(handle);");
            writer.AddLine($"static int ISchemaClass<{interfaceName}>.Size => {@class.Size};");
            writer.AddLine($"static string? ISchemaClass<{interfaceName}>.ClassName => {designerName};");
            writer.AddLine();
            if (@class.FieldsCount > 0)
            {
                List<string> Updators = [];

                foreach (var rawField in @class.Fields!)
                {
                    var field = FieldParser.ParseField(rawField, allClassNames, allEnumNames);

                    if (existingNames.Contains(field.Name))
                    {
                        duplicatedCounter++;
                        field.Name += $"{duplicatedCounter}";
                    }
                    else
                    {
                        existingNames.Add(field.Name);
                    }

                    if (field.Networked)
                    {
                        Updators.Add($"public void {field.Name}Updated();");
                    }

                    field.Setter = "";

                    if (field.IsFixedCharString || field.IsCharPtrString || field.IsStringHandle || field.IsUtlStringHandle)
                    {
                        field.Ref = "";
                        field.InterfaceType = "string";
                        field.Nullable = "";
                        field.Setter = " set;";
                        if (field.Kind == "fixed_array" && (field.IsStringHandle || field.IsUtlStringHandle))
                        {
                            field.InterfaceType = field.IsStringHandle ? "ISchemaStringFixedArray" : "ISchemaUtlStringFixedArray";
                            field.Setter = "";
                        }
                    }
                    else
                    {
                        field.Ref = field.IsValueType ? "ref " : "";
                        field.Nullable = field.Kind == "ptr" && !field.IsValueType && field.ImplementationType != "SchemaUntypedField" ? "?" : "";
                    }

                    field.Comment = "";

                    if (ErasedGenerics.Contains(field.ImplementationType) || field.ImplementationType == "SchemaUntypedField")
                    {
                        field.Comment = $"// {rawField.Templated ?? rawField.Type}";
                    }

                    writer.AddLine();
                    if (field.Comment != "") writer.AddLine(field.Comment);
                    writer.AddLine($"public {field.Ref}{field.InterfaceType}{field.Nullable} {field.Name} {{ get;{field.Setter} }}");
                }

                writer.AddLine();
                if (Updators.Count == 0) writer.AddLine();
                else writer.AddLines(Updators);
            }
        });

        var filePath = Path.Combine(OutputPath, "Interfaces", $"{interfaceName}.cs");
        File.WriteAllText(filePath, writer.GetCode());
    }

    private void WriteEnum(Enum enumeration)
    {
        var baseType = enumeration.Size switch
        {
            1 => "byte",
            2 => "ushort",
            4 => "uint",
            8 => "ulong",
            _ => throw new Exception($"Unsupported enum size: {enumeration.Size}")
        };

        var enumName = enumeration.Name.Replace(":", "_");

        var writer = new CodeWriter();
        writer.AddLine("// <auto-generated />");
        writer.AddLine();
        writer.AddLine("using SwiftlyS2.Shared.Schemas;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Shared.SchemaDefinitions;");
        writer.AddLine();
        writer.AddBlock($"public enum {enumName} : {baseType}", () =>
        {
            foreach (var field in enumeration.Fields)
            {
                var value = field.Value.ToString();

                if (field.Value < 0) value = $"{baseType}.MaxValue{(field.Value == -1 ? "" : $" - {Math.Abs(field.Value + 1)}")}";

                writer.AddLine($"{field.Name} = {value},");
            }
        });

        var filePath = Path.Combine(OutputPath, "Enums", $"{enumName}.cs");
        File.WriteAllText(filePath, writer.GetCode() + "\n");
    }
}

public class EnumFieldEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public long Value { get; set; } = 0;
}

public class Enum
{
    [JsonPropertyName("alignment")]
    public int Alignment { get; set; } = 0;

    [JsonPropertyName("fields_count")]
    public int FieldsCount { get; set; } = 0;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; } = 0;

    [JsonPropertyName("fields")]
    public List<EnumFieldEntry> Fields { get; set; } = [];
}

public class Class
{
    [JsonPropertyName("alignment")]
    public int Alignment { get; set; } = 0;

    [JsonPropertyName("base_classes")]
    public List<string>? BaseClasses { get; set; } = [];

    [JsonPropertyName("base_classes_count")]
    public int? BaseClassesCount { get; set; } = 0;

    [JsonPropertyName("fields_count")]
    public int FieldsCount { get; set; } = 0;

    [JsonPropertyName("has_chainer")]
    public bool HasChainer { get; set; } = false;

    [JsonPropertyName("is_struct")]
    public bool IsStruct { get; set; } = false;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_hash")]
    public ulong NameHash { get; set; } = 0;

    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; } = 0;

    [JsonPropertyName("fields")]
    public List<ClassFieldEntry>? Fields { get; set; } = [];
}

public class SDK
{
    [JsonPropertyName("classes")]
    public List<Class> Classes { get; set; } = [];

    [JsonPropertyName("enums")]
    public List<Enum> Enums { get; set; } = [];
}

public class EntityClass
{
    [JsonPropertyName("class_name")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("designer_name")]
    public string DesignerName { get; set; } = string.Empty;
}

public class EntitySystem
{
    [JsonPropertyName("entity_classes")]
    public List<EntityClass> EntityClasses { get; set; } = [];
}