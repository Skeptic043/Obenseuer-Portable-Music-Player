using PortableMusicPlayer.Core;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class RestoreTests
    {
        private static SavedState Saved(string session, string track) => new SavedState { Session = session, TrackPath = track };

        public static void ADoorKeepsTheSongPlaying()
        {
            // A door saves, then loads the record it just wrote.
            SavedState live = Saved("a", "C:\\Music\\song.mp3");
            Assert.True(RestorePolicy.KeepsLivePlayback(live, Saved("a", "C:\\Music\\song.mp3")), "a door restarted the song");
        }

        public static void LoadingAnotherSaveReplacesPlayback()
        {
            SavedState live = Saved("a", "C:\\Music\\song.mp3");
            Assert.True(!RestorePolicy.KeepsLivePlayback(live, Saved("b", "C:\\Music\\song.mp3")), "another save kept the live song");
        }

        public static void TheMainMenuReplacesPlayback()
        {
            // The framework loads every registered record with no data when the main menu opens.
            Assert.True(!RestorePolicy.KeepsLivePlayback(Saved("a", "C:\\Music\\song.mp3"), null), "the main menu kept the live song");
        }

        public static void AnOlderSaveFromThisSessionReplacesPlayback()
        {
            // Saved earlier in this run, then reloaded from the pause menu.
            SavedState live = Saved("second", "C:\\Music\\song.mp3");
            Assert.True(!RestorePolicy.KeepsLivePlayback(live, Saved("first", "C:\\Music\\song.mp3")), "an older save kept the live song");
        }

        public static void ASavedPowerStateThatDiffersReplacesPlayback()
        {
            SavedState live = Saved("a", "C:\\Music\\song.mp3");
            SavedState saved = Saved("a", "C:\\Music\\song.mp3");
            saved.Powered = true;
            Assert.True(!RestorePolicy.KeepsLivePlayback(live, saved), "a record with a different power state kept the live song");
        }

        public static void AChangedTrackReplacesPlayback()
        {
            SavedState live = Saved("a", "C:\\Music\\one.mp3");
            Assert.True(!RestorePolicy.KeepsLivePlayback(live, Saved("a", "C:\\Music\\two.mp3")), "a different saved track kept the live song");
        }
    }
}
