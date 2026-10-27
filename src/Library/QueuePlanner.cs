namespace PortableMusicPlayer.Library
{
    // Track order with shuffle off. Shuffle order lives in ShuffleDeck.
    internal static class QueuePlanner
    {
        // The following track, wrapping to the first. With wrap off the last track has none.
        internal static int Next(int count, int current, bool wrap)
        {
            if (count <= 0) return -1;
            if (current < 0 || current >= count) return 0;
            if (current + 1 < count) return current + 1;
            return wrap ? 0 : -1;
        }

        internal static int Previous(int count, int current)
        {
            if (count <= 0) return -1;
            if (current <= 0 || current >= count) return count - 1;
            return current - 1;
        }
    }
}
