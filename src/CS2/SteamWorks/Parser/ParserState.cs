namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class ParserState(SteamFile file)
{
    public SteamFile F { get; set; } = file;
    public List<string> Lines { get; set; } = [];
    public string Line { get; set; } = "";
    public string OriginalLine { get; set; } = "";
    public string[] LineSplit { get; set; } = [];
    public int LineNum { get; set; }

    public List<object> RawComments { get; set; } = [];
    public List<string> Comments { get; set; } = [];
    public string? RawLineComment { get; set; }
    public string? LineComment { get; set; }

    public List<string> IfStatements { get; set; } = [];
    public List<int> PackSize { get; set; } = [];
    public int FuncState { get; set; }
    public int ScopeDepth { get; set; }

    public Interface? Interface { get; set; }
    public Function? Function { get; set; }
    public Enum? Enum { get; set; }
    public Struct? Struct { get; set; }
    public Struct? CallbackMacro { get; set; }

    public bool InHeader { get; set; } = true;
    public bool InMultilineComment { get; set; }
    public bool InMultilineMacro { get; set; }
    public bool InPrivate { get; set; }
    public string? CallbackId { get; set; }
    public List<FunctionAttribute> FunctionAttributes { get; set; } = [];
}
