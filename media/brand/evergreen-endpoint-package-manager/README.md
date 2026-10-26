# Evergreen Endpoint | APPS — package-manager identity

This folder contains the approved icon and wordmark assets for **Evergreen Endpoint | APPS**.
The project, installer, policies, and management utility retain the full **Evergreen Endpoint
Package Manager** name.

## Canonical symbol

The package-manager symbol is the supplied download arrow entering an open package. Use the
bounded icon where Windows needs a stable application tile and the supplied background-free
variants in the notification area and on product-owned surfaces. Do not substitute a sprout,
shield, check, monogram, or a newly drawn variant.

- `evergreen-package-manager-icon-dark.svg` is the dark rounded-square Windows app icon.
- `evergreen-package-manager-logo-light.svg` is the transparent light-surface logo.
- `png/evergreen-endpoint-apps-logo-dark-*.png` is the supplied background-free mark for light
  surfaces and taskbars.
- `png/evergreen-endpoint-apps-logo-light-*.png` is the supplied background-free mark for dark
  surfaces and taskbars.
- `png/EEA-Logo-Text_Light*.png` is the supplied light-theme front-end wordmark family.
- `png/EEA-Logo-Text_Light-Alternate*.png` is the supplied dark-theme front-end wordmark family.
- `png/EEA-Logo-Text_Dark*.png` is retained for use on light, unbranded surfaces.
- `evergreen-endpoint-package-manager.ico` is the generated multi-resolution Windows icon.
- `png/` contains the supplied logo families and generated Windows and tray sizes.
- `icon-validation-v0.3.png` shows the canonical pair and small-size behavior.
- `app-shell-preview-v0.2.png` shows the product-owned titlebar and application shell.

## Core palette

| Token | Value | Intended use |
|---|---:|---|
| Evergreen | `#176B4D` | Light-theme primary chrome and actions |
| Faded mint | `#E2F5E9` | Light-theme page surface |
| Ink | `#17211D` | Text on faded mint |
| Paper | `#F7FAF8` | Text on evergreen and light card surfaces |
| Forest | `#0D3F30` | Dark-theme primary chrome and actions |
| Forest night | `#27332D` | Dark-theme page surface |
| Silverleaf | `#DCE5DF` | Dark-theme text on forest and forest night |

Use Lato for supplied wordmarks and Segoe UI Variable/Segoe UI for the Windows interface.

The Updates completion message uses bundled, unmodified Lato Regular and Bold from
`src/UniGetUI.Avalonia/Assets/Fonts/`, including its SIL Open Font License. Its message is
“Your apps are up to date!” using Evergreen in light mode and the mark's mint (`#79CDB0`)
in dark mode, with gold (`#D69B2D`) accents. The interim completion illustration combines
the canonical package mark on an r8 tile with a small status badge and no circular outline;
it does not replace the application icon. Replace the illustration in `UpdatesEmptyState.axaml`
when final artwork is supplied.

Regenerate the raster, ICO, Windows tile, and installer assets with:

```powershell
python scripts/brand/generate-evergreen-endpoint-package-manager-assets.py
```

The design-time generator requires Pillow and `resvg-py`. These are not application runtime
dependencies.

## Portable administrator identity

**Evergreen Endpoint | ADMIN** retains the canonical package symbol and adds a gold
management-controls badge. This derivative identifies the separate authoring utility;
it does not replace the APPS symbol. Its forest tile has r8 corners at 256 logical pixels.
`evergreen-endpoint-admin.svg` is the generated vector, and `src/Evergreen.Admin/Assets/`
contains the PNG and multi-resolution ICO used for the executable, window, and taskbar.
Regenerate them with `python scripts/brand/generate-admin-assets.py` (Pillow, `resvg-py`, and
`fonttools`). The same generator produces `evergreen-endpoint-admin-logo-light.svg` and
`evergreen-endpoint-admin-logo-dark.svg` plus PNGs in the Admin assets folder. The SVG
wordmarks outline the bundled Lato glyphs so they do not require an installed font.
The portable Admin ZIP includes these alternate logos and the ICO in its `Brand/` folder.
The administrator shell uses the same palette, Lato wordmark, and Segoe UI interface fonts,
with dedicated standalone styles to avoid loading the employee package engine.
