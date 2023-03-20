namespace Core.CodeGen.Code;

public static class StringCaseExtensions
{
    public static string ToCamelCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return value.ToLower();

        return words[0].ToLower()
            + string.Join(string.Empty, words, startIndex: 1, count: words.Length - 1);
    }

    public static string ToPascalCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return char.ToUpper(c: value[index: 0]) + value[1..];

        return string.Join(
            string.Empty,
            values: words.Select(word => char.ToUpper(c: word[index: 0]) + word[1..])
        );
    }

    public static string ToSnakeCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return value.ToLower();

        return string.Join(separator: "_", values: words.Select(word => word.ToLower()));
    }

    public static string ToKebabCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return value.ToLower();

        return string.Join(separator: "-", values: words.Select(word => word.ToLower()));
    }

    public static string ToAbbreviation(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] words = value.GetWords();
        if (words.Length == 1)
            return char.ToLower(c: value[index: 0]).ToString();

        return string.Join(
            string.Empty,
            values: words.Select(word => char.ToLower(c: word[index: 0]))
        );
    }
}
