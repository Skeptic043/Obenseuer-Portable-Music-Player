namespace PortableMusicPlayer.Core
{
    internal static class ResizeGripLayout
    {
        // Rows are counted from the bottom, so the longest edge meets the lower-right corner.
        internal static int DotsInRow(int row) => row >= 0 && row < 3 ? 3 - row : 0;
    }
}
