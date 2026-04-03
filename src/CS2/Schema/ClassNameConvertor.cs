namespace SwiftlyS2.Codegen.CS2.Schema;

public class ClassNameConvertor
{
    private static readonly HashSet<string> ReservedNames = [
        "SchemaClass",
        "SchemaField",
        "SchemaFixedArray",
        "SchemaFixedString"
    ];

    public static string GetInterfaceName(string className)
    {
        if (ReservedNames.Contains(className))
        {
            return $"I{className}";
        }

        return className.Replace(':', '_');
    }

    public static string GetImplementationName(string className)
    {
        if (ReservedNames.Contains(className))
        {
            return className;
        }

        return className.Replace(':', '_') + "Impl";
    }
}