using HojeWidget.Domain;

namespace HojeWidget.Services.System;

/// <summary>Relogio abstraido para permitir testar a transicao de dia.</summary>
public interface IClock
{
    DateTime UtcNow { get; }

    DateTime LocalNow { get; }

    /// <summary>Data civil local no formato YYYY-MM-DD.</summary>
    string LocalDate { get; }

    /// <summary>Data civil local.</summary>
    DateOnly Today { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => DateTime.Now;

    public string LocalDate => Domain.LocalDate.From(DateTime.Now);

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
