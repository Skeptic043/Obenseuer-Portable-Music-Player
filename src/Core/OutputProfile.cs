using System;

namespace PortableMusicPlayer.Core
{
    // How a pair of headphones shapes the music: a clean band limit and an output ceiling,
    // never noise or distortion.
    internal struct OutputProfile
    {
        internal float HighPassHz { get; }
        internal float LowPassHz { get; }
        internal float MaximumVolume { get; }
        internal bool MufflesWorld { get; }

        private OutputProfile(float highPassHz, float lowPassHz, float maximumVolume, bool mufflesWorld)
        {
            HighPassHz = highPassHz;
            LowPassHz = lowPassHz;
            MaximumVolume = maximumVolume;
            MufflesWorld = mufflesWorld;
        }

        internal static OutputProfile For(Device device)
        {
            switch (device)
            {
                // The tiers are deliberately broad enough to hear without adding noise or distortion.
                case Device.BudgetEarbuds: return new OutputProfile(300f, 5000f, 0.55f, false);
                case Device.WorkHeadphones: return new OutputProfile(120f, 11000f, 0.78f, true);
                case Device.HiFiHeadphones: return new OutputProfile(10f, 22000f, 1f, false);
                default: throw new ArgumentOutOfRangeException(nameof(device), device + " is not a pair of headphones");
            }
        }
    }
}
