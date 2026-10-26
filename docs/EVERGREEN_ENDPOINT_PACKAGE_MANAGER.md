# Evergreen Endpoint Package Manager

This document defines the product direction for the Technicull Solutions fork of UniGetUI. The project, installer, policies, and management utility are named **Evergreen Endpoint Package Manager**. Its employee-facing Windows experience is branded **Evergreen Endpoint | APPS**, alongside products such as **Evergreen Endpoint | Connect**.

## Product promise

Evergreen Endpoint | APPS gives employees a clear, safe place to find, install, and update organization-approved Windows applications. IT administrators manage the underlying Evergreen Endpoint Package Manager through policy, curated catalogs, audit history, and optional brokered operations.

The product targets Windows 10/11 Pro and Enterprise. Its supported software channels are:

- WinGet community sources
- Microsoft Store through WinGet's `msstore` source
- Windows PowerShell Gallery
- PowerShell 7 repositories

No other package manager is loaded in the Windows product profile. Package-manager enablement,
source allowlists, executable selection, and source provisioning are administrator-owned. They are
not exposed through the employee settings UI, TUI, or mutable local API routes.

## Experience principles

1. **Use employee language.** Prefer “Apps,” “Available,” “Installed,” and “Updates” over “Discover Packages,” manager names, package IDs, or operation terminology.
2. **Show trust before technical detail.** Each result leads with the app name, icon, verified publisher, source badge, short description, install state, and policy status.
3. **Make the safe action obvious.** A single primary action changes between Install, Update, Open, and Request access. Advanced install options remain available to administrators where policy allows them.
4. **Curate by default.** Organizations can publish a recommended catalog. Public search can be allowed, restricted, or disabled by policy.
5. **Explain blocked actions.** A policy decision includes a plain-language reason and, when configured, a support or request-access link.
6. **Keep administration out of the employee path.** Policy status, service health, and diagnostics can live in a read-only Admin area that can be hidden from standard users. Source and package-manager changes remain deployment-controlled.

## Proposed navigation

| Area | Employee purpose | Primary content |
|---|---|---|
| Home | See what needs attention | Recommended apps, pending updates, recent activity |
| Apps | Find approved software | Curated categories and ranked search results |
| Installed | Understand what is on this device | Friendly app inventory with update and repair actions |
| Updates | Keep software current | Available updates, restart impact, scheduled maintenance |
| Activity | See what happened | Human-readable install/update history |
| Settings | Control personal preferences | Theme, notifications, permitted update preferences |
| Admin | Inspect the endpoint client | Effective source policy, policy status, broker/service health, diagnostics |

The current Bundles, package-manager settings, raw logs, and experimental controls should be hidden from the employee navigation. Features still required by IT can remain in Admin or command-line workflows.

## Search and result model

Search should merge and rank results from the selected allowed sources. The default result presentation is a card/list hybrid rather than the current technical data grid.

### Visible result fields

- App name and icon
- Publisher, with a verified indicator when identity can be established
- One-line description
- Source badge: Organization, WinGet, Microsoft Store, or PowerShell Gallery
- Installed state and installed/available version when useful
- Architecture and scope only when they affect the decision
- Policy state: Approved, Requires approval, or Blocked
- Primary action

Package ID, raw source URL, installer type, hashes, product codes, and manager diagnostics belong in an expandable **Technical details** section.

### Ranking

Rank results deterministically using the following signals, in order:

1. Exact normalized app-name match
2. Organization catalog membership
3. Verified publisher match
4. Exact package-ID match when the user entered an ID
5. Token coverage in name and publisher
6. Preferred source order set by policy
7. Stable alphabetical tie-breaker

Duplicate WinGet and Microsoft Store entries should be grouped under one app card when publisher and product identity are strong enough. The user can choose the source from the details view; organization policy can choose it automatically.

## Brand direction

The product uses the Technicull evergreen palette:

| Token | Value | Use |
|---|---:|---|
| Forest | `#176B4D` | Primary actions and active navigation |
| Deep forest | `#0D3F30` | Titles, dark surfaces, primary icon shape |
| Gold | `#D69B2D` | Attention and small brand accents |
| Ink | `#17211D` | Light-theme text |
| Muted | `#5F6C66` | Secondary text |
| Paper | `#F7FAF8` | Light-theme app background |
| Line | `#DCE5DF` | Borders and separators |

The proposed mark combines an open software package, an evergreen sprout, and a confirmation check. The production mark must be redrawn as a small set of deterministic SVG paths and tested at 16, 20, 24, 32, 44, 64, 150, 256, and 512 pixels. The tray version should use a one-color silhouette with separate light and dark variants; it should not rely on the gold accent to remain legible.

## Windows management model

Administrative policy is read from:

`HKLM\Software\Policies\Technicull Solutions\Evergreen Endpoint\Package Manager`

Machine policy takes precedence over the machine JSON document. Package-manager controls are absent
from the end-user UI. Policy and JSON changes take effect when the application or background service
restarts, avoiding partially reconfigured package-manager sessions.

Package-manager configuration resolves in this order:

1. Group Policy values under the machine policy key
2. Machine JSON at `%ProgramData%\Technicull Solutions\Evergreen Endpoint\Package Manager\package-managers.json`, or the path selected by policy
3. Built-in secure defaults

Per-user settings never participate in package-manager or source configuration. The JSON schema is
versioned and contains only the supported manager names, their enabled state, and source allowlists.
An example is provided at `package/Policies/package-managers.example.json`. Source registration on
the operating system is performed during managed deployment; the application enforces the allowlist
when it enumerates configured sources. The desktop UI, terminal UI, IPC API, and command-line API
cannot add or remove sources. The source-operation layer also rejects mutation requests for the three
supported managers, so a legacy or indirect interface cannot bypass the managed boundary.

The ADMX package should contain:

- `EvergreenEndpointPackageManager.admx`
- `en-US\EvergreenEndpointPackageManager.adml`
- a deployment README with Local Group Policy and Central Store instructions
- PowerShell install/remove scripts for evaluation and automated deployment

### Initial policy inventory

| Policy | Registry value | Type | Default |
|---|---|---|---|
| Application availability | `Enabled` | DWORD | Enabled |
| Employee public search | `PublicSearchMode` | ENUM | Allowed |
| Organization catalog URL | `CatalogUrl` | STRING | Empty |
| Require organization catalog signature | `RequireCatalogSignature` | DWORD | Enabled |
| Machine JSON path | `ConfigurationFile` | STRING | ProductData default |
| Enable WinGet | `EnableWinGet` | DWORD | Enabled |
| WinGet executable | `WinGetExecutablePath` | STRING | Automatic discovery |
| Allowed WinGet sources | `AllowedWinGetSources` | MULTI_SZ | winget, msstore |
| Enable Windows PowerShell | `EnablePowerShell` | DWORD | Enabled |
| Windows PowerShell executable | `PowerShellExecutablePath` | STRING | Automatic discovery |
| Allowed Windows PowerShell repositories | `AllowedPowerShellSources` | MULTI_SZ | PSGallery |
| Enable PowerShell 7 | `EnablePowerShell7` | DWORD | Enabled |
| PowerShell 7 executable | `PowerShell7ExecutablePath` | STRING | Automatic discovery |
| Allowed PowerShell 7 repositories | `AllowedPowerShell7Sources` | MULTI_SZ | PSGallery |
| Preferred source order | `PreferredSources` | MULTI_SZ | Organization, WinGet, msstore, PSGallery |
| Allow user installs | `AllowInstall` | DWORD | Enabled |
| Allow user uninstalls | `AllowUninstall` | DWORD | Disabled |
| Allow advanced install options | `AllowAdvancedOptions` | DWORD | Disabled |
| Default install scope | `DefaultInstallScope` | ENUM | Policy/automatic |
| Update mode | `UpdateMode` | ENUM | Notify |
| Maintenance schedule | `MaintenanceSchedule` | STRING | Empty |
| Minimum update age | `MinimumUpdateAgeDays` | DWORD | 0 |
| Allow deferral | `AllowUpdateDeferral` | DWORD | Enabled |
| Maximum deferral | `MaximumDeferralDays` | DWORD | 7 |
| Broker requirement | `BrokerMode` | ENUM | When required |
| Policy document path | `BrokerPolicyPath` | STRING | Empty |
| Hide Admin area | `HideAdminArea` | DWORD | Enabled for standard users |
| Telemetry level | `TelemetryLevel` | ENUM | Security and reliability only |
| Self-update mode | `SelfUpdateMode` | ENUM | Managed |
| Support URL | `SupportUrl` | STRING | Empty |

Package allow/deny rules should continue to use the existing signed broker policy document rather than encoding complex package lists into ADMX values. Group Policy selects and locks the policy location; the policy document expresses package, publisher, source, version, and operation constraints.

## Security baseline

- Keep WinGet and PowerShell execution argument-safe; never build command lines from display text.
- Prefer source and publisher identity from signed or authoritative metadata.
- Require HTTPS for remote catalogs and repositories unless an explicit enterprise policy allows a local/UNC source.
- Verify organization catalog signatures before parsing content.
- Preserve the existing broker policy validation and secure settings mechanisms.
- Record installs, updates, removals, policy decisions, source, requesting user, result, and restart state in an audit-friendly history.
- Run the desktop client as the user. Elevation flows through the existing broker/service boundary or a deliberate UAC operation.
- Keep NativeAOT and trim safety as release requirements.

## Delivery sequence

The first portable administrator increment is described in [ADMIN_CLIENT.md](ADMIN_CLIENT.md): offline software-library and manager-configuration authoring/export plus read-only local diagnostics. The employee library is enabled by default under Apps, with source search available separately as Search apps. This increment does not provide signed catalogs, remote management, or portable privileged policy application. Broader phases below remain the product roadmap.

### 1. Product boundary and identity

- Load only WinGet, Windows PowerShell, and PowerShell 7 on Windows.
- Confirm the product name and application identity.
- Replace names, links, installer identity, application IDs, data directories, and upgrade/migration behavior in one coordinated change.
- Build the final SVG/ICO/PNG asset set and light/dark tray icons.

### 2. Employee shell

- Rename and reduce navigation.
- Add Home and Admin boundaries.
- Apply the Evergreen design tokens to light and dark themes.
- Move advanced controls out of the default path.

### 3. App catalog and search

- Introduce a presentation model that separates friendly metadata from package identity.
- Implement ranking, source badges, policy badges, and duplicate grouping.
- Make the curated software catalog the default landing experience.
- Add accessible keyboard and screen-reader behavior for every result action.

### 4. Enterprise management

- Add a typed policy provider with HKLM precedence and UI lock state.
- Ship ADMX/ADML templates and deployment scripts.
- Map simple configuration into Group Policy and retain complex package authorization in the signed broker policy.
- Add policy diagnostics and effective-policy export.

### 5. Packaging and hardening

- Remove unused manager projects and assets from the Windows shipping graph.
- Add migration from the fork's existing settings/data paths.
- Validate x64 and arm64 NativeAOT publishing.
- Measure cold start, idle memory, package size, search latency, and update reliability against release budgets.

## Release acceptance targets

- Cold start to usable shell: under 2 seconds on a representative managed Windows 11 device
- Idle private working set: measured and budgeted before release, with no background manager processes left running
- Search: first meaningful results within 500 ms from cached organization data and clear progress for remote sources
- Tray icon: recognizable in Windows light, dark, and high-contrast themes at 16 px
- Every configurable employee capability has a documented policy state and effective-policy view
- No trim/AOT warnings in supported Windows publish profiles
- Standard users can complete approved installs without seeing package IDs or command-line terminology
