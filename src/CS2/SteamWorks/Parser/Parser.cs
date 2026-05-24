using System.Text;
using System.Text.RegularExpressions;

namespace SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

public class SteamworksParser
{
    public List<SteamFile> Files { get; private set; }
    public List<Typedef> Typedefs { get; private set; }

    public SteamworksParser(string folder)
    {
        Files = Directory.GetFiles(folder, "*.h")
            .Select(Path.GetFileName)
            .Where(f => f != null && !ParserData.SkippedFiles.Contains(f))
            .OrderBy(f => f)
            .Select(f => new SteamFile(f!))
            .ToList();

        Typedefs = [];

        foreach (var f in Files)
        {
            var s = new ParserState(f);
            var filepath = Path.Combine(folder, f.Name);
            s.Lines = [.. File.ReadAllLines(filepath, Encoding.Latin1)];

            if (s.Lines.Count > 0 && s.Lines[0].StartsWith("ï»¿", StringComparison.Ordinal))
            {
                s.Lines[0] = s.Lines[0][3..];
                if (ParserSettings.WarnUTF8Bom)
                    PrintWarning("File contains a UTF8 BOM.", s);
            }

            Parse(s);
        }

        if (ParserSettings.FakeGameserverInterfaces)
        {
            var gameServerFiles = Files.Where(f => ParserData.GameServerInterfaces.Contains(f.Name)).ToList();
            foreach (var f in gameServerFiles)
            {
                var gsF = new SteamFile(f.Name.Replace("isteam", "isteamgameserver", StringComparison.Ordinal));
                gsF.Interfaces = f.Interfaces.Select(i => new Interface
                {
                    Name = i.Name.Replace("ISteam", "ISteamGameServer", StringComparison.Ordinal),
                    Functions = i.Functions,
                    C = i.C
                }).ToList();
                Files.Add(gsF);
            }
        }
    }

    private void Parse(ParserState s)
    {
        for (int lineNum = 0; lineNum < s.Lines.Count; lineNum++)
        {
            s.Line = s.Lines[lineNum];
            s.OriginalLine = s.Lines[lineNum];
            s.LineNum = lineNum;

            s.Line = s.Line.TrimEnd();

            ParseComments(s);

            if (string.IsNullOrEmpty(s.Line))
                continue;

            s.LineSplit = s.Line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (s.InHeader)
                ParseHeader(s);

            if (ParseSkippedLines(s))
            {
                ConsumeComments(s);
                continue;
            }

            ParsePreprocessor(s);
            ParseTypedefs(s);
            ParseConstants(s);
            ParseEnums(s);
            ParseStructs(s);
            ParseCallbackMacros(s);
            ParseInterfaces(s);

            if (string.IsNullOrEmpty(s.Line))
                continue;

            ParseClasses(s);
            ParseScope(s);
        }
    }

    private void ParseComments(ParserState s)
    {
        ParseCommentsMultiline(s);
        ParseCommentsSingleline(s);
        s.Line = s.Line.Trim();
    }

    private void ParseCommentsMultiline(ParserState s)
    {
        int openCount = CountOccurrences(s.Line, "/*");
        int closeCount = CountOccurrences(s.Line, "*/");
        bool multipleQuoteblocks = openCount > 1 || closeCount > 1;

        int openerPos = s.Line.IndexOf("/*", StringComparison.Ordinal);
        int closerPos = s.Line.IndexOf("*/", StringComparison.Ordinal);
        bool hasOpening = openerPos != -1;
        bool hasClosing = closerPos != -1;

        string? strComment = null;

        if (hasOpening)
        {
            if (hasClosing)
            {
                strComment = s.Line[(openerPos + 2)..closerPos];
                s.Line = s.Line[..openerPos] + s.Line[(closerPos + 2)..];
                s.InMultilineComment = false;
            }
            else
            {
                strComment = s.Line[(openerPos + 2)..];
                s.Line = s.Line[..openerPos];
                s.InMultilineComment = true;
            }
        }
        else if (s.InMultilineComment)
        {
            if (hasClosing)
            {
                strComment = s.Line[..closerPos];
                s.Line = s.Line[(closerPos + 2)..];
                s.InMultilineComment = false;
            }
            else
            {
                strComment = s.Line;
                s.Line = "";
            }
        }

        if (strComment != null)
            s.Comments.Add(strComment.TrimEnd());

        if (multipleQuoteblocks)
            ParseCommentsMultiline(s);
    }

    private void ParseCommentsSingleline(ParserState s)
    {
        if (s.LineComment != null)
        {
            s.Comments.Add(s.LineComment);
            s.RawComments.Add(s.RawLineComment!);
            s.RawLineComment = null;
            s.LineComment = null;
        }

        if (string.IsNullOrEmpty(s.Line))
        {
            s.RawComments.Add(new BlankLine());
            return;
        }

        int commentPos = s.Line.IndexOf("//", StringComparison.Ordinal);
        if (commentPos != -1)
        {
            s.LineComment = s.Line[(commentPos + 2)..];
            s.Line = s.Line[..commentPos];

            int origPos = s.OriginalLine.IndexOf("//", StringComparison.Ordinal);
            int whitespace = s.OriginalLine[..origPos].Length - s.OriginalLine[..origPos].TrimEnd().Length;
            s.RawLineComment = s.OriginalLine[(origPos - whitespace)..].TrimEnd();
        }
    }

    private static void ParseHeader(ParserState s)
    {
        if (!string.IsNullOrEmpty(s.Line))
        {
            s.F.Header.AddRange(s.Comments);
            s.Comments.Clear();
            s.InHeader = false;
        }
    }

    private bool ParseSkippedLines(ParserState s)
    {
        if (s.Struct != null && ParserData.SkippedStructs.Contains(s.Struct.Name))
        {
            ParseScope(s);
            if (s.Line == "};" && s.ScopeDepth == 0)
                s.Struct = null;
            return true;
        }

        if (s.IfStatements.Contains("!defined(API_GEN)"))
        {
            if (s.Line.StartsWith("#if", StringComparison.Ordinal))
                s.IfStatements.Add("ugh");
            else if (s.Line.StartsWith("#endif", StringComparison.Ordinal))
                s.IfStatements.RemoveAt(s.IfStatements.Count - 1);
            return true;
        }

        if (s.Line.EndsWith('\\'))
        {
            s.InMultilineMacro = true;
            return true;
        }

        if (s.InMultilineMacro)
        {
            s.InMultilineMacro = false;
            return true;
        }

        foreach (var skip in ParserData.SkippedLines)
        {
            if (s.Line.Contains(skip, StringComparison.Ordinal))
                return true;
        }

        if (s.Interface == null && s.Line.Contains("inline", StringComparison.Ordinal))
            return true;

        return false;
    }

    private void ParsePreprocessor(ParserState s)
    {
        if (!s.Line.StartsWith('#'))
            return;

        if (s.Line.StartsWith("#else", StringComparison.Ordinal))
        {
            var prevIf = s.IfStatements[^1];
            s.IfStatements.RemoveAt(s.IfStatements.Count - 1);
            s.IfStatements.Add($"!({prevIf}) // #else");
        }
        else if (s.Line.StartsWith("#include", StringComparison.Ordinal))
        {
            ConsumeComments(s);
            var inc = s.LineSplit[1];
            s.F.Includes.Add(inc[1..^1]);
        }
        else if (s.Line.StartsWith("#ifdef", StringComparison.Ordinal))
        {
            s.IfStatements.Add($"defined({s.LineSplit[1]})");
        }
        else if (s.Line.StartsWith("#ifndef", StringComparison.Ordinal))
        {
            s.IfStatements.Add($"!defined({s.LineSplit[1]})");
        }
        else if (s.Line.StartsWith("#if", StringComparison.Ordinal))
        {
            s.IfStatements.Add(s.Line[3..].Trim());
        }
        else if (s.Line.StartsWith("#endif", StringComparison.Ordinal))
        {
            if (s.IfStatements.Count > 0)
                s.IfStatements.RemoveAt(s.IfStatements.Count - 1);
        }
        else if (s.Line.StartsWith("#define", StringComparison.Ordinal))
        {
            var comments = ConsumeComments(s);

            if (ParserSettings.WarnIncludeGuardName && s.IfStatements.Count == 0)
            {
                if (s.LineSplit[1] != s.F.Name.ToUpperInvariant().Replace('.', '_'))
                    PrintWarning("Include guard does not match the file name.", s);
            }

            if (s.LineSplit.Length > 2)
            {
                int t1End = s.Line.IndexOf(s.LineSplit[1], StringComparison.Ordinal) + s.LineSplit[1].Length;
                int t2Start = s.Line.IndexOf(s.LineSplit[2], t1End, StringComparison.Ordinal);
                s.F.Defines.Add(new Define(s.LineSplit[1], s.LineSplit[2], s.Line[t1End..t2Start], comments));
            }
            else if (ParserSettings.PrintUnusedDefines)
            {
                Console.WriteLine("Unused Define: " + s.Line);
            }
        }
        else if (s.Line.StartsWith("#pragma pack", StringComparison.Ordinal))
        {
            if (s.Line.Contains("push", StringComparison.Ordinal))
            {
                int comma = s.Line.IndexOf(',');
                var num = s.Line[(comma + 1)..s.Line.LastIndexOf(')')].Trim();
                s.PackSize.Add(int.Parse(num));
            }
            else if (s.Line.Contains("pop", StringComparison.Ordinal))
            {
                if (s.PackSize.Count > 0)
                    s.PackSize.RemoveAt(s.PackSize.Count - 1);
            }
        }
        else if (s.Line.StartsWith("#pragma", StringComparison.Ordinal)) { }
        else if (s.Line.StartsWith("#error", StringComparison.Ordinal)) { }
        else if (s.Line.StartsWith("#warning", StringComparison.Ordinal)) { }
        else if (s.Line.StartsWith("#elif", StringComparison.Ordinal)) { }
        else if (s.Line.StartsWith("#undef", StringComparison.Ordinal)) { }
        else
        {
            PrintUnhandled("Preprocessor", s);
        }
    }

    private void ParseTypedefs(ParserState s)
    {
        if (s.LineSplit.Length == 0 || s.LineSplit[0] != "typedef")
            return;

        var comments = ConsumeComments(s);

        if (s.ScopeDepth > 0)
        {
            if (ParserSettings.PrintSkippedTypedefs)
                Console.WriteLine("Skipped typedef (in class/struct): " + s.Line);
            return;
        }

        if (s.Line.Contains('(') || s.Line.Contains('['))
        {
            if (ParserSettings.PrintSkippedTypedefs)
                Console.WriteLine("Skipped typedef (contains '(' or '['): " + s.Line);
            return;
        }

        if (!s.Line.EndsWith(';'))
        {
            if (ParserSettings.PrintSkippedTypedefs)
                Console.WriteLine("Skipped typedef (no trailing ';'): " + s.Line);
            return;
        }

        string name = s.LineSplit[^1].TrimEnd(';');
        string typee = string.Join(" ", s.LineSplit[1..^1]);
        if (name.StartsWith('*'))
        {
            typee += " *";
            name = name[1..];
        }

        var typedef = new Typedef(name, typee, s.F.Name, comments);
        Typedefs.Add(typedef);
        s.F.Typedefs.Add(typedef);
    }

    private void ParseConstants(ParserState s)
    {
        if (s.LineSplit.Length == 0)
            return;
        if (s.LineSplit[0] != "const" && !s.Line.StartsWith("static const", StringComparison.Ordinal))
            return;
        if (s.ScopeDepth > 1)
            return;

        var comments = ConsumeComments(s);

        if (!s.Line.Contains('='))
            return;

        var m = Regex.Match(s.Line, @".*const\s+(.*)\s+(\w+)\s+=\s+(.*);$");
        if (!m.Success)
            return;

        s.F.Constants.Add(new Constant(m.Groups[2].Value, m.Groups[3].Value, m.Groups[1].Value, comments));
    }

    private void ParseEnums(ParserState s)
    {
        if (s.Enum != null)
        {
            if (s.Line == "{")
                return;

            if (s.Line.EndsWith("};"))
            {
                s.Enum.EndComments = ConsumeComments(s);
                if (s.Enum.Name != null)
                    s.F.Enums.Add(s.Enum);
                s.Enum = null;
                return;
            }

            ParseEnumFields(s);
            return;
        }

        if (s.LineSplit.Length == 0 || s.LineSplit[0] != "enum")
            return;

        var comments = ConsumeComments(s);

        if (s.Line.Contains("};"))
        {
            if (s.Line.Contains(','))
                return;
            if (s.LineSplit.Length > 0 && s.LineSplit[^1] == "\\")
                return;

            if (s.Struct != null)
            {
                var m = Regex.Match(s.Line, @"^enum \{ (.*) = (.*) \};");
                if (m.Success && m.Groups[1].Value == "k_iCallback")
                {
                    s.CallbackId = m.Groups[2].Value;
                    return;
                }
            }

            if (s.LineSplit.Length >= 5)
                s.F.Constants.Add(new Constant(s.LineSplit[2], s.LineSplit[4], "int", comments));
            return;
        }

        s.Enum = s.LineSplit.Length == 1
            ? new Enum(null, comments)
            : new Enum(s.LineSplit[1], comments);
    }

    private void ParseEnumFields(ParserState s)
    {
        var m = Regex.Match(s.Line, @"^(\w+,?)([ \t]*)=?([ \t]*)(.*)$");
        var comments = ConsumeComments(s);

        string value = s.Line.EndsWith('=') ? "=" : (m.Success ? m.Groups[4].Value : "");

        if (s.Enum!.Name == null)
        {
            if (s.Enum.C != null)
            {
                comments.PreComments = s.Enum.C.PreComments;
                s.Enum.C = null;
            }
            s.F.Constants.Add(new Constant(
                m.Success ? m.Groups[1].Value : "",
                value.TrimEnd(','),
                "int",
                comments
            ));
            return;
        }

        var field = new EnumField { Name = m.Success ? m.Groups[1].Value : "" };
        if (!string.IsNullOrEmpty(value))
        {
            field.PreSpacing = m.Success ? m.Groups[2].Value : " ";
            field.PostSpacing = m.Success ? m.Groups[3].Value : " ";
            field.Value = value;
        }
        field.C = comments;
        s.Enum.Fields.Add(field);
    }

    private void ParseStructs(ParserState s)
    {
        if (s.Enum != null)
            return;

        if (s.Struct != null)
        {
            if (s.Line == "};")
            {
                if (s.ScopeDepth != 1)
                    return;

                s.Struct.EndComments = ConsumeComments(s);

                if (s.CallbackId != null)
                {
                    s.Struct.CallbackId = s.CallbackId;
                    s.F.Callbacks.Add(s.Struct);
                    s.CallbackId = null;
                }
                else
                {
                    s.F.Structs.Add(s.Struct);
                }

                s.Struct = null;
            }
            else
            {
                ParseStructFields(s);
            }
            return;
        }

        if (s.LineSplit.Length == 0 || s.LineSplit[0] != "struct")
            return;
        if (s.LineSplit.Length > 1 && s.LineSplit[1].EndsWith(';'))
            return;

        var comments = ConsumeComments(s);

        if (s.ScopeDepth != 0)
            return;

        s.Struct = new Struct(s.LineSplit[1], new List<int>(s.PackSize), comments);
    }

    private void ParseStructFields(ParserState s)
    {
        var comments = ConsumeComments(s);

        if (s.Line.StartsWith("enum", StringComparison.Ordinal) || s.Line == "{")
            return;

        string? arraySize = null;
        var m = Regex.Match(s.Line, @"^([^=.]*\s\**)(\w+);$");
        if (!m.Success)
        {
            m = Regex.Match(s.Line, @"^(.*\s\*?)(\w+)\[\s*(\w+)?\s*\];$");
            if (!m.Success)
                return;
            arraySize = m.Groups[3].Value.Length > 0 ? m.Groups[3].Value : null;
        }

        s.Struct!.Fields.Add(new StructField(m.Groups[2].Value, m.Groups[1].Value.TrimEnd(), arraySize, comments));
    }

    private void ParseCallbackMacros(ParserState s)
    {
        if (s.CallbackMacro != null)
        {
            var comments = ConsumeComments(s);

            if (s.Line.StartsWith("STEAM_CALLBACK_END(", StringComparison.Ordinal))
            {
                s.F.Callbacks.Add(s.CallbackMacro);
                s.CallbackMacro = null;
            }
            else if (s.Line.StartsWith("STEAM_CALLBACK_MEMBER_ARRAY", StringComparison.Ordinal))
            {
                var m = Regex.Match(s.Line, @"^STEAM_CALLBACK_MEMBER_ARRAY\(.*,\s+(.*?)\s*,\s*(\w*)\s*,\s*(\d*)\s*\)");
                if (m.Success)
                    s.CallbackMacro.Fields.Add(new StructField(
                        m.Groups[2].Value, m.Groups[1].Value,
                        m.Groups[3].Value.Length > 0 ? m.Groups[3].Value : null, comments));
            }
            else if (s.Line.StartsWith("STEAM_CALLBACK_MEMBER", StringComparison.Ordinal))
            {
                var m = Regex.Match(s.Line, @"^STEAM_CALLBACK_MEMBER\(.*,\s+(.*?)\s*,\s*(\w*)\[?(\d+)?\]?\s*\)");
                if (m.Success)
                    s.CallbackMacro.Fields.Add(new StructField(
                        m.Groups[2].Value, m.Groups[1].Value,
                        m.Groups[3].Value.Length > 0 ? m.Groups[3].Value : null, comments));
            }
            else
            {
                PrintWarning("Unexpected line in Callback Macro", s);
            }
            return;
        }

        if (!s.Line.StartsWith("STEAM_CALLBACK_BEGIN", StringComparison.Ordinal))
            return;

        var cbComments = ConsumeComments(s);
        var cbm = Regex.Match(s.Line, @"^STEAM_CALLBACK_BEGIN\(\s?(\w+),\s?(.*?)\s*\)");
        if (cbm.Success)
        {
            s.CallbackMacro = new Struct(cbm.Groups[1].Value, new List<int>(s.PackSize), cbComments)
            {
                CallbackId = cbm.Groups[2].Value
            };
        }
    }

    private void ParseInterfaces(ParserState s)
    {
        if (s.Line.StartsWith("class ISteam", StringComparison.Ordinal))
        {
            var comments = ConsumeComments(s);
            if (s.LineSplit.Length > 1 && (s.LineSplit[1].EndsWith(';') || s.LineSplit[1].EndsWith("Response")))
                return;

            s.Interface = new Interface { Name = s.LineSplit[1], C = comments };
        }

        if (s.Interface != null)
            ParseInterfaceFunctions(s);
    }

    private void ParseInterfaceFunctionAttributes(ParserState s)
    {
        foreach (var a in ParserData.FunctionAttributes)
        {
            if (!s.Line.StartsWith(a, StringComparison.Ordinal))
                continue;
            int open = s.Line.IndexOf('(');
            int close = s.Line.LastIndexOf(')');
            s.FunctionAttributes.Add(new FunctionAttribute
            {
                Name = s.Line[..open],
                Value = s.Line[(open + 1)..close].Trim()
            });
        }
    }

    private void ParseInterfaceFunctions(ParserState s)
    {
        ParseInterfaceFunctionAttributes(s);

        if (s.Line.StartsWith("STEAM_PRIVATE_API", StringComparison.Ordinal))
        {
            s.InPrivate = true;
            s.Line = s.Line[(s.Line.IndexOf('(') + 1)..].Trim();
            s.LineSplit = s.LineSplit.Length > 1 ? s.LineSplit[1..] : [];
        }

        bool bInPrivate = s.InPrivate;
        if (s.InPrivate && s.Line.EndsWith(')'))
        {
            s.InPrivate = false;
            s.Line = s.Line[..^1].Trim();
            if (s.LineSplit.Length > 0)
                s.LineSplit = s.LineSplit[..^1];
        }

        if (s.Function == null && !(s.Line.StartsWith("virtual", StringComparison.Ordinal) || s.Line.StartsWith("inline", StringComparison.Ordinal)))
            return;

        if (s.Line.Contains('~'))
            return;

        string args = "";
        ArgAttribute? attr = null;

        if (s.Function == null)
        {
            s.Function = new Function
            {
                IfStatements = s.IfStatements.Count > 1 ? [s.IfStatements[^1]] : [],
                Comments = [.. s.Comments],
                LineComment = s.LineComment,
                Private = bInPrivate,
                Attributes = [.. s.FunctionAttributes]
            };
            s.FunctionAttributes.Clear();
            ConsumeComments(s);
        }

        for (int i = 0; i < s.LineSplit.Length; i++)
        {
            var token = s.LineSplit[i];

            // State 0: Return type — uses sequential if (not else if) to allow fall-through into state 1
            if (s.FuncState == 0)
            {
                if (token == "virtual" || token == "inline")
                    continue;

                if (token.StartsWith('*'))
                {
                    s.Function.ReturnType += "*";
                    token = token[1..];
                    s.FuncState = 1;
                }
                else if (token.Contains('('))
                {
                    s.Function.ReturnType = s.Function.ReturnType.Trim();
                    s.FuncState = 1;
                }
                else
                {
                    s.Function.ReturnType += token + " ";
                    continue;
                }
            }

            // State 1: Method name — sequential if to allow fall-through into state 2/3
            if (s.FuncState == 1)
            {
                s.Function.Name = token.Split('(')[0];

                if (token[^1] == ')')
                {
                    s.FuncState = 3;
                }
                else if (token[^1] == ';')
                {
                    s.FuncState = 0;
                    s.Interface!.Functions.Add(s.Function);
                    s.Function = null;
                    break;
                }
                else if (token[^1] != '(')
                {
                    if (ParserSettings.WarnSpacing)
                        PrintWarning("Function missing whitespace before first arg.", s);
                    token = token.Split('(')[1];
                    s.FuncState = 2;
                }
                else
                {
                    s.FuncState = 2;
                    continue;
                }
            }

            // State 2: Args — always ends with continue
            if (s.FuncState == 2)
            {
                bool bIsAttrib = false;
                foreach (var a in ParserData.ArgumentAttributes)
                {
                    if (token.StartsWith(a, StringComparison.Ordinal))
                    {
                        attr = new ArgAttribute();
                        bIsAttrib = true;
                        break;
                    }
                }

                if (bIsAttrib)
                {
                    int op = token.IndexOf('(');
                    attr!.Name = token[..op];
                    if (token.Length > op + 1)
                    {
                        if (token.EndsWith(')'))
                        {
                            attr.Value = token[(op + 1)..^1];
                            continue;
                        }
                        attr.Value = token[(op + 1)..];
                    }
                    s.FuncState = 4;
                    continue;
                }

                if (token.StartsWith("**"))
                {
                    args += "**";
                    token = token[2..];
                }
                else if (token.StartsWith('*') || token.StartsWith('&'))
                {
                    args += token[0];
                    token = token[1..];
                }

                if (token.Length == 0)
                    continue;

                if (token.StartsWith(')'))
                {
                    if (args.Length > 0)
                    {
                        int TEST = 1, TEST2 = 0;
                        string prev = i > 0 ? s.LineSplit[i - 1] : "";
                        if (prev.Contains("**")) { TEST -= 2; TEST2 += 2; }
                        else if (prev.Contains('*') || prev.Contains('&')) { TEST -= 1; TEST2 += 1; }

                        s.Function!.Args.Add(new Arg
                        {
                            Type = args[..Math.Max(0, args.Length - prev.Length - TEST)].Trim(),
                            Name = prev.Length > TEST2 ? prev[TEST2..] : prev,
                            Attribute = attr
                        });
                        args = "";
                        attr = null;
                    }
                    s.FuncState = 3;
                }
                else if (token.EndsWith(')'))
                {
                    if (ParserSettings.WarnSpacing)
                        PrintWarning("Function missing whitespace before closing parenthesis.", s);
                    s.Function!.Args.Add(new Arg { Type = args.Trim(), Name = token[..^1], Attribute = attr });
                    args = "";
                    attr = null;
                    s.FuncState = 3;
                }
                else if (token[^1] == ',')
                {
                    string noComma = token[..^1];
                    int TEST2 = noComma.Contains('*') || noComma.Contains('&') ? 1 : 0;
                    s.Function!.Args.Add(new Arg
                    {
                        Type = args.Trim(),
                        Name = noComma.Length > TEST2 ? noComma[TEST2..] : noComma,
                        Attribute = attr
                    });
                    args = "";
                    attr = null;
                }
                else if (token == "=")
                {
                    int TEST = 1, TEST2 = 0;
                    string prev = i > 0 ? s.LineSplit[i - 1] : "";
                    if (prev.Contains('*') || prev.Contains('&')) { TEST -= 1; TEST2 += 1; }

                    string next = i + 1 < s.LineSplit.Length ? s.LineSplit[i + 1].TrimEnd(',') : "";
                    s.Function!.Args.Add(new Arg
                    {
                        Type = args[..Math.Max(0, args.Length - prev.Length - TEST)].Trim(),
                        Name = prev.Length > TEST2 ? prev[TEST2..] : prev,
                        Default = next,
                        Attribute = attr
                    });
                    args = "";
                    attr = null;
                    i++; // skip default value token
                }
                else
                {
                    args += token + " ";
                }
                continue;
            }

            // State 3: = 0; or end of line
            if (s.FuncState == 3)
            {
                if (token.EndsWith(';'))
                {
                    s.FuncState = 0;
                    s.Interface!.Functions.Add(s.Function!);
                    s.Function = null;
                    break;
                }
                continue;
            }

            // State 4: attribute value continuation
            if (s.FuncState == 4)
            {
                if (token.EndsWith(')'))
                {
                    attr!.Value += token[..^1];
                    s.FuncState = 2;
                }
                else
                {
                    attr!.Value += token;
                }
                continue;
            }
        }
    }

    private void ParseClasses(ParserState s)
    {
        if (s.LineSplit.Length == 0 || s.LineSplit[0] != "class")
            return;
        if (s.Line.StartsWith("class ISteam", StringComparison.Ordinal))
            return;
        ConsumeComments(s);
    }

    private void ParseScope(ParserState s)
    {
        if (s.Line.Contains('{'))
        {
            s.ScopeDepth++;
            if (s.Line.Count(c => c == '{') > 1)
                PrintWarning("Multiple occurrences of '{'", s);
        }

        if (s.Line.Contains('}'))
        {
            s.ScopeDepth--;

            if (s.Interface != null && s.ScopeDepth == 0)
            {
                s.F.Interfaces.Add(s.Interface);
                s.Interface = null;
            }

            if (s.ScopeDepth < 0)
                PrintWarning("scopeDepth is less than 0!", s);

            if (s.Line.Count(c => c == '}') > 1)
                PrintWarning("Multiple occurrences of '}'", s);
        }
    }

    private Comment ConsumeComments(ParserState s)
    {
        var c = new Comment(s.RawComments, s.Comments, s.RawLineComment, s.LineComment);
        s.RawComments = [];
        s.Comments = [];
        s.RawLineComment = null;
        s.LineComment = null;
        return c;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }

    private static void PrintWarning(string message, ParserState s) =>
        Console.WriteLine($"[WARNING] {message} - In File: {s.F.Name} - On Line {s.LineNum} - {s.Line}");

    private static void PrintWarning(string message) =>
        Console.WriteLine($"[WARNING] {message}");

    private static void PrintUnhandled(string message, ParserState s) =>
        Console.WriteLine($"[UNHANDLED] {message} - In File: {s.F.Name} - On Line {s.LineNum} - {s.Line}");

    public static SteamworksParser Parse(string folder) => new(folder);
}
