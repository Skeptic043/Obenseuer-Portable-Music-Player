using System;
using System.Collections.Generic;
using OS.Items;
using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Playback
{
    // The carried player that is switched on: its item's charge data, the battery meter in front of it,
    // and whether it is still in the inventory. These always change together.
    internal sealed class ActivePlayer
    {
        private const float AbsenceGraceSeconds = 1.5f;
        private const double SecondsPerHour = 60d * 60d;
        private readonly AbsenceTimer absence = new AbsenceTimer(AbsenceGraceSeconds);
        private string identity;
        private ItemAmmodata charge;
        private BatteryMeter battery;

        // Still in the inventory, or not missing for long.
        internal bool Present { get; private set; }
        internal bool HasCharge => battery == null || battery.Remaining > 0f;
        internal float ChargePercent => battery == null || charge.maxAmmo <= 0f ? 0f : 100f * battery.Remaining / charge.maxAmmo;

        internal bool Is(CarriedPlayer<ItemAmmodata> player) => player != null &&
            (ReferenceEquals(player.Charge, charge) || !string.IsNullOrEmpty(identity) &&
             string.Equals(player.Identity, identity, StringComparison.OrdinalIgnoreCase));

        internal void Bind(CarriedPlayer<ItemAmmodata> player)
        {
            identity = player.Identity;
            charge = player.Charge;
            battery = NewBattery(player.Device, charge);
            Present = true;
            absence.Reset();
        }

        // After a load the saved player is unbound until the rebuilt inventory shows it again.
        internal void Expect(string expectedIdentity)
        {
            identity = expectedIdentity;
            charge = null;
            battery = null;
            Present = true;
            absence.Reset();
        }

        // Follows the player when the game hands its stack fresh data, keeping the unwritten drain.
        internal string Follow(Device device, string expectedIdentity, IEnumerable<CarriedPlayer<ItemAmmodata>> carried, bool loading, float now)
        {
            if (!string.IsNullOrEmpty(expectedIdentity)) identity = expectedIdentity;
            CarriedPlayer<ItemAmmodata> chosen = PlayerBinding.Choose(device, identity, charge, carried);
            absence.Observe(chosen != null, now);
            if (loading) absence.Reset();
            Present = !absence.Lost(now);
            if (chosen == null) return identity;
            identity = chosen.Identity;
            if (ReferenceEquals(chosen.Charge, charge)) return identity;
            charge = chosen.Charge;
            if (battery == null) battery = NewBattery(device, charge);
            else battery.Rebind(charge.ammo);
            return identity;
        }

        private static BatteryMeter NewBattery(Device device, ItemAmmodata charge) =>
            new BatteryMeter(charge.ammo, charge.maxAmmo, Catalog.Entry(device).PlaybackHours * SecondsPerHour);

        internal void Spend(int gameSeconds, BatteryUse use, double passingTimeRate)
        {
            if (battery == null) return;
            // A charger or a battery swap changed the item.
            if (Math.Abs(charge.ammo - battery.Stored) > 0.0001f) battery.Refilled(charge.ammo);
            battery.Spend(gameSeconds, use, passingTimeRate);
            if (battery.ShouldWrite) Write();
        }

        internal void Write()
        {
            if (battery == null) return;
            charge.ammo = battery.Remaining;
            battery.Written();
        }

        internal void Refresh(CarriedPlayer<ItemAmmodata> player)
        {
            if (battery != null && Is(player)) battery.Refilled(player.Charge.ammo);
        }

        internal void Release()
        {
            Write();
            identity = null;
            charge = null;
            battery = null;
            Present = false;
        }
    }
}
