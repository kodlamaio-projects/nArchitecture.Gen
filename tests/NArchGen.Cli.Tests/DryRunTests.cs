using System.Text.Json;
using ConsoleUI;

namespace NArchGen.Cli.Tests;

/// <summary><c>--dry-run</c> must predict the real run exactly: same files, same failures, and nothing written.</summary>
public class DryRunTests
{
    private const string _dynamicQueryFile = "Application/Features/Brands/Queries/GetListByDynamic/GetListByDynamicBrandQuery.cs";

    private static string[] strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    [Fact]
    public async Task Lists_the_files_it_would_create_without_writing_them()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand", "--dry-run", "--json");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        using JsonDocument json = JsonDocument.Parse(result.StandardOutput);
        Assert.Empty(strings(json.RootElement.GetProperty("created")));
        JsonElement details = json.RootElement.GetProperty("details");
        Assert.True(details.GetProperty("dryRun").GetBoolean());
        Assert.Contains(Path.GetFullPath(project.Combine(_dynamicQueryFile)), strings(details.GetProperty("plannedCreated")));
        Assert.Empty(strings(details.GetProperty("plannedUpdated")));
        Assert.False(Directory.Exists(project.Combine("Application/Features/Brands/Queries")));
    }

    [Fact]
    public async Task Fails_like_the_real_run_when_a_file_exists()
    {
        using SampleProject project = new();
        project.Write(_dynamicQueryFile, "// hand written");

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand", "--dry-run");

        Assert.Equal(CliExitCodes.Conflict, result.ExitCode);
        Assert.Equal("// hand written", project.Read(_dynamicQueryFile));
    }

    [Fact]
    public async Task With_force_shows_the_diff_of_the_files_it_would_overwrite()
    {
        using SampleProject project = new();
        project.Write(_dynamicQueryFile, "// hand written\n");

        NArchGenResult result = await NArchGenProcess.RunAsync(
            project.Path,
            "generate",
            "dynamic-query",
            "Brand",
            "--force",
            "--dry-run",
            "--json"
        );

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        using JsonDocument json = JsonDocument.Parse(result.StandardOutput);
        string fullPath = Path.GetFullPath(project.Combine(_dynamicQueryFile));
        JsonElement details = json.RootElement.GetProperty("details");
        Assert.Contains(fullPath, strings(details.GetProperty("plannedUpdated")));
        Assert.Contains("-// hand written", details.GetProperty("diffs").GetProperty(fullPath).GetString());
        Assert.Equal("// hand written\n", project.Read(_dynamicQueryFile));
    }

    [Fact]
    public async Task Human_output_says_nothing_has_been_written()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand", "--dry-run");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        Assert.Contains("Files that would be created", result.StandardOutput);
        Assert.Contains("GetListByDynamicBrandQuery.cs", result.StandardOutput);
        Assert.Contains("Dry run: nothing has been written.", result.StandardOutput);
    }
}
