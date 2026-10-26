---
name: evergreen-avalonia-ui
description: Implement Evergreen Avalonia views, bindings, themes, and accessible employee workflows, with terminal-specific guidance when changing the TUI.
---

# Develop the Evergreen UI

Read [the contributor workflow](../../../docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md) and relevant experience requirements in [product direction](../../../docs/EVERGREEN_ENDPOINT_PACKAGE_MANAGER.md). Distinguish proposed navigation from the implemented shell.

## Use the existing application

Work in `src/UniGetUI.Avalonia/Views/`, `ViewModels/`, `Assets/Styles/`, and `Assets/Brand/`. Inspect nearby controls and bindings before adding a new abstraction. Keep the existing Avalonia app; starter templates are for separate experiments, not replacement of the product shell.

The portable administrator shell lives in `src/Evergreen.Admin/`; its offline library contract is shared with the employee client through `src/Evergreen.Configuration/`. Read `docs/ADMIN_CLIENT.md` for the supported authoring/export scope. Keep machine deployment and broker authorization separate from workspace editing. The employee library is enabled under Apps; Search apps remains the package-source search surface.

For optional administrator catalog retrieval, read `docs/CATALOG_SOURCE_PROFILES.md`. Keep server/repository settings environment-specific and tokens session-only. Opening a profile must not connect or apply configuration. Apps continues consuming a separately deployed protected local catalog.

- Keep package execution and authorization in application/engine services. View models translate state into employee actions and explanations; hidden controls are not policy enforcement.
- Lead with friendly app names, trust/source and policy status, and one clear action. Show raw package identity and diagnostics in technical/admin views when needed.
- Use existing theme resources and branding assets. Verify light, dark, and high-contrast behavior, scaling, text overflow, and empty/loading/error/blocked states relevant to the change.
- Preserve compiled bindings with explicit data types. Use direct construction or existing AOT-safe factories; avoid reflection-based view discovery, runtime generic construction, and dynamic loading.
- Localize new visible text through established translation helpers and controls. Use existing translation skills for source synchronization or language-file changes.
- Provide keyboard access, sensible focus after navigation/dialog dismissal, accessible names, and screen-reader-readable status. Do not convey policy or operation state through color alone.

## Validate at the appropriate surface

Use affected tests in `src/UniGetUI.Tests/` and inspect the actual UI at representative window sizes. Validate bindings and view loading through the published NativeAOT path when changed. If GUI verification is unavailable, report it as a gap rather than inferring appearance from XAML.

For diagnostics changes, follow `EnableAvaloniaDiagnostics` and `AVALONIA_DIAGNOSTICS_ENABLED` in `AGENTS.md`; keep developer controls out of employee settings and public CLI documentation.

## When changing the terminal UI

Read `docs/TUI.md`. Pages must use `TuiEngine`; use the existing in-window `TuiModal` overlays and `TuiPalette` theme brushes. Add E2E coverage under `src/UniGetUI.Tui.Tests/E2E/` for a new page action.

Use the sandboxed fake dataset:

```powershell
dotnet run --project src/UniGetUI.Avalonia/UniGetUI.Avalonia.csproj -- tui --fake-data
```

Report UI states exercised, keyboard/accessibility checks, test results, and published-runtime coverage. Do not broaden the UI redesign beyond the requested interaction.
