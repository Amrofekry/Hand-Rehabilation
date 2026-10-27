"""Stage 11 design-system contrast checks on the saved theme assets (WCAG 2.2 formula; no Unity needed).

Text pairs need 4.5:1 (WCAG 1.4.3 AA). The High Contrast theme needs 7:1 for text (Xbox Accessibility Guideline 102).
The focus ring and selected marker use GripUiContrast.MostVisible: on every control fill they must reach 3:1
(WCAG 1.4.11 / 2.4.11).
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
THEMES = ['Dark', 'Light', 'HighContrast']
TEXT_PAIRS = [('textOnDark', 'surface'), ('textOnDark', 'darkSurface'), ('textOnDark', 'backdrop'),
              ('mutedOnDark', 'surface'), ('mutedOnDark', 'darkSurface'), ('textOnPrimary', 'primary'),
              ('textOnSecondary', 'secondary'), ('danger', 'surface'), ('warning', 'surface'), ('success', 'surface')]


def load(name):
    text = (ROOT / f'Assets/UI/Themes/{name}.asset').read_text(encoding='utf-8')
    return {m.group(1): tuple(float(v) for v in m.groups()[1:])
            for m in re.finditer(r'^  (\w+): \{r: ([\d.e-]+), g: ([\d.e-]+), b: ([\d.e-]+), a: [\d.]+\}', text, re.M)}


def luminance(c):
    return sum(w * (v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4) for w, v in zip((.2126, .7152, .0722), c))


def ratio(a, b):
    x, y = luminance(a), luminance(b)
    return (max(x, y) + .05) / (min(x, y) + .05)


def most_visible(fill, preferred, second, third, minimum):
    """Mirror of GripUiContrast.MostVisible."""
    if ratio(preferred, fill) >= minimum:
        return preferred
    return max((preferred, second, third), key=lambda c: ratio(c, fill))


class UiContrastTests(unittest.TestCase):
    def test_text_pairs_meet_aa(self):
        for name in THEMES:
            theme = load(name)
            minimum = 7 if name == 'HighContrast' else 4.5
            for text, surface in TEXT_PAIRS:
                with self.subTest(theme=name, pair=f'{text}/{surface}'):
                    self.assertGreaterEqual(ratio(theme[text], theme[surface]), minimum)

    def test_focus_ring_reaches_3_to_1_on_every_fill(self):
        for name in THEMES:
            theme = load(name)
            for fill in ('primary', 'secondary', 'surface', 'darkSurface', 'backdrop'):
                ring = most_visible(theme[fill], theme['accent'], theme['textOnDark'], theme['backdrop'], 3)
                with self.subTest(theme=name, fill=fill):
                    self.assertGreaterEqual(ratio(ring, theme[fill]), 3)

    def test_plain_accent_ring_was_invisible_somewhere(self):
        # Records why the contrast-aware ring exists: the old accent-only ring failed on these fills.
        failures = [(n, f) for n in THEMES for f in ('primary', 'secondary')
                    if ratio(load(n)['accent'], load(n)[f]) < 3]
        self.assertIn(('Dark', 'primary'), failures)
        self.assertIn(('HighContrast', 'secondary'), failures)

    def test_selected_marker_reaches_4_5_on_selected_fill(self):
        for name in THEMES:
            theme = load(name)
            marker = most_visible(theme['primary'], theme['textOnPrimary'], theme['textOnDark'], theme['backdrop'], 4.5)
            with self.subTest(theme=name):
                self.assertGreaterEqual(ratio(marker, theme['primary']), 4.5)


if __name__ == '__main__':
    unittest.main()
