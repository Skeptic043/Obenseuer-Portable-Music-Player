using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class PlaybackGateTests
    {
        private static PlaybackConditions Listening() => new PlaybackConditions
        {
            Powered = true, Carried = true, HeadphonesWorn = true, HasCharge = true
        };

        public static void PoweredCarriedPlayerWithHeadphonesPlays()
        {
            PlaybackDecision decision = PlaybackGate.Decide(Listening());
            Assert.Equal(PlaybackOutput.Play, decision.Output, "output");
            Assert.Equal(BatteryUse.Playing, decision.Battery, "battery use");
        }

        public static void NoHeadphonesMeansNoMusicButKeepsThePlace()
        {
            PlaybackConditions conditions = Listening();
            conditions.HeadphonesWorn = false;
            PlaybackDecision decision = PlaybackGate.Decide(conditions);
            Assert.Equal(PlaybackOutput.Hold, decision.Output, "output without headphones");
            Assert.Equal(BatteryUse.Silent, decision.Battery, "battery use without headphones");
        }

        public static void LeavingTheInventoryTurnsThePlayerOff()
        {
            PlaybackConditions conditions = Listening();
            conditions.Carried = false;
            Assert.Equal(PlaybackOutput.Off, PlaybackGate.Decide(conditions).Output, "output after the player left");
        }

        public static void APlayerMissingWhileLoadingIsNotLost()
        {
            // The inventory is rebuilt during a load, so a missing player then is not a drop.
            PlaybackConditions conditions = Listening();
            conditions.Carried = false;
            conditions.Loading = true;
            Assert.True(PlaybackGate.Decide(conditions).Output != PlaybackOutput.Off, "a load turned the player off");
        }

        public static void LoadingHoldsUnlessPlayingThroughLoadsIsOn()
        {
            PlaybackConditions conditions = Listening();
            conditions.Loading = true;
            Assert.Equal(PlaybackOutput.Hold, PlaybackGate.Decide(conditions).Output, "output while loading");
            conditions.PlayThroughLoading = true;
            Assert.Equal(PlaybackOutput.Play, PlaybackGate.Decide(conditions).Output, "output while loading with the setting on");
        }

        public static void PassingTimeHoldsUnlessPlayWhilePassingTimeIsOn()
        {
            PlaybackConditions conditions = Listening();
            conditions.PassingTime = true;
            PlaybackDecision held = PlaybackGate.Decide(conditions);
            Assert.Equal(PlaybackOutput.Hold, held.Output, "output while time passes");
            Assert.Equal(BatteryUse.Silent, held.Battery, "battery use while time passes");
            conditions.PlayWhilePassingTime = true;
            PlaybackDecision playing = PlaybackGate.Decide(conditions);
            Assert.Equal(PlaybackOutput.Play, playing.Output, "output while time passes with the setting on");
            Assert.Equal(BatteryUse.PlayingPassingTime, playing.Battery, "battery use while time passes with the setting on");
        }

        public static void AnEmptyBatteryTurnsThePlayerOff()
        {
            PlaybackConditions conditions = Listening();
            conditions.HasCharge = false;
            Assert.Equal(PlaybackOutput.Off, PlaybackGate.Decide(conditions).Output, "output with an empty battery");
        }

        public static void PausingHoldsThePlace()
        {
            PlaybackConditions conditions = Listening();
            conditions.Paused = true;
            PlaybackDecision decision = PlaybackGate.Decide(conditions);
            Assert.Equal(PlaybackOutput.Hold, decision.Output, "output while paused");
            Assert.Equal(BatteryUse.Silent, decision.Battery, "battery use while paused");
        }

        public static void HeadphonesStillBeingRestoredByALoadDoNotPauseTheSong()
        {
            // The game puts worn items back a moment after a load.
            PlaybackConditions conditions = Listening();
            conditions.HeadphonesWorn = false;
            conditions.Loading = true;
            conditions.PlayThroughLoading = true;
            Assert.Equal(PlaybackOutput.Play, PlaybackGate.Decide(conditions).Output, "output while a load restores the head slot");
        }

        public static void ThePauseMenuHoldsTheSong()
        {
            PlaybackConditions conditions = Listening();
            conditions.MenuOpen = true;
            PlaybackDecision decision = PlaybackGate.Decide(conditions);
            Assert.Equal(PlaybackOutput.Hold, decision.Output, "output in the pause menu");
            Assert.Equal(BatteryUse.Silent, decision.Battery, "battery use in the pause menu");
        }

        public static void AnUnpoweredPlayerIsOff()
        {
            PlaybackConditions conditions = Listening();
            conditions.Powered = false;
            Assert.Equal(PlaybackOutput.Off, PlaybackGate.Decide(conditions).Output, "output when powered off");
        }
    }
}
