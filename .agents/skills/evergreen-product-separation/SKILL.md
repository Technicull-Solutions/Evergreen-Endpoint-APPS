---
name: evergreen-product-separation
description: Audit or implement Evergreen independence from UniGetUI across product identity, runtime state, dependencies, packaging, and owned services.
---

# Separate Evergreen product identity

Read [the contributor workflow](../../../docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md), [product direction](../../../docs/EVERGREEN_ENDPOINT_PACKAGE_MANAGER.md), and [UPSTREAM.json](../../../UPSTREAM.json). Establish whether the request is an audit, a scoped migration, or a dependency removal.

## Inventory before changing

Use targeted `rg` searches for `UniGetUI`, `WingetUI`, `Devolutions`, `marticliment`, and old hosts. Classify each match as customer identity, runtime identity/state, operational dependency, internal code name, or attribution/test fixture. A remaining upstream string is not automatically a defect.

Inspect the relevant surfaces:

- `src/UniGetUI.Core.Data/CoreData.cs`, `src/Directory.Build.props`, and `src/SharedAssemblyInfo.cs`: names, publisher metadata, versions, and release URLs.
- `src/UniGetUI.Avalonia/Infrastructure/`: updater, single-instance routing, tray, toast, and restart behavior.
- `src/UniGetUI.Core.Settings/`, `src/UniGetUI.Core.SecureSettings/`, broker/elevated-helper projects, and `src/UniGetUI.Interface.IpcApi/`: data paths, policy locations, IPC identities, and trust configuration.
- `UniGetUI.iss`, `package/`, `scripts/`, `update/`, and `.github/workflows/`: installer/upgrade identities, executable names, registrations, artifacts, and publishing ownership.
- README, contributor/security documentation, and UI links: product description, support destinations, and provenance.

## Make a coordinated migration

For each identity, record old value, intended value, consumers, collision risk, migration behavior, and verification. Define side-by-side installation and uninstall behavior before changing installer IDs or state paths. Do not silently adopt or delete another product's settings, credentials, or installation.

Prioritize release trust, endpoints, state, and OS registration separation. Rename internal namespaces/projects in separate buildable batches. Exclude unsupported components from the shipping graph before deleting source; inspect project references and shared consumers first. Preserve existing portable guarantees unless a support change is explicitly in scope.

Keep license notices, third-party attribution, and upstream provenance. Do not remove history, remotes, repositories, or publish destinations as an incidental cleanup. Do not invent a support address, signing identity, domain, or service endpoint.

## Verify independence

Exercise install, launch, single-instance routing, deep links, update, and uninstall alongside UniGetUI in a disposable test environment. Check both products' files, state, registrations, and logs. Inspect outbound connections and classify intentional package-feed traffic separately from upstream application services.

Report completed changes and residual dependencies with file evidence. An audit must distinguish observed behavior from untested installer/network behavior; renamed strings alone do not establish independence.
