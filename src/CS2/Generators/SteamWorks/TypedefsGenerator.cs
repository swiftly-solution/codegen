using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators.SteamWorks;

internal static class TypedefsGenerator
{
    // ─── Static Data ─────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> PrettyFilenames = new()
    {
        ["SteamClientpublic"]  = "SteamClientPublic",
        ["SteamHtmlsurface"]   = "SteamHTMLSurface",
        ["SteamHttp"]          = "SteamHTTP",
        ["SteamRemotestorage"] = "SteamRemoteStorage",
        ["SteamUgc"]           = "SteamUGC",
        ["SteamUserstats"]     = "SteamUserStats",
        ["SteamRemoteplay"]    = "SteamRemotePlay",
    };

    private static readonly Dictionary<string, string> TypeDict = new()
    {
        ["int16"]  = "short",
        ["int32"]  = "int",
        ["int64"]  = "long",
        ["uint32"] = "uint",
        ["uint64"] = "ulong",
        ["void*"]  = "System.IntPtr",
    };

    private static readonly HashSet<string> UnusedTypedefs =
    [
        "int8", "int16", "int32", "int64", "intp",
        "lint64", "uint8", "uint16", "uint32", "uint64",
        "uintp", "ulint64",
    ];

    // typedef name → field name → value
    private static readonly Dictionary<string, Dictionary<string, string>> ReadOnlyValues = new()
    {
        ["HAuthTicket"]                  = new() { ["Invalid"] = "0" },
        ["FriendsGroupID_t"]             = new() { ["Invalid"] = "-1" },
        ["HHTMLBrowser"]                 = new() { ["Invalid"] = "0" },
        ["HTTPCookieContainerHandle"]    = new() { ["Invalid"] = "0" },
        ["HTTPRequestHandle"]            = new() { ["Invalid"] = "0" },
        ["SteamInventoryResult_t"]       = new() { ["Invalid"] = "-1" },
        ["SteamItemInstanceID_t"]        = new() { ["Invalid"] = "0xFFFFFFFFFFFFFFFF" },
        ["HServerListRequest"]           = new() { ["Invalid"] = "System.IntPtr.Zero" },
        ["HServerQuery"]                 = new() { ["Invalid"] = "-1" },
        ["PublishedFileId_t"]            = new() { ["Invalid"] = "0" },
        ["PublishedFileUpdateHandle_t"]  = new() { ["Invalid"] = "0xffffffffffffffff" },
        ["UGCFileWriteStreamHandle_t"]   = new() { ["Invalid"] = "0xffffffffffffffff" },
        ["UGCHandle_t"]                  = new() { ["Invalid"] = "0xffffffffffffffff" },
        ["ScreenshotHandle"]             = new() { ["Invalid"] = "0" },
        ["AppId_t"]                      = new() { ["Invalid"] = "0x0" },
        ["DepotId_t"]                    = new() { ["Invalid"] = "0x0" },
        ["SteamAPICall_t"]               = new() { ["Invalid"] = "0x0" },
        ["UGCQueryHandle_t"]             = new() { ["Invalid"] = "0xffffffffffffffff" },
        ["UGCUpdateHandle_t"]            = new() { ["Invalid"] = "0xffffffffffffffff" },
        ["ClientUnifiedMessageHandle"]   = new() { ["Invalid"] = "0" },
        ["SiteId_t"]                     = new() { ["Invalid"] = "0" },
        ["SteamInventoryUpdateHandle_t"] = new() { ["Invalid"] = "0xffffffffffffffff" },
        ["PartyBeaconID_t"]              = new() { ["Invalid"] = "0" },
        ["HSteamNetConnection"]          = new() { ["Invalid"] = "0" },
        ["HSteamListenSocket"]           = new() { ["Invalid"] = "0" },
        ["HSteamNetPollGroup"]           = new() { ["Invalid"] = "0" },
    };

    // ─── Entry Point ─────────────────────────────────────────────────────────────

    public static async Task GenerateAsync(SteamworksParser parser, string outputPath, string templatesPath)
    {
        string header       = await File.ReadAllTextAsync(Path.Combine(templatesPath, "header.txt"));
        string typeTemplate = await File.ReadAllTextAsync(Path.Combine(templatesPath, "typetemplate.txt"));

        string typesRoot = Path.Combine(outputPath, "Types");

        await CopyCustomTypesAsync(Path.Combine(templatesPath, "custom_types"), typesRoot, header);
        await GenerateTypedefsAsync(parser, typeTemplate, header, typesRoot);
    }

    // ─── Custom Types ─────────────────────────────────────────────────────────────

    private static async Task CopyCustomTypesAsync(string customTypesRoot, string outputRoot, string header)
    {
        if (!Directory.Exists(customTypesRoot))
            return;

        foreach (var file in Directory.EnumerateFiles(customTypesRoot, "*", SearchOption.AllDirectories))
        {
            var relative  = Path.GetRelativePath(customTypesRoot, file);
            var dest      = Path.Combine(outputRoot, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            var content = await File.ReadAllTextAsync(file);
            await File.WriteAllTextAsync(dest, header + content);
        }
    }

    // ─── Typedefs ─────────────────────────────────────────────────────────────────

    private static async Task GenerateTypedefsAsync(SteamworksParser parser, string template, string header, string typesRoot)
    {
        foreach (var t in parser.Typedefs)
        {
            if (UnusedTypedefs.Contains(t.Name))
                continue;

            string ourType = TypeDict.GetValueOrDefault(t.Type, t.Type);

            string readonlyBlock = BuildReadonlyBlock(t.Name, ourType);

            string output = ApplyTemplate(template, t.Name, ourType, readonlyBlock);

            string folder = GetFolderName(t.FileName);
            string dir    = Path.Combine(typesRoot, folder);
            Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(Path.Combine(dir, t.Name + ".cs"), header + output);
        }
    }

    private static string BuildReadonlyBlock(string name, string type)
    {
        if (!ReadOnlyValues.TryGetValue(name, out var fields))
            return "";

        var sb = new System.Text.StringBuilder();
        foreach (var (fieldName, value) in fields)
            sb.AppendLine($"\t\tpublic static readonly {name} {fieldName} = new {name}({value});");

        return sb.ToString();
    }

    private static string ApplyTemplate(string template, string name, string type, string readonlyBlock)
    {
        int tIdx = name.IndexOf("_t", StringComparison.Ordinal);
        string nameStripped = tIdx >= 0 ? name.Remove(tIdx, 2) : name;

        string result = template;

        if (type == "System.IntPtr")
        {
            result = result.Replace(", System.IComparable<{NAME}>", "", StringComparison.Ordinal);
            result = result.Replace(
                "\n\t\tpublic int CompareTo({NAME} other) {\n\t\t\treturn m_{NAMESTRIPPED}.CompareTo(other.m_{NAMESTRIPPED});\n\t\t}\n",
                "", StringComparison.Ordinal);
        }

        result = result.Replace("{NAME}",        name,         StringComparison.Ordinal);
        result = result.Replace("{NAMESTRIPPED}", nameStripped, StringComparison.Ordinal);
        result = result.Replace("{TYPE}",         type,         StringComparison.Ordinal);
        result = result.Replace("{READONLY}\r\n", readonlyBlock, StringComparison.Ordinal);
        result = result.Replace("{READONLY}\n",   readonlyBlock, StringComparison.Ordinal);
        result = result.Replace("{READONLY}",     readonlyBlock, StringComparison.Ordinal);

        return result;
    }

    private static string GetFolderName(string fileName)
    {
        // Strip extension
        string folder = Path.GetFileNameWithoutExtension(fileName);

        // isteamX → steamX
        folder = folder.Replace("isteam", "steam", StringComparison.Ordinal);

        // Capitalize everything after the "steam" prefix → "Steam" + rest.Capitalize()
        if (folder.Length > 5)
            folder = "Steam" + char.ToUpperInvariant(folder[5]) + folder[6..];
        else
            folder = "Steam" + folder[5..];

        return PrettyFilenames.GetValueOrDefault(folder, folder);
    }
}
