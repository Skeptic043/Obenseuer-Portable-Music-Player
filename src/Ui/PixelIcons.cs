using System.Collections.Generic;
using UnityEngine;

namespace PortableMusicPlayer.Ui
{
    // Pixel-art glyphs for the player's LCD and buttons. Each map row is one pixel row from the top; X is lit.
    // Sprites are white and point-filtered, so an Image tints them and they stay crisp at any whole scale.
    internal static class PixelIcons
    {
        internal static readonly string[] Play =
        {
            "X....",
            "XX...",
            "XXX..",
            "XXXX.",
            "XXXXX",
            "XXXX.",
            "XXX..",
            "XX...",
            "X....",
        };

        internal static readonly string[] Pause =
        {
            "XX..XX",
            "XX..XX",
            "XX..XX",
            "XX..XX",
            "XX..XX",
            "XX..XX",
            "XX..XX",
            "XX..XX",
            "XX..XX",
        };

        internal static readonly string[] Next =
        {
            "X....X.....XX",
            "XX...XX....XX",
            "XXX..XXX...XX",
            "XXXX.XXXX..XX",
            "XXXXXXXXXX.XX",
            "XXXX.XXXX..XX",
            "XXX..XXX...XX",
            "XX...XX....XX",
            "X....X.....XX",
        };

        internal static readonly string[] Previous = Mirror(Next);

        internal static readonly string[] Shuffle =
        {
            "..........X..",
            "...........X.",
            "XXXX...XXXXXX",
            "....X.X....X.",
            ".....X....X..",
            "....X.X....X.",
            "XXXX...XXXXXX",
            "...........X.",
            "..........X..",
        };

        internal static readonly string[] Repeat =
        {
            ".........X...",
            "..........X..",
            ".XXXXXXXXXXX.",
            "X.........X..",
            "X........X...",
            "X...........X",
            "...X........X",
            "..X.........X",
            ".XXXXXXXXXXX.",
            "..X..........",
            "...X.........",
        };

        internal static readonly string[] RepeatOne =
        {
            ".........X...",
            "..........X..",
            ".XXXXXXXXXXX.",
            "X.........X..",
            "X....XX..X...",
            "X.....X.....X",
            "...X..X.....X",
            "..X.........X",
            ".XXXXXXXXXXX.",
            "..X..........",
            "...X.........",
        };

        internal static readonly string[] Headphones =
        {
            "..XXXXX..",
            ".X.....X.",
            "X.......X",
            "X.......X",
            "XX.....XX",
            "XX.....XX",
            "XX.....XX",
        };

        internal static readonly string[] Folder =
        {
            "XXX......",
            "XXXXXXXXX",
            "X.......X",
            "X.......X",
            "X.......X",
            "XXXXXXXXX",
        };

        internal static readonly string[] Note =
        {
            "...XXXXX",
            "...X...X",
            "...X...X",
            "...X...X",
            ".XXX.XXX",
            "XXXXXXXX",
            ".XX..XX.",
        };

        internal static readonly string[] Forward =
        {
            "X...",
            "XX..",
            ".XX.",
            "..XX",
            ".XX.",
            "XX..",
            "X...",
        };

        internal static readonly string[] Back = Mirror(Forward);

        internal static readonly string[] Menu =
        {
            "XXXXXXXXX",
            ".........",
            ".........",
            "XXXXXXXXX",
            ".........",
            ".........",
            "XXXXXXXXX",
        };

        internal static readonly string[] Close =
        {
            "XX...XX",
            ".XX.XX.",
            "..XXX..",
            "...X...",
            "..XXX..",
            ".XX.XX.",
            "XX...XX",
        };

        // A speaker with one sound wave for quieter and two for louder.
        internal static readonly string[] VolumeDown =
        {
            "....X....",
            "...XX....",
            "XXXXX..X.",
            "XXXXX...X",
            "XXXXX...X",
            "XXXXX...X",
            "XXXXX..X.",
            "...XX....",
            "....X....",
        };

        internal static readonly string[] VolumeUp =
        {
            "....X.....X.",
            "...XX......X",
            "XXXXX..X...X",
            "XXXXX...X..X",
            "XXXXX...X..X",
            "XXXXX...X..X",
            "XXXXX..X...X",
            "...XX......X",
            "....X.....X.",
        };

        private static readonly Dictionary<string[], Sprite> sprites = new Dictionary<string[], Sprite>();

        internal static Sprite Sprite(string[] map)
        {
            if (sprites.TryGetValue(map, out Sprite sprite) && sprite != null) return sprite;
            int height = map.Length, width = map[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                    // Textures fill from the bottom row up.
                    pixels[(height - 1 - row) * width + column] = map[row][column] == 'X'
                        ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprites[map] = sprite;
            return sprite;
        }

        internal static Vector2 Size(string[] map, float pixel) => new Vector2(map[0].Length * pixel, map.Length * pixel);

        private static string[] Mirror(string[] map)
        {
            var mirrored = new string[map.Length];
            for (int i = 0; i < map.Length; i++)
            {
                char[] row = map[i].ToCharArray();
                System.Array.Reverse(row);
                mirrored[i] = new string(row);
            }
            return mirrored;
        }
    }
}
