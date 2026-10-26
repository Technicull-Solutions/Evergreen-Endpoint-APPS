# Technicull accessibility baseline

Apply this baseline to Evergreen employee and administrator experiences and use it as
the starting point for other Technicull products. This repository cannot enforce changes
in other applications. No product-wide conformance claim has been established.

Target WCAG 2.2 Level AA, applying the W3C WCAG2ICT guidance to desktop software.
Track the WCAG 3 Working Draft as evolving design guidance, not a completed conformance
standard. Revisit draft changes before adopting requirements; retain the measurable 2.2 gate.

Sources: [WCAG 2.2](https://www.w3.org/TR/WCAG22/),
[WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/),
[WCAG 3 Working Draft](https://www.w3.org/TR/wcag-3.0/) (reviewed 2026-10-18).

## Design and implementation

- Normal text: at least 4.5:1; large text: at least 3:1. Meaningful control boundaries,
  icons, and state indicators: at least 3:1 against adjacent colours. Measure final
  composited colours, including hover, pressed, selected, and focus states.
- Use shared semantic theme tokens. Keep log bodies white in light mode and dark ink
  in dark mode. Use Evergreen selection and amber warning actions with explicit labels.
- Preserve platform keyboard focus adorners. Keyboard focus must be visible and not
  obscured, with meaningful order, no traps, and predictable restoration after dialogs.
- Provide names, roles, values, and status announcements through accessibility APIs.
  Action names must contain their visible label. Hide only decorative content from readers.
- Do not use colour alone: include status text, icons, selected-state semantics, and
  clear warning/error explanations. Disabled states must remain understandable.
- Interactive targets should be at least 24 by 24 logical pixels or satisfy the WCAG
  spacing exception; prefer 32 or larger for desktop controls.
- Support text enlargement, 200% scaling, narrow windows, long translations, and OS
  high contrast. Content should wrap without concealing actions or requiring two-axis
  scrolling except for genuinely two-dimensional content such as tables.
- Respect reduced motion and user theme preferences; make errors actionable and avoid
  unnecessary memory, precision, or repeated-input demands.

## Release evidence

Run `python scripts/validate-brand-contrast.py` for the measured shared colour pairs.
This checks tokens, not rendered controls or complete WCAG conformance.

Before the signed MVP, manually exercise Apps, Search apps, Updates, Activity history,
log/error dialogs, settings, and administrator import/export in both themes. Cover:
keyboard-only operation; Narrator names and announcements; focus on open/dismiss;
hover/press/selection visibility; 100/150/200% scaling; narrow windows; high contrast;
and reduced motion. Record OS, architecture, build, test steps, and results. Test the
published NativeAOT package, not only Debug. Any untested state remains a gap.

The current change improves shared control boundaries, interaction states, warning
actions, log surfaces, and wrapping of the settings label/action row. Full assistive
technology, high-contrast, and rendered-state validation remains required.
