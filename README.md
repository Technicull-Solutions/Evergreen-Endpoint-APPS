# Evergreen Endpoint | APPS

**Evergreen Endpoint Package Manager**, maintained by **Technicull Solutions**, provides the Evergreen Endpoint | APPS experience for organization-managed Windows software.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="src/UniGetUI.Avalonia/Assets/Brand/EEA-Logo-Text_Light-Alternate@2x.png">
  <img alt="Evergreen Endpoint | APPS" src="src/UniGetUI.Avalonia/Assets/Brand/EEA-Logo-Text_Dark@2x.png" width="321">
</picture>

[Download test releases](https://github.com/Technicull-Solutions/PackageManager/releases) · [Report an issue](https://github.com/Technicull-Solutions/PackageManager/issues/new/choose) · [Contribute](CONTRIBUTING.md)

## Install and test

Download the Windows x64 `.Setup.exe` from this repository's [releases](https://github.com/Technicull-Solutions/PackageManager/releases), or extract the application `.x64.zip` into a writable folder. Run `Evergreen Endpoint Package Manager.exe`. The application is self-contained and does not require a separate .NET installation.

The current releases are **unsigned device-testing prereleases**. Windows may show an unknown-publisher warning. Signed policy elevation is unavailable. Check the release notes for the exact supported architecture and limitations; ARM64, Linux, and macOS are not validated by the Windows x64 test-release workflow.

SHA-256 hashes are supplied in `checksums.x64.txt`. Optional Group Policy templates are provided in the Policies ZIP. See [policy deployment](package/Policies/README.md).

## Product scope

The Windows endpoint profile supports WinGet community packages, Microsoft Store through WinGet, Windows PowerShell repositories, and PowerShell 7 repositories. Administrators configure package managers and sources through Group Policy or managed JSON. Employee-facing controls do not grant administrator rights.

The source retains additional inherited adapters and cross-platform build profiles. Their presence does not establish support in the shipped Windows endpoint product.

## Documentation

- [Product direction and roadmap](docs/EVERGREEN_ENDPOINT_PACKAGE_MANAGER.md)
- [Portable administrator client and library deployment](docs/ADMIN_CLIENT.md)
- [Contributor workflow](docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md)
- [Command-line reference](docs/CLI.md)
- [Terminal interface](docs/TUI.md)
- [Portable mode](docs/PORTABLE.md)
- [IPC reference](docs/IPC.md)
- [Version and build numbering](docs/VERSIONING.md)
- [Security reporting](SECURITY.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Development

Use the checked-in .NET SDK configuration and [repository instructions](AGENTS.md). The Windows solution is `src/UniGetUI.Windows.slnx`; existing project names and namespaces are internal implementation identifiers.

```powershell
dotnet restore src/UniGetUI.Windows.slnx
dotnet test src/UniGetUI.Windows.slnx --verbosity q --nologo /p:Platform=x64
```

The maintained Windows packaging workflow is [Windows Test Release](.github/workflows/test-release.yml). Release stamping uses `scripts/set-version.ps1`, and packaging uses `scripts/build.ps1`. The inherited upstream release workflow is guarded against running in this fork.

## Attribution and license

Evergreen is a derivative of [UniGetUI](https://github.com/Devolutions/UniGetUI), created by Martí Climent and subsequently maintained by Devolutions. Its exact source baseline is recorded in [UPSTREAM.json](UPSTREAM.json). Upstream project links here acknowledge provenance; Evergreen downloads, support, contributions, and releases belong to this repository.

Retain the inherited [MIT license and notices](LICENSE) and bundled third-party license notices when redistributing the software.
