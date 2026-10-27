using System;
using OS.Items;

namespace PortableMusicPlayer.Items
{
    // Inventory actions on the two players. Item definitions are catalogue data, not saved per stack.
    [Serializable]
    internal sealed class PowerAction : Item.ItemAction
    {
        private readonly ReplaceBatteryAction replacement = new ReplaceBatteryAction();

        public PowerAction() { Name = "Power"; }

        public override bool CanUse(ItemData itemData, out string message)
        {
            if (!Player(out message)) return false;
            if (PortableMusicPlayerMod.Player.NeedsBatteryReplacement(itemData))
            {
                Name = "Change batteries";
                return replacement.CanUse(itemData, out message);
            }
            Name = "Power";
            return PortableMusicPlayerMod.Player.CanTogglePower(itemData, out message);
        }

        public override void Invoke(ItemData itemData, bool worldUse)
        {
            if (PortableMusicPlayerMod.Player != null && PortableMusicPlayerMod.Player.NeedsBatteryReplacement(itemData))
            {
                replacement.Invoke(itemData, worldUse);
                return;
            }
            base.Invoke(itemData, worldUse);
            PortableMusicPlayerMod.Player?.TogglePower(itemData);
        }

        private static bool Player(out string message)
        {
            message = PortableMusicPlayerMod.Player == null ? "Music player unavailable" : string.Empty;
            return PortableMusicPlayerMod.Player != null;
        }
    }

    [Serializable]
    internal sealed class OpenPlayerAction : Item.ItemAction
    {
        public OpenPlayerAction() { Name = "Open"; }

        public override bool CanUse(ItemData itemData, out string message)
        {
            if (PortableMusicPlayerMod.Player == null) { message = "Music player unavailable"; return false; }
            return PortableMusicPlayerMod.Player.CanOpen(itemData, out message);
        }

        public override void Invoke(ItemData itemData, bool worldUse)
        {
            base.Invoke(itemData, worldUse);
            PortableMusicPlayerMod.Player?.OpenWindow();
        }
    }

    [Serializable]
    internal sealed class ReplaceBatteryAction : ItemReloadable
    {
        public ReplaceBatteryAction() { Name = "Change batteries"; }

        public override bool CanUse(ItemData itemData, out string message)
        {
            PortableMusicPlayerMod.Player?.PrepareBatteryReplacement(itemData);
            return base.CanUse(itemData, out message);
        }

        public override void Invoke(ItemData itemData, bool worldUse)
        {
            PortableMusicPlayerMod.Player?.PrepareBatteryReplacement(itemData);
            base.Invoke(itemData, worldUse);
            PortableMusicPlayerMod.Player?.FinishBatteryReplacement(itemData);
        }
    }
}
