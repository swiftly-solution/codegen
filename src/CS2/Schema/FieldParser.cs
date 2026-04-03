using System.Text.Json.Serialization;

namespace SwiftlyS2.Codegen.CS2.Schema;

public class ClassFieldEntry
{
    [JsonPropertyName("alignment")]
    public int Alignment { get; set; } = 0;

    [JsonPropertyName("element_alignment")]
    public int? ElementAlignment { get; set; }

    [JsonPropertyName("element_count")]
    public int? ElementCount { get; set; }

    [JsonPropertyName("element_size")]
    public int? ElementSize { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_hash")]
    public ulong NameHash { get; set; } = 0;

    [JsonPropertyName("networked")]
    public bool Networked { get; set; } = false;

    [JsonPropertyName("offset")]
    public int Offset { get; set; } = 0;

    [JsonPropertyName("size")]
    public int Size { get; set; } = 0;

    [JsonPropertyName("templated")]
    public string? Templated { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class ProcessedFieldEntry
{
    public string Name = string.Empty;
    public string Hash = string.Empty;
    public string Kind = string.Empty;
    public bool Networked;
    public string Type = string.Empty;
    public string ImplementationType = string.Empty;
    public string InterfaceType = string.Empty;
    public bool IsValueType;
    public bool IsCharPtrString => Kind == "ptr" && Type == "char";
    public bool IsFixedCharString => Kind == "fixed_array" && ImplementationType == "SchemaFixedString";
    public bool IsStringHandle => Type == "CUtlSymbolLarge";
    public bool IsUtlStringHandle => Type == "CUtlString";

    public int ElementCount = 0;
    public int ElementSize = 0;
    public int ElementAlignment = 0;

    public string Setter = string.Empty;
    public string Ref = string.Empty;
    public string Nullable = string.Empty;
    public string Comment = string.Empty;
    public string RefMethod = string.Empty;
}

public class FieldParser
{
    public static ProcessedFieldEntry ParseField(ClassFieldEntry field, List<string> AllClassNames, List<string> AllEnumNames)
    {
        var ProcessedEntryType = field.Templated ?? field.Type;
        var (Name, IsValueType) = FieldTypeParser.ConvertFieldType(ProcessedEntryType, field.Kind, AllClassNames, AllEnumNames, false);

        var processedEntry = new ProcessedFieldEntry
        {
            Name = FieldNameConvertor.ConvertFieldName(field.Name),
            Hash = $"0x{field.NameHash:X}",
            Kind = field.Kind,
            Networked = field.Networked,
            Type = ProcessedEntryType,
            ImplementationType = Name,
        };

        if (!IsValueType)
        {
            (processedEntry.InterfaceType, processedEntry.IsValueType) = FieldTypeParser.ConvertFieldType(ProcessedEntryType, field.Kind, AllClassNames, AllEnumNames, true);
        }
        else
        {
            processedEntry.InterfaceType = processedEntry.ImplementationType;
            processedEntry.IsValueType = IsValueType;
        }

        processedEntry.InterfaceType = processedEntry.InterfaceType.Replace(":", "_");
        processedEntry.ImplementationType = processedEntry.ImplementationType.Replace(":", "_");

        if (field.Kind == "fixed_array")
        {
            processedEntry.ElementCount = field.ElementCount ?? 0;
            processedEntry.ElementSize = field.ElementSize ?? 0;
            processedEntry.ElementAlignment = field.ElementAlignment ?? 0;
        }

        return processedEntry;
    }
}