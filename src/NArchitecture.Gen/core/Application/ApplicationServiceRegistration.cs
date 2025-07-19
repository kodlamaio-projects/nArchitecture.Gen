using System.Reflection;
using NArchitecture.Gen.Application.Features.CodeGeneration.Rules;
using Core.CodeGen.TemplateEngine;
using Core.CodeGen.TemplateEngine.Scriban;
using Microsoft.Extensions.DependencyInjection;

namespace NArchitecture.Gen.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddSingleton<ITemplateEngine, TemplateEngine>();

        services.AddSingleton<GenerateBusinessRules>();

        return services;
    }
}
