namespace PortableMusicPlayer.Core
{
    internal static class RestorePolicy
    {
        // A door saves and then loads the record it just wrote. Restarting then would reset the song, so the
        // record from the latest save, unchanged since, keeps live playback. Anything else (an older save, another
        // game, the main menu) replaces it. Session is renewed at every save.
        internal static bool KeepsLivePlayback(SavedState live, SavedState saved) =>
            live != null && saved != null && !string.IsNullOrEmpty(saved.Session) && saved.Session == live.Session &&
            saved.Powered == live.Powered &&
            string.Equals(saved.TrackPath ?? "", live.TrackPath ?? "", System.StringComparison.OrdinalIgnoreCase);
    }
}
