using System.Text;
using Application;
using ConsoleUI.Commands.Generate.Crud;
using ConsoleUI.Commands.New;
using Core.ConsoleUI.IoC.SpectreConsoleCli;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

#region Console Configuration

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

#endregion

#region IoC

IServiceCollection services = new ServiceCollection();
services.AddApplicationServices();
TypeRegistrar registrar = new(services);

#endregion

CommandApp app = new(registrar);
app.Configure(config =>
{
    #region Controller

    config.AddBranch(
        name: "generate",
        action: config =>
        {
            config.SetDescription("Generate project elements");
            config
                .AddCommand<GenerateCrudCliCommand>(name: "crud")
                .WithDescription(description: "Generate CRUD operations for new entity")
                .WithExample(args: new[] { "generate", "crud", "User" });
        }
    );

    config
        .AddCommand<CreateNewProjectCliCommand>(name: "new")
        .WithDescription(description: "Create a new project")
        .WithExample(args: new[] { "new", "ExampleProject" });

    #endregion
});

return app.Run(args);
