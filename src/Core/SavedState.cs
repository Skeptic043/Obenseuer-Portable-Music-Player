using System;
using System.Collections.Generic;

namespace PortableMusicPlayer.Core
{
    public enum RepeatMode { Off, All, One }

    // The per-save record. Public fields are what the framework writes. Charge is not here: each
    // player's battery stays in its own item data.
    [Serializable]
    public sealed class SavedState
    {
        public string Device = "";
        public string PlayerId = "";
        public bool Powered;
        public string TrackPath = "";
        public double PositionSeconds;
        public float Volume = 0.65f;
        public bool Shuffle;
        public RepeatMode Repeat;
        public bool Paused;
        // The shuffle pass in play order; see ShuffleDeck.
        public List<string> ShuffleOrder = new List<string>();
        // Which live session wrote this record. A door reloads the record it just saved.
        public string Session = "";

        internal Device? ActiveDevice
        {
            get => Enum.TryParse(Device, out Device parsed) && Enum.IsDefined(typeof(Device), parsed) ? parsed : (Device?)null;
            set => Device = value?.ToString() ?? "";
        }

        internal void Normalize()
        {
            PlayerId = PlayerId ?? "";
            if (ActiveDevice == null || Powered && PlayerId.Length == 0) { Device = ""; PlayerId = ""; Powered = false; }
            TrackPath = TrackPath ?? "";
            ShuffleOrder = ShuffleOrder ?? new List<string>();
            ShuffleOrder.RemoveAll(string.IsNullOrWhiteSpace);
            if (!Enum.IsDefined(typeof(RepeatMode), Repeat)) Repeat = RepeatMode.Off;
            if (double.IsNaN(PositionSeconds) || double.IsInfinity(PositionSeconds) || PositionSeconds < 0d) PositionSeconds = 0d;
            Volume = float.IsNaN(Volume) ? 0.65f : Math.Max(0f, Math.Min(1f, Volume));
        }
    }
}
