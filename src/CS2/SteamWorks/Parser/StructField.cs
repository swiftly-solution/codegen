namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class StructField(string name, string type, string? arraySize, Comment? c)
{
    public string Name { get; set; } = name;
    public string Type { get; set; } = type;
    public string? ArraySize { get; set; } = arraySize;
    public Comment? C { get; set; } = c;
}
