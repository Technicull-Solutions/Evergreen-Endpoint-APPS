# Evergreen contributor workflow

This is the shared entry point for human contributors and coding agents working on Evergreen Endpoint Package Manager. Repository workflows live in `.agents/skills/`; read their `SKILL.md` files directly when your tooling does not discover skills automatically.

## Sources of truth

- [AGENTS.md](../AGENTS.md) defines repository engineering requirements.
- [Product direction](EVERGREEN_ENDPOINT_PACKAGE_MANAGER.md) defines the intended experience and roadmap. Proposed navigation, policy settings, and acceptance targets are not proof that a feature is implemented.
- [Expanded vision review](EVERGREEN_EXPANDED_VISION_REVIEW.md) records the inspected implementation gaps and proposed portable administrator boundaries; its architecture and next milestone remain proposals.
- [Portable administrator client](ADMIN_CLIENT.md) documents the implemented offline authoring/export utility and the employee Apps library deployment path.
- [Catalog source profiles](CATALOG_SOURCE_PROFILES.md) documents optional administrator Gitea retrieval, session-only authentication, and the deployment boundary.
- [Accessibility baseline](ACCESSIBILITY.md) defines the WCAG 2.2 AA target and the use of WCAG 3 draft guidance.
- [Windows identity migration](WINDOWS_IDENTITY_MIGRATION.md) records installer/CLI ownership and the remaining runtime-state migration boundaries.
- [Signed MVP release path](SIGNED_MVP_RELEASE.md) records release gates, publisher setup, and the GitHub-to-WinGet sequence.
- Source code, tests, and checked-in build workflows establish current behavior. Confirm implementation before documenting it as available.
- [UPSTREAM.json](../UPSTREAM.json) records inherited source provenance; [LICENSE](../LICENSE) contains the inherited license notice.

## Product knowledge

- **Evergreen Endpoint Package Manager** is the product, installer, policies, and management utility. **Evergreen Endpoint | APPS** is the employee-facing experience. Technicull Solutions maintains this derivative of UniGetUI.
- The employee product targets managed Windows devices. Its supported channels are WinGet community sources, Microsoft Store through WinGet, Windows PowerShell repositories, and PowerShell 7 repositories. Store is a WinGet source, not an additional manager.
- Manager enablement, source allowlists, executable selection, and source provisioning are administrator-owned. Hiding a UI control does not enforce authorization at the engine or IPC boundary.
- Keep C#/.NET for application and package-engine logic, Avalonia/AXAML for the desktop UI, and PowerShell for deployment automation. A language or UI-framework migration needs a concrete requirement and a separately scoped design.
- Lead employee flows with app names, trust information, policy reasons, and clear actions. Put package IDs, raw URLs, and diagnostics in technical or administrator views.
- The desktop app runs as the requesting user. Privileged work uses the existing broker/elevation boundary; do not make the entire app require administrator rights.
- Existing `UniGetUI.*` project and namespace names are implementation identities. They do not imply that customer branding or release endpoints should point upstream. Rename them only with a coordinated migration.
- Product independence means separate runtime identities, state, release trust, and service ownership. Preserve upstream license notices and provenance when removing operational dependencies.

## Choose a workflow

| Task | Repository skill |
|---|---|
| Build, test, format, or validate NativeAOT | [evergreen-build-validate](../.agents/skills/evergreen-build-validate/SKILL.md) |
| Audit or change product identity and upstream separation | [evergreen-product-separation](../.agents/skills/evergreen-product-separation/SKILL.md) |
| Review a security-sensitive change or trust boundary | [evergreen-security-review](../.agents/skills/evergreen-security-review/SKILL.md) |
| Change a package-manager adapter or managed source behavior | [evergreen-manager-change](../.agents/skills/evergreen-manager-change/SKILL.md) |
| Prepare or verify a release, installer, or updater | [evergreen-release](../.agents/skills/evergreen-release/SKILL.md) |
| Change the Avalonia employee experience or terminal UI | [evergreen-avalonia-ui](../.agents/skills/evergreen-avalonia-ui/SKILL.md) |
| Measure or improve runtime performance | [evergreen-performance](../.agents/skills/evergreen-performance/SKILL.md) |

The existing `translation-*` skills remain the workflows for localization export, translation, import, review, source synchronization, and status. Select only the skills relevant to the change; every task does not need every workflow.

## Contribution sequence

1. Inspect the working tree and preserve unrelated changes. Identify the behavior being changed and its current implementation.
2. Read the relevant skill and the applicable sections of `AGENTS.md`. Keep the change within the requested scope.
3. Implement the change and validate the affected behavior. For C# changes, run both required formatters. For shipping runtime changes, include the applicable published NativeAOT path.
4. Report what changed, commands and results, and any untested architecture or runtime behavior. Keep measured results separate from targets.

Skills guide contributors; they do not execute automatically in GitHub Actions or replace enforced CI checks. Use existing scripts and workflows as the executable implementations. Change their enforcement only as part of an explicitly scoped CI change.

## Keeping knowledge current

Update the owning document when product requirements change, and update affected skills when workflow commands or code locations change. Link to canonical information instead of copying it into personal configurations or provider-specific instruction mirrors. Never commit credentials, signing keys, private policy documents, or identifying endpoint logs.
