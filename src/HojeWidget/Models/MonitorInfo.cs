using System.Drawing;

namespace HojeWidget.Models;

/// <summary>
/// Snapshot de um monitor conectado, em pixels fisicos da area virtual.
/// <paramref name="Scale"/> e o fator de DPI do monitor (1.0 = 96 dpi).
/// </summary>
public sealed record MonitorInfo(
    string DeviceName,
    Rectangle Bounds,
    Rectangle WorkArea,
    double Scale,
    bool IsPrimary);
