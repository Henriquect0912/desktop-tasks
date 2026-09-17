namespace HojeWidget.Models;

/// <summary>
/// Layout salvo por configuracao de monitores.
/// X/Y sao deslocamentos a partir da origem da area util do monitor de destino, em DIPs daquele monitor.
/// Guardar relativo ao monitor evita misturar pixels fisicos de telas com escalas diferentes.
/// </summary>
public sealed record StoredLayout(
    string DisplaySignature,
    string MonitorDevice,
    double XDip,
    double YDip,
    double WidthDip,
    double HeightDip,
    DateTime UpdatedAtUtc);
