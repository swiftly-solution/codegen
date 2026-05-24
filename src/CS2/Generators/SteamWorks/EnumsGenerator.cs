using System.Text;
using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators.SteamWorks;

internal static class EnumsGenerator
{
    private static readonly HashSet<string> FlagEnums =
    [
        "EPersonaChange",
        "EFriendFlags",
        "EHTMLKeyModifiers",
        "EControllerHapticLocation",
        "ESteamItemFlags",
        "EChatMemberStateChange",
        "ERemoteStoragePlatform",
        "EItemState",
        "EChatSteamIDInstanceFlags",
        "EMarketNotAllowedReasonFlags",
    ];

    // name -> filename: skip when both match
    private static readonly Dictionary<string, string> SkippedEnums = new()
    {
        ["EGameIDType"]      = "steamclientpublic.h",
        ["EXboxOrigin"]      = "isteamcontroller.h",
        ["ESteamInputType"]  = "isteamcontroller.h",
    };

    // ordered: first matching substring wins
    private static readonly List<(string From, string To)> ValueConversions =
    [
        ("0xffffffff",                                                          "-1"),
        ("0x80000000",                                                          "-2147483647"),
        ("k_unSteamAccountInstanceMask",                                        "Constants.k_unSteamAccountInstanceMask"),
        ("( 1 << k_ESteamControllerPad_Left | 1 << k_ESteamControllerPad_Right )", "( 1 << ESteamControllerPad.k_ESteamControllerPad_Left | 1 << ESteamControllerPad.k_ESteamControllerPad_Right )"),
        ("( 1 << k_ESteamControllerPad_Left )",                                 "( 1 << ESteamControllerPad.k_ESteamControllerPad_Left )"),
        ("( 1 << k_ESteamControllerPad_Right )",                                "( 1 << ESteamControllerPad.k_ESteamControllerPad_Right )"),
    ];

    public static async Task GenerateAsync(SteamworksParser parser, string outputPath)
    {
        var lines = new List<string>();

        foreach (var f in parser.Files)
        {
            foreach (var e in f.Enums)
            {
                if (e.Name is null)
                    continue;

                if (SkippedEnums.TryGetValue(e.Name, out var skipFile) && skipFile == f.Name)
                    continue;

                WriteRawPreComments(lines, e.C?.RawPreComments, indent: "\t", skipBlankLines: true);

                if (FlagEnums.Contains(e.Name))
                    lines.Add("\t[FlagsAttribute]");

                lines.Add($"\tpublic enum {e.Name} : int {{");

                foreach (var field in e.Fields)
                {
                    WriteRawPreComments(lines, field.C?.RawPreComments, indent: "\t", skipBlankLines: false);

                    var line = "\t\t" + field.Name;

                    if (!string.IsNullOrEmpty(field.Value))
                    {
                        if (field.Value.Contains("<<", StringComparison.Ordinal) && !FlagEnums.Contains(e.Name))
                            Console.WriteLine($"[WARNING] Enum {e.Name} contains '<<' but is not a flag enum - {f.Name}");

                        line += field.Value is "=" or "|"
                            ? " "
                            : field.PreSpacing + "=" + field.PostSpacing;

                        line += ApplyValueConversions(field.Value);
                    }

                    if (field.C?.RawLineComment is { } rawLineComment)
                        line += rawLineComment;

                    lines.Add(line);
                }

                WriteRawPreComments(lines, e.EndComments?.RawPreComments, indent: "\t", skipBlankLines: false);

                lines.Add("\t}");
                lines.Add("");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("using Flags = System.FlagsAttribute;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftlyS2.Shared.SteamAPI {");
        foreach (var line in lines)
            sb.AppendLine(line);
        sb.AppendLine("}");
        sb.AppendLine();

        await File.WriteAllTextAsync(Path.Combine(outputPath, "SteamEnums.cs"), sb.ToString(), Encoding.UTF8);
    }

    private static void WriteRawPreComments(List<string> lines, List<object>? rawComments, string indent, bool skipBlankLines)
    {
        if (rawComments is null)
            return;

        foreach (var comment in rawComments)
        {
            if (comment is BlankLine)
            {
                if (!skipBlankLines)
                    lines.Add("");
            }
            else if (comment is string s)
            {
                lines.Add(indent + s);
            }
        }
    }

    private static string ApplyValueConversions(string value)
    {
        foreach (var (from, to) in ValueConversions)
        {
            if (value.Contains(from, StringComparison.Ordinal))
                return value.Replace(from, to, StringComparison.Ordinal);
        }
        return value;
    }
}
