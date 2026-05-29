# CLAUDE.md

## Project Overview

GitDelta is a lightweight, native Windows 11, read-only Git diff and commit viewer (Pure WPF, .NET 10). It shows working-tree-vs-HEAD, single-commit-vs-parent, and any-two-commit diffs with a modern Fluent UI. It is NOT a Git client: no staging, committing, or any write operation. Architecture: **Pure WPF**.

## Build & Run

```shell
dotnet build GitDelta.sln
dotnet test tests/
dotnet run --project src/GitDelta.UI/
```

The published executable is named `gitdelta.exe` (AssemblyName `gitdelta` in `GitDelta.UI.csproj`).

## Architecture

**Type:** Pure WPF

- `src/GitDelta.Core` — WPF-free classlib (`net10.0`): `IGitReader`/`CliGitReader`, git output parsers, `IIntraLineDiffer` (DiffPlex), repo discovery, CLI arg routing, settings persistence, domain models.
- `src/GitDelta.UI` — WPF exe (`net10.0-windows10.0.26100.0`, `gitdelta.exe`): App bootstrap + CLI arg routing + global exception handlers, FluentWindow shell with a 3-pane workspace, start screen, ViewModels, and the AvalonEdit-based DiffView control.
- MVVM with CommunityToolkit.Mvvm source generators
- Autofac DI with Module pattern (one module per layer)
- IHost bootstrapping with AutofacServiceProviderFactory
- WPF-UI FluentWindow with Mica/Acrylic backdrop

**Layering:** `UI -> Core`; Core never references UI. Services are `SingleInstance`; ViewModels are `InstancePerDependency`. Constructor injection only.

**Reference template:** `C:/code/template-projects/desktop-development-template/wpf-development-template.md`

## Git access

GitDelta shells out to the `git` CLI (no LibGit2Sharp) via `IGitProcessRunner`. Requires Git for Windows >= 2.30 on PATH. All path-bearing git output uses `-z` (NUL-framed) with `-c core.quotepath=false` and `--no-optional-locks`.

## Key Conventions

- File-scoped namespaces
- Private fields: `_camelCase`, public members: PascalCase
- Test naming: `MethodName_Scenario_ExpectedResult`
- No logic in View code-behind — all in ViewModels
- Constructor injection only — no service locator
- `ConfigureAwait(false)` in Core/library code; never `.Result`/`.Wait()` on the UI thread
- No `async void` except event handlers
- `DynamicResource` for all theme colors (never StaticResource or hardcoded hex)
- 4 spaces C#, 2 spaces XAML/XML/csproj, CRLF line endings

## Testing

- xUnit v3 + NSubstitute + Shouldly
- Run: `dotnet test tests/`
- Core: parsers, diff enrichment, repo discovery, CLI arg routing (canned git output).
- UI: ViewModel behavior with `IGitReader` mocked. No tests against live AvalonEdit.

## Environment

- .NET 10 SDK
- Windows 11 SDK (10.0.26100)
- Git for Windows >= 2.30 on PATH (runtime dependency)

## Build Gate

`dotnet build` must produce zero warnings (`TreatWarningsAsErrors=true`), and `dotnet test tests/` must pass before every commit.
