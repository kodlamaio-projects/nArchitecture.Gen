namespace Application.Features.Generate.Constants;

public static class GenerateBusinessMessages
{
    public static string EntityClassShouldBeInheritEntityBaseClass(string entityName) =>
        $"{entityName} class must be inherit Entity base class.";

    public static string FileAlreadyExists(string path) => $"File already exists in path: {path}";
}
