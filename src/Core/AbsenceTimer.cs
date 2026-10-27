namespace PortableMusicPlayer.Core
{
    // Counts a carried player as gone only after it has been missing for a while. The game refills hand
    // and head slots a moment after a load, and dragging a stack lifts it out of its slot.
    internal sealed class AbsenceTimer
    {
        private readonly float graceSeconds;
        private float missingSince = -1f;

        internal AbsenceTimer(float graceSeconds) { this.graceSeconds = graceSeconds; }

        internal void Observe(bool present, float now)
        {
            if (present) missingSince = -1f;
            else if (missingSince < 0f) missingSince = now;
        }

        internal bool Lost(float now) => missingSince >= 0f && now - missingSince >= graceSeconds;

        internal void Reset() => missingSince = -1f;
    }
}
