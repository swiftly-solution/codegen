namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class SteamFile(string name)
{
    public string Name { get; set; } = name;
    public List<string> Header { get; set; } = [];
    public List<string> Includes { get; set; } = [];
    public List<Define> Defines { get; set; } = [];
    public List<Constant> Constants { get; set; } = [];
    public List<Enum> Enums { get; set; } = [];
    public List<Struct> Structs { get; set; } = [];
    public List<Struct> Callbacks { get; set; } = [];
    public List<Interface> Interfaces { get; set; } = [];
    public List<Typedef> Typedefs { get; set; } = [];
}
