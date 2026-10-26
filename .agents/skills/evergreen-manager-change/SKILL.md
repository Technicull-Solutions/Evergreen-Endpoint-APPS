---
name: evergreen-manager-change
description: Change Evergreen package-manager adapters, package operations, or managed sources while preserving feed semantics and administrator policy.
---

# Change a package-manager integration

Read [the contributor workflow](../../../docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md) and the manager, NuGet-feed, and version-comparison sections of [AGENTS.md](../../../AGENTS.md).

## Locate the correct boundary

Evergreen's employee Windows profile supports WinGet (including `msstore`), Windows PowerShell, and PowerShell 7. Other adapters remain in the inherited source tree; their presence does not authorize enabling them in the product.

Inspect the relevant manager and its `Helpers/`, shared base classes under `src/UniGetUI.PackageEngine.PackageManagerClasses/`, and operation orchestration under `src/UniGetUI.PackageEngine.Operations/`. Trace loading and enablement before changing behavior. For managed sources, inspect `src/UniGetUI.Core.Settings/ManagedPackageManagerConfiguration.cs` and its tests.

## Preserve integration semantics

- Keep manager capabilities and helper behavior consistent. Account for source, architecture, user/machine scope, installed version, and restart/result handling.
- Preserve administrator-owned enablement, allowed sources, executable selection, and provisioning across every entry point. Do not expose an employee preference that bypasses deployment policy.
- Treat CLI output and feed content as untrusted. Use representative fixtures for spaces, Unicode, truncation, malformed rows, empty output, and exit codes. Preserve structured parsing when available.
- Use the ecosystem's `CompareVersions` implementation; do not globally change version ordering or use cross-manager display sorting for update decisions.
- PowerShell repository locations are CLI-owned identities. Preserve literal URLs, local folders, and UNC paths; follow shared NuGet local/V3/V2 selection without speculative probes or fabricated V2 fallback.
- Keep cancellation, timeouts, process disposal, and argument handling consistent with existing helpers. Do not launch real installs to validate parsing.

## Validate the changed contract

Use applicable tests in `src/UniGetUI.PackageEngine.Tests/`, `src/UniGetUI.Core.Tools.Tests/`, and `src/UniGetUI.Core.Settings.Tests/`. Cover the changed observable behavior: search/details identity, update direction, operation results, or source denial. For shared feed changes, test each affected protocol and consumer, including malformed/unlisted/local cases where relevant.

Run both C# formatters and relevant NativeAOT publish validation. Report fixtures, affected managers, policy checks, and runtime coverage. Distinguish a mocked adapter test from execution against an installed package manager.
