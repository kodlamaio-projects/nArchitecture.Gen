namespace NArchGen.Cli.Tests;

/// <summary>
/// The smallest project layout the generate commands accept: layer folders, a <c>Brand</c> and a <c>Model</c> entity,
/// a DbContext and a <c>Brands</c> feature. It is enough to reach the checks that run before any file is written.
/// </summary>
public sealed class SampleProject : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("nArchGen-cli-").FullName;

    public SampleProject()
    {
        foreach (string layer in new[] { "Application", "Domain", "Persistence", "WebAPI" })
            Directory.CreateDirectory(Combine(layer));

        foreach (string entity in new[] { "Brand", "Model" })
            Write(
                $"Domain/Entities/{entity}.cs",
                $$"""
                using NArchitecture.Core.Persistence.Repositories;

                namespace Domain.Entities;

                public class {{entity}} : Entity<Guid>
                {
                    public string Name { get; set; }
                }
                """
            );

        Write("Persistence/Contexts/BaseDbContext.cs", "namespace Persistence.Contexts;\n\npublic class BaseDbContext { }\n");
        Directory.CreateDirectory(Combine("Application/Features/Brands"));
    }

    public string Combine(string relativePath) => System.IO.Path.Combine(Path, relativePath);

    public void Write(string relativePath, string content)
    {
        string filePath = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
    }

    public string Read(string relativePath) => File.ReadAllText(Combine(relativePath));

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
