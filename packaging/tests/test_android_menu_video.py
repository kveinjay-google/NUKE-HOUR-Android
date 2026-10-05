from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[2]
class AndroidMenuVideoTest(unittest.TestCase):
    def test_home_video_uses_persistent_stream_decoder(self):
        s = (ROOT / 'android/OpenRA.Android/AndroidMenuVideoSource.cs').read_text()
        self.assertNotIn('MediaMetadataRetriever', s)
        self.assertNotIn('GetFrameAtTime', s)
        for operation in ('MediaExtractor', 'MediaCodec', 'DequeueOutputBuffer', 'QueueInputBuffer', 'GetOutputImage'):
            self.assertIn(operation, s)
    def test_loop_rewinds_stream_and_flushes_decoder(self):
        s = (ROOT / 'android/OpenRA.Android/AndroidMenuVideoSource.cs').read_text()
        self.assertIn('codec.Flush()', s)
        self.assertIn('extractor.SeekTo(0', s)
if __name__ == '__main__': unittest.main()
