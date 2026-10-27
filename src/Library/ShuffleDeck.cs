using System;
using System.Collections.Generic;

namespace PortableMusicPlayer.Library
{
    // Shuffle plays every track in the queue once, in a random order, before any track plays again. The order
    // is also the playback record: entries before the current track have played and entries after it are to come.
    internal static class ShuffleDeck
    {
        // Brings the order in line with the queue. Tracks that left the queue are dropped, new tracks are dealt
        // into the part still to come, and an order without the current track starts over from it.
        internal static void Reconcile(List<string> order, IList<string> queue, string current, Random random)
        {
            var inQueue = new HashSet<string>(queue, LibraryPaths.Comparer);
            var dealt = new HashSet<string>(LibraryPaths.Comparer);
            order.RemoveAll(path => !inQueue.Contains(path) || !dealt.Add(path));
            int at = IndexOf(order, current);
            if (at < 0)
            {
                order.Clear();
                dealt.Clear();
                if (!string.IsNullOrWhiteSpace(current) && inQueue.Contains(current))
                {
                    order.Add(current);
                    dealt.Add(current);
                }
                var rest = new List<string>();
                foreach (string path in queue)
                    if (dealt.Add(path)) rest.Add(path);
                Shuffle(rest, random);
                order.AddRange(rest);
                return;
            }
            foreach (string path in queue)
                if (dealt.Add(path)) order.Insert(random.Next(at + 1, order.Count + 1), path);
        }

        // The track after the current one. At the end of the order this is null, unless wrap deals a fresh
        // order that does not open with the track that just played.
        internal static string Next(List<string> order, string current, bool wrap, Random random)
        {
            if (order.Count == 0) return null;
            int at = IndexOf(order, current);
            if (at + 1 < order.Count) return order[at + 1];
            if (!wrap) return null;
            Shuffle(order, random);
            if (order.Count > 1 && LibraryPaths.Comparer.Equals(order[0], current))
                Swap(order, 0, random.Next(1, order.Count));
            return order[0];
        }

        // The track that played before the current one, or null at the start of the order.
        internal static string Previous(List<string> order, string current)
        {
            int at = IndexOf(order, current);
            return at > 0 ? order[at - 1] : null;
        }

        private static int IndexOf(List<string> order, string path) =>
            string.IsNullOrWhiteSpace(path) ? -1 : order.FindIndex(entry => LibraryPaths.Comparer.Equals(entry, path));

        private static void Shuffle(List<string> items, Random random)
        {
            for (int i = items.Count - 1; i > 0; i--) Swap(items, i, random.Next(i + 1));
        }

        private static void Swap(List<string> items, int a, int b)
        {
            string held = items[a];
            items[a] = items[b];
            items[b] = held;
        }
    }
}
