using System.Text;
using NArchitecture.Gen.Application;
using NArchitecture.Gen.ConsoleUI.Features.CodeGeneration.Commands.Command;
using NArchitecture.Gen.ConsoleUI.Features.CodeGeneration.Commands.Crud;
using NArchitecture.Gen.ConsoleUI.Features.CodeGeneration.Commands.Query;
using NArchitecture.Gen.ConsoleUI.Features.ProjectManagement.Commands.New;
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
                .WithExample(args: new[] { "generate", "crud", "User", "BaseDbContext" });
            config
                .AddCommand<GenerateCommandCliCommand>(name: "command")
                .WithDescription(description: "Generate new command for a feature")
                .WithExample(args: new[] { "generate", "command", "SyncUser", "Users" });
            config
                .AddCommand<GenerateQueryCliCommand>(name: "query")
                .WithDescription(description: "Generate new query for a feature")
                .WithExample(args: new[] { "generate", "query", "GetUserByEmail", "Users" });
        }
    );

    config
        .AddCommand<CreateNewProjectCliCommand>(name: "new")
        .WithDescription(description: "Create a new project")
        .WithExample(args: new[] { "new", "ExampleProject" });

    #endregion
});

AnsiConsole.Write(new FigletText("nArchitecture").LeftJustified().Color(Color.Blue));

return app.Run(args);
