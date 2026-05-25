using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace RamMonitor;

internal static class IconRenderer
{
    public enum Band { Green, Yellow, Red }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSMICON = 49;

    // Single number = current commit limit (GB).
    // Background color = limit band (vs baseline) — leading signal: pagefile growth.
    // Text color      = committed band (vs current limit) — concurrent signal: thrash proximity.
    public static Icon Render(string text, Band textBand, Band backgroundBand)
    {
        int size = GetSystemMetrics(SM_CXSMICON);
        if (size <= 0) size = 16;

        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.Clear(Color.Transparent);

            using var bg = new SolidBrush(BgColor(backgroundBand));
            g.FillRectangle(bg, 0, 0, size, size);

            using var fgBrush = new SolidBrush(FgColor(textBand, backgroundBand));

            // Largest font that fits the whole icon.
            float fontSize = size;
            Font? font = null;
            while (fontSize >= 6f)
            {
                font?.Dispose();
                font = new Font("Tahoma", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                var sz = g.MeasureString(text, font);
                if (sz.Width <= size && sz.Height <= size + 2) break;
                fontSize -= 0.5f;
            }
            font ??= new Font("Tahoma", 6f, FontStyle.Bold, GraphicsUnit.Pixel);

            var textSize = g.MeasureString(text, font);
            float x = (size - textSize.Width) / 2f;
            float y = (size - textSize.Height) / 2f;
            g.DrawString(text, font, fgBrush, x, y);
            font.Dispose();
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static Color BgColor(Band band) => band switch
    {
        Band.Green => Color.FromArgb(20, 110, 20),
        Band.Yellow => Color.FromArgb(220, 170, 0),
        Band.Red => Color.FromArgb(200, 40, 40),
        _ => Color.Gray,
    };

    // Text color encodes the committed band. Keep contrast against the background band.
    private static Color FgColor(Band textBand, Band bgBand)
    {
        // On yellow bg, white text is low-contrast — use darker shades.
        bool yellowBg = bgBand == Band.Yellow;
        return textBand switch
        {
            Band.Green => yellowBg ? Color.FromArgb(0, 80, 0) : Color.White,
            Band.Yellow => yellowBg ? Color.Black : Color.FromArgb(255, 230, 80),
            Band.Red => yellowBg ? Color.FromArgb(140, 0, 0) : Color.FromArgb(255, 120, 120),
            _ => Color.White,
        };
    }
}
