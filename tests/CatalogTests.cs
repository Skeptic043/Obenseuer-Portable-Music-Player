using System.Linq;
using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class CatalogTests
    {
        public static void HeadphoneTiersGetWiderAndLouderWithPrice()
        {
            OutputProfile budget = OutputProfile.For(Device.BudgetEarbuds);
            OutputProfile work = OutputProfile.For(Device.WorkHeadphones);
            OutputProfile hifi = OutputProfile.For(Device.HiFiHeadphones);
            Assert.True(budget.HighPassHz > work.HighPassHz && work.HighPassHz > hifi.HighPassHz, "bass extends further on better headphones");
            Assert.True(budget.LowPassHz < work.LowPassHz && work.LowPassHz < hifi.LowPassHz, "treble extends further on better headphones");
            Assert.True(budget.MaximumVolume < work.MaximumVolume && work.MaximumVolume < hifi.MaximumVolume, "better headphones go louder");
        }

        public static void HeadphoneTiersHaveClearlySeparatedSoundProfiles()
        {
            OutputProfile budget = OutputProfile.For(Device.BudgetEarbuds);
            OutputProfile work = OutputProfile.For(Device.WorkHeadphones);
            OutputProfile hifi = OutputProfile.For(Device.HiFiHeadphones);
            Assert.True(budget.HighPassHz >= work.HighPassHz * 2f && work.HighPassHz >= hifi.HighPassHz * 8f,
                "bass cutoffs are too similar");
            Assert.True(work.LowPassHz >= budget.LowPassHz * 2f && hifi.LowPassHz >= work.LowPassHz * 2f,
                "treble cutoffs are too similar");
            Assert.True(work.MaximumVolume - budget.MaximumVolume >= 0.2f && hifi.MaximumVolume - work.MaximumVolume >= 0.2f,
                "volume ceilings are too similar");
        }

        public static void RechargeablePlayerDescriptionUsesTheAcceptedShortCopy()
        {
            Assert.Equal("A slim music player with a rechargeable battery. Needs headphones.",
                Catalog.Entry(Device.RechargeablePlayer).Description, "rechargeable player description");
        }

        public static void NoHeadphonesAmplifyPastFullScale()
        {
            foreach (Device device in Catalog.Headphones)
                Assert.True(OutputProfile.For(device).MaximumVolume <= 1f, device + " could clip");
        }

        public static void OnlyWorkHeadphonesMuffleTheWorld()
        {
            foreach (Device device in Catalog.Headphones)
                Assert.Equal(device == Device.WorkHeadphones, OutputProfile.For(device).MufflesWorld, device + " muffling");
        }

        public static void OnlyThePocketPlayerTakesBatteriesAndOnlyTheRechargeableCharges()
        {
            Assert.True(Catalog.Entry(Device.PocketPlayer).TakesAaBatteries, "the Pocket Music Player takes AA batteries");
            Assert.True(!Catalog.Entry(Device.PocketPlayer).Charges, "the Pocket Music Player is refused by chargers");
            Assert.True(Catalog.Entry(Device.RechargeablePlayer).Charges, "the Rechargeable Music Player charges");
            Assert.True(!Catalog.Entry(Device.RechargeablePlayer).TakesAaBatteries, "the Rechargeable Music Player has no battery door");
        }

        public static void EveryItemHasAUniqueKey()
        {
            Assert.Equal(5, Catalog.All.Count, "required item count");
            Assert.Equal(Catalog.All.Count, Catalog.All.Select(e => e.Key).Distinct().Count(), "distinct item keys");
        }
    }
}
