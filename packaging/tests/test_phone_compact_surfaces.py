from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class PhoneCompactSurfacesTest(unittest.TestCase):
    def test_complex_phone_pages_opt_in(self):
        for name in ('settings', 'lobby', 'multiplayer-browser', 'multiplayer-createserver', 'multiplayer-directconnect'):
            with self.subTest(page=name):
                self.assertIn('CompactPhoneSurface: true', (ROOT / f'mods/ra2/chrome/{name}.yaml').read_text())

    def test_renderer_is_phone_only_and_cloning_preserves_opt_in(self):
        source = (ROOT / 'OpenRA.Mods.RA2/Widgets/StretchBackgroundWidget.cs').read_text()
        self.assertIn('CompactPhoneSurface && Platform.UsesMobileLayout', source)
        self.assertIn('if (phonePolicy.IsPhone)', source)
        self.assertIn('CompactPhoneSurface = other.CompactPhoneSurface;', source)

    def test_phone_reuses_approved_metal_shell_without_flat_placeholder(self):
        source = (ROOT / 'OpenRA.Mods.RA2/Widgets/StretchBackgroundWidget.cs').read_text()
        phone = source[source.index('if (CompactPhoneSurface'):source.index('var bounds = RenderBounds;')]
        self.assertIn('cc-ingame-command-shell', phone)
        self.assertIn('MultiplayerScreenLayout.ContentBounds(screen, compactPhone: true)', phone)
        self.assertIn('CalculateAspectFillCrop(interior, surface.Size)', phone)
        self.assertLess(phone.index('FillRectWithColor'), phone.index('WidgetUtils.DrawSprite(steel'))

    def test_ingame_phone_opts_in_to_shared_shell(self):
        source = (ROOT / 'mods/ra2/chrome/ingame-menu.yaml').read_text()
        self.assertIn('CompactPhoneSurface: true', source)
