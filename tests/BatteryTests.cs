using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class BatteryTests
    {
        private const int Hour = 3600;

        public static void PocketPlayerLastsTwelveInGameHoursOfPlayback()
        {
            var battery = MeterFor(Device.PocketPlayer);
            for (int hour = 0; hour < 11; hour++) battery.Spend(Hour, BatteryUse.Playing, 0.5d);
            Assert.True(battery.Remaining > 0f, "a full charge ran out before 12 hours of playback");
            battery.Spend(Hour, BatteryUse.Playing, 0.5d);
            Assert.Near(0d, battery.Remaining, 0.001d, "charge after 12 hours of playback");
        }

        public static void PlayersHaveDistinctPlaybackDurations()
        {
            Assert.Near(12d, Catalog.Entry(Device.PocketPlayer).PlaybackHours, 0d, "Pocket player playback hours");
            Assert.Near(24d, Catalog.Entry(Device.RechargeablePlayer).PlaybackHours, 0d, "rechargeable playback hours");
        }

        public static void RechargeablePlayerLastsTwentyFourInGameHoursOfPlayback()
        {
            var battery = MeterFor(Device.RechargeablePlayer);
            battery.Spend(12 * Hour, BatteryUse.Playing, 0.5d);
            Assert.Near(50d, battery.Remaining, 0.001d, "charge after 12 hours of playback");
            battery.Spend(12 * Hour, BatteryUse.Playing, 0.5d);
            Assert.Near(0d, battery.Remaining, 0.001d, "charge after 24 hours of playback");
        }

        public static void PassingTimeDrainsTheChosenShareOfEachPlayersRate()
        {
            BatteryMeter pocket = MeterFor(Device.PocketPlayer);
            BatteryMeter rechargeable = MeterFor(Device.RechargeablePlayer);
            BatteryMeter free = MeterFor(Device.PocketPlayer);
            pocket.Spend(12 * Hour, BatteryUse.PlayingPassingTime, 0.5d);
            rechargeable.Spend(12 * Hour, BatteryUse.PlayingPassingTime, 0.25d);
            free.Spend(12 * Hour, BatteryUse.PlayingPassingTime, 0d);
            Assert.Near(50d, pocket.Remaining, 0.001d, "Pocket charge after 12 hours at half drain");
            Assert.Near(87.5d, rechargeable.Remaining, 0.001d, "rechargeable charge after 12 hours at quarter drain");
            Assert.Near(100d, free.Remaining, 0.001d, "charge after 12 hours with drain off");
        }

        public static void SilenceDoesNotDrain()
        {
            var battery = new BatteryMeter(50f, 100f, 24d * Hour);
            battery.Spend(8 * Hour, BatteryUse.Silent, 0.5d);
            Assert.Near(50d, battery.Remaining, 0.0001d, "charge after silent time");
        }

        public static void UnflushedDrainNeverExceedsOneChargePoint()
        {
            // A dropped or sold player keeps whatever was last written to its item, so the
            // drain held back from the item must stay small.
            var battery = new BatteryMeter(100f, 100f, 24d * Hour);
            for (int second = 0; second < 2 * Hour; second++)
            {
                battery.Spend(1, BatteryUse.Playing, 0.5d);
                if (battery.ShouldWrite) battery.Written();
                Assert.True(battery.Stored - battery.Remaining <= 1f, "unwritten drain exceeded one charge point");
            }
        }

        public static void AFreshBatteryStartsFull()
        {
            // Drain held back from the old battery went out with it.
            var battery = new BatteryMeter(10f, 100f, 24d * Hour);
            battery.Spend(Hour / 2, BatteryUse.Playing, 0.5d);
            battery.Refilled(100f);
            Assert.Near(100d, battery.Remaining, 0.001d, "charge after a battery swap");
        }

        public static void FreshItemDataKeepsTheUnwrittenDrain()
        {
            // Moving a stack can hand it a copy of its data holding the last written charge.
            var battery = new BatteryMeter(60f, 100f, 24d * Hour);
            battery.Spend(Hour / 2, BatteryUse.Playing, 0.5d);
            battery.Rebind(60f);
            Assert.Near(60d - 100d / 48d, battery.Remaining, 0.001d, "charge after the data was replaced");
        }

        private static BatteryMeter MeterFor(Device device) => new BatteryMeter(
            100f, 100f, Catalog.Entry(device).PlaybackHours * Hour);
    }
}
