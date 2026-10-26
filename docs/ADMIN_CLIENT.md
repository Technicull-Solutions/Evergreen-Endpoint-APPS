# Portable Evergreen Endpoint Admin

The administrator test client is a separate Windows x64 NativeAOT application. Extract the `EvergreenEndpointAdmin.*.x64.zip` release into a writable folder and run `Evergreen Endpoint Admin.exe`. A separate .NET installation is not required. Current test releases are unsigned.

## Appearance and identity

The window is branded **Evergreen Endpoint | ADMIN**, using the APPS forest sidebar/header,
page and card colours, gold navigation marker, Lato wordmark, and segmented view selector.
It retains the Evergreen package symbol and
a gold management-controls badge that distinguishes its executable, window, and taskbar icon
from APPS. **Appearance** offers **System**, **Light**, and **Dark**. System follows the operating
system theme; the selected preference is saved in `AdminData/appearance.txt` beside the portable
executable. The header uses the bundled Lato wordmark font; controls use Segoe UI Variable/Segoe UI.
Draft editors use a white body in light mode and ink in dark mode. Tab moves out of the JSON
editor so keyboard navigation does not become trapped.

The portable ZIP's `Brand/` folder includes the alternate administrator icon (ICO),
light/dark Lato wordmarks (PNG and outlined SVG), and the administrator symbol SVG.

## Configure your catalog repository

Use **Catalog source** to configure your environment's Gitea server, repository, revision,
and catalog path. Import/export a reusable source profile, enter a session-only read token
for private access, and **Fetch and validate catalog** into the library draft. Review and
export that draft for separate administrator deployment. See
[catalog source profiles](CATALOG_SOURCE_PROFILES.md) for the contract and authentication
boundaries. The ZIP includes `catalog-source.example.json` with placeholders only.

## Author and export a library

1. Open **Software libraries**. The initial draft contains an empty **Apps** library. **Cards** is the default; the selector offers **List**, **Cards**, **Icons**, and **JSON**, in that order.
2. Use **New library** to create a library, or open an existing library to **Add app**, **Edit library**, or remove draft entries. App forms include friendly name, exact package ID, manager, source, and optional version. Use `WinGet`, `PowerShell`, or `PowerShell7`; Microsoft Store applications use WinGet with source `msstore`. Editing preserves version pins. Removing an entry changes the draft, not installed software.
3. All four views share the same document, including imports from Catalog source. **JSON** is optional. Invalid JSON stays in the editor with an explanation when switching to a graphical view is blocked. Graphical forms validate required fields and duplicate identities before saving; a form cannot overwrite a draft replaced while it was open. **Validate and format** checks document structure and duplicate identities. It does not certify publisher identity, package availability, or authorization on a target device.
4. Select **Export JSON** and save `SoftwareCatalog.json` in your workspace. Exports replace only the explicitly chosen workspace file after the save dialog's overwrite confirmation. Opening another draft or closing the app prompts before discarding edits.

No broker or package-manager process is required for authoring. The utility does not install software, register sources, sign documents, connect to remote devices, or apply machine policy. It starts with in-memory drafts; save the files you want to carry between devices. Private settings and logs stay in `AdminData` beside the administrator executable, separately from the employee client and UniGetUI. An unwritable application folder or export destination reports an error and does not silently select another folder.

## Test the employee client

The updated employee client enables the library by default under **Apps**. **Search apps** searches configured package sources separately. `Ctrl+1` opens Apps; `Ctrl+5` opens Search apps. With no deployed library, Apps explains how to proceed. Administrative catalog editing is unavailable from employee navigation.

An administrator or deployment system must copy the exported library to:

`%ProgramData%\Technicull Solutions\Evergreen Endpoint\Package Manager\SoftwareCatalog.json`

Create that directory with standard-user read access and administrator/deployment-system write access. Reload Apps after deployment. Do not place the deployed library in an employee-writable directory. The old `%ProgramData%\Devolutions\UniGetUI\SoftwareCatalog.json` is not automatically adopted or modified; explicitly review and export any previous library to the new destination.

The library is a discovery document, not an authorization grant. Existing manager/source policy and package-operation authorization still apply. Unavailable sources are filtered by default; users can inspect unavailable entries through the library filters. Signed organization catalogs and remote catalog retrieval remain future capabilities.

For a self-hosted configuration repository, follow [Gitea catalog testing](GITEA_CATALOG_TESTING.md).
The portable ZIP includes that guide and a `SoftwareCatalog.example.json` seed. This pilot
uses reviewed Git revisions and local deployment; it does not require a Gitea token in Apps.

## Manager configuration and diagnostics

**Package manager configuration** opens, validates, and exports the current version 1 machine-JSON format. Only supported manager names are accepted; explicit executable paths must be absolute. Sources must already be provisioned by device deployment. Read [the deployment guide](../package/Policies/README.md) for Group Policy precedence, exact registry paths, and JSON deployment.

**Maximum concurrent uninstalls** defaults to **3** (range 1–100). Use **Set uninstall limit
in draft**, then export the JSON and deploy it separately. Opening or editing a draft does
not apply policy. This value also caps the number of apps in a desktop uninstall selection;
selections above it show an administrator-limit explanation rather than uninstalling a subset.
Group Policy takes precedence. Local diagnostics displays the effective value on this device.

**Local diagnostics** reads the local effective manager enablement and executable selection through the same provider as the employee client. It displays the machine configuration and library locations. It does not connect to a broker or prove that a manager executable/source is usable. Configuration-provider errors may fall back to its built-in defaults and are not a successful validation of the target configuration file.

Portable exports are unsigned drafts. The existing elevated policy-write protocol still requires signed binaries and a protected installation; this utility does not weaken that boundary.

## Validation and packaging

`src/Evergreen.Admin.UiTests` exercises the actual Avalonia control tree on its in-memory
headless backend: all four views, light/dark appearance, narrow layouts, graphical forms,
invalid JSON, removal confirmation, and stale-draft protection. Set
`EVERGREEN_ADMIN_UI_ARTIFACTS` to an output directory before `dotnet test` to save rendered
previews. See [Avalonia headless testing](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform).
These tests do not replace native Windows, Narrator, high-contrast, or published NativeAOT checks.

Read [Windows release trust and package contents](WINDOWS_RELEASE_TRUST.md) for the unsigned
testing-build limitation and the signing prerequisite. Windows may block an unsigned download
entirely; this portable utility does not require installation, but its executable still needs
to satisfy the device's security policy.

The release workflow tests the shared employee/admin document contract and publishes both clients. `scripts/package-admin.ps1` publishes the administrator executable with NativeAOT, runs its published `--self-test` document/export path, and packages the portable application with this guide, policy templates, and manager-configuration example. The release pipeline also verifies that its desktop window starts and exits on a disposable Windows runner. Check the release report for actual tested architecture and GUI coverage.
