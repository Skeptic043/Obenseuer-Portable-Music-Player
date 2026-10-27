using System;
using System.IO;
using System.Threading;

namespace PortableMusicPlayer.Tests
{
    // Writes small 16-bit mono WAV files whose samples encode their own frame index.
    internal static class TestAudio
    {
        internal const int Rate = 22050;

        internal static string Ramp(int seconds)
        {
            string path = Path.Combine(Path.GetTempPath(), "pmp-test-" + Guid.NewGuid().ToString("N") + ".wav");
            int frames = Rate * seconds;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write("RIFF".ToCharArray()); writer.Write(36 + frames * 2);
                writer.Write("WAVE".ToCharArray()); writer.Write("fmt ".ToCharArray());
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write("data".ToCharArray()); writer.Write(frames * 2);
                for (int frame = 0; frame < frames; frame++) writer.Write(SampleAt(frame));
            }
            return path;
        }

        internal static short SampleAt(int frame) => (short)(frame % 30000 - 15000);
        internal static float FloatAt(int frame) => SampleAt(frame) / 32768f;

        // StreamedAudio never waits for its decoder thread to close the file.
        internal static void Delete(string path)
        {
            WaitUntil(() =>
            {
                try { File.Delete(path); return true; }
                catch (IOException) { return false; }
            }, "the decoder to release " + Path.GetFileName(path));
        }

        internal static void WaitUntil(Func<bool> condition, string what)
        {
            DateTime limit = DateTime.UtcNow.AddSeconds(10);
            while (!condition())
            {
                if (DateTime.UtcNow > limit) throw new Exception("timed out waiting for " + what);
                Thread.Sleep(5);
            }
        }
    }
}
