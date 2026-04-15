
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
namespace FourPlayWebApp.Shared.Helpers;

public static class BackgroundRemover {
    public static async Task<bool> IsTbaImage(byte[] image)
    {
        using var ms = new MemoryStream(image);
        using var img = await Image.LoadAsync<Rgba32>(ms);
        // Sample pixels
        Rgba32 topLeft = img[0, 0];
        Rgba32 bottomLeft = img[0, img.Height - 1];

        // Define thresholds
        const int whiteTolerance = 30; // allows near-white
        const int grayTolerance = 30;  // allows near-gray

        bool topIsWhite = topLeft is { R: >= 255 - whiteTolerance, G: >= 255 - whiteTolerance, B: >= 255 - whiteTolerance };

        bool bottomIsGray = Math.Abs(bottomLeft.R - bottomLeft.G) < grayTolerance &&
                            Math.Abs(bottomLeft.R - bottomLeft.B) < grayTolerance &&
                            bottomLeft.R < 220; // not too bright

        return topIsWhite && bottomIsGray;
    }
    public static async Task<byte[]> FloodFillTransparent(
        byte[] image,
        int startX,
        int startY,
        int tolerance = 24,
        byte haloAlphaThreshold = 60,
        bool doShrinkHalo = true) {
        using var ms = new MemoryStream(image);
        using var img = await Image.LoadAsync<Rgba32>(ms);

        int width = img.Width;
        int height = img.Height;

        // Validate start
        if ((uint)startX >= width || (uint)startY >= height)
            throw new ArgumentOutOfRangeException(nameof(startX));

        // Get starting color
        Rgba32 target = img[startX, startY];

        // Flood-fill
        var visited = new byte[width * height];
        var q = new Queue<(int X, int Y)>();
        q.Enqueue((startX, startY));

        bool Within(Rgba32 a, Rgba32 b, int t)
            => Math.Abs(a.R - b.R) <= t &&
               Math.Abs(a.G - b.G) <= t &&
               Math.Abs(a.B - b.B) <= t;

        while (q.Count > 0) {
            var (px, py) = q.Dequeue();
            if ((uint)px >= width || (uint)py >= height)
                continue;

            int idx = py * width + px;
            if (visited[idx] == 1)
                continue;

            visited[idx] = 1;

            // Ref-safe pixel access
            Span<Rgba32> row = img.DangerousGetPixelRowMemory(py).Span;
            ref Rgba32 cur = ref row[px];

            if (!Within(target, cur, tolerance))
                continue;

            // Fully transparent – MUST clear RGB for no-flicker effects
            cur = new Rgba32(0, 0, 0, 0);

            q.Enqueue((px + 1, py));
            q.Enqueue((px - 1, py));
            q.Enqueue((px, py + 1));
            q.Enqueue((px, py - 1));
        }

        // Build alpha map
        var alpha = new byte[width * height];
        for (int y = 0; y < height; y++) {
            Span<Rgba32> row = img.DangerousGetPixelRowMemory(y).Span;
            for (int x = 0; x < width; x++)
                alpha[y * width + x] = row[x].A;
        }

        // Remove low-alpha halo pixels
        for (int y = 0; y < height; y++) {
            Span<Rgba32> row = img.DangerousGetPixelRowMemory(y).Span;
            for (int x = 0; x < width; x++) {
                int i = y * width + x;
                if (alpha[i] != 0 && alpha[i] < haloAlphaThreshold) {
                    row[x] = new Rgba32(0, 0, 0, 0);
                    alpha[i] = 0;
                }
            }
        }

        // Shrink remaining 1px fringes
        if (doShrinkHalo) {
            var clearList = new List<(int X, int Y)>();

            for (int y = 1; y < height - 1; y++) {
                for (int x = 1; x < width - 1; x++) {
                    int i = y * width + x;
                    if (alpha[i] == 0)
                        continue;

                    bool neighborsTransparent =
                        alpha[(y - 1) * width + x] == 0 &&
                        alpha[(y + 1) * width + x] == 0 &&
                        alpha[y * width + x - 1] == 0 &&
                        alpha[y * width + x + 1] == 0;

                    if (neighborsTransparent)
                        clearList.Add((x, y));
                }
            }

            foreach (var (x, y) in clearList) {
                img[x, y] = new Rgba32(0, 0, 0, 0);
                alpha[y * width + x] = 0;
            }
        }

        // Premultiply alpha to avoid compositor flicker
        for (int y = 0; y < height; y++) {
            Span<Rgba32> row = img.DangerousGetPixelRowMemory(y).Span;
            for (int x = 0; x < width; x++) {
                ref Rgba32 p = ref row[x];
                if (p.A == 0) {
                    p.R = p.G = p.B = 0;
                    continue;
                }
                if (p.A == 255)
                    continue;

                float a = p.A / 255f;
                p.R = (byte)(p.R * a);
                p.G = (byte)(p.G * a);
                p.B = (byte)(p.B * a);
            }
        }

        // Encode PNG
        using var outMs = new MemoryStream();
        var encoder = new PngEncoder {
            ColorType = PngColorType.RgbWithAlpha,
            BitDepth = PngBitDepth.Bit8,
            TransparentColorMode = PngTransparentColorMode.Preserve
        };
        await img.SaveAsync(outMs, encoder);

        return outMs.ToArray();
    }
    private static bool WithinTolerance(Rgba32 a, Rgba32 b, int t)
    {
        return Math.Abs(a.R - b.R) <= t &&
               Math.Abs(a.G - b.G) <= t &&
               Math.Abs(a.B - b.B) <= t;
    }
}
