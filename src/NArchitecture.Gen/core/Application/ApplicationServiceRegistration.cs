using System.Reflection;
using NArchitecture.Gen.Application.Features.CodeGeneration.Rules;
using NArchitecture.Gen.Application.Features.TemplateManagement.Services;
using NArchitecture.Gen.Domain.Features.TemplateManagement.DomainServices;
using Core.CodeGen.TemplateEngine;
using Core.CodeGen.TemplateEngine.Scriban;
using Microsoft.Extensions.DependencyInjection;
using NArchitecture.Core.Mediator;

namespace NArchitecture.Gen.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediator(Assembly.GetExecutingAssembly());

        services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddSingleton<ITemplateEngine, TemplateEngine>();
        services.AddSingleton<ITemplateService, TemplateService>();

        services.AddSingleton<GenerateBusinessRules>();

        return services;
    }
}
