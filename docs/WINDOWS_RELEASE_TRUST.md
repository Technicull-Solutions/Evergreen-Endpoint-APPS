# Windows release trust and package contents

Build 3's public administrator executable is unsigned (`Get-AuthenticodeSignature` reports
`NotSigned`). The repository has no configured signing secrets. A passing build, GitHub
download, or matching checksum does not provide publisher authentication or Windows reputation.

Record the exact Windows message, detection name, affected file, and whether extraction or
launch was blocked. SmartScreen reputation, Smart App Control, Defender malware detection,
and an organization's application-control policy require different investigations. Microsoft's
[Smart App Control guidance](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/overview)
explains that unknown unsigned code can be blocked. A trusted signature is not a guarantee
that every security product or organization policy will accept a release.

## Signing prerequisite

Use a publisher-controlled, publicly trusted Windows code-signing certificate or signing
service. The publisher must provision its identity and credentials through the repository's
secure configuration, never in source, logs, issues, or chat. `scripts/sign.ps1` supports an
existing Azure Key Vault certificate through its mandatory tenant, vault, client, and
certificate parameters; tool installation does not import any root certificates.

The Windows Test Release workflow currently produces **unsigned** testing packages. Its
`publish` input defaults to false so verification can produce Actions artifacts without
creating a public release. A signed release needs a separate, verified signing integration:

1. Sign the final admin executable and employee application/helper binaries with the owned
   identity and a timestamp; verify Authenticode validity and expected signer identity.
2. Refresh the employee integrity tree after signing, before archiving and building the installer.
3. Sign the final installer and its uninstaller through the installer signing configuration.
4. Package and checksum the final bytes, verify extracted signatures and integrity, and run
   the published smoke tests. Re-test on a protected Windows device with download provenance.

Do not import test roots, weaken privileged-helper signer checks, disable Windows protections,
or present a repackaged unsigned binary as a trust fix. If Defender names a threat, investigate
that detection and use Microsoft's submission process as appropriate before distributing it.

## Package separation

- Device ZIPs exclude all PDB debug symbols, including transitive dependencies. Build 3's admin
  ZIP accidentally included them; they are unnecessary at runtime.
- The Windows command launcher is `evergreen.exe`; the controlled PowerShell operation
  wrapper is `Assets/Utilities/evergreen_ps_operation.ps1`. Installer upgrades retire the old
  names only within the selected Evergreen installation directory. Non-Windows launchers
  retain their existing name until a coordinated packaging migration.
- The admin client does not bundle Pinget or initialize package managers. The employee client
  currently bundles `pinget.exe` from `Devolutions.Pinget.Cli.Rust`, plus its managed library.
  WinGet uses it for CLI operations and installer metadata. Removing it requires a tested
  replacement; renaming a third-party binary would conceal its provenance rather than remove
  the dependency.
- `LICENSE`, `UPSTREAM.json`, and third-party license notices intentionally preserve attribution.
  Internal `UniGetUI.*` projects/namespaces and legacy employee state identities remain separate
  migration work; the portable administrator uses its own `AdminData` directory.

`scripts/verify-release-package.ps1` enforces required launcher paths, excludes symbols and
retired launcher names, and ensures the administrator ZIP contains only its own executable.
