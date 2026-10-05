from pathlib import Path
import unittest
import re

ROOT = Path(__file__).resolve().parents[2]

class AndroidNativeThanksTest(unittest.TestCase):
    def test_title_and_footer_centers_land_inside_painted_frames(self):
        source = (ROOT / 'android/OpenRA.Android/MainActivityNativePages.cs').read_text()
        phone = source.split('Place(title, .365, .085', 1)[1]
        phone = 'Place(title, .365, .085' + phone
        # Bounds measured on the phone artwork, in its own normalized coordinates.
        frames = {'title': (.36, .085, .64, .16),
                  'support': (.16, .81, .37, .905),
                  'supporters': (.40, .81, .60, .905),
                  'close': (.63, .81, .84, .905)}
        for name, (left, top, right, bottom) in frames.items():
            with self.subTest(control=name):
                match = re.search(r'Place\(' + name + r', ([.\d]+), ([.\d]+), ([.\d]+), ([.\d]+)', phone)
                self.assertIsNotNone(match)
                x, y, w, h = map(float, match.groups())
                self.assertTrue(left <= x + w / 2 <= right)
                self.assertTrue(top <= y + h / 2 <= bottom, 'Text must be centered in its painted frame')
        self.assertIn('art.Drawable.IntrinsicWidth', source)
        self.assertIn('art.Drawable.IntrinsicHeight', source)

    def test_thanks_retains_names_and_actions_without_intro_paragraph(self):
        source = (ROOT / 'android/OpenRA.Android/MainActivityNativePages.cs').read_text()
        page = source.split('void ShowThanksPage()', 1)[1].split('internal void ShowEngineFailure', 1)[0]
        self.assertNotIn('NUKE HOUR 的每一步', page)
        self.assertNotIn('NUKE HOUR exists because', page)
        for text in ('特别鸣谢', '阿顿', '1***r@163.com', 'Yung', '仅在支持者', '支持一下', '官网查看更多', '返回首页'):
            self.assertIn(text, page)

if __name__ == '__main__':
    unittest.main()
