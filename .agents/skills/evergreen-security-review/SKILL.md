---
name: evergreen-security-review
description: Review Evergreen trust boundaries in privileged operations, policy enforcement, IPC, package execution, updates, and untrusted data handling.
---

# Review Evergreen security boundaries

Read [the contributor workflow](../../../docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md) and relevant NativeAOT/interop requirements in [AGENTS.md](../../../AGENTS.md). Scope the review to the requested feature, change, or attack surface.

## Trace the operation

Follow inputs from desktop, CLI, TUI, local API, remote metadata, or policy files to the actual privileged or filesystem action. Identify the requesting identity, trust source, authorization decision, process boundary, and audit result. UI visibility is not authorization.

Start at the applicable code:

- `src/UniGetUI.PackageEngine.AgentBroker/`: request construction/validation, policy validation, management, and policy-write elevation.
- `src/UniGetUI.AgentPolicy.ElevatedHelper/`: helper launch and authenticated transport.
- `src/UniGetUI.Core.SecureSettings/`, `src/UniGetUI.Core.Settings/ManagedPackageManagerConfiguration.cs`, and `src/UniGetUI.Interface.IpcApi/`: protected configuration, administrator ownership, and API entry points.
- `src/UniGetUI.Avalonia/Infrastructure/AvaloniaAutoUpdater.cs` and package operation helpers: downloads, trust verification, and execution.

## Review relevant failure modes

- Enforce package/source/operation policy at the execution boundary, including alternate CLI, TUI, and API routes. Check failed, unavailable, and changed policy states.
- Authenticate IPC peers and bind authorization to the actual peer/request. Check ACLs, bounded frames, timeouts, replay resistance, cancellation, and resource cleanup where applicable.
- Preserve argument boundaries; do not construct executable commands from display text. Inspect executable resolution, working directories, environment, scope, and user-versus-system behavior.
- Verify signatures, hashes, and signer trust according to the existing protocol. A hash delivered by the same untrusted source does not alone establish publisher authenticity. Prevent release validation bypasses.
- Treat archives, manifests, catalogs, URLs, and paths as untrusted. Check bounded reads, traversal, replacement races, and protected destination ownership where they affect the operation.
- Keep credentials and identifying endpoint data out of logs, fixtures, and reports. Preserve source-generated JSON and NativeAOT-safe interop.

## Validate and report

Use existing broker/elevation tests in `src/UniGetUI.PackageEngine.Tests/`, settings tests, and the affected UI/IPC tests. Prefer denial, malformed-input, unauthorized-peer, and boundary tests that demonstrate behavior. Use isolated fixtures; do not install real software, alter machine policy, or exploit a live endpoint as part of a review.

For each finding, provide affected file/line, reachable trigger, consequence, and a focused remediation. Separate confirmed defects from assumptions and coverage gaps. For fixes, verify the original failure and the intended authorized path, including the applicable published binary. Do not broaden trust or suppress warnings to make tests pass.
