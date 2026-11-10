using StbImageSharp;
using War3Net.Drawing.Blp;

namespace Moonwell.Build
{
    /// <summary>Turns the PNG images of a map's assets into BLP, the game's texture format (decision 249).</summary>
    public static class Images
    {
        // BLP1's header: magic, content, alpha depth, width, height, extra, has mipmaps.
        private const int AlphaDepthOffset = 8;

        /// <summary>The name a file of the assets has in the map: a <c>.png</c> becomes a <c>.blp</c> at the same path.</summary>
        /// <param name="file">The file's path relative to the assets folder.</param>
        /// <returns>The path in the map.</returns>
        public static string PackedName(string file)
        {
            return IsPng(file) ? Path.ChangeExtension(file, ".blp") : file;
        }

        /// <summary>Whether a file of the assets is a PNG, which the map holds as a BLP.</summary>
        public static bool IsPng(string file)
        {
            return string.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Writes a PNG as a BLP1 of JPEG mipmaps, as the game's own textures are, with its alpha when a pixel is not opaque.</summary>
        /// <param name="pngPath">The PNG.</param>
        /// <param name="blpPath">The BLP to write; replaced if it exists.</param>
        /// <param name="name">The PNG's name in the messages, <c>assets\Icons\BTNFire.png</c>.</param>
        /// <exception cref="BuildException">The file is not a PNG the build can read, or a side is not a power of two.</exception>
        public static void WriteBlp(string pngPath, string blpPath, string name)
        {
            ImageResult image;
            try
            {
                using var stream = File.OpenRead(pngPath);
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            }
            catch (Exception failure) when (failure is not IOException and not UnauthorizedAccessException)
            {
                throw new BuildException($"{name} is not a PNG the build can read ({failure.Message})");
            }

            if (!IsPowerOfTwo(image.Width) || !IsPowerOfTwo(image.Height))
            {
                throw new BuildException($"{name} is {image.Width}x{image.Height}; each side of a texture must be a power of two (32, 64, 128, 256...), so resize it");
            }

            var pixels = image.Data;
            var transparent = false;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                transparent |= pixels[i + 3] != 255;
            }

            // shortcut: War3Net's encoder stops the mipmaps when the shorter side is 1 (64x32 ends at 2x1), where the game's files go to 1x1;
            // write the mipmaps here if a long texture shows wrong from afar in the game.
            using var blp = new MemoryStream();
            new BlpEncoder().Encode(blp, image.Width, image.Height, pixels);
            var bytes = blp.ToArray();

            // The encoder always writes an alpha depth of 0, which the game reads as opaque.
            if (transparent)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(AlphaDepthOffset, 4), 8u);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(blpPath))!);
            File.WriteAllBytes(blpPath, bytes);
        }

        private static bool IsPowerOfTwo(int side)
        {
            return side > 0 && (side & (side - 1)) == 0;
        }
    }
}
