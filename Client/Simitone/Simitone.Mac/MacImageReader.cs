using StbImageSharp;
using System;
using System.IO;

namespace Simitone.Mac
{
    /// <summary>
    /// Replacement for the GDI+ image hook used on Windows. The Sims ships RLE-compressed BMPs, which
    /// MonoGame's built-in decoder rejects, so BMPs are decoded here and everything else goes to StbImageSharp.
    /// Output is RGBA, top row first.
    /// </summary>
    public static class MacImageReader
    {
        public static Tuple<byte[], int, int> Read(Stream str)
        {
            byte[] data;
            using (var mem = new MemoryStream())
            {
                str.CopyTo(mem);
                data = mem.ToArray();
            }

            if (data.Length > 2 && data[0] == 'B' && data[1] == 'M')
            {
                try
                {
                    return ReadBMP(data);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            try
            {
                var img = ImageResult.FromMemory(data, ColorComponents.RedGreenBlueAlpha);
                return new Tuple<byte[], int, int>(img.Data, img.Width, img.Height);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const int BI_RGB = 0;
        private const int BI_RLE8 = 1;
        private const int BI_RLE4 = 2;
        private const int BI_BITFIELDS = 3;

        public static Tuple<byte[], int, int> ReadBMP(byte[] file)
        {
            var pixelOffset = BitConverter.ToInt32(file, 10);
            var headerSize = BitConverter.ToInt32(file, 14);
            var width = BitConverter.ToInt32(file, 18);
            var rawHeight = BitConverter.ToInt32(file, 22);
            var bpp = BitConverter.ToInt16(file, 28);
            var compression = BitConverter.ToInt32(file, 30);
            var paletteCount = BitConverter.ToInt32(file, 46);

            var topDown = rawHeight < 0;
            var height = Math.Abs(rawHeight);
            if (width <= 0 || height == 0) throw new InvalidDataException("Bad BMP size.");

            //palette (BGRx entries) follows the info header
            byte[][] palette = null;
            if (bpp <= 8)
            {
                if (paletteCount == 0) paletteCount = 1 << bpp;
                palette = new byte[paletteCount][];
                var palStart = 14 + headerSize;
                for (int i = 0; i < paletteCount; i++)
                {
                    var p = palStart + i * 4;
                    palette[i] = new byte[] { file[p + 2], file[p + 1], file[p], 255 };
                }
            }

            //decode to palette indices or RGBA rows, bottom-up as stored unless top-down
            var result = new byte[width * height * 4];
            Action<int, int, int> setIndex = (x, y, index) =>
            {
                if (x >= width || y >= height) return;
                var row = topDown ? y : (height - 1 - y);
                var c = palette[index < palette.Length ? index : 0];
                Buffer.BlockCopy(c, 0, result, (row * width + x) * 4, 4);
            };

            if (compression == BI_RLE8 || compression == BI_RLE4)
            {
                DecodeRLE(file, pixelOffset, compression == BI_RLE4, setIndex);
            }
            else if (compression == BI_RGB || compression == BI_BITFIELDS)
            {
                var stride = ((width * bpp + 31) / 32) * 4;
                for (int y = 0; y < height; y++)
                {
                    var src = pixelOffset + y * stride;
                    var row = topDown ? y : (height - 1 - y);
                    for (int x = 0; x < width; x++)
                    {
                        switch (bpp)
                        {
                            case 1: setIndex(x, y, (file[src + x / 8] >> (7 - (x % 8))) & 1); break;
                            case 4: setIndex(x, y, (x % 2 == 0) ? (file[src + x / 2] >> 4) : (file[src + x / 2] & 0xF)); break;
                            case 8: setIndex(x, y, file[src + x]); break;
                            case 16:
                                {
                                    var v = BitConverter.ToUInt16(file, src + x * 2); //5-5-5
                                    var o = (row * width + x) * 4;
                                    result[o] = (byte)(((v >> 10) & 31) * 255 / 31);
                                    result[o + 1] = (byte)(((v >> 5) & 31) * 255 / 31);
                                    result[o + 2] = (byte)((v & 31) * 255 / 31);
                                    result[o + 3] = 255;
                                    break;
                                }
                            case 24:
                            case 32:
                                {
                                    var s = src + x * (bpp / 8);
                                    var o = (row * width + x) * 4;
                                    result[o] = file[s + 2];
                                    result[o + 1] = file[s + 1];
                                    result[o + 2] = file[s];
                                    result[o + 3] = 255; //the game's BMPs use colour keys, not alpha
                                    break;
                                }
                            default:
                                throw new InvalidDataException("Unsupported BMP depth " + bpp);
                        }
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Unsupported BMP compression " + compression);
            }

            return new Tuple<byte[], int, int>(result, width, height);
        }

        /// <summary>
        /// Windows RLE8/RLE4. Coordinates passed to setIndex are in storage order (y = 0 is the first stored row).
        /// </summary>
        private static void DecodeRLE(byte[] file, int pos, bool rle4, Action<int, int, int> setIndex)
        {
            int x = 0, y = 0;
            while (pos + 1 < file.Length)
            {
                int count = file[pos++];
                int value = file[pos++];
                if (count > 0)
                {
                    //encoded run
                    for (int i = 0; i < count; i++)
                    {
                        var index = rle4 ? ((i % 2 == 0) ? (value >> 4) : (value & 0xF)) : value;
                        setIndex(x++, y, index);
                    }
                    continue;
                }

                switch (value)
                {
                    case 0: //end of line
                        x = 0; y++;
                        break;
                    case 1: //end of bitmap
                        return;
                    case 2: //delta
                        x += file[pos++];
                        y += file[pos++];
                        break;
                    default:
                        {
                            //absolute run of `value` pixels, padded to a 16-bit boundary
                            var bytes = rle4 ? (value + 1) / 2 : value;
                            for (int i = 0; i < value; i++)
                            {
                                var index = rle4 ? ((i % 2 == 0) ? (file[pos + i / 2] >> 4) : (file[pos + i / 2] & 0xF)) : file[pos + i];
                                setIndex(x++, y, index);
                            }
                            pos += bytes + (bytes & 1);
                            break;
                        }
                }
            }
        }
    }
}
