using System;
using System.Linq;
using ObenseuerCore;
using PortableMusicPlayer.Core;
using UnityEngine;

namespace PortableMusicPlayer.Items
{
    // The framework has no API for what a trader buys, so this adds the five items to the buyers' own
    // allowed lists using the game's "_item_<title>" rule, which accepts that exact item and nothing
    // else. The lists come from the scene, so they are extended each time a scene loads.
    internal static class ShopBuyers
    {
        private static readonly string[] entries = Catalog.All.Select(e => "_item_" + e.Title).ToArray();

        internal static void Install() => Events.SceneLoaded += OnSceneLoaded;

        internal static void Remove() => Events.SceneLoaded -= OnSceneLoaded;

        private static void OnSceneLoaded(string scene)
        {
            foreach (Trade trade in UnityEngine.Object.FindObjectsByType<Trade>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!LootPlan.Buyers.Contains(trade.title)) continue;
                string[] allowed = trade.allowedCategories ?? Array.Empty<string>();
                // An empty list already accepts everything.
                if (allowed.Length == 0) continue;
                string[] missing = entries.Where(e => !allowed.Contains(e, StringComparer.OrdinalIgnoreCase)).ToArray();
                if (missing.Length > 0) trade.allowedCategories = allowed.Concat(missing).ToArray();
            }
        }
    }
}
