import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]

class AndroidHomeArtworkPolicyTest(unittest.TestCase):
    def test_mobile_footer_uses_baked_shell_and_shared_feedback(self):
        source = (ROOT / 'OpenRA.Mods.RA2/Widgets/MacHomeUtilityButtonWidget.cs').read_text()
        self.assertIn('if (!Platform.UsesMobileLayout)', source)
        self.assertIn('Platform.CurrentPlatform != PlatformType.OSX && !Platform.UsesMobileLayout', source)
        self.assertIn('CalculateArtworkBounds', source)

    def test_labels_share_unclipped_button_artwork_coordinates(self):
        source = (ROOT / 'OpenRA.Mods.RA2/Widgets/MenuIconLabelWidget.cs').read_text()
        self.assertIn('AdaptiveRouteButtonWidget.CalculateArtworkBounds', source)
        self.assertIn('MacHomeUtilityButtonWidget.CalculateArtworkBounds', source)

if __name__ == '__main__':
    unittest.main()
