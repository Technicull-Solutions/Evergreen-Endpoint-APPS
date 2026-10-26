# Evergreen Endpoint Package Manager administrative configuration

Copy `EvergreenEndpointPackageManager.admx` to `%SystemRoot%\PolicyDefinitions` and copy the `en-US`
resource file into `%SystemRoot%\PolicyDefinitions\en-US`. For a domain, copy the same files to the
domain Central Store under `SYSVOL\<domain>\Policies\PolicyDefinitions`.

Policies appear under:

`Computer Configuration > Administrative Templates > Technicull Solutions > Evergreen Endpoint Package Manager > Package managers`

Package-manager configuration is machine-owned and resolves in this order:

1. Group Policy values
2. The JSON file selected by policy
3. `%ProgramData%\Technicull Solutions\Evergreen Endpoint\Package Manager\package-managers.json`
4. Built-in defaults

Deploy the JSON file with permissions that allow standard users to read it and only administrators
or the deployment system to modify it. Use `package-managers.example.json` as the starting point.
Allowed-source entries match source/repository names case-insensitively. Register or remove the
corresponding WinGet and PowerShell sources as part of device deployment; the application filters
the configured source inventory against the allowlist. The desktop UI, terminal UI, IPC API, and
command-line API cannot add or remove sources for managed package managers.

Configured executable paths fail closed if the file does not exist. Leave an executable path
unconfigured to use the application's normal trusted executable discovery.

`maximumConcurrentUninstalls` is an optional top-level integer in the version 1 JSON.
It defaults to **3**; supported values are **1–100**. The **Set the maximum concurrent
uninstalls** policy writes a `MaximumConcurrentUninstalls` DWORD under the registry key
`HKLM\Software\Policies\Technicull Solutions\Evergreen Endpoint\Package Manager\PackageManagers`
and takes precedence over JSON. An invalid deployed number restricts the limit to 1.
Restart the client after deployment to reload its cached machine configuration.

Installed has no select-all action. Selecting multiple apps shows **Uninstall Apps**;
the confirmation lists the apps, defaults focus to Cancel, and blocks selections above
the effective limit. Other desktop uninstall selections use the same confirmation.
The shared operation engine also limits simultaneous uninstall execution across managers
and its desktop, CLI, TUI, and API callers within each client process. Excess operations
wait for a slot and can be canceled. Existing operation/manager limits may serialize
them further. This is not a machine-wide restriction on Windows or external uninstall commands.
