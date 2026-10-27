using System;

namespace PortableMusicPlayer.Core
{
    internal enum BatteryUse { Silent, Playing, PlayingPassingTime }

    // Tracks a player's charge between writes to its item. The item stays the authority: drain is
    // held here and written back once it reaches one charge point, so the item (and anything that
    // watches it, such as an open crafting list) changes rarely, and a player that leaves the
    // inventory unexpectedly loses at most that point.
    internal sealed class BatteryMeter
    {
        private const double WriteThreshold = 1d;
        private readonly float maximum;
        private readonly double fullChargeSeconds;
        private double pending;

        internal BatteryMeter(float stored, float maximum, double fullChargeSeconds)
        {
            this.maximum = maximum;
            this.fullChargeSeconds = fullChargeSeconds;
            Stored = stored;
        }

        // The charge last read from or written to the item.
        internal float Stored { get; private set; }
        internal float Remaining => (float)Math.Max(0d, Math.Min(maximum, Stored) - pending);
        internal bool ShouldWrite => pending >= WriteThreshold || (pending > 0d && Remaining <= 0f);

        // passingTimeRate is the share of the normal drain while time skips ahead, from 0 to 1.
        internal void Spend(int gameSeconds, BatteryUse use, double passingTimeRate)
        {
            if (gameSeconds <= 0 || maximum <= 0f || fullChargeSeconds <= 0d || use == BatteryUse.Silent) return;
            double rate = use == BatteryUse.PlayingPassingTime ? passingTimeRate : 1d;
            pending += maximum * gameSeconds * rate / fullChargeSeconds;
        }

        // Call after Remaining has been written to the item.
        internal void Written()
        {
            Stored = Remaining;
            pending = 0d;
        }

        // The game changed the item's charge, by charging it or swapping its battery. Drain held back from
        // the old charge went with it.
        internal void Refilled(float itemCharge)
        {
            Stored = itemCharge;
            pending = 0d;
        }

        // The game handed the player fresh item data holding the last written charge.
        internal void Rebind(float itemCharge) => Stored = itemCharge;
    }
}
