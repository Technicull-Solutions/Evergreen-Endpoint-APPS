"""Generate the Admin derivative icon while preserving the canonical package symbol.
Design-time requirements: Pillow, resvg-py, and fonttools.
"""
from pathlib import Path
from io import BytesIO

from PIL import Image
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
import resvg_py

ROOT = Path(__file__).resolve().parents[2]
BRAND = ROOT / 'media/brand/evergreen-endpoint-package-manager'
ADMIN = ROOT / 'src/Evergreen.Admin/Assets'
FONTS = ROOT / 'src/UniGetUI.Avalonia/Assets/Fonts'


def word_paths(text, font_name, size, x, y, color):
    """Outline bundled Lato glyphs so standalone SVGs need no installed fonts."""
    with TTFont(FONTS / font_name) as font:
        glyphs = font.getGlyphSet()
        cmap = font.getBestCmap()
        scale = size / font['head'].unitsPerEm
        paths = []
        for character in text:
            name = cmap[ord(character)]
            pen = SVGPathPen(glyphs)
            glyphs[name].draw(pen)
            paths.append(f'<path transform="translate({x:.3f} {y}) scale({scale} {-scale})" d="{pen.getCommands()}"/>')
            x += font['hmtx'].metrics[name][0] * scale
        return f'<g fill="{color}">' + ''.join(paths) + '</g>'


def write_wordmarks(icon_svg):
    icon_body = icon_svg[icon_svg.index('>') + 1:icon_svg.rindex('</svg>')]
    for theme, foreground in (('light', '#176B4D'), ('dark', '#DCE5DF')):
        text = word_paths('EVERGREEN', 'Lato-Bold.ttf', 34, 210, 83, foreground)
        text += word_paths('ENDPOINT', 'Lato-Bold.ttf', 34, 210, 123, foreground)
        text += word_paths('ADMIN', 'Lato-Regular.ttf', 58, 455, 113, foreground)
        svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 680 192" role="img" aria-label="Evergreen Endpoint Admin">
<g transform="translate(16 16) scale(.3125)">{icon_body}</g>
<rect x="428" y="52" width="4" height="80" fill="#D69B2D"/>{text}</svg>'''
        (BRAND / f'evergreen-endpoint-admin-logo-{theme}.svg').write_text(svg, encoding='utf-8')
        image = Image.open(BytesIO(resvg_py.svg_to_bytes(svg_string=svg, width=1360, height=384))).convert('RGBA')
        image.save(ADMIN / f'admin-logo-{theme}.png', optimize=True)


def main():
    canonical = (BRAND / 'evergreen-package-manager-icon-dark.svg').read_text(encoding='utf-8')
    symbol = canonical[canonical.index('<g '):canonical.rindex('</svg>')]
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img" aria-label="Evergreen Endpoint Admin: package with management controls">
<rect width="512" height="512" rx="16" fill="#0D3F30"/>
<g transform="translate(8 0) scale(.82)">{symbol}</g>
<rect x="308" y="312" width="188" height="184" rx="16" fill="#D69B2D" stroke="#0D3F30" stroke-width="12"/>
<g stroke="#17211D" stroke-width="14" stroke-linecap="round" fill="none">
<path d="M340 358 H464 M340 404 H464 M340 450 H464"/>
<path d="M374 344 V372 M432 390 V418 M386 436 V464" stroke-width="20"/>
</g></svg>
'''
    (BRAND / 'evergreen-endpoint-admin.svg').write_text(svg, encoding='utf-8')
    ADMIN.mkdir(parents=True, exist_ok=True)
    rendered = Image.open(BytesIO(resvg_py.svg_to_bytes(svg_string=svg, width=512, height=512))).convert('RGBA')
    rendered.save(ADMIN / 'admin.png', optimize=True)
    rendered.save(ADMIN / 'admin.ico', sizes=[(n, n) for n in (16, 20, 24, 32, 40, 48, 64, 128, 256)])
    write_wordmarks(svg)


if __name__ == '__main__':
    main()
