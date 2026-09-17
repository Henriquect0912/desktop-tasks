using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HojeWidget.Models;

namespace HojeWidget.Services.DisplayLayout;

/// <summary>
/// Assinatura deterministica do conjunto de monitores conectados.
/// A mesma topologia produz sempre a mesma string, independentemente da ordem de enumeracao.
/// </summary>
public static class DisplaySignature
{
    public static string Compute(IEnumerable<MonitorInfo> monitors)
    {
        var canonical = Canonical(monitors);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Forma legivel usada como entrada do hash. Exposta para diagnostico e testes.</summary>
    public static string Canonical(IEnumerable<MonitorInfo> monitors)
    {
        var parts = monitors
            .OrderBy(m => m.DeviceName, StringComparer.Ordinal)
            .ThenBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .Select(Describe);

        return string.Join(";", parts);
    }

    private static string Describe(MonitorInfo m)
    {
        var c = CultureInfo.InvariantCulture;
        var bounds = $"{m.Bounds.X.ToString(c)},{m.Bounds.Y.ToString(c)},{m.Bounds.Width.ToString(c)},{m.Bounds.Height.ToString(c)}";
        var work = $"{m.WorkArea.X.ToString(c)},{m.WorkArea.Y.ToString(c)},{m.WorkArea.Width.ToString(c)},{m.WorkArea.Height.ToString(c)}";
        var scale = m.Scale.ToString("0.###", c);
        var role = m.IsPrimary ? "P" : "S";

        return string.Join("|", m.DeviceName, bounds, work, scale, role);
    }
}
