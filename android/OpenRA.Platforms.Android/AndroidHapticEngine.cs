using System;
using Android.Content;
using Android.OS;

namespace OpenRA.Platforms.Default
{
    /// <summary>Native impact feedback using the strength already resolved by the game.</summary>
    sealed class AndroidHapticEngine : IHapticEngine
    {
        readonly object sync = new();
        readonly Vibrator vibrator;
        bool disposed;

        public AndroidHapticEngine()
        {
            var context = global::Android.App.Application.Context;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S &&
                context.GetSystemService(Context.VibratorManagerService) is VibratorManager manager)
                vibrator = manager.DefaultVibrator;
            else
                vibrator = context.GetSystemService(Context.VibratorService) as Vibrator;
        }

        public void Play(HapticEffect effect, float intensity)
        {
            if (!float.IsFinite(intensity))
                return;
            var strength = Math.Clamp(intensity, 0f, 1f);
            if (strength <= 0f)
                return;

            lock (sync)
            {
                if (disposed || vibrator == null || !vibrator.HasVibrator)
                    return;

                // Match the heavy nuclear / medium lightning distinction. Devices
                // without amplitude control express attenuation using pulse length.
                var duration = effect == HapticEffect.NuclearExplosion ? 90L : 35L;
                try
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        var amplitude = vibrator.HasAmplitudeControl ?
                            Math.Clamp((int)Math.Round(strength * 255), 1, 255) : VibrationEffect.DefaultAmplitude;
                        if (!vibrator.HasAmplitudeControl)
                            duration = Math.Max(1, (long)Math.Round(duration * strength));
                        using var vibration = VibrationEffect.CreateOneShot(duration, amplitude);
                        vibrator.Vibrate(vibration);
                    }
                    else
                        vibrator.Vibrate(Math.Max(1, (long)Math.Round(duration * strength)));
                }
                catch (Java.Lang.SecurityException e)
                {
                    Log.Write("debug", "Android impact feedback unavailable: " + e.Message);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                if (vibrator != null)
                {
                    try { vibrator.Cancel(); }
                    catch (Java.Lang.SecurityException) { }
                    vibrator.Dispose();
                }
            }
        }
    }
}
