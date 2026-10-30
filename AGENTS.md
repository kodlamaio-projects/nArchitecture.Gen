# AGENTS.md

This file provides guidance to AI coding agents (Claude Code, Codex, Cursor, Copilot, …) when working with code in this repository.

`nArchGen` (NuGet package `NArchitecture.Gen`) is a .NET 10 global tool that scaffolds and upgrades solutions built on the
[nArchitecture](https://github.com/kodlamaio-projects/nArchitecture) starter: `new`, `generate crud|command|query|dynamic-query`,
`upgrade` (1.x → 2.0) and `ai init` (AI agent instructions for the solution). Generated code targets `NArchitecture.Core.*` 2.0, `NArchitecture.Core.Mediator` and Mapperly.

## Commands

```bash
dotnet restore
dotnet build
dotnet test                                   # all tests (Microsoft Testing Platform, set in global.json)
dotnet test --project tests/NArchGen.Templates.Tests --filter-method "*Crud_mapper*"   # single test (also --filter-class)
dotnet tool restore                           # csharpier + roslynator
dotnet csharpier format .                     # format; CI runs `dotnet csharpier check .` and fails on unformatted code
dotnet roslynator analyze

# Run the CLI from source; it works on the current directory, so run it inside a target nArchitecture solution
dotnet run --project <path-to-this-repo>/src/nArchGen/ConsoleUI -- generate crud User BaseDbContext --no-interactive
```

If the matching `NArchitecture.Core.*` version is not on nuget.org yet, pack the Core repo locally and restore with
`--source https://api.nuget.org/v3/index.json --source ../nArchitecture.Core/artifacts/packages`.

## Architecture

The tool is itself structured like an nArchitecture project:

- `src/nArchGen/ConsoleUI` – Spectre.Console.Cli entry point (`Program.cs` registers commands). Each `*CliCommand` collects
  input from its `*Settings` (prompting only when interactive), sends a stream request through `IMediator.CreateStream`, and
  prints the `CurrentStatusMessage` / `LastOperationMessage` progress it yields.
- `src/nArchGen/Application/Features` – the real work as `IStreamRequest` handlers (`NArchitecture.Core.Mediator`):
  `Create` (new project), `Generate` (crud/command/query/dynamic-query), `Upgrade` and `Ai` (`ai init`).
- `src/nArchGen/Domain` – template data value objects (`*TemplateData`) and the Scriban templates in `Domain/Templates`.
- `src/corePackages` – in-repo helper libraries: `Core.CodeGen` (Scriban template engine, Roslyn-based `CSharpCodeReader` /
  `CSharpCodeInjector`, process/git helpers), `Core.ConsoleUI` (DI bridge for Spectre), `Core.CrossCuttingConcerns`
  (`BusinessException`, `ConflictException`, `PlatformHelper.SecuredPathJoin`).

### Templates

- Each generator has a folder under `Domain/Templates/<Generator>/` with two parts:
  - `Folders/` mirrors the target solution layout. Each `*.sbn` file there becomes a new file. Upper-case path segments
    (`PLURAL_ENTITY`, `FEATURE`, `COMMAND`, …) are replaced with rendered names.
  - `Lines/` holds one-line snippets injected into existing files: `DbSet` properties, usings, service registrations,
    operation claim seeds.
- Templates are copied to the build output (`ContentWithTargetPath` in `Domain.csproj`) and read at runtime from
  `DirectoryHelper.AssemblyDirectory`. Always edit them under `src/nArchGen/Domain/Templates`. The copies under `bin/` are
  build output.
- `generate` handlers first compute every output path (`pathsOnly: true`), then fail with `ConflictException` if any already
  exists unless `--force` is passed. Injections are idempotent, so a forced regenerate does not duplicate registrations,
  seeds or endpoints. Keep both properties when adding a generator.

- `Templates/Ai` is different: AGENTS.md, the per-tool files, `Skills/` and `Rules/` are rendered from `AiTemplateData`,
  and their output paths come from `AiToolFiles` / `AiInstructions`, not from the template folder layout. Check a tool's
  current docs before changing where its files go.

### Upgrade

- `UpgradeToV2Command` runs an ordered list of `IUpgradeStep`s (`Features/Upgrade/Steps`) over an in-memory
  `UpgradeFileSet`. Every step `Id` can be passed to `--skip`.
- Files are only written after all rewrite steps have run. After that, `FormatStep` (csharpier) and `VerifyStep`
  (`dotnet build`) run.
- Anything a step cannot convert is added to `UpgradeReport.ManualWork`, which is rendered to `MIGRATION-REPORT.md`.
- The AutoMapper → Mapperly conversion uses Roslyn and lives in `Steps/AutoMapper`.

### CLI contract (scripts / AI agents)

- Nothing prompts when `--no-interactive` is passed, stdin is redirected or `CI` is set (`InteractiveCliCommandSettings`).
- Errors go to stderr. `CliExceptionHandler` maps exceptions to exit codes (`CliExitCodes`):
  - `UsageException` → 2
  - `ConflictException` → 3
  - `BusinessException` → 1
  - anything else → 4
- Throw the matching exception type instead of returning codes manually. The README documents these codes, so keep it in sync.

## Tests

- `tests/NArchGen.Templates.Tests` renders the shipped templates and asserts on the generated code: Mediator usage, Mapperly
  mappers and no removed dependencies.
- `tests/NArchGen.Upgrade.Tests` copies `Fixtures/v1-sample` to a temp folder and runs the in-process upgrade steps on it.
- `Fixtures/v1-sample` is a real solution generated by nArchGen 1.2.2. It is test data: excluded from compilation and from
  csharpier (`.csharpierignore`). Do not modernize, reformat or "fix" it.

## Conventions

- Formatting comes from csharpier (`printWidth` 140). Package versions are centralized in `Directory.Packages.props`. The tool
  version is set in `src/nArchGen/ConsoleUI/ConsoleUI.csproj`.
- Commits follow semantic commit messages: `<type>(<scope>): <subject>` with `feat`, `fix`, `docs`, `style`, `perf`, `refactor`,
  `test`, `build` or `ci` (see `docs/Semantic Commit Messages.md`).
- Pushing a `v*.*.*` tag triggers the release and NuGet publish workflows.
