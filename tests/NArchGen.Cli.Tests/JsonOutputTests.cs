using System.Text.Json;
using ConsoleUI;

namespace NArchGen.Cli.Tests;

/// <summary>
/// <c>--json</c> is the contract scripts and AI agents parse: stdout must hold exactly one JSON document with the same
/// shape on success and failure, and its exit code must match the process exit code.
/// </summary>
public class JsonOutputTests
{
    private const string _dynamicQueryFile = "Application/Features/Brands/Queries/GetListByDynamic/GetListByDynamicBrandQuery.cs";

    private static JsonElement parse(NArchGenResult result)
    {
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement root = document.RootElement.Clone();

        Assert.Equal(result.ExitCode, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(result.ExitCode == CliExitCodes.Success ? "success" : "error", root.GetProperty("status").GetString());
        foreach (string property in new[] { "command", "created", "updated", "warnings", "error", "details" })
            Assert.True(root.TryGetProperty(property, out _), $"Missing \"{property}\".");
        return root;
    }

    private static string[] strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    [Fact]
    public async Task Success_lists_created_files_as_full_paths_and_warnings()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand", "--json");

        Assert.Equal(CliExitCodes.Success, result.ExitCode);
        JsonElement json = parse(result);
        Assert.Equal("generate dynamic-query", json.GetProperty("command").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("error").ValueKind);
        Assert.Contains(Path.GetFullPath(project.Combine(_dynamicQueryFile)), strings(json.GetProperty("created")));
        Assert.All(strings(json.GetProperty("created")), path => Assert.True(Path.IsPathFullyQualified(path)));
        Assert.Contains(strings(json.GetProperty("warnings")), warning => warning.Contains("BrandsController not found"));
        // Human readable progress still goes somewhere useful, just not to stdout.
        Assert.Contains("Dynamic Query codes have been generated", result.StandardError);
    }

    [Fact]
    public async Task Conflict_reports_the_existing_files()
    {
        using SampleProject project = new();
        project.Write(_dynamicQueryFile, "// hand written");

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "dynamic-query", "Brand", "--json");

        Assert.Equal(CliExitCodes.Conflict, result.ExitCode);
        JsonElement error = parse(result).GetProperty("error");
        Assert.Equal("conflict", error.GetProperty("type").GetString());
        Assert.Contains(Path.GetFullPath(project.Combine(_dynamicQueryFile)), strings(error.GetProperty("paths")));
    }

    [Fact]
    public async Task Missing_argument_is_a_usage_error_and_never_prompts()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "crud", "--json");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        JsonElement json = parse(result);
        Assert.Equal("generate crud", json.GetProperty("command").GetString());
        Assert.Equal("usage", json.GetProperty("error").GetProperty("type").GetString());
        Assert.Contains("Brand, Model", json.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Unknown_option_is_reported_as_json()
    {
        using SampleProject project = new();

        NArchGenResult result = await NArchGenProcess.RunAsync(project.Path, "generate", "crud", "--forse", "--json");

        Assert.Equal(CliExitCodes.UsageError, result.ExitCode);
        Assert.Equal("usage", parse(result).GetProperty("error").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Upgrade_dry_run_reports_planned_changes_without_writing_them()
    {
        string solutionPath = Directory.CreateTempSubdirectory("nArchGen-cli-").FullName;
        try
        {
            copyDirectory(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v1-sample"), solutionPath);

            NArchGenResult result = await NArchGenProcess.RunAsync(solutionPath, "upgrade", "--dry-run", "--json");

            Assert.Equal(CliExitCodes.Success, result.ExitCode);
            JsonElement json = parse(result);
            Assert.Equal("upgrade", json.GetProperty("command").GetString());
            Assert.Empty(strings(json.GetProperty("updated")));
            JsonElement details = json.GetProperty("details");
            Assert.True(details.GetProperty("dryRun").GetBoolean());
            Assert.NotEmpty(strings(details.GetProperty("changesPlanned")));
            Assert.True(File.Exists(details.GetProperty("reportFile").GetString()));
        }
        finally
        {
            Directory.Delete(solutionPath, recursive: true);
        }
    }

    private static void copyDirectory(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string targetFile = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile);
        }
    }
}
