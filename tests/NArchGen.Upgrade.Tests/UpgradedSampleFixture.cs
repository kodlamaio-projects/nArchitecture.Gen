using Application.Features.Upgrade;
using Application.Features.Upgrade.Steps;

namespace NArchGen.Upgrade.Tests;

/// <summary>
/// Copies the committed 1.x sample (<c>Fixtures/v1-sample</c>, generated with nArchGen 1.2.2 and extended with a
/// configuration-heavy AutoMapper profile) to a temp folder and runs every in-process upgrade step over it once.
/// </summary>
public sealed class UpgradedSampleFixture : IAsyncLifetime
{
    public static string SamplePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "v1-sample");

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "narchgen-upgrade-" + Guid.NewGuid().ToString("N")[..8]);

    public UpgradeReport Report { get; private set; } = new();

    public UpgradeFileSet Files { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        CopyDirectory(SamplePath, Root);
        UpgradeContext context = await RunStepsAsync(Root, new UpgradeOptions { SolutionPath = Root, ProjectName = "v1Sample" });
        await context.Files.FlushAsync();
        Report = context.Report;
        Files = context.Files;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
        return ValueTask.CompletedTask;
    }

    public string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    public bool Exists(string relativePath) =>
        File.Exists(Path.Combine(Root, relativePath)) || Directory.Exists(Path.Combine(Root, relativePath));

    /// <summary>Runs the analysis and rewrite steps (no git check, no formatter, no build) against a solution folder.</summary>
    public static async Task<UpgradeContext> RunStepsAsync(string solutionPath, UpgradeOptions options)
    {
        UpgradeContext context = new()
        {
            Options = options,
            Files = await UpgradeFileSet.LoadAsync(solutionPath),
            Report = new UpgradeReport { DryRun = options.DryRun },
        };
        IUpgradeStep[] steps =
        [
            new AnalyzeStep(),
            new ProjectFilesStep(),
            new MediatorStep(),
            new AutoMapperStep(),
            new TestsStep(),
            new WebApiStep(),
        ];
        foreach (IUpgradeStep step in steps)
            await step.ApplyAsync(context, CancellationToken.None);
        return context;
    }

    public static void CopyDirectory(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
