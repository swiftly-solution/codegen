using System.Text;
using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace SwiftlyS2.Codegen.CS2.Generators;

/// <summary>
/// Generator for protobufs
/// </summary>
public class Protobufs : BaseGenerator
{
    private readonly string _protobufsPath;
    private readonly List<string> _allEnumNames = new();
    private readonly HashSet<string> _explicitProtoFiles = new();
    private FileDescriptorSet? _fileSet;

    /// <summary>
    /// Types to skip during generation. Add any message names here that you don't want generated.
    /// This is useful for excluding Google protobuf descriptor types and other unwanted messages.
    /// </summary>
    private static readonly HashSet<string> SkipTypes = new()
    {
        "FileDescriptorSet",
        "FileDescriptorProto",
        "DescriptorProto",
        "FieldDescriptorProto",
        "OneofDescriptorProto",
        "EnumDescriptorProto",
        "EnumValueDescriptorProto",
        "ServiceDescriptorProto",
        "MethodDescriptorProto",
        "FileOptions",
        "MessageOptions",
        "FieldOptions",
        "OneofOptions",
        "EnumOptions",
        "EnumValueOptions",
        "ServiceOptions",
        "MethodOptions",
        "UninterpretedOption",
        "SourceCodeInfo",
        "GeneratedCodeInfo",
        "Duration",
        "Timestamp",
        "Any",
        "Empty",
        "Struct",
        "Value",
        "ListValue",
        "NullValue",
        "DoubleValue",
        "FloatValue",
        "Int64Value",
        "UInt64Value",
        "Int32Value",
        "UInt32Value",
        "BoolValue",
        "StringValue",
        "BytesValue",
    };

    private static readonly Dictionary<string, (string Prefix, string MessagePrefix)> NetMessageEnums = new()
    {
        { "EBaseUserMessages", ("UM_", "CUserMessage") },
        { "ETEProtobufIds", ("TE_", "CMsgTE") },
        { "ECsgoGameEvents", ("GE_", "CMsgTE") },
        { "ECstrike15UserMessages", ("CS_UM_", "CCSUsrMsg_") },
        { "EBaseGameEvents", ("GE_", "CMsg") },
        { "CLC_Messages", ("clc_", "CCLCMsg_") },
        { "SVC_Messages", ("svc_", "CSVCMsg_") },
        { "NET_Messages", ("net_", "CNETMsg_") }
    };

    private static readonly Dictionary<string, (string AccessorType, string CsType)> BaseTypes = new()
    {
        { "bool", ("Bool", "bool") },
        { "int32", ("Int32", "int") },
        { "sint32", ("Int32", "int") },
        { "fixed32", ("UInt32", "uint") },
        { "int64", ("Int64", "long") },
        { "fixed64", ("UInt64", "ulong") },
        { "sint64", ("Int64", "long") },
        { "uint32", ("UInt32", "uint") },
        { "uint64", ("UInt64", "ulong") },
        { "float", ("Float", "float") },
        { "double", ("Double", "double") },
        { "string", ("String", "string") },
        { "bytes", ("Bytes", "byte[]") }
    };

    private static readonly Dictionary<string, string> ManagedNestedTypes = new()
    {
        { "CMsgVector", "Vector" },
        { "CMsgQAngle", "QAngle" },
        { "CMsgVector2D", "Vector2D" },
        { "CMsgRGBA", "Color" }
    };

    /// <summary>
    /// Initializes a new instance of the Protobufs generator
    /// </summary>
    /// <param name="protobufsPath">Path to the protobufs folder</param>
    public Protobufs(string? protobufsPath = null)
    {
        _protobufsPath = protobufsPath ?? Path.Combine(Entrypoint.ProjectRootPath, "protobufs", "cs2");
    }

    /// <inheritdoc />
    public override string Name => "Protobufs";

    /// <inheritdoc />
    public override string OutputPath => Path.Combine(Entrypoint.ProjectRootPath, "output", "src", "SwiftlyS2.Generated", "Protobufs");

    /// <inheritdoc />
    public override string? DataPath
    {
        get => _protobufsPath;
        protected set { }
    }

    /// <inheritdoc />
    public async override Task<GeneratorResult> GenerateFilesAsync()
    {
        try
        {
            Progress.Report("Starting protobuf generation...");

            if (!Directory.Exists(_protobufsPath))
            {
                return new GeneratorResult
                {
                    Success = false,
                    ErrorMessage = $"Protobufs path not found: {_protobufsPath}"
                };
            }

            Progress.Report("Preparing output directories...");

            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }

            var outInterfaces = Path.Combine(OutputPath, "Interfaces");
            var outClasses = Path.Combine(OutputPath, "Classes");
            var outEnums = Path.Combine(OutputPath, "Enums");

            Directory.CreateDirectory(outInterfaces);
            Directory.CreateDirectory(outClasses);
            Directory.CreateDirectory(outEnums);

            Progress.Report("Parsing proto files...");
            await ParseProtoFilesAsync();

            Progress.Report("Processing enums...");
            ProcessEnums(outEnums);

            Progress.Report("Processing messages...");
            ProcessMessages(outInterfaces, outClasses);

            Progress.Report("Protobuf generation completed successfully");

            return new GeneratorResult { Success = true };
        }
        catch (Exception ex)
        {
            return new GeneratorResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }

    private async Task ParseProtoFilesAsync()
    {
        var protoFiles = Directory.GetFiles(_protobufsPath, "*.proto");

        _fileSet = new FileDescriptorSet
        {
        };
        _fileSet.AddImportPath(_protobufsPath);

        foreach (var protoFile in protoFiles)
        {
            var fileName = Path.GetFileName(protoFile);
            Progress.Report($"Loading proto file: {fileName}");
            _fileSet.Add(fileName, true);
            _explicitProtoFiles.Add(fileName);
        }

        Progress.Report("Processing proto schema...");
        _fileSet.Process();
        await Task.CompletedTask;
    }

    private void ProcessEnums(string outEnums)
    {
        if (_fileSet == null) return;

        foreach (var file in _fileSet.Files)
        {
            if (!_explicitProtoFiles.Contains(file.Name))
                continue;

            ProcessEnumsRecursive(file.EnumTypes, outEnums, "");
            foreach (var message in file.MessageTypes)
            {
                ProcessMessageEnums(message, outEnums, message.Name + "_");
            }
        }
    }

    private void ProcessEnumsRecursive(IEnumerable<EnumDescriptorProto> enums, string outEnums, string prefix)
    {
        foreach (var enumProto in enums)
        {
            var enumName = prefix + enumProto.Name;

            if (SkipTypes.Contains(enumName))
            {
                continue;
            }

            _allEnumNames.Add(enumName);
            WriteEnum(enumProto, outEnums, prefix);
        }
    }

    private void ProcessMessageEnums(DescriptorProto message, string outEnums, string prefix)
    {
        ProcessEnumsRecursive(message.EnumTypes, outEnums, prefix);
        foreach (var nestedMessage in message.NestedTypes)
        {
            var nestedName = prefix + nestedMessage.Name;

            if (SkipTypes.Contains(nestedName))
            {
                continue;
            }

            ProcessMessageEnums(nestedMessage, outEnums, prefix + nestedMessage.Name + "_");
        }
    }

    private void ProcessMessages(string outInterfaces, string outClasses)
    {
        if (_fileSet == null) return;

        foreach (var file in _fileSet.Files)
        {
            if (!_explicitProtoFiles.Contains(file.Name))
                continue;

            var messages = file.MessageTypes.ToList();
            var enums = file.EnumTypes.ToList();
            var handledMessages = new HashSet<DescriptorProto>();

            foreach (var enumProto in enums)
            {
                if (!NetMessageEnums.TryGetValue(enumProto.Name, out var enumInfo))
                    continue;

                var (enumPrefix, messagePrefix) = enumInfo;
                var handledEnumFields = new HashSet<string>();

                foreach (var enumField in enumProto.Values)
                {
                    var name = enumField.Name;
                    if (name.StartsWith(enumPrefix))
                    {
                        name = name.Substring(enumPrefix.Length);
                    }

                    if (enumProto.Name is "ETEProtobufIds" or "ECsgoGameEvents")
                    {
                        if (name.EndsWith("Id"))
                        {
                            name = name.Substring(0, name.Length - 2);
                        }
                    }

                    foreach (var message in messages)
                    {
                        if (message.Name.Contains(messagePrefix) && message.Name.Contains(name))
                        {
                            Progress.Report($"Generating net message: {message.Name} = {enumField.Number}");
                            handledEnumFields.Add(enumField.Name);
                            handledMessages.Add(message);
                            WriteNetMessage(message, outInterfaces, outClasses, enumField.Number);
                            break;
                        }
                    }
                }

                foreach (var enumField in enumProto.Values)
                {
                    if (!handledEnumFields.Contains(enumField.Name))
                    {
                        Progress.Report($"WARNING: MISSING {enumProto.Name}.{enumField.Name}");
                    }
                }
            }

            foreach (var message in messages)
            {
                if (!handledMessages.Contains(message))
                {
                    Progress.Report($"Generating message: {message.Name}");
                    WriteNetMessage(message, outInterfaces, outClasses, -1);
                }
            }
        }
    }

    private void WriteEnum(EnumDescriptorProto enumProto, string outEnums, string prefix)
    {
        var enumName = prefix + enumProto.Name;
        var writer = new CodeWriter();

        writer.AddLine("namespace SwiftlyS2.Shared.ProtobufDefinitions;");
        writer.AddLine();
        writer.AddBlock($"public enum {enumName}", () =>
        {
            foreach (var field in enumProto.Values)
            {
                writer.AddLine($"{field.Name} = {field.Number},");
            }
        });

        File.WriteAllText(Path.Combine(outEnums, $"{enumName}.cs"), writer.ToString());
    }

    private void WriteNetMessage(DescriptorProto message, string outInterfaces, string outClasses, int messageId, string prefix = "")
    {
        var className = prefix + message.Name;
        // Skip types listed in SkipTypes
        if (SkipTypes.Contains(className)) return;

        var fields = new List<string>();
        var interfaceFields = new List<string>();

        foreach (var field in message.Fields)
        {
            var (implField, interfaceField) = GetFieldTemplate(field);
            fields.Add(implField);
            interfaceFields.Add(interfaceField);
        }

        // Process nested messages
        foreach (var nestedMessage in message.NestedTypes)
        {
            WriteNetMessage(nestedMessage, outInterfaces, outClasses, -1, prefix + message.Name + "_");
        }

        var classNameImpl = prefix + message.Name + "Impl";
        var interfaceName = prefix + message.Name;

        // Write implementation class
        var implWriter = new CodeWriter();
        WriteImplClass(implWriter, classNameImpl, interfaceName, fields, messageId);
        File.WriteAllText(Path.Combine(outClasses, $"{classNameImpl}.cs"), implWriter.ToString());

        // Write interface
        var interfaceWriter = new CodeWriter();
        WriteInterface(interfaceWriter, interfaceName, interfaceFields, messageId);
        File.WriteAllText(Path.Combine(outInterfaces, $"{interfaceName}.cs"), interfaceWriter.ToString());
    }

    private void WriteImplClass(CodeWriter writer, string className, string interfaceName, List<string> fields, int messageId)
    {
        writer.AddLine("using SwiftlyS2.Core.Natives;");
        writer.AddLine("using SwiftlyS2.Core.NetMessages;");
        writer.AddLine("using SwiftlyS2.Shared.Natives;");
        writer.AddLine("using SwiftlyS2.Shared.NetMessages;");
        writer.AddLine("using SwiftlyS2.Shared.ProtobufDefinitions;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.ProtobufDefinitions;");
        writer.AddLine();

        var baseClass = messageId != -1 ? "NetMessage" : "TypedProtobuf";
        writer.AddBlock($"internal class {className} : {baseClass}<{interfaceName}>, {interfaceName}", () =>
        {
            writer.AddBlock($"public {className}(nint handle, bool isManuallyAllocated) : base(handle{(messageId != -1 ? ", isManuallyAllocated" : "")})", () => { });
            writer.AddLine();

            foreach (var field in fields)
            {
                writer.AddLines(field.Split('\n'));
            }
        });
    }

    private void WriteInterface(CodeWriter writer, string interfaceName, List<string> fields, int messageId)
    {
        writer.AddLine("using SwiftlyS2.Core.ProtobufDefinitions;");
        writer.AddLine("using SwiftlyS2.Shared.Natives;");
        writer.AddLine("using SwiftlyS2.Shared.NetMessages;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Shared.ProtobufDefinitions;");
        writer.AddLine();

        if (messageId != -1)
        {
            writer.AddBlock($"public interface {interfaceName} : ITypedProtobuf<{interfaceName}>, INetMessage<{interfaceName}>, IDisposable", () =>
            {
                writer.AddLine($"static int INetMessage<{interfaceName}>.MessageId => {messageId};");
                writer.AddLine();
                writer.AddLine($"static string INetMessage<{interfaceName}>.MessageName => \"{interfaceName}\";");
                writer.AddLine();
                writer.AddLine($"static {interfaceName} ITypedProtobuf<{interfaceName}>.Wrap(nint handle, bool isManuallyAllocated) => new {interfaceName}Impl(handle, isManuallyAllocated);");
                writer.AddLine();

                foreach (var field in fields)
                {
                    writer.AddLines(field.Split('\n'));
                }
            });
        }
        else
        {
            writer.AddBlock($"public interface {interfaceName} : ITypedProtobuf<{interfaceName}>", () =>
            {
                writer.AddLine($"static {interfaceName} ITypedProtobuf<{interfaceName}>.Wrap(nint handle, bool isManuallyAllocated) => new {interfaceName}Impl(handle, isManuallyAllocated);");
                writer.AddLine();

                foreach (var field in fields)
                {
                    writer.AddLines(field.Split('\n'));
                }
            });
        }
    }

    private (string implField, string interfaceField) GetFieldTemplate(FieldDescriptorProto field)
    {
        var fieldType = field.TypeName?.TrimStart('.') ?? "";
        var isRepeated = field.label == FieldDescriptorProto.Label.LabelRepeated;

        fieldType = fieldType.Replace(".", "_");

        var csName = ToCamelCase(field.Name);
        var fieldName = field.Name;

        // Check if it's an enum
        if (_allEnumNames.Contains(fieldType))
        {
            var content = $"public {fieldType} {csName}\n{{ get => ({fieldType})Accessor.GetInt32(\"{fieldName}\"); set => Accessor.SetInt32(\"{fieldName}\", (int)value); }}";
            var interfaceContent = $"public {fieldType} {csName} {{ get; set; }}";
            return (content, interfaceContent);
        }

        // Get the proto type name
        var protoType = GetProtoTypeName(field);

        // Check if it's a base type
        if (BaseTypes.TryGetValue(protoType, out var baseTypeInfo))
        {
            var (accessorType, csType) = baseTypeInfo;

            if (isRepeated)
            {
                var csTypeImpl = $"ProtobufRepeatedFieldValueType<{csType}>";
                var csTypeInterface = $"IProtobufRepeatedFieldValueType<{csType}>";
                var content = $"public {csTypeInterface} {csName}\n{{ get => new {csTypeImpl}(Accessor, \"{fieldName}\"); }}";
                var interfaceContent = $"public {csTypeInterface} {csName} {{ get; }}";
                return (content, interfaceContent);
            }

            var fieldContent = $"public {csType} {csName}\n{{ get => Accessor.Get{accessorType}(\"{fieldName}\"); set => Accessor.Set{accessorType}(\"{fieldName}\", value); }}";
            var fieldInterfaceContent = $"public {csType} {csName} {{ get; set; }}";
            return (fieldContent, fieldInterfaceContent);
        }

        // Check if it's a managed nested type
        if (ManagedNestedTypes.TryGetValue(fieldType, out var managedType))
        {
            if (isRepeated)
            {
                var csTypeImpl = $"ProtobufRepeatedFieldValueType<{managedType}>";
                var csTypeInterface = $"IProtobufRepeatedFieldValueType<{managedType}>";
                var content = $"public {csTypeInterface} {csName}\n{{ get => new {csTypeImpl}(Accessor, \"{fieldName}\"); }}";
                var interfaceContent = $"public {csTypeInterface} {csName} {{ get; }}";
                return (content, interfaceContent);
            }

            var fieldContent = $"public {managedType} {csName}\n{{ get => Accessor.Get{managedType}(\"{fieldName}\"); set => Accessor.Set{managedType}(\"{fieldName}\", value); }}";
            var fieldInterfaceContent = $"public {managedType} {csName} {{ get; set; }}";
            return (fieldContent, fieldInterfaceContent);
        }

        // It's a nested message
        if (isRepeated)
        {
            var csTypeImpl = $"ProtobufRepeatedFieldSubMessageType<{fieldType}>";
            var csTypeInterface = $"IProtobufRepeatedFieldSubMessageType<{fieldType}>";
            var content = $"public {csTypeInterface} {csName}\n{{ get => new {csTypeImpl}(Accessor, \"{fieldName}\"); }}";
            var interfaceContent = $"public {csTypeInterface} {csName} {{ get; }}";
            return (content, interfaceContent);
        }

        var nestedContent = $"public {fieldType} {csName}\n{{ get => new {fieldType}Impl(NativeNetMessages.GetNestedMessage(Address, \"{fieldName}\"), false); }}";
        var nestedInterfaceContent = $"public {fieldType} {csName} {{ get; }}";
        return (nestedContent, nestedInterfaceContent);
    }

    private static string GetProtoTypeName(FieldDescriptorProto field)
    {
        return field.type switch
        {
            FieldDescriptorProto.Type.TypeBool => "bool",
            FieldDescriptorProto.Type.TypeInt32 => "int32",
            FieldDescriptorProto.Type.TypeSint32 => "sint32",
            FieldDescriptorProto.Type.TypeFixed32 => "fixed32",
            FieldDescriptorProto.Type.TypeInt64 => "int64",
            FieldDescriptorProto.Type.TypeFixed64 => "fixed64",
            FieldDescriptorProto.Type.TypeSint64 => "sint64",
            FieldDescriptorProto.Type.TypeUint32 => "uint32",
            FieldDescriptorProto.Type.TypeUint64 => "uint64",
            FieldDescriptorProto.Type.TypeFloat => "float",
            FieldDescriptorProto.Type.TypeDouble => "double",
            FieldDescriptorProto.Type.TypeString => "string",
            FieldDescriptorProto.Type.TypeBytes => "bytes",
            _ => ""
        };
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var parts = name.Split('_');
        var result = new StringBuilder();

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;

            result.Append(char.ToUpper(part[0]));
            if (part.Length > 1)
            {
                result.Append(part.Substring(1));
            }
        }

        return result.ToString();
    }
}
