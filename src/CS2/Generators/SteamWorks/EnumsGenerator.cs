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

                XmlDocWriter.Write(lines, e.C, "\t");

                if (FlagEnums.Contains(e.Name))
                    lines.Add("\t[Flags]");

                lines.Add($"\tpublic enum {e.Name} : int");
                lines.Add("\t{");

                foreach (var field in e.Fields)
                {
                    XmlDocWriter.Write(lines, field.C, "\t\t");

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

                    lines.Add(line);
                }

                WriteRawPreComments(lines, e.EndComments?.RawPreComments, indent: "\t", skipBlankLines: false);

                lines.Add("\t}");
                lines.Add("");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("namespace SwiftlyS2.Shared.SteamAPI;");
        sb.AppendLine();
        foreach (var line in lines)
            sb.AppendLine(line.StartsWith('\t') ? line[1..] : line);

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
