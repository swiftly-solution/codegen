using System.Text;

namespace SwiftlyS2.Codegen;

/// <summary>
/// A helper class for generating formatted C# code with automatic indentation management
/// </summary>
public class CodeWriter
{
    private readonly List<string> lines = new();
    private int indentLevel = 0;
    private readonly int spacesPerIndent;
    private readonly string indentString;

    /// <summary>
    /// Creates a new CodeWriter instance
    /// </summary>
    /// <param name="spacesPerIndent">Number of spaces per indentation level (default: 4)</param>
    public CodeWriter(int spacesPerIndent = 4)
    {
        this.spacesPerIndent = spacesPerIndent;
        this.indentString = new string(' ', spacesPerIndent);
    }

    /// <summary>
    /// Adds a line of code with automatic indentation
    /// </summary>
    /// <param name="text">The text to add. If empty, adds a blank line</param>
    public void AddLine(string text = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            lines.Add("");
        }
        else
        {
            lines.Add(new string(' ', indentLevel * spacesPerIndent) + text);
        }
    }

    /// <summary>
    /// Adds multiple lines of code with automatic indentation
    /// </summary>
    /// <param name="textLines">The lines to add</param>
    public void AddLines(params string[] textLines)
    {
        foreach (var line in textLines)
        {
            AddLine(line);
        }
    }

    /// <summary>
    /// Adds multiple lines of code with automatic indentation
    /// </summary>
    /// <param name="textLines">The lines to add</param>
    public void AddLines(IEnumerable<string> textLines)
    {
        foreach (var line in textLines)
        {
            AddLine(line);
        }
    }

    /// <summary>
    /// Adds raw text without any indentation
    /// </summary>
    /// <param name="text">The text to add</param>
    public void AddRaw(string text)
    {
        lines.Add(text);
    }

    /// <summary>
    /// Increases the indentation level
    /// </summary>
    public void Indent()
    {
        indentLevel++;
    }

    /// <summary>
    /// Decreases the indentation level
    /// </summary>
    public void Dedent()
    {
        indentLevel = Math.Max(0, indentLevel - 1);
    }

    /// <summary>
    /// Temporarily changes indentation level for a scope
    /// </summary>
    /// <param name="action">The action to execute with modified indentation</param>
    /// <param name="levels">Number of levels to indent (can be negative to dedent)</param>
    public void WithIndent(Action action, int levels = 1)
    {
        var previousLevel = indentLevel;
        indentLevel += levels;
        indentLevel = Math.Max(0, indentLevel);
        try
        {
            action();
        }
        finally
        {
            indentLevel = previousLevel;
        }
    }

    /// <summary>
    /// Adds a block of code with opening/closing braces and automatic indentation
    /// </summary>
    /// <param name="header">The block header (e.g., "public class MyClass")</param>
    /// <param name="contentFunc">Action to generate the block content</param>
    /// <param name="openBrace">Opening brace character (default: "{")</param>
    /// <param name="closeBrace">Closing brace character (default: "}")</param>
    public void AddBlock(string header, Action contentFunc, string openBrace = "{", string closeBrace = "}")
    {
        AddLine(header);
        AddLine(openBrace);
        Indent();
        contentFunc();
        Dedent();
        AddLine(closeBrace);
    }

    /// <summary>
    /// Adds a block without a header
    /// </summary>
    /// <param name="contentFunc">Action to generate the block content</param>
    /// <param name="openBrace">Opening brace character (default: "{")</param>
    /// <param name="closeBrace">Closing brace character (default: "}")</param>
    public void AddBlock(Action contentFunc, string openBrace = "{", string closeBrace = "}")
    {
        AddLine(openBrace);
        Indent();
        contentFunc();
        Dedent();
        AddLine(closeBrace);
    }

    /// <summary>
    /// Adds an XML documentation comment block
    /// </summary>
    /// <param name="summary">The summary text</param>
    public void AddXmlSummary(string summary)
    {
        AddLine("/// <summary>");
        AddLine($"/// {summary}");
        AddLine("/// </summary>");
    }

    /// <summary>
    /// Adds a multi-line XML documentation comment
    /// </summary>
    /// <param name="summaryLines">The summary lines</param>
    public void AddXmlSummary(params string[] summaryLines)
    {
        AddLine("/// <summary>");
        foreach (var line in summaryLines)
        {
            AddLine($"/// {line}");
        }
        AddLine("/// </summary>");
    }

    /// <summary>
    /// Adds an XML documentation parameter comment
    /// </summary>
    /// <param name="paramName">The parameter name</param>
    /// <param name="description">The parameter description</param>
    public void AddXmlParam(string paramName, string description)
    {
        AddLine($"/// <param name=\"{paramName}\">{description}</param>");
    }

    /// <summary>
    /// Adds an XML documentation returns comment
    /// </summary>
    /// <param name="description">The return value description</param>
    public void AddXmlReturns(string description)
    {
        AddLine($"/// <returns>{description}</returns>");
    }

    /// <summary>
    /// Adds a using directive
    /// </summary>
    /// <param name="namespace">The namespace to import</param>
    public void AddUsing(string @namespace)
    {
        AddLine($"using {@namespace};");
    }

    /// <summary>
    /// Adds multiple using directives
    /// </summary>
    /// <param name="namespaces">The namespaces to import</param>
    public void AddUsings(params string[] namespaces)
    {
        foreach (var ns in namespaces)
        {
            AddUsing(ns);
        }
    }

    /// <summary>
    /// Adds multiple using directives
    /// </summary>
    /// <param name="namespaces">The namespaces to import</param>
    public void AddUsings(IEnumerable<string> namespaces)
    {
        foreach (var ns in namespaces)
        {
            AddUsing(ns);
        }
    }

    /// <summary>
    /// Adds a namespace declaration
    /// </summary>
    /// <param name="namespaceName">The namespace name</param>
    /// <param name="contentFunc">Action to generate the namespace content</param>
    /// <param name="fileScoped">If true, uses file-scoped namespace syntax</param>
    public void AddNamespace(string namespaceName, Action contentFunc, bool fileScoped = true)
    {
        if (fileScoped)
        {
            AddLine($"namespace {namespaceName};");
            AddLine();
            contentFunc();
        }
        else
        {
            AddBlock($"namespace {namespaceName}", contentFunc);
        }
    }

    /// <summary>
    /// Adds a class declaration
    /// </summary>
    /// <param name="className">The class name with modifiers (e.g., "public class MyClass")</param>
    /// <param name="contentFunc">Action to generate the class content</param>
    public void AddClass(string className, Action contentFunc)
    {
        AddBlock(className, contentFunc);
    }

    /// <summary>
    /// Adds a method declaration
    /// </summary>
    /// <param name="methodSignature">The complete method signature</param>
    /// <param name="contentFunc">Action to generate the method content</param>
    public void AddMethod(string methodSignature, Action contentFunc)
    {
        AddBlock(methodSignature, contentFunc);
    }

    /// <summary>
    /// Adds a property with get/set accessors
    /// </summary>
    /// <param name="propertySignature">The property signature (e.g., "public string Name")</param>
    /// <param name="getContent">Optional getter content. If null, uses auto-property</param>
    /// <param name="setContent">Optional setter content. If null, uses auto-property</param>
    public void AddProperty(string propertySignature, Action? getContent = null, Action? setContent = null)
    {
        if (getContent == null && setContent == null)
        {
            AddLine($"{propertySignature} {{ get; set; }}");
        }
        else
        {
            AddBlock(propertySignature, () =>
            {
                if (getContent != null)
                {
                    AddBlock("get", getContent);
                }
                else
                {
                    AddLine("get;");
                }

                if (setContent != null)
                {
                    AddBlock("set", setContent);
                }
                else
                {
                    AddLine("set;");
                }
            });
        }
    }

    /// <summary>
    /// Adds a region directive
    /// </summary>
    /// <param name="regionName">The region name</param>
    /// <param name="contentFunc">Action to generate the region content</param>
    public void AddRegion(string regionName, Action contentFunc)
    {
        AddLine($"#region {regionName}");
        AddLine();
        contentFunc();
        AddLine();
        AddLine("#endregion");
    }

    /// <summary>
    /// Adds a pragma directive
    /// </summary>
    /// <param name="pragma">The pragma directive (e.g., "warning disable CS0649")</param>
    public void AddPragma(string pragma)
    {
        AddLine($"#pragma {pragma}");
    }

    /// <summary>
    /// Adds an attribute
    /// </summary>
    /// <param name="attribute">The attribute text (e.g., "Serializable" or "DataMember(Name = \"id\")")</param>
    public void AddAttribute(string attribute)
    {
        AddLine($"[{attribute}]");
    }

    /// <summary>
    /// Adds multiple attributes on separate lines
    /// </summary>
    /// <param name="attributes">The attributes to add</param>
    public void AddAttributes(params string[] attributes)
    {
        foreach (var attr in attributes)
        {
            AddAttribute(attr);
        }
    }

    /// <summary>
    /// Inserts a separator comment line
    /// </summary>
    /// <param name="title">Optional title for the separator</param>
    /// <param name="width">Width of the separator (default: 80)</param>
    public void AddSeparator(string? title = null, int width = 80)
    {
        if (string.IsNullOrEmpty(title))
        {
            AddLine("// " + new string('-', width - 3));
        }
        else
        {
            var dashCount = Math.Max(0, width - title.Length - 6);
            var leftDashes = dashCount / 2;
            var rightDashes = dashCount - leftDashes;
            AddLine($"// {new string('-', leftDashes)} {title} {new string('-', rightDashes)}");
        }
    }

    /// <summary>
    /// Adds a single-line comment
    /// </summary>
    /// <param name="comment">The comment text</param>
    public void AddComment(string comment)
    {
        AddLine($"// {comment}");
    }

    /// <summary>
    /// Clears all lines from the writer
    /// </summary>
    public void Clear()
    {
        lines.Clear();
        indentLevel = 0;
    }

    /// <summary>
    /// Gets the current line count
    /// </summary>
    public int LineCount => lines.Count;

    /// <summary>
    /// Gets the current indentation level
    /// </summary>
    public int CurrentIndentLevel => indentLevel;

    /// <summary>
    /// Gets the generated code as a string
    /// </summary>
    /// <param name="lineEnding">Line ending to use (default: "\n")</param>
    /// <returns>The complete generated code</returns>
    public string GetCode(string lineEnding = "\n")
    {
        return string.Join(lineEnding, lines);
    }

    /// <summary>
    /// Gets the generated code as a StringBuilder
    /// </summary>
    /// <param name="lineEnding">Line ending to use (default: "\n")</param>
    /// <returns>StringBuilder containing the generated code</returns>
    public StringBuilder GetCodeBuilder(string lineEnding = "\n")
    {
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append(lines[i]);
            if (i < lines.Count - 1)
            {
                sb.Append(lineEnding);
            }
        }
        return sb;
    }

    /// <summary>
    /// Gets all lines as a read-only collection
    /// </summary>
    public IReadOnlyList<string> Lines => lines.AsReadOnly();

    /// <summary>
    /// Writes the generated code to a file
    /// </summary>
    /// <param name="filePath">The file path to write to</param>
    /// <param name="lineEnding">Line ending to use (default: "\n")</param>
    public async Task WriteToFileAsync(string filePath, string lineEnding = "\n")
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, GetCode(lineEnding));
    }

    /// <summary>
    /// Writes the generated code to a file synchronously
    /// </summary>
    /// <param name="filePath">The file path to write to</param>
    /// <param name="lineEnding">Line ending to use (default: "\n")</param>
    public void WriteToFile(string filePath, string lineEnding = "\n")
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, GetCode(lineEnding));
    }

    /// <summary>
    /// Creates a string representation of the generated code
    /// </summary>
    public override string ToString() => GetCode();
}
