---
name: evergreen-build-validate
description: Build, test, format, and validate Evergreen changes, including published NativeAOT behavior and architecture coverage.
---

# Build and validate Evergreen

Read [the contributor workflow](../../../docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md) and the build, formatting, and NativeAOT requirements in [AGENTS.md](../../../AGENTS.md).

## Select validation

Inspect `git status --short` first. Identify changed projects and their consumers; preserve unrelated edits, including when formatters touch them. Use `global.json` for the SDK and `.github/workflows/dotnet-test.yml` for the current CI implementation.

- For documentation-only work, validate links, examples, and skill metadata. A full application build is unnecessary.
- For behavior changes, choose tests that observe the changed behavior. Use the Windows solution for Windows integration and the Avalonia solution for portable behavior.
- For serialization, startup, XAML loading, package-manager, IPC, interop, or other shipping runtime changes, validate the relevant NativeAOT publish path and exercise the published behavior. A Debug test pass does not establish publish safety.

## Windows commands

Run from the repository root; check each exit code before continuing:

```powershell
dotnet restore src/UniGetUI.Windows.slnx
dotnet format whitespace src --folder
dotnet format style src/UniGetUI.Windows.slnx --no-restore
dotnet format whitespace src --folder --verify-no-changes --verbosity minimal
dotnet format style src/UniGetUI.Windows.slnx --no-restore --verify-no-changes --verbosity minimal
dotnet test src/UniGetUI.Windows.slnx --no-restore --verbosity q --nologo /p:Platform=x64
```

For NativeAOT validation, use the checked-in profile:

```powershell
dotnet publish src/UniGetUI.Avalonia/UniGetUI.Avalonia.csproj --configuration Release --runtime win-x64 --self-contained true /p:Platform=x64 /p:PublishProfile=Win-x64-NativeAot
```

Use `win-arm64`, `Platform=arm64`, and `Win-arm64-NativeAot` for Windows ARM64. Portable profiles are under `src/UniGetUI.Avalonia/Properties/PublishProfiles/`; use a compatible host/toolchain. Do not change the support matrix just to pass a local check.

## Evaluate the result

Inspect publish diagnostics for `IL2xxx` and `IL3xxx`; fix concrete trimming/AOT defects rather than adding blanket suppressions. Verify the produced executable and changed runtime path, not only command success. JSON metadata must remain source generated.

Report commands, exit status, relevant tests, publish target, and runtime checks. Distinguish compilation from execution on the target architecture. Record unavailable prerequisites, pre-existing failures, and untested RIDs as gaps, never passes.
