using Core.CodeGen.Code.CSharp.ValueObjects;
using Core.CodeGen.TemplateEngine;
using Core.CodeGen.TemplateEngine.Scriban;
using Domain.ValueObjects;

namespace NArchGen.Templates.Tests;

/// <summary>
/// Renders every Scriban template with representative data and checks the output is free of the removed
/// MediatR/AutoMapper dependencies and structurally sound. It does not compile the output; that happens
/// end-to-end against the starter project.
/// </summary>
public class TemplateRenderingTests
{
    private static readonly ScribanTemplateRenderer _renderer = new();
    private static readonly string[] _forbiddenFragments = ["using MediatR;", "using AutoMapper;", "IMapper", "_mapper", "AddMediatR"];

    private static string templatesRoot => Path.Combine(AppContext.BaseDirectory, Domain.Constants.Templates.Paths.Root);

    private static Entity brand() =>
        new()
        {
            Name = "Brand",
            IdType = "Guid",
            Properties =
            [
                new PropertyInfo("Name", "string", "public"),
                new PropertyInfo("Discount", "decimal?", "public"),
                new PropertyInfo("Category", "Category", "public", "Domain.Enums"),
            ],
        };

    private static CrudTemplateData crud(bool flags) =>
        new()
        {
            Entity = brand(),
            DbContextName = "BaseDbContext",
            IsCachingUsed = flags,
            IsLoggingUsed = flags,
            IsTransactionUsed = flags,
            IsSecuredOperationUsed = flags,
            IsDynamicQueryUsed = flags,
            IsMenuRoleIncluded = flags,
            OperationClaimType = flags ? OperationClaimType.Guid : OperationClaimType.Numeric,
            OperationClaimTypeString = flags ? "Guid" : "Numeric",
        };

    private static DynamicQueryTemplateData dynamicQuery(bool flags) =>
        new()
        {
            Entity = brand(),
            IsCachingUsed = flags,
            IsLoggingUsed = flags,
            IsSecuredOperationUsed = flags,
        };

    private static CommandTemplateData command(bool flags) =>
        new()
        {
            CommandName = "SyncBrand",
            FeatureName = "Brands",
            EndPointMethod = "HttpPost",
            IsCachingUsed = flags,
            IsLoggingUsed = flags,
            IsTransactionUsed = flags,
            IsSecuredOperationUsed = flags,
        };

    private static QueryTemplateData query(bool flags) =>
        new()
        {
            QueryName = "GetBrandByName",
            FeatureName = "Brands",
            IsCachingUsed = flags,
            IsLoggingUsed = flags,
            IsSecuredOperationUsed = flags,
        };

    private static IEnumerable<string> templatesUnder(string folder) =>
        Directory
            .EnumerateFiles(Path.Combine(templatesRoot, folder), $"*.{_renderer.TemplateExtension}", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(templatesRoot, path))
            .Order();

    public static TheoryData<string, bool> Cases()
    {
        TheoryData<string, bool> data = [];
        foreach (string folder in new[] { "CRUD", "DynamicQuery", "Command", "Query" })
        foreach (string template in templatesUnder(folder))
        foreach (bool flags in new[] { true, false })
            data.Add(template, flags);
        return data;
    }

    private static ITemplateData dataFor(string relativeTemplatePath, bool flags) =>
        relativeTemplatePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] switch
        {
            "CRUD" => crud(flags),
            "DynamicQuery" => dynamicQuery(flags),
            "Command" => command(flags),
            "Query" => query(flags),
            string other => throw new InvalidOperationException($"No template data for '{other}'."),
        };

    [Fact]
    public void Templates_are_shipped_next_to_the_assembly()
    {
        Assert.True(Directory.Exists(templatesRoot), $"Templates not found at {templatesRoot}");
        Assert.NotEmpty(Cases());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Template_renders_without_removed_dependencies(string relativeTemplatePath, bool flags)
    {
        string template = await File.ReadAllTextAsync(
            Path.Combine(templatesRoot, relativeTemplatePath),
            TestContext.Current.CancellationToken
        );

        string rendered = await _renderer.RenderAsync(template, dataFor(relativeTemplatePath, flags));

        Assert.False(string.IsNullOrWhiteSpace(rendered), "Rendered output is empty.");
        Assert.DoesNotContain("{{", rendered);
        foreach (string fragment in _forbiddenFragments)
            Assert.DoesNotContain(fragment, rendered);

        if (relativeTemplatePath.EndsWith(".cs.sbn", StringComparison.OrdinalIgnoreCase))
            Assert.Equal(rendered.Count(c => c == '{'), rendered.Count(c => c == '}'));
    }

    [Fact]
    public async Task Crud_mapper_covers_every_handler_mapping()
    {
        string template = await File.ReadAllTextAsync(
            Path.Combine(templatesRoot, "CRUD", "Folders", "Application", "Features", "PLURAL_ENTITY", "Mappers", "ENTITYMapper.cs.sbn"),
            TestContext.Current.CancellationToken
        );

        string rendered = await _renderer.RenderAsync(template, crud(flags: true));

        Assert.Contains("[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]", rendered);
        Assert.Contains("public static partial class BrandMapper", rendered);
        foreach (
            string method in new[]
            {
                "Brand ToEntity(CreateBrandCommand command)",
                "CreatedBrandResponse ToCreatedResponse(Brand entity)",
                "void UpdateEntity(UpdateBrandCommand command, Brand entity)",
                "UpdatedBrandResponse ToUpdatedResponse(Brand entity)",
                "DeletedBrandResponse ToDeletedResponse(Brand entity)",
                "GetByIdBrandResponse ToGetByIdResponse(Brand entity)",
                "GetListBrandListItemDto ToGetListItemDto(Brand entity)",
                "GetListResponse<GetListBrandListItemDto> ToGetListResponse(IPaginate<Brand> paginate)",
            }
        )
            Assert.Contains(method, rendered);
    }

    [Fact]
    public async Task Create_handler_uses_static_mapper_and_core_mediator()
    {
        string template = await File.ReadAllTextAsync(
            Path.Combine(
                templatesRoot,
                "CRUD",
                "Folders",
                "Application",
                "Features",
                "PLURAL_ENTITY",
                "Commands",
                "Create",
                "CreateENTITYCommand.cs.sbn"
            ),
            TestContext.Current.CancellationToken
        );

        string rendered = await _renderer.RenderAsync(template, crud(flags: true));

        Assert.Contains("using NArchitecture.Core.Mediator;", rendered);
        Assert.Contains("using Application.Features.Brands.Mappers;", rendered);
        Assert.Contains("Brand brand = BrandMapper.ToEntity(request);", rendered);
        Assert.Contains("BrandMapper.ToCreatedResponse(brand)", rendered);
        Assert.Contains("ISecuredRequest, ICacheRemoverRequest, ILoggableRequest, ITransactionalRequest", rendered);
        Assert.Contains("using Domain.Enums;", rendered);
    }
}
