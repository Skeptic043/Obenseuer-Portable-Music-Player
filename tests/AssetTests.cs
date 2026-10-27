using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class AssetTests
    {
        public static void WorldModelsUseAMutedLimitedPalette()
        {
            string assets = Path.Combine(ProjectRoot(), "assets");
            foreach (string path in Directory.GetFiles(assets, "*.mtl").OrderBy(path => path))
            {
                string[] materials = File.ReadAllLines(path).Where(line => line.StartsWith("newmtl ", StringComparison.Ordinal)).ToArray();
                Assert.True(materials.Length > 0 && materials.Length <= 8, Path.GetFileName(path) + " material count");
                foreach (string line in File.ReadAllLines(path).Where(line => line.StartsWith("Kd ", StringComparison.Ordinal)))
                {
                    float brightest = line.Substring(3).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => float.Parse(value, CultureInfo.InvariantCulture)).Max();
                    Assert.True(brightest <= 0.82f, Path.GetFileName(path) + " has a bright polished material: " + line);
                }
            }
        }

        // The framework silently falls back to the flat Kd colour when a texture is missing or over 2048 px a side.
        // The template materials read smoothness from albedo alpha, so a texture without alpha renders mirror-glossy.
        public static void EveryModelTextureShipsBesideItsModelWithinTheFrameworkLimit()
        {
            string assets = Path.Combine(ProjectRoot(), "assets");
            foreach (string path in Directory.GetFiles(assets, "*.mtl"))
            foreach (string line in File.ReadAllLines(path).Where(line => line.StartsWith("map_Kd ", StringComparison.Ordinal)))
            {
                string texture = Path.Combine(assets, line.Substring(7).Trim());
                Assert.True(File.Exists(texture), Path.GetFileName(path) + " names a missing texture " + Path.GetFileName(texture));
                byte[] header = new byte[26];
                using (FileStream stream = File.OpenRead(texture)) stream.Read(header, 0, header.Length);
                // PNG IHDR: width and height are big-endian at bytes 16 and 20, the colour type at byte 25.
                int width = header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19];
                int height = header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23];
                Assert.True(header[1] == 'P' && header[2] == 'N' && header[3] == 'G' && width > 0 && width <= 2048 &&
                        height > 0 && height <= 2048, Path.GetFileName(texture) + " is not a PNG within 2048 px a side");
                Assert.True(header[25] == 6, Path.GetFileName(texture) + " has no alpha channel to carry its smoothness");
            }
        }

        private static string ProjectRoot()
        {
            DirectoryInfo directory = new FileInfo(typeof(AssetTests).Assembly.Location).Directory;
            while (directory != null && directory.Name != ".local") directory = directory.Parent;
            Assert.True(directory?.Parent != null, "test assembly is not under the project .local directory");
            return directory.Parent.FullName;
        }
    }
}
