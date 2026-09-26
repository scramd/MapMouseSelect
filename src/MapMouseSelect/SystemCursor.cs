using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// The current Windows cursor scheme's move cursor, read out as pixels for Unity. Unity owns the pointer while
    /// the game runs, so anything set on the OS pointer behind it flickers. A cursor arrives as a colour bitmap plus
    /// a monochrome mask, and a pixel is transparent only where both agree. Bitmap rows are bottom-up, so the pixels
    /// flip and the hotspot does not.
    /// </summary>
    internal static class SystemCursor
    {
        /// <summary>IDC_SIZEALL, the pointer Windows uses for moving something.</summary>
        private const int MoveCursor = 32646;

        /// <summary>SM_CXCURSOR – the size Windows draws cursors at, larger on a scaled display.</summary>
        private const int CursorWidth = 13;

        private const uint RgbColors = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            public bool IsIcon;
            public int HotspotX;
            public int HotspotY;
            public IntPtr Mask;
            public IntPtr Color;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        /// <summary>With room for the palette a one-bit format needs – two entries.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
            public uint Colors2;
            public uint Colors3;
            public uint Colors4;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Bitmap
        {
            public int Type;
            public int Width;
            public int Height;
            public int WidthBytes;
            public ushort Planes;
            public ushort BitsPixel;
            public IntPtr Bits;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr obj, int size, ref Bitmap bitmap);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfo info, uint usage);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        internal static int Size()
        {
            try
            {
                var size = GetSystemMetrics(CursorWidth);
                if (size >= 16 && size <= 256)
                {
                    return size;
                }
            }
            catch
            {
                // 32 fits any cursor.
            }

            return 32;
        }

        /// <summary>The theme's move cursor as a texture with its hotspot. Null when Windows will not hand
        /// it over, which leaves the caller with the game's own arrow.</summary>
        internal static Texture2D Move(int size, out Vector2 hotspot)
        {
            hotspot = Vector2.zero;

            var handle = LoadCursor(IntPtr.Zero, new IntPtr(MoveCursor));
            if (handle == IntPtr.Zero || !GetIconInfo(handle, out var info))
            {
                Plugin.Log.LogWarning("Could not read the system's move cursor, so the map pan keeps the game's arrow.");
                return null;
            }

            try
            {
                var shape = default(Bitmap);
                GetObject(info.Color, Marshal.SizeOf(typeof(Bitmap)), ref shape);

                var width = shape.Width;
                var height = shape.Height;
                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                var dc = GetDC(IntPtr.Zero);
                var color = Read(dc, info.Color, width, height, 32);
                var mask = Read(dc, info.Mask, width, height, 1);
                ReleaseDC(IntPtr.Zero, dc);

                if (color == null)
                {
                    return null;
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "MapMouseSelect.PanCursor",

                    // Point, because the cursor is drawn at its own size and a filter would only soften the edge.
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };

                var maskStride = (width + 7) / 8;
                var pixels = new Color32[size * size];

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var fromX = width * x / size;
                        var fromY = height * y / size;
                        var index = (fromY * width) + fromX;
                        var alpha = color[(index * 4) + 3];
                        var masked = mask != null
                            && (mask[(fromY * maskStride) + (fromX / 8)] & (0x80 >> (fromX % 8))) != 0;

                        pixels[(y * size) + x] = masked && alpha == 0
                            ? new Color32(0, 0, 0, 0)
                            : new Color32(
                                color[(index * 4) + 2],
                                color[(index * 4) + 1],
                                color[index * 4],
                                alpha == 0 ? (byte)255 : alpha);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                // Unity measures the hotspot from the top left like Windows does, so it needs no flipping.
                hotspot = new Vector2(
                    info.HotspotX * size / (float)width,
                    info.HotspotY * size / (float)height);

                Plugin.Verbose(
                    "Pan cursor: " + width + "x" + height + "px cursor read at " + size + "px, hotspot "
                    + info.HotspotX + "," + info.HotspotY + ", so the click point sits at " + hotspot.x.ToString("0.#")
                    + "," + hotspot.y.ToString("0.#") + " from the top left.");

                return texture;
            }
            finally
            {
                DeleteObject(info.Color);
                DeleteObject(info.Mask);
            }
        }

        private static byte[] Read(IntPtr dc, IntPtr bitmap, int width, int height, ushort bits)
        {
            var stride = bits == 1 ? (width + 7) / 8 : width * (bits / 8);
            var buffer = new byte[stride * height];
            var info = new BitmapInfo();

            info.Header.Size = 40;
            info.Header.Width = width;
            info.Header.Height = height;
            info.Header.Planes = 1;
            info.Header.BitCount = bits;
            info.Header.Compression = 0;
            info.Header.SizeImage = (uint)buffer.Length;

            return GetDIBits(dc, bitmap, 0, (uint)height, buffer, ref info, RgbColors) == 0 ? null : buffer;
        }
    }
}
