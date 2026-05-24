namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Constant(string name, string value, string type, Comment? c)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
    public string Type { get; set; } = type;
    public Comment? C { get; set; } = c;
}
