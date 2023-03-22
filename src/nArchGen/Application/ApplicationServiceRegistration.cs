using System.Reflection;
using Application.Features.Generate.Rules;
using Core.CodeGen.TemplateEngine;
using Core.CodeGen.TemplateEngine.Scriban;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(Assembly.GetExecutingAssembly());

        services.AddSingleton<ITemplateRenderer, ScribanTemplateRenderer>();
        services.AddSingleton<ITemplateEngine, TemplateEngine>();

        services.AddSingleton<GenerateBusinessRules>();

        return services;
    }
}
