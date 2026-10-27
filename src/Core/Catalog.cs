using System;
using System.Collections.Generic;
using System.Linq;

namespace PortableMusicPlayer.Core
{
    internal enum Device { PocketPlayer, RechargeablePlayer, BudgetEarbuds, WorkHeadphones, HiFiHeadphones }

    internal sealed class CatalogEntry
    {
        internal Device Device { get; set; }
        // Never rename a key: the framework derives the saved item ID from it.
        internal string Key { get; set; }
        internal string Title { get; set; }
        internal string Description { get; set; }
        internal int BaseValue { get; set; }
        internal int TemplateId { get; set; }
        internal bool IsPlayer { get; set; }
        // Full-charge lifetime during normal playback; BatteryMeter applies the passing-time rate.
        internal double PlaybackHours { get; set; }
        internal bool TakesAaBatteries { get; set; }
        internal bool Charges { get; set; }
    }

    internal static class Catalog
    {
        internal const int HandheldConsoleId = 19300;
        internal const int EarmuffsId = 19480;
        internal const int BatteriesAaId = 10730;

        internal static readonly IReadOnlyList<CatalogEntry> All = new[]
        {
            new CatalogEntry
            {
                Device = Device.PocketPlayer, Key = "pocket-player", Title = "Pocket Music Player",
                Description = "A cheap music player that runs on one AA battery. Needs headphones.",
                BaseValue = 260, TemplateId = HandheldConsoleId, IsPlayer = true, PlaybackHours = 12d,
                TakesAaBatteries = true
            },
            new CatalogEntry
            {
                Device = Device.RechargeablePlayer, Key = "rechargeable-player", Title = "Rechargeable Music Player",
                Description = "A slim music player with a rechargeable battery. Needs headphones.",
                BaseValue = 950, TemplateId = HandheldConsoleId, IsPlayer = true, PlaybackHours = 24d,
                Charges = true
            },
            new CatalogEntry
            {
                Device = Device.BudgetEarbuds, Key = "budget-earbuds", Title = "Budget Earbuds",
                Description = "Cheap earbuds. Thin and quiet, but they play music.",
                BaseValue = 45, TemplateId = EarmuffsId
            },
            new CatalogEntry
            {
                Device = Device.WorkHeadphones, Key = "work-headphones", Title = "Work Headphones",
                Description = "Hearing protection with speakers inside. Blocks out the world, not the music.",
                BaseValue = 320, TemplateId = EarmuffsId
            },
            new CatalogEntry
            {
                Device = Device.HiFiHeadphones, Key = "hifi-headphones", Title = "Hi-Fi Headphones",
                Description = "Studio headphones with full, clean sound.",
                BaseValue = 1600, TemplateId = EarmuffsId
            }
        };

        internal static IEnumerable<Device> Players => All.Where(e => e.IsPlayer).Select(e => e.Device);
        internal static IEnumerable<Device> Headphones => All.Where(e => !e.IsPlayer).Select(e => e.Device);

        internal static CatalogEntry Entry(Device device) =>
            All.FirstOrDefault(e => e.Device == device) ?? throw new ArgumentOutOfRangeException(nameof(device));
    }
}
