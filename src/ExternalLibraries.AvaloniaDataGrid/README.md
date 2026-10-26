# Evergreen DataGrid compatibility source

This directory contains AvaloniaUI's `Avalonia.Controls.DataGrid` 12.0.0 source
from https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid/tree/12.0.0
(commit `3d2cf024d6f15d4e770f655246185ad417f93f51`).
The original MIT license and per-file Microsoft notices are retained.
`Avalonia.public.snk` contains only the upstream public key, used with delay signing
to retain the assembly identity expected by Consolonia's grid theme.

Evergreen uses explicit columns, compiled bindings, read-only grids, and explicit
sort comparers. The upstream grid nevertheless makes legacy reflection-based
fallbacks reachable during NativeAOT compilation. This compatibility build removes
those fallbacks instead of suppressing their trim/AOT diagnostics:

- Automatic column generation throws: columns must be declared explicitly.
- Row properties and indexers are not discovered by name. Sort comparers and
  compiled bindings supply the data access used by the application.
- Enumerable item metadata uses a representative item, with no interface scanning.
- The default display converter supports assignable values and string formatting.
  Other conversions require an explicit converter; they return DoNothing otherwise.

This is a deliberately restricted grid, not a general replacement for Avalonia's
editable grid. Keep the existing desktop and TUI behavior tests and the published
NativeAOT terminal smoke test when changing these boundaries. Do not restore
reflection-based fallbacks or broad metadata roots to address a regression.
