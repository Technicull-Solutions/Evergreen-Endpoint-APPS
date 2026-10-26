#!/usr/bin/env python3
"""Generate Windows assets from the approved Evergreen package-manager SVGs."""

from __future__ import annotations

from io import BytesIO
from pathlib import Path
import shutil

from PIL import Image, ImageDraw, ImageFont
import resvg_py


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "media" / "brand" / "evergreen-endpoint-package-manager"
PNG = OUT / "png"
APP_BRAND = ROOT / "src" / "UniGetUI.Avalonia" / "Assets" / "Brand"
SHARED_ASSETS = ROOT / "src" / "SharedAssets"
INSTALLER_ASSETS = ROOT / "InstallerExtras"

DARK_SVG = OUT / "evergreen-package-manager-icon-dark.svg"
LIGHT_SVG = OUT / "evergreen-package-manager-logo-light.svg"
WORDMARK_PNGS = (
    "EEA-Logo-Text_Dark.png",
    "EEA-Logo-Text_Dark@0.5.png",
    "EEA-Logo-Text_Dark@2x.png",
    "EEA-Logo-Text_Light-Alternate.png",
    "EEA-Logo-Text_Light-Alternate@0.5.png",
    "EEA-Logo-Text_Light-Alternate@2x.png",
    "EEA-Logo-Text_Light.png",
    "EEA-Logo-Text_Light@0.5.png",
    "EEA-Logo-Text_Light@2x.png",
)
TRANSPARENT_LOGO_SIZES = (16, 32, 64, 128, 256, 512, 1024)
TRANSPARENT_LOGO_PNGS = tuple(
    f"evergreen-endpoint-apps-logo-{theme}-{size}.png"
    for theme in ("dark", "light")
    for size in TRANSPARENT_LOGO_SIZES
)

COLORS = {
    "deep_forest": "#0D3F30",
    "evergreen": "#176B4D",
    "forest_night": "#27332D",
    "faded_mint": "#E2F5E9",
    "gold": "#D69B2D",
    "mint": "#79CDB0",
    "paper": "#F7FAF8",
    "ink": "#17211D",
    "muted": "#5F6C66",
    "line": "#DCE5DF",
    "white": "#FFFFFF",
}


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(Path("C:/Windows/Fonts") / name), size=size)


def render_svg(path: Path, size: int) -> Image.Image:
    png_bytes = resvg_py.svg_to_bytes(
        svg_path=str(path), width=size, height=size, shape_rendering="geometric_precision"
    )
    with Image.open(BytesIO(png_bytes)) as rendered:
        return rendered.convert("RGBA")


def save_png(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path = path.with_name(f".{path.name}.tmp")
    image.save(temporary_path, format="PNG", optimize=True)
    temporary_path.replace(path)


def load_transparent_logo(theme: str, size: int) -> Image.Image:
    """Load the supplied transparent mark, using the nearest larger master if needed."""
    source_size = next(
        (candidate for candidate in TRANSPARENT_LOGO_SIZES if candidate >= size),
        TRANSPARENT_LOGO_SIZES[-1],
    )
    with Image.open(PNG / f"evergreen-endpoint-apps-logo-{theme}-{source_size}.png") as source:
        logo = source.convert("RGBA")
    if logo.size != (size, size):
        logo = logo.resize((size, size), Image.Resampling.LANCZOS)
    return logo


def write_canonical_copies() -> None:
    """Expose the approved icon, wordmark, and transparent-mark assets to Avalonia."""
    APP_BRAND.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(DARK_SVG, APP_BRAND / DARK_SVG.name)
    shutil.copyfile(LIGHT_SVG, APP_BRAND / LIGHT_SVG.name)
    for file_name in (*WORDMARK_PNGS, *TRANSPARENT_LOGO_PNGS):
        shutil.copyfile(PNG / file_name, APP_BRAND / file_name)


def write_windows_assets() -> None:
    assets = SHARED_ASSETS / "Assets"
    images = assets / "Images"
    dark_512 = render_svg(DARK_SVG, 512)
    light_512 = render_svg(LIGHT_SVG, 512)

    save_png(dark_512, images / "icon.png")
    save_png(light_512, images / "icon_unsquare.png")
    dark_512.convert("RGB").save(images / "icon.bmp")

    ico_sizes = [
        (16, 16),
        (20, 20),
        (24, 24),
        (32, 32),
        (48, 48),
        (64, 64),
        (128, 128),
        (256, 256),
    ]
    for path in (
        SHARED_ASSETS / "icon.ico",
        images / "icon.ico",
        OUT / "evergreen-endpoint-package-manager.ico",
    ):
        dark_512.save(path, format="ICO", sizes=ico_sizes)

    for file_name, size in (
        ("Square44x44Logo.png", 44),
        ("Square150x150Logo.png", 150),
        ("StoreLogo.png", 50),
    ):
        save_png(render_svg(DARK_SVG, size), assets / file_name)

    wide = Image.new("RGBA", (310, 150), COLORS["deep_forest"])
    wordmark = Image.open(APP_BRAND / "EEA-Logo-Text_Light-Alternate@2x.png").convert("RGBA")
    wordmark.thumbnail((290, 96), Image.Resampling.LANCZOS)
    wide.alpha_composite(
        wordmark,
        ((wide.width - wordmark.width) // 2, (wide.height - wordmark.height) // 2),
    )
    save_png(wide, assets / "Wide310x150Logo.png")

    write_installer_assets()


def write_installer_assets() -> None:
    """Compose the approved icon and APPS wordmark at 4x installer resolution."""
    scale = 4
    banner = Image.new("RGB", (164 * scale, 314 * scale), COLORS["deep_forest"])
    mark = render_svg(DARK_SVG, 132 * scale)
    banner.paste(mark, (16 * scale, 28 * scale), mark)
    with Image.open(APP_BRAND / "EEA-Logo-Text_Light-Alternate@2x.png") as source:
        wordmark = source.convert("RGBA")
    width = 140 * scale
    height = round(wordmark.height * width / wordmark.width)
    wordmark = wordmark.resize((width, height), Image.Resampling.LANCZOS)
    banner.paste(wordmark, ((banner.width - width) // 2, 196 * scale), wordmark)
    save_png(banner, INSTALLER_ASSETS / "installer-banner.png")
    save_png(render_svg(DARK_SVG, 256), INSTALLER_ASSETS / "evergreen-package-manager-256.png")


def write_tray_status_assets() -> None:
    """Build every runtime tray state from the supplied background-free artwork."""
    images = SHARED_ASSETS / "Assets" / "Images"
    status_colors = {
        "empty": None,
        "blue": "#3B82F6",
        "green": "#22C55E",
        "orange": "#F59E0B",
        "turquoise": "#22D3C5",
    }

    # TrayService uses the historical black/white suffixes to describe the taskbar
    # contrast requirement. Dark artwork belongs on light taskbars and vice versa.
    variants = {
        "black": ("dark", COLORS["paper"]),
        "white": ("light", COLORS["ink"]),
    }
    ico_sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (48, 48), (64, 64)]

    for suffix, (theme, dot_outline) in variants.items():
        base = load_transparent_logo(theme, 256)
        for status, color in status_colors.items():
            icon = base.copy()
            if color is not None:
                draw = ImageDraw.Draw(icon)
                draw.ellipse(
                    (174, 174, 250, 250),
                    fill=color,
                    outline=dot_outline,
                    width=10,
                )
            icon.save(
                images / f"tray_{status}_{suffix}_legacy.ico",
                format="ICO",
                sizes=ico_sizes,
            )


def build_validation_preview() -> Image.Image:
    width, height = 1500, 920
    board = Image.new("RGB", (width, height), COLORS["paper"])
    draw = ImageDraw.Draw(board)
    title = font("Lato-Black.ttf", 46)
    heading = font("Lato-Bold.ttf", 24)
    body = font("Lato-Regular.ttf", 18)
    small = font("Lato-Regular.ttf", 15)

    draw.text((64, 48), "Evergreen Endpoint | APPS", fill=COLORS["ink"], font=title)
    draw.text(
        (66, 108),
        "Approved package-manager icon · exact download-button artwork",
        fill=COLORS["muted"],
        font=body,
    )

    draw.rounded_rectangle((64, 164, 710, 610), radius=24, fill=COLORS["white"], outline=COLORS["line"], width=2)
    draw.text((94, 190), "DARK / WINDOWS APP ICON", fill=COLORS["deep_forest"], font=heading)
    dark = render_svg(DARK_SVG, 330)
    board.paste(dark, (220, 246), dark)

    draw.rounded_rectangle((790, 164, 1436, 610), radius=24, fill="#18211E", outline=COLORS["line"], width=2)
    draw.text((820, 190), "LIGHT / BACKGROUND-FREE MARK", fill=COLORS["white"], font=heading)
    light = load_transparent_logo("light", 330)
    board.paste(light, (946, 246), light)

    draw.text((64, 662), "WINDOWS SIZE CHECK", fill=COLORS["ink"], font=heading)
    x = 64
    for icon_size in (128, 64, 48, 32, 24, 20, 16):
        icon = render_svg(DARK_SVG, icon_size)
        board.paste(icon, (x, 718), icon)
        draw.text((x, 858), f"{icon_size}px", fill=COLORS["muted"], font=small)
        x += max(icon_size + 54, 94)
    draw.text(
        (870, 730),
        "Bounded app icon for Windows shell; background-free mark for tray and UI.",
        fill=COLORS["ink"],
        font=body,
    )
    draw.text(
        (870, 768),
        "No alternate sprout, shield, check, or monogram variants.",
        fill=COLORS["muted"],
        font=body,
    )
    return board


def main() -> None:
    if not DARK_SVG.is_file() or not LIGHT_SVG.is_file():
        raise FileNotFoundError("The approved dark and light package-manager SVGs are required")
    missing_pngs = [file_name for file_name in TRANSPARENT_LOGO_PNGS if not (PNG / file_name).is_file()]
    if missing_pngs:
        raise FileNotFoundError(f"Missing supplied transparent logo PNGs: {', '.join(missing_pngs)}")

    PNG.mkdir(parents=True, exist_ok=True)
    write_canonical_copies()

    sizes = (16, 20, 24, 32, 44, 48, 64, 128, 150, 256, 512)
    for size in sizes:
        save_png(render_svg(DARK_SVG, size), PNG / f"app-icon-{size}.png")

    for surface, artwork in (("light", "dark"), ("dark", "light")):
        for size in (16, 20, 24, 32):
            save_png(load_transparent_logo(artwork, size), PNG / f"tray-{surface}-{size}.png")

    write_windows_assets()
    write_tray_status_assets()
    preview = build_validation_preview()
    save_png(preview, OUT / "identity-preview-v0.1.png")
    save_png(preview, OUT / "icon-validation-v0.3.png")


if __name__ == "__main__":
    main()
