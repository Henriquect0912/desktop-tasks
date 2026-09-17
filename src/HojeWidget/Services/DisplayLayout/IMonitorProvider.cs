using HojeWidget.Models;

namespace HojeWidget.Services.DisplayLayout;

/// <summary>Fonte dos monitores conectados. Abstraida para permitir testes sem hardware.</summary>
public interface IMonitorProvider
{
    IReadOnlyList<MonitorInfo> GetMonitors();
}
