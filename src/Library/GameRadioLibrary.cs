using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortableMusicPlayer.Library
{
    // The native Radio component already holds the exact AudioClips used by the current game build.
    // Reusing those references avoids duplicating or extracting the game's audio files.
    internal sealed class GameRadioLibrary
    {
        internal const string Root = "game-radio:";
        private const float RefreshSeconds = 2f;
        private static readonly FieldInfo Sounds = AccessTools.Field(typeof(Radio), "sounds");
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(LibraryPaths.Comparer);
        private MusicCollection collection;
        private string signature = "";
        private float refreshAt;

        // Set when a radio wakes or a scene loads, which also covers radios that load inactive. Searching every
        // loaded object is the costly part, so it runs only then or once a held clip was unloaded.
        internal static bool RadioAppeared = true;

        static GameRadioLibrary() { SceneManager.sceneLoaded += (scene, mode) => RadioAppeared = true; }

        internal bool IsReady => clips.Count > 0;

        internal bool Refresh(float now)
        {
            if (now < refreshAt) return false;
            refreshAt = now + RefreshSeconds;
            if (!RadioAppeared && HeldClipsAlive()) return false;
            RadioAppeared = false;
            var found = new Dictionary<string, AudioClip>(LibraryPaths.Comparer);
            foreach (Radio radio in Resources.FindObjectsOfTypeAll<Radio>())
            {
                RandomAudio sounds = radio == null ? null : Sounds?.GetValue(radio) as RandomAudio;
                if (sounds?.audioClips == null) continue;
                foreach (AudioClip clip in sounds.audioClips)
                {
                    if (clip == null || string.IsNullOrWhiteSpace(clip.name) ||
                        string.Equals(clip.name, "radio_static_001", StringComparison.OrdinalIgnoreCase)) continue;
                    string path = PathFor(clip.name);
                    if (!found.ContainsKey(path)) found.Add(path, clip);
                }
            }
            string next = string.Join("|", found.OrderBy(pair => pair.Key, LibraryPaths.Comparer)
                .Select(pair => pair.Key + "#" + pair.Value.GetInstanceID()));
            if (string.Equals(signature, next, StringComparison.Ordinal)) return false;
            signature = next;
            clips.Clear();
            foreach (KeyValuePair<string, AudioClip> pair in found) clips.Add(pair.Key, pair.Value);
            MusicTrack[] tracks = clips.OrderBy(pair => Label(pair.Value.name), LibraryPaths.Comparer)
                .Select(pair => new MusicTrack(pair.Key, new TrackInfo(pair.Key, Label(pair.Value.name))))
                .ToArray();
            collection = tracks.Length == 0 ? null : new MusicCollection(Root, "Game Radio", tracks);
            return true;
        }

        internal LibrarySnapshot Merge(LibrarySnapshot local)
        {
            var collections = new List<MusicCollection>();
            if (collection != null) collections.Add(collection);
            collections.AddRange(local.Collections);
            return new LibrarySnapshot(collections.ToArray(), local.Warnings.ToArray());
        }

        private bool HeldClipsAlive()
        {
            foreach (AudioClip clip in clips.Values) if (clip == null) return false;
            return true;
        }

        internal bool TryGetClip(string path, out AudioClip clip) => clips.TryGetValue(path ?? "", out clip) && clip != null;

        internal static bool IsPath(string path) => !string.IsNullOrEmpty(path) &&
            path.StartsWith(Root + "\\", StringComparison.OrdinalIgnoreCase);

        internal static string PathFor(string name) => Root + "\\" + (name ?? "");

        private static string Label(string name)
        {
            string value = name ?? "Game radio track";
            if (value.StartsWith("radio_", StringComparison.OrdinalIgnoreCase)) value = value.Substring(6);
            else if (value.StartsWith("music_", StringComparison.OrdinalIgnoreCase)) value = value.Substring(6);
            value = value.Replace('_', ' ').Trim();
            return value.Length == 0 ? "Game radio track" : value;
        }
    }
}
