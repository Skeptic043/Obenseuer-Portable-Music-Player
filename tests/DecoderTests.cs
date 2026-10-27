using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using PortableMusicPlayer.Audio;

namespace PortableMusicPlayer.Tests
{
    // Each fixture is two seconds of 44.1 kHz stereo, 440 Hz on the left and 660 Hz on the right, made with ffmpeg:
    // -filter_complex "sine=f=440:r=44100:d=2[l];sine=f=660:r=44100:d=2[r];[l][r]join=inputs=2:channel_layout=stereo"
    [TestSuite]
    public static class DecoderTests
    {
        private const int Rate = 44100;

        public static void Mp3DecodesAndSeeks() => Check(".mp3");
        public static void OggDecodesAndSeeks() => Check(".ogg");
        public static void FlacDecodesAndSeeks() => Check(".flac");
        public static void M4aDecodesAndSeeks() => Check(".m4a");
        public static void AacDecodesAndSeeks() => Check(".aac");

        private static void Check(string extension)
        {
            MediaFoundationSupport.Detect();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "tone" + extension);
            float[] whole;
            using (IPcmDecoder decoder = PcmDecoders.Open(path, CancellationToken.None))
            {
                Assert.Equal(2, decoder.Channels, extension + " channels");
                Assert.Equal(Rate, decoder.SampleRate, extension + " sample rate");
                Assert.Near(2 * Rate, decoder.Frames, 4096, extension + " frames");
                whole = Read(decoder, decoder.Frames);
                Assert.Equal(decoder.Frames, whole.Length / 2L, extension + " frames read to the end");
            }
            // The middle second keeps encoder padding and fades out of the pitch check.
            Assert.Near(440, Cycles(whole, 0, Rate / 2, Rate), 4, extension + " left channel pitch");
            Assert.Near(660, Cycles(whole, 1, Rate / 2, Rate), 4, extension + " right channel pitch");
            foreach (int frame in new[] { 30011, 61440 })
            {
                using (IPcmDecoder decoder = PcmDecoders.Open(path, CancellationToken.None))
                {
                    decoder.Seek(frame);
                    Assert.Equal((long)frame, decoder.Position, extension + " position after seek");
                    float[] part = Read(decoder, 1024);
                    double worst = 0d;
                    for (int i = 0; i < part.Length; i++) worst = Math.Max(worst, Math.Abs(part[i] - whole[frame * 2 + i]));
                    Assert.Near(0d, worst, 0.001d, extension + " samples after seeking to " + frame);
                }
            }
        }

        private static float[] Read(IPcmDecoder decoder, long frames)
        {
            var samples = new List<float>();
            var buffer = new float[4096];
            while (samples.Count < frames * 2)
            {
                int got = decoder.Read(buffer, 0, (int)Math.Min(buffer.Length, frames * 2 - samples.Count));
                if (got <= 0) break;
                for (int i = 0; i < got; i++) samples.Add(buffer[i]);
            }
            return samples.ToArray();
        }

        // Upward zero crossings of one channel, which for a pure tone is its frequency per second.
        private static int Cycles(float[] samples, int channel, int start, int frames)
        {
            int cycles = 0;
            for (int frame = start + 1; frame < start + frames; frame++)
                if (samples[(frame - 1) * 2 + channel] < 0f && samples[frame * 2 + channel] >= 0f) cycles++;
            return cycles;
        }
    }
}
