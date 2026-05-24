namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Arg
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Default { get; set; }
    public ArgAttribute? Attribute { get; set; }
}
