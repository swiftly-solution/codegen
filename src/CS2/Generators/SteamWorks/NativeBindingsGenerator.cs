using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators.SteamWorks;

internal sealed record NativeFunc(string Entry, string Name, string ReturnType, List<(string Type, string Name)> Params);

internal static class NativeBindingsGenerator
{
    private const string CallConv = "delegate* unmanaged[Cdecl]";

    private static readonly HashSet<string> Primitives =
    [
        "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong",
        "float", "double", "nint", "nuint", "IntPtr",
    ];

    private static readonly HashSet<string> BlittableCustom =
    [
        "CSteamID", "CGameID", "SteamIPAddress_t", "SteamNetworkingConfigValue_t", "servernetadr_t",
        "SteamNetworkingIdentity", "SteamNetworkingIPAddr", "SteamNetworkingErrMsg", "SteamNetworkingMessage_t",
        "SteamDatagramRelayAuthTicket", "SteamDatagramHostedAddress", "gameserveritem_t",
        "ISteamNetworkingConnectionSignaling", "ISteamNetworkingSignalingRecvContext",
    ];

    private static readonly HashSet<string> NonBlittableCustom = [];

    private static readonly HashSet<string> DelegateTypes =
    [
        "FSteamNetworkingSocketsDebugOutput", "SteamAPIWarningMessageHook_t",
    ];

    private static HashSet<string> _blittable = [.. Primitives, .. BlittableCustom];

    public static void Configure(SteamworksParser parser)
    {
        var blittable = new HashSet<string>(Primitives);
        blittable.UnionWith(BlittableCustom);

        foreach (var t in parser.Typedefs)
            blittable.Add(t.Name);

        foreach (var f in parser.Files)
            foreach (var e in f.Enums)
                if (e.Name is not null)
                    blittable.Add(e.Name);

        var structs = parser.Files.SelectMany(f => f.Structs.Concat(f.Callbacks)).ToList();
        var nonBlittable = new HashSet<string>(NonBlittableCustom);

        bool changed;
        do
        {
            changed = false;
            foreach (var s in structs)
            {
                if (nonBlittable.Contains(s.Name) || BlittableCustom.Contains(s.Name))
                    continue;

                if (s.Fields.Any(fl => (fl.ArraySize is null && (fl.Type == "string" || fl.Type.Contains("char", StringComparison.Ordinal)))
                                       || nonBlittable.Contains(fl.Type)))
                {
                    nonBlittable.Add(s.Name);
                    changed = true;
                }
            }
        } while (changed);

        foreach (var s in structs)
            if (!nonBlittable.Contains(s.Name))
                blittable.Add(s.Name);

        blittable.ExceptWith(nonBlittable);
        blittable.UnionWith(BlittableCustom);
        _blittable = blittable;
    }

    public static async Task GenerateAsync(IEnumerable<string> declarationLines, string outputPath)
    {
        var items = Parse(declarationLines);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "NativeMethods.Surface.cs"), RenderSurface(items));
        await File.WriteAllTextAsync(Path.Combine(outputPath, "NativeMethods.Bind.cs"), RenderBinder(items));
    }

    private static readonly System.Text.RegularExpressions.Regex EntryRegex =
        new(@"EntryPoint\s*=\s*""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex ExternRegex =
        new(@"^\s*public static extern (\S+) (\w+)\((.*)\);\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static List<(string? Directive, NativeFunc? Func)> Parse(IEnumerable<string> lines)
    {
        var items = new List<(string?, NativeFunc?)>();
        string? entry = null;
        bool returnsBool = false;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (line.StartsWith('#'))
            {
                if (!line.StartsWith("#region", StringComparison.Ordinal) && line != "#endregion")
                    items.Add((line, null));
            }
            else if (line.TrimStart().StartsWith("[DllImport", StringComparison.Ordinal))
            {
                entry = EntryRegex.Match(line) is { Success: true } m ? m.Groups[1].Value : null;
                returnsBool = false;
            }
            else if (line.TrimStart().StartsWith("[return:", StringComparison.Ordinal))
            {
                returnsBool = true;
            }
            else if (ExternRegex.Match(line) is { Success: true } m && entry is not null)
            {
                var ret = m.Groups[1].Value;
                var ps = SplitParams(m.Groups[3].Value).Select(ParseParam).ToList();
                items.Add((null, new NativeFunc(entry, m.Groups[2].Value, returnsBool && ret == "bool" ? "bool" : ret, ps)));
                entry = null;
            }
        }

        return items;
    }

    private static List<string> SplitParams(string text)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        if (text[start..].Trim().Length > 0)
            parts.Add(text[start..].Trim());
        return parts;
    }

    private static (string Type, string Name) ParseParam(string p)
    {
        int sp = p.LastIndexOf(' ');
        return (p[..sp].Trim(), p[(sp + 1)..].Trim());
    }

    public static string RenderSurface(IReadOnlyList<(string? Directive, NativeFunc? Func)> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine("using SwiftlyS2.Core.Natives;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftlyS2.Shared.SteamAPI;");
        sb.AppendLine();
        sb.AppendLine("internal static unsafe partial class NativeMethods");
        sb.AppendLine("{");

        foreach (var (directive, func) in items)
        {
            if (directive is not null)
            {
                if (!directive.StartsWith("#region", StringComparison.Ordinal) && directive != "#endregion")
                    sb.AppendLine(directive);
            }
            else if (func is not null)
            {
                var (dTypes, _) = Build(func);
                sb.AppendLine($"\tinternal static {CallConv}<{dTypes}> _{func.Name};");
            }
        }

        sb.AppendLine();

        foreach (var (directive, func) in items)
        {
            if (directive is not null)
            {
                sb.AppendLine(directive);
            }
            else if (func is not null)
            {
                var (_, wrapper) = Build(func);
                foreach (var line in wrapper)
                    sb.AppendLine(line);
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string RenderBinder(IReadOnlyList<(string? Directive, NativeFunc? Func)> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftlyS2.Shared.SteamAPI;");
        sb.AppendLine();
        sb.AppendLine("internal static unsafe class NativeMethodsBinder");
        sb.AppendLine("{");
        sb.AppendLine("\t// Resolves every function pointer in NativeMethods from the loaded steam_api library.");
        sb.AppendLine("\t// Functions missing from the library stay null and throw EntryPointNotFoundException when called.");
        sb.AppendLine("\tinternal static void BindAll(nint libraryHandle)");
        sb.AppendLine("\t{");

        foreach (var (directive, func) in items)
        {
            if (directive is not null)
            {
                if (!directive.StartsWith("#region", StringComparison.Ordinal) && directive != "#endregion")
                    sb.AppendLine(directive);
            }
            else if (func is not null)
            {
                var (dTypes, _) = Build(func);
                sb.AppendLine(
                    $"\t\tif (NativeLibrary.TryGetExport(libraryHandle, \"{func.Entry}\", out nint p{func.Name})) " +
                    $"NativeMethods._{func.Name} = ({CallConv}<{dTypes}>)p{func.Name};");
            }
        }

        sb.AppendLine("\t}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static (string DelegateTypes, List<string> Wrapper) Build(NativeFunc f)
    {
        var sig       = new List<string>();
        var dTypes    = new List<string>();
        var args      = new List<string>();
        var prelude   = new List<string>();
        var postlude  = new List<string>();
        var cleanup   = new List<string>();
        var fixeds    = new List<string>();

        foreach (var (rawType, name) in f.Params)
        {
            bool inOut = rawType.Contains("[In, Out]", StringComparison.Ordinal);
            string type = rawType
                .Replace("[In, Out] ", "", StringComparison.Ordinal)
                .Replace("[MarshalAs(UnmanagedType.I1)] ", "", StringComparison.Ordinal);

            string mod = "";
            if (type.StartsWith("out ", StringComparison.Ordinal)) { mod = "out "; type = type[4..]; }
            else if (type.StartsWith("ref ", StringComparison.Ordinal)) { mod = "ref "; type = type[4..]; }

            sig.Add($"{mod}{type} {name}");

            if (type == "string" && mod == "")
            {
                dTypes.Add("byte*");
                prelude.Add($"using var {name}Str = new ScopedCString({name} ?? string.Empty);");
                fixeds.Add($"fixed (byte* {name}BufferPtr = {name}Str)");
                args.Add($"{name} is null ? null : {name}BufferPtr");
            }
            else if (DelegateTypes.Contains(type) && mod == "")
            {
                dTypes.Add("nint");
                args.Add($"{name} is null ? 0 : Marshal.GetFunctionPointerForDelegate({name})");
            }
            else if (type.EndsWith("[]", StringComparison.Ordinal))
            {
                string elem = type[..^2];
                if (IsBlittable(elem))
                {
                    string p = Pointee(elem);
                    dTypes.Add($"{p}*");
                    fixeds.Add($"fixed ({p}* {name}Ptr = {name})");
                    args.Add($"{name}Ptr");
                }
                else
                {
                    dTypes.Add("void*");
                    prelude.Add($"var {name}Stride = Marshal.SizeOf<{elem}>();");
                    prelude.Add($"var {name}Buf = {name} is null ? 0 : Marshal.AllocHGlobal({name}Stride * {name}.Length);");
                    prelude.Add($"if ({name} is not null) for (var i = 0; i < {name}.Length; i++) Marshal.StructureToPtr({name}[i], {name}Buf + i * {name}Stride, false);");
                    if (inOut)
                        postlude.Add($"if ({name} is not null) for (var i = 0; i < {name}.Length; i++) {name}[i] = Marshal.PtrToStructure<{elem}>({name}Buf + i * {name}Stride);");
                    cleanup.Add($"if ({name}Buf != 0) Marshal.FreeHGlobal({name}Buf);");
                    args.Add($"(void*){name}Buf");
                }
            }
            else if (mod != "")
            {
                if (type == "bool")
                {
                    dTypes.Add("byte*");
                    prelude.Add(mod == "out " ? $"byte {name}Val = 0;" : $"byte {name}Val = (byte)({name} ? 1 : 0);");
                    postlude.Add($"{name} = {name}Val != 0;");
                    args.Add($"&{name}Val");
                }
                else if (IsBlittable(type))
                {
                    string p = Pointee(type);
                    dTypes.Add($"{p}*");
                    if (mod == "out ")
                        prelude.Add($"{name} = default;");
                    fixeds.Add($"fixed ({p}* {name}Ptr = &{name})");
                    args.Add($"{name}Ptr");
                }
                else
                {
                    dTypes.Add("void*");
                    prelude.Add($"var {name}Size = Marshal.SizeOf<{type}>();");
                    prelude.Add($"var {name}Buf = Marshal.AllocHGlobal({name}Size);");
                    prelude.Add(mod == "ref "
                        ? $"Marshal.StructureToPtr({name}, {name}Buf, false);"
                        : $"NativeMemory.Clear((void*){name}Buf, (nuint){name}Size);");
                    postlude.Add($"{name} = Marshal.PtrToStructure<{type}>({name}Buf);");
                    cleanup.Add($"Marshal.FreeHGlobal({name}Buf);");
                    args.Add($"(void*){name}Buf");
                }
            }
            else if (type == "bool")
            {
                dTypes.Add("byte");
                args.Add($"(byte)({name} ? 1 : 0)");
            }
            else if (IsBlittable(type))
            {
                dTypes.Add(Pointee(type));
                args.Add(name);
            }
            else
            {
                throw new NotSupportedException($"{f.Name}: cannot pass '{rawType} {name}' to a function pointer.");
            }
        }

        string dRet;
        string wrapperRet = f.ReturnType;
        if (f.ReturnType == "void") dRet = "void";
        else if (f.ReturnType == "bool") dRet = "byte";
        else if (IsBlittable(f.ReturnType)) dRet = Pointee(f.ReturnType);
        else throw new NotSupportedException($"{f.Name}: cannot return '{f.ReturnType}' from a function pointer.");

        string delegateTypes = string.Join(", ", dTypes.Append(dRet));

        var lines = new List<string>
        {
            $"\tinternal static {wrapperRet} {f.Name}({string.Join(", ", sig)})",
            "\t{",
            $"\t\tif ((nint)_{f.Name} == 0) throw new EntryPointNotFoundException(\"'{f.Entry}' is not available.\");",
        };

        foreach (var l in prelude)
            lines.Add("\t\t" + l);

        bool hasReturn = f.ReturnType != "void";
        if (hasReturn && (cleanup.Count > 0 || fixeds.Count > 0 || postlude.Count > 0))
            lines.Add($"\t\t{dRet} ret;");

        int depth = 2;
        bool useTry = cleanup.Count > 0;
        if (useTry)
        {
            lines.Add("\t\ttry");
            lines.Add("\t\t{");
            depth++;
        }

        foreach (var fx in fixeds)
        {
            lines.Add(new string('\t', depth) + fx);
            lines.Add(new string('\t', depth) + "{");
            depth++;
        }

        string call = $"_{f.Name}({string.Join(", ", args)})";
        bool inline = !useTry && fixeds.Count == 0 && postlude.Count == 0;

        if (inline)
        {
            if (!hasReturn)
                lines.Add($"\t\t{call};");
            else if (f.ReturnType == "bool")
                lines.Add($"\t\treturn {call} != 0;");
            else
                lines.Add($"\t\treturn {call};");
        }
        else
        {
            string pad = new('\t', depth);
            lines.Add(hasReturn ? $"{pad}ret = {call};" : $"{pad}{call};");
            foreach (var l in postlude)
                lines.Add(pad + l);

            while (depth > (useTry ? 3 : 2))
            {
                depth--;
                lines.Add(new string('\t', depth) + "}");
            }

            if (useTry)
            {
                lines.Add("\t\t}");
                lines.Add("\t\tfinally");
                lines.Add("\t\t{");
                foreach (var l in cleanup)
                    lines.Add("\t\t\t" + l);
                lines.Add("\t\t}");
            }

            if (hasReturn)
                lines.Add(f.ReturnType == "bool" ? "\t\treturn ret != 0;" : "\t\treturn ret;");
        }

        lines.Add("\t}");
        return (delegateTypes, lines);
    }

    private static bool IsBlittable(string type) => _blittable.Contains(type);

    private static string Pointee(string type) => type == "IntPtr" ? "nint" : type;
}
