# Evergreen application-update testing

Evergreen uses its owned ProductInfo channel. It does not fall back to an upstream update service.

## Sources of truth

- Metadata: [update/productinfo.json](../update/productinfo.json)
- Product key: `TechnicullSolutions.EvergreenEndpointPackageManager`
- Default URL: `https://raw.githubusercontent.com/Technicull-Solutions/PackageManager/main/update/productinfo.json`
- Windows updater policy: `HKLM\Software\Policies\Technicull Solutions\Evergreen Endpoint\Package Manager\Updater`
- Implementation: [AvaloniaAutoUpdater.cs](../src/UniGetUI.Avalonia/Infrastructure/AvaloniaAutoUpdater.cs)
- Release validation: [Windows Test Release](../.github/workflows/test-release.yml)

Do not configure or remove another product's registry keys, files, or update metadata when testing Evergreen.

## Unsigned device-testing releases

The current test installers are unsigned. Download and install them manually from [Evergreen releases](https://github.com/Technicull-Solutions/PackageManager/releases). Do not advertise an unsigned installer as a trusted automatic update or disable validation to make it install automatically.

The owned metadata may contain no installer files while automatic-update signing is unavailable. This is not a production automatic-update channel. Updating its version/build values does not configure signing or deploy trusted update assets.

The release workflow exercises fresh installation, same-version reinstallation, packaged NativeAOT startup, protocol activation, portable installation, and uninstall. When a previous published installer exists, it also installs that release, upgrades it, and checks the installed executable against the tested current executable. These tests run only on disposable GitHub-hosted Windows runners.

## Validate version and build information

Use `scripts/set-version.ps1` to stamp the intended version and build. It updates the application, installer inputs, and owned ProductInfo values together. Packaging rejects mismatched metadata.

The sidebar compares the running build with `scripts/BuildNumber` on this repository's main branch. Release-channel build values must not overwrite this comparison. A stale channel from an older version must not report a fictional number of main-branch builds behind.

Run the relevant regression tests from the repository root:

```powershell
dotnet test src/UniGetUI.Windows.slnx --filter "FullyQualifiedName~AutoUpdaterTests|FullyQualifiedName~BuildFreshnessTests|FullyQualifiedName~ReleaseStampTests" /p:Platform=x64
```

## Signed automatic-update validation

Use an isolated environment and an explicitly configured, organization-owned source containing trusted signed artifacts. Preserve HTTPS, architecture selection, installer hashes, publisher/signature checks, and downgrade protection. Release builds ignore developer validation bypasses. Follow the current source and [release skill](../.agents/skills/evergreen-release/SKILL.md) for supported policy inputs.

Record the exact current and target versions/builds, architecture, signer, hashes, metadata source, and observed outcome. Report unsupported platforms and untested upgrade paths as gaps.
