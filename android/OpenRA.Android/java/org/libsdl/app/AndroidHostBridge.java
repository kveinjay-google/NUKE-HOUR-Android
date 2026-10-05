package org.libsdl.app;

import android.app.Activity;
import android.os.Build;
import android.view.Display;
import android.view.WindowManager;
import android.util.Log;
import android.view.Surface;
import android.view.KeyEvent;
import android.view.View;

/**
 * Minimal host bridge used by the .NET-owned Android host (Model B).
 *
 * The .NET launcher activity (a normal android.app.Activity) hosts an
 * {@link SDLSurface} created here, registers it with SDLActivity's static
 * surface slot, and hands the view back to managed code. SDL2 is loaded from a
 * Java context so that libSDL2.so's JNI_OnLoad can register its natives against
 * the org.libsdl.app classes through the application class loader.
 *
 * SDLActivity itself is never started as an Activity in this model; only its
 * class-level glue (static natives + static surface/context slots) is used.
 */
final class AndroidHostBridge {
    private static final String TAG = "OpenRA.HostBridge";

    private static SDLSurface sdlSurface;
    private static Activity hostActivity;
    private static boolean managedHostStarted;
    private static boolean activityResumed;
    private static boolean nativePaused;
    private static float desiredRenderRate;

    public static synchronized void startManagedHost() {
        managedHostStarted = true;
    }

    public static synchronized void pauseManagedHost() {
        activityResumed = false;
        pauseForSurfaceLoss();
    }

    public static synchronized void pauseForSurfaceLoss() {
        if (managedHostStarted && !nativePaused) {
            nativePaused = true;
            SDLActivity.nativePause();
        }
        if (sdlSurface != null) sdlSurface.handlePause();
        SDLActivity.mCurrentNativeState = SDLActivity.NativeState.PAUSED;
    }

    public static synchronized void resumeManagedHost() {
        activityResumed = true;
        requestHighRefreshRate();
        resumeAfterSurfaceChange();
    }

    public static synchronized void resumeAfterSurfaceChange() {
        // SDL recreates its EGL surface in onNativeSurfaceChanged before this.
        if (!activityResumed || !isSurfaceReady() || !sdlSurface.mIsSurfaceReady) return;
        if (managedHostStarted && nativePaused) {
            nativePaused = false;
            SDLActivity.nativeResume();
        }
        sdlSurface.handleResume();
        SDLActivity.mCurrentNativeState = SDLActivity.NativeState.RESUMED;
    }

    static {
        System.loadLibrary("SDL2");
        Log.i(TAG, "SDL2 loaded from Java context (JNI_OnLoad executed)");
    }

    private AndroidHostBridge() {
    }

    /** Create the SDLSurface, register it, and return it as a View for managed code. */
    public static View createSDLSurface(Activity activity) {
        hostActivity = activity;
        // Mirror SDLActivity.onCreate ordering without running SDLActivity:
        // setupJNI caches the org.libsdl.app classes natively and marks SDL
        // main ready; initialize resets Java state; setContext provides the
        // activity context to the glue.
        SDL.setupJNI();
        SDL.initialize();
        SDL.setContext(activity);
        sdlSurface = new SDLSurface(activity);
        SDLActivity.mSurface = sdlSurface; // same package: protected static slot
        Log.i(TAG, "SDLSurface created and registered with SDLActivity");
        return sdlSurface;
    }

    public static void sendEscapeKey() {
        if (!managedHostStarted) return;
        // Match SDLSurface/SDLActivity's native key route. Never call managed
        // widget handlers from the Android UI thread.
        SDLActivity.onNativeKeyDown(KeyEvent.KEYCODE_ESCAPE);
        SDLActivity.onNativeKeyUp(KeyEvent.KEYCODE_ESCAPE);
    }

    public static Activity getHostActivity() {
        return hostActivity;
    }

    public static boolean isSurfaceReady() {
        if (sdlSurface == null) {
            return false;
        }
        Surface s = sdlSurface.getHolder().getSurface();
        return s != null && s.isValid();
    }

    public static int surfaceWidth() {
        return sdlSurface == null ? 0 : sdlSurface.getHolder().getSurfaceFrame().width();
    }

    public static int surfaceHeight() {
        return sdlSurface == null ? 0 : sdlSurface.getHolder().getSurfaceFrame().height();
    }

    public static void requestRenderFrameRate(float frameRate) {
        desiredRenderRate = frameRate;
        if (hostActivity != null) hostActivity.runOnUiThread(new Runnable() {
            @Override public void run() { requestHighRefreshRate(); }
        });
    }

    public static void requestHighRefreshRate() {
        if (hostActivity == null || Build.VERSION.SDK_INT < 23) return;
        try {
            Display display = hostActivity.getWindowManager().getDefaultDisplay();
            Display.Mode active = display.getMode();
            float highest = active.getRefreshRate();
            for (Display.Mode mode : display.getSupportedModes()) {
                if (mode.getPhysicalWidth() == active.getPhysicalWidth() &&
                    mode.getPhysicalHeight() == active.getPhysicalHeight())
                    highest = Math.max(highest, mode.getRefreshRate());
            }
            float target = desiredRenderRate > 0 ? Math.min(highest, desiredRenderRate) : highest;
            WindowManager.LayoutParams attributes = hostActivity.getWindow().getAttributes();
            attributes.preferredRefreshRate = target;
            hostActivity.getWindow().setAttributes(attributes);
            Surface surface = sdlSurface == null ? null : sdlSurface.getHolder().getSurface();
            if (Build.VERSION.SDK_INT >= 30 && surface != null && surface.isValid())
                surface.setFrameRate(target, Surface.FRAME_RATE_COMPATIBILITY_DEFAULT);
            Log.i(TAG, "Refresh request: " + target + " Hz; display=" + display.getRefreshRate() +
                " Hz; supported=" + highest + " Hz; render cap=" + desiredRenderRate);
        } catch (RuntimeException error) {
            Log.w(TAG, "Refresh request unavailable", error);
        }
    }

    public static float displayRefreshRate() {
        if (hostActivity == null) {
            return 0f;
        }
        android.view.Display display = ((android.view.WindowManager)
                hostActivity.getSystemService(android.content.Context.WINDOW_SERVICE)).getDefaultDisplay();
        return display.getRefreshRate();
    }

    public static String eglVendor() {
        return android.opengl.EGL14.eglQueryString(android.opengl.EGL14.eglGetCurrentDisplay(), android.opengl.EGL14.EGL_VENDOR);
    }

    public static String eglVersion() {
        return android.opengl.EGL14.eglQueryString(android.opengl.EGL14.eglGetCurrentDisplay(), android.opengl.EGL14.EGL_VERSION);
    }
}
