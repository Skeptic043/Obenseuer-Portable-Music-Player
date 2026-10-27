using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class AbsenceTests
    {
        public static void ABriefAbsenceIsNotALoss()
        {
            // The game refills hand and head slots a moment after a load, and a drag lifts the stack out.
            var absence = new AbsenceTimer(1.5f);
            absence.Observe(present: false, now: 10f);
            Assert.True(!absence.Lost(now: 11f), "a one-second absence counted as a loss");
            absence.Observe(present: true, now: 11f);
            absence.Observe(present: false, now: 12f);
            Assert.True(!absence.Lost(now: 13f), "the timer did not restart after the player came back");
        }

        public static void ALastingAbsenceIsALoss()
        {
            var absence = new AbsenceTimer(1.5f);
            absence.Observe(present: false, now: 10f);
            Assert.True(absence.Lost(now: 11.6f), "a lasting absence was not a loss");
        }

        public static void PresenceIsNeverALoss()
        {
            var absence = new AbsenceTimer(1.5f);
            absence.Observe(present: true, now: 10f);
            Assert.True(!absence.Lost(now: 100f), "a carried player counted as lost");
        }
    }
}
