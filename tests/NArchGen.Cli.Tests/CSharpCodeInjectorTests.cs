using Core.CodeGen.Code.CSharp;

namespace NArchGen.Cli.Tests;

/// <summary>Generating again with <c>--force</c> re-runs every injection; each must leave the file as the first run did.</summary>
public class CSharpCodeInjectorTests : IDisposable
{
    private readonly string _filePath = Path.GetTempFileName();

    public void Dispose() => File.Delete(_filePath);

    [Fact]
    public async Task Adding_the_same_lines_to_a_method_twice_adds_them_once()
    {
        await File.WriteAllTextAsync(
            _filePath,
            """
            public static class Registration
            {
                public static void AddServices()
                {
                    services.AddScoped<IUserService, UserManager>();
                }
            }
            """,
            TestContext.Current.CancellationToken
        );
        string[] line = ["services.AddScoped<IBrandService, BrandManager>();"];

        await CSharpCodeInjector.AddCodeLinesToMethodAsync(_filePath, "AddServices", line);
        string afterFirstRun = await File.ReadAllTextAsync(_filePath, TestContext.Current.CancellationToken);
        await CSharpCodeInjector.AddCodeLinesToMethodAsync(_filePath, "AddServices", line);

        Assert.Contains("IBrandService", afterFirstRun);
        Assert.Equal(afterFirstRun, await File.ReadAllTextAsync(_filePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_snippet_is_recognized_by_its_first_line_even_when_later_lines_differ()
    {
        await File.WriteAllTextAsync(
            _filePath,
            """
            public class Seeds
            {
                private void getFeatureOperationClaims()
                {
                    int lastId = 0;
                }
            }
            """,
            TestContext.Current.CancellationToken
        );

        await CSharpCodeInjector.AddCodeLinesToMethodAsync(
            _filePath,
            "getFeatureOperationClaims",
            ["", "#region Brands CRUD", "// id 1", "#endregion"]
        );
        string afterFirstRun = await File.ReadAllTextAsync(_filePath, TestContext.Current.CancellationToken);
        await CSharpCodeInjector.AddCodeLinesToMethodAsync(
            _filePath,
            "getFeatureOperationClaims",
            ["", "#region Brands CRUD", "// id 2", "#endregion"]
        );

        Assert.Equal(afterFirstRun, await File.ReadAllTextAsync(_filePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Adding_the_same_method_to_a_class_twice_adds_it_once()
    {
        await File.WriteAllTextAsync(
            _filePath,
            """
            public class BrandsController
            {
                public void Get() { }
            }
            """,
            TestContext.Current.CancellationToken
        );
        string[] method = ["[HttpPut(\"SyncBrand\")]", "public void SyncBrand() { }"];

        await CSharpCodeInjector.AddMethodToClass(_filePath, "BrandsController", method);
        string afterFirstRun = await File.ReadAllTextAsync(_filePath, TestContext.Current.CancellationToken);
        await CSharpCodeInjector.AddMethodToClass(_filePath, "BrandsController", method);

        Assert.Contains("SyncBrand", afterFirstRun);
        Assert.Equal(afterFirstRun, await File.ReadAllTextAsync(_filePath, TestContext.Current.CancellationToken));
    }
}
