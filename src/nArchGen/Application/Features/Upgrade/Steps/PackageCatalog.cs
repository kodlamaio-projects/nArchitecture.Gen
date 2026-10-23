namespace Application.Features.Upgrade.Steps;

/// <summary>Package versions that nArchitecture 2.0 projects use. Single source of truth for the project-file steps.</summary>
public static class PackageCatalog
{
    public const string CoreVersion = "2.0.0";
    public const string TargetFramework = "net10.0";
    public const string SdkVersion = "10.0.100";

    public static readonly string[] RemovedPackages =
    [
        "MediatR",
        "MediatR.Contracts",
        "MediatR.Extensions.Microsoft.DependencyInjection",
        "AutoMapper",
        "AutoMapper.Extensions.Microsoft.DependencyInjection",
    ];

    /// <summary>Packages the web SDK already brings in; an explicit reference only produces NU1510.</summary>
    public static readonly string[] RedundantInWebProjects = ["Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Logging"];

    public static readonly string[] RemovedTestPackages =
    [
        "xunit",
        "xunit.core",
        "xunit.assert",
        "Microsoft.NET.Test.Sdk",
        "coverlet.collector",
    ];

    public static readonly (string Id, string Version)[] TestPackages = [("xunit.v3", "4.0.2"), ("xunit.runner.visualstudio", "4.0.1")];

    public const string XunitDependencyInjectionVersion = "12.0.1";

    public const string MediatorPackage = "NArchitecture.Core.Mediator";
    public const string MapperlyPackage = "Riok.Mapperly";
    public const string MapperlyVersion = "4.3.1";

    /// <summary>Returns the 2.0 version for a package, or null when the package is not managed by the upgrade.</summary>
    public static string? ResolveVersion(string packageId, bool upgradeTestFramework)
    {
        if (packageId.StartsWith("NArchitecture.Core.", StringComparison.OrdinalIgnoreCase))
            return CoreVersion;
        if (packageId.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
            return "10.0.13";
        if (packageId.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase))
            return "10.0.13";
        if (packageId.Equals("Microsoft.AspNetCore.Authentication.JwtBearer", StringComparison.OrdinalIgnoreCase))
            return "10.0.13";
        if (packageId.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase))
            return "10.0.13";
        if (packageId.StartsWith("Swashbuckle.AspNetCore", StringComparison.OrdinalIgnoreCase))
            return "10.3.1";
        if (packageId.Equals("System.IdentityModel.Tokens.Jwt", StringComparison.OrdinalIgnoreCase))
            return "8.23.0";
        if (packageId.StartsWith("Microsoft.IdentityModel.", StringComparison.OrdinalIgnoreCase))
            return "8.23.0";
        if (packageId.Equals("CloudinaryDotNet", StringComparison.OrdinalIgnoreCase))
            return "1.29.3";
        if (packageId.Equals("Moq", StringComparison.OrdinalIgnoreCase))
            return "4.21.0";
        if (packageId.Equals("FluentValidation", StringComparison.OrdinalIgnoreCase))
            return "12.2.0";
        if (packageId.Equals("FluentValidation.DependencyInjectionExtensions", StringComparison.OrdinalIgnoreCase))
            return "12.2.0";
        if (packageId.Equals("Xunit.DependencyInjection", StringComparison.OrdinalIgnoreCase) && upgradeTestFramework)
            return XunitDependencyInjectionVersion;
        return null;
    }

    public static readonly (string Id, string Version, string Command)[] Tools =
    [
        ("dotnet-ef", "10.0.13", "dotnet-ef"),
        ("csharpier", "1.3.0", "csharpier"),
        ("roslynator.dotnet.cli", "1.0.1", "roslynator"),
        ("narchitecture.gen", "2.0.0", "nArchGen"),
    ];
}
