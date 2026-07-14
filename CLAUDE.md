# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

**Super Squad Among Us** is a client-side BepInEx IL2CPP addon mod for
[Among Us](https://store.steampowered.com/app/945360/Among_Us) that adds new custom roles on top of
[Town of Us: Mira](https://github.com/AU-Avengers/TOU-Mira) (TOU-Mira), which itself is built on
[MiraAPI](https://github.com/All-Of-Us-Mods/MiraAPI). This is **not** a fork of TOU-Mira — it's a
separate plugin that depends on the compiled MiraAPI + TownOfUsMira packages, and new roles are added
here, never by editing TOU-Mira source.

`reference/` (git-ignored, local only) contains full checkouts of MiraAPI and TOU-Mira. Use it to grep
base classes/interfaces this project depends on — it is not part of this repo and should never be
edited.

## Build

```
dotnet build SuperSquadAmongUs.sln
```

or via the Cake build script used in CI: `dotnet cake build.cake`.

- Target framework is `net6.0` (set in `Directory.Build.props`), IL2CPP/BepInEx via
  `BepInEx.Unity.IL2CPP` and `Reactor`.
- Package versions for `Reactor`, `AllOfUs.MiraAPI`, and `TownOfUsMira` are pinned in the root
  `AmongUs.props`. `RestorePackagesWithLockFile` is on, so `SuperSquadAmongUs/packages.lock.json` must
  stay in sync — run `dotnet restore --force-evaluate` after bumping a package version.
- To auto-deploy after building, set an `AmongUs` environment variable to your local Among Us install
  directory; the `Copy` target in `AmongUs.props` will symlink/copy the built DLL into
  `BepInEx/plugins/` automatically.
- There is no automated test suite (this is a client-side Unity/IL2CPP mod, not unit-testable in
  isolation). A successful `dotnet build` is the primary automated check; actual behavior needs manual
  verification in a running Among Us + BepInEx instance.
- StyleCop, SonarAnalyzer, and .NET analyzers are enabled (`Directory.Build.props`, `stylecop.json`,
  `stylecop.ruleset`). `GenerateDocumentationFile` is on, so every new public member without a `///`
  doc comment produces a CS1591 warning at build time — this is expected, pre-existing noise in this
  codebase, not something that needs to be silenced for a build to be considered good.

## Architecture

See **[docs/architecture.md](docs/architecture.md)** for how roles/buttons/options are wired up
(auto-registration via reflection — there's no manual list to edit) and the file-layout convention to
follow when adding a new role.

See **[docs/il2cpp-gotchas.md](docs/il2cpp-gotchas.md)** for IL2CPP/namespace pitfalls specific to this
codebase (constructor requirements, namespaces that don't match their source folder, locale load
ordering).

See **[docs/roles/](docs/roles)** for per-role design notes and known follow-ups — one file per role
(e.g. `docs/roles/apparater.md`). Check the relevant file before modifying an existing role.

## Keeping this memory up to date

When you learn something during a session that would help a future session — a gotcha, a base-game
quirk, a design decision and its rationale, a follow-up that still needs doing — write it down:

- Cross-cutting facts (build/tooling, IL2CPP quirks, conventions that apply to every role) go in this
  file or `docs/il2cpp-gotchas.md`.
- Anything specific to one role's design or implementation goes in `docs/roles/<name>.md` (create it
  if it doesn't exist yet).

Keep this root file short — push details into the linked docs and reference them here rather than
letting this file grow.
