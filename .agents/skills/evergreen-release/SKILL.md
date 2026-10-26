---
name: evergreen-release
description: Prepare and verify Evergreen release versions, NativeAOT packages, installer upgrades, integrity, and updater compatibility.
---

# Prepare and verify an Evergreen release

Read [the contributor workflow](../../../docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md), [versioning](../../../docs/VERSIONING.md), and current release workflows. Preparing artifacts does not itself authorize publishing a GitHub release, uploading assets, or deploying update metadata; preserve the user's requested scope.

## Establish release inputs

Inspect the working tree, intended commit, version/build, architectures, signing availability, and destination channel. Use `scripts/set-version.ps1` for stamping rather than editing synchronized values by hand. Inspect its current parameters before invoking it.

Executable sources of truth:

- `scripts/build.ps1`: tests, NativeAOT publishing, staging, Windows installer, policy bundle, and artifact naming. It cleans/stages output; inspect paths before running it.
- `scripts/package-admin.ps1`: separate portable administrator NativeAOT package, documentation/policy staging, and published document self-test. `testing/automation/native-admin-smoke.ps1` checks the extracted executable's document path, desktop startup, and clean exit.
- `scripts/publish-nativeaot.ps1`: standalone Windows publish helper; its output directory is removed before publishing. Prefer the direct profile command when existing output must be preserved.
- `scripts/sign.ps1`, `scripts/generate-integrity-tree.ps1`, and `scripts/verify-integrity-tree.ps1`: inspect their interfaces and the release pipeline's signing/integrity order.
- `.github/workflows/test-release.yml` and `.github/workflows/build-release.yml`: current packaging and publication behavior. Dispatching a release workflow can publish externally.
- `UniGetUI.iss`, `package/Policies/`, and `src/UniGetUI.Avalonia/Properties/PublishProfiles/`: installer, deployment policies, and target profiles.

## Build and check

Run required tests and both format checks. A typical Windows packaging command from the root is:

```powershell
pwsh ./scripts/build.ps1 -Platform x64
```

Check prerequisites and script side effects first. Do not use `-SkipTests` to describe an artifact as tested without equivalent recorded results. Validate ARM64 separately when included.

Inspect the actual artifacts for product names, version/build, architecture, license notices, policy files, hashes, and expected executable names. The internal assembly name can differ from the staged customer-facing launcher. Check integrity after the final staging/signing transformations as required by the existing pipeline.

Exercise packaged CLI/help, desktop startup, and the changed behavior from the extracted/published output. In a disposable environment, test fresh installation, supported upgrade, portable mode, and uninstall without disturbing UniGetUI or another Evergreen installation.

## Verify update and trust behavior

Use current `AvaloniaAutoUpdater.cs`, `CoreData.cs`, and their tests for product key, metadata source, policy overrides, architecture selection, signature/hash checks, and restart behavior. Follow `testing/UPDATE-TESTING.md` for the owned update channel and unsigned-build limitations; confirm endpoints and registry keys against current source before running its examples.

Report artifact paths, checksums, signing status, exact tested upgrade paths, and architecture/runtime gaps. Label unsigned device-testing builds explicitly. Do not claim production readiness from a successful package build alone.
