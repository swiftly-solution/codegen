using System.Text;
using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators.SteamWorks;

internal static class InterfacesGenerator
{
    // ─── Static Data ─────────────────────────────────────────────────────────────

    private static readonly HashSet<string> SkippedFiles =
    [
        "isteamappticket.h",
        "isteamgamecoordinator.h",
        "isteamps3overlayrenderer.h",
    ];

    private static readonly HashSet<string> SkippedInterfaces =
    [
        "ISteamNetworkingConnectionCustomSignaling",
        "ISteamGameServerNetworkingConnectionCustomSignaling",
        "ISteamNetworkingCustomSignalingRecvContext",
        "ISteamGameServerNetworkingCustomSignalingRecvContext",
        "ISteamNetworkingFakeUDPPort",
    ];

    private static readonly HashSet<string> SkippedTypedefs =
    [
        "uint8", "int8", "int16", "uint16", "int32", "uint32", "int64", "uint64",
    ];

    private static readonly Dictionary<string, string> TypeDict = new()
    {
        ["char*"]                              = "IntPtr",
        ["char *"]                             = "IntPtr",
        ["char **"]                            = "out IntPtr",
        ["const char*"]                        = "InteropHelp.UTF8StringHandle",
        ["const char *"]                       = "InteropHelp.UTF8StringHandle",
        ["const void *"]                       = "IntPtr",
        ["unsigned short"]                     = "ushort",
        ["void *"]                             = "IntPtr",
        ["void*"]                              = "IntPtr",
        ["uint8"]                              = "byte",
        ["int16"]                              = "short",
        ["uint16"]                             = "ushort",
        ["int32"]                              = "int",
        ["uint32"]                             = "uint",
        ["unsigned int"]                       = "uint",
        ["const uint32"]                       = "uint",
        ["int64"]                              = "long",
        ["uint64"]                             = "ulong",
        ["uint64_t"]                           = "ulong",
        ["size_t"]                             = "ulong",
        ["intptr_t"]                           = "IntPtr",
        ["const char **"]                      = "IntPtr",
        ["RTime32"]                            = "uint",
        ["const SteamItemInstanceID_t"]        = "SteamItemInstanceID_t",
        ["const SteamItemDef_t"]               = "SteamItemDef_t",
        ["SteamParamStringArray_t *"]          = "IntPtr",
        ["const SteamParamStringArray_t *"]    = "IntPtr",
        ["ISteamMatchmakingServerListResponse *"] = "IntPtr",
        ["ISteamMatchmakingPingResponse *"]    = "IntPtr",
        ["ISteamMatchmakingPlayersResponse *"] = "IntPtr",
        ["ISteamMatchmakingRulesResponse *"]   = "IntPtr",
        ["ControllerAnalogActionData_t"]       = "InputAnalogActionData_t",
        ["ControllerDigitalActionData_t"]      = "InputDigitalActionData_t",
        ["ControllerMotionData_t"]             = "InputMotionData_t",
        ["SteamNetworkPingLocation_t &"]       = "out SteamNetworkPingLocation_t",
        ["const SteamNetworkPingLocation_t &"] = "ref SteamNetworkPingLocation_t",
        ["SteamNetworkingIPAddr &"]            = "out SteamNetworkingIPAddr",
        ["const SteamNetworkingIPAddr &"]      = "ref SteamNetworkingIPAddr",
        ["const SteamNetworkingConfigValue_t *"] = "SteamNetworkingConfigValue_t[]",
        ["const SteamNetworkingIdentity &"]    = "ref SteamNetworkingIdentity",
        ["const SteamNetworkingIdentity *"]    = "ref SteamNetworkingIdentity",
        ["SteamNetworkingErrMsg &"]            = "out SteamNetworkingErrMsg",
        ["const SteamNetConnectionInfo_t &"]   = "ref SteamNetConnectionInfo_t",
        ["SteamNetworkingMessage_t **"]        = "IntPtr[]",
        ["SteamDatagramGameCoordinatorServerLogin *"] = "IntPtr",
        ["ISteamNetworkingFakeUDPPort *"]      = "IntPtr",
        ["const ScePadTriggerEffectParam *"]   = "IntPtr",
    };

    private static readonly Dictionary<string, string> WrapperArgsTypeDict = new()
    {
        ["SteamParamStringArray_t *"]          = "System.Collections.Generic.IList<string>",
        ["const SteamParamStringArray_t *"]    = "System.Collections.Generic.IList<string>",
        ["ISteamMatchmakingServerListResponse *"] = "ISteamMatchmakingServerListResponse",
        ["ISteamMatchmakingPingResponse *"]    = "ISteamMatchmakingPingResponse",
        ["ISteamMatchmakingPlayersResponse *"] = "ISteamMatchmakingPlayersResponse",
        ["ISteamMatchmakingRulesResponse *"]   = "ISteamMatchmakingRulesResponse",
        ["MatchMakingKeyValuePair_t **"]       = "MatchMakingKeyValuePair_t[]",
        ["char **"]                            = "out string",
    };

    private static readonly Dictionary<string, string> ReturnTypeDict = new()
    {
        ["const char *"]          = "IntPtr",
        ["CSteamID"]              = "ulong",
        ["gameserveritem_t *"]    = "IntPtr",
        ["SteamNetworkingMessage_t *"] = "IntPtr",
        ["ISteamAppList *"]       = "IntPtr",
        ["ISteamApps *"]          = "IntPtr",
        ["ISteamController *"]    = "IntPtr",
        ["ISteamFriends *"]       = "IntPtr",
        ["ISteamGameSearch *"]    = "IntPtr",
        ["ISteamGameServer *"]    = "IntPtr",
        ["ISteamGameServerStats *"] = "IntPtr",
        ["ISteamHTMLSurface *"]   = "IntPtr",
        ["ISteamHTTP *"]          = "IntPtr",
        ["ISteamInput *"]         = "IntPtr",
        ["ISteamInventory *"]     = "IntPtr",
        ["ISteamMatchmaking *"]   = "IntPtr",
        ["ISteamMatchmakingServers *"] = "IntPtr",
        ["ISteamMusic *"]         = "IntPtr",
        ["ISteamMusicRemote *"]   = "IntPtr",
        ["ISteamNetworking *"]    = "IntPtr",
        ["ISteamParentalSettings *"] = "IntPtr",
        ["ISteamParties *"]       = "IntPtr",
        ["ISteamPS3OverlayRender *"] = "IntPtr",
        ["ISteamRemotePlay *"]    = "IntPtr",
        ["ISteamRemoteStorage *"] = "IntPtr",
        ["ISteamScreenshots *"]   = "IntPtr",
        ["ISteamUGC *"]           = "IntPtr",
        ["ISteamUser *"]          = "IntPtr",
        ["ISteamUserStats *"]     = "IntPtr",
        ["ISteamUtils *"]         = "IntPtr",
        ["ISteamVideo *"]         = "IntPtr",
    };

    private static readonly Dictionary<string, string> SpecialReturnTypeDict = new()
    {
        ["ISteamUtils_GetAppID"]           = "AppId_t",
        ["ISteamGameServerUtils_GetAppID"] = "AppId_t",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> SpecialArgsDict = new()
    {
        ["ISteamAppList_GetInstalledApps"]  = new() { ["pvecAppID"] = "AppId_t[]" },
        ["ISteamApps_GetInstalledDepots"]   = new() { ["pvecDepots"] = "DepotId_t[]" },
        ["ISteamGameServer_SendUserConnectAndAuthenticate_DEPRECATED"] = new() { ["pvAuthBlob"] = "byte[]" },
        ["ISteamGameServer_GetAuthSessionTicket"]  = new() { ["pTicket"] = "byte[]" },
        ["ISteamGameServer_BeginAuthSession"]      = new() { ["pAuthTicket"] = "byte[]" },
        ["ISteamGameServer_HandleIncomingPacket"]  = new() { ["pData"] = "byte[]" },
        ["ISteamGameServer_GetNextOutgoingPacket"] = new() { ["pOut"] = "byte[]" },
        ["ISteamHTTP_GetHTTPResponseHeaderValue"]  = new() { ["pHeaderValueBuffer"] = "byte[]" },
        ["ISteamHTTP_GetHTTPResponseBodyData"]     = new() { ["pBodyDataBuffer"] = "byte[]" },
        ["ISteamHTTP_GetHTTPStreamingResponseBodyData"] = new() { ["pBodyDataBuffer"] = "byte[]" },
        ["ISteamHTTP_SetHTTPRequestRawPostBody"]   = new() { ["pubBody"] = "byte[]" },
        ["ISteamInventory_SerializeResult"]        = new() { ["pOutBuffer"] = "byte[]" },
        ["ISteamInventory_DeserializeResult"]      = new() { ["pBuffer"] = "byte[]" },
        ["ISteamMatchmaking_SendLobbyChatMsg"]     = new() { ["pvMsgBody"] = "byte[]" },
        ["ISteamMatchmaking_GetLobbyChatEntry"]    = new() { ["pvData"] = "byte[]" },
        ["ISteamMusicRemote_SetPNGIcon_64x64"]     = new() { ["pvBuffer"] = "byte[]" },
        ["ISteamMusicRemote_UpdateCurrentEntryCoverArt"] = new() { ["pvBuffer"] = "byte[]" },
        ["ISteamNetworking_SendP2PPacket"]          = new() { ["pubData"] = "byte[]" },
        ["ISteamNetworking_ReadP2PPacket"]          = new() { ["pubDest"] = "byte[]" },
        ["ISteamNetworking_SendDataOnSocket"]       = new() { ["pubData"] = "byte[]" },
        ["ISteamNetworking_RetrieveDataFromSocket"] = new() { ["pubDest"] = "byte[]" },
        ["ISteamNetworking_RetrieveData"]           = new() { ["pubDest"] = "byte[]" },
        ["ISteamRemoteStorage_FileWrite"]            = new() { ["pvData"] = "byte[]" },
        ["ISteamRemoteStorage_FileRead"]             = new() { ["pvData"] = "byte[]" },
        ["ISteamRemoteStorage_FileWriteAsync"]       = new() { ["pvData"] = "byte[]" },
        ["ISteamRemoteStorage_FileReadAsyncComplete"] = new() { ["pvBuffer"] = "byte[]" },
        ["ISteamRemoteStorage_FileWriteStreamWriteChunk"] = new() { ["pvData"] = "byte[]" },
        ["ISteamRemoteStorage_UGCRead"]             = new() { ["pvData"] = "byte[]" },
        ["ISteamScreenshots_WriteScreenshot"]        = new() { ["pubRGB"] = "byte[]" },
        ["ISteamUGC_CreateQueryUGCDetailsRequest"]   = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamUGC_GetQueryUGCChildren"]            = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamUGC_GetSubscribedItems"]             = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamUGC_StartPlaytimeTracking"]          = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamUGC_StopPlaytimeTracking"]           = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamUser_InitiateGameConnection_DEPRECATED"] = new() { ["pAuthBlob"] = "byte[]" },
        ["ISteamUser_GetAvailableVoice"]   = new() { ["pcbUncompressed_Deprecated"] = "IntPtr" },
        ["ISteamUser_GetVoice"] = new()
        {
            ["pDestBuffer"] = "byte[]",
            ["pUncompressedDestBuffer_Deprecated"] = "IntPtr",
            ["nUncompressBytesWritten_Deprecated"] = "IntPtr",
        },
        ["ISteamUser_DecompressVoice"]     = new() { ["pCompressed"] = "byte[]", ["pDestBuffer"] = "byte[]" },
        ["ISteamUser_GetAuthSessionTicket"]= new() { ["pTicket"] = "byte[]" },
        ["ISteamUser_BeginAuthSession"]    = new() { ["pAuthTicket"] = "byte[]" },
        ["ISteamUser_RequestEncryptedAppTicket"] = new() { ["pDataToInclude"] = "byte[]" },
        ["ISteamUser_GetEncryptedAppTicket"]     = new() { ["pTicket"] = "byte[]" },
        ["ISteamUserStats_GetDownloadedLeaderboardEntry"] = new() { ["pDetails"] = "int[]" },
        ["ISteamUserStats_UploadLeaderboardScore"]        = new() { ["pScoreDetails"] = "int[]" },
        ["ISteamUtils_GetImageRGBA"]       = new() { ["pubDest"] = "byte[]" },
        ["ISteamGameServerHTTP_GetHTTPResponseHeaderValue"]       = new() { ["pHeaderValueBuffer"] = "byte[]" },
        ["ISteamGameServerHTTP_GetHTTPResponseBodyData"]          = new() { ["pBodyDataBuffer"] = "byte[]" },
        ["ISteamGameServerHTTP_GetHTTPStreamingResponseBodyData"] = new() { ["pBodyDataBuffer"] = "byte[]" },
        ["ISteamGameServerHTTP_SetHTTPRequestRawPostBody"]        = new() { ["pubBody"] = "byte[]" },
        ["ISteamGameServerInventory_SerializeResult"]   = new() { ["pOutBuffer"] = "byte[]" },
        ["ISteamGameServerInventory_DeserializeResult"] = new() { ["pBuffer"] = "byte[]" },
        ["ISteamGameServerNetworking_SendP2PPacket"]          = new() { ["pubData"] = "byte[]" },
        ["ISteamGameServerNetworking_ReadP2PPacket"]          = new() { ["pubDest"] = "byte[]" },
        ["ISteamGameServerNetworking_SendDataOnSocket"]       = new() { ["pubData"] = "byte[]" },
        ["ISteamGameServerNetworking_RetrieveDataFromSocket"] = new() { ["pubDest"] = "byte[]" },
        ["ISteamGameServerNetworking_RetrieveData"]           = new() { ["pubDest"] = "byte[]" },
        ["ISteamGameServerUtils_GetImageRGBA"] = new() { ["pubDest"] = "byte[]" },
        ["ISteamGameServerUGC_CreateQueryUGCDetailsRequest"] = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamGameServerUGC_GetQueryUGCChildren"]          = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamGameServerUGC_GetSubscribedItems"]            = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamGameServerUGC_StartPlaytimeTracking"]         = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamGameServerUGC_StopPlaytimeTracking"]          = new() { ["pvecPublishedFileID"] = "PublishedFileId_t[]" },
        ["ISteamFriends_GetFriendCount"]   = new() { ["iFriendFlags"] = "EFriendFlags" },
        ["ISteamFriends_GetFriendByIndex"] = new() { ["iFriendFlags"] = "EFriendFlags" },
        ["ISteamFriends_HasFriend"]        = new() { ["iFriendFlags"] = "EFriendFlags" },
        ["ISteamInventory_GetResultItems"] = new() { ["punOutItemsArraySize"] = "ref uint" },
        ["ISteamInventory_GetItemDefinitionProperty"]  = new() { ["punValueBufferSizeOut"] = "ref uint" },
        ["ISteamInventory_GetResultItemProperty"]      = new() { ["punValueBufferSizeOut"] = "ref uint" },
        ["ISteamInventory_GetItemDefinitionIDs"]       = new() { ["punItemDefIDsArraySize"] = "ref uint" },
        ["ISteamInventory_GetEligiblePromoItemDefinitionIDs"] = new() { ["punItemDefIDsArraySize"] = "ref uint" },
        ["ISteamGameServerInventory_GetResultItems"]   = new() { ["punOutItemsArraySize"] = "ref uint" },
        ["ISteamGameServerInventory_GetItemDefinitionProperty"]  = new() { ["punValueBufferSizeOut"] = "ref uint" },
        ["ISteamGameServerInventory_GetResultItemProperty"]      = new() { ["punValueBufferSizeOut"] = "ref uint" },
        ["ISteamGameServerInventory_GetItemDefinitionIDs"]       = new() { ["punItemDefIDsArraySize"] = "ref uint" },
        ["ISteamGameServerInventory_GetEligiblePromoItemDefinitionIDs"] = new() { ["punItemDefIDsArraySize"] = "ref uint" },
        ["ISteamVideo_GetOPFStringForApp"]  = new() { ["pnBufferSize"] = "ref int" },
        ["ISteamParties_CreateBeacon"]      = new() { ["pBeaconLocation"] = "ref SteamPartyBeaconLocation_t" },
        ["ISteamParties_GetAvailableBeaconLocations"] = new() { ["pLocationList"] = "SteamPartyBeaconLocation_t[]" },
        ["ISteamClient_SetLocalIPBinding"]  = new() { ["unIP"] = "ref SteamIPAddress_t" },
        ["ISteamNetworkingUtils_SteamNetworkingIPAddr_ToString"] = new()
        {
            ["addr"] = "ref SteamNetworkingIPAddr", ["cbBuf"] = "uint",
        },
        ["ISteamGameServerNetworkingUtils_SteamNetworkingIPAddr_ToString"] = new()
        {
            ["addr"] = "ref SteamNetworkingIPAddr", ["cbBuf"] = "uint",
        },
        ["ISteamNetworkingUtils_SteamNetworkingIdentity_ToString"] = new()
        {
            ["identity"] = "ref SteamNetworkingIdentity", ["cbBuf"] = "uint",
        },
        ["ISteamGameServerNetworkingUtils_SteamNetworkingIdentity_ToString"] = new()
        {
            ["identity"] = "ref SteamNetworkingIdentity", ["cbBuf"] = "uint",
        },
        ["ISteamNetworkingUtils_GetConfigValue"] = new() { ["cbResult"] = "ref ulong" },
        ["ISteamGameServerNetworkingUtils_GetConfigValue"] = new() { ["cbResult"] = "ref ulong" },
        ["ISteamNetworkingSockets_SendMessages"] = new()
        {
            ["pMessages"] = "SteamNetworkingMessage_t[]", ["pOutMessageNumberOrResult"] = "long[]",
        },
        ["ISteamGameServerNetworkingSockets_SendMessages"] = new()
        {
            ["pMessages"] = "SteamNetworkingMessage_t[]", ["pOutMessageNumberOrResult"] = "long[]",
        },
        ["ISteamNetworkingSockets_GetConnectionRealTimeStatus"] = new()
        {
            ["pStatus"] = "ref SteamNetConnectionRealTimeStatus_t",
            ["pLanes"]  = "ref SteamNetConnectionRealTimeLaneStatus_t",
        },
        ["ISteamGameServerNetworkingSockets_GetConnectionRealTimeStatus"] = new()
        {
            ["pStatus"] = "ref SteamNetConnectionRealTimeStatus_t",
            ["pLanes"]  = "ref SteamNetConnectionRealTimeLaneStatus_t",
        },
    };

    private static readonly Dictionary<string, Dictionary<string, string>> SpecialWrapperArgsDict = new()
    {
        ["ISteamFriends_GetClanChatMessage"] = new() { ["prgchText"] = "out string" },
        ["ISteamFriends_GetFriendMessage"]   = new() { ["pvData"] = "out string" },
        ["ISteamClient_SetLocalIPBinding"]   = new() { ["unIP"] = "ref SteamIPAddress_t" },
        ["ISteamGameServerClient_SetLocalIPBinding"] = new() { ["unIP"] = "ref SteamIPAddress_t" },
    };

    // entry → argName → attributeName → value
    private static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> FixedAttributeValues = new()
    {
        ["ISteamInventory_GetItemsWithPrices"] = new()
        {
            ["pArrayItemDefs"]  = new() { ["STEAM_OUT_ARRAY_COUNT"] = "unArrayLength" },
            ["pCurrentPrices"]  = new() { ["STEAM_OUT_ARRAY_COUNT"] = "unArrayLength" },
            ["pBasePrices"]     = new() { ["STEAM_OUT_ARRAY_COUNT"] = "unArrayLength" },
        },
        ["ISteamGameServerInventory_GetItemsWithPrices"] = new()
        {
            ["pArrayItemDefs"]  = new() { ["STEAM_OUT_ARRAY_COUNT"] = "unArrayLength" },
            ["pCurrentPrices"]  = new() { ["STEAM_OUT_ARRAY_COUNT"] = "unArrayLength" },
            ["pBasePrices"]     = new() { ["STEAM_OUT_ARRAY_COUNT"] = "unArrayLength" },
        },
    };

    private static readonly Dictionary<string, string> SpecialOutStringRetCmp = new()
    {
        ["ISteamFriends_GetClanChatMessage"] = "ret != 0",
        ["ISteamFriends_GetFriendMessage"]   = "ret != 0",
    };

    private static readonly Dictionary<string, string> ArgDefaultLookup = new()
    {
        ["k_EActivateGameOverlayToWebPageMode_Default"] = "EActivateGameOverlayToWebPageMode.k_EActivateGameOverlayToWebPageMode_Default",
        ["NULL"]    = "null",
        ["nullptr"] = "null",
    };

    // ─── Entry Point ─────────────────────────────────────────────────────────────

    public static async Task GenerateAsync(SteamworksParser parser, string outputPath, string templatesPath)
    {
        var nativeMethods = new List<string>();

        foreach (var f in parser.Files)
            await ParseFile(f, parser.Typedefs, nativeMethods, outputPath);

        string templateContent = await File.ReadAllTextAsync(Path.Combine(templatesPath, "nativemethods.txt"));

        var sb = new StringBuilder();
        sb.Append(templateContent);
        foreach (var line in nativeMethods)
            sb.AppendLine(line);
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine();

        await File.WriteAllTextAsync(Path.Combine(outputPath, "NativeMethods.cs"), sb.ToString(), Encoding.UTF8);
    }

    // ─── Per-File ────────────────────────────────────────────────────────────────

    private static async Task ParseFile(SteamFile f, List<Typedef> typedefs, List<string> nativeMethods, string outputPath)
    {
        if (SkippedFiles.Contains(f.Name))
            return;

        var output = new List<string>();

        foreach (var iface in f.Interfaces)
            ParseInterface(f, iface, typedefs, nativeMethods, output);

        if (output.Count == 0)
            return;

        var sb = new StringBuilder();
        if (IsNetworkingFile(f.Name))
            sb.AppendLine("#define STEAMNETWORKINGSOCKETS_ENABLE_SDR");
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftlyS2.Shared.SteamAPI {");
        foreach (var line in output)
            sb.AppendLine(line);
        sb.AppendLine("}");
        sb.AppendLine();

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, Path.GetFileNameWithoutExtension(f.Name) + ".cs"),
            sb.ToString(), Encoding.UTF8);
    }

    private static bool IsNetworkingFile(string name) =>
        name is "isteamnetworkingutils.h" or "isteamnetworkingsockets.h"
            or "isteamgameservernetworkingutils.h" or "isteamgameservernetworkingsockets.h";

    // ─── Per-Interface ────────────────────────────────────────────────────────────

    private static void ParseInterface(SteamFile f, Interface iface, List<Typedef> typedefs,
        List<string> nativeMethods, List<string> output)
    {
        if (SkippedInterfaces.Contains(iface.Name))
            return;

        bool bGameServerVersion = iface.Name.Contains("GameServer")
            && iface.Name != "ISteamGameServer"
            && iface.Name != "ISteamGameServerStats";

        bool isClientInterface = !iface.Name.Contains("GameServer");

        if (!isClientInterface)
            output.Add($"\tpublic static class {iface.Name[1..]} {{");

        if (!bGameServerVersion)
            nativeMethods.Add($"#region {iface.Name[1..]}");

        string? lastIfStatement = null;

        foreach (var func in iface.Functions)
        {
            string? funcIf = func.IfStatements.Count > 0 ? func.IfStatements[0] : null;

            if (funcIf != lastIfStatement)
            {
                if (lastIfStatement != null)
                {
                    // Close previous #if block (replace trailing blank line)
                    if (!bGameServerVersion && nativeMethods.Count > 0)
                        nativeMethods[^1] = "#endif";
                    if (!isClientInterface && output.Count > 0)
                        output[^1] = "#endif";
                    lastIfStatement = null;

                    if (funcIf != null)
                    {
                        var directive = "#if " + CleanIfStatement(funcIf);
                        if (!bGameServerVersion) nativeMethods.Add(directive);
                        if (!isClientInterface) output.Add(directive);
                        lastIfStatement = funcIf;
                    }
                }
                else if (funcIf != null)
                {
                    var directive = "#if " + CleanIfStatement(funcIf);
                    if (!bGameServerVersion && nativeMethods.Count > 0)
                        nativeMethods[^1] = directive;
                    if (!isClientInterface && output.Count > 0)
                        output[^1] = directive;
                    lastIfStatement = funcIf;
                }
            }

            if (func.Private)
                continue;

            ParseFunc(f, iface, func, typedefs, isClientInterface, bGameServerVersion, nativeMethods, output);
        }

        // Remove trailing blank line appended by last ParseFunc call
        if (!bGameServerVersion && nativeMethods.Count > 0)
            nativeMethods.RemoveAt(nativeMethods.Count - 1);
        if (!isClientInterface && output.Count > 0)
            output.RemoveAt(output.Count - 1);

        if (lastIfStatement != null)
        {
            if (!bGameServerVersion) nativeMethods.Add("#endif");
            if (!isClientInterface) output.Add("#endif");
        }

        if (!bGameServerVersion) nativeMethods.Add("#endregion");
        if (!isClientInterface) output.Add("\t}");
    }

    private static string CleanIfStatement(string stmt) =>
        stmt.Replace("defined(", "").Replace(")", "");

    // ─── Per-Function ─────────────────────────────────────────────────────────────

    private static void ParseFunc(SteamFile f, Interface iface, Function func, List<Typedef> typedefs,
        bool isClientInterface, bool bGameServerVersion, List<string> nativeMethods, List<string> output)
    {
        string strEntryPoint = iface.Name + '_' + func.Name;
        foreach (var attr in func.Attributes)
        {
            if (attr.Name == "STEAM_FLAT_NAME") { strEntryPoint = iface.Name + '_' + attr.Value; break; }
        }

        // ── Resolve return type ──────────────────────────────────────────────────
        string? wrapperReturnType = null;
        string strCast = "";
        string returnType = func.ReturnType;

        if (SpecialReturnTypeDict.TryGetValue(strEntryPoint, out var specialRet))
            returnType = specialRet;

        foreach (var td in typedefs)
        {
            if (td.Name == returnType)
            {
                if (!SkippedTypedefs.Contains(td.Name))
                {
                    wrapperReturnType = returnType;
                    strCast = $"({returnType})";
                    returnType = td.Type;
                }
                break;
            }
        }

        returnType = TypeDict.GetValueOrDefault(returnType, returnType);
        returnType = TypeDict.GetValueOrDefault(func.ReturnType, returnType);
        returnType = ReturnTypeDict.GetValueOrDefault(func.ReturnType, returnType);
        wrapperReturnType ??= returnType;

        // ── Parse args ──────────────────────────────────────────────────────────
        var parsed = ParseArgs(strEntryPoint, func.Args, typedefs);

        // ── DllImport (not for GameServer re-exports) ────────────────────────────
        if (!bGameServerVersion)
        {
            nativeMethods.Add($"\t\t[DllImport(NativeLibraryName, EntryPoint = \"SteamAPI_{strEntryPoint}\", CallingConvention = CallingConvention.Cdecl)]");
            if (returnType == "bool")
                nativeMethods.Add("\t\t[return: MarshalAs(UnmanagedType.I1)]");
            nativeMethods.Add($"\t\tpublic static extern {returnType} {strEntryPoint}({parsed.PInvokeArgs});");
            nativeMethods.Add("");
        }

        if (isClientInterface)
            return;

        // ── Wrapper method body ──────────────────────────────────────────────────
        var body = new List<string>();

        body.Add(iface.Name.Contains("GameServer")
            ? "\t\t\tInteropHelp.TestIfAvailableGameServer();"
            : "\t\t\tInteropHelp.TestIfAvailableClient();");

        foreach (var (argName, argSize) in parsed.ArgsWithExplicitCount)
        {
            string sizeRef = parsed.ArgNames.Contains(argSize, StringComparison.Ordinal)
                ? argSize
                : "Constants." + argSize;
            body.Add($"\t\t\tif ({argName} != null && {argName}.Length != {sizeRef}) {{");
            body.Add($"\t\t\t\tthrow new System.ArgumentException(\"{argName} must be the same size as {sizeRef}!\");");
            body.Add("\t\t\t}");
        }

        string strReturnable = func.ReturnType == "void" ? "" : "return ";
        string argNamesFinal = parsed.ArgNames;

        if (func.ReturnType is "const char *" or "const char*")
        {
            wrapperReturnType = "string";
            strReturnable += "InteropHelp.PtrToStringUTF8(";
            argNamesFinal += ")";
        }
        else if (func.ReturnType == "gameserveritem_t *")
        {
            wrapperReturnType = "gameserveritem_t";
            strReturnable += "(gameserveritem_t)Marshal.PtrToStructure(";
            argNamesFinal += "), typeof(gameserveritem_t)";
        }
        else if (func.ReturnType == "CSteamID")
        {
            wrapperReturnType = "CSteamID";
            strReturnable += "(CSteamID)";
        }

        // Allocate out-string buffers
        if (parsed.OutStringArgs.Count > 0)
        {
            if (returnType != "void")
                strReturnable = returnType + " ret = ";

            for (int i = 0; i < parsed.OutStringArgs.Count; i++)
            {
                var a = parsed.OutStringArgs[i];
                if (parsed.OutStringSize.Count == 0)
                {
                    body.Add($"\t\t\tIntPtr {a}2;");
                    continue;
                }
                string cast = parsed.OutStringSize[i].Type != "int" ? "(int)" : "";
                body.Add($"\t\t\tIntPtr {a}2 = Marshal.AllocHGlobal({cast}{parsed.OutStringSize[i].Name});");
            }
        }

        string indent = "\t\t\t";
        if (parsed.StringArgs.Count > 0)
        {
            indent += "\t";
            foreach (var a in parsed.StringArgs)
                body.Add($"\t\t\tusing (var {a}2 = new InteropHelp.UTF8StringHandle({a}))");
            body[^1] += " {";
        }

        // Native call entry point (GameServer re-exports strip "GameServer")
        string nativeEntry = bGameServerVersion
            ? iface.Name.Replace("GameServer", "") + '_' + func.Name
            : strEntryPoint;
        if (bGameServerVersion)
        {
            foreach (var attr in func.Attributes)
            {
                if (attr.Name == "STEAM_FLAT_NAME")
                {
                    nativeEntry = iface.Name.Replace("GameServer", "") + '_' + attr.Value;
                    break;
                }
            }
        }

        body.Add($"{indent}{strReturnable}{strCast}NativeMethods.{nativeEntry}({argNamesFinal});");

        if (parsed.OutStringArgs.Count > 0)
        {
            string retcmp = SpecialOutStringRetCmp.GetValueOrDefault(strEntryPoint, returnType switch
            {
                "bool" => "ret",
                "int"  => "ret != -1",
                _      => "ret != 0",
            });

            foreach (var a in parsed.OutStringArgs)
            {
                if (returnType == "void")
                    body.Add($"{indent}{a} = InteropHelp.PtrToStringUTF8({a}2);");
                else
                    body.Add($"{indent}{a} = {retcmp} ? InteropHelp.PtrToStringUTF8({a}2) : null;");

                if (strEntryPoint != "ISteamRemoteStorage_GetUGCDetails")
                    body.Add($"{indent}Marshal.FreeHGlobal({a}2);");
            }

            if (returnType != "void")
                body.Add($"{indent}return ret;");
        }

        if (parsed.StringArgs.Count > 0)
            body.Add("\t\t\t}");

        // XML doc
        var comments = new List<string>(func.Comments);
        if (func.LineComment is { } lc && !string.IsNullOrEmpty(lc))
            comments.Add(lc);

        if (comments.Count > 0)
        {
            output.Add("\t\t/// <summary>");
            foreach (var c in comments)
            {
                var esc = c.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                if (!string.IsNullOrEmpty(esc))
                    output.Add($"\t\t/// <para>{esc}</para>");
            }
            output.Add("\t\t/// </summary>");
        }

        string methodName = func.Name.TrimEnd('0');
        output.Add($"\t\tpublic static {wrapperReturnType} {methodName}({parsed.WrapperArgs}) {{");
        output.AddRange(body);
        output.Add("\t\t}");
        output.Add("");
    }

    // ─── Args ─────────────────────────────────────────────────────────────────────

    private static ParsedArgs ParseArgs(string strEntryPoint, List<Arg> args, List<Typedef> typedefs)
    {
        // Context pointer as first real argument
        string ifaceName = strEntryPoint.Substring(1, strEntryPoint.IndexOf('_') - 1);
        bool hasGameServer = ifaceName.Contains("GameServer");
        if (hasGameServer && ifaceName != "SteamGameServer" && ifaceName != "SteamGameServerStats")
            ifaceName = ifaceName.Replace("GameServer", "");
        string ctx = hasGameServer ? "CSteamGameServerAPIContext" : "CSteamAPIContext";

        string pinvokeArgs = "IntPtr instancePtr, ";
        string wrapperArgs = "";
        string argNames = $"{ctx}.Get{ifaceName}(), ";
        var stringArgs    = new List<string>();
        var outStringArgs = new List<string>();
        var outStringSize = new List<Arg>();
        var argsWithExplicitCount = new Dictionary<string, string>();

        bool getSize = false;
        int pendingOutStrings = 0;

        foreach (var arg in args)
        {
            // ── Determine PInvoke arg type ─────────────────────────────────────
            string argType = TypeDict.GetValueOrDefault(arg.Type, arg.Type);
            if (argType.StartsWith("const ", StringComparison.Ordinal))
                argType = argType[6..];
            if (argType.EndsWith("*"))
            {
                string potential = arg.Type.TrimEnd('*', ' ');
                if (potential.StartsWith("const ")) potential = potential[6..];
                potential = potential.Trim();
                argType = "out " + TypeDict.GetValueOrDefault(potential, potential);
            }

            // Special override per entry-point
            if (SpecialArgsDict.TryGetValue(strEntryPoint, out var specArgs) && specArgs.TryGetValue(arg.Name, out var specType))
                argType = specType;

            // Attribute-driven array conversion
            if (arg.Attribute is { } attr)
            {
                if (attr.Name is "STEAM_OUT_ARRAY" or "STEAM_OUT_ARRAY_CALL" or "STEAM_OUT_ARRAY_COUNT"
                    or "STEAM_ARRAY_COUNT" or "STEAM_ARRAY_COUNT_D")
                {
                    string potential = arg.Type.TrimEnd('*', ' ');
                    argType = TypeDict.GetValueOrDefault(potential, potential) + "[]";
                }

                if (attr.Name == "STEAM_OUT_ARRAY_COUNT")
                {
                    string attrVal = FixedAttributeValues
                        .GetValueOrDefault(strEntryPoint, [])
                        .GetValueOrDefault(arg.Name, [])
                        .GetValueOrDefault(attr.Name, attr.Value);
                    int comma = attrVal.IndexOf(',');
                    argsWithExplicitCount[arg.Name] = comma > 0 ? attrVal[..comma] : attrVal;
                }
            }

            // MatchMakingKeyValuePair_t ** hack
            if (arg.Type == "MatchMakingKeyValuePair_t **")
                argType = "IntPtr";

            // Marshal attributes for PInvoke
            string pinvokeType = argType.EndsWith("[]") && argType != "byte[]"
                ? "[In, Out] " + argType
                : argType == "bool"
                    ? "[MarshalAs(UnmanagedType.I1)] " + argType
                    : argType;

            pinvokeArgs += pinvokeType + " " + arg.Name + ", ";

            string cleanType = argType
                .Replace("[In, Out] ", "")
                .Replace("[MarshalAs(UnmanagedType.I1)] ", "");

            // ── Wrapper arg type ───────────────────────────────────────────────
            string wrapperType = WrapperArgsTypeDict.GetValueOrDefault(arg.Type, cleanType);
            if (SpecialWrapperArgsDict.TryGetValue(strEntryPoint, out var specWrapper)
                && specWrapper.TryGetValue(arg.Name, out var specWrapperType))
                wrapperType = specWrapperType;

            if (wrapperType == "InteropHelp.UTF8StringHandle")
                wrapperType = "string";
            else if (arg.Type is "char *" or "char*")
                wrapperType = "out string";

            if (!arg.Name.EndsWith("Deprecated"))
            {
                wrapperArgs += wrapperType + " " + arg.Name;
                if (arg.Default is { } def)
                    wrapperArgs += " = " + ArgDefaultLookup.GetValueOrDefault(def, def);
                wrapperArgs += ", ";
            }

            // ── Build argNames (call site) ─────────────────────────────────────
            if (cleanType.StartsWith("out"))      argNames += "out ";
            else if (wrapperType.StartsWith("ref")) argNames += "ref ";

            if (wrapperType == "System.Collections.Generic.IList<string>")
                argNames += $"new InteropHelp.SteamParamStringArray({arg.Name})";
            else if (wrapperType == "MatchMakingKeyValuePair_t[]")
                argNames += $"new MMKVPMarshaller({arg.Name})";
            else if (wrapperType.EndsWith("Response"))
                argNames += $"(IntPtr){arg.Name}";
            else if (arg.Name.EndsWith("Deprecated"))
                argNames += cleanType == "IntPtr" ? "IntPtr.Zero" : cleanType == "bool" ? "false" : "0";
            else
                argNames += arg.Name;

            if (getSize && wrapperType != "out string")
            {
                getSize = false;
                for (int p = 0; p < pendingOutStrings; p++)
                    outStringSize.Add(arg);
                pendingOutStrings = 0;
            }

            if (wrapperType == "string")
            {
                stringArgs.Add(arg.Name);
                argNames += "2";
            }
            else if (wrapperType == "out string")
            {
                outStringArgs.Add(arg.Name);
                argNames += "2";
                pendingOutStrings++;
                if (strEntryPoint != "ISteamRemoteStorage_GetUGCDetails")
                    getSize = true;
            }

            argNames += ", ";
        }

        return new ParsedArgs(
            pinvokeArgs.TrimEnd(',', ' '),
            wrapperArgs.TrimEnd(',', ' '),
            argNames.TrimEnd(',', ' '),
            stringArgs, outStringArgs, outStringSize, argsWithExplicitCount);
    }

    private sealed record ParsedArgs(
        string PInvokeArgs, string WrapperArgs, string ArgNames,
        List<string> StringArgs, List<string> OutStringArgs,
        List<Arg> OutStringSize, Dictionary<string, string> ArgsWithExplicitCount);
}
