using System.Text.RegularExpressions;
using PluralizeService.Core;

namespace Core.CodeGen.Code;

public static class StringWordExtension
{
    public static string[] GetWords(this string value) =>
        Regex
            .Split(value, pattern: @"(?<!^)(?=[A-Z0-9])|\s+|_|-")
            .Where(word => !string.IsNullOrEmpty(word))
            .ToArray();

    public static string ToPlural(this string value) => PluralizationProvider.Pluralize(value);

    public static string ToSingular(this string value) => PluralizationProvider.Singularize(value);
}
