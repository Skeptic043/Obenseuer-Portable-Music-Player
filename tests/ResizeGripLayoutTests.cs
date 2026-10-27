using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class ResizeGripLayoutTests
    {
        public static void TheRightAngleSitsInTheLowerRightCorner()
        {
            Assert.Equal(3, ResizeGripLayout.DotsInRow(0), "bottom row");
            Assert.Equal(2, ResizeGripLayout.DotsInRow(1), "middle row");
            Assert.Equal(1, ResizeGripLayout.DotsInRow(2), "top row");
            Assert.Equal(0, ResizeGripLayout.DotsInRow(3), "outside the grip");
        }
    }
}
