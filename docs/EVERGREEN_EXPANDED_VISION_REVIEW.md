# Expanded product vision review

Review date: 2026-10-15. Source baseline: `094ccb158` on this repository's `main` branch. GitHub had one branch and no pull requests at review time; the seven Evergreen skills were introduced in `2eee2c190`. No separate portable administrator project was present in the reviewed solution.

This review complements [product direction](EVERGREEN_ENDPOINT_PACKAGE_MANAGER.md). Observations describe inspected source, not fresh runtime verification. The administrator architecture and delivery order below are proposals for discussion.

Implementation follow-up: the user accepted the local-first direction. [ADMIN_CLIENT.md](ADMIN_CLIENT.md) documents the resulting portable administrator increment, shared library contract, and employee Apps navigation. The initial inspection understated the inherited catalog implementation: a library viewer and editor already existed behind the experimental switch. This increment reuses that viewer and extracts the document contract instead of treating catalogs as a new subsystem. GitHub Issues has also been enabled. The baseline observations below remain a historical review; remaining state migration, signing, and remote-management work is not complete.

## Product responsibilities

The intended product is an organization-managed Windows application experience with three responsibilities:

| Surface | Responsibility | Authority |
|---|---|---|
| Evergreen Endpoint \| APPS | Find, install, update, and explain approved applications | Runs as the employee; effective machine policy constrains actions |
| Evergreen Endpoint Package Manager administrator utility | Author configuration, inspect effective policy, prepare deployment material, and collect diagnostics | Drafting/export is unprivileged; applying machine changes requires a separately authorized operation |
| Execution and policy services | Enforce source and operation rules and perform privileged work | Authorization belongs at the execution boundary, including alternate CLI/TUI/API routes |

The portable administrator utility is a proposed additional deliverable. The existing portable ZIP is the desktop client with relocated writable state; it does not establish a dedicated admin application or remote-management framework.

## Review of the seven skills

| Skill | Assessment | Implication for the expanded product |
|---|---|---|
| `evergreen-build-validate` | Defines formatting, test selection, NativeAOT validation, and honest architecture coverage | Every new shipping executable needs its own publish/runtime checks |
| `evergreen-product-separation` | Correctly separates visible identity, runtime state, dependencies, and attribution | Complete state and trust ownership before claiming product independence |
| `evergreen-security-review` | Follows requests to authorization and privileged execution | Apply it to admin imports, exports, deployment, and any future remote transport |
| `evergreen-manager-change` | Preserves supported managers, source identity, and ecosystem version semantics | Keep the Windows employee profile restricted to its three managers |
| `evergreen-release` | Covers versioning, artifacts, upgrades, integrity, and updater trust | Add separate client/admin artifact acceptance when the admin executable exists |
| `evergreen-avalonia-ui` | Keeps employee language, accessibility, compiled bindings, and AOT-safe construction | Reuse established UI patterns while keeping employee and administrator navigation distinct |
| `evergreen-performance` | Requires reproducible measurements before optimization | Establish measured budgets for both applications; targets are not benchmark results |

The skills share a useful canonical contributor entry point and do not require a rewrite. They guide contributors; CI enforces only the checks actually implemented in workflows. The release skill's stale description of the update-testing guide was corrected during this review.

There is no dedicated admin-product workflow yet. Initially, use the existing UI, manager, security, separation, and release skills together as relevant. Add a focused skill once the administrator contracts and repeatable development workflow exist, rather than encoding speculative architecture as instructions.

## Implemented foundation and remaining work

| Area | Inspected foundation | Gap relative to the product direction |
|---|---|---|
| Windows manager profile | `PEInterface` selects WinGet, Windows PowerShell, and PowerShell 7 | Keep supported channels distinct from inherited adapters in the source tree |
| Managed configuration | `ManagedPackageManagerConfiguration` resolves HKLM policy, machine JSON, and defaults; ADMX/ADML templates exist | Broader catalog, public-search, and employee-capability policies remain roadmap items |
| Policy editor | Structured/raw editing, sessions, validation adapters, confirmation, and conflict handling live in the desktop project | Separate reusable editing contracts from desktop views and broker-specific adapters |
| Policy application | Broker management/validation and a separate elevated helper exist | Depends on Devolutions policy packages and an external broker; no independent Evergreen service is established by this repository |
| Portable operation | Marker-based relocation, scratch storage, and first-run import exist | Uses inherited marker/state conventions and can fall back to per-user storage when unwritable; define stronger admin-workspace behavior |
| Product identity | Owned installer registrations, release metadata, and visible branding have been introduced | `CoreData` still uses UniGetUI per-user state and Devolutions/UniGetUI machine paths; migration needs explicit ownership rules |
| Employee experience | Existing Avalonia shell and package engine | Home, curated app presentation, catalog trust, ranking, and deduplication need implementation evidence before being advertised |
| Release trust | Owned test releases and integrity/installer checks | Current test artifacts are unsigned; trusted automatic updates and elevated policy writes require their signing prerequisites |
| Support | Documentation and UI point to the owned repository | GitHub Issues was disabled at review time, so the destination cannot accept reports |

The deployed manager registry path includes the `PackageManagers` subkey beneath the product policy root. Use [the deployment guide](../package/Policies/README.md), ADMX, and source for exact current keys; the proposed policy inventory is not an implementation contract.

Existing maintenance scheduling does not establish that the proposed machine-owned maintenance policy is implemented. Likewise, a broker policy editor is not an organization catalog editor.

## Proposed framework and component boundaries

Retain C#/.NET 10, Avalonia/AXAML, CommunityToolkit.Mvvm, source-generated JSON, NativeAOT, and PowerShell deployment automation. This follows the existing contributor requirements and allows reuse of the current editor and tests. No requirement found in this review warrants a language or UI-framework migration.

Use a separate administrator executable with a small shell. Extract shared contracts and editing logic in buildable increments; do not make the admin shell launch the employee shell or initialize all package managers merely to edit a file.

Proposed dependency direction:

```text
Employee shell                 Portable administrator shell
       |                                  |
Employee application services  Admin authoring/inspection services
       |                                  |
       +---- Shared typed configuration contracts ----+
       |                                  |
Package execution adapters     File/export and broker adapters
       |                                  |
       +---- Existing authorization boundaries -------+
```

Shared contracts should contain versioned document models, validation results, effective-policy explanations, and stable identifiers. Keep Avalonia controls, registry access, file access, signing, and broker transport in their owning adapters. Preserve the distinction between package-manager configuration, package-operation authorization, and the proposed organization catalog.

Reuse the policy editor's separation between `IPolicyValidationClient` and `IPolicyWriteClient`, but do not present offline structural validation as authoritative broker validation. The current production validation adapter calls the broker. Offline authoring needs an explicitly scoped validator and clear status for checks requiring the target endpoint.

Portable administration should start with an explicit workspace for drafts and exports, offline validation where supported, local read-only inspection, and deployment previews. Machine policy remains authoritative. Carrying a workspace to another device must not import that device's user settings, grant privileges, or provision sources automatically. An unwritable workspace should produce an actionable error rather than silently writing elsewhere.

## Portable administration and elevation

The existing policy-write protocol requires mutually verified signer identity and a protected installation root (`PolicyElevationTrustPolicy.RequireProtectedInstallRoot`). A user-writable portable folder does not meet that protected-root requirement. Current unsigned testing artifacts also do not satisfy signer verification.

Keep these checks intact. The first portable milestone can author and export drafts and inspect supported local state. Describe exports as unsigned drafts unless an actual signing operation has occurred.

If direct application is required later, design an authenticated interaction with an installed trusted component, or a separately scoped administrator deployment operation. Define caller authentication, authorization, destination ownership, conflict detection, audit results, and failure behavior before implementation. Do not copy the existing helper beside a portable executable and disable preflight to make writes succeed.

Remote device management remains an open product decision. It requires its own endpoint identity, authentication, authorization, transport, deployment, and audit design; local named-pipe broker code does not establish those capabilities.

## Proposed next milestone

The working proposal is local authoring and diagnostics first, pending confirmation of the intended remote-management scope.

1. Inventory and isolate remaining runtime state identities. Specify migration of prior Evergreen installs without silently adopting or deleting UniGetUI state. Verify installed and portable side-by-side behavior.
2. Define administrator workspace and configuration contracts. Start with the implemented package-manager JSON schema; distinguish broker-policy and future catalog documents. Preserve schema versions and source-generated serialization.
3. Create a minimal administrator shell with explicit open/save/export and effective-configuration inspection. Extract existing editor logic only where needed; keep source provisioning and machine writes out of this initial slice.
4. Add observable acceptance coverage: no broker required for supported offline authoring, no machine changes from opening a workspace, explicit unwritable-folder errors, bounded malformed-document handling, and accurate offline/endpoint validation status. Publish and exercise the admin NativeAOT executable.
5. Deliver the employee Apps presentation against the same effective contracts, beginning with friendly metadata and policy explanations. Add signed catalog and privileged deployment capabilities as separately validated increments.

Windows x64 is the first verified release baseline. Validate ARM64 independently and preserve existing portable profiles; no architecture is covered by this source review alone. Signing and an owned support destination must be resolved before claiming a production management release.

## Review validation

This pass inspected skills, product/contributor documentation, project references, configuration policy, editor adapters, portable paths, and policy-write preflight. GitHub branch, pull-request, commit, and issue-availability state were checked through the API. No application code, machine policy, GitHub settings, or release artifacts were changed. Documentation links and skill metadata were checked locally; no new application build or runtime/security certification is claimed.
