using System.Collections.Generic;
using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class BindingTests
    {
        private sealed class Charge { }

        private static CarriedPlayer<Charge> Carried(Device device, string identity, Charge charge) =>
            new CarriedPlayer<Charge>(device, identity, charge);

        public static void KeepsTheBoundPlayerWhileItIsCarried()
        {
            Charge bound = new Charge(), other = new Charge();
            var carried = new List<CarriedPlayer<Charge>> { Carried(Device.PocketPlayer, "other", other), Carried(Device.PocketPlayer, "active", bound) };
            Assert.True(ReferenceEquals(bound, PlayerBinding.Choose(Device.PocketPlayer, "active", bound, carried).Charge), "switched to another player");
        }

        public static void AReferenceMatchWinsEvenWhenADuplicateIdentityWasScannedFirst()
        {
            Charge bound = new Charge(), duplicate = new Charge();
            var carried = new List<CarriedPlayer<Charge>>
            {
                Carried(Device.PocketPlayer, "active", duplicate),
                Carried(Device.PocketPlayer, "renewed", bound)
            };

            CarriedPlayer<Charge> chosen = PlayerBinding.Choose(Device.PocketPlayer, "active", bound, carried);
            Assert.True(chosen != null && ReferenceEquals(bound, chosen.Charge),
                "a duplicate identity took ownership before the bound charge was checked");
            Assert.Equal("renewed", chosen.Identity, "renewed identity of the bound player");
        }

        public static void FollowsTheSameKindWhenTheGameReplacedTheItemData()
        {
            // Moving a stack can give it a fresh copy of its data.
            Charge stale = new Charge(), replacement = new Charge();
            var carried = new List<CarriedPlayer<Charge>> { Carried(Device.RechargeablePlayer, "other", new Charge()), Carried(Device.PocketPlayer, "active", replacement) };
            CarriedPlayer<Charge> chosen = PlayerBinding.Choose(Device.PocketPlayer, "active", stale, carried);
            Assert.True(chosen != null && ReferenceEquals(replacement, chosen.Charge), "did not follow the moved Pocket Music Player");
        }

        public static void NothingIsChosenWhenNoPlayerOfThatKindIsCarried()
        {
            var carried = new List<CarriedPlayer<Charge>> { Carried(Device.RechargeablePlayer, "active", new Charge()) };
            Assert.True(PlayerBinding.Choose(Device.PocketPlayer, "active", new Charge(), carried) == null, "chose a different kind of player");
        }

        public static void DroppingThePlayerDoesNotHandPlaybackToAnotherOfTheSameKind()
        {
            var carried = new List<CarriedPlayer<Charge>> { Carried(Device.PocketPlayer, "spare", new Charge()) };
            Assert.True(PlayerBinding.Choose(Device.PocketPlayer, "active", new Charge(), carried) == null, "playback moved to the spare player");
        }

        public static void DroppingOneOfTwoEquallyChargedPlayersDoesNotHandPlaybackToTheOther()
        {
            // Full players are common. Equal charge alone cannot establish that fresh metadata belongs
            // to the selected physical player.
            var carried = new List<CarriedPlayer<Charge>> { Carried(Device.PocketPlayer, "spare", new Charge()) };
            Assert.True(PlayerBinding.Choose(Device.PocketPlayer, "active", new Charge(), carried) == null,
                "playback moved to a different full Pocket Music Player");
        }

        public static void AfterALoadTheSavedIdentityIsAccepted()
        {
            var carried = new List<CarriedPlayer<Charge>> { Carried(Device.PocketPlayer, "active", new Charge()) };
            Assert.True(PlayerBinding.Choose(Device.PocketPlayer, "active", null, carried) != null, "the saved player was not bound after a load");
        }

        public static void WithTwoOfTheSameKindItFollowsTheOneWhoseIdentityMatches()
        {
            // The active player was moved and got fresh data while a second one is carried.
            Charge spare = new Charge(), moved = new Charge();
            var carried = new List<CarriedPlayer<Charge>> { Carried(Device.PocketPlayer, "spare", spare), Carried(Device.PocketPlayer, "active", moved) };
            CarriedPlayer<Charge> chosen = PlayerBinding.Choose(Device.PocketPlayer, "active", new Charge(), carried);
            Assert.True(chosen != null && ReferenceEquals(moved, chosen.Charge), "followed the spare player instead of the moved one");
        }
    }
}
