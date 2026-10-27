using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using ObenseuerCore;
using PortableMusicPlayer.Audio;
using UnityEngine;

namespace PortableMusicPlayer.Library
{
    // Optional reflection boundary: Portable Music Player still loads when Obentunes is absent.
    internal sealed class ObentunesLibrary
    {
        internal const string Root = "obentunes:";
        private const float RefreshSeconds = 1f;
        // Obentunes extracts a game track to a file, which is read into memory and deleted at once.
        private static readonly string CacheFolder = Path.Combine(Path.GetTempPath(), "PortableMusicPlayer");
        private readonly CoreLog log;
        private readonly Dictionary<string, string> names = new Dictionary<string, string>(LibraryPaths.Comparer);
        private readonly Dictionary<string, object> clips = new Dictionary<string, object>(LibraryPaths.Comparer);
        private PropertyInfo clipsProperty;
        private MethodInfo indexClips;
        private MethodInfo extract;
        private Type customType;
        private PropertyInfo customPath;
        private MusicCollection collection;
        private string signature = "";
        private float refreshAt;
        private bool bindFailed;
        private object walked;
        private int walkedCount = -1;

        internal ObentunesLibrary(CoreLog log) { this.log = log; }

        internal bool IsDetected { get; private set; }
        internal bool IsReady { get; private set; }

        internal bool Refresh(float now)
        {
            if (now < refreshAt) return false;
            refreshAt = now + RefreshSeconds;
            bool wasDetected = IsDetected;
            if (!IsDetected && !TryBind()) return false;

            IEnumerable clips;
            try { clips = clipsProperty.GetValue(null, null) as IEnumerable; }
            catch (Exception error)
            {
                log.Warn("Could not read the Obentunes music index: " + RootCause(error).Message);
                return wasDetected != IsDetected;
            }
            if (clips == null) return wasDetected != IsDetected;
            // Obentunes 1.1.0 fills one list once, on a background thread, so the same list at the same
            // count is the same index and needs no walk.
            int count = (clips as ICollection)?.Count ?? -1;
            if (IsReady && count >= 0 && ReferenceEquals(clips, walked) && count == walkedCount) return wasDetected != IsDetected;
            walked = clips;
            walkedCount = count;

            var found = new Dictionary<string, Tuple<string, string>>(LibraryPaths.Comparer);
            var refs = new Dictionary<string, object>(LibraryPaths.Comparer);
            foreach (object clip in clips)
            {
                if (clip == null) continue;
                PropertyInfo nameProperty = clip.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo displayProperty = clip.GetType().GetProperty("DisplayName", BindingFlags.Instance | BindingFlags.Public);
                string name = nameProperty?.GetValue(clip, null) as string;
                string display = displayProperty?.GetValue(clip, null) as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                string path = PathFor(name);
                if (found.ContainsKey(path)) continue;
                found.Add(path, Tuple.Create(name, Label(display, name)));
                refs.Add(path, clip);
            }

            string next = string.Join("|", found.OrderBy(pair => pair.Key, LibraryPaths.Comparer)
                .Select(pair => pair.Key + "#" + pair.Value.Item2));
            bool becameReady = !IsReady;
            IsReady = true;
            this.clips.Clear();
            foreach (KeyValuePair<string, object> pair in refs) this.clips.Add(pair.Key, pair.Value);
            if (!becameReady && string.Equals(signature, next, StringComparison.Ordinal)) return wasDetected != IsDetected;
            signature = next;
            names.Clear();
            foreach (KeyValuePair<string, Tuple<string, string>> pair in found) names.Add(pair.Key, pair.Value.Item1);
            MusicTrack[] tracks = found.OrderBy(pair => pair.Value.Item2, LibraryPaths.Comparer)
                .ThenBy(pair => pair.Key, LibraryPaths.Comparer)
                .Select(pair => new MusicTrack(pair.Key, DisplayInfo(pair.Key, pair.Value.Item2)))
                .ToArray();
            collection = tracks.Length == 0 ? null : new MusicCollection(Root, "Obentunes", tracks);
            return true;
        }

        internal LibrarySnapshot Merge(LibrarySnapshot local)
        {
            var collections = new List<MusicCollection>();
            if (collection != null) collections.Add(collection);
            collections.AddRange(local.Collections);
            return new LibrarySnapshot(collections.ToArray(), local.Warnings.ToArray());
        }

        internal bool Contains(string path) => names.ContainsKey(path ?? "");

        internal bool TryMapGameRadioPath(string gameRadioPath, out string path)
        {
            path = null;
            if (!GameRadioLibrary.IsPath(gameRadioPath)) return false;
            string name = gameRadioPath.Substring((GameRadioLibrary.Root + "\\").Length);
            string candidate = PathFor(name);
            if (!names.ContainsKey(candidate)) return false;
            path = candidate;
            return true;
        }

        // Opens an indexed track for the stream worker, or returns null when the path is not indexed. Custom
        // tracks stream from their own file. Game tracks are extracted from the sound banks by Obentunes and decoded
        // from memory, so nothing waits for a whole-song decode and Obentunes' clip cache is never involved.
        internal Func<CancellationToken, IPcmDecoder> Opener(string path)
        {
            if (!clips.TryGetValue(path ?? "", out object clip)) return null;
            if (customType.IsInstanceOfType(clip))
            {
                string file = customPath.GetValue(clip, null) as string;
                return token =>
                {
                    if (!File.Exists(file)) throw new FileNotFoundException("The Obentunes track file is missing");
                    return PcmDecoders.Open(file, token);
                };
            }
            string dataPath = Application.dataPath;
            return token =>
            {
                string file;
                try { file = extract.Invoke(null, new[] { clip, dataPath, CacheFolder }) as string; }
                catch (TargetInvocationException error) { throw new IOException(RootCause(error).Message, RootCause(error)); }
                if (string.IsNullOrEmpty(file)) throw new IOException("Obentunes could not extract this track");
                byte[] data;
                try
                {
                    // Extraction cannot be cancelled; a song skipped meanwhile stops here.
                    token.ThrowIfCancellationRequested();
                    data = File.ReadAllBytes(file);
                }
                finally { DeleteExtracted(file); }
                token.ThrowIfCancellationRequested();
                return PcmDecoders.Open(new MemoryStream(data, false), Path.GetExtension(file), token);
            };
        }

        // A virus scanner can briefly hold a freshly written file. A leftover file in the temp folder is harmless,
        // and must not turn a successful read into a failed song.
        private static void DeleteExtracted(string file)
        {
            try { File.Delete(file); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static bool IsPath(string path) => !string.IsNullOrEmpty(path) &&
            path.StartsWith(Root + "\\", StringComparison.OrdinalIgnoreCase);

        internal static string NameFromPath(string path) => IsPath(path)
            ? path.Substring((Root + "\\").Length) : "";

        private bool TryBind()
        {
            if (bindFailed) return false;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
                string.Equals(candidate.GetName().Name, "Obentunes", StringComparison.OrdinalIgnoreCase));
            if (assembly == null) return false;
            Type fetcher = assembly.GetType("Obentunes.ClipFetcher", false);
            Type gameType = assembly.GetType("Obentunes.GameClipRef", false);
            customType = assembly.GetType("Obentunes.CustomClipRef", false);
            customPath = customType?.GetProperty("Path", BindingFlags.Instance | BindingFlags.Public);
            clipsProperty = fetcher?.GetProperty("Clips", BindingFlags.Static | BindingFlags.Public);
            indexClips = fetcher?.GetMethod("IndexClips", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
            // Obentunes' own extractor (1.1.0), so game tracks decode exactly as Obentunes rebuilds them.
            extract = gameType == null ? null : fetcher?.GetMethod("ExtractToCache", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { gameType, typeof(string), typeof(string) }, null);
            if (clipsProperty == null || indexClips == null || extract == null || customPath == null)
            {
                bindFailed = true;
                log.Warn("Obentunes was detected, but its music-library API is not compatible. Game Radio will be used instead.");
                return false;
            }
            try { indexClips.Invoke(null, null); }
            catch (Exception error)
            {
                bindFailed = true;
                log.Warn("Obentunes was detected, but its music index could not start: " + RootCause(error).Message);
                return false;
            }
            IsDetected = true;
            log.Info("Obentunes detected. Its indexed music will replace the Game Radio collection.");
            return true;
        }

        private static string PathFor(string name) => Root + "\\" + (name ?? "");

        private static string Label(string display, string name)
        {
            string value = string.IsNullOrWhiteSpace(display) ? name : display;
            return string.IsNullOrWhiteSpace(value) ? "Obentunes track" : value.Trim();
        }

        // Obentunes exposes one display string rather than separate metadata. Preserve its common
        // "artist - title" convention when possible so Now Playing can use the same hierarchy as files.
        private static TrackInfo DisplayInfo(string path, string label)
        {
            int separator = label.IndexOf(" - ", StringComparison.Ordinal);
            return separator > 0 && separator + 3 < label.Length
                ? new TrackInfo(path, label.Substring(separator + 3).Trim(), label.Substring(0, separator).Trim())
                : new TrackInfo(path, label);
        }

        private static Exception RootCause(Exception error)
        {
            while (error is TargetInvocationException invocation && invocation.InnerException != null)
                error = invocation.InnerException;
            return error;
        }
    }
}
