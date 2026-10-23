using Application.Features.Upgrade;
using Application.Features.Upgrade.Steps;
using Core.CodeGen.CommandLine;
using Core.CrossCuttingConcerns.Exceptions;

namespace NArchGen.Upgrade.Tests;

public class UpgradeSampleTests(UpgradedSampleFixture sample) : IClassFixture<UpgradedSampleFixture>
{
    private const string _app = "src/v1Sample/Application";

    [Fact]
    public void ProjectFiles_TargetNet10AndSwapPackages()
    {
        string csproj = sample.Read($"{_app}/Application.csproj");
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", csproj);
        Assert.DoesNotContain("\"MediatR\"", csproj);
        Assert.DoesNotContain("\"AutoMapper\"", csproj);
        Assert.Contains("Include=\"NArchitecture.Core.Mediator\" Version=\"2.0.0\"", csproj);
        Assert.Contains("Include=\"Riok.Mapperly\" Version=\"4.3.1\" PrivateAssets=\"all\" ExcludeAssets=\"runtime\"", csproj);
        Assert.Contains("Include=\"NArchitecture.Core.Application\" Version=\"2.0.0\"", csproj);

        string webApi = sample.Read("src/v1Sample/WebAPI/WebAPI.csproj");
        Assert.Contains("Include=\"Swashbuckle.AspNetCore\" Version=\"10.3.1\"", webApi);
        Assert.DoesNotContain("Include=\"Microsoft.Extensions.Logging\"", webApi);
    }

    [Fact]
    public void ProjectFiles_CreateGlobalJsonAndUpdateTools()
    {
        string globalJson = sample.Read("global.json");
        Assert.Contains("\"version\": \"10.0.100\"", globalJson);
        Assert.Contains("Microsoft.Testing.Platform", globalJson);

        string tools = sample.Read(".config/dotnet-tools.json");
        Assert.Contains("\"csharpier\"", tools);
        Assert.Contains("\"1.3.0\"", tools);
        Assert.DoesNotContain("dotnet-csharpier", tools);
        Assert.Contains("\"narchitecture.gen\"", tools);

        string workflow = sample.Read(".github/workflows/dotnet.yml");
        Assert.Contains("global-json-file: global.json", workflow);
        Assert.Contains("actions/setup-dotnet@v4", workflow);
        Assert.Contains("dotnet csharpier check .", workflow);
    }

    [Fact]
    public void Mediator_UsingsAndRegistrationAreReplaced()
    {
        string registration = sample.Read($"{_app}/ApplicationServiceRegistration.cs");
        Assert.Contains("services.AddMediator(", registration);
        Assert.Contains("using NArchitecture.Core.Mediator.DependencyInjection;", registration);
        Assert.DoesNotContain("AddAutoMapper", registration);
        Assert.DoesNotContain("AddMediatR", registration);

        string handler = sample.Read($"{_app}/Features/Brands/Commands/Create/CreateBrandCommand.cs");
        Assert.Contains("using NArchitecture.Core.Mediator;", handler);
        Assert.DoesNotContain("using MediatR;", handler);
        Assert.DoesNotContain("MediatR", sample.Read("src/v1Sample/WebAPI/Controllers/BaseController.cs"));
    }

    [Fact]
    public void AutoMapper_HandlersCallStaticMappersAndLoseIMapper()
    {
        string create = sample.Read($"{_app}/Features/Brands/Commands/Create/CreateBrandCommand.cs");
        Assert.Contains("BrandMapper.ToEntity(request)", create);
        Assert.Contains("BrandMapper.ToCreatedResponse(", create);
        Assert.Contains("using Application.Features.Brands.Mappers;", create);
        Assert.DoesNotContain("IMapper", create);
        Assert.DoesNotContain("_mapper", create);
        Assert.DoesNotContain("using AutoMapper;", create);
        Assert.Contains("CreateBrandCommandHandler(IBrandRepository brandRepository,", create);

        string update = sample.Read($"{_app}/Features/Brands/Commands/Update/UpdateBrandCommand.cs");
        Assert.Contains("BrandMapper.UpdateEntity(request, brand", update);
        Assert.DoesNotContain("brand = brand;", update);
        Assert.Contains("BrandMapper.ToUpdatedResponse(", update);

        string list = sample.Read($"{_app}/Features/Brands/Queries/GetList/GetListBrandQuery.cs");
        Assert.Contains("BrandMapper.ToGetListResponse(", list);
    }

    [Fact]
    public void AutoMapper_GeneratedMapperCoversCrudAndHeavyConfiguration()
    {
        string mapper = sample.Read($"{_app}/Features/Brands/Mappers/BrandMapper.cs");
        Assert.Contains("[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]", mapper);
        Assert.Contains("public static partial class BrandMapper", mapper);
        Assert.Contains("public static partial Brand ToEntity(CreateBrandCommand source);", mapper);
        Assert.Contains("[MapperIgnoreTarget(nameof(Brand.CreatedDate))]", mapper);
        Assert.Contains("public static partial void UpdateEntity(UpdateBrandCommand source, Brand target);", mapper);
        Assert.Contains("public static partial GetListBrandListItemDto ToGetListItemDto(Brand source);", mapper);
        Assert.Contains(
            "public static partial GetListResponse<GetListBrandListItemDto> ToGetListResponse(IPaginate<Brand> source);",
            mapper
        );
        Assert.DoesNotContain("nameof(CreatedBrandResponse.Id)", mapper);

        // the hand-written, configuration-heavy profile is merged into the same mapper
        Assert.Contains("[MapProperty(nameof(Brand.Name), nameof(BrandSummaryDto.DisplayName))]", mapper);
        Assert.Contains("[MapperIgnoreTarget(nameof(BrandSummaryDto.Secret))]", mapper);
        Assert.Contains("TODO(narch-upgrade)", mapper);
        Assert.Contains("d.Label", mapper);
        Assert.Contains("public static partial BrandSummaryDto ToSummaryDto(Brand source);", mapper);
        Assert.Contains("public static partial List<BrandSummaryDto> ToSummaryDtoList(IEnumerable<Brand> source);", mapper);
        Assert.Contains("public static partial IQueryable<BrandSummaryDto> ProjectToSummaryDto(this IQueryable<Brand> source);", mapper);

        Assert.False(sample.Exists($"{_app}/Features/Brands/Profiles"), "profiles folder should be deleted");
    }

    [Fact]
    public void AutoMapper_ServicesAndPlaceholdersAreRewritten()
    {
        // the doc comment mentions IMapper on purpose: comments must not keep AutoMapper usings alive
        string service = string.Join(
            '\n',
            sample.Read($"{_app}/Services/BrandReports/BrandReportService.cs").Split('\n').Where(l => !l.TrimStart().StartsWith("///"))
        );
        Assert.Contains("BrandMapper.ToSummaryDto(brand)", service);
        Assert.Contains("BrandMapper.ToSummaryDtoList(brands)", service);
        Assert.Contains("BrandMapper.ProjectToSummaryDto(_brandRepository.Query())", service);
        Assert.DoesNotContain("IMapper", service);
        Assert.DoesNotContain("AutoMapper", service);
        Assert.Contains("BrandReportService(IBrandRepository brandRepository)", service);

        string sync = sample.Read($"{_app}/Features/Brands/Commands/SyncBrand/SyncBrandCommand.cs");
        Assert.Contains("new SyncBrandResponse()", sync);
        Assert.DoesNotContain("_mapper", sync);
    }

    [Fact]
    public void AutoMapper_RefreshTokenMapperKeepsQualifiedCoreType()
    {
        string mapper = sample.Read($"{_app}/Features/Auth/Mappers/RefreshTokenMapper.cs");
        Assert.Contains("ToEntity(NArchitecture.Core.Security.Entities.RefreshToken<Guid, Guid> source)", mapper);
        Assert.Contains("[MapperIgnoreTarget(nameof(RefreshToken.User))]", mapper);
        Assert.Contains("RefreshTokenMapper.ToEntity(", sample.Read($"{_app}/Services/AuthService/AuthManager.cs"));
    }

    [Fact]
    public void Tests_MockRepositoryAndHandlerConstructionLoseTheMapper()
    {
        const string tests = "tests/V1Sample.Application.Tests";
        Assert.Contains(
            "BaseMockRepository<IUserRepository, User, Guid, UserBusinessRules, UserFakeData>",
            sample.Read($"{tests}/Mocks/Repositories/UserMockRepository.cs")
        );
        string createTests = sample.Read($"{tests}/Features/Users/Commands/Create/CreateUserTests.cs");
        Assert.Contains("new CreateUserCommandHandler(MockRepository.Object, BusinessRules)", createTests);
        string loginTests = sample.Read($"{tests}/Features/Auth/Commands/Login/LoginTests.cs");
        Assert.DoesNotContain("IMapper", loginTests);
        Assert.DoesNotContain("MapperConfiguration", loginTests);
        Assert.DoesNotContain("using AutoMapper;", loginTests);

        string csproj = sample.Read($"{tests}/V1Sample.Application.Tests.csproj");
        Assert.Contains("Include=\"xunit.v3\"", csproj);
        Assert.Contains("<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>", csproj);
        Assert.DoesNotContain("Microsoft.NET.Test.Sdk", csproj);
        Assert.DoesNotContain("coverlet", csproj);
        Assert.Contains("Include=\"Xunit.DependencyInjection\" Version=\"12.0.1\"", csproj);
    }

    [Fact]
    public void WebApi_OpenApiNamespaceIsUpdated()
    {
        string program = sample.Read("src/v1Sample/WebAPI/Program.cs");
        Assert.Contains("using Microsoft.OpenApi;", program);
        Assert.DoesNotContain("Microsoft.OpenApi.Models", program);
    }

    [Fact]
    public void Report_OnlyTheExpressionMappingIsLeftToTheUser()
    {
        ManualWorkItem item = Assert.Single(sample.Report.ManualWork);
        Assert.Equal(ManualWorkKind.AutoMapper, item.Kind);
        Assert.Contains("d.Label", item.Reason);
        Assert.Contains("MediatR referenced", sample.Report.Analysis.Keys);
        Assert.Contains("mapping method(s)", sample.Report.Render(sample.Root));
    }

    [Fact]
    public void NoMediatROrAutoMapperReferenceSurvives()
    {
        IEnumerable<string> files = Directory
            .EnumerateFiles(sample.Root, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".csproj"))
            .Where(f => !f.Contains("MIGRATION-REPORT"));
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("using MediatR;", text);
            Assert.DoesNotContain("using AutoMapper", text);
            Assert.DoesNotContain("\"MediatR\"", text);
            Assert.DoesNotContain("\"AutoMapper\"", text);
        }
    }

    [Fact]
    public async Task Analyze_RefusesAnAlreadyUpgradedSolution()
    {
        UpgradeContext context = new()
        {
            Options = new UpgradeOptions { SolutionPath = sample.Root, ProjectName = "v1Sample" },
            Files = await UpgradeFileSet.LoadAsync(sample.Root),
            Report = new UpgradeReport(),
        };
        await Assert.ThrowsAsync<BusinessException>(() => new AnalyzeStep().ApplyAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task DryRun_LeavesTheDiskUntouched()
    {
        string root = Path.Combine(Path.GetTempPath(), "narchgen-upgrade-dry-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            UpgradedSampleFixture.CopyDirectory(UpgradedSampleFixture.SamplePath, root);
            UpgradeContext context = await UpgradedSampleFixture.RunStepsAsync(
                root,
                new UpgradeOptions
                {
                    SolutionPath = root,
                    ProjectName = "v1Sample",
                    DryRun = true,
                }
            );

            Assert.NotEmpty(context.Files.ChangedFiles);
            Assert.Contains(
                "using MediatR;",
                File.ReadAllText(Path.Combine(root, "src/v1Sample/Application/Features/Brands/Commands/Create/CreateBrandCommand.cs"))
            );
            Assert.False(File.Exists(Path.Combine(root, "src/v1Sample/Application/Features/Brands/Mappers/BrandMapper.cs")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task KeepTestFramework_LeavesXunit2InPlace()
    {
        string root = Path.Combine(Path.GetTempPath(), "narchgen-upgrade-keep-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            UpgradedSampleFixture.CopyDirectory(UpgradedSampleFixture.SamplePath, root);
            UpgradeContext context = await UpgradedSampleFixture.RunStepsAsync(
                root,
                new UpgradeOptions
                {
                    SolutionPath = root,
                    ProjectName = "v1Sample",
                    KeepTestFramework = true,
                }
            );
            string csproj = context.Files.Read(Path.Combine(root, "tests/V1Sample.Application.Tests/V1Sample.Application.Tests.csproj"));
            Assert.Contains("Include=\"xunit\" Version=\"2.7.0\"", csproj);
            Assert.DoesNotContain("xunit.v3", csproj);
            Assert.DoesNotContain("Microsoft.Testing.Platform", context.Files.Read(Path.Combine(root, "global.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>End-to-end: the upgraded sample must restore and build. Needs the 2.0.0 packages on a reachable feed, so it is opt-in.</summary>
    [Fact]
    public async Task EndToEnd_UpgradedSampleBuilds()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("NARCH_UPGRADE_E2E") == "1",
            "set NARCH_UPGRADE_E2E=1 (and a NuGet feed with NArchitecture.Core 2.0.0) to run"
        );

        string? nugetConfig = Environment.GetEnvironmentVariable("NARCH_UPGRADE_NUGET_CONFIG");
        if (nugetConfig is not null)
            File.Copy(nugetConfig, Path.Combine(sample.Root, "nuget.config"), overwrite: true);

        ProcessResult restore = await ProcessRunner.RunAsync("dotnet", "restore", sample.Root);
        Assert.True(restore.ExitCode == 0, restore.Output);
        ProcessResult build = await ProcessRunner.RunAsync("dotnet", "build --no-restore -c Release", sample.Root);
        Assert.True(build.ExitCode == 0, build.Output);
    }
}
