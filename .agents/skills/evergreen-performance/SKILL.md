---
name: evergreen-performance
description: Measure and improve Evergreen startup, memory, search latency, process usage, and package size using repeatable before-and-after evidence.
---

# Measure Evergreen performance

Read [the contributor workflow](../../../docs/EVERGREEN_CONTRIBUTOR_WORKFLOW.md) and the acceptance targets in [product direction](../../../docs/EVERGREEN_ENDPOINT_PACKAGE_MANAGER.md). Targets are goals, not recorded measurements.

## Define a reproducible scenario

Choose the metric and user-visible endpoint before changing code:

| Concern | Useful measurement |
|---|---|
| Startup | Process launch to usable shell; separate cold and warm runs |
| Idle memory | Private working set at a fixed idle point, with child processes accounted for |
| Search | Time to first meaningful result and completion; separate cached catalog and remote sources |
| Process overhead | Spawn count, elapsed lifetime, and processes remaining after completion/cancellation |
| Distribution size | Published application, archive, and installer sizes measured separately |
| Update reliability | Success/failure/restart outcomes across specified upgrade scenarios |

Record commit, configuration, RID, device/OS, power conditions, package count, allowed managers/sources, cache state, and connectivity. Use the same scenario for baseline and candidate. Prefer Release NativeAOT artifacts for release claims; do not compare a Debug run with a published baseline.

## Measure before optimizing

Use available OS/process instrumentation and existing logging first. Choose tools that support NativeAOT; do not assume JIT-specific profilers or counters work. Keep temporary traces out of source control and redact identifying package/user/source information.

Repeat runs enough to show variability; report run count and median/range or percentiles supported by the sample size. Keep cold-cache and warm-cache results separate. Do not clear real user settings, feeds, or shared caches to manufacture a cold run; use an isolated profile or disposable environment.

Inspect the measured path: bootstrap/manager initialization, UI-thread work, package enumeration, repeated CLI launches, icon fetching, shared HTTP/feed caches, and rendering of large result lists. Change the demonstrated bottleneck rather than assuming the language or UI framework is responsible.

## Validate the improvement

Preserve policy checks, source/signature validation, cancellation, accurate version comparison, freshness semantics, localization, and NativeAOT safety. An optimization must not silently reduce trust or omit required results.

Re-run the same scenario, relevant behavior tests, and the applicable publish validation. Use fake data for repeatable UI/engine scenarios, and identify it as such; simulated timings do not establish live feed latency or real install reliability.

Deliver a before/after table with environment, sample sizes, metric definitions, variability, and tradeoffs. State unmeasured metrics and architecture gaps. Add permanent instrumentation or a benchmark script only when it provides a repeatable benefit and has been exercised.
