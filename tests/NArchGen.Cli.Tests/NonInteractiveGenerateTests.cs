using ConsoleUI;

namespace NArchGen.Cli.Tests;

/// <summary>
/// Drives the generate commands without a terminal, as CI scripts and AI agents do, and checks that they never wait
/// for input, report through exit codes and do not overwrite existing files unless <c>--force</c> is passed.
/// </summary>
public class NonInteractiveGenerateTests
{
    private const string _dynamicQueryFile = "Application/Features/Brands/Queries/GetListByDynamic/GetListByDynamicBrandQuery.cs";

    [Fact]
    public async Task Missing_argument_fails_with_usage_error_and_lists_the_choices()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "crud");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.Contains("Missing <EntityName> argument", result.StandardError);
        Assert.Contains("Brand, Model", result.StandardError);
    }

    [Fact]
    public async Task Unknown_entity_fails_with_usage_error()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "crud", "Car", "BaseDbContext");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.Contains("Entity \"Car\" not found", result.StandardError);
    }

    [Fact]
    public async Task Unknown_option_fails_with_usage_error()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand", "--forse");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.Contains("Unknown option 'forse'", result.StandardError);
        Assert.False(Directory.Exists(project.Combine("Application/Features/Brands/Queries")));
    }

    [Fact]
    public async Task Running_outside_a_project_fails_with_usage_error()
    {
        string emptyDirectory = Directory.CreateTempSubdirectory("nArchGen-cli-").FullName;
        try
        {
            NArchGenResult result = await NArchGenProcess.RunAsync(emptyDirectory, "generate", "crud", "Brand", "BaseDbContext");

            Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
            Assert.Contains("No project found here", result.StandardError);
        }
        finally
        {
            Directory.Delete(emptyDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Command_takes_name_and_feature_as_separate_positional_arguments()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "command", "SyncBrand", "Brands");

        // The feature is recognized; the only thing missing is the endpoint method, which cannot be asked for.
        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.Contains("Missing --endpoint-method option", result.StandardError);
    }

    [Fact]
    public async Task Crud_does_not_write_anything_when_a_generated_file_already_exists()
    {
        using SampleProject project = new();
        project.Write("Persistence/Repositories/BrandRepository.cs", "// hand written");
        string dbContextBefore = project.Read("Persistence/Contexts/BaseDbContext.cs");

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "crud", "Brand", "BaseDbContext");

        Assert.Equal(CliExitCodes.Conflict, result.ExitCode);
        Assert.Contains("BrandRepository.cs", result.StandardError);
        Assert.Equal("// hand written", project.Read("Persistence/Repositories/BrandRepository.cs"));
        Assert.Equal(dbContextBefore, project.Read("Persistence/Contexts/BaseDbContext.cs"));
        Assert.False(Directory.Exists(project.Combine("Application/Features/Brands/Commands")));
    }

    [Fact]
    public async Task Dynamic_query_is_generated_without_prompting()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(project.Combine(_dynamicQueryFile)));
    }

    [Fact]
    public async Task Dynamic_query_overwrites_existing_files_only_with_force()
    {
        using SampleProject project = new();
        project.Write(_dynamicQueryFile, "// hand written");

        NArchGenResult withoutForce = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand");

        Assert.Equal(CliExitCodes.Conflict, withoutForce.ExitCode);
        Assert.Equal("// hand written", project.Read(_dynamicQueryFile));

        NArchGenResult withForce = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand", "--force");

        Assert.Equal(CliExitCodes.Success, withForce.ExitCode);
        Assert.Contains("GetListByDynamicBrandQuery", project.Read(_dynamicQueryFile));
    }
}
