namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class EnumField
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string PreSpacing { get; set; } = " ";
    public string PostSpacing { get; set; } = " ";
    public Comment? C { get; set; }
}
