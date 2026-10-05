"""Guard Android's opt-in to bounded shared music decoding and resource snapshots."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]

class StreamingAudioDiagnosticsContract(unittest.TestCase):
    def test_named_music_uses_shared_streaming_mixer(self):
        sound = (ROOT / 'android/OpenRA.Platforms.Android/AndroidSoundEngine.cs').read_text()
        self.assertIn('ISoundEngine, IStreamingSoundEngine', sound)
        self.assertIn('Play2DStream(Func<Stream> open', sound)
        self.assertIn('Add(mixer.PlayStream(open, channels, sampleBits, sampleRate, loop, volume, relative, pos))', sound)
        self.assertIn('Sdl2Sound Add(PcmPlayback playback)', sound)
        self.assertIn('mixerThread.Join(500)', sound)

    def test_resource_snapshots_remain_game_thread_observations(self):
        diagnostics = (ROOT / 'android/OpenRA.Android/AndroidFailureDiagnostics.cs').read_text()
        self.assertIn('ChromeProvider.GetCacheSnapshot()', diagnostics)
        self.assertIn('models.GetCacheSnapshot()', diagnostics)
        self.assertIn('chrome.colorPixelEstimateBytes=', diagnostics)
        self.assertIn('model.colorPixelEstimateBytes=', diagnostics)
        self.assertIn('Game.RunAfterTick(() => { if (!disposed) Observe(force: true); });', diagnostics)
        self.assertIn('AndroidFailureReport.Capture', diagnostics)

if __name__ == '__main__':
    unittest.main()
