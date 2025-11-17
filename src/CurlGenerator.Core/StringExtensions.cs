using System.Text.RegularExpressions;

namespace CurlGenerator.Core;

public static class StringExtensions
{
    public static string ConvertKebabCaseToPascalCase(this string str)
    {
        return string.Concat(str.Split('-').Select(s => s.CapitalizeFirstCharacter().Replace(".", "_")));
    }

    public static string ConvertKebabCaseToSnakeCase(this string str)
    {
        return str.Replace("-", "_").ToLowerInvariant();
    }

    public static string ConvertRouteToCamelCase(this string str)
    {
        return string.Concat(str.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(CapitalizeFirstCharacter));
    }

    public static string CapitalizeFirstCharacter(this string str)
    {
        return str[0..1].ToUpperInvariant() + str[1..];
    }

    public static string ConvertSpacesToPascalCase(this string str)
    {
        return string.Concat(str.Split(' ').Select(CapitalizeFirstCharacter));
    }

    public static string Prefix(this string str, string prefix)
    {
        return str.StartsWith(prefix) ? str : prefix + str;
    }
}
