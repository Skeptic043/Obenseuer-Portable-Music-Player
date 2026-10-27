using System;
using System.IO;
using PortableMusicPlayer.Audio;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class StreamTests
    {
        public static void AResumedTrackPlaysFromItsSavedPositionAfterUnityRewindsTheClip()
        {
            string path = TestAudio.Ramp(6);
            try
            {
                using (StreamedAudio stream = StreamedAudio.Start(path, 2.0))
                {
                    TestAudio.WaitUntil(() => stream.IsOpen || stream.Fault != null, "the stream to open");
                    Assert.True(stream.Fault == null, "stream failed: " + stream.Fault?.Message);
                    int start = 2 * TestAudio.Rate;
                    TestAudio.WaitUntil(() => stream.Ready(start), "the saved position to decode");
                    StreamedAudio.Reader reader = stream.CreateReader(start);
                    // Unity moves a streamed clip to its first sample whenever it starts playing.
                    reader.SetPosition(0);
                    var buffer = new float[64];
                    reader.Read(buffer);
                    Assert.Near(TestAudio.FloatAt(start), buffer[0], 0.0001d, "first sample after Play");
                    Assert.Equal(stream.Frames - start, reader.Frames, "frames the clip covers");
                    Assert.Equal(start + 64, reader.TrackFrame, "track position after one read");
                }
            }
            finally { TestAudio.Delete(path); }
        }

        public static void ALoopingTrackDecodesItsOpeningBeforeItWraps()
        {
            string path = TestAudio.Ramp(30);
            try
            {
                // Opened mid-track, so nothing near the start is decoded unless the look-ahead wraps.
                using (StreamedAudio stream = StreamedAudio.Start(path, 12d))
                {
                    TestAudio.WaitUntil(() => stream.IsOpen || stream.Fault != null, "the stream to open");
                    Assert.True(stream.Fault == null, "stream failed: " + stream.Fault?.Message);
                    stream.Loop = true;
                    // Twenty-five seconds in, the ten-second look-ahead reaches past the end of the track.
                    stream.Request(25 * TestAudio.Rate);
                    TestAudio.WaitUntil(() => stream.Ready(0), "the opening to decode ahead of the wrap");
                    StreamedAudio.Reader reader = stream.CreateReader(0);
                    var buffer = new float[64];
                    reader.Read(buffer);
                    Assert.Near(TestAudio.FloatAt(0), buffer[0], 0.0001d, "first sample after the wrap");
                }
            }
            finally { TestAudio.Delete(path); }
        }

        public static void ADecoderFailureAfterAudioMarksWherePlaybackMustEnd()
        {
            int failAt = 3 * TestAudio.Rate;
            using (StreamedAudio stream = StreamedAudio.Start(token => new FailingDecoder(10 * TestAudio.Rate, failAt), 0d))
            {
                TestAudio.WaitUntil(() => stream.IsOpen || stream.Fault != null, "the stream to open");
                stream.Request(0);
                TestAudio.WaitUntil(() => stream.DamageFrame != int.MaxValue || stream.Fault != null, "the decoder failure");
                Assert.True(stream.Fault == null, "a failure after audio played faulted the whole track");
                Assert.Equal(failAt, stream.DamageFrame, "first damaged frame");
                Assert.True(stream.Damage != null, "the failure reason is kept");
            }
        }

        // Mono silence that throws once it reaches a set frame.
        private sealed class FailingDecoder : IPcmDecoder
        {
            private readonly int failAt;
            internal FailingDecoder(long frames, int failAt) { Frames = frames; this.failAt = failAt; }
            public int Channels => 1;
            public int SampleRate => TestAudio.Rate;
            public long Frames { get; }
            public long Position { get; private set; }
            public void Seek(long frame) => Position = frame;
            public int Read(float[] buffer, int offset, int count)
            {
                if (Position >= failAt) throw new IOException("damaged frame");
                int got = (int)Math.Min(count, failAt - Position);
                Array.Clear(buffer, offset, got);
                Position += got;
                return got;
            }
            public void Dispose() { }
        }
    }
}
