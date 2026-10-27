using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ObenseuerCore;
using OS.Items;
using PortableMusicPlayer.Audio;
using PortableMusicPlayer.Core;
using PortableMusicPlayer.Items;
using PortableMusicPlayer.Library;
using PortableMusicPlayer.Ui;
using UnityEngine;

namespace PortableMusicPlayer.Playback
{
    // Owns the listening session: which carried player is on, the queue, and the output.
    internal sealed class MusicPlayer : IDisposable
    {
        private const float ScanInterval = 0.25f;
        private const float HeadphoneGraceSeconds = 1.5f;
        private readonly Settings settings;
        private readonly CoreLog log;
        private readonly LibraryScanner scanner = new LibraryScanner();
        private readonly GameRadioLibrary gameRadio = new GameRadioLibrary();
        private readonly ObentunesLibrary obentunes;
        private readonly AudioOutput output = new AudioOutput();
        private readonly CarriedPlayers carried;
        private readonly ActivePlayer active = new ActivePlayer();
        private readonly System.Random random = new System.Random();
        // The game puts worn items back a moment after a load.
        private readonly AbsenceTimer headphoneAbsence = new AbsenceTimer(HeadphoneGraceSeconds);
        private readonly PlayerWindow window;
        private SavedState state = new SavedState();
        private PlaybackDecision decision;
        private float scanAt;
        private int lastSample = -1;
        // The song moved forward since the game clock last ticked.
        private bool advanced;
        private string additionalFolders;
        private Device? lastHeadphones;
        private string playbackNotice;
        private LibrarySnapshot library = LibrarySnapshot.Empty;

        internal MusicPlayer(Settings settings, CoreLog log)
        {
            this.settings = settings;
            this.log = log;
            obentunes = new ObentunesLibrary(log);
            carried = new CarriedPlayers(log);
            window = new PlayerWindow(this, log);
            additionalFolders = settings.AdditionalFolders.Value;
            scanner.RequestScan(settings.MusicRoots);
            TimeOfDayAzure.SecondsPassed += OnSecondsPassed;
        }

        internal SavedState State => state;
        internal LibrarySnapshot Library => library;
        internal bool IsScanning => scanner.IsScanning;
        internal Device? ActiveDevice => state.ActiveDevice;
        internal Device? Headphones => WornHeadphones();
        internal float ChargePercent => active.ChargePercent;
        internal string Failure => playbackNotice;
        internal bool Holding => state.Powered && decision.Output == PlaybackOutput.Hold;
        internal double PlaybackPositionSeconds => output.Path == null ? state.PositionSeconds : output.PositionSeconds;
        internal double TrackDurationSeconds => output.DurationSeconds;
        internal TrackInfo CurrentTrackInfo => Library.GetTrackInfo(state.TrackPath);

        internal void Tick()
        {
            bool libraryChanged = scanner.Poll();
            if (obentunes.Refresh(Time.unscaledTime)) libraryChanged = true;
            if (!obentunes.IsDetected && gameRadio.Refresh(Time.unscaledTime)) libraryChanged = true;
            if (libraryChanged)
            {
                LibrarySnapshot previous = library;
                library = obentunes.IsDetected ? obentunes.Merge(scanner.Snapshot) : gameRadio.Merge(scanner.Snapshot);
                ReportScan(previous);
            }
            if (additionalFolders != settings.AdditionalFolders.Value)
            {
                additionalFolders = settings.AdditionalFolders.Value;
                scanner.RequestScan(settings.MusicRoots);
            }
            HandleKeys();
            bool loading = SaveController.Loading || SaveData.Busy;
            Device? worn = WornHeadphones();
            if (worn != null) lastHeadphones = worn;
            headphoneAbsence.Observe(worn != null, Time.unscaledTime);
            if (loading) headphoneAbsence.Reset();
            if (Time.unscaledTime >= scanAt)
            {
                scanAt = Time.unscaledTime + ScanInterval;
                carried.Scan();
                if (state.Powered && state.ActiveDevice is Device device)
                    state.PlayerId = active.Follow(device, state.PlayerId, carried.Players, loading, Time.unscaledTime);
            }

            decision = PlaybackGate.Decide(Conditions(loading));
            if (state.Powered && decision.Output == PlaybackOutput.Off)
                PowerOff(active.HasCharge ? "Music player turned off because it left your inventory." :
                    "Music player turned off because its battery is empty.");
            if (state.Powered) Play(decision.Output == PlaybackOutput.Play);
            window.Refresh();
        }

        private void HandleKeys()
        {
            if (UiState.IsTyping) return;
            if (settings.TogglePower.Down()) TogglePowerFromKey();
            if (settings.OpenPlayer.Down()) OpenWindow();
            if (!state.Powered) return;
            if (settings.PreviousTrack.Down()) Previous();
            if (settings.PlayPause.Down()) TogglePause();
            if (settings.NextTrack.Down()) Next();
        }

        private void Play(bool play)
        {
            if (lastHeadphones is Device headphones) output.Shape(OutputProfile.For(headphones), state.Volume);
            if (play && output.Path == null && output.Failure == null) StartTrack();
            output.Loop = state.Repeat == RepeatMode.One;
            output.Tick(play);
            if (output.Failure != null) { StopFailedTrack(); return; }
            int sample = output.Sample;
            if (output.Advancing && lastSample >= 0 && sample != lastSample) advanced = true;
            lastSample = sample;
            if (output.Advancing) state.PositionSeconds = output.PositionSeconds;
            if (play && output.Finished)
            {
                if (output.Damage != null) log.Warn("Stopped " + state.TrackPath + " early: " + output.Damage.Message);
                FinishTrack();
            }
        }

        private PlaybackConditions Conditions(bool loading)
        {
            WaitingController waiting = WaitingController.instance;
            return new PlaybackConditions
            {
                Powered = state.Powered,
                Carried = active.Present,
                HeadphonesWorn = !headphoneAbsence.Lost(Time.unscaledTime) && lastHeadphones != null,
                HasCharge = active.HasCharge,
                Paused = state.Paused,
                // Not GameIsPaused: autosaves set that for several frames.
                MenuOpen = PauseMenu.instance != null && PauseMenu.instance.PauseIsActive(),
                PassingTime = waiting != null && (waiting.IsSleeping || waiting.IsWaiting),
                Loading = loading,
                PlayWhilePassingTime = settings.PlayWhilePassingTime.Value,
                PlayThroughLoading = settings.PlayThroughLoading.Value
            };
        }

        private void OnSecondsPassed(int seconds)
        {
            active.Spend(seconds, advanced ? decision.Battery : BatteryUse.Silent, settings.PassingTimeDrain.Value / 100d);
            advanced = false;
            if (state.Powered && !active.HasCharge) PowerOff("Music player turned off because its battery is empty.");
        }

        internal bool CanTogglePower(ItemData itemData, out string message)
        {
            message = string.Empty;
            if (!carried.TryFind(itemData, out CarriedPlayer<ItemAmmodata> player)) { message = "Must be carried"; return false; }
            if (IsOn(player)) return true;
            if (player.Charge.ammo <= 0f) { message = "Battery empty"; return false; }
            return true;
        }

        internal bool NeedsBatteryReplacement(ItemData itemData)
        {
            if (!carried.TryFind(itemData, out CarriedPlayer<ItemAmmodata> player) ||
                !Catalog.Entry(player.Device).TakesAaBatteries) return false;
            if (active.Is(player)) active.Write();
            return player.Charge.ammo <= 0f;
        }

        internal void TogglePower(ItemData itemData)
        {
            if (!carried.TryFind(itemData, out CarriedPlayer<ItemAmmodata> player)) return;
            if (IsOn(player)) PowerOff(null);
            else PowerOn(player);
        }

        internal bool CanOpen(ItemData itemData, out string message)
        {
            message = string.Empty;
            if (!carried.TryFind(itemData, out CarriedPlayer<ItemAmmodata> player)) { message = "Must be carried"; return false; }
            if (!IsOn(player)) { message = "Turn on the music player"; return false; }
            return true;
        }

        internal void OpenWindow()
        {
            if (!state.Powered) { Notify("Turn on the music player"); return; }
            if (window.IsOpen) window.Close();
            else window.Open();
        }

        // A track picked from the library. With shuffle on, a new shuffle pass starts from it.
        internal void Select(string path)
        {
            state.ShuffleOrder.Clear();
            Play(path, false);
        }

        // Picking a song plays it; moving on by itself keeps a pause the player chose.
        private void Play(string path, bool automatic)
        {
            state.TrackPath = path;
            state.PositionSeconds = 0d;
            if (!automatic) Resume();
            output.Stop();
        }

        private void Resume()
        {
            state.Paused = false;
            playbackNotice = null;
        }

        internal void TogglePause()
        {
            state.Paused = !state.Paused;
            if (!state.Paused) playbackNotice = null;
        }

        internal void SetShuffle(bool shuffle)
        {
            // A new pass starts from the current track.
            if (state.Shuffle != shuffle) state.ShuffleOrder.Clear();
            state.Shuffle = shuffle;
        }

        internal void CycleRepeat()
        {
            state.Repeat = state.Repeat == RepeatMode.Off ? RepeatMode.All :
                state.Repeat == RepeatMode.All ? RepeatMode.One : RepeatMode.Off;
        }

        internal void SetVolume(float volume) => state.Volume = Mathf.Clamp01(volume);

        private void FinishTrack()
        {
            // Only a clip that began mid-track ends under Repeat One; every later pass loops in AudioOutput.
            // A damaged song moves on instead, since replaying it would only stop at the damage again.
            if (state.Repeat == RepeatMode.One && output.Damage == null) { RestartTrack(); return; }
            string next = Following(state.Repeat == RepeatMode.All);
            if (next != null) { Play(next, true); return; }
            state.PositionSeconds = output.PositionSeconds;
            output.Stop();
            state.Paused = true;
            playbackNotice = "Queue finished";
        }

        // Repeat One keeps Next on the current song.
        internal void Next()
        {
            if (state.Repeat == RepeatMode.One && !string.IsNullOrWhiteSpace(state.TrackPath))
            {
                RestartTrack();
                Resume();
                return;
            }
            string next = Following(true);
            if (next != null) Play(next, false);
            else Notify("No music found. Put songs in the player's Music folder.");
        }

        // At the start of a shuffle pass, Previous restarts the current song. The sequential queue wraps.
        internal void Previous()
        {
            List<string> queue = Queue();
            string previous;
            if (state.Shuffle)
            {
                ShuffleDeck.Reconcile(state.ShuffleOrder, queue, state.TrackPath, random);
                previous = ShuffleDeck.Previous(state.ShuffleOrder, state.TrackPath);
            }
            else
            {
                int index = QueuePlanner.Previous(queue.Count, IndexOf(queue));
                previous = index < 0 ? null : queue[index];
            }
            if (previous != null) Play(previous, false);
            else if (!string.IsNullOrWhiteSpace(state.TrackPath))
            {
                RestartTrack();
                Resume();
            }
        }

        // The track after the current one in play order, or null when the queue ends without wrapping.
        private string Following(bool wrap)
        {
            List<string> queue = Queue();
            if (!state.Shuffle)
            {
                int index = QueuePlanner.Next(queue.Count, IndexOf(queue), wrap);
                return index < 0 ? null : queue[index];
            }
            ShuffleDeck.Reconcile(state.ShuffleOrder, queue, state.TrackPath, random);
            return ShuffleDeck.Next(state.ShuffleOrder, state.TrackPath, wrap, random);
        }

        private void RestartTrack()
        {
            state.PositionSeconds = 0d;
            output.Restart();
        }

        internal void PrepareBatteryReplacement(ItemData itemData)
        {
            if (carried.TryFind(itemData, out CarriedPlayer<ItemAmmodata> player) && active.Is(player)) active.Write();
        }

        internal void FinishBatteryReplacement(ItemData itemData)
        {
            if (carried.TryFind(itemData, out CarriedPlayer<ItemAmmodata> player)) active.Refresh(player);
        }

        internal SavedState Capture()
        {
            active.Write();
            if (output.Path != null) state.PositionSeconds = output.PositionSeconds;
            // A door loads this record straight back; any older record replaces live playback.
            state.Session = Guid.NewGuid().ToString("N");
            return state;
        }

        internal void Restore(SavedState saved)
        {
            if (RestorePolicy.KeepsLivePlayback(state, saved)) return;
            output.Stop();
            window.Close();
            state = saved ?? new SavedState();
            playbackNotice = null;
            state.Normalize();
            if (state.Powered) active.Expect(state.PlayerId);
            else active.Release();
        }

        private void PowerOn(CarriedPlayer<ItemAmmodata> player)
        {
            if (state.Powered) PowerOff(null);
            active.Bind(player);
            state.ActiveDevice = player.Device;
            state.PlayerId = player.Identity;
            state.Powered = true;
            state.Paused = false;
            playbackNotice = null;
            // Songs added or removed while the game runs show up the next time a player turns on.
            scanner.RequestScan(settings.MusicRoots);
            if (Headphones == null) Notify(Catalog.Entry(player.Device).Title + " is on. Wear headphones to listen.");
        }

        private void PowerOff(string reason)
        {
            active.Release();
            if (output.Path != null) state.PositionSeconds = output.PositionSeconds;
            output.Stop();
            window.Close();
            state.Powered = false;
            playbackNotice = null;
            if (reason != null) Notify(reason);
        }

        private void TogglePowerFromKey()
        {
            if (state.Powered) { PowerOff(null); return; }
            CarriedPlayer<ItemAmmodata> player = carried.Preferred(state.ActiveDevice);
            if (player == null) Notify("You are not carrying a music player.");
            else if (player.Charge.ammo <= 0f) Notify(Catalog.Entry(player.Device).Title + ": battery empty.");
            else PowerOn(player);
        }

        private void StartTrack()
        {
            if (string.IsNullOrWhiteSpace(state.TrackPath)) state.TrackPath = Following(true) ?? "";
            if (string.IsNullOrWhiteSpace(state.TrackPath)) return;
            if (obentunes.IsDetected && GameRadioLibrary.IsPath(state.TrackPath))
            {
                if (!obentunes.IsReady) return;
                if (obentunes.TryMapGameRadioPath(state.TrackPath, out string replacement)) state.TrackPath = replacement;
                else { ReplaceMissingTrack(); return; }
            }
            if (ObentunesLibrary.IsPath(state.TrackPath))
            {
                if (!obentunes.IsDetected)
                {
                    // Native Radio objects expose their clips only after the owning scene arrives.
                    // Preserve this restored selection until there is a real catalog to map against.
                    if (!gameRadio.IsReady) return;
                    string fallback = GameRadioLibrary.PathFor(ObentunesLibrary.NameFromPath(state.TrackPath));
                    if (gameRadio.TryGetClip(fallback, out AudioClip fallbackClip))
                    {
                        state.TrackPath = fallback;
                        output.Start(fallbackClip, fallback, state.PositionSeconds);
                        lastSample = -1;
                        return;
                    }
                    ReplaceMissingTrack();
                    return;
                }
                if (!obentunes.IsReady) return;
                Func<CancellationToken, IPcmDecoder> open = obentunes.Opener(state.TrackPath);
                if (open == null) { ReplaceMissingTrack(); return; }
                output.Start(state.TrackPath, open, state.PositionSeconds);
                lastSample = -1;
                return;
            }
            if (gameRadio.TryGetClip(state.TrackPath, out AudioClip gameClip))
            {
                output.Start(gameClip, state.TrackPath, state.PositionSeconds);
                lastSample = -1;
                return;
            }
            // Radio assets arrive with their owning scene. Keep a restored selection intact until
            // that scene's native Radio objects have exposed their clips.
            if (GameRadioLibrary.IsPath(state.TrackPath))
            {
                lastSample = -1;
                return;
            }
            try { output.Start(PathRules.NormalizeLocalDrivePath(state.TrackPath), state.PositionSeconds); }
            catch (ArgumentException error) { log.Warn("Cannot play " + state.TrackPath + ": " + error.Message); state.TrackPath = ""; }
            lastSample = -1;
        }

        // A restored track that is no longer in the library gives way to the start of the queue, or to a new
        // shuffle pass.
        private void ReplaceMissingTrack()
        {
            state.TrackPath = "";
            state.PositionSeconds = 0d;
            state.TrackPath = Following(true) ?? "";
        }

        // A failed decoder or integration never chooses another track for the player. Keep the device
        // and window available, pause here, and leave recovery to an explicit selection.
        private void StopFailedTrack()
        {
            log.Warn("Cannot play " + state.TrackPath + ": " + output.Failure);
            const string message = "This song could not be loaded.";
            output.Stop();
            state.PositionSeconds = 0d;
            state.Paused = true;
            playbackNotice = message;
            Notify(message);
        }

        private bool IsOn(CarriedPlayer<ItemAmmodata> player) => state.Powered && active.Is(player);

        private static Device? WornHeadphones()
        {
            int worn = Wardrobe.WornOn("Head");
            return worn >= 0 && ItemSetup.TryDevice(worn, out Device device) && !Catalog.Entry(device).IsPlayer ? device : (Device?)null;
        }

        // The collection that holds the current track, or the first one.
        private List<string> Queue()
        {
            MusicCollection holder = Library.Collections.FirstOrDefault(collection => collection.Tracks.Any(track =>
                LibraryPaths.Comparer.Equals(track.Path, state.TrackPath))) ?? Library.Collections.FirstOrDefault();
            return holder == null ? new List<string>() : holder.Tracks.Select(track => track.Path).ToList();
        }

        private int IndexOf(List<string> queue) => queue.FindIndex(path => LibraryPaths.Comparer.Equals(path, state.TrackPath));

        private void ReportScan(LibrarySnapshot previous)
        {
            int tracks = 0;
            foreach (MusicCollection collection in Library.Collections) tracks += collection.Tracks.Count;
            log.Info("Music scan found " + tracks + " track(s) in " + Library.Collections.Count + " collection(s).");
            foreach (string warning in Library.WarningsSince(previous)) log.Warn(warning);
        }

        private static void Notify(string text) => Hud.Notify("Music", text);

        public void Dispose()
        {
            TimeOfDayAzure.SecondsPassed -= OnSecondsPassed;
            active.Write();
            window.Close();
            output.Dispose();
            scanner.Dispose();
        }
    }
}
