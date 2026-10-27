using System;
using System.Collections.Generic;

namespace PortableMusicPlayer.Core
{
    internal sealed class CarriedPlayer<TCharge> where TCharge : class
    {
        internal Device Device { get; }
        internal string Identity { get; }
        internal TCharge Charge { get; }

        internal CarriedPlayer(Device device, string identity, TCharge charge)
        {
            Device = device;
            Identity = identity;
            Charge = charge;
        }
    }

    // Selects which carried stack drives playback. A live charge reference takes priority so duplicate repair
    // cannot steal the active player; the durable metadata identity follows it across cloned item data.
    internal static class PlayerBinding
    {
        internal static CarriedPlayer<TCharge> Choose<TCharge>(Device device, string identity, TCharge bound,
            IEnumerable<CarriedPlayer<TCharge>> carried) where TCharge : class
        {
            if (bound != null)
                foreach (CarriedPlayer<TCharge> player in carried)
                    if (player.Device == device && ReferenceEquals(player.Charge, bound)) return player;

            if (string.IsNullOrEmpty(identity)) return null;
            foreach (CarriedPlayer<TCharge> player in carried)
            {
                if (player.Device != device) continue;
                if (string.Equals(player.Identity, identity, StringComparison.OrdinalIgnoreCase))
                    return player;
            }
            return null;
        }
    }
}
