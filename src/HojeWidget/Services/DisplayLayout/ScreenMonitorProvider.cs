using System.Drawing;
using HojeWidget.Interop;
using HojeWidget.Models;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace HojeWidget.Services.DisplayLayout;

/// <summary>Le os monitores reais do Windows, com DPI por monitor.</summary>
public sealed class ScreenMonitorProvider : IMonitorProvider
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();

        foreach (var screen in WinFormsScreen.AllScreens)
        {
            result.Add(new MonitorInfo(
                screen.DeviceName,
                screen.Bounds,
                screen.WorkingArea,
                GetScale(screen.Bounds),
                screen.Primary));
        }

        if (result.Count == 0)
        {
            // Defesa: sem monitores enumerados, assume um monitor virtual coerente.
            var fallback = new Rectangle(0, 0, 1920, 1080);
            result.Add(new MonitorInfo("DISPLAY-FALLBACK", fallback, fallback, 1.0, true));
        }

        return result;
    }

    private static double GetScale(Rectangle bounds)
    {
        try
        {
            var point = new NativeMethods.POINT
            {
                X = bounds.X + bounds.Width / 2,
                Y = bounds.Y + bounds.Height / 2,
            };

            var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero
                && NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0
                && dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (DllNotFoundException)
        {
            // shcore.dll ausente: assume escala neutra.
        }
        catch (EntryPointNotFoundException)
        {
            // API indisponivel nesta versao do Windows: assume escala neutra.
        }

        return 1.0;
    }
}
