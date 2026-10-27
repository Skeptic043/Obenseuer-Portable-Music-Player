using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ObenseuerCore;
using ObenseuerCore.Ui;
using PortableMusicPlayer.Core;
using PortableMusicPlayer.Library;
using PortableMusicPlayer.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PortableMusicPlayer.Ui
{
    // The player as a handheld device: a reflective LCD that shows Now Playing or the library, and hardware keys
    // below it. A non-modal overlay: it shows the cursor but adds no gameplay-input lock, so movement and the
    // configured music controls keep working while it is open.
    internal sealed class PlayerWindow
    {
        private enum Page { NowPlaying, Library }

        private const int TracksPerPage = 100;
        private const int VolumeCells = 12;
        private const float VolumeStep = 0.05f;
        private const float KeyPixel = 3f;
        private const float LcdPixel = 2f;
        private const float RowHeight = 28f;
        private static readonly Vector2 BodySize = new Vector2(360f, 580f);
        private static readonly Rect LcdArea = new Rect(26f, 50f, 308f, 300f);
        private const float ProgressWidth = 272f;

        private static readonly Color LcdBack = new Color(0.47f, 0.52f, 0.39f, 1f);
        private static readonly Color Ink = new Color(0.08f, 0.11f, 0.07f, 1f);
        private static readonly Color InkSoft = new Color(0.08f, 0.11f, 0.07f, 0.85f);
        // Unlit LCD segments stay faintly visible, as on a real reflective screen.
        private static readonly Color Ghost = new Color(0.08f, 0.11f, 0.07f, 0.14f);
        private static readonly Color Bezel = new Color(0.055f, 0.055f, 0.05f, 1f);
        private static readonly Color KeyFace = new Color(0.12f, 0.115f, 0.105f, 1f);
        private static readonly Color KeyHover = new Color(0.18f, 0.17f, 0.155f, 1f);
        private static readonly Color KeyPressed = new Color(0.07f, 0.07f, 0.065f, 1f);
        private static readonly Color KeyShadow = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color KeyGlyph = new Color(0.80f, 0.77f, 0.69f, 1f);
        private static readonly Color KeyLit = new Color(0.95f, 0.66f, 0.26f, 1f);
        // Cream silkscreen paint, readable on either case colour.
        private static readonly Color Print = new Color(0.93f, 0.90f, 0.80f, 1f);
        private const string HudFontName = "Erika Ormig SDF Menu";
        private const string HudMaterialName = "Erika Ormig SDF Default";
        private static TMP_FontAsset hudFont;
        private static Material hudMaterial;

        private readonly MusicPlayer player;
        private readonly CoreLog log;
        private GameObject root, ownedEventSystem, nowPlayingPage, libraryPage;
        private RectTransform body, rows, progressFill;
        private Scene home;
        private TextMeshProUGUI title, artist, album, notice, elapsed, remaining, battery, path, pageLabel;
        private Image stateIcon, shuffleIcon, repeatIcon, headphoneIcon, playKey, shuffleKey, repeatKey;
        private RectTransform currentRow;
        private Image[] batteryCase;
        private Image[] batteryCells, volumeCells;
        private GameObject back, pageControls;
        private ScrollRect list;
        private LibrarySnapshot listedLibrary;
        private string listedTrack;
        private bool listDirty, scrollToCurrent;
        private Page page = Page.NowPlaying;
        private bool atRoot;
        private int collection, trackPage;
        private string folder = "";
        private bool cursorShown;
        private float windowScale = 1f;

        internal static bool BlocksViewRotation { get; private set; }
        internal static bool IsAnyOpen => active != null && active.IsOpen;
        private static PlayerWindow active;

        internal PlayerWindow(MusicPlayer player, CoreLog log)
        {
            this.player = player;
            this.log = log;
        }

        internal bool IsOpen => root != null;

        internal void Open()
        {
            if (root != null) return;
            try
            {
                EnsureEventSystem();
                root = new GameObject("PortableMusicPlayer.Overlay", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                home = root.scene;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                if (!FindHudFont())
                    log.Error("Music player could not find the game's " + HudFontName + " font with its " + HudMaterialName + " material.");
                body = NewRect("Player body", root.transform);
                body.anchorMin = body.anchorMax = body.pivot = new Vector2(0f, 1f);
                body.anchoredPosition = new Vector2(24f, -24f);
                body.sizeDelta = BodySize;
                body.localScale = new Vector3(windowScale, windowScale, 1f);
                BuildCase();
                BuildScreen();
                BuildKeys();
                AddScaleGrip();
                listedLibrary = null;
                listedTrack = null;
                ShowPage(page);
                SaveController.PlayerWillLoadGame += OnSaveLoad;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                ShowCursor();
                BlocksViewRotation = true;
                active = this;
            }
            catch (Exception error)
            {
                log.Error("Music player overlay failed to open: " + error.GetType().Name + ": " + error.Message);
                Close();
            }
        }

        // ------------------------------------------------------------------------------------------ the case

        private void BuildCase()
        {
            Color shell = ShellColor(player.ActiveDevice);
            Image face = Surface(body, shell, true);
            face.gameObject.name = "Player shell";
            Grain(body);
            // A moulded edge: lit from above, shadowed below.
            Block("Edge light", body, Shade(shell, 1.45f), 0f, 0f, BodySize.x, 3f);
            Block("Edge light", body, Shade(shell, 1.25f), 0f, 0f, 3f, BodySize.y);
            Block("Edge shadow", body, Shade(shell, 0.55f), 0f, BodySize.y - 4f, BodySize.x, 4f);
            Block("Edge shadow", body, Shade(shell, 0.7f), BodySize.x - 3f, 0f, 3f, BodySize.y);
            // The scale grip takes the lower-right corner.
            foreach (Vector2 screw in new[] { new Vector2(10f, 10f), new Vector2(BodySize.x - 18f, 10f),
                         new Vector2(10f, BodySize.y - 20f) })
            {
                Block("Screw", body, Shade(shell, 0.45f), screw.x, screw.y, 8f, 8f);
                Block("Screw slot", body, Shade(shell, 1.3f), screw.x + 2f, screw.y + 3f, 4f, 2f);
            }

            string name = player.ActiveDevice is Device device ? Catalog.Entry(device).Title : "Music player";
            TextMeshProUGUI badge = Text(body, name.ToUpperInvariant(), 13f, Print, TextAlignmentOptions.Left,
                26f, 12f, 260f, 22f);
            badge.characterSpacing = 6f;
            Key(body, "Close", BodySize.x - 62f, 9f, 36f, 26f, PixelIcons.Close, 2f, Close);

            // The bezel holds the LCD; a thin inner shadow sets the glass back into it.
            Block("Bezel", body, Bezel, LcdArea.x - 8f, LcdArea.y - 8f, LcdArea.width + 16f, LcdArea.height + 16f);
            Block("Bezel lip", body, Shade(shell, 0.4f), LcdArea.x - 10f, LcdArea.y - 10f, LcdArea.width + 20f, 2f);
        }

        // ---------------------------------------------------------------------------------------- the screen

        private void BuildScreen()
        {
            RectTransform lcd = NewRect("LCD", body);
            Place(lcd, LcdArea.x, LcdArea.y, LcdArea.width, LcdArea.height);
            Surface(lcd, LcdBack, false);
            float width = LcdArea.width;

            // Status line: play state, shuffle and repeat on the left; headphones and battery on the right.
            stateIcon = Glyph(lcd, PixelIcons.Play, LcdPixel, Ink, 10f, 9f);
            shuffleIcon = Glyph(lcd, PixelIcons.Shuffle, LcdPixel, Ghost, 30f, 9f);
            repeatIcon = Glyph(lcd, PixelIcons.Repeat, LcdPixel, Ghost, 64f, 7f);
            float batteryX = width - 46f;
            batteryCase = Outline(lcd, "Battery", Ink, batteryX, 11f, 30f, 15f, 2f);
            Block("Battery tip", lcd, Ink, batteryX + 30f, 15f, 3f, 7f);
            batteryCells = new Image[4];
            for (int i = 0; i < batteryCells.Length; i++)
                batteryCells[i] = Block("Battery cell", lcd, Ink, batteryX + 4f + i * 6f, 15f, 4f, 7f);
            battery = Text(lcd, "", 17f, Ink, TextAlignmentOptions.Right, batteryX - 58f, 6f, 54f, 24f);
            headphoneIcon = Glyph(lcd, PixelIcons.Headphones, LcdPixel, Ink, batteryX - 76f, 11f);
            Block("Divider", lcd, Ink, 8f, 34f, width - 16f, 2f);

            nowPlayingPage = NewRect("Now Playing", lcd).gameObject;
            Place((RectTransform)nowPlayingPage.transform, 0f, 40f, width, LcdArea.height - 40f);
            BuildNowPlaying((RectTransform)nowPlayingPage.transform, width);
            libraryPage = NewRect("Library", lcd).gameObject;
            Place((RectTransform)libraryPage.transform, 0f, 40f, width, LcdArea.height - 40f);
            BuildLibrary((RectTransform)libraryPage.transform, width);

            // A faint dot-matrix grid over everything, then glare along the top of the glass.
            PixelGrid(lcd);
            Block("Glass shadow", lcd, new Color(0f, 0f, 0f, 0.12f), 0f, 0f, width, 3f);
        }

        private void BuildNowPlaying(RectTransform parent, float width)
        {
            float inner = width - 24f;
            title = Text(parent, "", 25f, Ink, TextAlignmentOptions.Center, 12f, 6f, inner, 66f, true);
            title.fontStyle = FontStyles.Bold;
            artist = Text(parent, "", 19f, Ink, TextAlignmentOptions.Center, 12f, 74f, inner, 26f);
            album = Text(parent, "", 18f, InkSoft, TextAlignmentOptions.Center, 12f, 100f, inner, 26f);
            notice = Text(parent, "", 16f, Ink, TextAlignmentOptions.Center, 12f, 130f, inner, 24f);
            notice.characterSpacing = 3f;

            float barX = (width - ProgressWidth) / 2f - 4f;
            Outline(parent, "Progress", Ink, barX, 164f, ProgressWidth + 8f, 14f, 2f);
            progressFill = Block("Progress fill", parent, Ink, barX + 4f, 168f, 0f, 6f).rectTransform;
            elapsed = Text(parent, "0:00", 19f, Ink, TextAlignmentOptions.Left, barX, 182f, 120f, 26f);
            remaining = Text(parent, "", 19f, Ink, TextAlignmentOptions.Right, barX + ProgressWidth + 8f - 120f, 182f, 120f, 26f);

            TextMeshProUGUI volume = Text(parent, "VOL", 16f, Ink, TextAlignmentOptions.Left, barX, 218f, 44f, 22f);
            volume.characterSpacing = 2f;
            volumeCells = new Image[VolumeCells];
            for (int i = 0; i < VolumeCells; i++)
            {
                // Rising bars, bottom-aligned like a level meter.
                float height = 5f + i;
                volumeCells[i] = Block("Volume cell", parent, Ghost, barX + 44f + i * 19f, 236f - height, 14f, height);
            }
        }

        private void BuildLibrary(RectTransform parent, float width)
        {
            back = LcdButton(parent, "Back", PixelIcons.Back, 6f, 0f, 28f, 30f, Back).gameObject;
            path = Text(parent, "", 16f, Ink, TextAlignmentOptions.Left, 38f, 3f, width - 116f, 26f);
            path.characterSpacing = 2f;
            pageControls = NewRect("Pages", parent).gameObject;
            Place((RectTransform)pageControls.transform, width - 78f, 0f, 74f, 30f);
            LcdButton(pageControls.transform, "Previous page", PixelIcons.Back, 0f, 0f, 20f, 30f, () => ChangePage(-1));
            pageLabel = Text(pageControls.transform, "", 15f, Ink, TextAlignmentOptions.Center, 20f, 3f, 34f, 26f);
            LcdButton(pageControls.transform, "Next page", PixelIcons.Forward, 54f, 0f, 20f, 30f, () => ChangePage(1));
            Block("Divider", parent, Ink, 8f, 32f, width - 16f, 1f);

            RectTransform frame = NewRect("Track list", parent);
            Place(frame, 4f, 36f, width - 8f, LcdArea.height - 40f - 40f);
            list = frame.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = NewRect("Viewport", frame);
            ModUi.Place(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            // Wheel and drag events need something to hit between rows.
            Surface(viewport, Color.clear, true);
            rows = NewRect("Rows", viewport);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(0.5f, 1f);
            rows.offsetMin = rows.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 1f;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform track = NewRect("Scrollbar", frame);
            ModUi.Place(track, new Vector2(1f, 0f), Vector2.one, new Vector2(-6f, 2f), new Vector2(0f, -2f));
            Surface(track, Ghost, true);
            RectTransform slide = NewRect("Sliding area", track);
            ModUi.Place(slide, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform handle = NewRect("Handle", slide);
            ModUi.Place(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = Surface(handle, Ink, true);
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };

            list.viewport = viewport;
            list.content = rows;
            list.horizontal = false;
            list.movementType = ScrollRect.MovementType.Clamped;
            list.inertia = false;
            list.scrollSensitivity = RowHeight;
            list.verticalScrollbar = bar;
            list.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // ------------------------------------------------------------------------------------------ the keys

        private void BuildKeys()
        {
            Color shell = ShellColor(player.ActiveDevice);
            const float top = 372f;
            playKey = Key(body, "Play", 125f, top, 110f, 68f, PixelIcons.Pause, KeyPixel, player.TogglePause);
            Key(body, "Previous", 23f, top, 92f, 68f, PixelIcons.Previous, KeyPixel, player.Previous);
            Key(body, "Next", 245f, top, 92f, 68f, PixelIcons.Next, KeyPixel, player.Next);

            const float row = 460f;
            Key(body, "Menu", 24f, row, 56f, 42f, PixelIcons.Menu, 2f, () => ShowPage(page == Page.Library ? Page.NowPlaying : Page.Library));
            shuffleKey = Key(body, "Shuffle", 88f, row, 56f, 42f, PixelIcons.Shuffle, 2f, () => player.SetShuffle(!player.State.Shuffle));
            repeatKey = Key(body, "Repeat", 152f, row, 56f, 42f, PixelIcons.Repeat, 2f, player.CycleRepeat);
            Key(body, "Volume down", 216f, row, 56f, 42f, PixelIcons.VolumeDown, 2f, () => player.SetVolume(player.State.Volume - VolumeStep));
            Key(body, "Volume up", 280f, row, 56f, 42f, PixelIcons.VolumeUp, 2f, () => player.SetVolume(player.State.Volume + VolumeStep));

            // Speaker slots along the bottom of the case.
            for (int i = 0; i < 4; i++)
            {
                Block("Grille", body, Shade(shell, 0.5f), 120f, 532f + i * 9f, 120f, 3f);
                Block("Grille", body, Shade(shell, 1.3f), 120f, 535f + i * 9f, 120f, 1f);
            }
        }

        // A hardware key with a drop shadow; its glyph Image is returned so the window can change it.
        private Image Key(Transform parent, string name, float x, float y, float width, float height, string[] glyph,
            float pixel, Action onClick)
        {
            Block(name + " shadow", parent, KeyShadow, x, y + 3f, width, height);
            Image face = Block(name, parent, Color.white, x, y, width, height, true);
            Block(name + " rim", face.transform, new Color(1f, 1f, 1f, 0.07f), 0f, 0f, width, 2f);
            UnityEngine.UI.Button button = face.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            button.colors = Tints(KeyFace, KeyHover, KeyPressed);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick());
            Image icon = Glyph(face.transform, glyph, pixel, KeyGlyph);
            return icon;
        }

        // An invisible LCD hot spot whose dark ink appears on hover.
        private UnityEngine.UI.Button LcdButton(Transform parent, string name, string[] glyph, float x, float y,
            float width, float height, Action onClick)
        {
            Image face = Block(name, parent, Ink, x, y, width, height, true);
            UnityEngine.UI.Button button = face.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            button.colors = Tints(Color.clear, new Color(1f, 1f, 1f, 0.16f), new Color(1f, 1f, 1f, 0.3f));
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick());
            Glyph(face.transform, glyph, LcdPixel, Ink);
            return button;
        }

        private void AddScaleGrip()
        {
            RectTransform handle = NewRect("Resize grip", body);
            handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(1f, 0f);
            handle.anchoredPosition = new Vector2(-4f, 6f);
            handle.sizeDelta = new Vector2(30f, 30f);
            Surface(handle, Color.clear, true);
            Color dots = Shade(ShellColor(player.ActiveDevice), 0.5f);
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < ResizeGripLayout.DotsInRow(row); column++)
            {
                RectTransform dot = NewRect("Grip dot", handle);
                dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(1f, 0f);
                dot.anchoredPosition = new Vector2(-5f - column * 6f, 5f + row * 6f);
                dot.sizeDelta = new Vector2(3f, 3f);
                Surface(dot, dots, false);
            }
            handle.gameObject.AddComponent<ResizeHandle>().Initialize(body, scale => windowScale = scale);
        }

        // ------------------------------------------------------------------------------------------- refresh

        private void ShowPage(Page next)
        {
            page = next;
            if (nowPlayingPage != null) nowPlayingPage.SetActive(next == Page.NowPlaying);
            if (libraryPage != null) libraryPage.SetActive(next == Page.Library);
            if (next == Page.Library) scrollToCurrent = true;
        }

        internal void Close()
        {
            SaveController.PlayerWillLoadGame -= OnSaveLoad;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (root != null) UnityEngine.Object.Destroy(root);
            if (ownedEventSystem != null) UnityEngine.Object.Destroy(ownedEventSystem);
            root = null;
            ownedEventSystem = null;
            listedLibrary = null;
            body = null;
            BlocksViewRotation = false;
            if (active == this) active = null;
            RestoreCursor();
        }

        internal static bool TryCloseFromCancel()
        {
            if (!IsAnyOpen) return false;
            active.Close();
            return true;
        }

        internal void Refresh()
        {
            if (root == null) return;
            try
            {
                ShowCursor();
                // Real LCD blink rate: on a little longer than off.
                bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
                RefreshStatus(blink);
                RefreshKeys();
                if (page == Page.NowPlaying) RefreshNowPlaying(blink);
                RefreshLibrary();
            }
            catch (Exception error)
            {
                log.Error("Music player overlay faulted and closed: " + error.GetType().Name + ": " + error.Message);
                Close();
            }
        }

        private void RefreshStatus(bool blink)
        {
            SavedState state = player.State;
            string[] stateGlyph = state.Paused ? PixelIcons.Pause : PixelIcons.Play;
            stateIcon.sprite = PixelIcons.Sprite(stateGlyph);
            stateIcon.rectTransform.sizeDelta = PixelIcons.Size(stateGlyph, LcdPixel);
            shuffleIcon.color = state.Shuffle ? Ink : Ghost;
            repeatIcon.sprite = PixelIcons.Sprite(state.Repeat == RepeatMode.One ? PixelIcons.RepeatOne : PixelIcons.Repeat);
            repeatIcon.color = state.Repeat == RepeatMode.Off ? Ghost : Ink;
            headphoneIcon.color = player.Headphones != null || blink ? Ink : Ghost;

            float charge = Mathf.Clamp(player.ChargePercent, 0f, 100f);
            int lit = Mathf.CeilToInt(charge / 25f);
            for (int i = 0; i < batteryCells.Length; i++) batteryCells[i].color = i < lit ? Ink : Ghost;
            Tint(batteryCase, charge > 10f || blink ? Ink : Ghost);
            battery.text = Mathf.RoundToInt(charge) + "%";
        }

        private void RefreshKeys()
        {
            SavedState state = player.State;
            string[] playGlyph = state.Paused ? PixelIcons.Play : PixelIcons.Pause;
            playKey.sprite = PixelIcons.Sprite(playGlyph);
            playKey.rectTransform.sizeDelta = PixelIcons.Size(playGlyph, KeyPixel);
            shuffleKey.color = state.Shuffle ? KeyLit : KeyGlyph;
            string[] repeatGlyph = state.Repeat == RepeatMode.One ? PixelIcons.RepeatOne : PixelIcons.Repeat;
            repeatKey.sprite = PixelIcons.Sprite(repeatGlyph);
            repeatKey.color = state.Repeat == RepeatMode.Off ? KeyGlyph : KeyLit;
        }

        private void RefreshNowPlaying(bool blink)
        {
            bool hasTrack = !string.IsNullOrWhiteSpace(player.State.TrackPath);
            TrackInfo track = player.CurrentTrackInfo;
            title.text = hasTrack && !string.IsNullOrWhiteSpace(track.Title) ? track.Title : "No track";
            artist.text = !hasTrack ? "" : string.IsNullOrWhiteSpace(track.Artist) ? "Unknown artist" : track.Artist;
            artist.color = hasTrack && string.IsNullOrWhiteSpace(track.Artist) ? InkSoft : Ink;
            album.text = hasTrack ? track.Album ?? "" : "Press MENU to choose a song";
            notice.text = Notice();

            double position = player.PlaybackPositionSeconds;
            double duration = player.TrackDurationSeconds;
            float done = duration > 0d ? Mathf.Clamp01((float)(position / duration)) : 0f;
            progressFill.sizeDelta = new Vector2(Mathf.Round(ProgressWidth * done), progressFill.sizeDelta.y);
            // Both clocks count whole seconds of the same position, so they change on the same frame.
            int seconds = Math.Max(0, (int)position), length = Math.Max(0, (int)duration);
            elapsed.text = FormatTime(seconds);
            // A paused clock blinks, as hardware players do.
            elapsed.color = player.State.Paused && hasTrack && !blink ? Ghost : Ink;
            remaining.text = duration > 0d ? "-" + FormatTime(Math.Max(0, length - seconds)) : "";

            int lit = Mathf.RoundToInt(player.State.Volume * VolumeCells);
            for (int i = 0; i < volumeCells.Length; i++) volumeCells[i].color = i < lit ? Ink : Ghost;
        }

        private string Notice()
        {
            if (!string.IsNullOrEmpty(player.Failure)) return player.Failure.TrimEnd('.').ToUpperInvariant();
            if (player.Headphones == null) return "WEAR HEADPHONES";
            if (player.State.Paused) return "PAUSED";
            return player.Holding ? "WAITING" : "";
        }

        // ------------------------------------------------------------------------------------------- library

        private void RefreshLibrary()
        {
            if (!ReferenceEquals(listedLibrary, player.Library))
            {
                bool first = listedLibrary == null;
                listedLibrary = player.Library;
                if (first) GoToCurrentTrack();
                else if (collection >= listedLibrary.Collections.Count) atRoot = true;
                listDirty = true;
            }
            if (!LibraryPaths.Comparer.Equals(listedTrack ?? "", player.State.TrackPath ?? ""))
            {
                listedTrack = player.State.TrackPath;
                listDirty = true;
            }
            if (page != Page.Library || !listDirty && !scrollToCurrent) return;
            if (listDirty) ShowFolder();
            listDirty = false;
            if (scrollToCurrent) ScrollToCurrent();
            scrollToCurrent = false;
        }

        private void GoToCurrentTrack()
        {
            atRoot = true;
            string current = player.State.TrackPath;
            for (int i = 0; i < listedLibrary.Collections.Count; i++)
            {
                MusicCollection candidate = listedLibrary.Collections[i];
                if (!candidate.Tracks.Any(track => LibraryPaths.Comparer.Equals(track.Path, current))) continue;
                atRoot = false;
                collection = i;
                folder = FolderBrowser.RelativeDirectory(candidate, current);
                int index = FolderBrowser.Open(candidate, folder).Tracks.ToList()
                    .FindIndex(track => LibraryPaths.Comparer.Equals(track.Path, current));
                trackPage = Math.Max(0, index) / TracksPerPage;
                return;
            }
        }

        private void ShowFolder()
        {
            foreach (Transform child in rows)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            pageControls.SetActive(false);
            currentRow = null;
            IReadOnlyList<MusicCollection> collections = listedLibrary?.Collections ?? new MusicCollection[0];
            if (collections.Count == 0)
            {
                back.SetActive(false);
                path.text = "LIBRARY";
                Row(null, player.IsScanning ? "Scanning…" : "No music found", null, false, null);
                return;
            }
            if (atRoot || collection < 0 || collection >= collections.Count)
            {
                atRoot = true;
                back.SetActive(false);
                path.text = "LIBRARY";
                for (int i = 0; i < collections.Count; i++)
                {
                    int index = i;
                    Row(PixelIcons.Folder, collections[i].Label, collections[i].Tracks.Count.ToString(), false, () => Enter(index));
                }
                return;
            }

            MusicCollection selected = collections[collection];
            FolderView view = FolderBrowser.Open(selected, folder);
            folder = view.RelativePath;
            back.SetActive(true);
            path.text = (folder.Length == 0 ? selected.Label : Path.GetFileName(folder)).ToUpperInvariant();
            foreach (MusicFolder child in view.Folders)
            {
                string next = child.RelativePath;
                Row(PixelIcons.Folder, child.Name, null, false, () => Navigate(next));
            }
            int pages = Math.Max(1, (view.Tracks.Count + TracksPerPage - 1) / TracksPerPage);
            trackPage = Mathf.Clamp(trackPage, 0, pages - 1);
            if (pages > 1)
            {
                pageControls.SetActive(true);
                pageLabel.text = (trackPage + 1) + "/" + pages;
            }
            foreach (MusicTrack track in view.Tracks.Skip(trackPage * TracksPerPage).Take(TracksPerPage))
            {
                string trackPath = track.Path;
                bool current = LibraryPaths.Comparer.Equals(trackPath, player.State.TrackPath);
                Row(current ? PixelIcons.Play : PixelIcons.Note, track.Info.Label, null, current, () => SelectTrack(trackPath));
            }
            if (view.Folders.Count == 0 && view.Tracks.Count == 0) Row(null, "Empty folder", null, false, null);
        }

        private void Row(string[] icon, string label, string detail, bool current, Action onClick)
        {
            RectTransform row = NewRect("Row", rows);
            LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = RowHeight;
            if (current) currentRow = row;
            Image face = Surface(row, Ink, onClick != null);
            face.color = onClick == null ? Color.clear : Ink;
            if (onClick != null)
            {
                UnityEngine.UI.Button button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = face;
                button.colors = Tints(current ? new Color(1f, 1f, 1f, 0.1f) : Color.clear,
                    new Color(1f, 1f, 1f, 0.2f), new Color(1f, 1f, 1f, 0.34f));
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => onClick());
            }
            if (icon != null)
            {
                Image glyph = Glyph(row, icon, LcdPixel, Ink);
                glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = glyph.rectTransform.pivot = new Vector2(0f, 0.5f);
                glyph.rectTransform.anchoredPosition = new Vector2(8f, 0f);
            }
            TextMeshProUGUI text = Label(row, label, 18f, Ink, TextAlignmentOptions.Left, false);
            if (current) text.fontStyle = FontStyles.Bold;
            ModUi.Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(icon == null ? 8f : 32f, 0f),
                new Vector2(detail == null && onClick != null && icon == PixelIcons.Folder ? -22f : detail == null ? -6f : -50f, 0f));
            if (detail != null)
            {
                TextMeshProUGUI count = Label(row, detail, 15f, InkSoft, TextAlignmentOptions.Right, false);
                ModUi.Place(count.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-48f, 0f), new Vector2(-6f, 0f));
            }
            else if (onClick != null && icon == PixelIcons.Folder)
            {
                Image more = Glyph(row, PixelIcons.Forward, LcdPixel, Ink);
                more.rectTransform.anchorMin = more.rectTransform.anchorMax = more.rectTransform.pivot = new Vector2(1f, 0.5f);
                more.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
            }
        }

        // Centres the playing track in the list.
        private void ScrollToCurrent()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
            float scrollable = rows.rect.height - list.viewport.rect.height;
            if (currentRow == null || scrollable <= 0f) { list.verticalNormalizedPosition = 1f; return; }
            float centre = -currentRow.localPosition.y - list.viewport.rect.height / 2f;
            list.verticalNormalizedPosition = 1f - Mathf.Clamp01(centre / scrollable);
        }

        private void Enter(int index)
        {
            atRoot = false;
            collection = index;
            folder = "";
            trackPage = 0;
            ShowFolderFromTop();
        }

        private void Navigate(string relativePath)
        {
            folder = relativePath;
            trackPage = 0;
            ShowFolderFromTop();
        }

        private void Back()
        {
            if (atRoot) return;
            if (folder.Length == 0) atRoot = true;
            else folder = FolderBrowser.Parent(folder);
            trackPage = 0;
            ShowFolderFromTop();
        }

        private void ChangePage(int offset)
        {
            trackPage += offset;
            ShowFolderFromTop();
        }

        private void ShowFolderFromTop()
        {
            ShowFolder();
            listDirty = false;
            list.verticalNormalizedPosition = 1f;
        }

        private void SelectTrack(string trackPath)
        {
            player.Select(trackPath);
            ShowPage(Page.NowPlaying);
        }

        // ------------------------------------------------------------------------------------------- helpers

        private static Color ShellColor(Device? device)
        {
            switch (device)
            {
                case Device.PocketPlayer: return new Color(0.25f, 0.205f, 0.15f, 1f);
                case Device.RechargeablePlayer: return new Color(0.12f, 0.23f, 0.24f, 1f);
                default: return new Color(0.22f, 0.21f, 0.19f, 1f);
            }
        }

        private static Color Shade(Color color, float factor) =>
            new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), color.a);

        private static ColorBlock Tints(Color normal, Color highlighted, Color pressed)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = normal;
            colors.disabledColor = normal;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.04f;
            return colors;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool wrap)
        {
            TextMeshProUGUI label = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (hudFont != null)
            {
                label.font = hudFont;
                label.fontSharedMaterial = hudMaterial;
            }
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.richText = false;
            label.raycastTarget = false;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = text;
            return label;
        }

        // The HUD money counter's face. Every MoneyPanel text in the installed game uses this font asset with this
        // material; the research note records how that was resolved from the game data.
        private static bool FindHudFont()
        {
            if (hudFont != null && hudMaterial != null) return true;
            hudFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(asset => asset.name == HudFontName);
            hudMaterial = hudFont == null ? null : Resources.FindObjectsOfTypeAll<Material>()
                .FirstOrDefault(material => material.name == HudMaterialName && material.mainTexture == hudFont.atlasTexture);
            if (hudMaterial == null) hudFont = null;
            return hudFont != null;
        }

        private static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align,
            float x, float y, float width, float height, bool wrap = false)
        {
            TextMeshProUGUI label = Label(parent, text, size, color, align, wrap);
            Place(label.rectTransform, x, y, width, height);
            return label;
        }

        // A white pixel-art sprite at a whole-number scale, top-left placed, or centred when no position is given.
        private static Image Glyph(Transform parent, string[] map, float pixel, Color color, float? x = null, float? y = null)
        {
            RectTransform rect = NewRect("Glyph", parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = PixelIcons.Sprite(map);
            image.color = color;
            image.raycastTarget = false;
            if (x.HasValue) Place(rect, x.Value, y ?? 0f, map[0].Length * pixel, map.Length * pixel);
            else
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = PixelIcons.Size(map, pixel);
            }
            return image;
        }

        private static Image Block(string name, Transform parent, Color color, float x, float y, float width, float height,
            bool raycast = false)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, x, y, width, height);
            return Surface(rect, color, raycast);
        }

        // A hollow rectangle drawn as four edges, returned so the window can tint them.
        private static Image[] Outline(Transform parent, string name, Color color, float x, float y, float width, float height, float line)
        {
            RectTransform frame = NewRect(name, parent);
            Place(frame, x, y, width, height);
            return new[]
            {
                Block("Top", frame, color, 0f, 0f, width, line),
                Block("Bottom", frame, color, 0f, height - line, width, line),
                Block("Left", frame, color, 0f, 0f, line, height),
                Block("Right", frame, color, width - line, 0f, line, height)
            };
        }

        private static void Tint(Image[] edges, Color color)
        {
            foreach (Image edge in edges) edge.color = color;
        }

        // Positions a rect from the top-left of its parent, in parent units.
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Image Surface(RectTransform rect, Color color, bool raycast)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return (RectTransform)item.transform;
        }

        private static void Grain(RectTransform parent)
        {
            RawImage grain = NewRect("Grain", parent).gameObject.AddComponent<RawImage>();
            ModUi.Place(grain.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            grain.texture = Textures.Grain;
            grain.uvRect = new Rect(0f, 0f, BodySize.x / 64f, BodySize.y / 64f);
            grain.raycastTarget = false;
        }

        private static void PixelGrid(RectTransform lcd)
        {
            RawImage grid = NewRect("Dot matrix", lcd).gameObject.AddComponent<RawImage>();
            ModUi.Place(grid.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            grid.texture = Textures.Grid;
            grid.uvRect = new Rect(0f, 0f, LcdArea.width / 3f, LcdArea.height / 3f);
            grid.raycastTarget = false;
        }

        private static string FormatTime(int seconds) => (seconds / 60) + ":" + (seconds % 60).ToString("00");

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            ownedEventSystem = new GameObject("PortableMusicPlayer.EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void ShowCursor()
        {
            if (GameController.instance == null) return;
            GameController.instance.ChangeCursorStatus(CursorLockMode.None, true);
            cursorShown = true;
        }

        private void RestoreCursor()
        {
            if (!cursorShown) return;
            cursorShown = false;
            if (GameController.instance != null && !UiState.IsMenuOpen && !UiState.IsPaused && !UiState.IsInputBlocked)
                GameController.instance.ChangeCursorStatus(CursorLockMode.Locked, false);
        }

        private void OnSaveLoad() => Close();

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene == home) Close();
        }

        // Generated once and shared by every window.
        private static class Textures
        {
            internal static readonly Texture2D Grain = Make(32, 32, FilterMode.Bilinear, (x, y, random) =>
            {
                float value = (float)random.NextDouble();
                byte shade = value > 0.5f ? (byte)255 : (byte)0;
                return new Color32(shade, shade, shade, (byte)(Math.Abs(value - 0.5f) * 40f));
            });

            // One dark line along the right and bottom of each 3-unit LCD dot.
            internal static readonly Texture2D Grid = Make(3, 3, FilterMode.Bilinear, (x, y, random) =>
                new Color32(0, 0, 0, (byte)(x == 2 || y == 0 ? 16 : 0)));

            private static Texture2D Make(int width, int height, FilterMode filter, Func<int, int, System.Random, Color32> pixel)
            {
                var random = new System.Random(43);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = filter,
                    wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = pixel(x, y, random);
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return texture;
            }
        }
    }
}
