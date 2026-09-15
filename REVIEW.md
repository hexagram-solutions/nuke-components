# REVIEW.md

Reviewer guidance for changes in this repo. This is a shared NUKE build-component
library consumed by other repos' build projects — a regression here silently
breaks CI or publishing in every downstream repo, often without a compile error,
so review priorities skew toward behavioral correctness of the composed
`Target`/`Configure<>` pipelines over typical app-level concerns.

## Review priorities

1. **Settings composition order and correctness** in `src/Components/*.cs`.
   Each target builds settings via a chain like
   `.Apply(XSettingsBase).Apply(XSettings)` (see `ICompile.cs`, `IPack.cs`,
   `ITest.cs`, `IPush.cs`). Confirm a change doesn't reorder `Apply` calls in a
   way that lets the consumer's `XSettings` override something `XSettingsBase`
   depends on being final, and that new settings are added to the `*Base`
   variant (immutable, `sealed`) rather than requiring consumers to repeat
   boilerplate.
2. **`WhenNotNull(this as IOtherComponent, ...)` guards.** These are how
   optional cross-component behavior is wired (e.g. `ICompile` only sets
   version metadata if the consumer also implements `IHasVersioning`). A
   miscast or wrong interface here fails silently — the block is just skipped
   — rather than throwing, so double-check the interface named in `as` matches
   the interface whose members are being accessed.
3. **Changes to `.github/workflows/*.yml`.** These are hand-maintained (the
   NUKE `[GitHubActions]` generator was dropped because it can't emit
   `fetch-tags`). Confirm `checkout` keeps both `fetch-depth: 0` and
   `fetch-tags: true` — without tags, MinVer computes `0.0.0` and the `Push`
   target's `GitRepository.Tags.Any()` guard aborts the release. Confirm any
   newly added action is pinned to a commit SHA with a trailing version
   comment, matching the existing steps.

## Security-sensitive surfaces

- **`NuGetApiKey`** (`src/Components/IPush.cs`) — marked `[Secret]`, resolved
  from the `NuGetApiKey` parameter, which `release.yml` supplies from the
  `NUGET_API_KEY` repo secret. Any change to how this value is resolved,
  logged, or passed to `DotNetNuGetPush` needs scrutiny — `[Secret]` only
  redacts NUKE's own console/summary output, it does not prevent the value
  from leaking if someone adds a `Trace`/`Serilog` call or writes it to a
  report file.
- **`release.yml` / `IPush.Push`** — the only target in this repo with a
  real-world irreversible side effect (publishes a package to nuget.org, where
  a version can never be re-pushed). Changes to `Push`'s `Requires(() =>
  GitRepository.Tags.Any())` guard or to the release workflow's trigger
  (`on.push.tags`) deserve extra scrutiny — loosening either could cause an
  unintended publish.

## Invariants that must hold

- **Backward compatibility of the public interface surface.** Every `interface`
  under `src/Components` is the public API of the `Hexagrams.Nuke.Components` NuGet
  package. Adding a new member to an existing interface is source-breaking for
  any consumer that doesn't use default interface methods correctly, and
  renaming/removing a `Target`, parameter, or settings property is breaking for
  every downstream repo pinned to a version range. Flag any rename/removal on an
  existing public member and confirm it's an intentional major-version change.
- **Target names are part of the CLI contract.** NUKE maps a `Target` property
  name to a kebab-case CLI argument (e.g. `VerifyFormat` → `nuke verify-format`).
  Renaming a `Target` property changes the CLI surface for every consumer's
  `build.ps1`/`build.sh`/CI invocation.
- **`sealed` on `*SettingsBase` members** (e.g. `CompileSettingsBase` in
  `ICompile.cs`) is intentional — it keeps consumers from overriding the base
  configuration and only extending it via the paired `*Settings` hook. Don't
  suggest removing `sealed` here without recognizing this is deliberate.

## Known false positives

- **Interfaces with only default implementations and no state** (every file in
  `src/Components`) are correct NUKE usage, not dead abstractions — this is the
  documented [NUKE component pattern](https://nuke.build/docs/sharing/build-components),
  not over-engineering.
- **`this as IOtherComponent` casts** in `ICompile.cs`, `IPack.cs`, `ITest.cs`,
  etc. are the intended mechanism for optional inter-component wiring, not a
  code smell — don't flag them as needing a proper dependency-injection
  redesign.
- **Duplicated-looking `*SettingsBase`/`*Settings` pairs** (e.g.
  `CompileSettingsBase` and `CompileSettings` both being `Configure<...>`
  properties) are intentional — one is the sealed base, the other the
  consumer's extension point. Not redundant.

## Skip rules

- `docs/` (DocFX config/templates) and `.nuke/*.json` (schema/parameter cache)
  are tooling scaffolding, not hand-maintained content.
- Formatting/style is enforced by `nuke verify-format` in CI — don't flag
  whitespace or `dotnet format`-covered style issues.
