using System.Text;
using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators.SteamWorks;

internal static class StructsGenerator
{
    // ─── Static Data ─────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> TypeConversionDict = new()
    {
        ["uint8"]        = "byte",
        ["uint16"]       = "ushort",
        ["uint32"]       = "uint",
        ["uint64"]       = "ulong",
        ["char"]         = "string",
        ["int32"]        = "int",
        ["int64"]        = "long",
        ["uint8 *"]      = "IntPtr",
        ["const char *"] = "string",
        ["const char **"] = "IntPtr",
        ["HSteamUser"]   = "int",
    };

    private static readonly Dictionary<string, string> CustomPackSize = new()
    {
        ["AvatarImageLoaded_t"]                = "4",
        ["FriendRichPresenceUpdate_t"]         = "4",
        ["GameConnectedClanChatMsg_t"]         = "4",
        ["GameConnectedChatLeave_t"]           = "1",
        ["JoinClanChatRoomCompletionResult_t"] = "4",
        ["GameConnectedFriendChatMsg_t"]       = "4",
        ["FriendsGetFollowerCount_t"]          = "4",
        ["FriendsIsFollowing_t"]               = "4",
        ["FriendsEnumerateFollowingList_t"]    = "4",
        ["GSClientDeny_t"]                     = "4",
        ["GSClientKick_t"]                     = "4",
        ["GSClientGroupStatus_t"]              = "1",
        ["GSStatsReceived_t"]                  = "4",
        ["GSStatsStored_t"]                    = "4",
        ["P2PSessionConnectFail_t"]            = "1",
        ["SocketStatusCallback_t"]             = "4",
        ["ValidateAuthTicketResponse_t"]       = "4",
        ["InputAnalogActionData_t"]            = "1",
        ["InputDigitalActionData_t"]           = "1",
    };

    private static readonly HashSet<string> SkippedStructs =
    [
        "PSNGameBootInviteResult_t",
        "PS3TrophiesInstalled_t",
        "ControllerAnalogActionData_t",
        "ControllerDigitalActionData_t",
        "ControllerMotionData_t",
        "SteamNetworkingIdentityRender",
        "SteamNetworkingIPAddrRender",
        "SteamNetworkingPOPIDRender",
        "SteamIPAddress_t",
        "SteamInputActionEvent_t",
    ];

    private static readonly HashSet<string> SequentialStructs =
    [
        "MatchMakingKeyValuePair_t",
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> SpecialFieldTypes = new()
    {
        ["PersonaStateChange_t"]   = new() { ["m_nChangeFlags"] = "EPersonaChange" },
        ["HTML_NeedsPaint_t"]      = new() { ["pBGRA"] = "IntPtr" },
        ["InputAnalogActionData_t"] = new() { ["bActive"] = "byte" },
        ["InputDigitalActionData_t"] = new() { ["bState"] = "byte", ["bActive"] = "byte" },
    };

    // struct name → field name → explicit FieldOffset value
    private static readonly Dictionary<string, Dictionary<string, string>> ExplicitStructs = new()
    {
        ["UserStatsReceived_t"] = new()
        {
            ["m_nGameID"]     = "0",
            ["m_eResult"]     = "8",
            ["m_steamIDUser"] = "12",
        },
    };

    // ─── Entry Point ─────────────────────────────────────────────────────────────

    public static async Task GenerateAsync(SteamworksParser parser, string outputPath)
    {
        var structLines   = new List<string>();
        var callbackLines = new List<string>();

        foreach (var f in parser.Files)
        {
            foreach (var s in f.Structs)   structLines.AddRange(ParseStruct(s));
            foreach (var c in f.Callbacks) callbackLines.AddRange(ParseStruct(c));
        }

        await WriteFile(Path.Combine(outputPath, "SteamStructs.cs"),   structLines);
        await WriteFile(Path.Combine(outputPath, "SteamCallbacks.cs"), callbackLines);
    }

    private static async Task WriteFile(string path, List<string> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftlyS2.Shared.SteamAPI {");
        foreach (var line in lines)
            sb.AppendLine(line);
        sb.AppendLine("}");
        sb.AppendLine();
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    // ─── Struct ───────────────────────────────────────────────────────────────────

    private static List<string> ParseStruct(Struct s)
    {
        if (SkippedStructs.Contains(s.Name))
            return [];

        var lines = new List<string>();

        WriteRawPreComments(lines, s.C?.RawPreComments, "\t", skipBlankLines: true);

        string packSize = CustomPackSize.GetValueOrDefault(s.Name, "Packsize.value");
        bool isExplicit = ExplicitStructs.ContainsKey(s.Name);

        if (isExplicit)
        {
            lines.Add($"\t[StructLayout(LayoutKind.Explicit, Pack = {packSize})]");
        }
        else if (s.PackSize.Count > 0)
        {
            string sizeExtra = s.Fields.Count == 0 ? ", Size = 1" : "";
            lines.Add($"\t[StructLayout(LayoutKind.Sequential, Pack = {packSize}{sizeExtra})]");
        }

        if (s.CallbackId is { } cbId)
            lines.Add($"\t[CallbackIdentity(Constants.{cbId})]");

        if (SequentialStructs.Contains(s.Name))
            lines.Add("\t[StructLayout(LayoutKind.Sequential)]");

        lines.Add($"\tpublic struct {s.Name} {{");
        lines.AddRange(InsertConstructors(s.Name));

        if (s.CallbackId is { } cbId2)
            lines.Add($"\t\tpublic const int k_iCallback = Constants.{cbId2};");

        foreach (var field in s.Fields)
            lines.AddRange(ParseField(field, s.Name));

        if (s.EndComments is not null)
        {
            foreach (var comment in s.EndComments.RawPreComments)
            {
                if (comment is BlankLine)     lines.Add("\t\t");
                else if (comment is string sc) lines.Add("\t" + sc);
            }
        }

        lines.Add("\t}");
        lines.Add("");
        return lines;
    }

    // ─── Field ────────────────────────────────────────────────────────────────────

    private static List<string> ParseField(StructField field, string structName)
    {
        var lines = new List<string>();

        if (field.C?.RawPreComments is { } preComments)
        {
            foreach (var preComment in preComments)
            {
                if (preComment is BlankLine)      lines.Add("\t\t");
                else if (preComment is string sc) lines.Add("\t" + sc);
            }
        }

        string fieldType = TypeConversionDict.GetValueOrDefault(field.Type, field.Type);
        if (SpecialFieldTypes.TryGetValue(structName, out var specFields) && specFields.TryGetValue(field.Name, out var specType))
            fieldType = specType;

        if (ExplicitStructs.TryGetValue(structName, out var offsets) && offsets.TryGetValue(field.Name, out var offset))
            lines.Add($"\t\t[FieldOffset({offset})]");

        string comment      = field.C?.RawLineComment ?? "";
        string constantsStr = "";

        if (field.ArraySize is not null)
        {
            constantsStr = (field.ArraySize.Length > 0 && field.ArraySize.All(char.IsDigit)) ? "" : "Constants.";

            // Replicate Python's two independent if-chains exactly:
            if (fieldType == "byte[]")
                lines.Add($"\t\t[MarshalAs(UnmanagedType.ByValArray, SizeConst = {constantsStr}{field.ArraySize})]");

            if (structName == "MatchMakingKeyValuePair_t")
                lines.Add($"\t\t[MarshalAs(UnmanagedType.ByValTStr, SizeConst = {constantsStr}{field.ArraySize})]");
            else
            {
                lines.Add($"\t\t[MarshalAs(UnmanagedType.ByValArray, SizeConst = {constantsStr}{field.ArraySize})]");
                fieldType += "[]";
            }
        }

        if (fieldType == "bool")
            lines.Add("\t\t[MarshalAs(UnmanagedType.I1)]");

        if (field.ArraySize is not null && fieldType == "string[]")
        {
            lines.Add($"\t\tprivate byte[] {field.Name}_;");
            lines.Add($"\t\tpublic string {field.Name}{comment}");
            lines.Add("\t\t{");
            lines.Add($"\t\t\tget {{ return InteropHelp.ByteArrayToStringUTF8({field.Name}_); }}");
            lines.Add($"\t\t\tset {{ InteropHelp.StringToByteArrayUTF8(value, {field.Name}_, {constantsStr}{field.ArraySize}); }}");
            lines.Add("\t\t}");
        }
        else
        {
            lines.Add($"\t\tpublic {fieldType} {field.Name};{comment}");
        }

        return lines;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static List<string> InsertConstructors(string name)
    {
        if (name != "MatchMakingKeyValuePair_t")
            return [];

        return
        [
            "\t\tMatchMakingKeyValuePair_t(string strKey, string strValue) {",
            "\t\t\tm_szKey = strKey;",
            "\t\t\tm_szValue = strValue;",
            "\t\t}",
            "",
        ];
    }

    private static void WriteRawPreComments(List<string> lines, List<object>? rawComments, string indent, bool skipBlankLines)
    {
        if (rawComments is null) return;
        foreach (var comment in rawComments)
        {
            if (comment is BlankLine)      { if (!skipBlankLines) lines.Add(""); }
            else if (comment is string sc) lines.Add(indent + sc);
        }
    }
}
