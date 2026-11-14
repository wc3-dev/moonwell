using StbImageSharp;
using War3Net.Drawing.Blp;

namespace Moonwell.Build
{
    /// <summary>Turns the PNG images of a map's assets into BLP, the game's texture format (decision 249), and makes its icons' grey copies (decision 250).</summary>
    public static class Images
    {
        // BLP1's header: magic, content, alpha depth, width, height, extra, has mipmaps.
        private const int AlphaDepthOffset = 8;

        // A disabled button shows this folder's DIS + its icon's file name, wherever the icon is (game facts, 11-10).
        private const string GreyFolder = @"ReplaceableTextures\CommandButtonsDisabled";

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

        /// <summary>Where the game reads the grey copy of an icon named <c>BTN…</c> or <c>PASBTN…</c>, by its name in the map; null for any other file.</summary>
        /// <param name="packedName">The file's path in the map.</param>
        /// <returns><c>ReplaceableTextures\CommandButtonsDisabled\DIS</c> and the file name, or null.</returns>
        public static string? GreyCopyName(string packedName)
        {
            var name = Path.GetFileName(packedName);
            var icon = string.Equals(Path.GetExtension(name), ".blp", StringComparison.OrdinalIgnoreCase)
                && (name.StartsWith("BTN", StringComparison.OrdinalIgnoreCase) || name.StartsWith("PASBTN", StringComparison.OrdinalIgnoreCase));
            return icon ? Path.Combine(GreyFolder, "DIS" + name) : null;
        }

        /// <summary>Writes a PNG as a BLP1 of JPEG mipmaps, as the game's own textures are, with its alpha when a pixel is not opaque.</summary>
        /// <param name="pngPath">The PNG.</param>
        /// <param name="blpPath">The BLP to write; replaced if it exists.</param>
        /// <param name="name">The PNG's name in the messages, <c>assets\Icons\BTNFire.png</c>.</param>
        /// <exception cref="BuildException">The file is not a PNG the build can read, or a side is not a power of two.</exception>
        public static void WriteBlp(string pngPath, string blpPath, string name)
        {
            var (width, height, pixels) = ReadPng(pngPath, name);
            Write(blpPath, width, height, pixels);
        }

        /// <summary>Writes the grey copy of an icon, a PNG or a BLP, as the game's HD copies are: grey, at 0.46 of the brightness plus 8.5, measured over 398 of them.</summary>
        /// <param name="iconPath">The icon in the assets.</param>
        /// <param name="greyPath">The grey copy to write.</param>
        /// <param name="name">The icon's name in the messages.</param>
        /// <exception cref="BuildException">The icon cannot be read.</exception>
        public static void WriteGreyCopy(string iconPath, string greyPath, string name)
        {
            var (width, height, pixels) = IsPng(iconPath) ? ReadPng(iconPath, name) : ReadBlp(iconPath, name);
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var brightness = (0.114 * pixels[i]) + (0.587 * pixels[i + 1]) + (0.299 * pixels[i + 2]);
                pixels[i] = pixels[i + 1] = pixels[i + 2] = (byte)Math.Round((0.459 * brightness) + 8.5);
            }

            Write(greyPath, width, height, pixels);
        }

        /// <summary>A PNG's or a BLP's pixels as blue, green, red and alpha.</summary>
        /// <exception cref="BuildException">The file cannot be read as one, or a PNG's side is not a power of two.</exception>
        internal static (int Width, int Height, byte[] Pixels) Read(string path, string name)
        {
            return IsPng(path) ? ReadPng(path, name) : ReadBlp(File.ReadAllBytes(path), name);
        }

        /// <summary>One of the game's textures, a BLP or a DDS (as Reforged stores even its Classic ones), from its bytes, as blue, green, red and alpha.</summary>
        /// <exception cref="BuildException">The bytes are neither a BLP nor a DDS the build can read.</exception>
        internal static (int Width, int Height, byte[] Pixels) ReadGame(byte[] bytes, string name)
        {
            if (bytes.Length < 4 || bytes[0] != 'D' || bytes[1] != 'D' || bytes[2] != 'S' || bytes[3] != ' ')
            {
                return ReadBlp(bytes, name);
            }

            try
            {
                using var image = Pfim.Dds.Create(bytes, new Pfim.PfimConfig());
                if (image.Compressed)
                {
                    image.Decompress();
                }

                var size = image.Format switch
                {
                    Pfim.ImageFormat.Rgba32 => 4,
                    Pfim.ImageFormat.Rgb24 => 3,
                    _ => throw new BuildException($"{name} is a DDS of {image.Format}, which the build does not read"),
                };

                // Pfim gives blue, green, red (and alpha) rows, each Stride bytes long.
                var pixels = new byte[image.Width * image.Height * 4];
                for (var y = 0; y < image.Height; y++)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        var from = (y * image.Stride) + (x * size);
                        var to = ((y * image.Width) + x) * 4;
                        (pixels[to], pixels[to + 1], pixels[to + 2]) = (image.Data[from], image.Data[from + 1], image.Data[from + 2]);
                        pixels[to + 3] = size == 4 ? image.Data[from + 3] : (byte)255;
                    }
                }

                return (image.Width, image.Height, pixels);
            }
            catch (Exception failure) when (failure is not BuildException)
            {
                // Pfim says a file is damaged with a plain Exception.
                throw new BuildException($"{name} is not a DDS the build can read ({failure.Message})");
            }
        }

        /// <summary>A BLP's pixels, from its bytes, as blue, green, red and alpha.</summary>
        /// <exception cref="BuildException">The bytes are not a BLP the build can read.</exception>
        internal static (int Width, int Height, byte[] Pixels) ReadBlp(byte[] bytes, string name)
        {
            try
            {
                using var blp = new BlpFile(new MemoryStream(bytes));
                var pixels = blp.GetPixels(0, out var width, out var height);
                return (width, height, pixels);
            }
            catch (Exception failure)
            {
                throw new BuildException($"{name} is not a BLP the build can read ({failure.Message})");
            }
        }

        // The image's pixels as blue, green, red and alpha.
        private static (int Width, int Height, byte[] Pixels) ReadPng(string pngPath, string name)
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
            for (var i = 0; i < pixels.Length; i += 4)
            {
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            }

            return (image.Width, image.Height, pixels);
        }

        private static (int Width, int Height, byte[] Pixels) ReadBlp(string blpPath, string name)
        {
            try
            {
                using var blp = new BlpFile(File.OpenRead(blpPath));
                var pixels = blp.GetPixels(0, out var width, out var height);
                return (width, height, pixels);
            }
            catch (Exception failure) when (failure is not IOException and not UnauthorizedAccessException)
            {
                throw new BuildException($@"{name} is not a BLP the build can read to make its grey copy ({failure.Message}); put one at assets\{GreyCopyName(name)} yourself");
            }
        }

        /// <summary>Writes pixels, blue, green, red and alpha, as a BLP1 of JPEG mipmaps, with its alpha when a pixel is not opaque.</summary>
        internal static void Write(string blpPath, int width, int height, byte[] pixels)
        {
            var transparent = false;
            for (var i = 3; i < pixels.Length && !transparent; i += 4)
            {
                transparent = pixels[i] != 255;
            }

            // shortcut: War3Net's encoder stops the mipmaps when the shorter side is 1 (64x32 ends at 2x1), where the game's files go to 1x1;
            // write the mipmaps here if a long texture shows wrong from afar in the game.
            using var blp = new MemoryStream();
            new BlpEncoder().Encode(blp, width, height, pixels);
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
