import ctypes
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]

class AndroidNativeVideoPixelsTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        compiler = shutil.which('clang') or shutil.which('cc')
        if not compiler:
            raise unittest.SkipTest('A C compiler is required for native pixel conversion checks')
        cls.temp = tempfile.TemporaryDirectory()
        library = str(Path(cls.temp.name) / 'video-pixels.so')
        subprocess.run([compiler, '-O3', '-shared', '-fPIC', str(ROOT / 'android/native/menu-video-pixels.c'), '-o', library], check=True)
        cls.library = ctypes.CDLL(library)
        cls.convert = cls.library.nh_video_convert420
        pointer = ctypes.POINTER(ctypes.c_ubyte)
        cls.convert.argtypes = [pointer] * 3 + [ctypes.c_int] * 11 + [pointer]
        cls.convert.restype = None

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def check_pixels(self, y, u, v, layout, expected):
        planes = [(ctypes.c_ubyte * len(p))(*p) for p in (y, u, v)]
        output = (ctypes.c_ubyte * len(expected))()
        self.convert(*planes, *layout, output)
        self.assertEqual(expected, list(output))

    def test_limited_black_white(self):
        self.check_pixels([16,235], [128], [128], [2,1,1,1,1,0,0,2,1,1,0], [0,0,0,255,255,255,255,255])

    def test_cropped_interleaved_chroma_skips_padding(self):
        self.check_pixels([16,16,16,16,16,16,63,63], [128,99,102], [128,99,240],
                          [4,4,4,2,2,2,1,2,1,1,0], [0,1,255,255,0,1,255,255])

    def test_full_range_white(self):
        self.check_pixels([255], [128], [128], [1,1,1,1,1,0,0,1,1,0,1], [255,255,255,255])

if __name__ == '__main__': unittest.main()
