namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Typedef(string name, string type, string fileName, Comment? c = null)
{
    public string Name { get; set; } = name;
    public string Type { get; set; } = type;
    public string FileName { get; set; } = fileName;
    public Comment? C { get; set; } = c;
}
