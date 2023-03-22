using Core.CodeGen.Code;
using Scriban.Functions;

namespace Core.CodeGen.TemplateEngine.Scriban;

public class ScribanStringFunctionsExtensions : StringFunctions
{
    public static string CamelCase(string input) => input.ToCamelCase();

    public static string PascalCase(string input) => input.ToPascalCase();

    public static string SnakeCase(string input) => input.ToSnakeCase();

    public static string KebabCase(string input) => input.ToKebabCase();

    public static string Abbreviation(string input) => input.ToAbbreviation();

    public static string Plural(string input) => input.ToPlural();

    public static string Singular(string input) => input.ToSingular();

    public static string Words(string input) =>
        string.Join(separator: ' ', value: input.GetWords());
}
