namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Define(string name, string value, string spacing, Comment? c)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
    public string Spacing { get; set; } = spacing;
    public Comment? C { get; set; } = c;
}
