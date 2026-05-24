namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Enum(string? name, Comment? c = null)
{
    public string? Name { get; set; } = name;
    public List<EnumField> Fields { get; set; } = [];
    public Comment? C { get; set; } = c;
    public Comment? EndComments { get; set; }
}
