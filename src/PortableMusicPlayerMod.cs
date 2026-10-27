using System;
using System.IO;
using BepInEx;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif
using ObenseuerCore;
using PortableMusicPlayer.Audio;
using PortableMusicPlayer.Core;
using PortableMusicPlayer.Items;
using PortableMusicPlayer.Library;
using PortableMusicPlayer.Playback;
using UnityEngine;

namespace PortableMusicPlayer
{
    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    [BepInDependency(ModInfo.Framework)]
    public sealed class PortableMusicPlayerMod : BaseUnityPlugin
    {
        internal static MusicPlayer Player { get; private set; }
        private CoreMod mod;
        private CoreLog log;
        private Settings settings;
        private string folder;
        private bool initialized;
        private bool loadAttempted;
        private bool refreshFailureLogged;

        private void Awake()
        {
            mod = CoreMod.For(this);
            log = mod.Log;
            folder = Path.GetDirectoryName(typeof(PortableMusicPlayerMod).Assembly.Location);
            settings = new Settings(Config, Settings.CreateDefaultFolder(folder, log));
            if (!ItemRegistry.IsAvailable)
            {
                log.Error("Portable Music Player will remain inactive because this Modding Framework build does not provide the required item registry API.");
                return;
            }
            if (!ItemRegistry.CanDefine)
            {
                log.Error("Portable Music Player will remain inactive because the item registry closed before the plugin's Awake method ran.");
                return;
            }
            if (!ItemSetup.Define(mod.Id, Path.Combine(folder, "assets"), log, out string failure))
            {
                log.Error("Portable Music Player will remain inactive because " + failure + ".");
                return;
            }
            ItemRegistry.Loaded += FinishLoad;
        }

        private void FinishLoad()
        {
            if (initialized || loadAttempted) return;
            loadAttempted = true;
            ItemRegistry.Loaded -= FinishLoad;
            MusicPlayer player = null;
            try
            {
                if (!ItemSetup.Verify(out string failure))
                {
                    log.Error("Portable Music Player will remain inactive because " + failure + ".");
                    return;
                }
                ItemSetup.Normalize();
                MediaFoundationSupport.Detect();
                LibraryPaths.MediaFoundationFormats = MediaFoundationSupport.Enabled;
                log.Info(MediaFoundationSupport.Summary + ".");

                LootSetup.Apply(mod.Id, log);
                GamePatches.Apply(mod, log);

                player = new MusicPlayer(settings, log);
                SaveData.Register<SavedState>(mod.Id, player.Capture, player.Restore);
                ItemRegistry.Loaded += RefreshItems;
                Player = player;
                initialized = true;
                log.Info(ModInfo.Name + " " + ModInfo.Version + " loaded.");
            }
            catch (Exception error)
            {
                ItemRegistry.Loaded -= RefreshItems;
                player?.Dispose();
                GamePatches.Remove();
                Player = null;
                initialized = false;
                log.Error("Portable Music Player will remain inactive because startup failed: " + error.Message);
            }
        }

        private void RefreshItems()
        {
            if (!ItemSetup.Verify(out string failure))
            {
                if (!refreshFailureLogged)
                    log.Error("Portable Music Player did not refresh its items because " + failure + ".");
                refreshFailureLogged = true;
                return;
            }
            refreshFailureLogged = false;
            ItemSetup.Normalize();
        }

        private void Update() => Player?.Tick();

        private void OnDestroy()
        {
            ItemRegistry.Loaded -= FinishLoad;
            ItemRegistry.Loaded -= RefreshItems;
            GamePatches.Remove();
            Player?.Dispose();
            Player = null;
        }
    }
}
