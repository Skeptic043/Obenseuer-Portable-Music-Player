using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class LootPlanTests
    {
        public static void EveryPlacementNamesARealCategoryWithARareList()
        {
            // Adding to an empty rare list would switch rare picks on and take half of that container.
            Dictionary<string, JObject> categories = GameCategories();
            foreach (LootPlacement placement in LootPlan.All)
            {
                Assert.True(categories.ContainsKey(placement.Category), "no game category named '" + placement.Category + "'");
                Assert.True(((JArray)categories[placement.Category]["ItemRare"]).Count > 0, placement.Category + " has no rare items");
            }
        }

        public static void CheaperItemsAreNeverScarcerThanDearerOnes()
        {
            Dictionary<string, JObject> categories = GameCategories();
            List<CatalogEntry> byPrice = Catalog.All.OrderBy(e => e.BaseValue).ToList();
            for (int i = 1; i < byPrice.Count; i++)
                Assert.True(Availability(categories, byPrice[i - 1].Device) >= Availability(categories, byPrice[i].Device),
                    byPrice[i - 1].Device + " is scarcer than the dearer " + byPrice[i].Device);
        }

        // The summed chance that one pick from each placement's list is this item. A container tries its
        // rare list first half the time, then picks evenly by copies.
        private static double Availability(Dictionary<string, JObject> categories, Device device) =>
            LootPlan.All.Where(p => p.Device == device).Sum(p =>
                0.5d / (((JArray)categories[p.Category][p.List == LootList.Rare ? "ItemRare" : "ItemCommon"]).Count + 1));

        public static void EveryItemCanBeFoundAndBought()
        {
            foreach (CatalogEntry entry in Catalog.All)
            {
                Assert.True(LootPlan.All.Any(p => p.Device == entry.Device && !p.Shop), entry.Device + " is never found in containers");
                Assert.True(LootPlan.All.Any(p => p.Device == entry.Device && p.Shop), entry.Device + " is never sold");
            }
        }

        private static Dictionary<string, JObject> GameCategories()
        {
            string file = Path.Combine(Program.GamePath, "Obenseuer_Data", "StreamingAssets", "StorageSpawnCategory.json");
            JToken root = JToken.Parse(File.ReadAllText(file));
            return ((JArray)root["$content"]).Cast<JObject>().GroupBy(c => (string)c["Name"]).ToDictionary(g => g.Key, g => g.First());
        }
    }
}
