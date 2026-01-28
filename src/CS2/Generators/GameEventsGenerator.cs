using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using WordNinjaSharp;
using WordNinjaSharp.App;

namespace SwiftlyS2.Codegen.CS2.Generators;

/// <summary>
/// Generator for game events
/// </summary>
public class GameEvents : BaseGenerator
{
    private static readonly HttpClient httpClient = new();

    private static readonly Dictionary<string, (string CsType, string Accessor, bool CanSet, string? CastKind)> TypeMap = new()
    {
        { "string", ("string", "String", true, null) },
        { "bool", ("bool", "Bool", true, null) },
        { "byte", ("byte", "Int", true, "byte") },
        { "short", ("short", "Int", true, "short") },
        { "long", ("int", "Int", true, null) },
        { "int", ("int", "Int", true, null) },
        { "float", ("float", "Float", true, null) },
        { "uint64", ("ulong", "Uint64", true, null) },
        { "player_controller", ("int", "PlayerSlot", true, null) },
        { "player_controller_and_pawn", ("int", "PlayerSlot", true, null) },
        { "player_pawn", ("int", "PawnEntityIndex", false, null) },
        { "ehandle", ("nint", "Ptr", true, null) }
    };

    private static readonly HashSet<string> SkipTypes = new() { "none", "local" };

    private static readonly Regex EventNameRegex = new(@"^\s*""([^""]+)""");
    private static readonly Regex FieldRegex = new(@"^\s*""([^""]+)""\s+""([^""]+)""(?:\s*//\s*(.*))?\s*$");

    private readonly List<uint> hashes = new();

    /// <summary>
    /// Initializes a new instance of the GameEvents generator
    /// </summary>
    /// <param name="dataPath">Optional custom path to the game events folder</param>
    public GameEvents(string? dataPath = null)
    {
        DataPath = dataPath;
    }

    /// <inheritdoc />
    public override string Name => "Game Events";

    /// <inheritdoc />
    public override string OutputPath => Entrypoint.GenerateOutputPath("src/SwiftlyS2.Generated/GameEvents");

    /// <inheritdoc />
    public override async Task<GeneratorResult> GenerateFilesAsync()
    {
        try
        {
            Progress.Report("Initializing game events generation...");
            var gameEventsDir = DataPath ?? Path.Combine(Entrypoint.ProjectRootPath, "data", "gameevents");
            Directory.CreateDirectory(gameEventsDir);

            Progress.Report("Downloading game events files...");
            await DownloadGameEventsAsync(gameEventsDir);

            Progress.Report("Parsing game events...");
            var allEvents = await ParseAllGameEventsAsync(gameEventsDir);
            Progress.Report($"Parsed {allEvents.Count} game event(s)");

            var interfacesDir = Path.Combine(OutputPath, "Interfaces");
            var classesDir = Path.Combine(OutputPath, "Classes");

            Progress.Report("Preparing output directories...");
            if (Directory.Exists(interfacesDir))
                Directory.Delete(interfacesDir, true);
            if (Directory.Exists(classesDir))
                Directory.Delete(classesDir, true);

            Directory.CreateDirectory(interfacesDir);
            Directory.CreateDirectory(classesDir);

            int count = 0;
            foreach (var ev in allEvents.Values)
            {
                count++;
                if (count % 10 == 0 || count == allEvents.Count)
                {
                    Progress.Report($"Generating events ({count}/{allEvents.Count})...");
                }
                GenerateInterface(ev, interfacesDir);
                GenerateClass(ev, classesDir);
            }

            Progress.Report($"Successfully generated {allEvents.Count} game event(s)");
            return new GeneratorResult { Success = true };
        }
        catch (Exception ex)
        {
            Progress.Report($"Error: {ex.Message}");
            return new GeneratorResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }

    private async Task DownloadGameEventsAsync(string outputDir)
    {
        var files = new Dictionary<string, string>
        {
            { "core.gameevents", "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/core/pak01_dir/resource/core.gameevents" },
            { "game.gameevents", "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/pak01_dir/resource/game.gameevents" },
            { "mod.gameevents", "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/pak01_dir/resource/mod.gameevents" }
        };

        int count = 0;
        foreach (var (filename, url) in files)
        {
            count++;
            var filePath = Path.Combine(outputDir, filename);
            try
            {
                Progress.Report($"Downloading {filename} ({count}/{files.Count})...");
                var content = await httpClient.GetStringAsync(url);
                await File.WriteAllTextAsync(filePath, content);
            }
            catch (Exception ex)
            {
                if (!File.Exists(filePath))
                {
                    throw new Exception($"Failed to download {filename} and no local file exists: {ex.Message}");
                }
                Progress.Report($"Using cached {filename}");
            }
        }
    }

    private async Task<Dictionary<string, GameEventDef>> ParseAllGameEventsAsync(string dir)
    {
        var allEvents = new Dictionary<string, GameEventDef>();
        var fileOrder = new[] { "core.gameevents", "game.gameevents", "mod.gameevents" };

        foreach (var filename in fileOrder)
        {
            var filePath = Path.Combine(dir, filename);
            if (!File.Exists(filePath))
                continue;

            var events = await ParseGameEventsFileAsync(filePath);
            foreach (var (name, ev) in events)
            {
                if (!allEvents.ContainsKey(name))
                {
                    allEvents[name] = ev;
                }
                else
                {
                    foreach (var (fname, fdef) in ev.Fields)
                    {
                        allEvents[name].AddField(fdef);
                    }
                }
            }
        }

        return allEvents;
    }

    private async Task<Dictionary<string, GameEventDef>> ParseGameEventsFileAsync(string path)
    {
        var events = new Dictionary<string, GameEventDef>();
        var lines = await File.ReadAllLinesAsync(path);

        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            i++;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            var match = EventNameRegex.Match(line);
            if (!match.Success)
                continue;

            var name = match.Groups[1].Value;
            var headerComment = ExtractInlineComment(lines[i - 1]);

            if (lines[i - 1].Contains("{}"))
            {
                var ev = events.GetValueOrDefault(name) ?? new GameEventDef(name);
                if (!string.IsNullOrEmpty(headerComment) && string.IsNullOrEmpty(ev.Comment))
                    ev.Comment = headerComment;
                events[name] = ev;
                continue;
            }

            int j = i;
            while (j < lines.Length && (string.IsNullOrWhiteSpace(lines[j]) || lines[j].Trim().StartsWith("//")))
                j++;

            if (j >= lines.Length)
                continue;

            if (j < lines.Length && lines[j].Trim() == "{}")
            {
                var ev = events.GetValueOrDefault(name) ?? new GameEventDef(name);
                if (!string.IsNullOrEmpty(headerComment) && string.IsNullOrEmpty(ev.Comment))
                    ev.Comment = headerComment;
                events[name] = ev;
                i = j + 1;
                continue;
            }

            if (!lines[j].Trim().StartsWith("{"))
                continue;

            i = j + 1;
            ParseEventBlock(lines, ref i, events, name, headerComment);
        }

        return events;
    }

    private void ParseEventBlock(string[] lines, ref int i, Dictionary<string, GameEventDef> events, string? parentName = null, string? parentComment = null)
    {
        int depth = 1;
        GameEventDef? currentEvent = null;

        if (parentName != null)
        {
            currentEvent = events.GetValueOrDefault(parentName) ?? new GameEventDef(parentName);
            if (!string.IsNullOrEmpty(parentComment) && string.IsNullOrEmpty(currentEvent.Comment))
                currentEvent.Comment = parentComment;
        }

        while (i < lines.Length && depth > 0)
        {
            var line = lines[i].Trim();
            i++;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            if (line.StartsWith("}"))
            {
                depth--;
                continue;
            }

            if (line.StartsWith("{"))
            {
                depth++;
                continue;
            }

            var eventMatch = EventNameRegex.Match(line);
            if (eventMatch.Success && !line.Contains("\"\""))
            {
                var evName = eventMatch.Groups[1].Value;
                var evComment = ExtractInlineComment(lines[i - 1]);

                if (lines[i - 1].Contains("{}"))
                {
                    var ev = events.GetValueOrDefault(evName) ?? new GameEventDef(evName);
                    if (!string.IsNullOrEmpty(evComment) && string.IsNullOrEmpty(ev.Comment))
                        ev.Comment = evComment;
                    events[evName] = ev;
                    continue;
                }

                int k = i;
                while (k < lines.Length && (string.IsNullOrWhiteSpace(lines[k]) || lines[k].Trim().StartsWith("//")))
                    k++;

                if (k < lines.Length && lines[k].Trim() == "{}")
                {
                    var ev = events.GetValueOrDefault(evName) ?? new GameEventDef(evName);
                    if (!string.IsNullOrEmpty(evComment) && string.IsNullOrEmpty(ev.Comment))
                        ev.Comment = evComment;
                    events[evName] = ev;
                    i = k + 1;
                    continue;
                }

                if (k < lines.Length && lines[k].Trim().StartsWith("{"))
                {
                    i = k + 1;
                    ParseEventBlock(lines, ref i, events, evName, evComment);
                    continue;
                }
            }

            var fieldMatch = FieldRegex.Match(line);
            if (fieldMatch.Success && currentEvent != null)
            {
                var fieldName = fieldMatch.Groups[1].Value;
                var fieldType = fieldMatch.Groups[2].Value.ToLower();
                var fieldComment = fieldMatch.Groups.Count > 3 ? fieldMatch.Groups[3].Value : "";

                if (SkipTypes.Contains(fieldType) || fieldType == "1" || fieldType == "0")
                    continue;

                if (fieldType == "uint64_t")
                    fieldType = "uint64";
                if (fieldType == "ehandle_t")
                    fieldType = "ehandle";

                currentEvent.AddField(new EventField(fieldName, fieldType, fieldComment));
            }
        }

        if (currentEvent != null && parentName != null)
        {
            events[parentName] = currentEvent;
        }
    }

    private string ExtractInlineComment(string line)
    {
        var idx = line.IndexOf("//");
        return idx >= 0 ? line.Substring(idx + 2).Trim() : "";
    }

    private void GenerateInterface(GameEventDef ev, string outputDir)
    {
        var baseTypeName = ToPascalCase(ev.Name);
        var typeName = $"Event{baseTypeName}";
        var writer = new CodeWriter();

        writer.AddUsings(
            "SwiftlyS2.Shared.SchemaDefinitions",
            "SwiftlyS2.Shared.GameEvents",
            "SwiftlyS2.Core.GameEventDefinitions",
            "SwiftlyS2.Shared.Players"
        );
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Shared.GameEventDefinitions;");
        writer.AddLine();

        RenderHeaderComment(writer, ev);
        writer.AddBlock($"public interface {typeName} : IGameEvent<{typeName}>", () =>
        {
            writer.AddLine();
            writer.AddLine($"static {typeName} IGameEvent<{typeName}>.Create(nint address) => new {typeName}Impl(address);");
            writer.AddLine();
            writer.AddLine($"static string IGameEvent<{typeName}>.GetName() => \"{ev.Name}\";");
            writer.AddLine();

            var hash = Fnv1a32(ev.Name);
            if (hashes.Contains(hash))
            {
                Console.WriteLine($"WARNING: Hash collision detected for event: {ev.Name}");
            }
            hashes.Add(hash);
            writer.AddLine($"static uint IGameEvent<{typeName}>.GetHash() => 0x{hash:X8}u;");

            RenderInterfaceFields(writer, ev);
        });

        var filePath = Path.Combine(outputDir, $"{typeName}.cs");
        File.WriteAllText(filePath, writer.GetCode() + "\n");
    }

    private void GenerateClass(GameEventDef ev, string outputDir)
    {
        var baseTypeName = ToPascalCase(ev.Name);
        var typeName = $"Event{baseTypeName}";
        var writer = new CodeWriter();

        writer.AddUsings(
            "SwiftlyS2.Core.GameEvents",
            "SwiftlyS2.Shared.GameEvents",
            "SwiftlyS2.Shared.SchemaDefinitions",
            "SwiftlyS2.Shared.GameEventDefinitions",
            "SwiftlyS2.Shared.Players"
        );
        writer.AddLine();
        writer.AddLine("namespace SwiftlyS2.Core.GameEventDefinitions;");
        writer.AddLine();
        writer.AddLine("// generated");

        RenderHeaderComment(writer, ev);
        writer.AddBlock($"internal class {typeName}Impl : GameEvent<{typeName}>, {typeName}", () =>
        {
            writer.AddLine();
            writer.AddBlock($"public {typeName}Impl(nint address) : base(address)", () => { });

            RenderClassFields(writer, ev);
        });

        var filePath = Path.Combine(outputDir, $"{typeName}Impl.cs");
        File.WriteAllText(filePath, writer.GetCode() + "\n");
    }

    private void RenderHeaderComment(CodeWriter writer, GameEventDef ev)
    {
        writer.AddLine("/// <summary>");
        writer.AddLine($"/// Event \"{ev.Name}\"");
        if (!string.IsNullOrEmpty(ev.Comment))
        {
            writer.AddLine($"/// {ev.Comment}");
        }
        writer.AddLine("/// </summary>");
    }

    private void RenderInterfaceFields(CodeWriter writer, GameEventDef ev)
    {
        var usedPropNames = new Dictionary<string, int>();

        foreach (var (fname, fdef) in ev.Fields)
        {
            var ftype = fdef.TypeName;

            if (IsPlayerType(ftype, fname))
            {
                RenderInterfacePlayerField(writer, fname, fdef, ftype, usedPropNames);
                continue;
            }

            if (!TypeMap.TryGetValue(ftype, out var typeInfo))
                continue;

            var (csType, _, canSet, _) = typeInfo;
            var propName = GetUniquePropName(ToPropertyName(fname), usedPropNames);

            writer.AddLine();
            writer.AddLine("/// <summary>");
            if (!string.IsNullOrEmpty(fdef.Comment))
            {
                writer.AddLine($"/// {fdef.Comment}");
                writer.AddLine("/// <br/>");
            }
            writer.AddLine($"/// type: {ftype}");
            writer.AddLine("/// </summary>");
            writer.AddLine($"{csType} {propName} {{ get; {(canSet ? "set; " : "")}}}");
        }
    }

    private void RenderInterfacePlayerField(CodeWriter writer, string fname, EventField fdef, string ftype, Dictionary<string, int> usedPropNames)
    {
        var baseProp = ToPropertyName(fname);

        // Controller property
        var propNameCtrl = GetUniquePropName($"{baseProp}Controller", usedPropNames);
        writer.AddLine();
        writer.AddLine("/// <summary>");
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"/// {fdef.Comment}");
        writer.AddLine("/// <br/>");
        writer.AddLine($"/// type: {ftype}");
        writer.AddLine("/// </summary>");
        writer.AddLine($"CCSPlayerController {propNameCtrl} {{ get; }}");

        // Pawn property
        var propNamePawn = GetUniquePropName($"{baseProp}Pawn", usedPropNames);
        writer.AddLine();
        writer.AddLine("/// <summary>");
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"/// {fdef.Comment}");
        writer.AddLine("/// <br/>");
        writer.AddLine($"/// type: {ftype}");
        writer.AddLine("/// </summary>");
        writer.AddLine($"CCSPlayerPawn {propNamePawn} {{ get; }}");

        // Player property
        var propNamePlayer = GetUniquePropName($"{baseProp}Player", usedPropNames);
        writer.AddLine();
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"// {fdef.Comment}");
        writer.AddLine($"public IPlayer? {propNamePlayer}");
        writer.AddLine($"{{ get => Accessor.GetPlayer(\"{fname}\"); }}");

        // Raw int property
        var propNameRaw = GetUniquePropName(baseProp, usedPropNames);
        writer.AddLine();
        writer.AddLine("/// <summary>");
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"/// {fdef.Comment}");
        writer.AddLine("/// <br/>");
        writer.AddLine($"/// type: {ftype}");
        writer.AddLine("/// </summary>");
        writer.AddLine($"int {propNameRaw} {{ get; set; }}");
    }

    private void RenderClassFields(CodeWriter writer, GameEventDef ev)
    {
        var usedPropNames = new Dictionary<string, int>();

        foreach (var (fname, fdef) in ev.Fields)
        {
            var ftype = fdef.TypeName;

            if (IsPlayerType(ftype, fname))
            {
                RenderClassPlayerField(writer, fname, fdef, usedPropNames);
                continue;
            }

            if (!TypeMap.TryGetValue(ftype, out var typeInfo))
                continue;

            var (csType, accessor, canSet, castKind) = typeInfo;
            var propName = GetUniquePropName(ToPropertyName(fname), usedPropNames);

            var (getter, setter) = BuildAccessors(fname, accessor, castKind);

            writer.AddLine();
            if (!string.IsNullOrEmpty(fdef.Comment))
                writer.AddLine($"// {fdef.Comment}");
            writer.AddLine($"public {csType} {propName}");
            if (canSet && setter != null)
                writer.AddLine($"{{ get => {getter}; set => {setter}; }}");
            else
                writer.AddLine($"{{ get => {getter}; }}");
        }
    }

    private void RenderClassPlayerField(CodeWriter writer, string fname, EventField fdef, Dictionary<string, int> usedPropNames)
    {
        var baseProp = ToPropertyName(fname);

        // Controller
        var propNameCtrl = GetUniquePropName($"{baseProp}Controller", usedPropNames);
        writer.AddLine();
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"// {fdef.Comment}");
        writer.AddLine($"public CCSPlayerController {propNameCtrl}");
        writer.AddLine($"{{ get => Accessor.GetPlayerController(\"{fname}\"); }}");

        // Pawn
        var propNamePawn = GetUniquePropName($"{baseProp}Pawn", usedPropNames);
        writer.AddLine();
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"// {fdef.Comment}");
        writer.AddLine($"public CCSPlayerPawn {propNamePawn}");
        writer.AddLine($"{{ get => Accessor.GetPlayerPawn(\"{fname}\"); }}");

        // Player
        var propNamePlayer = GetUniquePropName($"{baseProp}Player", usedPropNames);
        writer.AddLine();
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"// {fdef.Comment}");
        writer.AddLine($"public IPlayer? {propNamePlayer}");
        writer.AddLine($"{{ get => Accessor.GetPlayer(\"{fname}\"); }}");

        // Raw int
        var propNameRaw = GetUniquePropName(baseProp, usedPropNames);
        writer.AddLine();
        if (!string.IsNullOrEmpty(fdef.Comment))
            writer.AddLine($"// {fdef.Comment}");
        writer.AddLine($"public int {propNameRaw}");
        writer.AddLine($"{{ get => Accessor.GetInt32(\"{fname}\"); set => Accessor.SetInt32(\"{fname}\", value); }}");
    }

    private (string getter, string? setter) BuildAccessors(string fname, string accessor, string? castKind)
    {
        return accessor switch
        {
            "String" => ($"Accessor.GetString(\"{fname}\")", $"Accessor.SetString(\"{fname}\", value)"),
            "Bool" => ($"Accessor.GetBool(\"{fname}\")", $"Accessor.SetBool(\"{fname}\", value)"),
            "Int" when castKind == "byte" => ($"(byte)Accessor.GetInt32(\"{fname}\")", $"Accessor.SetInt32(\"{fname}\", value)"),
            "Int" when castKind == "short" => ($"(short)Accessor.GetInt32(\"{fname}\")", $"Accessor.SetInt32(\"{fname}\", value)"),
            "Int" => ($"Accessor.GetInt32(\"{fname}\")", $"Accessor.SetInt32(\"{fname}\", value)"),
            "Uint64" => ($"Accessor.GetUInt64(\"{fname}\")", $"Accessor.SetUInt64(\"{fname}\", value)"),
            "Float" => ($"Accessor.GetFloat(\"{fname}\")", $"Accessor.SetFloat(\"{fname}\", value)"),
            "PlayerSlot" => ($"Accessor.GetPlayerSlot(\"{fname}\")", $"Accessor.SetPlayerSlot(\"{fname}\", value)"),
            "PawnEntityIndex" => ($"Accessor.GetPawnEntityIndex(\"{fname}\")", null),
            "Ptr" => ($"Accessor.GetPtr(\"{fname}\")", $"Accessor.SetPtr(\"{fname}\", value)"),
            _ => ("", null)
        };
    }

    private bool IsPlayerType(string ftype, string fname) =>
        ftype == "player_controller" || ftype == "player_controller_and_pawn" || fname.ToLower() == "userid";

    private string GetUniquePropName(string baseName, Dictionary<string, int> used)
    {
        if (!used.ContainsKey(baseName))
        {
            used[baseName] = 1;
            return baseName;
        }

        used[baseName]++;
        return $"{baseName}{used[baseName]}";
    }

    /// <summary>
    /// Property name conversion: enforce PascalCase; split concatenated lowercase sequences using KNOWN_TOKENS
    /// </summary>
    private string ToPropertyName(string field)
    {
        if (field.ToLower() == "userid")
            return "UserId";

        if (field.Contains("_"))
            return ToPascalCase(field);

        if (Regex.IsMatch(field, @"^[a-z0-9]+$"))
        {
            var tokens = SplitConcatenatedLowercase(field);
            return string.Join("", tokens.Select(ToTitleCase));
        }

        return ToPascalCase(field);
    }

    private string ToTitleCase(string tok)
    {
        if (tok == "id" || tok == "ui" || tok == "ip" || tok == "x" || tok == "y" || tok == "z")
            return tok.ToUpper();
        return char.ToUpper(tok[0]) + tok.Substring(1);
    }

    private List<string> SplitConcatenatedLowercase(string word)
    {
        var s = word.ToLower();

        if (s == "assister")
            return new List<string> { "Assister" };

        var dictionaryPath = Path.Combine(Entrypoint.ProjectRootPath, "data", "wordninja.words.txt.gz");
        var tokens = WordNinja.Split(s, dictionaryPath);
        return tokens.Split(' ').ToList();
    }

    /// <summary>
    /// Convert event or field names to PascalCase identifiers
    /// </summary>
    private string ToPascalCase(string name)
    {
        name = Regex.Replace(name, @"[^0-9a-zA-Z_]", "_");
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "Unnamed";

        var converted = string.Join("", parts.Select(p => char.ToUpper(p[0]) + p.Substring(1)));
        if (char.IsDigit(converted[0]))
            converted = "E" + converted;

        return converted;
    }

    private uint Fnv1a32(string text)
    {
        uint hash = 2166136261;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 16777619;
        }
        return hash;
    }

    internal static Dictionary<string, (string, string, bool, string?)> GetTypeMapForEventDef() =>
        new(TypeMap);
}

internal class EventField
{
    public string Name { get; }
    public string TypeName { get; set; }
    public string Comment { get; set; }

    public EventField(string name, string typeName, string comment)
    {
        Name = name;
        TypeName = typeName;
        Comment = comment ?? "";
    }
}

internal class GameEventDef
{
    public string Name { get; }
    public Dictionary<string, EventField> Fields { get; } = new();
    public string Comment { get; set; }

    public GameEventDef(string name, string? comment = null)
    {
        Name = name;
        Comment = comment ?? "";
    }

    public void AddField(EventField field)
    {
        if (Fields.ContainsKey(field.Name))
        {
            var existing = Fields[field.Name];
            if (string.IsNullOrEmpty(existing.Comment) && !string.IsNullOrEmpty(field.Comment))
                existing.Comment = field.Comment;

            if (!TypeMap.ContainsKey(existing.TypeName) && TypeMap.ContainsKey(field.TypeName))
                existing.TypeName = field.TypeName;
        }
        else
        {
            Fields[field.Name] = field;
        }
    }

    private static readonly Dictionary<string, (string, string, bool, string?)> TypeMap = [];
}
