using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftlyS2.Codegen.CS2.Schema;

public class TypeMappings
{
    [JsonPropertyName("blacklistedTypes")]
    public HashSet<string> BlacklistedTypes { get; set; } = [];

    [JsonPropertyName("unmanagedTypeMaps")]
    public Dictionary<string, string> UnmanagedTypeMaps { get; set; } = [];

    [JsonPropertyName("managedTypes")]
    public HashSet<string> ManagedTypes { get; set; } = [];
}

public class DangerousFields
{
    [JsonPropertyName("dangerousFields")]
    public HashSet<string> DangerousFieldsSet { get; set; } = [];
}

public class BlacklistedClasses
{
    [JsonPropertyName("blacklistedClasses")]
    public HashSet<string> BlacklistedClassesSet { get; set; } = [];
}

public static class FieldTypeParser
{
    private static HashSet<string> BlacklistedTypes = [];
    private static Dictionary<string, string> UnmanagedTypeMaps = [];
    private static HashSet<string> ManagedTypes = [];
    private static HashSet<string> DangerousFields = [];
    private static HashSet<string> BlacklistedClasses = [];

    static FieldTypeParser()
    {
        var jsonContent = File.ReadAllText(Path.Combine(Entrypoint.ProjectRootPath, "data", "schema", "typeMappings.json"));
        var datamapsData = JsonSerializer.Deserialize<TypeMappings>(jsonContent);

        BlacklistedTypes = datamapsData!.BlacklistedTypes;
        UnmanagedTypeMaps = datamapsData!.UnmanagedTypeMaps;
        ManagedTypes = datamapsData!.ManagedTypes;

        jsonContent = File.ReadAllText(Path.Combine(Entrypoint.ProjectRootPath, "data", "schema", "blacklistedFields.json"));
        var dangerousFieldsData = JsonSerializer.Deserialize<DangerousFields>(jsonContent);
        DangerousFields = dangerousFieldsData!.DangerousFieldsSet;

        jsonContent = File.ReadAllText(Path.Combine(Entrypoint.ProjectRootPath, "data", "schema", "blacklistedClasses.json"));
        var blacklistedClassesData = JsonSerializer.Deserialize<BlacklistedClasses>(jsonContent);
        BlacklistedClasses = blacklistedClassesData!.BlacklistedClassesSet;
    }

    public static HashSet<string> GetManagedTypes() => ManagedTypes;
    public static HashSet<string> GetBlacklistedTypes() => BlacklistedTypes;
    public static HashSet<string> GetDangerousFields() => DangerousFields;
    public static Dictionary<string, string> GetUnmanagedTypeMaps() => UnmanagedTypeMaps;
    public static HashSet<string> GetBlacklistedClasses() => BlacklistedClasses;

    public static (string Name, bool IsValueType) ConvertHandleType(string type, bool iface = false)
    {
        string variableName;
        int originalLength;
        if (type.StartsWith("CWeakHandle"))
        {
            variableName = "CWeakHandle";
            originalLength = "CWeakHandle".Length;
        }
        else if (type.StartsWith("CStrongHandleCopyable"))
        {
            variableName = "CStrongHandle";
            originalLength = "CStrongHandleCopyable".Length;
        }
        else if (type.StartsWith("CStrongHandle"))
        {
            variableName = "CStrongHandle";
            originalLength = "CStrongHandle".Length;
        }
        else
        {
            variableName = "CHandle";
            originalLength = "CHandle".Length;
        }

        var GenericT1 = type.Substring(originalLength + 1);
        GenericT1 = GenericT1.Substring(0, GenericT1.Length - 1);
        GenericT1 = iface ? ClassNameConvertor.GetInterfaceName(GenericT1) : ClassNameConvertor.GetImplementationName(GenericT1);

        return ($"{variableName}<{GenericT1}>", true);
    }

    public static (string Name, bool IsValueType) ConvertUtlVectorType(string type, List<string> AllClassNames, List<string> AllEnumNames, bool iface = false)
    {
        string variableName;
        int originalLength;
        if (type.StartsWith("CUtlVectorFixedGrowable"))
        {
            variableName = "CUtlVectorFixedGrowable";
            originalLength = "CUtlVectorFixedGrowable".Length;
        }
        else if (type.StartsWith("CUtlLeanVector"))
        {
            variableName = "CUtlLeanVector";
            originalLength = "CUtlLeanVector".Length;
        }
        else if (type.StartsWith("CUtlVectorEmbeddedNetworkVar"))
        {
            variableName = "CUtlVector";
            originalLength = "CUtlVectorEmbeddedNetworkVar".Length;
        }
        else if (type.StartsWith("CNetworkUtlVectorBase"))
        {
            variableName = "CUtlVector";
            originalLength = "CNetworkUtlVectorBase".Length;
        }
        else
        {
            variableName = "CUtlVector";
            originalLength = "CUtlVector".Length;
        }

        if (originalLength + 1 > type.Length) throw new Exception($"Invalid type format: {type}");

        var GenericT1 = type.Substring(originalLength + 1);
        GenericT1 = GenericT1.Substring(0, GenericT1.Length - 1);
        if (GenericT1.Contains(',')) GenericT1 = GenericT1.Split(',')[0].Trim();

        bool IsPtr = GenericT1.EndsWith("*");
        if (IsPtr) GenericT1 = GenericT1.Substring(0, GenericT1.Length - 1).Trim();

        var (GenericT1Type, IsValueType) = ConvertFieldType(GenericT1, "ref", AllClassNames, AllEnumNames, iface);

        if (variableName == "CUtlLeanVector")
        {
            if (IsPtr && GenericT1Type == "char") return ($"{variableName}<CString, int>", true);
            else if (IsPtr) return ($"{variableName}<PointerTo<{GenericT1Type}>, int>", true);
            else return ($"{variableName}<{GenericT1Type}, int>", true);
        }
        else
        {
            if (IsPtr && GenericT1Type == "char") return ($"{variableName}<CString>", true);
            else if (IsPtr) return ($"{variableName}<PointerTo<{GenericT1Type}>>", true);

            foreach (var blacklistedType in BlacklistedTypes)
            {
                if (GenericT1Type.Contains(blacklistedType))
                {
                    return ($"{variableName}<SchemaUntypedField>", true);
                }
            }

            return ($"{variableName}<{GenericT1Type}>", true);
        }
    }

    public static (string Name, bool IsValueType) ConvertFieldType(string type, string kind, List<string> AllClassNames, List<string> AllEnumNames, bool iface = false)
    {
        type = type.Replace(" ", "").Trim();
        type = type.Replace(":", "_");
        var prefix = iface ? "I" : "";

        foreach (var blacklistedType in BlacklistedTypes)
        {
            if (type.StartsWith(blacklistedType) && type != "CUtlSymbolLarge")
            {
                return ("SchemaUntypedField", false);
            }
        }

        if (kind == "ptr" && type == "char") return ("CString", true);

        foreach (var kvp in UnmanagedTypeMaps)
        {
            if (type.StartsWith(kvp.Key))
            {
                if (type.StartsWith("CWeakHandle") || type.StartsWith("CStrongHandle") || type.StartsWith("CHandle"))
                {
                    var (Name, IsValueType) = ConvertHandleType(type, true);
                    if (kind == "fixed_array") return ($"{prefix}SchemaFixedArray<{Name}>", false);
                    return (Name, IsValueType);
                }

                if (type.StartsWith("CUtlVector") || type.StartsWith("CNetworkUtlVector") || type.StartsWith("CUtlLeanVector"))
                {
                    var (Name, IsValueType) = ConvertUtlVectorType(type, AllClassNames, AllEnumNames, true);
                    if (kind == "fixed_array") return ($"{prefix}SchemaFixedArray<{Name}>", false);
                    return (Name, IsValueType);
                }

                if (kind == "fixed_array")
                {
                    if (type == "char")
                    {
                        return ($"{prefix}SchemaFixedString", false);
                    }
                    else if (!type.Contains("["))
                    {
                        return ($"{prefix}SchemaFixedArray<{type.Replace(kvp.Key, kvp.Value)}>", false);
                    }
                    else return ("SchemaUntypedField", false);
                }

                return (type.Replace(kvp.Key, kvp.Value), true);
            }
        }

        if (AllEnumNames.Contains(type))
        {
            if (kind == "fixed_array") return ($"{prefix}SchemaFixedArray<{type}>", false);
            return (type, true);
        }

        if (AllClassNames.Contains(type))
        {
            if (kind == "fixed_array") return ($"{prefix}SchemaClassFixedArray<{type}>", false);
            var complexType = iface ? ClassNameConvertor.GetInterfaceName(type) : ClassNameConvertor.GetImplementationName(type);
            return (complexType, false);
        }

        return ("SchemaUntypedField", false);
    }
}