using System.Runtime.InteropServices;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Padallock;

public sealed class ArtworkRenderer
{
    public async Task<string> RenderAsync(MediaArtwork artwork, bool showTrackDetails, string outputDirectory)
    {
        var (width, height) = GetPrimaryDisplaySize();
        using var source = Image.Load<Rgba32>(artwork.Bytes);
        using var canvas = source.Clone(context => context
            .Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop
            })
            .GaussianBlur(32)
            .Brightness(0.55f));

        var foregroundWidth = Math.Min(width * 3 / 4, source.Width);
        var foregroundHeight = Math.Min(height * 3 / 4, source.Height);
        using var foreground = source.Clone(context => context.Resize(new ResizeOptions
        {
            Size = new Size(foregroundWidth, foregroundHeight),
            Mode = ResizeMode.Max
        }));

        var foregroundPosition = new Point(
            (width - foreground.Width) / 2,
            (height - foreground.Height) / 2);
        canvas.Mutate(context => context.DrawImage(foreground, foregroundPosition, 1f));

        if (showTrackDetails)
        {
            DrawTrackDetails(canvas, artwork, foregroundPosition, foreground.Height);
        }

        var outputPath = Path.Combine(outputDirectory, $"padallock-{Guid.NewGuid():N}.jpg");
        await using var output = File.Create(outputPath);
        await canvas.SaveAsJpegAsync(output, new JpegEncoder { Quality = 90 });
        return outputPath;
    }

    private static void DrawTrackDetails(Image<Rgba32> canvas, MediaArtwork artwork, Point artworkPosition, int artworkHeight)
    {
        var lines = new[] { artwork.Title, artwork.Artist }
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .ToArray();
        if (lines.Length == 0)
        {
            return;
        }

        var font = SystemFonts.CreateFont("Segoe UI", 28, FontStyle.Regular);
        var position = new PointF(artworkPosition.X, artworkPosition.Y + artworkHeight + 20);
        canvas.Mutate(context => context.DrawText(string.Join(Environment.NewLine, lines), font, Color.White, position));
    }

    private static (int Width, int Height) GetPrimaryDisplaySize() => (GetSystemMetrics(0), GetSystemMetrics(1));

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int systemMetric);
}
