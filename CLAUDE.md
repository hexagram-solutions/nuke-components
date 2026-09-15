# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with
code in this repository.

## Code Review

This section applies only when reviewing a pull request or diff — skip it during
normal development. When reviewing a change in this repo, read `REVIEW.md` at the
repo root and apply its guidance (review priorities, security-sensitive surfaces,
invariants, known false positives). Don't load it during ordinary authoring work.

## What this is

A shared [NUKE build system](https://nuke.build) component library, published as
the `Hexagrams.Nuke.Components` NuGet package. It provides reusable `Target`s
(`Compile`, `Test`, `Pack`, `Push`, `ReportCoverage`, etc.) as C# interfaces that
other repos' NUKE build projects implement, per NUKE's
[shared build components](https://nuke.build/docs/sharing/build-components)
pattern.

## Build System

This project uses NUKE to build itself (dogfooding its own components).

```powershell
nuke                # default build (Test, via Build.cs Main())
nuke compile        # compile only (runs VerifyFormat first)
nuke test           # run tests
nuke report-coverage
nuke pack           # create NuGet package
nuke verify-format  # check formatting (dotnet format --verify-no-changes)
nuke format         # fix formatting (dotnet format)
```

Install the NUKE global tool once, or use the generated `./build.ps1` /
`./build.cmd` / `./build.sh` scripts instead:

```powershell
dotnet tool restore
dotnet tool install nuke.globaltool -g
```

For a single test project, use `dotnet test` directly against the relevant
`*.Tests` project.

## Project Structure

- **`src/Components`** (`Hexagrams.Nuke.Components.csproj`) — the package source.
  Each build capability is a separate interface file (`ICompile.cs`, `ITest.cs`,
  `IPack.cs`, `IPush.cs`, `IReportCoverage.cs`, `IClean.cs`, `IRestore.cs`,
  `IFormat.cs`), plus supporting `IHas*` capability interfaces
  (`IHasSolution`, `IHasConfiguration`, `IHasArtifacts`, `IHasReports`,
  `IHasGitRepository`, `IHasVersioning`) and small statics
  (`ComponentExtensions.cs`, `ToolSettingsExtensions.cs`, `Configuration.cs`).
- **`build/`** — this repo's own NUKE build project (`Build.cs`), which
  consumes the components from `src/Components` to build/test/pack/push itself.
- **`samples/`** — three standalone, runnable NUKE build projects
  (`1-basic`, `2-format`, `3-test`), each with its own `.slnx` and `build/`,
  demonstrating one component in isolation. Their READMEs link to specific files
  under `src/Components/` — keep those links in sync if files move or are renamed.
- **`docs/`** — DocFX site config, published to GitHub Pages
  (`hexagram-solutions.github.io/nuke-components`) by `.github/workflows/publish-docs.yml`.

## Architecture: the component interface pattern

Every component is a C# interface with default interface method implementations,
inheriting `INukeBuild` (directly or via `IHasSolution`, etc.). A consuming
`Build` class implements one or more component interfaces and gets their
`Target`s and settings for free. Key conventions used throughout `src/Components`:

- Interfaces declare capability dependencies via inheritance, e.g.
  `ICompile : IRestore, IClean, IHasConfiguration` — implementing `ICompile`
  pulls in `Restore` and `Clean` targets automatically.
- Optional cross-component behavior is wired with `this as IOtherComponent`
  null checks (see `WhenNotNull` in `ToolSettingsExtensions.cs`) rather than
  hard dependencies, so components stay independently implementable — e.g.
  `ICompile` only sets version/repo-URL metadata `WhenNotNull(this as
  IHasVersioning, ...)`.
- Each target exposes a `*SettingsBase` (`sealed`, not meant to be overridden)
  and a `*Settings` (open `Configure<>` hook for consumers to extend) — see the
  `CompileSettingsBase`/`CompileSettings` pair in `ICompile.cs`.
- Components needing an external client tool document the required
  `<PackageDownload>` in an XML doc `<remarks>` block (see `IHasVersioning.cs`
  for MinVer, `IReportCoverage.cs` for ReportGenerator) rather than taking a
  package dependency themselves.

## SDK & Language

- .NET SDK pinned to `10.0.302` (`rollForward: latestMinor`) in `global.json`;
  `src/Components` and samples target `net10.0`.
- Central Package Management (`Directory.Packages.props`) — never edit
  `.csproj`/`Directory.Packages.props` directly; use `dotnet package add`/
  `dotnet package update`.
- Local tools pinned in `.config/dotnet-tools.json`: `nuke.globaltool`,
  `docfx`, `husky`, `dotnet-outdated-tool`. MinVer is consumed via the
  `minver-cli` `<PackageDownload>` in `build/_build.csproj`, not as a local tool.

## CI

- `.github/workflows/continuous.yml` — runs `nuke test` on Ubuntu on PRs and
  pushes to `main`/`release/v*`.
- `.github/workflows/release.yml` — on `v*` tags, runs test/pack/push, publishing
  to nuget.org with the `NUGET_API_KEY` repo secret.
- Both workflows are hand-maintained. NUKE's `[GitHubActions]` generator can't
  emit `fetch-tags`, so the generator was dropped in favor of hand-written YAML.
- `checkout` uses `fetch-depth: 0` **and** `fetch-tags: true` — MinVer needs the
  tags to compute the version, and the `IPush` target guards on their presence.
  All actions are pinned to a commit SHA with a trailing version comment.
- Husky.Net (`.husky/pre-push`) runs `dotnet nuke` (the default `Test` target)
  before every push.

## Conventions

- No `Async` suffix on `Task`-returning methods unless a sync equivalent exists
  (repo convention).
- XML doc `<summary>` tags are near-one-liners describing the "what"; use
  `<remarks>` for required external tooling or "why" context (see existing
  interfaces in `src/Components` for the pattern).
