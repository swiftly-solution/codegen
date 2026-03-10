namespace SwiftlyS2.Codegen.CS2.Generators;

/// <summary>
/// Generator for natives
/// </summary>
public class Natives : BaseGenerator
{
    private static readonly Dictionary<string, string> ParamTypeMap = new()
    {
        { "int16", "short" },
        { "uint16", "ushort" },
        { "int32", "int" },
        { "float", "float" },
        { "double", "double" },
        { "bool", "bool" },
        { "byte", "byte" },
        { "int64", "long" },
        { "uint32", "uint" },
        { "uint64", "ulong" },
        { "ptr", "nint" },
        { "string", "string" },
        { "void", "void" },
        { "vector2", "Vector2D" },
        { "vector", "Vector" },
        { "vector4", "Vector4D" },
        { "qangle", "QAngle" },
        { "color", "Color" },
        { "bytes", "byte[]" },
        { "cutlstringtoken", "CUtlStringToken" }
    };

    private static readonly Dictionary<string, string> DelegateParamTypeMap;
    private static readonly Dictionary<string, string> DelegateReturnTypeMap;
    private static readonly Dictionary<string, string> ReturnTypeMap;

    static Natives()
    {
        DelegateParamTypeMap = new Dictionary<string, string>(ParamTypeMap)
        {
            ["string"] = "byte*",
            ["bytes"] = "byte*",
            ["bool"] = "byte"
        };

        DelegateReturnTypeMap = new Dictionary<string, string>(ParamTypeMap)
        {
            ["string"] = "int",
            ["bytes"] = "int",
            ["bool"] = "byte"
        };

        ReturnTypeMap = new Dictionary<string, string>(ParamTypeMap)
        {
            ["string"] = "string",
            ["bytes"] = "byte[]"
        };
    }

    /// <summary>
    /// Initializes a new instance of the Natives generator
    /// </summary>
    /// <param name="dataPath">Optional custom path to the natives folder</param>
    public Natives(string? dataPath = null)
    {
        DataPath = dataPath;
    }

    /// <inheritdoc />
    public override string Name => "Natives";

    /// <inheritdoc />
    public override string OutputPath => Entrypoint.GenerateOutputPath("src/SwiftlyS2.Generated/Natives");

    /// <inheritdoc />
    public override async Task<GeneratorResult> GenerateFilesAsync()
    {
        try
        {
            Progress.Report("Locating natives directory...");
            var nativesDir = DataPath ?? Path.Combine(Entrypoint.ProjectRootPath, "data", "natives");
            if (!Directory.Exists(nativesDir))
            {
                Progress.Report($"Directory not found: {nativesDir}");
                return new GeneratorResult
                {
                    Success = false,
                    ErrorMessage = $"Natives directory not found: {nativesDir}"
                };
            }

            Progress.Report($"Found natives directory: {Path.GetFileName(nativesDir)}");
            var outputDir = OutputPath;
            if (Directory.Exists(outputDir))
            {
                Progress.Report("Cleaning output directory...");
                Directory.Delete(outputDir, true);
            }
            Directory.CreateDirectory(outputDir);

            var nativeFiles = Directory.GetFiles(nativesDir, "*.native", SearchOption.AllDirectories);
            Progress.Report($"Found {nativeFiles.Length} native file(s)");

            for (int i = 0; i < nativeFiles.Length; i++)
            {
                var fileName = Path.GetFileName(nativeFiles[i]);
                Progress.Report($"Processing {fileName} ({i + 1}/{nativeFiles.Length})...");
                await ParseNativeFileAsync(nativeFiles[i], outputDir);
            }

            Progress.Report($"Successfully generated {nativeFiles.Length} native file(s)");
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

    private async Task ParseNativeFileAsync(string filePath, string outputDir)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length == 0) return;

        var namespaceLine = lines[0];
        var namespaceContent = namespaceLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1].Trim();
        var (namespacePrefix, className) = SplitByLastDot(namespaceContent);

        var writer = new CodeWriter();
        writer.AddLine("#pragma warning disable CS0649");
        writer.AddLine("#pragma warning disable CS0169");
        writer.AddLine();
        writer.AddLine("using System.Buffers;");
        writer.AddLine("using System.Text;");
        writer.AddLine("using System.Threading;");
        writer.AddLine("using SwiftlyS2.Shared.Natives;");
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.Natives;");
        writer.AddLine();

        writer.AddBlock($"internal static class Native{className}", () =>
        {
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                ParseNativeFunction(writer, line);
            }
        });

        var outputPath = Path.Combine(outputDir, $"{className}.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, writer.GetCode());
    }

    private void ParseNativeFunction(CodeWriter writer, string line)
    {
        var parts = line.Split('=', 2);
        if (parts.Length != 2) return;

        var left = parts[0].Trim();
        var right = parts[1].Trim();

        var isMarkedSync = false;
        if (left.StartsWith("sync "))
        {
            isMarkedSync = true;
            left = left.Substring(5).Trim();
        }

        var leftParts = left.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (leftParts.Length != 2) return;

        var returnType = leftParts[0];
        var functionName = leftParts[1];

        var commentIndex = right.IndexOf("//");
        var paramsRaw = commentIndex >= 0 ? right.Substring(0, commentIndex).Trim() : right.Trim();
        var comment = commentIndex >= 0 ? right.Substring(commentIndex + 2).Trim() : null;

        var nativeParams = ParseParameters(paramsRaw);

        var nativeParamTypes = new List<string>();
        foreach (var (type, name) in nativeParams)
        {
            if (type == "bytes")
            {
                nativeParamTypes.Add(DelegateParamTypeMap[type]);
                nativeParamTypes.Add("int");
            }
            else
            {
                nativeParamTypes.Add(DelegateParamTypeMap[type]);
            }
        }

        var delegateParamTypes = IsBufferReturn(returnType)
            ? new[] { "byte*" }.Concat(nativeParamTypes).ToList()
            : nativeParamTypes;

        var delegateGeneric = string.Join(", ", delegateParamTypes.Append(DelegateReturnTypeMap[returnType]));

        writer.AddLine();
        writer.AddLine($"private unsafe static delegate* unmanaged<{delegateGeneric}> _{functionName};");
        writer.AddLine();

        if (!string.IsNullOrWhiteSpace(comment))
        {
            writer.AddLine("/// <summary>");
            writer.AddLine($"/// {comment}");
            writer.AddLine("/// </summary>");
        }

        var methodSignature = string.Join(", ", nativeParams.Select(p => $"{ParamTypeMap[p.type]} {p.name}"));
        writer.AddBlock($"public unsafe static {ReturnTypeMap[returnType]} {functionName}({methodSignature})", () =>
        {
            WriteMethodBody(writer, returnType, functionName, nativeParams, isMarkedSync);
        });
    }

    private void WriteMethodBody(CodeWriter writer, string returnType, string functionName, List<(string type, string name)> nativeParams, bool isMarkedSync)
    {
        if (isMarkedSync)
        {
            writer.AddBlock("if (!NativeBinding.IsMainThread)", () =>
            {
                writer.AddLine("throw new InvalidOperationException(\"This method can only be called from the main thread.\");");
            });
        }

        var stringParams = nativeParams.Where(p => p.type == "string").Select(p => p.name).ToList();
        var bytesParams = nativeParams.Where(p => p.type == "bytes").Select(p => p.name).ToList();

        foreach (var paramName in bytesParams)
        {
            writer.AddLine($"var {paramName}Length = {paramName}.Length;");
        }

        if (stringParams.Count > 0)
        {
            WriteStringAllocCalls(writer, returnType, functionName, nativeParams, stringParams, bytesParams, 0);
        }
        else if (bytesParams.Count > 0)
        {
            var fixedBlocks = bytesParams.Select(p => $"fixed (byte* {p}BufferPtr = {p})").ToList();
            WriteWithFixedBlocks(writer, fixedBlocks, 0, () =>
            {
                WriteNativeCall(writer, returnType, functionName, nativeParams);
            });
        }
        else
        {
            WriteNativeCall(writer, returnType, functionName, nativeParams);
        }
    }

    private void WriteStringAllocCalls(CodeWriter writer, string returnType, string functionName,
        List<(string type, string name)> nativeParams, List<string> stringParams, List<string> bytesParams, int index)
    {
        if (index < stringParams.Count)
        {
            var paramName = stringParams[index];
            var returnPrefix = returnType != "void" ? "return " : "";

            writer.AddLine($"{returnPrefix}StringAlloc.CreateCString({paramName}, {paramName}BufferPtr =>");
            writer.AddLine("{");
            writer.Indent();
            WriteStringAllocCalls(writer, returnType, functionName, nativeParams, stringParams, bytesParams, index + 1);
            writer.Dedent();
            writer.AddLine("});");
        }
        else
        {
            if (bytesParams.Count > 0)
            {
                var fixedBlocks = bytesParams.Select(p => $"fixed (byte* {p}BufferPtr = {p})").ToList();
                WriteWithFixedBlocks(writer, fixedBlocks, 0, () =>
                {
                    WriteNativeCall(writer, returnType, functionName, nativeParams);
                });
            }
            else
            {
                WriteNativeCall(writer, returnType, functionName, nativeParams);
            }
        }
    }

    private void WriteWithFixedBlocks(CodeWriter writer, List<string> blocks, int index, Action finalAction)
    {
        if (index < blocks.Count)
        {
            writer.AddBlock(blocks[index], () =>
            {
                WriteWithFixedBlocks(writer, blocks, index + 1, finalAction);
            });
        }
        else
        {
            finalAction();
        }
    }

    private void WriteNativeCall(CodeWriter writer, string returnType, string functionName,
        List<(string type, string name)> nativeParams)
    {
        if (IsBufferReturn(returnType))
        {
            var firstCallArgs = new List<string> { "null" };
            firstCallArgs.AddRange(BuildCallArgs(nativeParams));
            writer.AddLine($"var ret = _{functionName}({string.Join(", ", firstCallArgs)});");

            if (returnType == "string")
            {
                writer.AddLine("return StringAlloc.CreateCSharpString(ret, retBufferPtr =>");
                writer.AddLine("{");
                writer.Indent();
                var secondCallArgs = new List<string> { "(byte*)retBufferPtr" };
                secondCallArgs.AddRange(BuildCallArgs(nativeParams));
                writer.AddLine($"_ = _{functionName}({string.Join(", ", secondCallArgs)});");
                writer.Dedent();
                writer.AddLine("});");
            }
            else
            {
                writer.AddLine("var pool = ArrayPool<byte>.Shared;");
                writer.AddLine("var retBuffer = pool.Rent(ret + 1);");

                writer.AddBlock("fixed (byte* retBufferPtr = retBuffer)", () =>
                {
                    var secondCallArgs = new List<string> { "retBufferPtr" };
                    secondCallArgs.AddRange(BuildCallArgs(nativeParams));
                    writer.AddLine($"ret = _{functionName}({string.Join(", ", secondCallArgs)});");

                    writer.AddLine("var retBytes = new byte[ret];");
                    writer.AddLine("for (int i = 0; i < ret; i++) retBytes[i] = retBufferPtr[i];");
                    writer.AddLine("pool.Return(retBuffer);");
                    writer.AddLine("return retBytes;");
                });
            }
        }
        else
        {
            var callArgs = BuildCallArgs(nativeParams);

            if (returnType == "void")
            {
                writer.AddLine($"_{functionName}({string.Join(", ", callArgs)});");
            }
            else
            {
                writer.AddLine($"var ret = _{functionName}({string.Join(", ", callArgs)});");
            }

            if (returnType != "void")
            {
                if (returnType == "bool")
                {
                    writer.AddLine("return ret == 1;");
                }
                else
                {
                    writer.AddLine("return ret;");
                }
            }
        }
    }

    private List<string> BuildCallArgs(List<(string type, string name)> nativeParams)
    {
        var args = new List<string>();
        foreach (var (type, name) in nativeParams)
        {
            if (type == "string")
            {
                args.Add($"(byte*){name}BufferPtr");
            }
            else if (type == "bytes")
            {
                args.Add($"{name}BufferPtr");
                args.Add($"{name}Length");
            }
            else if (type == "bool")
            {
                args.Add($"{name} ? (byte)1 : (byte)0");
            }
            else
            {
                args.Add(name);
            }
        }
        return args;
    }

    private List<(string type, string name)> ParseParameters(string paramsRaw)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(paramsRaw) || paramsRaw == "void")
            return result;

        var paramsList = paramsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var param in paramsList)
        {
            var parts = param.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                result.Add((parts[0], parts[1]));
            }
        }
        return result;
    }

    private static bool IsBufferReturn(string returnType) => returnType == "string" || returnType == "bytes";

    private static (string prefix, string className) SplitByLastDot(string value)
    {
        var idx = value.LastIndexOf('.');
        if (idx == -1)
            return ("", value);
        return (value[..idx], value[(idx + 1)..]);
    }
}
