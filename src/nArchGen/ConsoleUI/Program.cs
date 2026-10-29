using System.Text;
using Application;
using ConsoleUI;
using ConsoleUI.Commands.Generate.Command;
using ConsoleUI.Commands.Generate.Crud;
using ConsoleUI.Commands.Generate.DynamicQuery;
using ConsoleUI.Commands.Generate.Query;
using ConsoleUI.Commands.New;
using ConsoleUI.Commands.Upgrade;
using ConsoleUI.Output;
using Core.ConsoleUI.IoC.SpectreConsoleCli;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
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

CliOutput.Configure(args);

CommandApp app = new(registrar);
app.Configure(config =>
{
    config.SetExceptionHandler((exception, _) => CliExceptionHandler.Handle(exception, CliOutput.ErrorConsole));
    // Reject unknown options instead of ignoring them, so a mistyped flag such as --forse fails with exit code 2.
    config.UseStrictParsing();

    #region Controller

    config.AddBranch(
        name: "generate",
        action: config =>
        {
            config.SetDescription("Generate project elements");

            config
                .AddCommand<GenerateCrudCliCommand>(name: "crud")
                .WithDescription(description: "Generate CRUD operations for new entity")
                .WithExample(args: new[] { "generate", "crud", "User", "BaseDbContext" });
            config
                .AddCommand<GenerateCommandCliCommand>(name: "command")
                .WithDescription(description: "Generate new command for a feature")
                .WithExample(args: new[] { "generate", "command", "SyncUser", "Users" });
            config
                .AddCommand<GenerateQueryCliCommand>(name: "query")
                .WithDescription(description: "Generate new query for a feature")
                .WithExample(args: new[] { "generate", "query", "GetUserByEmail", "Users" });
            config
                .AddCommand<GenerateDynamicQueryCliCommand>(name: "dynamic-query")
                .WithDescription(description: "Generate dynamic query for an entity")
                .WithExample(args: new[] { "generate", "dynamic-query", "User" });
        }
    );

    config
        .AddCommand<CreateNewProjectCliCommand>(name: "new")
        .WithDescription(description: "Create a new project")
        .WithExample(args: new[] { "new", "ExampleProject" });

    config
        .AddCommand<UpgradeCliCommand>(name: "upgrade")
        .WithDescription(description: "Upgrade a nArchitecture 1.x solution to 2.0 (.NET 10, Core.Mediator, Mapperly)")
        .WithExample(args: new[] { "upgrade", "--dry-run" })
        .WithExample(args: new[] { "upgrade", "--project", "exampleProject" });

    #endregion
});

if (!CliOutput.IsJson && !Console.IsOutputRedirected)
    AnsiConsole.Write(new FigletText("nArchitecture").LeftJustified().Color(Color.Blue));

return app.Run(args);
