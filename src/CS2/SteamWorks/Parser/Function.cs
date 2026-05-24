namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Function
{
    public string Name { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public List<Arg> Args { get; set; } = [];
    public List<string> IfStatements { get; set; } = [];
    public List<string> Comments { get; set; } = [];
    public string? LineComment { get; set; }
    public List<FunctionAttribute> Attributes { get; set; } = [];
    public bool Private { get; set; }
}
