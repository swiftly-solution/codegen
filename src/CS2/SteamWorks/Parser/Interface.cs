namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Interface
{
    public string Name { get; set; } = "";
    public List<Function> Functions { get; set; } = [];
    public Comment? C { get; set; }
}
