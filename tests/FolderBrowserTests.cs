using System.IO;
using System.Linq;
using PortableMusicPlayer.Library;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class FolderBrowserTests
    {
        private static readonly string Root = Path.Combine("C:\\", "Music");

        public static void TheCollectionRootShowsImmediateFoldersAndTracksOnly()
        {
            MusicCollection collection = Collection();
            FolderView root = FolderBrowser.Open(collection, "");
            Assert.Equal("Jazz|Rock", string.Join("|", root.Folders.Select(folder => folder.Name)), "root folders");
            Assert.Equal("Loose song", string.Join("|", root.Tracks.Select(track => track.Info.Title)), "root tracks");
        }

        public static void AFolderShowsItsDirectTracksAndImmediateChildren()
        {
            FolderView rock = FolderBrowser.Open(Collection(), "Rock");
            Assert.Equal("Live", string.Join("|", rock.Folders.Select(folder => folder.Name)), "Rock child folders");
            Assert.Equal("Studio song", string.Join("|", rock.Tracks.Select(track => track.Info.Title)), "Rock tracks");
            Assert.Equal("", FolderBrowser.Parent("Rock"), "Rock parent");
            Assert.Equal("Rock", FolderBrowser.Parent(Path.Combine("Rock", "Live")), "Live parent");
        }

        public static void TheCurrentTrackOpensItsContainingFolder()
        {
            MusicCollection collection = Collection();
            string track = Path.Combine(Root, "Rock", "Live", "Concert.mp3");
            Assert.Equal(Path.Combine("Rock", "Live"), FolderBrowser.RelativeDirectory(collection, track),
                "current track folder");
        }

        public static void AStaleFolderFallsBackToTheCollectionRoot()
        {
            FolderView view = FolderBrowser.Open(Collection(), Path.Combine("Missing", "Folder"));
            Assert.Equal("", view.RelativePath, "stale-folder fallback");
            Assert.Equal(2, view.Folders.Count, "root folder count after fallback");
        }

        private static MusicCollection Collection() => new MusicCollection(Root, "Music", new[]
        {
            Track(Path.Combine(Root, "Loose.mp3"), "Loose song"),
            Track(Path.Combine(Root, "Jazz", "Blue.mp3"), "Blue song"),
            Track(Path.Combine(Root, "Rock", "Studio.mp3"), "Studio song"),
            Track(Path.Combine(Root, "Rock", "Live", "Concert.mp3"), "Concert song")
        });

        private static MusicTrack Track(string path, string title) => new MusicTrack(path, new TrackInfo(path, title));
    }
}
