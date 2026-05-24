namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Comment(List<object> rawPreComments, List<string> preComments, string? rawLineComment, string? lineComment)
{
    public List<object> RawPreComments { get; set; } = rawPreComments;
    public List<string> PreComments { get; set; } = preComments;
    public string? RawLineComment { get; set; } = rawLineComment;
    public string? LineComment { get; set; } = lineComment;
}
