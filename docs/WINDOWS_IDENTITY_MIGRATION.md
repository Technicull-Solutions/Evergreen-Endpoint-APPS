# Evergreen Windows identity migration

This pass prepares the next testing build. Preserve the existing Evergreen installer AppId
so upgrades continue to find Build 5. Do not reuse UniGetUI's AppId or silently adopt its
files, settings, shortcuts, registry registrations, or PATH ownership.

| Surface | Next-build identity | Upgrade behavior |
|---|---|---|
| Fresh installation | `{autopf64}\Technicull Solutions\Evergreen Endpoint\Package Manager` | `UsePreviousAppDir=yes` retains the selected directory of existing Evergreen installations; no automatic folder move |
| Windows CLI | `evergreen.exe`, invoked as `evergreen` | Existing branded launcher retained; installer task and diagnostic messages now match it |
| PATH ownership | `Software\Technicull Solutions\Evergreen Endpoint\Package Manager\InstallerPath` | Only a matching, hashed record for a confirmed previous Evergreen install can move from the legacy key; unrelated records remain untouched |
| Portable marker | `EvergreenEndpoint.portable` beside the executable | Runtime recognizes the older marker for existing portable folders; setup removes the older marker only after writing the new one |
| Portable data | `Settings` beside the executable | Existing portable data stays in place |
| Update-in-progress marker | `.evergreen-endpoint-update-in-progress` | Runtime still recognizes the earlier marker; during an upgrade setup briefly writes both to protect older launchers, then removes both |
| Protocol | `evergreen-endpoint://` | Existing Evergreen registration retained |
| Startup entry | `EvergreenEndpointApps` | Existing Evergreen registration and user-disabled state retained |
| Catalog deployment | `%ProgramData%\Technicull Solutions\Evergreen Endpoint\Package Manager\SoftwareCatalog.json` | Existing separate catalog path retained |

`{autopf64}` follows Inno's installation scope: current-user programs for a per-user
install, or Program Files for an all-users install. Do not hard-code a user's Windows profile.
The PATH task remains optional. Regular installs use the appropriate user/machine PATH;
portable installs do not modify PATH. Open a new terminal after changing PATH.

The installer action strip uses forest `#0D3F30` across its full width, matching the brand
sidebar. It uses a separate panel with client style overrides so the native dark style
cannot turn the strip back into the page colour. Native button behavior and keyboard
focus remain in place. Manual light/dark/high-contrast and DPI inspection remains required.

## Verification

The isolated Inno PATH harness exercises add/remove, existing entries, registry string
types, interruption recovery, access denial, task selection, and per-install ownership
migration without writing the actual environment PATH. It also compiles the production
installer against inert fixture payloads. The release workflow runs it before packaging.

The NativeAOT installer smoke checks run only on a disposable runner. They cover fresh
installation, reinstall, upgrade from the last published installer, branded CLI startup,
portable mode, and uninstall alongside upstream registry/shortcut/process sentinels.
C# tests cover the new portable marker and legacy compatibility, and active/stale update
markers. A successful compile is not visual verification of the footer.

## Remaining coordinated work

The inherited employee per-user profile still resolves to the `UniGetUI` local data
directory, and the shared global configuration helper still exposes the inherited
Devolutions/UniGetUI location. Credentials, backup paths, temporary directories, IPC
identities, and legacy CLI/environment names need a coordinated inventory and migration.
Some of these paths contain another product's state, so do not move/delete or automatically
copy them based on the old name alone. Define explicit import/ownership rules and test
side-by-side behavior before relocating them. This installer pass does not claim that
runtime state separation is complete. Internal project/namespace names and license
notices are separate from the customer-facing identities above.
