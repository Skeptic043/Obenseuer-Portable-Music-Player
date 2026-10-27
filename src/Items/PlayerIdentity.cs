using System;
using System.Collections.Generic;
using OS.Items;

namespace PortableMusicPlayer.Items
{
    internal static class PortablePlayerIdentity
    {
        internal const string Prefix = "PortableMusicPlayer/v1/";
        internal const string TooltipText = "Portable music player";

        internal static ItemTextData Template() => new ItemTextData { content = Prefix };

        internal static bool TryGet(object[] meta, bool create, out string identity)
        {
            identity = null;
            ItemTextData text = Text(meta);
            if (text == null) return false;
            Normalize(meta);
            if (TryParse(text.content, out identity)) return true;
            if (!create || !IsToken(text.content)) return false;
            text.content = Create();
            return TryParse(text.content, out identity);
        }

        internal static bool Renew(object[] meta, out string identity)
        {
            identity = null;
            ItemTextData text = Text(meta);
            if (text == null || !IsToken(text.content)) return false;
            text.content = Create();
            return TryParse(text.content, out identity);
        }

        internal static void Normalize(object[] meta)
        {
            if (meta == null) return;
            foreach (object value in meta)
            {
                if (!(value is ItemAmmodata charge)) continue;
                if (charge.ammoItemcategories == null) charge.ammoItemcategories = Array.Empty<string>();
                if (charge.ammoItems == null) charge.ammoItems = new List<ItemReference>();
                if (charge.itemsToSpawn == null) charge.itemsToSpawn = new List<ItemReference>();
            }
        }

        internal static bool IsToken(string value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

        private static string Create() => Prefix + Guid.NewGuid().ToString("N");

        private static bool TryParse(string value, out string identity)
        {
            identity = null;
            if (!IsToken(value)) return false;
            string candidate = value.Substring(Prefix.Length);
            if (candidate.Length != 32 || !Guid.TryParseExact(candidate, "N", out Guid parsed)) return false;
            identity = parsed.ToString("N");
            return true;
        }

        private static ItemTextData Text(object[] meta)
        {
            if (meta != null)
                foreach (object value in meta)
                    if (value is ItemTextData text) return text;
            return null;
        }
    }
}
