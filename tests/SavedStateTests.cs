using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class SavedStateTests
    {
        public static void DamagedValuesLoadAsSafeDefaults()
        {
            var saved = new SavedState
            {
                Device = "toaster", Powered = true, PositionSeconds = double.NaN, Volume = 7f,
                TrackPath = null, ShuffleOrder = null, Repeat = (RepeatMode)99
            };
            saved.Normalize();
            Assert.True(saved.ActiveDevice == null, "an unknown device was accepted");
            Assert.True(!saved.Powered, "a player with no device stayed powered");
            Assert.Equal(0d, saved.PositionSeconds, "position");
            Assert.Equal(1f, saved.Volume, "volume");
            Assert.Equal("", saved.TrackPath, "track path");
            Assert.True(saved.ShuffleOrder != null, "a missing shuffle order was not replaced");
            Assert.Equal(RepeatMode.Off, saved.Repeat, "invalid repeat mode");
        }

        public static void AKnownDeviceRoundTrips()
        {
            var saved = new SavedState();
            saved.ActiveDevice = Device.RechargeablePlayer;
            saved.Normalize();
            Assert.Equal(Device.RechargeablePlayer, saved.ActiveDevice.Value, "device after a round trip");
        }
    }
}
