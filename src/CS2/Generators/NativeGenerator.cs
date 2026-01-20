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

    /// <inheritdoc />
    public override string Name => "Natives";

    /// <inheritdoc />
    public override string OutputPath => Entrypoint.GenerateOutputPath("src/SwiftlyS2.Generated/Natives");

    /// <inheritdoc />
    public override async Task<GeneratorResult> GenerateFilesAsync()
    {
        try
        {
            var nativesDir = Path.Combine(Entrypoint.ProjectRootPath, "data", "natives");
            if (!Directory.Exists(nativesDir))
            {
                return new GeneratorResult
                {
                    Success = false,
                    ErrorMessage = $"Natives directory not found: {nativesDir}"
                };
            }

            var outputDir = OutputPath;
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
            Directory.CreateDirectory(outputDir);

            var nativeFiles = Directory.GetFiles(nativesDir, "*.native", SearchOption.AllDirectories);

            foreach (var nativeFile in nativeFiles)
            {
                await ParseNativeFileAsync(nativeFile, outputDir);
            }

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
        var poolDeclared = false;

        // string params
        foreach (var paramName in stringParams)
        {
            if (!poolDeclared)
            {
                writer.AddLine("var pool = ArrayPool<byte>.Shared;");
                poolDeclared = true;
            }
            writer.AddLine($"var {paramName}Length = Encoding.UTF8.GetByteCount({paramName});");
            writer.AddLine($"var {paramName}Buffer = pool.Rent({paramName}Length + 1);");
            writer.AddLine($"Encoding.UTF8.GetBytes({paramName}, {paramName}Buffer);");
            writer.AddLine($"{paramName}Buffer[{paramName}Length] = 0;");
        }

        // bytes params
        foreach (var paramName in bytesParams)
        {
            writer.AddLine($"var {paramName}Length = {paramName}.Length;");
        }

        // fixed blocks
        var fixedBlocks = new List<string>();
        foreach (var paramName in stringParams)
        {
            fixedBlocks.Add($"fixed (byte* {paramName}BufferPtr = {paramName}Buffer)");
        }
        foreach (var paramName in bytesParams)
        {
            fixedBlocks.Add($"fixed (byte* {paramName}BufferPtr = {paramName})");
        }

        if (fixedBlocks.Count > 0)
        {
            WriteWithFixedBlocks(writer, fixedBlocks, 0, () =>
            {
                WriteNativeCall(writer, returnType, functionName, nativeParams, stringParams, bytesParams, ref poolDeclared);
            });
        }
        else
        {
            WriteNativeCall(writer, returnType, functionName, nativeParams, stringParams, bytesParams, ref poolDeclared);
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
        List<(string type, string name)> nativeParams, List<string> stringParams, List<string> bytesParams, ref bool poolDeclared)
    {
        if (IsBufferReturn(returnType))
        {
            // First call to get buffer size
            var firstCallArgs = new List<string> { "null" };
            firstCallArgs.AddRange(BuildCallArgs(nativeParams));
            writer.AddLine($"var ret = _{functionName}({string.Join(", ", firstCallArgs)});");

            if (!poolDeclared)
            {
                writer.AddLine("var pool = ArrayPool<byte>.Shared;");
                poolDeclared = true;
            }
            writer.AddLine("var retBuffer = pool.Rent(ret + 1);");

            writer.AddBlock("fixed (byte* retBufferPtr = retBuffer)", () =>
            {
                var secondCallArgs = new List<string> { "retBufferPtr" };
                secondCallArgs.AddRange(BuildCallArgs(nativeParams));
                writer.AddLine($"ret = _{functionName}({string.Join(", ", secondCallArgs)});");

                if (returnType == "string")
                {
                    writer.AddLine("var retString = Encoding.UTF8.GetString(retBufferPtr, ret);");
                    writer.AddLine("pool.Return(retBuffer);");
                    foreach (var param in stringParams)
                    {
                        writer.AddLine($"pool.Return({param}Buffer);");
                    }
                    writer.AddLine("return retString;");
                }
                else // bytes
                {
                    writer.AddLine("var retBytes = new byte[ret];");
                    writer.AddLine("for (int i = 0; i < ret; i++) retBytes[i] = retBufferPtr[i];");
                    writer.AddLine("pool.Return(retBuffer);");
                    foreach (var param in stringParams)
                    {
                        writer.AddLine($"pool.Return({param}Buffer);");
                    }
                    writer.AddLine("return retBytes;");
                }
            });
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

            foreach (var param in stringParams)
            {
                writer.AddLine($"pool.Return({param}Buffer);");
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
                args.Add($"{name}BufferPtr");
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
