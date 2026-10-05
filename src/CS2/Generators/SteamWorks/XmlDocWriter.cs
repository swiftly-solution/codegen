using System.Security;
using System.Text.RegularExpressions;
using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators.SteamWorks;

internal static class XmlDocWriter
{
    private static readonly Regex CommentedOutCode = new(@"^[A-Za-z_]\w*\s*=\s*\S+", RegexOptions.Compiled);

    private static readonly Regex DocMarker = new(@"^/+[!<]?", RegexOptions.Compiled);

    private static string CleanText(string raw) => DocMarker.Replace(raw.Trim(), "").Trim();

    public static void Write(List<string> lines, Comment? c, string indent)
    {
        if (c is not null)
            Write(lines, c.PreComments, c.LineComment, indent);
    }

    public static void Write(List<string> lines, List<string> preComments, string? lineComment, string indent)
    {
        var doc = new List<string>();

        foreach (var raw in preComments)
        {
            var text = CleanText(raw);
            if (text.Length == 0 || text.All(ch => ch is '-' or '/' or '=' or '*'))
                continue;

            if (CommentedOutCode.IsMatch(text))
            {
                lines.Add($"{indent}// {text}");
                continue;
            }

            if (text.StartsWith("Purpose:", StringComparison.Ordinal))
                text = text["Purpose:".Length..].Trim();

            doc.Add(text);
        }

        if (lineComment is not null && CleanText(lineComment) is { Length: > 0 } lineText)
            doc.Add(lineText);

        if (doc.Count == 0)
            return;

        lines.Add($"{indent}/// <summary>");
        foreach (var text in doc)
            lines.Add($"{indent}/// <para>{SecurityElement.Escape(text)}</para>");
        lines.Add($"{indent}/// </summary>");
    }
}
