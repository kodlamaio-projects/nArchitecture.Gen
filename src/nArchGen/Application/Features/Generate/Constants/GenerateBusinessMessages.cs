namespace Application.Features.Generate.Constants;

public static class GenerateBusinessMessages
{
    public static string EntityClassShouldBeInheritEntityBaseClass(string entityName) =>
        $"{entityName} class must be inherit Entity base class.";

    public static string FilesAlreadyExist(int count) =>
        $"{count} file(s) to be generated already exist. Nothing has been written; use --force to overwrite them.";
}
