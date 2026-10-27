using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PortableMusicPlayer.Library
{
    internal sealed class MusicFolder
    {
        internal MusicFolder(string name, string relativePath)
        {
            Name = name;
            RelativePath = relativePath;
        }

        internal string Name { get; }
        internal string RelativePath { get; }
    }

    internal sealed class FolderView
    {
        internal FolderView(string relativePath, MusicFolder[] folders, MusicTrack[] tracks)
        {
            RelativePath = relativePath;
            Folders = Array.AsReadOnly(folders);
            Tracks = Array.AsReadOnly(tracks);
        }

        internal string RelativePath { get; }
        internal IReadOnlyList<MusicFolder> Folders { get; }
        internal IReadOnlyList<MusicTrack> Tracks { get; }
    }

    internal static class FolderBrowser
    {
        internal static FolderView Open(MusicCollection collection, string relativePath)
        {
            if (collection == null) return new FolderView("", Array.Empty<MusicFolder>(), Array.Empty<MusicTrack>());
            string requested = Normalize(relativePath);
            FolderView view = Build(collection, requested);
            return requested.Length == 0 || view.Folders.Count > 0 || view.Tracks.Count > 0
                ? view : Build(collection, "");
        }

        internal static string Parent(string relativePath)
        {
            string value = Normalize(relativePath);
            int separator = value.LastIndexOf(Path.DirectorySeparatorChar);
            return separator < 0 ? "" : value.Substring(0, separator);
        }

        internal static string RelativeDirectory(MusicCollection collection, string trackPath)
        {
            if (collection == null || string.IsNullOrEmpty(trackPath)) return "";
            string root = collection.Id.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string directory;
            try { directory = Path.GetDirectoryName(trackPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch (Exception error) when (LibraryPaths.FilesystemError(error)) { return ""; }
            if (string.IsNullOrEmpty(directory) || LibraryPaths.Comparer.Equals(directory, root)) return "";
            string prefix = root + Path.DirectorySeparatorChar;
            return directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? Normalize(directory.Substring(prefix.Length)) : "";
        }

        private static FolderView Build(MusicCollection collection, string current)
        {
            var folders = new Dictionary<string, MusicFolder>(LibraryPaths.Comparer);
            var tracks = new List<MusicTrack>();
            string childPrefix = current.Length == 0 ? "" : current + Path.DirectorySeparatorChar;
            foreach (MusicTrack track in collection.Tracks)
            {
                string directory = RelativeDirectory(collection, track.Path);
                string remaining;
                if (current.Length == 0) remaining = directory;
                else if (LibraryPaths.Comparer.Equals(directory, current)) remaining = "";
                else if (directory.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase)) remaining = directory.Substring(childPrefix.Length);
                else continue;

                if (remaining.Length == 0)
                {
                    tracks.Add(track);
                    continue;
                }
                int separator = remaining.IndexOf(Path.DirectorySeparatorChar);
                string name = separator < 0 ? remaining : remaining.Substring(0, separator);
                string path = current.Length == 0 ? name : current + Path.DirectorySeparatorChar + name;
                if (!folders.ContainsKey(path)) folders.Add(path, new MusicFolder(name, path));
            }
            MusicFolder[] children = folders.Values.OrderBy(item => item.Name, LibraryPaths.Comparer).ToArray();
            MusicTrack[] direct = tracks.OrderBy(track => track.Info.Label, LibraryPaths.Comparer)
                .ThenBy(track => track.Path, LibraryPaths.Comparer).ToArray();
            return new FolderView(current, children, direct);
        }

        private static string Normalize(string relativePath)
        {
            string value = (relativePath ?? "").Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Trim(Path.DirectorySeparatorChar);
            if (value.Length == 0) return "";
            string[] parts = value.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Any(part => part == "." || part == "..") ? "" : string.Join(Path.DirectorySeparatorChar.ToString(), parts);
        }
    }
}
