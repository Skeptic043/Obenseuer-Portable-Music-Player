using ObenseuerCore;
using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Items
{
    internal static class LootSetup
    {
        // The framework keeps these entries and reapplies them after every rebuild of the game's pools.
        internal static void Apply(string modId, CoreLog log)
        {
            foreach (LootPlacement placement in LootPlan.All)
            {
                if (!ItemSetup.TryIdOf(placement.Device, out int id) || !ItemSetup.IsUsableId(id))
                {
                    log.Error("Loot registration stopped because " + Catalog.Entry(placement.Device).Title + " has no valid registered item ID.");
                    return;
                }
                LootTier tier = placement.List == LootList.Rare ? LootTier.Rare : LootTier.Common;
                if (!LootPools.Add(modId, placement.Category, id, 1, tier))
                    log.Warn(Catalog.Entry(placement.Device).Title + " was not added to " + placement.Category + ".");
            }
        }
    }
}
