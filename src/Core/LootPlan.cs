using System.Collections.Generic;

namespace PortableMusicPlayer.Core
{
    internal enum LootList { Common, Rare }

    internal sealed class LootPlacement
    {
        internal Device Device { get; }
        // The game's category name, exactly as in StorageSpawnCategory.json.
        internal string Category { get; }
        internal LootList List { get; }
        // A trader's stock category rather than a container's.
        internal bool Shop { get; }

        internal LootPlacement(Device device, string category, LootList list, bool shop)
        {
            Device = device;
            Category = category;
            List = list;
            Shop = shop;
        }
    }

    // Where each item can be found or bought, by analogy with the native items it resembles:
    // phones and consoles for the players, Earmuffs and work gear for the Work Headphones.
    // Every entry has weight 1. Traders restock from their own categories, so shop stock follows
    // the game's rarity and restocking.
    internal static class LootPlan
    {
        internal static readonly IReadOnlyList<LootPlacement> All = new[]
        {
            Find(Device.BudgetEarbuds, "NightStand"), Find(Device.BudgetEarbuds, "Suitcase"),
            Find(Device.BudgetEarbuds, "Locker"), Find(Device.BudgetEarbuds, "SchoolDesk"),
            Find(Device.BudgetEarbuds, "OfficeElectronics"), Find(Device.BudgetEarbuds, "Cupboard"),
            Sell(Device.BudgetEarbuds, "General Store Trader"), Sell(Device.BudgetEarbuds, "Kalle Ketku", LootList.Common),
            Sell(Device.BudgetEarbuds, "Keke Roinaneva"),

            Find(Device.PocketPlayer, "NightStand"), Find(Device.PocketPlayer, "Suitcase"),
            Find(Device.PocketPlayer, "Cupboard"), Find(Device.PocketPlayer, "OfficeElectronics"),
            Sell(Device.PocketPlayer, "Kalle Ketku", LootList.Common), Sell(Device.PocketPlayer, "Ari Pulsunmäki"),
            Sell(Device.PocketPlayer, "Keke Roinaneva"), Sell(Device.PocketPlayer, "Pawnshop"),

            Find(Device.WorkHeadphones, "Locker"), Find(Device.WorkHeadphones, "Plastic Box"),
            Find(Device.WorkHeadphones, "MinesSteelCabinet "), Find(Device.WorkHeadphones, "Toolbox"),
            Find(Device.WorkHeadphones, "Storage"),
            Sell(Device.WorkHeadphones, "General Store Trader"), Sell(Device.WorkHeadphones, "OS Mining Services"),
            Sell(Device.WorkHeadphones, "General Store Bazaar"), Sell(Device.WorkHeadphones, "Kalle Ketku"),

            Find(Device.RechargeablePlayer, "DeskFancy"), Find(Device.RechargeablePlayer, "Locker"),
            Find(Device.RechargeablePlayer, "OfficeElectronics"),
            Sell(Device.RechargeablePlayer, "Ari Pulsunmäki"), Sell(Device.RechargeablePlayer, "Kalle Ketku"),
            Sell(Device.RechargeablePlayer, "Pawnshop"), Sell(Device.RechargeablePlayer, "General Store Bazaar"),

            Find(Device.HiFiHeadphones, "DeskFancy"), Find(Device.HiFiHeadphones, "Suitcase"),
            Sell(Device.HiFiHeadphones, "Pawnshop"), Sell(Device.HiFiHeadphones, "Ari Pulsunmäki"),
            Sell(Device.HiFiHeadphones, "Kalle Ketku")
        };

        private static LootPlacement Find(Device device, string category) => new LootPlacement(device, category, LootList.Rare, false);
        private static LootPlacement Sell(Device device, string category, LootList list = LootList.Rare) => new LootPlacement(device, category, list, true);
    }
}
