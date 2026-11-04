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
    // the game's rarity and restocking. No placement names Kalle Ketku or Ari Pulsunmäki:
    // those categories exist, but no trader in the game uses them.
    internal static class LootPlan
    {
        internal static readonly IReadOnlyList<LootPlacement> All = new[]
        {
            Find(Device.BudgetEarbuds, "NightStand"), Find(Device.BudgetEarbuds, "Suitcase"),
            Find(Device.BudgetEarbuds, "Locker"), Find(Device.BudgetEarbuds, "SchoolDesk"),
            Find(Device.BudgetEarbuds, "OfficeElectronics"), Find(Device.BudgetEarbuds, "Cupboard"),
            Sell(Device.BudgetEarbuds, "General Store Trader"), Sell(Device.BudgetEarbuds, "Keke Roinaneva"),

            Find(Device.PocketPlayer, "NightStand"), Find(Device.PocketPlayer, "Suitcase"),
            Find(Device.PocketPlayer, "Cupboard"), Find(Device.PocketPlayer, "OfficeElectronics"),
            Sell(Device.PocketPlayer, "Keke Roinaneva"),

            Find(Device.WorkHeadphones, "Locker"), Find(Device.WorkHeadphones, "Plastic Box"),
            Find(Device.WorkHeadphones, "MinesSteelCabinet "), Find(Device.WorkHeadphones, "Toolbox"),
            Find(Device.WorkHeadphones, "Storage"),
            Sell(Device.WorkHeadphones, "General Store Trader"), Sell(Device.WorkHeadphones, "OS Mining Services"),
            Sell(Device.WorkHeadphones, "General Store Bazaar"),

            Find(Device.RechargeablePlayer, "DeskFancy"), Find(Device.RechargeablePlayer, "Locker"),
            Find(Device.RechargeablePlayer, "OfficeElectronics"),
            Sell(Device.RechargeablePlayer, "General Store Bazaar"),

            Find(Device.HiFiHeadphones, "DeskFancy"), Find(Device.HiFiHeadphones, "Suitcase"),
            Sell(Device.HiFiHeadphones, "Pawnshop")
        };

        // Items a trader always keeps one of, by the trader's in-game name. Names rather than trader IDs,
        // because the One stop shop shares its ID with a market stall.
        internal static readonly IReadOnlyList<(Device Device, string Shop)> AlwaysStocked = new[]
        {
            (Device.PocketPlayer, "One stop shop"), (Device.RechargeablePlayer, "One stop shop"),
            (Device.BudgetEarbuds, "One stop shop"),
            (Device.PocketPlayer, "General Trader"), (Device.RechargeablePlayer, "General Trader")
        };

        // Traders that buy all five items although their own rules would refuse them.
        internal static readonly IReadOnlyList<string> Buyers = new[] { "Pawn Shop", "One stop shop" };

        // The Pawn Shop's stock category. It buys the music players but never sells them.
        internal const string PawnShopCategory = "Pawnshop";

        private static LootPlacement Find(Device device, string category) => new LootPlacement(device, category, LootList.Rare, false);
        private static LootPlacement Sell(Device device, string category, LootList list = LootList.Rare) => new LootPlacement(device, category, list, true);
    }
}
