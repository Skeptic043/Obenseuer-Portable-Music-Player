using System;
using System.IO;
using BepInEx.Configuration;
using ObenseuerCore;
using UnityEngine;

namespace PortableMusicPlayer
{
    // Settings live in the framework's Mods menu. Readers use Value at the point of use, so edits apply live.
    internal sealed class Settings
    {
        internal Settings(ConfigFile config, string defaultFolder)
        {
            DefaultFolder = defaultFolder;
            AdditionalFolders = config.Bind("Music", "AdditionalFolders", "",
                @"Extra music folders, separated with |, for example D:\Music | E:\Game Music. Scans all subfolders.");
            PlayWhilePassingTime = config.Bind("Playback", "PlayWhilePassingTime", true,
                "Keep playing while time skips ahead: sleeping, waiting, reading, working a shift or watching TV.");
            PassingTimeDrain = config.Bind("Playback", "PassingTimeDrain", 50, new ConfigDescription(
                "Battery drain while playing as time skips ahead, as a percentage of the normal rate.",
                new AcceptableValueList<int>(0, 25, 50, 75, 100)));
            PlayThroughLoading = config.Bind("Playback", "PlayThroughLoading", true,
                "Keep the song going through doors and level loads instead of pausing until the load finishes.");
            TogglePower = Keybinds.Register(ModInfo.Guid, "TogglePower", "Toggle power", KeyCode.None);
            OpenPlayer = Keybinds.Register(ModInfo.Guid, "OpenPlayer", "Open player", KeyCode.None);
            PreviousTrack = Keybinds.Register(ModInfo.Guid, "PreviousTrack", "Previous track", KeyCode.LeftBracket);
            PlayPause = Keybinds.Register(ModInfo.Guid, "PlayPause", "Play / pause", KeyCode.Backslash);
            NextTrack = Keybinds.Register(ModInfo.Guid, "NextTrack", "Next track", KeyCode.RightBracket);
        }

        internal string DefaultFolder { get; }
        internal ConfigEntry<string> AdditionalFolders { get; }
        internal ConfigEntry<bool> PlayWhilePassingTime { get; }
        internal ConfigEntry<int> PassingTimeDrain { get; }
        internal ConfigEntry<bool> PlayThroughLoading { get; }
        internal ModKeybind TogglePower { get; }
        internal ModKeybind OpenPlayer { get; }
        internal ModKeybind PreviousTrack { get; }
        internal ModKeybind PlayPause { get; }
        internal ModKeybind NextTrack { get; }

        internal string MusicRoots =>
            string.IsNullOrWhiteSpace(AdditionalFolders.Value) ? DefaultFolder : DefaultFolder + "|" + AdditionalFolders.Value;

        internal static string CreateDefaultFolder(string modFolder, CoreLog log)
        {
            string folder = Path.Combine(modFolder, "Music");
            try { Directory.CreateDirectory(folder); }
            catch (Exception error) { log.Warn("Could not create the default Music folder: " + error.Message); }
            return folder;
        }
    }
}
