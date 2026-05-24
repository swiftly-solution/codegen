using System.Text;
using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators.SteamWorks;

internal static class ConstantsGenerator
{
    private static readonly Dictionary<string, string> TypeDict = new()
    {
        ["uint16"]       = "ushort",
        ["uint32"]       = "int",
        ["unsigned int"] = "int",
        ["uint64"]       = "ulong",
        ["size_t"]       = "int",
    };

    private static readonly HashSet<string> SkippedDefines =
    [
        "VALVE_COMPILE_TIME_ASSERT(",
        "REFERENCE(arg)",
        "STEAM_CALLBACK_BEGIN(",
        "STEAM_CALLBACK_MEMBER(",
        "STEAM_CALLBACK_ARRAY(",
        "END_CALLBACK_INTERNAL_BEGIN(",
        "END_CALLBACK_INTERNAL_SWITCH(",
        "END_CALLBACK_INTERNAL_END()",
        "STEAM_CALLBACK_END(",
        "INVALID_HTTPCOOKIE_HANDLE",
        "BChatMemberStateChangeRemoved(",
        "STEAM_COLOR_RED(",
        "STEAM_COLOR_GREEN(",
        "STEAM_COLOR_BLUE(",
        "STEAM_COLOR_ALPHA(",
        "INVALID_SCREENSHOT_HANDLE",
        "_snprintf",
        "S_API",
        "STEAM_CALLBACK(",
        "STEAM_CALLBACK_MANUAL(",
        "STEAM_GAMESERVER_CALLBACK(",
        "k_steamIDNil",
        "k_steamIDOutofDateGS",
        "k_steamIDLanModeGS",
        "k_steamIDNotInitYetGS",
        "k_steamIDNonSteamGS",
        "STEAM_PS3_PATH_MAX",
        "STEAM_PS3_SERVICE_ID_MAX",
        "STEAM_PS3_COMMUNICATION_ID_MAX",
        "STEAM_PS3_COMMUNICATION_SIG_MAX",
        "STEAM_PS3_LANGUAGE_MAX",
        "STEAM_PS3_REGION_CODE_MAX",
        "STEAM_PS3_CURRENT_PARAMS_VER",
        "STEAMPS3_MALLOC_INUSE",
        "STEAMPS3_MALLOC_SYSTEM",
        "STEAMPS3_MALLOC_OK",
        "S_CALLTYPE",
        "POSIX",
        "STEAM_PRIVATE_API(",
        "STEAMNETWORKINGSOCKETS_INTERFACE",
        "S_OVERRIDE",
        "ControllerAnalogActionData_t",
        "ControllerDigitalActionData_t",
        "ControllerMotionData_t",
    ];

    private static readonly HashSet<string> SkippedConstants =
    [
        "k_FriendsGroupID_Invalid",
        "INVALID_HTMLBROWSER",
        "k_SteamItemInstanceIDInvalid",
        "k_SteamInventoryResultInvalid",
        "k_SteamInventoryUpdateHandleInvalid",
        "HSERVERQUERY_INVALID",
        "k_UGCHandleInvalid",
        "k_PublishedFileIdInvalid",
        "k_PublishedFileUpdateHandleInvalid",
        "k_UGCFileStreamHandleInvalid",
        "k_UGCQueryHandleInvalid",
        "k_UGCUpdateHandleInvalid",
        "k_HAuthTicketInvalid",
        "k_uAppIdInvalid",
        "k_uDepotIdInvalid",
        "k_uAPICallInvalid",
        "k_HSteamNetConnection_Invalid",
        "k_HSteamListenSocket_Invalid",
        "k_HSteamNetPollGroup_Invalid",
        "k_SteamDatagramPOPID_dev",
        "MASTERSERVERUPDATERPORT_USEGAMESOCKETSHARE",
    ];

    private static readonly HashSet<string> SkippedTypedefs =
    [
        "uint8", "int8", "uint16", "int32", "uint32", "int64", "uint64",
    ];

    private static readonly Dictionary<string, (string Type, string? Value)> CustomDefines = new()
    {
        ["k_nMaxLobbyKeyLength"]                   = ("byte",  null),
        ["STEAM_CONTROLLER_HANDLE_ALL_CONTROLLERS"] = ("ulong", "0xFFFFFFFFFFFFFFFF"),
        ["STEAM_CONTROLLER_MIN_ANALOG_ACTION_DATA"] = ("float", "-1.0f"),
        ["STEAM_CONTROLLER_MAX_ANALOG_ACTION_DATA"] = ("float", "1.0f"),
        ["STEAM_INPUT_HANDLE_ALL_CONTROLLERS"]      = ("ulong", "0xFFFFFFFFFFFFFFFF"),
        ["STEAM_INPUT_MIN_ANALOG_ACTION_DATA"]      = ("float", "-1.0f"),
        ["STEAM_INPUT_MAX_ANALOG_ACTION_DATA"]      = ("float", "1.0f"),
    };

    public static async Task GenerateAsync(SteamworksParser parser, string outputPath)
    {
        var (interfaceVersions, defines) = ParseDefines(parser);
        var constants = ParseConstants(parser);

        var sb = new StringBuilder();
        sb.AppendLine("namespace SwiftlyS2.Shared.SteamAPI {");
        sb.AppendLine("\tpublic static class Constants {");

        foreach (var c in interfaceVersions.Concat(constants).Concat(defines))
        {
            foreach (var pre in c.PreComments)
                sb.AppendLine("\t\t//" + pre);
            sb.AppendLine($"\t\tpublic const {c.Type} {c.Name}{c.Spacing}= {c.Value};{c.Comment}");
        }

        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine();

        await File.WriteAllTextAsync(Path.Combine(outputPath, "SteamConstants.cs"), sb.ToString(), Encoding.UTF8);
    }

    private static (List<Constant> InterfaceVersions, List<Constant> Defines) ParseDefines(SteamworksParser parser)
    {
        var defines = new List<Constant>();
        var interfaceVersions = new List<Constant>();

        foreach (var f in parser.Files)
        {
            foreach (var d in f.Defines)
            {
                if (IsSkippedDefine(d.Name))
                    continue;

                var comment = d.C?.LineComment is { } lc ? " //" + lc : "";
                var preComments = d.C?.PreComments ?? [];

                var type = "int";
                var value = d.Value;

                if (CustomDefines.TryGetValue(d.Name, out var custom))
                {
                    type = custom.Type;
                    if (custom.Value is not null)
                        value = custom.Value;
                }
                else if (d.Value.StartsWith('"'))
                {
                    type = "string";
                    if (d.Name.StartsWith("STEAM", StringComparison.Ordinal))
                    {
                        interfaceVersions.Add(new Constant(d.Name, value, type, preComments, comment, " "));
                        continue;
                    }
                }

                value = NormalizeFloatLiteral(value);
                defines.Add(new Constant(d.Name, value, type, preComments, comment, d.Spacing));
            }
        }

        return (interfaceVersions, defines);
    }

    private static List<Constant> ParseConstants(SteamworksParser parser)
    {
        var result = new List<Constant>();

        foreach (var f in parser.Files)
        {
            foreach (var constant in f.Constants)
            {
                if (SkippedConstants.Contains(constant.Name))
                    continue;

                var comment = constant.C?.LineComment is { } lc ? " //" + lc : "";
                var preComments = constant.C?.PreComments ?? [];

                var type = constant.Type;
                foreach (var td in parser.Typedefs)
                {
                    if (SkippedTypedefs.Contains(td.Name))
                        continue;
                    if (td.Name == constant.Type)
                    {
                        type = td.Type;
                        break;
                    }
                }
                type = TypeDict.GetValueOrDefault(type, type);

                var value = NormalizeFloatLiteral(constant.Value switch
                {
                    "0xFFFFFFFF"            => "-1",
                    "0xffffffffffffffffull" => "0xffffffffffffffff",
                    var v                   => v
                });

                result.Add(new Constant(constant.Name, value, type, preComments, comment, " "));
            }
        }

        return result;
    }

    // Fixes C++ float literals like "600.f" → "600.0f"
    private static string NormalizeFloatLiteral(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"(\d)\.f\b", "$1.0f");

    private static bool IsSkippedDefine(string name) =>
        SkippedDefines.Any(skip =>
            skip.EndsWith('(')
                ? name.StartsWith(skip, StringComparison.Ordinal)
                : name == skip);

    private sealed record Constant(
        string Name, string Value, string Type,
        List<string> PreComments, string Comment, string Spacing);
}
