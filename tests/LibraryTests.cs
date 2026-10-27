using System;
using System.Collections.Generic;
using System.Linq;
using PortableMusicPlayer.Library;

namespace PortableMusicPlayer.Tests
{
    // Carried over from Obenseuer Radio with the queue and path code they protect.
    [TestSuite]
    public static class LibraryTests
    {
        public static void SequentialQueueWrapsOnlyWhenAsked()
        {
            Assert.Equal(0, QueuePlanner.Next(3, -1, false), "first track");
            Assert.Equal(2, QueuePlanner.Next(3, 1, false), "following track");
            Assert.Equal(-1, QueuePlanner.Next(3, 2, false), "the last track wrapped without repeat");
            Assert.Equal(0, QueuePlanner.Next(3, 2, true), "the last track did not wrap");
            Assert.Equal(2, QueuePlanner.Previous(3, 0), "previous wrap");
        }

        public static void ShufflePlaysEveryTrackOnceBeforeAnyRepeats()
        {
            for (int seed = 0; seed < 25; seed++)
            {
                var random = new Random(seed);
                List<string> queue = Tracks(12);
                var order = new List<string>();
                string current = queue[5];
                var heard = new List<string> { current };
                ShuffleDeck.Reconcile(order, queue, current, random);
                for (string next; (next = ShuffleDeck.Next(order, current, false, random)) != null; current = next)
                {
                    ShuffleDeck.Reconcile(order, queue, next, random);
                    heard.Add(next);
                }
                Assert.Equal(queue.Count, heard.Count, "tracks heard before the pass ended");
                Assert.Equal(queue.Count, new HashSet<string>(heard).Count, "a track repeated within one pass");

                string first = ShuffleDeck.Next(order, current, true, random);
                Assert.True(first != null && first != current, "a new pass opened with the track that just played");
            }
        }

        public static void ShufflePreviousRetracesThePassAndPickedTracksStartANewOne()
        {
            var random = new Random(3);
            List<string> queue = Tracks(6);
            var order = new List<string>();
            ShuffleDeck.Reconcile(order, queue, queue[2], random);
            Assert.Equal(queue[2], order[0], "a pass did not start from the picked track");
            string second = ShuffleDeck.Next(order, queue[2], false, random);
            string third = ShuffleDeck.Next(order, second, false, random);
            Assert.Equal(second, ShuffleDeck.Previous(order, third), "previous did not retrace the pass");
            Assert.Equal(null, ShuffleDeck.Previous(order, queue[2]), "the start of a pass has a previous track");
        }

        public static void ShuffleKeepsItsPassWhenTheLibraryChanges()
        {
            var random = new Random(7);
            List<string> queue = Tracks(5);
            var order = new List<string>();
            string current = queue[0];
            ShuffleDeck.Reconcile(order, queue, current, random);
            current = ShuffleDeck.Next(order, current, false, random);
            List<string> played = order.GetRange(0, 2);
            string removed = order[4];

            queue.Remove(removed);
            queue.Add("added.mp3");
            ShuffleDeck.Reconcile(order, queue, current, random);
            Assert.True(!order.Contains(removed), "a removed track stayed in the pass");
            Assert.True(order.IndexOf("added.mp3") > 1, "a new track was dealt into the part already played");
            Assert.Equal(string.Join("|", played), string.Join("|", order.GetRange(0, 2)), "played tracks were reordered");

            // A damaged save can hold a track twice; the pass still plays it once.
            order.Add(order[0]);
            ShuffleDeck.Reconcile(order, queue, current, random);
            Assert.Equal(queue.Count, order.Distinct().Count(), "the pass lost a track");
            Assert.Equal(order.Count, order.Distinct().Count(), "a duplicated track stayed in the pass");
        }

        private static List<string> Tracks(int count)
        {
            var tracks = new List<string>();
            for (int i = 0; i < count; i++) tracks.Add("track" + i + ".mp3");
            return tracks;
        }

        public static void OnlyLocalDrivePathsArePlayable()
        {
            Assert.Equal("C:\\Music\\track.mp3", PathRules.NormalizeLocalDrivePath("C:\\Music\\track.mp3"), "local drive path");
            foreach (string path in new[] { "https://example.com/a.mp3", "\\\\server\\music\\a.mp3", "relative.mp3" })
            {
                bool refused = false;
                try { PathRules.NormalizeLocalDrivePath(path); }
                catch (ArgumentException) { refused = true; }
                Assert.True(refused, path + " was accepted");
            }
        }
    }
}
