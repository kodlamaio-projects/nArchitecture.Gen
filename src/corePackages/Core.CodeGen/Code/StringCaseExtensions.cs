using System.Globalization;

namespace Core.CodeGen.Code;

public static class StringCaseExtensions
{
    public static string ToCamelCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return value.ToLower(CultureInfo.GetCultureInfo("en-EN"));

        return words[0].ToLower(CultureInfo.GetCultureInfo("en-EN"))
            + string.Join(string.Empty, words, startIndex: 1, count: words.Length - 1);
    }

    public static string ToPascalCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return char.ToUpper(value[index: 0], CultureInfo.GetCultureInfo("en-EN")) + value[1..];

        return string.Join(
            string.Empty,
            values: words.Select(
                word =>
                    char.ToUpper(word[index: 0], CultureInfo.GetCultureInfo("en-EN")) + word[1..]
            )
        );
    }

    public static string ToSnakeCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return value.ToLower(CultureInfo.GetCultureInfo("en-EN"));

        return string.Join(
            separator: "_",
            values: words.Select(word => word.ToLower(CultureInfo.GetCultureInfo("en-EN")))
        );
    }

    public static string ToKebabCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return value.ToLower(CultureInfo.GetCultureInfo("en-EN"));

        return string.Join(
            separator: "-",
            values: words.Select(word => word.ToLower(CultureInfo.GetCultureInfo("en-EN")))
        );
    }

    public static string ToAbbreviation(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return char.ToLower(value[index: 0], CultureInfo.GetCultureInfo("en-EN")).ToString();

        return string.Join(
            string.Empty,
            values: words.Select(
                word => char.ToLower(word[index: 0], CultureInfo.GetCultureInfo("en-EN"))
            )
        );
    }
}
