"""Regression for GLES3 drivers that omit the legacy ES2 derivatives extension."""
from pathlib import Path
import unittest

class Gles3FeatureContract(unittest.TestCase):
    def test_core_derivatives_do_not_require_legacy_extension(self):
        source = (Path(__file__).resolve().parents[2] / 'engine/OpenRA.Platforms.Default/OpenGL.cs').read_text()
        self.assertIn('var hasDerivatives = isEmbedded && major >= 3 ||', source)
        self.assertIn('if (isEmbedded && hasBGRA && hasDerivatives && major >= 3)', source)
        self.assertIn('else if (!isEmbedded &&', source)

class AndroidDrawableContract(unittest.TestCase):
    def test_resize_updates_window_and_drawable_before_notifying_scale(self):
        source = (Path(__file__).resolve().parents[2] / 'engine/OpenRA.Platforms.Default/Sdl2PlatformWindow.cs').read_text()
        method = source.split('internal void WindowSizeChanged()', 1)[1].split('if (OperatingSystem.IsIOS())', 1)[0]
        self.assertIn('if (Platform.IsAndroid)', method)
        self.assertIn('SDL.SDL_GetWindowSize', method)
        self.assertIn('SDL.SDL_GL_GetDrawableSize', method)
        self.assertLess(method.index('windowSize ='), method.index('OnWindowScaleChanged('))
        self.assertLess(method.index('surfaceSize ='), method.index('OnWindowScaleChanged('))

    def test_render_thread_refreshes_default_viewport_before_offscreen_capture(self):
        source = (Path(__file__).resolve().parents[2] / 'engine/OpenRA.Platforms.Default/Sdl2GraphicsContext.cs').read_text()
        clear = source.split('public void Clear()', 1)[1].split('public void EnableDepthBuffer()', 1)[0]
        self.assertIn('VerifyThreadAffinity();', clear)
        self.assertIn('lastAndroidSurfaceSize != window.SurfaceSize', clear)
        self.assertLess(clear.index('OpenGL.glViewport'), clear.index('OpenGL.glClear('))

if __name__ == '__main__':
    unittest.main()
