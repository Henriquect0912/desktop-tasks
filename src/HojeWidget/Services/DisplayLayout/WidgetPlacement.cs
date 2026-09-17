using System.Drawing;
using HojeWidget.Models;

namespace HojeWidget.Services.DisplayLayout;

/// <summary>Resultado de um posicionamento resolvido, em pixels fisicos da area virtual.</summary>
public sealed record PlacementResult(Rectangle Bounds, MonitorInfo Monitor);

/// <summary>
/// Converte layouts salvos em retangulos fisicos validos e vice-versa.
/// Funcoes puras: recebem a lista de monitores, nao consultam o Windows.
/// </summary>
public static class WidgetPlacement
{
    public const double DefaultWidthDip = 410;
    public const double DefaultHeightDip = 300;
    public const double MinWidthDip = 300;
    public const double MinHeightDip = 220;
    public const double DefaultMarginDip = 24;

    /// <summary>
    /// Decide onde o widget deve ficar. Nunca devolve um retangulo fora da area util
    /// de algum monitor conectado.
    /// </summary>
    public static PlacementResult Resolve(StoredLayout? layout, IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("E necessario pelo menos um monitor.", nameof(monitors));
        }

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];

        if (layout is null)
        {
            return Default(primary);
        }

        var monitor = monitors.FirstOrDefault(m => string.Equals(m.DeviceName, layout.MonitorDevice, StringComparison.Ordinal))
                      ?? primary;

        var scale = monitor.Scale <= 0 ? 1.0 : monitor.Scale;
        var work = monitor.WorkArea;

        var width = ClampLength(layout.WidthDip * scale, MinWidthDip * scale, work.Width);
        var height = ClampLength(layout.HeightDip * scale, MinHeightDip * scale, work.Height);

        var x = (int)Math.Round(work.X + layout.XDip * scale);
        var y = (int)Math.Round(work.Y + layout.YDip * scale);

        return Clamp(new Rectangle(x, y, width, height), monitors, primary);
    }

    /// <summary>Posicao inicial de fabrica: canto superior direito do monitor principal.</summary>
    public static PlacementResult Default(MonitorInfo monitor)
    {
        var scale = monitor.Scale <= 0 ? 1.0 : monitor.Scale;
        var work = monitor.WorkArea;

        var width = ClampLength(DefaultWidthDip * scale, MinWidthDip * scale, work.Width);
        var height = ClampLength(DefaultHeightDip * scale, MinHeightDip * scale, work.Height);
        var margin = (int)Math.Round(DefaultMarginDip * scale);

        var x = work.Right - width - margin;
        var y = work.Y + margin;

        var bounds = ClampToWorkArea(new Rectangle(x, y, width, height), work);
        return new PlacementResult(bounds, monitor);
    }

    /// <summary>
    /// Garante que o retangulo caiba inteiramente na area util de um monitor conectado.
    /// Escolhe o monitor com maior intersecao; se nao houver nenhuma, cai no fallback.
    /// </summary>
    public static PlacementResult Clamp(Rectangle candidate, IReadOnlyList<MonitorInfo> monitors, MonitorInfo fallback)
    {
        var target = PickMonitor(candidate, monitors, fallback);

        var scale = target.Scale <= 0 ? 1.0 : target.Scale;
        var work = target.WorkArea;

        var width = ClampLength(candidate.Width, MinWidthDip * scale, work.Width);
        var height = ClampLength(candidate.Height, MinHeightDip * scale, work.Height);

        var bounds = ClampToWorkArea(new Rectangle(candidate.X, candidate.Y, width, height), work);
        return new PlacementResult(bounds, target);
    }

    /// <summary>Converte um retangulo fisico atual em layout salvavel, relativo ao monitor que o contem.</summary>
    public static StoredLayout ToLayout(Rectangle bounds, IReadOnlyList<MonitorInfo> monitors, string signature, DateTime updatedAtUtc)
    {
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        var target = PickMonitor(bounds, monitors, primary);

        var scale = target.Scale <= 0 ? 1.0 : target.Scale;
        var work = target.WorkArea;

        return new StoredLayout(
            signature,
            target.DeviceName,
            (bounds.X - work.X) / scale,
            (bounds.Y - work.Y) / scale,
            bounds.Width / scale,
            bounds.Height / scale,
            updatedAtUtc);
    }

    private static MonitorInfo PickMonitor(Rectangle rect, IReadOnlyList<MonitorInfo> monitors, MonitorInfo fallback)
    {
        var target = fallback;
        var bestArea = 0L;

        foreach (var monitor in monitors)
        {
            var intersection = Rectangle.Intersect(rect, monitor.WorkArea);
            var area = (long)Math.Max(0, intersection.Width) * Math.Max(0, intersection.Height);
            if (area > bestArea)
            {
                bestArea = area;
                target = monitor;
            }
        }

        return target;
    }

    private static Rectangle ClampToWorkArea(Rectangle candidate, Rectangle work)
    {
        var x = candidate.X;
        var y = candidate.Y;

        if (x + candidate.Width > work.Right)
        {
            x = work.Right - candidate.Width;
        }

        if (x < work.X)
        {
            x = work.X;
        }

        if (y + candidate.Height > work.Bottom)
        {
            y = work.Bottom - candidate.Height;
        }

        if (y < work.Y)
        {
            y = work.Y;
        }

        return new Rectangle(x, y, candidate.Width, candidate.Height);
    }

    private static int ClampLength(double desired, double min, double max)
    {
        // Se o monitor for menor que o minimo, o limite do monitor vence:
        // nada pode ultrapassar a area util.
        var effectiveMin = Math.Min(min, max);
        var value = Math.Clamp(desired, effectiveMin, max);
        return (int)Math.Round(value);
    }
}
