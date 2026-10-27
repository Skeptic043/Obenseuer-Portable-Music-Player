namespace PortableMusicPlayer.Core
{
    internal enum PlaybackOutput
    {
        // Power the player off. Its track and position are kept for the next power-on.
        Off,
        // Keep the track loaded and paused at its current sample.
        Hold,
        Play
    }

    internal struct PlaybackConditions
    {
        internal bool Powered { get; set; }
        internal bool Carried { get; set; }
        internal bool HeadphonesWorn { get; set; }
        internal bool HasCharge { get; set; }
        internal bool Paused { get; set; }
        // The game's pause menu, where its own music stops too.
        internal bool MenuOpen { get; set; }
        // Any of the game's time skips: sleeping, waiting, reading, working a shift, watching TV and the like.
        internal bool PassingTime { get; set; }
        internal bool Loading { get; set; }
        internal bool PlayWhilePassingTime { get; set; }
        internal bool PlayThroughLoading { get; set; }
    }

    internal struct PlaybackDecision
    {
        internal PlaybackOutput Output { get; }
        internal BatteryUse Battery { get; }

        internal PlaybackDecision(PlaybackOutput output, BatteryUse battery)
        {
            Output = output;
            Battery = battery;
        }
    }

    // The one rule for whether the active player makes sound.
    internal static class PlaybackGate
    {
        private static readonly PlaybackDecision Off = new PlaybackDecision(PlaybackOutput.Off, BatteryUse.Silent);
        private static readonly PlaybackDecision Hold = new PlaybackDecision(PlaybackOutput.Hold, BatteryUse.Silent);

        internal static PlaybackDecision Decide(PlaybackConditions c)
        {
            if (!c.Powered || !c.HasCharge) return Off;
            // The inventory is rebuilt while a save or level loads, so absence then is not a drop.
            if (!c.Carried && !c.Loading) return Off;
            if (c.Paused || c.MenuOpen) return Hold;
            // A load puts worn items back a moment late, so missing headphones then are not taken off.
            if (!c.HeadphonesWorn && !c.Loading) return Hold;
            if (c.Loading) return c.PlayThroughLoading ? new PlaybackDecision(PlaybackOutput.Play, BatteryUse.Playing) : Hold;
            if (c.PassingTime) return c.PlayWhilePassingTime ? new PlaybackDecision(PlaybackOutput.Play, BatteryUse.PlayingPassingTime) : Hold;
            return new PlaybackDecision(PlaybackOutput.Play, BatteryUse.Playing);
        }
    }
}
