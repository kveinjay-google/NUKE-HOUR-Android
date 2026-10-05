from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
LOGIC = ROOT / 'engine/OpenRA.Mods.Common/Widgets/Logic'


class FixedColorPolicyTest(unittest.TestCase):
    def test_all_room_modes_use_the_fixed_palette(self):
        source = (LOGIC / 'Lobby/LobbyLogic.cs').read_text()
        self.assertIn('LobbyUtils.SetupEditableClassicColorWidget(', source)
        self.assertNotIn('LobbyUtils.SetupEditableColorWidget(', source)

    def test_legacy_room_entry_cannot_reintroduce_custom_mixer(self):
        source = (LOGIC / 'Lobby/LobbyUtils.cs').read_text()
        method = source.split('public static void SetupEditableColorWidget(', 1)[1].split('public static void SetupEditableClassicColorWidget(', 1)[0]
        self.assertIn('SetupEditableClassicColorWidget(parent, s, c, orderManager, colorManager);', method)
        self.assertNotIn('ShowColorDropDown(', method)

    def test_settings_keep_fixed_palette(self):
        source = (LOGIC / 'Settings/DisplaySettingsLogic.cs').read_text()
        self.assertIn('ShowClassicColorDropDown(colorDropdown', source)
        self.assertNotIn('ShowColorDropDown(colorDropdown', source)


if __name__ == '__main__':
    unittest.main()
