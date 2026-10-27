using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ObenseuerCore;
using OS.Items;
using PortableMusicPlayer.Core;
using UnityEngine;

namespace PortableMusicPlayer.Items
{
    // Defines the five items from their native templates, then replaces what the templates bring along
    // that does not belong: the console's game action and network data, and the wrong battery rules.
    internal static class ItemSetup
    {
        private static readonly Dictionary<int, Device> devices = new Dictionary<int, Device>();
        private static CoreLog log;

        internal static bool Define(string modId, string assets, CoreLog modLog, out string failure)
        {
            log = modLog;
            devices.Clear();
            var pending = new Dictionary<int, Device>();
            foreach (CatalogEntry entry in Catalog.All)
            {
                // The MTL beside the OBJ names the painted texture.
                ModelHandle model = ModelRegistry.LoadObj(modId, entry.Key, Path.Combine(assets, entry.Key + ".obj"), null, WorldModelOptions());
                if (!model.IsReady) log.Warn(entry.Title + " keeps its template's model: " + model.Error);
                int id = ItemRegistry.Define(modId, entry.Key, new ItemDefinition
                {
                    TemplateId = entry.TemplateId,
                    Title = entry.Title,
                    Description = entry.Description,
                    BaseValue = entry.BaseValue,
                    BaseStackable = 1,
                    IconPath = Path.Combine(assets, entry.Key + "-icon.png"),
                    Model = model.IsReady ? model : null
                });
                if (!IsUsableId(id))
                {
                    failure = "the item registry returned invalid ID " + id + " for " + entry.Title;
                    return false;
                }
                if (pending.ContainsKey(id))
                {
                    failure = "the item registry returned duplicate ID " + id + " for " + entry.Title;
                    return false;
                }
                pending[id] = entry.Device;
            }
            if (pending.Count != Catalog.All.Count)
            {
                failure = "the item registry did not return all five required item IDs";
                return false;
            }
            foreach (KeyValuePair<int, Device> pair in pending) devices.Add(pair.Key, pair.Value);
            failure = null;
            return true;
        }

        // Item templates spawn upright. These handheld models rest face-up, with their lowest point
        // aligned to the template so the box collider does not hold them above the floor. The models are
        // authored in metres; fitting them to the template's size would blow the earbuds up to earmuff size.
        private static ModelOptions WorldModelOptions() => new ModelOptions
        {
            FitToTemplate = false,
            Pivot = ModelPivot.BottomCenter,
            EulerOffset = new Vector3(-90f, 0f, 0f)
        };

        internal static bool TryIdOf(Device device, out int id)
        {
            foreach (KeyValuePair<int, Device> pair in devices)
            {
                if (pair.Value != device) continue;
                id = pair.Key;
                return true;
            }
            id = -1;
            return false;
        }

        internal static bool IsUsableId(int id) => id > 1;

        internal static bool Verify(out string failure)
        {
            if (ItemDatabase.database == null)
            {
                failure = "the native item catalogue is unavailable after ItemRegistry.Loaded";
                return false;
            }
            if (devices.Count != Catalog.All.Count)
            {
                failure = "the registry did not retain all five required item IDs";
                return false;
            }
            foreach (KeyValuePair<int, Device> pair in devices)
            {
                CatalogEntry expected = Catalog.Entry(pair.Value);
                Item actual = ItemDatabase.database.FirstOrDefault(item => item != null && item.ID == pair.Key);
                if (actual == null)
                {
                    failure = expected.Title + " (ID " + pair.Key + ") is missing from the native item catalogue";
                    return false;
                }
                if (!string.Equals(actual.Title, expected.Title, StringComparison.Ordinal))
                {
                    failure = "item ID " + pair.Key + " belongs to '" + actual.Title + "', not the expected '" + expected.Title + "'";
                    return false;
                }
            }
            failure = null;
            return true;
        }

        internal static bool TryDevice(Item item, out Device device)
        {
            device = default;
            return item != null && devices.TryGetValue(item.ID, out device);
        }

        internal static bool TryDevice(int itemId, out Device device) => devices.TryGetValue(itemId, out device);

        // Called only after all five registered IDs and titles have been verified in the native catalogue.
        internal static void Normalize()
        {
            if (ItemDatabase.database == null) return;
            foreach (KeyValuePair<int, Device> pair in devices)
            {
                Item item = ItemDatabase.database.FirstOrDefault(candidate => candidate != null && candidate.ID == pair.Key);
                if (item == null) continue;
                CatalogEntry entry = Catalog.Entry(pair.Value);
                if (entry.IsPlayer) NormalizePlayer(item, entry);
            }
        }

        private static void NormalizePlayer(Item item, CatalogEntry entry)
        {
            item.Actions = new Item.ItemAction[] { new PowerAction(), new OpenPlayerAction() };

            ItemAmmodata charge = item.Meta?.OfType<ItemAmmodata>().FirstOrDefault() ?? new ItemAmmodata();
            charge.maxAmmo = 100f;
            charge.ammo = 100f;
            charge.destroyWhenEmpty = false;
            charge.showAsAmount = false;
            charge.ammoAffectsPrice = false;
            charge.characterSlotDrain = false;
            charge.ammoItemcategories = Array.Empty<string>();
            charge.ammoItems = entry.TakesAaBatteries
                ? new List<ItemReference> { new ItemReference(Catalog.BatteriesAaId) }
                : new List<ItemReference>();
            charge.itemsToSpawn = new List<ItemReference>();
            item.Meta = new object[] { charge, PortablePlayerIdentity.Template() };
        }
    }
}
