namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class Struct(string name, List<int> packSize, Comment? c)
{
    public string Name { get; set; } = name;
    public List<int> PackSize { get; set; } = packSize;
    public Comment? C { get; set; } = c;
    public List<StructField> Fields { get; set; } = [];
    public string? CallbackId { get; set; }
    public Comment? EndComments { get; set; }
}
