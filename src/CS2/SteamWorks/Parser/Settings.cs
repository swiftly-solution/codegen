namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public static class ParserSettings
{
    public static bool WarnUTF8Bom { get; set; } = false;
    public static bool WarnIncludeGuardName { get; set; } = false;
    public static bool WarnSpacing { get; set; } = false;
    public static bool PrintUnusedDefines { get; set; } = false;
    public static bool PrintSkippedTypedefs { get; set; } = false;
    public static bool FakeGameserverInterfaces { get; set; } = true;
}
