using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Services.Database;

namespace HojeWidget.Services;

/// <summary>
/// V0.7 — capacidade diária configurável.
///
/// A capacidade semanal define quanto tempo cada dia da semana costuma ter; uma
/// exceção por data sobrescreve apenas aquele dia. Zero significa "não planejar
/// automaticamente" — o dia continua aceitando o que o usuário puser à mão.
///
/// Implementa a mesma interface que a V0.5 já usava, então o distribuidor passa a
/// respeitar a configuração real sem mudar uma linha.
/// </summary>
public sealed class CapacityService : ICapacityProvider
{
    private readonly CapacityRepository _repository;

    private IReadOnlyDictionary<int, int>? _weekly;
    private Dictionary<DateOnly, int>? _overrides;

    public CapacityService(CapacityRepository repository) => _repository = repository;

    public int MinutesFor(DateOnly date)
    {
        EnsureLoaded();

        if (_overrides!.TryGetValue(date, out var exception))
        {
            return exception;
        }

        return _weekly!.TryGetValue(DayIndex(date), out var minutes)
            ? minutes
            : FixedCapacityProvider.DefaultDailyMinutes;
    }

    /// <summary>Minutos de cada dia da semana, de segunda a domingo.</summary>
    public IReadOnlyList<int> WeeklyMinutes()
    {
        EnsureLoaded();
        return Enumerable.Range(0, 7)
            .Select(i => _weekly!.TryGetValue(i, out var minutes) ? minutes : FixedCapacityProvider.DefaultDailyMinutes)
            .ToList();
    }

    public IReadOnlyList<(DateOnly Date, int Minutes)> Overrides()
    {
        EnsureLoaded();
        return _overrides!.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToList();
    }

    public void SetWeekly(int dayIndex, int minutes)
    {
        if (dayIndex is < 0 or > 6)
        {
            return;
        }

        _repository.SetWeekly(dayIndex, minutes);
        Invalidate();
    }

    public void SetOverride(DateOnly date, int minutes)
    {
        _repository.SetOverride(date, minutes);
        Invalidate();
    }

    public void RemoveOverride(DateOnly date)
    {
        _repository.RemoveOverride(date);
        Invalidate();
    }

    /// <summary>Capacidade somada de um intervalo fechado.</summary>
    public int TotalBetween(DateOnly from, DateOnly to)
    {
        var total = 0;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            total += MinutesFor(date);
        }

        return total;
    }

    public void Invalidate()
    {
        _weekly = null;
        _overrides = null;
    }

    private void EnsureLoaded()
    {
        _weekly ??= _repository.GetWeekly();
        _overrides ??= _repository.GetOverrides().ToDictionary(p => p.Date, p => p.Minutes);
    }

    /// <summary>Segunda = 0 … domingo = 6, a mesma convenção das rotinas.</summary>
    public static int DayIndex(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    /// <summary>Nome do dia da semana pelo índice interno.</summary>
    public static string DayName(int index) => index switch
    {
        0 => "SEG",
        1 => "TER",
        2 => "QUA",
        3 => "QUI",
        4 => "SEX",
        5 => "SÁB",
        _ => "DOM",
    };
}
