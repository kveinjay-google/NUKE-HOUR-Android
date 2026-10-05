using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenRA;
using OpenRA.Platforms.Default;
using OpenRA.Support;

var engine = new Sdl2SoundEngine();
var source = engine.AddSoundSourceFromMemory(new byte[1024], 2, 16, 48000);
using var start = new ManualResetEventSlim();
var jobs = new Task[6];
for (var worker = 0; worker < jobs.Length; worker++)
{
    var role = worker;
    jobs[worker] = Task.Run(() => {
        start.Wait();
        for (var i = 0; i < 10000; i++)
        {
            if (role < 3)
            {
                var sound = engine.Play2D(source, true, true, WPos.Zero, 1, false);
                if (i % 3 == 0) engine.StopSound(sound);
            }
            else if (role == 3) engine.SetSoundVolume(.5f, null, null);
            else if (role == 4) engine.SetAllSoundsPaused(i % 2 == 0);
            else engine.StopAllSounds();
        }
    });
}
start.Set();
if (!Task.WaitAll(jobs, 30000)) throw new Exception("Audio operations deadlocked.");
engine.StopAllSounds();
var mixer = (PcmAudioMixer)typeof(Sdl2SoundEngine).GetField("mixer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(engine);
if (mixer.ActivePlaybackCount != 0) throw new Exception("StopAll missed a playback.");
// Exercise factory registration and teardown together, without slow device IO.
var stream = engine.Play2DStream(() => new MemoryStream(new byte[1024]), 2, 16, 48000, true, true, WPos.Zero, 1);
engine.Dispose();
if (!stream.Complete || mixer.ActivePlaybackCount != 0) throw new Exception("Dispose retained music.");
try { engine.Play2D(source, true, true, WPos.Zero, 1, false); throw new Exception("Playback accepted after dispose."); }
catch (ObjectDisposedException) { }
try { engine.Play2DStream(() => new MemoryStream(new byte[1024]), 2, 16, 48000, true, true, WPos.Zero, 1); throw new Exception("Streaming accepted after dispose."); }
catch (ObjectDisposedException) { }
engine.Dispose();
Console.WriteLine("PASS Android audio: 60000 concurrent operations, no enumeration failures/deadlock/missed stops; streaming teardown and rejection after dispose.");

Log.Dispose();

namespace SDL2
{
    // Only native device IO is replaced; engine and shared mixer run unchanged.
    static class SDL
    {
        public const uint SDL_INIT_AUDIO = 16;
        public const ushort AUDIO_F32SYS = 0x8120;
        public struct SDL_AudioSpec { public int freq; public ushort format; public byte channels; public ushort samples; public object callback; }
        public static uint SDL_WasInit(uint flags) => flags;
        public static int SDL_InitSubSystem(uint flags) => 0;
        public static string SDL_GetError() => "test device";
        public static uint SDL_OpenAudioDevice(IntPtr name, int capture, ref SDL_AudioSpec desired, out SDL_AudioSpec obtained, int changes) { obtained = desired; return 1; }
        public static void SDL_PauseAudioDevice(uint device, int paused) { }
        public static uint SDL_GetQueuedAudioSize(uint device) => uint.MaxValue;
        public static int SDL_QueueAudio(uint device, IntPtr buffer, uint bytes) => 0;
        public static void SDL_CloseAudioDevice(uint device) { }
    }
}
