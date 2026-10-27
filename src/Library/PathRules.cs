using System;
using System.IO;

namespace PortableMusicPlayer.Library
{
    internal static class PathRules
    {
        internal static string NormalizeLocalDrivePath(string path)
        {
            string value = (path ?? string.Empty).Trim();
            if (value.Length < 3 || !char.IsLetter(value[0]) || value[1] != ':' ||
                (value[2] != '\\' && value[2] != '/'))
                throw new ArgumentException("Path must be an absolute local drive path.");

            string full = Path.GetFullPath(value);
            Uri uri = new Uri(full);
            if (!uri.IsFile || uri.IsUnc)
                throw new ArgumentException("Path must be a local file or folder.");
            return full;
        }
    }
}
