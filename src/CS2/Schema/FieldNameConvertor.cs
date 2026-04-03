namespace SwiftlyS2.Codegen.CS2.Schema;

public static class FieldNameConvertor
{
    private static readonly HashSet<string> TypePrefixes = [
        "psz",
        "fl",
        "a",
        "n",
        "i",
        "isz",
        "vec",
        "us",
        "u",
        "ub",
        "un",
        "sz",
        "b",
        "f",
        "clr",
        "h",
        "ang",
        "af",
        "ch",
        "q",
        "p",
        "v",
        "arr",
        "bv",
        "e",
        "s",
    ];

    public static string ConvertFieldName(string fieldName)
    {
        fieldName = RemovePrefix(fieldName, "m_");
        foreach (var prefix in TypePrefixes)
        {
            if (fieldName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var tempRemoved = RemovePrefix(fieldName, prefix);
                if (tempRemoved.Length > 0 && char.IsUpper(tempRemoved[0]))
                {
                    return tempRemoved;
                }
            }
        }

        fieldName = char.ToUpper(fieldName[0]) + fieldName[1..];
        return fieldName;
    }

    private static string RemovePrefix(string text, string prefix)
    {
        if (text.StartsWith(prefix))
        {
            return text[prefix.Length..];
        }
        return text;
    }
}