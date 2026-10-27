using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OS.Items;
using PortableMusicPlayer.Core;
using PortableMusicPlayer.Items;
using ObenseuerCore;

namespace PortableMusicPlayer.Playback
{
    // The music players in the player's own slots: the grid, the hands and head, the worn backpack,
    // and a stack being dragged out of one of those.
    internal sealed class CarriedPlayers
    {
        private static readonly FieldInfo GlobalDrag = typeof(ItemData).GetField("globaldrag", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo LastSlot = typeof(ItemData).GetField("lastSlotController", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<CarriedPlayer<ItemAmmodata>> players = new List<CarriedPlayer<ItemAmmodata>>();
        private readonly CoreLog log;

        internal CarriedPlayers(CoreLog log) { this.log = log; }

        internal IReadOnlyList<CarriedPlayer<ItemAmmodata>> Players => players;

        internal void Scan()
        {
            players.Clear();
            Inventory inventory = Inventory.instance;
            if (inventory == null) return;
            foreach (SlotController slot in PlayerSlots(inventory)) Add(slot?.itemStack);
            ItemData dragging = GlobalDrag?.GetValue(null) as ItemData;
            if (dragging != null && (IsPlayerSlot(inventory, dragging.slotController) || IsPlayerSlot(inventory, LastSlot?.GetValue(dragging) as SlotController)))
                Add(dragging.stack);
        }

        internal bool TryFind(ItemData itemData, out CarriedPlayer<ItemAmmodata> player)
        {
            Scan();
            ItemAmmodata charge = Charge(itemData?.stack);
            string identity = Identity(itemData?.stack, false);
            player = charge == null ? null : players.FirstOrDefault(p => ReferenceEquals(p.Charge, charge) ||
                !string.IsNullOrEmpty(identity) && string.Equals(p.Identity, identity, StringComparison.OrdinalIgnoreCase));
            return player != null;
        }

        // The carried player to switch on from the hotkey: the kind used last, otherwise the first found.
        internal CarriedPlayer<ItemAmmodata> Preferred(Device? device)
        {
            Scan();
            return players.FirstOrDefault(p => p.Device == device) ?? players.FirstOrDefault();
        }

        private void Add(ItemStack stack)
        {
            if (stack?.itemReference == null || !ItemSetup.TryDevice(stack.itemReference.ID, out Device device)) return;
            if (!Catalog.Entry(device).IsPlayer) return;
            ItemAmmodata charge = Charge(stack);
            string identity = Identity(stack, true);
            if (charge == null || string.IsNullOrEmpty(identity)) return;
            CarriedPlayer<ItemAmmodata> duplicate = players.FirstOrDefault(player =>
                string.Equals(player.Identity, identity, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                if (ReferenceEquals(duplicate.Charge, charge)) return;
                if (!PortablePlayerIdentity.Renew(stack.Meta, out identity)) return;
                log.Warn("Repaired a duplicated portable music player identity.");
            }
            players.Add(new CarriedPlayer<ItemAmmodata>(device, identity, charge));
        }

        private static ItemAmmodata Charge(ItemStack stack) => stack?.Meta?.OfType<ItemAmmodata>().FirstOrDefault();

        private static string Identity(ItemStack stack, bool create) =>
            stack != null && PortablePlayerIdentity.TryGet(stack.Meta, create, out string identity) ? identity : null;

        private static IEnumerable<SlotController> PlayerSlots(Inventory inventory)
        {
            if (inventory.Slots != null) foreach (SlotController slot in inventory.Slots) yield return slot;
            if (inventory.CharacterSlots != null) foreach (SlotController slot in inventory.CharacterSlots) yield return slot;
            foreach (SlotController slot in BackpackSlots(inventory)) yield return slot;
        }

        // The backpack reads character slot 5, which is missing while the slots are being rebuilt.
        private static SlotController[] BackpackSlots(Inventory inventory)
        {
            if (BackpackStorage.instance == null || inventory.CharacterSlots == null || inventory.CharacterSlots.Length <= 5)
                return Array.Empty<SlotController>();
            try { return BackpackStorage.instance.GetStorage()?.Slots ?? Array.Empty<SlotController>(); }
            catch (Exception) { return Array.Empty<SlotController>(); }
        }

        private static bool IsPlayerSlot(Inventory inventory, SlotController candidate) =>
            candidate != null && PlayerSlots(inventory).Any(slot => ReferenceEquals(slot, candidate));
    }
}
