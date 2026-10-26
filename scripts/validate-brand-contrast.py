"""Check shared opaque theme pairs; this is not a rendered UI/WCAG conformance audit."""
from pathlib import Path
import xml.etree.ElementTree as ET


def luminance(value):
    if len(value) != 7 or not value.startswith('#'):
        raise ValueError(f'Expected opaque RGB colour, got {value}')
    rgb = [int(value[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in rgb]
    return sum(a * b for a, b in zip(linear, (0.2126, 0.7152, 0.0722)))


def contrast(a, b):
    light, dark = sorted((luminance(a), luminance(b)), reverse=True)
    return (light + 0.05) / (dark + 0.05)


def composite(foreground, background):
    if len(foreground) == 7:
        return foreground
    alpha = int(foreground[1:3], 16) / 255
    channels = [round(int(foreground[i:i + 2], 16) * alpha
                      + int(background[i - 2:i], 16) * (1 - alpha))
                for i in (3, 5, 7)]
    return '#' + ''.join(f'{value:02X}' for value in channels)


root = Path(__file__).resolve().parents[1]
path = root / 'src/UniGetUI.Avalonia/Assets/Styles/Styles.Common.axaml'
tree = ET.parse(path)
ns = {'a': 'https://github.com/avaloniaui'}
key = '{http://schemas.microsoft.com/winfx/2006/xaml}Key'
pairs = [
    ('TextFillColorPrimaryBrush', 'LogBodyBackground', 4.5),
    ('BrandSelectionForeground', 'BrandSelectionBackground', 4.5),
    ('BrandHoverForeground', 'BrandHoverBackground', 4.5),
    ('TextOnAccentFillColorPrimaryBrush', 'PackageActionHoverBackground', 4.5),
    ('WarningActionForeground', 'WarningActionBackground', 4.5),
    ('WarningActionForeground', 'WarningActionHoverBackground', 4.5),
    ('AccessibleControlBorderBrush', 'SettingsCardBackground', 3),
    ('AccessibleControlBorderBrush', 'LogBodyBackground', 3),
    ('StatusErrorForeground', 'LogBodyBackground', 4.5),
]
failures = []
for theme in tree.findall('./a:Styles.Resources/a:ResourceDictionary/a:ResourceDictionary.ThemeDictionaries/a:ResourceDictionary', ns):
    colours = {el.attrib.get(key): el.attrib['Color'] for el in theme.findall('a:SolidColorBrush', ns)}
    for foreground, background, minimum in pairs:
        ratio = contrast(colours[foreground], colours[background])
        result = f'{theme.attrib[key]}: {foreground} / {background}: {ratio:.2f}:1 (minimum {minimum}:1)'
        print(result)
        if ratio < minimum:
            failures.append(result)
    # View-selector underline against its selected, hovered and pressed fills.
    # Dark fills are translucent, so measure their final colour over the page.
    for surface in ('ControlFillColorDefaultBrush', 'ControlFillColorSecondaryBrush',
                    'ControlFillColorTertiaryBrush'):
        background = composite(colours[surface], colours['AppWindowBackground'])
        ratio = contrast(colours['BrandSelectionBackground'], background)
        result = f'{theme.attrib[key]}: view indicator / {surface}: {ratio:.2f}:1 (minimum 3:1)'
        print(result)
        if ratio < 3:
            failures.append(result)
# The standalone administrator shell deliberately avoids the employee engine dependency.
# Check its actual resources too, so independently packaged styles retain the same baseline.
admin = ET.parse(root / 'src/Evergreen.Admin/AdminBrandStyles.axaml')
admin_pairs = [
    ('AdminText', 'AdminPage', 4.5),
    ('AdminText', 'AdminCard', 4.5),
    ('AdminText', 'AdminEditor', 4.5),
    ('AdminSelectedText', 'AdminSelected', 4.5),
    ('AdminHoverText', 'AdminHover', 4.5),
    ('AdminHeaderText', 'AdminHeader', 4.5),
    ('AdminPrimaryText', 'AdminPrimary', 4.5),
    ('AdminPrimaryText', 'AdminPrimaryHover', 4.5),
    ('AdminBorder', 'AdminEditor', 3),
    ('AdminBorder', 'AdminCard', 3),
    ('AdminBorder', 'AdminPage', 3),
]
for theme in admin.findall('./a:Styles.Resources/a:ResourceDictionary/a:ResourceDictionary.ThemeDictionaries/a:ResourceDictionary', ns):
    colours = {el.attrib.get(key): el.attrib['Color'] for el in theme.findall('a:SolidColorBrush', ns)}
    for foreground, background, minimum in admin_pairs:
        ratio = contrast(colours[foreground], colours[background])
        result = f'Admin {theme.attrib[key]}: {foreground} / {background}: {ratio:.2f}:1 (minimum {minimum}:1)'
        print(result)
        if ratio < minimum:
            failures.append(result)
    for foreground, overlay, background, minimum in [
        ('AdminSelected', 'AdminSelectorFill', 'AdminPage', 3),
        ('AdminHeaderText', 'AdminSubtleHover', 'AdminHeader', 4.5),
    ]:
        ratio = contrast(colours[foreground], composite(colours[overlay], colours[background]))
        result = f'Admin {theme.attrib[key]}: {foreground} / {overlay} over {background}: {ratio:.2f}:1 (minimum {minimum}:1)'
        print(result)
        if ratio < minimum:
            failures.append(result)
if failures:
    raise SystemExit('Contrast failures:\n' + '\n'.join(failures))
