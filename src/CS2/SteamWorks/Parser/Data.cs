namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public static class ParserData
{
    public static readonly HashSet<string> SkippedFiles = [
        "steam_api_flat.h",
        "isteamps3overlayrenderer.h",
        "steamps3params.h",
        "isteamcontroller.h",
        "isteamdualsense.h",
    ];

    public static readonly HashSet<string> SkippedLines = [
        "STEAM_CLANG_ATTR",
        "#define VALVE_BIG_ENDIAN",
        "public:",
        "private:",
        "protected:",
        "_STEAM_CALLBACK_",
        "#define STEAM_CALLBACK_BEGIN",
        "#define STEAM_CALLBACK_END",
        "#define STEAM_CALLBACK_MEMBER",
        "STEAM_DEFINE_INTERFACE_ACCESSOR",
    ];

    public static readonly HashSet<string> SkippedStructs = [
        "SteamNetworkingIPAddr",
        "SteamNetworkingIdentity",
        "SteamNetworkingMessage_t",
        "SteamNetworkingConfigValue_t",

        "SteamDatagramHostedAddress",
        "SteamDatagramRelayAuthTicket",
    ];

    public static readonly HashSet<string> FunctionAttributes = [
         "STEAM_METHOD_DESC",
        "STEAM_IGNOREATTR",
        "STEAM_CALL_RESULT",
        "STEAM_CALL_BACK",
        "STEAM_FLAT_NAME",
    ];

    public static readonly HashSet<string> ArgumentAttributes = [
        "STEAM_ARRAY_COUNT",
        "STEAM_ARRAY_COUNT_D",
        "STEAM_BUFFER_COUNT",
        "STEAM_DESC",
        "STEAM_OUT_ARRAY_CALL",
        "STEAM_OUT_ARRAY_COUNT",
        "STEAM_OUT_BUFFER_COUNT",
        "STEAM_OUT_STRING",
        "STEAM_OUT_STRING_COUNT",
        "STEAM_OUT_STRUCT",
    ];

    public static readonly HashSet<string> GameServerInterfaces = [
        "isteamclient.h",
        "isteamutils.h",
        "isteamnetworking.h",
        "isteaminventory.h",
        "isteamhttp.h",
        "isteamugc.h",
        "isteamnetworkingutils.h",
        "isteamnetworkingsockets.h",
    ];
}
